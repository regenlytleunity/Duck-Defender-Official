using UnityEngine;

/// <summary>
/// Adjusts a RectTransform to fit inside the device's safe area.
/// Prevents UI from being hidden behind the iPhone notch, rounded corners,
/// or Android navigation bars.
///
/// SETUP:
/// 1. Create an empty GameObject as the FIRST CHILD of your Canvas
/// 2. Set its RectTransform to stretch-all (anchor min 0,0 max 1,1, offsets all 0)
/// 3. Attach this script to it
/// 4. Make all your UI elements children of this object instead of the Canvas directly
///
/// On devices without notches/cutouts, the safe area matches the full screen
/// and this script does nothing.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaPanel : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _lastSafeArea;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        // Recheck in case of orientation change
        if (_lastSafeArea != Screen.safeArea)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;

        // Convert safe area from screen pixels to anchor values (0-1)
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
    }
}
