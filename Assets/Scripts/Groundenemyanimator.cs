using UnityEngine;

/// <summary>
/// Handles animation states for ground-based enemies.
/// Attach to enemy GameObjects that have an Animator component.
/// 
/// FIX: Ground enemies were always playing idle because nothing was setting
/// the "isMoving" parameter. This script checks horizontal velocity each frame.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class GroundEnemyAnimator : MonoBehaviour
{
    private Animator _anim;
    private Rigidbody2D _rb;

    // Cache parameter hashes for performance
    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int VelocityY = Animator.StringToHash("velocityY");
    private static readonly int MoveSpeed = Animator.StringToHash("moveSpeed");

    [Header("Ground Check (Optional)")]
    [Tooltip("If assigned, uses this for ground detection. Otherwise assumes always grounded.")]
    public Transform GroundCheckPoint;
    public float GroundCheckRadius = 0.2f;
    public LayerMask GroundLayer;
    
    [Header("Animation Settings")]
    [Tooltip("Threshold velocity to consider 'moving' for animation purposes")]
    public float MoveThreshold = 0.1f;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        UpdateAnimationState();
    }

    void UpdateAnimationState()
    {
        // FIX: Check actual horizontal velocity to determine if moving
        float horizontalSpeed = Mathf.Abs(_rb.linearVelocity.x);
        bool isMoving = horizontalSpeed > MoveThreshold;
        
        _anim.SetBool(IsMoving, isMoving);
        
        // Optional: Pass normalized speed for blend trees
        _anim.SetFloat(MoveSpeed, horizontalSpeed);

        // Vertical velocity for jump animations
        _anim.SetFloat(VelocityY, _rb.linearVelocity.y);

        // Ground check (if configured)
        if (GroundCheckPoint != null)
        {
            bool grounded = Physics2D.OverlapCircle(GroundCheckPoint.position, GroundCheckRadius, GroundLayer);
            _anim.SetBool(IsGrounded, grounded);
        }
        else
        {
            // Fallback: assume grounded if vertical velocity is near zero
            _anim.SetBool(IsGrounded, Mathf.Abs(_rb.linearVelocity.y) < 0.1f);
        }
    }
}