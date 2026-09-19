using UnityEngine;

/// <summary>
/// A slow, gravity-affected projectile that bounces off the ground.
/// Uses raycast-based ground detection (configurable LayerMask) instead of 
/// relying on tags or the Physics 2D Layer Collision Matrix.
/// 
/// Damages the player on contact via trigger collision.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BouncyEnemyProjectile : MonoBehaviour
{
    [Header("Damage")]
    public int Damage = 1;
    
    [Header("Ground Detection")]
    [Tooltip("Layer mask for ground/walls. Set this to your Ground layer (same as enemy ground checks).")]
    public LayerMask GroundLayer;
    
    [Tooltip("Distance ahead of the projectile to check for ground. Should be slightly larger than the projectile's radius.")]
    public float GroundCheckDistance = 0.3f;
    
    [Header("Bounce Properties")]
    [Tooltip("How much energy is preserved per bounce. 0.7 means each bounce keeps 70% of speed.")]
    [Range(0f, 1f)]
    public float Bounciness = 0.7f;
    
    [Tooltip("Maximum number of ground bounces before the projectile deactivates.")]
    public int MaxBounces = 4;
    
    [Tooltip("Minimum speed to continue bouncing. Below this, the projectile stops and deactivates.")]
    public float MinBounceSpeed = 1.5f;
    
    [Header("Lifetime")]
    [Tooltip("Maximum total lifetime in seconds before forced deactivation.")]
    public float MaxLifetime = 6f;
    
    [Header("Visual")]
    [Tooltip("Rotation speed visual effect (degrees per second per unit of speed).")]
    public float VisualRotationFactor = 30f;
    
    [Header("Debug")]
    [Tooltip("If true, draws raycast lines and prints collision details to the console.")]
    public bool DebugMode = false;
    
    private Rigidbody2D _rb;
    private CircleCollider2D _circleCollider;
    private int _bouncesRemaining;
    private float _lifetimeRemaining;
    private bool _isActive;
    
    // Stored launch velocity - applied in OnEnable after the GameObject is active.
    // Setting velocity on an inactive Rigidbody2D doesn't always take effect.
    private Vector2 _pendingLaunchVelocity;
    private bool _hasPendingLaunch = false;
    
    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _circleCollider = GetComponent<CircleCollider2D>();
    }
    
    /// <summary>
    /// Stores the desired launch velocity. The actual velocity application happens 
    /// in OnEnable, since rigidbody velocity changes on inactive objects can be lost.
    /// Call this BEFORE SetActive(true) on the projectile.
    /// </summary>
    public void Launch(Vector2 initialVelocity)
    {
        _pendingLaunchVelocity = initialVelocity;
        _hasPendingLaunch = true;
        
        _bouncesRemaining = MaxBounces;
        _lifetimeRemaining = MaxLifetime;
    }
    
    void OnEnable()
    {
        // Apply the launch velocity NOW that the GameObject is active.
        // This is critical - velocity assignments on inactive rigidbodies can be discarded.
        if (_hasPendingLaunch)
        {
            _rb.gravityScale = 1f;
            _rb.linearVelocity = _pendingLaunchVelocity;
            _rb.angularVelocity = 0f;
            transform.rotation = Quaternion.identity;
            
            _isActive = true;
            _hasPendingLaunch = false;
            
            if (DebugMode)
            {
                Debug.Log($"[BouncyProjectile] Launched with velocity {_pendingLaunchVelocity}");
            }
        }
        else
        {
            // Failsafe: activated without Launch() being called first
            _isActive = false;
        }
    }
    
    void Update()
    {
        if (!_isActive) return;
        
        _lifetimeRemaining -= Time.deltaTime;
        if (_lifetimeRemaining <= 0)
        {
            Deactivate();
            return;
        }
        
        // Visual spin based on horizontal speed
        float spinDirection = -Mathf.Sign(_rb.linearVelocity.x);
        transform.Rotate(0, 0, spinDirection * Mathf.Abs(_rb.linearVelocity.x) * VisualRotationFactor * Time.deltaTime);
    }
    
    void FixedUpdate()
    {
        if (!_isActive) return;
        
        CheckForGroundBounce();
    }
    
    /// <summary>
    /// Raycasts in the direction of motion to detect imminent ground contact.
    /// When detected, manually reflects velocity and counts the bounce.
    /// 
    /// This approach bypasses Unity's collision/trigger system entirely - it works 
    /// regardless of whether the ground is a trigger or solid collider, as long as 
    /// the GroundLayer mask is set correctly.
    /// </summary>
    private void CheckForGroundBounce()
    {
        Vector2 velocity = _rb.linearVelocity;
        float speed = velocity.magnitude;
        
        if (speed < 0.01f) return; // Not moving, can't bounce
        
        Vector2 direction = velocity.normalized;
        float radius = _circleCollider != null ? _circleCollider.radius * transform.lossyScale.x : 0.2f;
        
        // CircleCast from current position in direction of motion.
        // This finds ground in front of the projectile that we'd hit this frame.
        RaycastHit2D hit = Physics2D.CircleCast(
            transform.position, 
            radius, 
            direction, 
            GroundCheckDistance, 
            GroundLayer
        );
        
        if (DebugMode)
        {
            Debug.DrawRay(transform.position, direction * GroundCheckDistance, hit.collider != null ? Color.red : Color.green);
        }
        
        if (hit.collider != null)
        {
            HandleBounce(hit);
        }
    }
    
    private void HandleBounce(RaycastHit2D hit)
    {
        if (DebugMode)
        {
            Debug.Log($"[BouncyProjectile] Bounce on '{hit.collider.name}'. Bounces left: {_bouncesRemaining - 1}");
        }
        
        _bouncesRemaining--;
        
        if (_bouncesRemaining <= 0)
        {
            Deactivate();
            return;
        }
        
        // Reflect velocity off the surface normal, scaled by bounciness
        Vector2 reflected = Vector2.Reflect(_rb.linearVelocity, hit.normal);
        _rb.linearVelocity = reflected * Bounciness;
        
        // Nudge the projectile slightly away from the surface so it doesn't 
        // immediately re-detect the same hit on the next frame
        transform.position = (Vector2)transform.position + hit.normal * 0.05f;
        
        if (_rb.linearVelocity.magnitude < MinBounceSpeed)
        {
            // Too slow to keep bouncing
            Invoke(nameof(Deactivate), 1f);
        }
    }
    
    // === PLAYER DAMAGE ===
    
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isActive) return;
        
        if (collision.CompareTag("Player"))
        {
            HitPlayer(collision);
        }
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isActive) return;
        
        // Handle solid-collider players too, just in case
        if (collision.collider.CompareTag("Player"))
        {
            HitPlayer(collision.collider);
        }
    }
    
    private void HitPlayer(Collider2D playerCollider)
    {
        PlayerHealth ph = playerCollider.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(Damage);
        }
        
        if (DebugMode)
        {
            Debug.Log($"[BouncyProjectile] Hit player for {Damage} damage");
        }
        
        Deactivate();
    }
    
    private void Deactivate()
    {
        _isActive = false;
        _hasPendingLaunch = false;
        CancelInvoke();
        gameObject.SetActive(false);
    }
}
