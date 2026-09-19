using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Reads input from two virtual joysticks and exposes it through the same interface
/// MobileInputManager used to. InputHelper.cs queries this controller transparently.
/// 
/// LEFT JOYSTICK (movement):
///   - Drag left/right for horizontal movement
///   - Drag up past JumpThreshold to jump
///   - Double-tap-drag in any direction within DashTimeWindow to dash that direction
/// 
/// RIGHT JOYSTICK (aim/shoot):
///   - Direction sets aim angle
///   - Holding the joystick at all = shooting
///   - Releasing the joystick stops shooting
///   - Slight off-axis angles snap to pure horizontal/vertical for easier targeting
/// </summary>
public class MobileInputController : MonoBehaviour
{
    public static MobileInputController Instance;

    [Header("Mobile Detection")]
    public bool IsMobileEnabled = false;
    public bool AutoDetectPlatform = true;

    [Header("Canvas Settings")]
    public string CanvasName = "MobileControlsCanvas";
    
    [Header("Joystick References")]
    public VirtualJoystick MoveJoystick;
    public VirtualJoystick AimJoystick;
    
    [Header("Movement Tuning")]
    [Range(0.1f, 1f)]
    public float JumpThreshold = 0.6f;
    public float JumpRetriggerDelay = 0.3f;
    
    [Header("Dash Tuning (Double-Drag)")]
    public float DashTimeWindow = 0.4f;
    [Range(0.3f, 1f)]
    public float DashMagnitudeThreshold = 0.7f;
    [Range(0.5f, 1f)]
    public float DashDirectionAlignment = 0.7f;
    
    [Header("Aim Snap Tuning")]
    [Tooltip("Aim angles within this many degrees of pure horizontal will snap to horizontal. " +
             "Helps players fire straight shots without precise stick control. " +
             "0 = no snap, 10-15 = comfortable default, 30+ = aggressive (limits diagonal aiming).")]
    [Range(0f, 45f)]
    public float HorizontalSnapTolerance = 12f;
    
    [Tooltip("Same as Horizontal snap, but for vertical aim. Helps with shots straight up/down.")]
    [Range(0f, 45f)]
    public float VerticalSnapTolerance = 12f;
    
    [Tooltip("If true, applies snap correction to aim. Disable for raw stick aim.")]
    public bool EnableAimSnap = true;
    
    // Virtual input state
    private float _virtualHorizontal = 0f;
    private bool _virtualJumpDown = false;
    private bool _virtualJumpHeld = false;
    private bool _virtualDashDown = false;
    private bool _virtualShootHeld = false;
    private Vector3 _virtualAimWorldDirection;
    
    // Jump state tracking
    private bool _jumpInputActive = false;
    private float _nextJumpAllowedTime = 0f;
    
    // Dash state tracking
    private float _lastDragReleaseTime = -999f;
    private Vector2 _lastDragReleaseDirection = Vector2.zero;
    private bool _wasMoveJoystickActive = false;
    
    // Track which frame each one-shot input was set
    private int _jumpDownFrame = -1;
    private int _dashDownFrame = -1;
    
    private Camera _mainCam;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (AutoDetectPlatform)
        {
            #if UNITY_IOS || UNITY_ANDROID
                IsMobileEnabled = true;
            #else
                IsMobileEnabled = Application.isMobilePlatform;
            #endif
        }
        
        _mainCam = Camera.main;
        SceneManager.sceneLoaded += OnSceneLoaded;
        FindAndSetupCanvas();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetAllInput();
        FindAndSetupCanvas();
        _mainCam = Camera.main;
    }

    void FindAndSetupCanvas()
    {
        GameObject mobileCanvas = null;
        
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == CanvasName)
            {
                mobileCanvas = root;
                break;
            }
            
            Transform found = root.transform.Find(CanvasName);
            if (found != null)
            {
                mobileCanvas = found.gameObject;
                break;
            }
        }

        if (mobileCanvas != null)
        {
            mobileCanvas.SetActive(IsMobileEnabled);
            
            if (MoveJoystick == null || AimJoystick == null)
            {
                VirtualJoystick[] joysticks = mobileCanvas.GetComponentsInChildren<VirtualJoystick>(true);
                foreach (var joy in joysticks)
                {
                    if (joy.gameObject.name.ToLower().Contains("move") || joy.gameObject.name.ToLower().Contains("left"))
                    {
                        MoveJoystick = joy;
                    }
                    else if (joy.gameObject.name.ToLower().Contains("aim") || joy.gameObject.name.ToLower().Contains("right"))
                    {
                        AimJoystick = joy;
                    }
                }
            }
        }
    }

    void ResetAllInput()
    {
        _virtualHorizontal = 0f;
        _virtualJumpDown = false;
        _virtualJumpHeld = false;
        _virtualDashDown = false;
        _virtualShootHeld = false;
        _jumpInputActive = false;
        _wasMoveJoystickActive = false;
        _lastDragReleaseTime = -999f;
        _lastDragReleaseDirection = Vector2.zero;
        _nextJumpAllowedTime = 0f;
        _jumpDownFrame = -1;
        _dashDownFrame = -1;
        
        if (MoveJoystick != null) MoveJoystick.ResetJoystick();
        if (AimJoystick != null) AimJoystick.ResetJoystick();
    }

    void Update()
    {
        if (!IsMobileEnabled) return;
        
        if (_jumpDownFrame >= 0 && _jumpDownFrame < Time.frameCount)
        {
            _virtualJumpDown = false;
            _jumpDownFrame = -1;
        }
        if (_dashDownFrame >= 0 && _dashDownFrame < Time.frameCount)
        {
            _virtualDashDown = false;
            _dashDownFrame = -1;
        }
        
        ProcessMoveJoystick();
        ProcessAimJoystick();
    }

    void ProcessMoveJoystick()
    {
        if (MoveJoystick == null)
        {
            _virtualHorizontal = 0f;
            return;
        }
        
        Vector2 dir = MoveJoystick.Direction;
        bool isActive = MoveJoystick.IsActive;
        
        _virtualHorizontal = dir.x;
        
        bool jumpInputThisFrame = isActive && dir.y >= JumpThreshold;
        
        if (jumpInputThisFrame && !_jumpInputActive && Time.time >= _nextJumpAllowedTime)
        {
            _virtualJumpDown = true;
            _jumpDownFrame = Time.frameCount;
            _nextJumpAllowedTime = Time.time + JumpRetriggerDelay;
        }
        
        _jumpInputActive = jumpInputThisFrame;
        _virtualJumpHeld = jumpInputThisFrame;
        
        if (!_wasMoveJoystickActive && isActive)
        {
            if (Time.time - _lastDragReleaseTime <= DashTimeWindow && 
                _lastDragReleaseDirection.magnitude >= DashMagnitudeThreshold &&
                dir.magnitude >= DashMagnitudeThreshold)
            {
                float alignment = Vector2.Dot(dir.normalized, _lastDragReleaseDirection.normalized);
                if (alignment >= DashDirectionAlignment)
                {
                    _virtualDashDown = true;
                    _dashDownFrame = Time.frameCount;
                    _lastDragReleaseTime = -999f;
                    _lastDragReleaseDirection = Vector2.zero;
                }
            }
        }
        
        if (_wasMoveJoystickActive && !isActive)
        {
            _lastDragReleaseTime = Time.time;
        }
        
        if (isActive && dir.magnitude >= DashMagnitudeThreshold)
        {
            _lastDragReleaseDirection = dir;
        }
        
        _wasMoveJoystickActive = isActive;
    }

    void ProcessAimJoystick()
    {
        if (AimJoystick == null)
        {
            _virtualShootHeld = false;
            return;
        }
        
        Vector2 dir = AimJoystick.Direction;
        bool isActive = AimJoystick.IsActive;
        
        if (isActive && dir.magnitude > 0.05f)
        {
            _virtualShootHeld = true;
            
            // Apply aim snap correction to ease targeting along horizontal/vertical axes
            Vector2 snappedDir = EnableAimSnap ? ApplyAimSnap(dir) : dir;
            
            if (_mainCam == null) _mainCam = Camera.main;
            
            if (_mainCam != null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    Vector3 aimWorldPoint = playerObj.transform.position + (Vector3)(snappedDir.normalized * 5f);
                    _virtualAimWorldDirection = aimWorldPoint;
                }
            }
        }
        else
        {
            _virtualShootHeld = false;
        }
    }
    
    /// <summary>
    /// Snaps aim direction to perfect horizontal or vertical when the input is 
    /// close to those axes. This makes it easier for the player to shoot straight 
    /// without needing pixel-perfect stick control.
    /// 
    /// Logic: convert direction to an angle, check if it's within tolerance of 
    /// 0/90/180/270 degrees, and if so, snap to that axis.
    /// </summary>
    Vector2 ApplyAimSnap(Vector2 rawDir)
    {
        if (rawDir.sqrMagnitude < 0.001f) return rawDir;
        
        // Calculate the angle of the input direction in degrees.
        // Atan2 returns -180 to 180; we normalize to 0-360 for easier reasoning.
        float angle = Mathf.Atan2(rawDir.y, rawDir.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        
        // Check proximity to each cardinal direction (0/90/180/270 degrees)
        // and snap if within tolerance.
        //   0   = right (positive X)
        //   90  = up (positive Y)
        //   180 = left (negative X)
        //   270 = down (negative Y)
        
        // Right (0 degrees) - also need to check 360 since 359 is near 0
        if (angle <= HorizontalSnapTolerance || angle >= 360f - HorizontalSnapTolerance)
        {
            return new Vector2(rawDir.magnitude, 0f);
        }
        
        // Left (180 degrees)
        if (Mathf.Abs(angle - 180f) <= HorizontalSnapTolerance)
        {
            return new Vector2(-rawDir.magnitude, 0f);
        }
        
        // Up (90 degrees)
        if (Mathf.Abs(angle - 90f) <= VerticalSnapTolerance)
        {
            return new Vector2(0f, rawDir.magnitude);
        }
        
        // Down (270 degrees)
        if (Mathf.Abs(angle - 270f) <= VerticalSnapTolerance)
        {
            return new Vector2(0f, -rawDir.magnitude);
        }
        
        // Not in any snap zone - return raw direction unchanged
        return rawDir;
    }

    public float GetHorizontal() => _virtualHorizontal;
    public bool GetJumpDown() => _virtualJumpDown;
    public bool GetJumpHeld() => _virtualJumpHeld;
    public bool GetDashDown() => _virtualDashDown;
    public bool GetShootHeld() => _virtualShootHeld;
    
    public Vector3 GetMousePosition()
    {
        if (!_virtualShootHeld || _mainCam == null) return Input.mousePosition;
        Vector3 screenPoint = _mainCam.WorldToScreenPoint(_virtualAimWorldDirection);
        return screenPoint;
    }
    
    public float GetVertical()
    {
        if (MoveJoystick == null) return 0f;
        return MoveJoystick.Direction.y;
    }
}