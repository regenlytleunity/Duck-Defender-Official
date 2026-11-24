using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class TutorialDirector
{
    UnityEngine.UI.Button _showTutorialButton, _skipButton, _keepTutorialButton, _confirmSkipButton;
    RectTransform _skipModal;
    GameObject _beforeSkipSelection;
    bool _skipOpen, _skipReleased;
    float _skipOpenedAt, _skipMoveAt, _tutorialHeld;
    public static bool SkipConfirmationOpen => Instance != null && Instance._skipOpen;
    string TutorialBindingLabel => ContinuePad != null ? ControllerBindings.Label("Tutorial") :
        (InputManager.Instance != null ? InputManager.Instance.TutorialKey : KeyCode.T).ToString();

    void BuildTutorialControls()
    {
        // Keep the corner actions reachable above the enemy-tip click blocker.
        var controls = CoopUIElements.Canvas("Tutorial controls", transform, 1100);
        _showTutorialButton = CoopUIElements.Button(controls, "Show tutorial", new Vector2(.82f, .085f), new Vector2(.985f, .14f), ExpandDialogue);
        _skipButton = CoopUIElements.Button(controls, "Skip tutorial", new Vector2(.82f, .02f), new Vector2(.985f, .075f), ShowSkipConfirmation);
        foreach (var button in new[] { _showTutorialButton, _skipButton })
        {
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontSizeMax = 22; label.fontSizeMin = 16; label.alignment = TextAlignmentOptions.Midline;
        }
        _skipModal = CoopUIElements.Canvas("Skip tutorial confirmation", transform, 1500);
        CoopUIElements.Panel(_skipModal, "Modal dimmer", Vector2.zero, Vector2.one, new Color(0, 0, 0, .65f));
        var box = CoopUIElements.Panel(_skipModal, "Skip tutorial dialog", new Vector2(.22f, .32f), new Vector2(.78f, .68f), new Color(.035f, .055f, .09f, .98f));
        CoopUIElements.Text(box, "Are you sure you want to skip the tutorial?", new Vector2(.06f, .57f), new Vector2(.94f, .94f), 34);
        CoopUIElements.Text(box, "Return to the main menu. Keep the coins and cards you already earned.", new Vector2(.06f, .31f), new Vector2(.94f, .56f), 25);
        _keepTutorialButton = CoopUIElements.Button(box, "Keep playing", new Vector2(.06f, .07f), new Vector2(.47f, .25f), CancelSkip);
        _confirmSkipButton = CoopUIElements.Button(box, "Skip tutorial", new Vector2(.53f, .07f), new Vector2(.94f, .25f), ConfirmSkip);
        _skipModal.gameObject.SetActive(false);
    }
    void UpdateTutorialControls()
    {
        if (_skipButton == null) return;
        bool active = CoreActive || Progress != null && Progress.ItemActive;
        _skipButton.gameObject.SetActive(active);
        _showTutorialButton.gameObject.SetActive((active || CompletionPrompt) && _lastPrompt != null);
        _showTutorialButton.GetComponentInChildren<TMP_Text>().text = "Show tutorial [" + TutorialBindingLabel + "]";
        _skipButton.GetComponentInChildren<TMP_Text>().text = ContinuePad != null ?
            "Skip tutorial [" + ControllerBindings.Label("SkipTutorial") + "]" : "Skip tutorial\nHold " + TutorialBindingLabel;
        if ((!active && !CompletionPrompt) || _skipOpen) { _tutorialHeld = 0; return; }
        var key = InputManager.Instance != null ? InputManager.Instance.TutorialKey : KeyCode.T;
        bool pressed = Input.GetKeyDown(key) || ControllerBindings.Pressed(ContinuePad, "Tutorial");
        bool held = Input.GetKey(key);
        if (active && ControllerBindings.Pressed(ContinuePad, "SkipTutorial")) { ShowSkipConfirmation(); return; }
        if (pressed) ExpandDialogue();
        _tutorialHeld = held ? _tutorialHeld + Mathf.Min(Time.unscaledDeltaTime, .1f) : 0;
        if (active && _tutorialHeld >= .85f) { _tutorialHeld = 0; ShowSkipConfirmation(); }
    }
    void ShowSkipConfirmation()
    {
        if (_skipOpen || _transitioning || !(CoreActive || Progress != null && Progress.ItemActive)) return;
        _skipOpen = true; _skipReleased = false; _skipOpenedAt = Time.unscaledTime;
        _beforeSkipSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        _skipModal.gameObject.SetActive(true);
        ControllerMenuNavigation.ConfigureNavigation(_skipModal.gameObject);
        EventSystem.current?.SetSelectedGameObject(_keepTutorialButton.gameObject);
        ConsumeContinueInput();
        Time.timeScale = 0;
    }
    void UpdateSkipConfirmation()
    {
        if (!ContinueHeld) _skipReleased = true;
        var pad = ContinuePad;
        if (Input.GetKeyDown(KeyCode.Escape) || ControllerBindings.Pressed(pad, "Back")) { CancelSkip(); return; }
        var events = EventSystem.current;
        if (events == null) return;
        if (events.currentSelectedGameObject != _keepTutorialButton.gameObject && events.currentSelectedGameObject != _confirmSkipButton.gameObject)
            events.SetSelectedGameObject(_keepTutorialButton.gameObject);
        Vector2 move = ControllerBindings.MenuMove(pad);
        if (move.sqrMagnitude < .2f) _skipMoveAt = 0;
        else if (Time.unscaledTime >= _skipMoveAt)
        {
            _skipMoveAt = Time.unscaledTime + .25f;
            events.SetSelectedGameObject((move.x > .3f || move.y < -.3f ? _confirmSkipButton : _keepTutorialButton).gameObject);
        }
        if (_skipReleased && Time.unscaledTime - _skipOpenedAt > .35f && ControllerBindings.Pressed(pad, "Confirm"))
        {
            if (events.currentSelectedGameObject == _confirmSkipButton.gameObject) ConfirmSkip(); else CancelSkip();
        }
    }
    void CancelSkip()
    {
        if (!_skipOpen) return;
        _skipOpen = false; _skipModal.gameObject.SetActive(false);
        ConsumeContinueInput(); _continueReleased = false;
        if (_beforeSkipSelection != null && _beforeSkipSelection.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(_beforeSkipSelection);
        else EventSystem.current?.SetSelectedGameObject(null);
        Pause(_pause);
    }
    void ConfirmSkip()
    {
        if (!_skipOpen || !_skipReleased || Time.unscaledTime - _skipOpenedAt < .35f || _transitioning) return;
        ConsumeContinueInput();
        LevelManager.Instance?.FlushCoinSave();
        var data = ShopManager.Instance != null ? ShopManager.Instance.Progress : SaveSystem.LoadData();
        data.Tutorial.Stage = TutorialStage.Complete;
        data.Tutorial.Items = ItemTutorialStage.Complete;
        SaveSystem.SaveData(data, updateTutorial: true);
        _skipOpen = false; HidePrompt();
        ReturnToMenu();
    }
}
