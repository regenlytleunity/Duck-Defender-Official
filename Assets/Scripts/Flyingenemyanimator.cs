using UnityEngine;

/// <summary>
/// Handles animation states for flying/shooting enemies (Buzzer, etc.).
/// 
/// QOL: Animation speed scales to 1/2 of attack cooldown.
/// FIX: Only triggers animation when BuzzerEnemy actually fires (cooldown ready).
/// </summary>
[RequireComponent(typeof(Animator))]
public class FlyingEnemyAnimator : MonoBehaviour
{
    private Animator _anim;
    private Rigidbody2D _rb;

    // Cache parameter hashes
    private static readonly int IsFlying = Animator.StringToHash("isFlying");
    private static readonly int ShootTrigger = Animator.StringToHash("Shoot");
    private static readonly int ShootSpeed = Animator.StringToHash("ShootSpeed");
    private static readonly int IsMoving = Animator.StringToHash("isMoving");

    [Header("Settings")]
    [Tooltip("If true, always sets isFlying to true (for hover enemies like Buzzer)")]
    public bool AlwaysFlying = true;
    
    [Header("Animation Settings")]
    [Tooltip("The base duration of your shoot animation clip in seconds")]
    public float BaseShootAnimationDuration = 0.5f;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (AlwaysFlying)
        {
            _anim.SetBool(IsFlying, true);
        }
    }

    void Update()
    {
        // Track movement for blend states
        if (_rb != null)
        {
            float speed = _rb.linearVelocity.magnitude;
            _anim.SetBool(IsMoving, speed > 0.1f);
        }
    }

    /// <summary>
    /// Call this from BuzzerEnemy.ShootProjectile() to trigger the shoot animation.
    /// Pass in the shoot rate so animation duration = cooldown / 2.
    /// </summary>
    public void TriggerShootAnimation(float shootCooldown)
    {
        // QOL: Calculate animation speed multiplier
        // Target duration = ShootCooldown / 2
        float targetDuration = shootCooldown / 2f;
        targetDuration = Mathf.Max(targetDuration, 0.05f);
        
        float speedMultiplier = BaseShootAnimationDuration / targetDuration;
        speedMultiplier = Mathf.Clamp(speedMultiplier, 0.5f, 4f);
        
        _anim.SetFloat(ShootSpeed, speedMultiplier);
        _anim.SetTrigger(ShootTrigger);
    }

    /// <summary>
    /// Overload for backwards compatibility (uses default speed)
    /// </summary>
    public void TriggerShootAnimation()
    {
        _anim.SetFloat(ShootSpeed, 1f);
        _anim.SetTrigger(ShootTrigger);
    }

    /// <summary>
    /// Manually set flying state (useful for enemies that can land)
    /// </summary>
    public void SetFlying(bool isFlying)
    {
        _anim.SetBool(IsFlying, isFlying);
    }
}