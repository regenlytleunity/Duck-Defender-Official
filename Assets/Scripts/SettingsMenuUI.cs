using UnityEngine;
using TMPro;

public class SettingsMenuUI : MonoBehaviour
{
    [Header("Tutorials and Effects")]
    public UnityEngine.UI.Toggle ShowTipsToggle;
    public UnityEngine.UI.Toggle ParticlesToggle;
    [Header("Volume Sliders")]
    public UnityEngine.UI.Slider MasterSlider, SfxSlider, MusicSlider;
    [Header("Percentage Display")]
    public TextMeshProUGUI MasterPercentText, SfxPercentText, MusicPercentText;
    [Header("Feedback")]
    public string SfxPreviewSound = "UI_button_Click";
    public float PreviewDebounceTime = .1f;
    float _lastSfxPreviewTime;
    [Header("Keybinds")]
    public GameObject KeybindPanel;
    public GameObject KeyCapturePanel;
    public TextMeshProUGUI KeyCaptureText;
    public TextMeshProUGUI[] KeybindLabels;
    public TextMeshProUGUI KeybindStatus;
    public static readonly string[] BindingActions = { "moveleft", "moveright", "jump", "crouch", "dash", "shoot", "moveleftalt", "moverightalt", "jumpalt", "crouchalt" };
    public static readonly string[] BindingNames = { "Move left", "Move right", "Jump / fly up", "Crouch / fly down", "Dash / blink", "Shoot", "Left (alternate)", "Right (alternate)", "Jump (alternate)", "Crouch (alternate)" };
    static readonly KeyCode[] Keys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));
    int _bindingIndex = -1;
    float _captureAfter;
    [Header("Reset Confirmation")]
    public GameObject ResetConfirmationPanel;

    void OnEnable() { Initialize(); }
    void Start() { Initialize(); }
    void Initialize()
    {
        if (ShowTipsToggle != null)
        {
            ShowTipsToggle.SetIsOnWithoutNotify(SaveSystem.LoadData().ShowTips);
            ShowTipsToggle.onValueChanged.RemoveListener(SetShowTips);
            ShowTipsToggle.onValueChanged.AddListener(SetShowTips);
        }
        if (ParticlesToggle != null)
        {
            ParticlesToggle.SetIsOnWithoutNotify(ParticleVisibility.Enabled);
            ParticlesToggle.onValueChanged.RemoveListener(ParticleVisibility.SetEnabled);
            ParticlesToggle.onValueChanged.AddListener(ParticleVisibility.SetEnabled);
        }
        var audio = AudioManager.Instance;
        if (audio == null) return;
        BindSlider(MasterSlider, audio.GetMasterVolume(), OnMasterSliderChanged);
        BindSlider(SfxSlider, audio.GetSFXVolume(), OnSfxSliderChanged);
        BindSlider(MusicSlider, audio.GetMusicVolume(), OnMusicSliderChanged);
        Percent(MasterPercentText, audio.GetMasterVolume());
        Percent(SfxPercentText, audio.GetSFXVolume());
        Percent(MusicPercentText, audio.GetMusicVolume());
    }
    static void BindSlider(UnityEngine.UI.Slider slider, float value, UnityEngine.Events.UnityAction<float> changed)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.RemoveListener(changed);
        slider.onValueChanged.AddListener(changed);
    }
    static void Percent(TextMeshProUGUI label, float value) { if (label != null) label.text = Mathf.RoundToInt(value * 100) + "%"; }
    public void SetShowTips(bool enabled)
    {
        var data = SaveSystem.LoadData();
        data.ShowTips = enabled;
        SaveSystem.SaveData(data, true);
    }
    void OnMasterSliderChanged(float value) { AudioManager.Instance?.SetMasterVolume(value); Percent(MasterPercentText, value); }
    void OnMusicSliderChanged(float value) { AudioManager.Instance?.SetMusicVolume(value); Percent(MusicPercentText, value); }
    void OnSfxSliderChanged(float value)
    {
        AudioManager.Instance?.SetSFXVolume(value);
        Percent(SfxPercentText, value);
        if (Time.unscaledTime - _lastSfxPreviewTime > PreviewDebounceTime)
        {
            _lastSfxPreviewTime = Time.unscaledTime;
            if (!string.IsNullOrEmpty(SfxPreviewSound)) AudioManager.Instance?.PlaySFX(SfxPreviewSound);
        }
    }
    public void OpenKeybinds()
    {
        if (KeybindPanel == null) return;
        KeybindPanel.SetActive(true);
        RefreshModalInput();
        FocusFirstButton(KeybindPanel);
        RefreshKeybinds();
        if (KeybindStatus != null) KeybindStatus.text = "Select a control to change it. Escape cancels binding.";
    }
    public void CloseKeybinds()
    {
        CancelBinding();
        if (KeybindPanel != null) KeybindPanel.SetActive(false);
        RefreshModalInput();
        FocusFirstButton(gameObject);
    }
    public void BeginBinding(int index)
    {
        if (index < 0 || index >= BindingActions.Length || InputManager.Instance == null) return;
        _bindingIndex = index;
        _captureAfter = Time.unscaledTime + .2f;
        if (KeyCapturePanel != null) KeyCapturePanel.SetActive(true);
        RefreshModalInput();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        if (KeyCaptureText != null) KeyCaptureText.text = BindingNames[index] + "\nPress a key or mouse button\nEscape to cancel";
    }
    void Update()
    {
        if (_bindingIndex < 0 || Time.unscaledTime < _captureAfter) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { CancelBinding(); return; }
        if (!Input.anyKeyDown) return;
        foreach (var key in Keys)
        {
            if (key == KeyCode.None || (int)key >= (int)KeyCode.JoystickButton0 || !Input.GetKeyDown(key)) continue;
            TryAssignBinding(_bindingIndex, key);
            CancelBinding();
            break;
        }
    }
    public bool TryAssignBinding(int index, KeyCode key)
    {
        var input = InputManager.Instance;
        if (input == null || index < 0 || index >= BindingActions.Length || key == KeyCode.None || key == KeyCode.Escape) return false;
        for (int i = 0; i < BindingActions.Length; i++)
            if (i != index && input.GetKeybind(BindingActions[i]) == key)
            {
                if (KeybindStatus != null) KeybindStatus.text = key + " is already used by " + BindingNames[i] + ".";
                return false;
            }
        input.SetKeybind(BindingActions[index], key);
        RefreshKeybinds();
        if (KeybindStatus != null) KeybindStatus.text = BindingNames[index] + " saved.";
        return true;
    }
    void CancelBinding()
    {
        _bindingIndex = -1;
        if (KeyCapturePanel != null) KeyCapturePanel.SetActive(false);
        RefreshModalInput();
        if (KeybindPanel != null && KeybindPanel.activeInHierarchy) FocusFirstButton(KeybindPanel);
    }
    void RefreshModalInput()
    {
        bool keys = KeybindPanel != null && KeybindPanel.activeSelf;
        bool reset = ResetConfirmationPanel != null && ResetConfirmationPanel.activeSelf;
        var settingsGroup = GetComponent<CanvasGroup>();
        if (settingsGroup != null) settingsGroup.interactable = !keys && !reset;
        var keysGroup = KeybindPanel != null ? KeybindPanel.GetComponent<CanvasGroup>() : null;
        if (keysGroup != null) keysGroup.interactable = _bindingIndex < 0 && !reset;
    }
    static void FocusFirstButton(GameObject panel)
    {
        if (panel == null || !panel.activeInHierarchy || UnityEngine.EventSystems.EventSystem.current == null) return;
        foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
            if (button.IsInteractable())
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
                return;
            }
    }
    public void RestoreKeybinds() { InputManager.Instance?.ResetToDefaults(); RefreshKeybinds(); if (KeybindStatus != null) KeybindStatus.text = "Default controls restored."; }
    void RefreshKeybinds()
    {
        if (KeybindLabels == null || InputManager.Instance == null) return;
        for (int i = 0; i < Mathf.Min(KeybindLabels.Length, BindingActions.Length); i++)
            if (KeybindLabels[i] != null) KeybindLabels[i].text = InputManager.Instance.GetKeybind(BindingActions[i]).ToString();
    }
    public void ShowResetConfirmation()
    {
        if (ResetConfirmationPanel != null) ResetConfirmationPanel.SetActive(true);
        RefreshModalInput();
        FocusFirstButton(ResetConfirmationPanel);
    }
    public void CancelReset()
    {
        if (ResetConfirmationPanel != null) ResetConfirmationPanel.SetActive(false);
        RefreshModalInput();
        FocusFirstButton(gameObject);
    }
    public void ConfirmReset()
    {
        if (ResetConfirmationPanel == null || !ResetConfirmationPanel.activeSelf || ShopManager.Instance == null) return;
        ShopManager.Instance.ResetProgress();
        CancelReset();
        Initialize();
    }
    void OnDisable()
    {
        CloseKeybinds();
        CancelReset();
        if (ShowTipsToggle != null) ShowTipsToggle.onValueChanged.RemoveListener(SetShowTips);
        if (ParticlesToggle != null) ParticlesToggle.onValueChanged.RemoveListener(ParticleVisibility.SetEnabled);
        if (MasterSlider != null) MasterSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
        if (SfxSlider != null) SfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
        if (MusicSlider != null) MusicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
    }
}
