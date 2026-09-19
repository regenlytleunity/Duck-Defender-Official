using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any UI Button to play a sound when it's clicked.
/// Automatically wires into the Button's onClick event at runtime.
/// 
/// Just add this component to any Button GameObject and it'll work — 
/// no Inspector setup needed (uses the default click sound).
/// 
/// USAGE:
///   1. Select a Button in your scene/prefab
///   2. Add Component → ButtonClickSound
///   3. Done. Click sound plays automatically on every button press.
/// 
/// To use a different sound for a specific button, change SoundName in the Inspector.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    [Tooltip("Which sound to play when the button is clicked. " +
             "Default is UI_button_Click but you can override per-button " +
             "(e.g., Open_Shop_Bell for the shop button).")]
    public string SoundName = "UI_button_Click";

    private Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(PlayClickSound);
    }
    
    void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(PlayClickSound);
        }
    }

    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundName);
        }
    }
}