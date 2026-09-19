using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pixel-perfect Canvas Scaler for pixel art UI.
/// 
/// Unity's built-in "Scale With Screen Size" uses fractional scale factors
/// which causes pixel art to scale unevenly (some pixels become 3px wide,
/// others 4px, creating a shimmering/blurry look).
///
/// This script uses INTEGER scaling only — pixels are always displayed as
/// exact multiples of their original size (2x, 3x, 4x, etc.), keeping
/// every pixel crisp and uniform.
///
/// SETUP:
/// 1. On your Canvas, set the Canvas Scaler to "Constant Pixel Size"
/// 2. Attach this script to the same Canvas
/// 3. Set ReferenceWidth/ReferenceHeight to your design resolution (e.g., 1920x1080)
/// 4. The script will calculate the largest integer scale that fits the screen
///
/// IMPORTANT: All UI sprites should be imported with:
///   - Filter Mode: Point (no filter)
///   - Compression: None
///   - Pixels Per Unit: matching your game's PPU
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class PixelPerfectCanvasScaler : MonoBehaviour
{
    [Header("Reference Resolution")]
    [Tooltip("The resolution your UI was designed for")]
    public int ReferenceWidth = 1920;
    public int ReferenceHeight = 1080;

    [Header("Scaling Options")]
    [Tooltip("If true, allows non-integer scaling as a fallback when the screen is smaller than the reference resolution")]
    public bool AllowFractionalFallback = true;
    
    [Tooltip("Minimum scale factor (prevents UI from becoming too small on tiny screens)")]
    public float MinScale = 1f;

    private CanvasScaler _scaler;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        UpdateScale();
    }

    void Update()
    {
        // Recalculate if screen size changes (e.g., window resize, orientation change)
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            UpdateScale();
        }
    }

    void UpdateScale()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        // Calculate the largest integer scale that fits both dimensions
        int scaleX = Mathf.FloorToInt((float)Screen.width / ReferenceWidth);
        int scaleY = Mathf.FloorToInt((float)Screen.height / ReferenceHeight);
        int integerScale = Mathf.Max(1, Mathf.Min(scaleX, scaleY));

        float finalScale = integerScale;

        // On screens smaller than the reference resolution, integer scale would be 0
        // Use fractional scaling as a fallback so UI isn't invisible
        if (AllowFractionalFallback && integerScale < 1)
        {
            float fractionalX = (float)Screen.width / ReferenceWidth;
            float fractionalY = (float)Screen.height / ReferenceHeight;
            finalScale = Mathf.Min(fractionalX, fractionalY);
        }

        finalScale = Mathf.Max(finalScale, MinScale);

        _scaler.scaleFactor = finalScale;
    }
}
