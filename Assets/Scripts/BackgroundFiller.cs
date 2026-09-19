using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures a background UI Image always fills the entire screen,
/// regardless of device aspect ratio. Scales up and crops the excess
/// rather than showing empty space.
///
/// This is the "cover" approach (like CSS background-size: cover) —
/// the image always fills the screen, and any overflow is cropped.
///
/// SETUP:
/// 1. Your background Image should be a child of the Canvas (or panel)
/// 2. Set the Image's RectTransform to stretch-all (anchor min 0,0 max 1,1)
/// 3. Attach this script to the background Image GameObject
/// 4. Set the source sprite's native width/height in the Inspector
///    (or it reads from the sprite automatically)
///
/// The background will ALWAYS fill the screen. On wider screens,
/// the top/bottom get cropped. On taller screens, the sides get cropped.
/// The image is always centered.
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(RectTransform))]
public class BackgroundFiller : MonoBehaviour
{
    [Header("Source Image Size")]
    [Tooltip("Leave at 0 to auto-detect from the sprite. Set manually if sprite is atlas'd.")]
    public float NativeWidth = 0;
    public float NativeHeight = 0;

    private Image _image;
    private RectTransform _rectTransform;
    private RectTransform _parentRect;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    void Awake()
    {
        _image = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();
        _parentRect = transform.parent as RectTransform;
    }

    void Start()
    {
        // Auto-detect native size from sprite
        if ((NativeWidth <= 0 || NativeHeight <= 0) && _image.sprite != null)
        {
            NativeWidth = _image.sprite.rect.width;
            NativeHeight = _image.sprite.rect.height;
        }

        UpdateFill();
    }

    void Update()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            UpdateFill();
        }
    }

    void UpdateFill()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        if (NativeWidth <= 0 || NativeHeight <= 0 || _parentRect == null) return;

        // Get the parent's size (the area we need to fill)
        float parentWidth = _parentRect.rect.width;
        float parentHeight = _parentRect.rect.height;

        if (parentWidth <= 0 || parentHeight <= 0) return;

        float imageAspect = NativeWidth / NativeHeight;
        float screenAspect = parentWidth / parentHeight;

        // Reset anchors to center
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition = Vector2.zero;

        if (screenAspect > imageAspect)
        {
            // Screen is wider than image — match width, crop top/bottom
            float width = parentWidth;
            float height = width / imageAspect;
            _rectTransform.sizeDelta = new Vector2(width, height);
        }
        else
        {
            // Screen is taller than image — match height, crop sides
            float height = parentHeight;
            float width = height * imageAspect;
            _rectTransform.sizeDelta = new Vector2(width, height);
        }
    }
}