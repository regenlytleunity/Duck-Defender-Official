using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;

/// <summary>
/// Toggles fullscreen mode using the browser's native Fullscreen API.
/// 
/// On mobile browsers, Unity's Screen.fullScreen doesn't reliably hide
/// the address bar. This script calls the JavaScript Fullscreen API directly
/// on the document element, which gives the browser a stronger signal
/// to enter true fullscreen.
///
/// NOTE: Mobile browsers require fullscreen to be triggered by a USER GESTURE
/// (a tap/click). This is why it's on a button — you cannot auto-fullscreen on load.
///
/// SETUP:
/// 1. Create a UI Button, attach this script
/// 2. For WebGL builds, this uses JavaScript interop
/// 3. For standalone PC builds, falls back to Screen.fullScreen
/// 4. Auto-hides on native iOS/Android apps (already fullscreen)
/// </summary>
public class FullscreenToggle : MonoBehaviour
{
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void JSEnterFullscreen();
    
    [DllImport("__Internal")]
    private static extern void JSExitFullscreen();
    
    [DllImport("__Internal")]
    private static extern bool JSIsFullscreen();
    #endif

    [Header("References")]
    public Button ToggleButton;

    [Header("Settings")]
    public bool HideOnNativeMobile = true;

    void Start()
    {
        if (ToggleButton == null)
            ToggleButton = GetComponent<Button>();

        if (ToggleButton != null)
            ToggleButton.onClick.AddListener(Toggle);

        #if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !UNITY_WEBGL
            if (HideOnNativeMobile)
                gameObject.SetActive(false);
        #endif
    }

    public void Toggle()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
            if (JSIsFullscreen())
                JSExitFullscreen();
            else
                JSEnterFullscreen();
        #else
            Screen.fullScreen = !Screen.fullScreen;
        #endif
    }
}
