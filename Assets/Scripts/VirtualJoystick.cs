using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A virtual joystick UI component. Listens for touch/click on its background,
/// moves a handle within a radius, and exposes the normalized direction vector.
/// 
/// Each joystick is finger-isolated — it captures one touch on press and ignores 
/// other touches until that touch releases. This lets two joysticks work 
/// independently without multi-touch conflicts.
/// 
/// SETUP:
/// 1. Create a UI Image that will be the joystick's BACKGROUND (the larger circle)
/// 2. Create a UI Image as a child of the background — this is the HANDLE (smaller circle)
/// 3. Attach this script to the BACKGROUND
/// 4. Drag the HANDLE GameObject's RectTransform into the Handle field
/// 5. Configure HandleRange (how far the handle can travel from center)
/// 6. Reference the joystick from MobileInputController
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("References")]
    [Tooltip("The handle that moves within the joystick background")]
    public RectTransform Handle;
    
    [Header("Configuration")]
    [Tooltip("How far the handle can travel from center, in pixels")]
    public float HandleRange = 100f;
    
    [Tooltip("Dead zone (0-1) - input below this magnitude is treated as zero. Prevents jitter.")]
    [Range(0f, 0.5f)]
    public float DeadZone = 0.1f;
    
    [Tooltip("If true, the joystick smoothly returns the handle to center when released. " +
             "If false, the handle snaps to center instantly.")]
    public bool SmoothReturn = true;
    
    [Tooltip("Speed of smooth return when SmoothReturn is enabled")]
    public float ReturnSpeed = 15f;

    // Public state - read by MobileInputController
    public Vector2 Direction { get; private set; } = Vector2.zero;
    public bool IsActive { get; private set; } = false;
    public int ActiveFingerId { get; private set; } = -1;
    
    private RectTransform _backgroundRect;
    private Canvas _canvas;
    private Vector2 _centerPosition;
    
    void Awake()
    {
        _backgroundRect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        
        if (Handle == null)
        {
            Debug.LogError($"[VirtualJoystick] '{gameObject.name}' has no Handle assigned!");
        }
    }
    
    void Start()
    {
        // Center the handle at startup
        if (Handle != null)
        {
            Handle.anchoredPosition = Vector2.zero;
        }
    }
    
    void Update()
    {
        // Smooth return to center when not active
        if (!IsActive && SmoothReturn && Handle != null)
        {
            Handle.anchoredPosition = Vector2.MoveTowards(
                Handle.anchoredPosition, 
                Vector2.zero, 
                HandleRange * ReturnSpeed * Time.deltaTime
            );
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        IsActive = true;
        ActiveFingerId = eventData.pointerId;
        UpdateHandlePosition(eventData);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        // Only respond to drags from our active finger
        if (eventData.pointerId != ActiveFingerId) return;
        UpdateHandlePosition(eventData);
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        // Only release on the finger that started us
        if (eventData.pointerId != ActiveFingerId) return;
        
        IsActive = false;
        ActiveFingerId = -1;
        Direction = Vector2.zero;
        
        if (!SmoothReturn && Handle != null)
        {
            Handle.anchoredPosition = Vector2.zero;
        }
    }
    
    private void UpdateHandlePosition(PointerEventData eventData)
    {
        if (Handle == null) return;
        
        // Convert touch position to local space within the joystick background
        Vector2 localPoint;
        Camera cam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera 
            ? _canvas.worldCamera 
            : null;
            
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _backgroundRect, 
            eventData.position, 
            cam, 
            out localPoint))
        {
            return;
        }
        
        // Clamp to circle of radius HandleRange
        Vector2 clamped = Vector2.ClampMagnitude(localPoint, HandleRange);
        Handle.anchoredPosition = clamped;
        
        // Calculate normalized direction (0 to 1 magnitude)
        Vector2 rawDirection = clamped / HandleRange;
        
        // Apply dead zone
        if (rawDirection.magnitude < DeadZone)
        {
            Direction = Vector2.zero;
        }
        else
        {
            // Rescale so magnitude goes from 0 (at deadzone edge) to 1 (at max)
            float scaledMagnitude = (rawDirection.magnitude - DeadZone) / (1f - DeadZone);
            Direction = rawDirection.normalized * scaledMagnitude;
        }
    }
    
    /// <summary>
    /// Forcibly resets the joystick state. Useful when scenes change or the player dies.
    /// </summary>
    public void ResetJoystick()
    {
        IsActive = false;
        ActiveFingerId = -1;
        Direction = Vector2.zero;
        if (Handle != null) Handle.anchoredPosition = Vector2.zero;
    }
}