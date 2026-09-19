using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Wires the Settings menu UI to the AudioManager's volume controls.
/// 
/// Sliders read their initial values from AudioManager on startup (which loads 
/// from PlayerPrefs), then write back to AudioManager whenever the player drags.
/// AudioManager handles saving to PlayerPrefs automatically.
/// 
/// SETUP:
/// 1. Attach this script to your SettingsPanel GameObject (or any UI element 
///    that's active when the settings menu is shown)
/// 2. Drag your SFX Slider into the SfxSlider field
/// 3. Drag your Music Slider into the MusicSlider field
/// 4. (Optional) Drag percentage text labels into SfxPercentText / MusicPercentText
/// </summary>
public class SettingsMenuUI : MonoBehaviour
{
    [Header("Volume Sliders")]
    [Tooltip("The slider that controls SFX volume. Range should be 0 to 1.")]
    public Slider SfxSlider;
    
    [Tooltip("The slider that controls music volume. Range should be 0 to 1.")]
    public Slider MusicSlider;
    
    [Header("Percentage Display (Optional)")]
    [Tooltip("TMP text that shows the SFX volume as a percentage. Leave null if you don't want a label.")]
    public TextMeshProUGUI SfxPercentText;
    
    [Tooltip("TMP text that shows the music volume as a percentage. Leave null if you don't want a label.")]
    public TextMeshProUGUI MusicPercentText;
    
    [Header("Feedback")]
    [Tooltip("Plays a short SFX sample when the SFX slider changes, so the player can " +
             "hear the result of their adjustment immediately.")]
    public string SfxPreviewSound = "UI_button_Click";
    
    [Tooltip("How long the player must stop dragging the SFX slider before another preview plays. " +
             "Prevents rapid-fire sound spam while sliding.")]
    public float PreviewDebounceTime = 0.1f;
    
    private float _lastSfxPreviewTime = 0f;

    void OnEnable()
    {
        // Wait until AudioManager is initialized (could be a frame or two)
        // then sync slider values to current saved volumes.
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[SettingsMenuUI] AudioManager.Instance is null. Sliders won't sync.");
            return;
        }
        
        InitializeSliders();
    }
    
    void Start()
    {
        // Also try in Start in case OnEnable ran before AudioManager was ready
        if (AudioManager.Instance != null)
        {
            InitializeSliders();
        }
    }
    
    /// <summary>
    /// Reads current volumes from AudioManager and sets the slider values 
    /// to match. Wires up the onValueChanged listeners.
    /// </summary>
    private void InitializeSliders()
    {
        if (SfxSlider != null)
        {
            // Set slider value WITHOUT triggering the listener (otherwise the act of 
            // initializing would re-save the same value, harmless but pointless).
            SfxSlider.SetValueWithoutNotify(AudioManager.Instance.GetSFXVolume());
            
            // Remove any existing listeners to prevent duplicates from re-enabling
            SfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            SfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            
            UpdateSfxPercentLabel(SfxSlider.value);
        }
        
        if (MusicSlider != null)
        {
            MusicSlider.SetValueWithoutNotify(AudioManager.Instance.GetMusicVolume());
            
            MusicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            MusicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            
            UpdateMusicPercentLabel(MusicSlider.value);
        }
    }
    
    /// <summary>
    /// Called whenever the SFX slider value changes (user dragging).
    /// Updates AudioManager (which saves to PlayerPrefs) and plays a 
    /// preview sound so the player can hear the result.
    /// </summary>
    private void OnSfxSliderChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
        
        UpdateSfxPercentLabel(value);
        
        // Preview the new volume by playing a brief sound.
        // Debounced so a fast drag doesn't trigger 50 sound plays.
        if (Time.unscaledTime - _lastSfxPreviewTime > PreviewDebounceTime)
        {
            _lastSfxPreviewTime = Time.unscaledTime;
            if (AudioManager.Instance != null && !string.IsNullOrEmpty(SfxPreviewSound))
            {
                AudioManager.Instance.PlaySFX(SfxPreviewSound);
            }
        }
    }
    
    /// <summary>
    /// Called whenever the music slider value changes. Updates the AudioManager 
    /// (which adjusts the currently playing track immediately and saves the preference).
    /// </summary>
    private void OnMusicSliderChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
        
        UpdateMusicPercentLabel(value);
    }
    
    private void UpdateSfxPercentLabel(float value)
    {
        if (SfxPercentText != null)
        {
            SfxPercentText.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
    
    private void UpdateMusicPercentLabel(float value)
    {
        if (MusicPercentText != null)
        {
            MusicPercentText.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
    
    void OnDisable()
    {
        // Clean up listeners when the panel hides to prevent leaks
        if (SfxSlider != null)
        {
            SfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
        }
        if (MusicSlider != null)
        {
            MusicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
        }
    }
}