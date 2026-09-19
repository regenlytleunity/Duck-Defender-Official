using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FlyingEnemy : EnemyBase
{
    [Header("Flight AI")]
    public float SwoopSmoothing = 0.5f;
    
    [Header("Engagement Range")]
    [Tooltip("Maximum distance at which this enemy will fire at the player. " +
             "Outside this range, the enemy approaches without firing.")]
    public float ShootRange = 8.0f;
    
    [Tooltip("Minimum distance from the player. The enemy won't approach closer than this " +
             "even while chasing - prevents flyers from sitting on top of the player.")]
    public float MinHoverDistance = 3.0f;
    
    [Header("Combat Behavior")]
    [Tooltip("If true, the enemy stops moving while firing each shot. Better readability for the player.\n" +
             "If false, the enemy can shoot while continuing to chase. Higher difficulty - harder to dodge.")]
    public bool StopToShoot = false;
    
    [Tooltip("How long the enemy holds still while firing (only used when StopToShoot is true). " +
             "Should be roughly equal to ShootAnimationDuration for a natural feel.")]
    public float StopDurationOnShoot = 0.4f;
    
    [Header("Combat - Firing")]
    [Tooltip("Base time between shots (seconds). Decreases with wave scaling and difficulty tier.")]
    public float BaseShootRate = 3.0f;
    [Tooltip("Minimum shoot rate floor (won't fire faster than this)")]
    public float MinShootRate = 0.6f;
    [Tooltip("How much shoot rate decreases per wave. Affected by difficulty tier multiplier.")]
    public float ShootRateScaling = 0.05f;
    
    [Header("Projectile Inaccuracy")]
    public float InaccuracyAngle = 12f;
    
    [Header("Retreat Hover")]
    public float RetreatInterval = 5.0f;
    public float RetreatDuration = 2.0f;
    public float RetreatHeightBonus = 4.0f;
    public float RetreatDistanceBonus = 5.0f;
    
    [Header("Animation Timing")]
    public float ShootAnimationDuration = 0.3f;

    private Vector2 _currentVelocity;
    private Animator _animator;
    private float _nextShootTime;
    private bool _isShooting = false;
    
    private float _currentShootRate;
    
    private float _nextRetreatTime;
    private float _retreatEndTime;
    private bool _isRetreating = false;
    
    // When StopToShoot is enabled, this tracks how long the enemy must remain still.
    // Cleared when the timer expires; movement resumes naturally afterward.
    private float _stopMovementUntil = 0f;

    private static readonly int AnimIsFlying = Animator.StringToHash("isFlying");
    private static readonly int AnimIsShooting = Animator.StringToHash("isShooting");
    private static readonly int AnimShootSpeed = Animator.StringToHash("ShootSpeed");

    public override void Initialize(float wave)
    {
        base.Initialize(wave);
        Rb.gravityScale = 0;
        _animator = GetComponent<Animator>();
        
        float tierMultiplier = 1f;
        if (WaveManager.Instance != null)
        {
            tierMultiplier = WaveManager.Instance.GetDifficultyScalingMultiplier();
        }
        
        _currentShootRate = BaseShootRate / (1f + (wave * ShootRateScaling * tierMultiplier));
        _currentShootRate = Mathf.Max(_currentShootRate, MinShootRate);
        
        _nextShootTime = Time.time + Random.Range(0.5f, _currentShootRate);
        _nextRetreatTime = Time.time + Random.Range(RetreatInterval * 0.5f, RetreatInterval * 1.5f);
        
        if (_animator != null)
        {
            _animator.SetBool(AnimIsFlying, true);
        }
    }

    protected override void Move()
    {
        if (PlayerTarget == null) return;

        UpdateRetreatState();

        // Check if we're in a "stopped to shoot" frame (only when StopToShoot is enabled)
        bool isStoppedForShot = StopToShoot && Time.time < _stopMovementUntil;
        
        Vector2 targetPos;
        
        if (isStoppedForShot)
        {
            // Hold position - target the current position so SmoothDamp keeps us still
            targetPos = transform.position;
        }
        else if (_isRetreating)
        {
            float awayDir = (transform.position.x > PlayerTarget.position.x) ? 1f : -1f;
            targetPos = new Vector2(
                PlayerTarget.position.x + awayDir * RetreatDistanceBonus,
                PlayerTarget.position.y + RetreatHeightBonus
            );
        }
        else
        {
            // Normal pursuit - approach the player but maintain minimum distance
            float distanceToPlayer = Vector2.Distance(transform.position, PlayerTarget.position);
            
            if (distanceToPlayer > MinHoverDistance)
            {
                // Far enough - chase the player directly
                targetPos = PlayerTarget.position;
            }
            else
            {
                // Too close - hold current position relative to player to maintain hover distance
                Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)PlayerTarget.position).normalized;
                targetPos = (Vector2)PlayerTarget.position + awayFromPlayer * MinHoverDistance;
            }
        }
        
        float smoothing = _isRetreating ? SwoopSmoothing * 2f : SwoopSmoothing;
        
        transform.position = Vector2.SmoothDamp(
            transform.position, 
            targetPos, 
            ref _currentVelocity, 
            smoothing,
            CurrentSpeed
        );

        // Handle firing logic (independent of movement state)
        TryShoot();
    }
    
    private void TryShoot()
    {
        if (_isRetreating) return;
        if (_isShooting) return;
        if (Time.time < _nextShootTime) return;
        
        // Check range to player
        float distanceToPlayer = Vector2.Distance(transform.position, PlayerTarget.position);
        if (distanceToPlayer > ShootRange) return;
        
        ShootAtPlayer();
    }
    
    private void UpdateRetreatState()
    {
        if (_isRetreating)
        {
            if (Time.time >= _retreatEndTime)
            {
                _isRetreating = false;
                _nextRetreatTime = Time.time + RetreatInterval;
            }
        }
        else
        {
            if (Time.time >= _nextRetreatTime)
            {
                _isRetreating = true;
                _retreatEndTime = Time.time + RetreatDuration;
            }
        }
    }

    private void ShootAtPlayer()
    {
        _isShooting = true;
        _nextShootTime = Time.time + _currentShootRate;
        
        // If StopToShoot is enabled, lock movement for the duration of the stop
        if (StopToShoot)
        {
            _stopMovementUntil = Time.time + StopDurationOnShoot;
        }

        if (_animator != null)
        {
            _animator.SetBool(AnimIsShooting, true);
            
            float targetAnimDuration = _currentShootRate / 2f;
            targetAnimDuration = Mathf.Max(targetAnimDuration, 0.05f);
            float speedMultiplier = ShootAnimationDuration / targetAnimDuration;
            speedMultiplier = Mathf.Clamp(speedMultiplier, 0.5f, 4f);
            _animator.SetFloat(AnimShootSpeed, speedMultiplier);
        }

        GameObject bullet = ObjectPooler.Instance.GetEnemyBullet();
        if (bullet != null)
        {
            bullet.transform.position = transform.position;
            
            Vector2 dir = (PlayerTarget.position - transform.position).normalized;
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            
            float inaccuracy = Random.Range(-InaccuracyAngle, InaccuracyAngle);
            float finalAngle = baseAngle + inaccuracy;
            
            bullet.transform.rotation = Quaternion.Euler(0, 0, finalAngle);
            bullet.SetActive(true);
        }

        float animDuration = Mathf.Min(ShootAnimationDuration, _currentShootRate * 0.5f);
        Invoke(nameof(EndShootAnimation), animDuration);
    }

    private void EndShootAnimation()
    {
        _isShooting = false;
        
        if (_animator != null)
        {
            _animator.SetBool(AnimIsShooting, false);
        }
    }

    protected override void Die()
    {
        if (_animator != null)
        {
            _animator.SetBool(AnimIsFlying, false);
            _animator.SetBool(AnimIsShooting, false);
        }
        
        base.Die();
    }
    
    void OnDrawGizmosSelected()
    {
        // Visualize the engagement and hover ranges in the editor
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, ShootRange);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, MinHoverDistance);
    }
}