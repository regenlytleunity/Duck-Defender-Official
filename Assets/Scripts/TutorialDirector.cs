using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-local coordinator. Existing owners still perform input, combat, purchases and saves.
[DefaultExecutionOrder(500)]
public partial class TutorialDirector : MonoBehaviour
{
    public static TutorialDirector Instance { get; private set; }
    public static TutorialProgress Progress { get; private set; }
    public static bool CoreActive => Progress != null && Progress.CoreActive;
    static bool CompletionPrompt => Instance != null && Instance._menu != null && Instance._next != null && !CoreActive;
    public static bool MenuLocked => CoreActive || Progress != null && Progress.ItemActive || CompletionPrompt;
    public static bool Practice => Instance != null && Instance._practice;
    public static bool ProtectedRun => Instance != null && Instance._protectedRun;
    public static bool HoldsPause => Instance != null && Instance._pause;
    public static bool HoldWaves => CoreActive && (Progress.Stage != TutorialStage.Waves || Instance == null || !Instance._wavesStarted);
    public static bool AllowMovement => !Practice || Instance._step == PracticeStep.Move || Instance._step == PracticeStep.Jump || Instance._step == PracticeStep.Combat;
    public static bool AllowJump => !Practice || Instance._step == PracticeStep.Jump || Instance._step == PracticeStep.Combat;
    public static bool AllowAim => !Practice || Instance._step >= PracticeStep.Aim;
    public static bool AllowShooting => !Practice || Instance._step == PracticeStep.Shoot || Instance._step == PracticeStep.Combat;
    public static bool AllowItems => !CoreActive;
    public static bool CanPlay => !MenuLocked || CoreActive && Progress.Stage == TutorialStage.Play;
    public static void RefreshProgress(PlayerData data) { if (Application.isPlaying) Progress = data.Tutorial; }

    MainMenuUI _menu;
    LocalPlayer _player;
    bool _practice, _protectedRun, _pause, _wavesStarted, _ready, _transitioning, _failed;
    float _promptElapsed, _readyElapsed, _readDelay;
    RectTransform _canvas, _panel, _stick;
    TMP_Text _message, _hint;
    UnityEngine.UI.Button _restartStage;
    System.Action _next;
    ControllerSelectionFrame _highlight;
    readonly Dictionary<UnityEngine.UI.Button, CanvasGroup> _gates = new Dictionary<UnityEngine.UI.Button, CanvasGroup>();
    readonly List<UnityEngine.UI.Button> _menuButtons = new List<UnityEngine.UI.Button>();
    readonly List<CardDisplay> _visibleCards = new List<CardDisplay>();
    string _lastPrompt;
    static readonly Color PromptColor = new Color(.035f, .055f, .09f, .97f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        Instance = null;
        Progress = null; // The scene callback runs after verification/save bootstrap methods.
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }
    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu" && scene.name != "SampleScene") return;
        if (Instance != null) Destroy(Instance.gameObject);
        Progress = SaveSystem.LoadData().Tutorial;
        new GameObject("Tutorial").AddComponent<TutorialDirector>();
    }
    void Awake()
    {
        Instance = this;
        _practice = CoreActive && Progress.Stage == TutorialStage.Basics && SceneManager.GetActiveScene().name == "SampleScene";
        _protectedRun = CoreActive && SceneManager.GetActiveScene().name == "SampleScene";
    }
    IEnumerator Start()
    {
        yield return null; // All scene owners and runtime menu extensions initialize first.
        yield return null; // Player loadouts initialize on the next frame.
        _menu = MainMenuUI.Instance;
        if (_menu != null)
        {
            Time.timeScale = 1;
            if (CoreActive && Progress.Stage == TutorialStage.Basics) { StartGame(); yield break; }
            if (CoreActive && Progress.Stage == TutorialStage.Waves) SaveStage(TutorialStage.Play);
            EnsureUI();
            if (CoreActive && Progress.Stage == TutorialStage.PackPurchased) _menu.ResumeTutorialPack();
            UpdateMenu();
        }
        else
        {
            _player = LocalCoopSession.Instance != null && LocalCoopSession.Instance.Players.Count > 0 ? LocalCoopSession.Instance.Players[0] : null;
            if (_protectedRun && _player == null) { Failure("The tutorial player is missing. Return to the menu and retry."); yield break; }
            if (_practice) { EnsureUI(); _origin = _player.transform.position; BeginPractice(PracticeStep.Welcome); }
            else if (CoreActive && (Progress.Stage == TutorialStage.Play || Progress.Stage == TutorialStage.Waves))
            {
                SaveStage(TutorialStage.Waves); EnsureUI();
                Prompt("Defeat enemies to level up and earn coins.", () => { _wavesStarted = true; Pause(false); HidePrompt(); });
                Pause(true);
            }
            else if (CoreActive) ReturnToMenu();
        }
    }
    void EnsureUI()
    {
        if (_canvas != null) return;
        _canvas = CoopUIElements.Canvas("Tutorial prompts", transform, 950);
        _panel = CoopUIElements.Panel(_canvas, "Tutorial prompt", new Vector2(.25f, .81f), new Vector2(.75f, .985f), PromptColor);
        _panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _message = CoopUIElements.Text(_panel, "", new Vector2(.10f, .25f), new Vector2(.97f, .94f), 30);
        _hint = CoopUIElements.Text(_panel, "", new Vector2(.10f, .03f), new Vector2(.97f, .23f), 18);
        _restartStage = CoopUIElements.Button(_canvas, "Restart stage", new Vector2(.85f, .02f), new Vector2(.98f, .075f), RestartWaves);
        _restartStage.gameObject.SetActive(false);
        var circle = CoopUIElements.Panel(_panel, "Stick illustration", new Vector2(.015f, .42f), new Vector2(.085f, .88f), new Color(.2f, .28f, .4f));
        circle.GetComponent<UnityEngine.UI.Image>().sprite = ItemArea.Circle;
        circle.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _stick = CoopUIElements.Panel(circle, "Stick", new Vector2(.3f, .3f), new Vector2(.7f, .7f), Color.white);
        _stick.GetComponent<UnityEngine.UI.Image>().sprite = ItemArea.Circle;
        _stick.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _highlight = new ControllerSelectionFrame("Tutorial highlight");
    }
    void Prompt(string message, System.Action next = null, bool ready = true)
    {
        EnsureUI();
        if (_lastPrompt == message) return;
        _lastPrompt = message; _message.text = message; _next = next; _ready = ready;
        _promptElapsed = _readyElapsed = 0;
        _readDelay = Mathf.Clamp(message.Length * .045f, 2.5f, 8);
        _panel.gameObject.SetActive(true);
        _panel.GetComponent<UnityEngine.UI.Image>().color = PromptColor;
    }
    void HidePrompt() { _lastPrompt = null; if (_panel != null) _panel.gameObject.SetActive(false); _next = null; }
    void Advance()
    {
        if (!_ready || _readyElapsed < .6f || _promptElapsed < _readDelay || _next == null) return;
        // Consume before invoking: callbacks may replace the prompt or change scenes.
        var action = _next; _next = null; action();
    }
    void Update()
    {
        if (_transitioning) return;
        if (_menu != null) UpdateMenu();
        else if (_practice && _player != null) UpdatePractice();
        else if (_protectedRun && WaveManager.Instance != null && WaveManager.Instance.HasWaveError)
            Failure("This wave could not start. Return to the menu to retry the stage.");
        if (_panel == null || !_panel.gameObject.activeSelf) return;
        bool covered = !_failed && (EnemyTipUI.Instance != null && EnemyTipUI.Instance.IsShowing || LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering);
        bool disconnected = LocalCoopSession.Instance != null && LocalCoopSession.Instance.DevicesMissing;
        if (!covered && !disconnected)
        {
            // Unscaled time also advances paused introductions/endings. Clamp long frames
            // so returning from a suspended app cannot consume an entire reading delay.
            float elapsed = Mathf.Min(Time.unscaledDeltaTime, .1f);
            _promptElapsed += elapsed;
            _readyElapsed = _ready ? _readyElapsed + elapsed : 0;
            Advance();
            if (_transitioning || _panel == null || !_panel.gameObject.activeSelf) return;
        }
        _hint.text = _next == null || !_ready ? "Follow the highlighted objective" : "Continuing automatically...";
        if (_stick != null) _stick.anchoredPosition = _step == PracticeStep.Jump ? new Vector2(0, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3)) * 22) : new Vector2(Mathf.Sin(Time.unscaledTime * 3) * 22, 0);
        if (_stick != null) _stick.parent.gameObject.SetActive(_practice && _step >= PracticeStep.Move && _step <= PracticeStep.Shoot);
        if (_lastPrompt != null && _lastPrompt.StartsWith("TUTORIAL COMPLETE"))
            _panel.GetComponent<UnityEngine.UI.Image>().color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * .2f, 1), .6f, .3f);
    }
    void LateUpdate()
    {
        if (_transitioning) return;
        if (_pause && (LocalCoopSession.Instance == null || !LocalCoopSession.Instance.GameOver)) Time.timeScale = 0;
        if (_menu != null) GateMenu();
        if (_practice) UpdatePracticeHUD();
        if (_restartStage != null) _restartStage.gameObject.SetActive(_protectedRun && !_practice && _wavesStarted && Progress.Stage == TutorialStage.Waves &&
            (LevelUpUI.Instance == null || !LevelUpUI.Instance.IsOffering) && (EnemyTipUI.Instance == null || !EnemyTipUI.Instance.IsShowing));
        if (_panel != null)
        {
            // Use the empty areas in the existing layouts, keeping prices, cards,
            // pagination and modal controls unobscured.
            Vector2 min = new Vector2(.25f, .81f), max = new Vector2(.75f, .985f);
            if (_menu != null)
            {
                if (_menu.OpeningOverlay.activeSelf || _menu.ConfirmPanel.activeSelf)
                { min.y = .015f; max.y = .155f; }
                else if (_menu.IndexPanel.activeSelf)
                { min = new Vector2(.58f, .79f); max = new Vector2(.98f, .985f); }
                else if (_menu.ShopPanel.activeSelf)
                { min.y = .565f; max.y = .705f; }
            }
            else if (LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering)
            { min.y = .85f; max.y = .995f; }
            if (_panel.anchorMin != min || _panel.anchorMax != max) CoopUIElements.Stretch(_panel, min, max);
        }
    }
    void Pause(bool paused)
    {
        _pause = paused;
        Time.timeScale = paused || LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering || EnemyTipUI.Instance != null && EnemyTipUI.Instance.IsShowing ||
            LocalCoopSession.Instance != null && (LocalCoopSession.Instance.DevicesMissing || LocalCoopSession.Instance.GameOver) ? 0 : 1;
    }
    public static void SaveStage(TutorialStage stage)
    {
        var data = ShopManager.Instance != null ? ShopManager.Instance.Progress : SaveSystem.LoadData();
        data.Tutorial.Stage = stage; SaveSystem.SaveData(data, updateTutorial: true);
    }
    void SaveItems(ItemTutorialStage stage)
    {
        var data = ShopManager.Instance.Progress;
        data.Tutorial.Items = stage; SaveSystem.SaveData(data, updateTutorial: true);
    }
    void StartGame()
    {
        if (_transitioning) return;
        _transitioning = true; LocalCoopSession.RequestedPlayers = 1;
        Time.timeScale = 1;
        var controller = FindFirstObjectByType<MainMenuController>();
        SceneManager.LoadScene(controller != null ? controller.GameSceneName : "SampleScene");
    }
    void ReturnToMenu()
    {
        if (_transitioning) return;
        _transitioning = true; LevelManager.Instance?.FlushCoinSave(); _pause = false; Time.timeScale = 1;
        SceneManager.LoadScene("MainMenu");
    }
    void RestartWaves()
    {
        if (!_protectedRun || _practice || Progress.Stage != TutorialStage.Waves || _transitioning) return;
        _transitioning = true; LevelManager.Instance?.FlushCoinSave(); _pause = false; Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    void Failure(string message) { _failed = true; Prompt(message, ReturnToMenu); Pause(true); }
    public static bool CompleteWave(int wave)
    {
        if (Instance == null || !Instance._protectedRun || wave < 5 || Progress.Stage != TutorialStage.Waves) return false;
        LevelManager.Instance?.FlushCoinSave();
        var data = SaveSystem.LoadData();
        TutorialProgress.GrantWaveReward(data);
        SaveSystem.SaveData(data, updateTutorial: true);
        if (LevelManager.Instance != null) LevelManager.Instance.TotalCoins = data.TotalCoins;
        Instance.Prompt("Stage 2 complete! Let's spend those coins at the shop.", Instance.ReturnToMenu);
        Instance.Pause(true);
        return true;
    }
    void OnDestroy()
    {
        foreach (var pair in _gates) if (pair.Value != null) Destroy(pair.Value);
        _highlight?.Dispose(); ClearPractice();
        if (Instance == this) { Instance = null; if (_pause) Time.timeScale = 1; }
    }
}
