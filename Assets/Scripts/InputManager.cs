using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Centralized input management with rebindable keys.
/// Handles all player input and allows keybind customization.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Movement Keys")]
    public KeyCode MoveLeftKey = KeyCode.A;
    public KeyCode MoveLeftAlt = KeyCode.LeftArrow;
    public KeyCode MoveRightKey = KeyCode.D;
    public KeyCode MoveRightAlt = KeyCode.RightArrow;
    public KeyCode JumpKey = KeyCode.Space;
    public KeyCode JumpAlt = KeyCode.W;
    public KeyCode CrouchKey = KeyCode.S;
    public KeyCode CrouchAlt = KeyCode.DownArrow;

    [Header("Action Keys")]
    public KeyCode DashKey = KeyCode.LeftShift;
    public KeyCode ShootKey = KeyCode.Mouse0;
    
    [Header("UI Keys")]
    public KeyCode PauseKey = KeyCode.Escape;

    // Events for input
    public event Action OnDashPressed;
    public event Action OnJumpPressed;
    public event Action OnShootPressed;
    public event Action OnShootReleased;

    // Input state
    private bool _isShootHeld = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadKeybinds();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // Dash - single key press
        if (Input.GetKeyDown(DashKey))
        {
            OnDashPressed?.Invoke();
        }

        // Jump
        if (Input.GetKeyDown(JumpKey) || Input.GetKeyDown(JumpAlt))
        {
            OnJumpPressed?.Invoke();
        }

        // Shoot (hold)
        if (Input.GetKey(ShootKey))
        {
            if (!_isShootHeld)
            {
                _isShootHeld = true;
                OnShootPressed?.Invoke();
            }
        }
        else if (_isShootHeld)
        {
            _isShootHeld = false;
            OnShootReleased?.Invoke();
        }
    }

    // --- INPUT QUERIES ---
    
    public float GetHorizontalInput()
    {
        float input = 0f;
        if (Input.GetKey(MoveLeftKey) || Input.GetKey(MoveLeftAlt)) input -= 1f;
        if (Input.GetKey(MoveRightKey) || Input.GetKey(MoveRightAlt)) input += 1f;
        return input;
    }

    public float GetVerticalInput()
    {
        float input = 0f;
        if (Input.GetKey(CrouchKey) || Input.GetKey(CrouchAlt)) input -= 1f;
        if (Input.GetKey(JumpKey) || Input.GetKey(JumpAlt)) input += 1f;
        return input;
    }

    public bool IsJumpPressed()
    {
        return Input.GetKeyDown(JumpKey) || Input.GetKeyDown(JumpAlt);
    }

    public bool IsJumpHeld()
    {
        return Input.GetKey(JumpKey) || Input.GetKey(JumpAlt);
    }

    public bool IsDashPressed()
    {
        return Input.GetKeyDown(DashKey);
    }

    public bool IsShootHeld()
    {
        return Input.GetKey(ShootKey);
    }

    public bool IsCrouchHeld()
    {
        return Input.GetKey(CrouchKey) || Input.GetKey(CrouchAlt);
    }

    // --- KEYBIND MANAGEMENT ---

    public void SetKeybind(string action, KeyCode newKey)
    {
        switch (action.ToLower())
        {
            case "moveleft": MoveLeftKey = newKey; break;
            case "moveright": MoveRightKey = newKey; break;
            case "jump": JumpKey = newKey; break;
            case "crouch": CrouchKey = newKey; break;
            case "dash": DashKey = newKey; break;
            case "shoot": ShootKey = newKey; break;
            case "pause": PauseKey = newKey; break;
        }
        SaveKeybinds();
    }

    public KeyCode GetKeybind(string action)
    {
        switch (action.ToLower())
        {
            case "moveleft": return MoveLeftKey;
            case "moveright": return MoveRightKey;
            case "jump": return JumpKey;
            case "crouch": return CrouchKey;
            case "dash": return DashKey;
            case "shoot": return ShootKey;
            case "pause": return PauseKey;
            default: return KeyCode.None;
        }
    }

    public void SaveKeybinds()
    {
        PlayerPrefs.SetInt("Key_MoveLeft", (int)MoveLeftKey);
        PlayerPrefs.SetInt("Key_MoveRight", (int)MoveRightKey);
        PlayerPrefs.SetInt("Key_Jump", (int)JumpKey);
        PlayerPrefs.SetInt("Key_Crouch", (int)CrouchKey);
        PlayerPrefs.SetInt("Key_Dash", (int)DashKey);
        PlayerPrefs.SetInt("Key_Shoot", (int)ShootKey);
        PlayerPrefs.SetInt("Key_Pause", (int)PauseKey);
        PlayerPrefs.Save();
    }

    public void LoadKeybinds()
    {
        if (PlayerPrefs.HasKey("Key_MoveLeft"))
            MoveLeftKey = (KeyCode)PlayerPrefs.GetInt("Key_MoveLeft");
        if (PlayerPrefs.HasKey("Key_MoveRight"))
            MoveRightKey = (KeyCode)PlayerPrefs.GetInt("Key_MoveRight");
        if (PlayerPrefs.HasKey("Key_Jump"))
            JumpKey = (KeyCode)PlayerPrefs.GetInt("Key_Jump");
        if (PlayerPrefs.HasKey("Key_Crouch"))
            CrouchKey = (KeyCode)PlayerPrefs.GetInt("Key_Crouch");
        if (PlayerPrefs.HasKey("Key_Dash"))
            DashKey = (KeyCode)PlayerPrefs.GetInt("Key_Dash");
        if (PlayerPrefs.HasKey("Key_Shoot"))
            ShootKey = (KeyCode)PlayerPrefs.GetInt("Key_Shoot");
        if (PlayerPrefs.HasKey("Key_Pause"))
            PauseKey = (KeyCode)PlayerPrefs.GetInt("Key_Pause");
    }

    public void ResetToDefaults()
    {
        MoveLeftKey = KeyCode.A;
        MoveLeftAlt = KeyCode.LeftArrow;
        MoveRightKey = KeyCode.D;
        MoveRightAlt = KeyCode.RightArrow;
        JumpKey = KeyCode.Space;
        JumpAlt = KeyCode.W;
        CrouchKey = KeyCode.S;
        CrouchAlt = KeyCode.DownArrow;
        DashKey = KeyCode.LeftShift;
        ShootKey = KeyCode.Mouse0;
        PauseKey = KeyCode.Escape;
        SaveKeybinds();
    }
}