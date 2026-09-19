using UnityEngine;

/// <summary>
/// Handles all player animation states.
/// Attach to the Player GameObject (same object with Animator component).
/// 
/// QOL: Animation speed scales to 1/2 of attack cooldown.
/// Death animation overrides all other animations.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    public static PlayerAnimator Instance { get; private set; }
    
    private Animator _anim;
    private Rigidbody2D _rb;
    private PlayerController _controller;
    private WeaponPlayer _weapon;
    
    private bool _isDead = false;

    // Cache parameter hashes for performance
    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int IsDashing = Animator.StringToHash("isDashing");
    private static readonly int VelocityY = Animator.StringToHash("velocityY");
    private static readonly int ShootTrigger = Animator.StringToHash("Shoot");
    private static readonly int ShootSpeed = Animator.StringToHash("ShootSpeed");
    private static readonly int DeathTrigger = Animator.StringToHash("Death");
    private static readonly int IsDead = Animator.StringToHash("isDead");

    [Header("Animation Settings")]
    [Tooltip("The base duration of your shoot animation clip in seconds")]
    public float BaseShootAnimationDuration = 0.5f;
    
    [Header("Death Animation")]
    [Tooltip("Name of the death animation state (for crossfade)")]
    public string DeathStateName = "Death";
    [Tooltip("Crossfade duration into death animation")]
    public float DeathCrossfadeDuration = 0.1f;

    void Awake()
    {
        Instance = this;
        
        _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _controller = GetComponent<PlayerController>();
        _weapon = GetComponent<WeaponPlayer>();
    }

    void Update()
    {
        // Don't update any animations if dead
        if (_isDead) return;
        
        UpdateMovementAnimations();
        CheckShootInput();
    }

    void UpdateMovementAnimations()
    {
        // Horizontal movement
        float speed = Mathf.Abs(_rb.linearVelocity.x);
        _anim.SetBool(IsMoving, speed > 0.1f);

        // Vertical velocity (for jump/fall blend trees)
        _anim.SetFloat(VelocityY, _rb.linearVelocity.y);

        // Grounded state
        if (_controller != null && _controller.GroundCheck != null)
        {
            bool grounded = Physics2D.OverlapCircle(
                _controller.GroundCheck.position, 
                _controller.GroundCheckRadius, 
                _controller.GroundLayer
            );
            _anim.SetBool(IsGrounded, grounded);
        }
    }

    void CheckShootInput()
    {
        // Trigger animation on click (not hold)
        if (Input.GetButtonDown("Fire1"))
        {
            TriggerShoot();
        }
    }

    /// <summary>
    /// Plays the shoot animation scaled to half the weapon's fire rate.
    /// </summary>
    public void TriggerShoot()
    {
        if (_isDead) return;
        
        float targetDuration = 0.25f;
        
        if (_weapon != null && _weapon.FireRate > 0)
        {
            targetDuration = _weapon.FireRate / 2f;
        }

        targetDuration = Mathf.Max(targetDuration, 0.05f);
        float speedMultiplier = BaseShootAnimationDuration / targetDuration;
        speedMultiplier = Mathf.Clamp(speedMultiplier, 0.5f, 4f);
        
        _anim.SetFloat(ShootSpeed, speedMultiplier);
        _anim.SetTrigger(ShootTrigger);
    }

    /// <summary>
    /// Plays the death animation, overriding any current animation.
    /// Call this from PlayerHealth.Die()
    /// </summary>
    public void TriggerDeath()
    {
        if (_isDead) return;
        _isDead = true;

        // Reset any active triggers that might interfere
        _anim.ResetTrigger(ShootTrigger);
        
        // Set the death bool (for state machine transitions)
        _anim.SetBool(IsDead, true);
        
        // Force crossfade to death state - this overrides EVERYTHING
        // Layer 0 is the base layer, crossfade ignores current state
        _anim.CrossFade(DeathStateName, DeathCrossfadeDuration, 0, 0f);
        
        // Also trigger in case using trigger-based transitions
        _anim.SetTrigger(DeathTrigger);
    }
}