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
    public static bool HoldsPause => Instance != null && (Instance._pause || Instance._skipOpen);
    public static bool HoldWaves => CoreActive && (Progress.Stage != TutorialStage.Waves || Instance == null || !Instance._wavesStarted);
    public static bool AllowMovement => !Practice || Instance._step >= PracticeStep.Move;
    public static bool AllowJump => !Practice || Instance._step >= PracticeStep.Jump;
    public static bool AllowAim => !Practice || Instance._step >= PracticeStep.Aim;
    public static bool AllowShooting => !Practice || Instance._step == PracticeStep.Shoot || Instance._step == PracticeStep.Combat;
    public static bool AllowItems => !CoreActive;
    public static bool CanPlay => !MenuLocked || CoreActive && Progress.Stage == TutorialStage.Play;
    public static void RefreshProgress(PlayerData data) { if (Application.isPlaying) Progress = data.Tutorial; }

    MainMenuUI _menu;
    LocalPlayer _player;
    bool _practice, _protectedRun, _pause, _wavesStarted, _ready, _transitioning, _failed;
    float _promptElapsed, _readyElapsed;
    bool _continueReleased;
    static int _continueConsumedFrame = -1;
    RectTransform _canvas, _panel, _stick;
    TMP_Text _message, _hint;
    System.Action _next;
    TutorialGuidance _highlight;
    readonly Dictionary<UnityEngine.UI.Button, CanvasGroup> _gates = new Dictionary<UnityEngine.UI.Button, CanvasGroup>();
    readonly List<UnityEngine.UI.Button> _menuButtons = new List<UnityEngine.UI.Button>();
    readonly List<CardDisplay> _visibleCards = new List<CardDisplay>();
    string _lastPrompt;
    static readonly Color PromptColor = new Color(.025f, .04f, .065f, .76f);

    public static bool CapturesContinue => Instance != null && Instance._lastPrompt != null && Instance._next != null;
    public static bool BlocksMenuSubmit => CapturesContinue || SkipConfirmationOpen || _continueConsumedFrame == Time.frameCount;
    public static void ConsumeContinueInput() => _continueConsumedFrame = Time.frameCount;
    static UnityEngine.InputSystem.Gamepad ContinuePad
    {
        get
        {
            if (Instance != null && Instance._player != null) return Instance._player.UsesGamepad ? Instance._player.Controller : null;
            foreach (var pad in UnityEngine.InputSystem.Gamepad.all) if (pad.enabled) return pad;
            return null;
        }
    }
    public static bool ContinueHeld => ControllerBindings.Held(ContinuePad, "Confirm") || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.touchCount > 0;
    public static bool ContinuePressed
    {
        get
        {
            if (SkipConfirmationOpen) return false;
            if (ControllerBindings.Pressed(ContinuePad, "Confirm") || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return true;
            for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            return false;
        }
    }
    public static string ContinueHint => ContinuePad != null ? "Press " + ControllerBindings.Label("Confirm") + " to continue" :
        MobileInputController.Instance != null && MobileInputController.Instance.IsMobileEnabled ? "Tap to continue" : "Press Enter to continue";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        Instance = null;
        _continueConsumedFrame = -1;
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
        BuildDialogue();
        BuildTutorialControls();
        _highlight = new TutorialGuidance();
        _panel.gameObject.SetActive(false); // Only an active lesson's Prompt should show it.
    }
    void Prompt(string message, System.Action next = null, bool ready = true)
    {
        EnsureUI();
        if (_lastPrompt == message) return;
        _lastPrompt = message; _next = next; _ready = ready;
        SetPromptText(message, true);
        _promptElapsed = _readyElapsed = 0;
        _continueReleased = false;
        _panel.gameObject.SetActive(true);
        _panel.GetComponent<UnityEngine.UI.Image>().color = PromptColor;
    }
    void HidePrompt() { _lastPrompt = null; if (_panel != null) _panel.gameObject.SetActive(false); if (_portrait != null) _portrait.gameObject.SetActive(false); _next = null; }
    void Advance()
    {
        if (!ContinuePressed) return;
        ContinueDialogue();
    }
    void ContinueDialogue()
    {
        if (_skipOpen) return;
        if (_collapsed && _next != null && _continueReleased) { ExpandDialogue(); ConsumeContinueInput(); _continueReleased = false; return; }
        if (!_ready || _readyElapsed < .1f || _promptElapsed < .35f || !_continueReleased || _next == null || _continueConsumedFrame == Time.frameCount) return;
        _continueConsumedFrame = Time.frameCount;
        if (!DialogueRevealed)
        {
            RevealDialogue(); _continueReleased = false;
            return;
        }
        // Consume before invoking: callbacks may replace the prompt or change scenes.
        var action = _next; _next = null; action();
    }
    void Update()
    {
        if (_transitioning) return;
        CheckBlockedPointer();
        UpdateTutorialControls();
        if (_skipOpen) { UpdateSkipConfirmation(); return; }
        if (_menu != null) UpdateMenu();
        else if (_practice && _player != null) UpdatePractice();
        else if (_protectedRun && WaveManager.Instance != null && WaveManager.Instance.HasWaveError)
            Failure("This wave could not start. Return to the menu to retry the stage.");
        if (_panel == null || _lastPrompt == null) return;
        bool covered = !_failed && (EnemyTipUI.Instance != null && EnemyTipUI.Instance.IsShowing || LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering && (!_practice || _step != PracticeStep.Cards));
        bool disconnected = LocalCoopSession.Instance != null && LocalCoopSession.Instance.DevicesMissing;
        if (!covered && !disconnected)
        {
            // Release-before-press prevents the action that finished an objective
            // (or opened a prompt) from also dismissing it.
            float elapsed = Mathf.Min(Time.unscaledDeltaTime, .1f);
            _promptElapsed += elapsed;
            _readyElapsed = _ready ? _readyElapsed + elapsed : 0;
            if (_promptElapsed >= .35f && !ContinueHeld) _continueReleased = true;
            UpdateDialogue(elapsed);
            Advance();
            if (_transitioning || _panel == null || !_panel.gameObject.activeSelf) return;
        }
        UpdateDialogueControls(covered || disconnected);
    }
    void LateUpdate()
    {
        if (_transitioning) return;
        if ((_pause || _skipOpen) && (LocalCoopSession.Instance == null || !LocalCoopSession.Instance.GameOver)) Time.timeScale = 0;
        if (_menu != null && !_skipOpen) GateMenu();
        if (_practice) UpdatePracticeHUD();
        LayoutDialogue();
        UpdateRejectedFeedback();
    }
    void Pause(bool paused)
    {
        _pause = paused;
        Time.timeScale = paused || _skipOpen || LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering || EnemyTipUI.Instance != null && EnemyTipUI.Instance.IsShowing ||
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
        RestoreRejectedFeedback();
        _highlight?.Dispose(); ClearPractice();
        if (Instance == this) { Instance = null; if (_pause) Time.timeScale = 1; }
    }
}
