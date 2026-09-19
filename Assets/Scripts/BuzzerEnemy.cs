using UnityEngine;

public class BuzzerEnemy : EnemyBase
{
    [Header("Hover Settings")]
    public float HoverHeight = 3.0f;
    public float HorizontalOffset = 4.0f;
    public float MoveSmoothing = 1.0f;
    
    [Header("Combat")]
    [Tooltip("Base time between shots (seconds). Decreases with wave scaling and difficulty tier.")]
    public float BaseShootRate = 2.5f;
    [Tooltip("Minimum shoot rate floor (won't fire faster than this)")]
    public float MinShootRate = 0.5f;
    [Tooltip("How much shoot rate decreases per wave. Affected by difficulty tier multiplier.")]
    public float ShootRateScaling = 0.05f;
    
    [Header("Projectile Inaccuracy")]
    public float InaccuracyAngle = 10f;
    
    [Header("Retreat Hover")]
    public float RetreatInterval = 6.0f;
    public float RetreatDuration = 2.0f;
    public float RetreatHeightBonus = 3.0f;
    public float RetreatDistanceBonus = 4.0f;
    
    [Header("Swarm Separation")]
    public float SeparationRadius = 1.5f;
    public float SeparationForce = 5.0f;

    private Vector2 _velocity;
    private float _nextShootTime;
    private FlyingEnemyAnimator _animator;
    
    private float _currentShootRate;
    
    private float _nextRetreatTime;
    private float _retreatEndTime;
    private bool _isRetreating = false;

    public override void Initialize(float wave)
    {
        base.Initialize(wave);
        Rb.gravityScale = 0;
        _animator = GetComponent<FlyingEnemyAnimator>();
        
        float tierMultiplier = 1f;
        if (WaveManager.Instance != null)
        {
            tierMultiplier = WaveManager.Instance.GetDifficultyScalingMultiplier();
        }
        
        _currentShootRate = BaseShootRate / (1f + (wave * ShootRateScaling * tierMultiplier));
        _currentShootRate = Mathf.Max(_currentShootRate, MinShootRate);
        
        _nextShootTime = Time.time + Random.Range(0, _currentShootRate);
        _nextRetreatTime = Time.time + Random.Range(RetreatInterval * 0.5f, RetreatInterval * 1.5f);
    }

    protected override void Move()
    {
        if (PlayerTarget == null) return;

        UpdateRetreatState();

        float targetX;
        float targetY;
        
        if (_isRetreating)
        {
            float awayDir = (transform.position.x > PlayerTarget.position.x) ? 1f : -1f;
            targetX = PlayerTarget.position.x + awayDir * (HorizontalOffset + RetreatDistanceBonus);
            targetY = PlayerTarget.position.y + HoverHeight + RetreatHeightBonus;
        }
        else
        {
            targetX = PlayerTarget.position.x + (transform.position.x > PlayerTarget.position.x ? HorizontalOffset : -HorizontalOffset);
            targetY = PlayerTarget.position.y + HoverHeight;
        }
        
        Vector2 desiredPos = new Vector2(targetX, targetY);
        Vector2 separationVector = CalculateSeparation();
        desiredPos += separationVector;

        float smoothing = _isRetreating ? MoveSmoothing * 1.5f : MoveSmoothing;
        
        transform.position = Vector2.SmoothDamp(transform.position, desiredPos, ref _velocity, smoothing);

        if (!_isRetreating && Time.time >= _nextShootTime)
        {
            ShootProjectile();
            _nextShootTime = Time.time + _currentShootRate;
        }
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

    private Vector2 CalculateSeparation()
    {
        Vector2 force = Vector2.zero;
        Collider2D[] neighbors = Physics2D.OverlapCircleAll(transform.position, SeparationRadius, 1 << 8);

        foreach (var neighbor in neighbors)
        {
            if (neighbor.gameObject != gameObject)
            {
                Vector2 away = transform.position - neighbor.transform.position;
                force += away.normalized;
            }
        }
        return force * SeparationForce;
    }

    private void ShootProjectile()
    {
        // PATCH: null-check AudioManager. Was crashing when AudioManager.Instance was null.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Flying_Enemy_Shoot");
        }
        
        if (_animator != null)
        {
            _animator.TriggerShootAnimation(_currentShootRate);
        }
        
        // PATCH: null-check ObjectPooler too
        if (ObjectPooler.Instance == null) return;
        
        GameObject bullet = ObjectPooler.Instance.GetEnemyBullet();
        if (bullet != null)
        {
            bullet.transform.position = transform.position;
            
            // PATCH: extra safety - PlayerTarget could theoretically be null mid-shot
            if (PlayerTarget == null) return;
            
            Vector2 dir = (PlayerTarget.position - transform.position).normalized;
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            
            float inaccuracy = Random.Range(-InaccuracyAngle, InaccuracyAngle);
            float finalAngle = baseAngle + inaccuracy;
            
            bullet.transform.rotation = Quaternion.Euler(0, 0, finalAngle);
            bullet.SetActive(true);
        }
    }
}