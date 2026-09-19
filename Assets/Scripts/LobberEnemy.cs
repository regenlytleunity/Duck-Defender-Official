using UnityEngine;

/// <summary>
/// A ground-based ranged enemy that walks toward the player until within firing range,
/// then stops to fire slow, gravity-affected bouncing projectiles in an arc.
/// 
/// POST-1.4.11 PATCH: null-checked AudioManager and ObjectPooler calls.
/// </summary>
public class LobberEnemy : EnemyBase
{
    [Header("Ground AI")]
    public float JumpForce = 5f;
    public float WallCheckDistance = 1.0f;
    public LayerMask GroundLayer;
    public Transform WallCheckPoint;
    
    [Header("Combat Range")]
    public float FiringRange = 8f;
    public float MinComfortDistance = 4f;
    public float DisengageRange = 10f;
    
    [Header("Combat - Firing")]
    public float BaseShootRate = 2.0f;
    public float MinShootRate = 0.7f;
    public float ShootRateScaling = 0.04f;
    public float FirstShotDelay = 0.8f;
    
    [Header("Projectile Properties")]
    public float ProjectileSpeed = 8f;
    public Transform FirePoint;
    public float InaccuracyAngle = 5f;
    
    [Header("Animation")]
    public Animator EnemyAnimator;
    public float BaseAttackAnimationDuration = 0.5f;
    
    private static readonly int AnimIsMoving = Animator.StringToHash("ismoving");
    private static readonly int AnimIsAttacking = Animator.StringToHash("isattacking");
    private static readonly int AnimShootSpeed = Animator.StringToHash("ShootSpeed");
    
    private enum LobberState { Approaching, Engaging }
    private LobberState _state = LobberState.Approaching;
    private float _nextShootTime;
    private float _currentShootRate;
    private float _currentAttackAnimDuration;

    public override void Initialize(float wave)
    {
        base.Initialize(wave);
        
        if (EnemyAnimator == null) EnemyAnimator = GetComponent<Animator>();
        
        float tierMultiplier = 1f;
        if (WaveManager.Instance != null)
        {
            tierMultiplier = WaveManager.Instance.GetDifficultyScalingMultiplier();
        }
        
        _currentShootRate = BaseShootRate / (1f + (wave * ShootRateScaling * tierMultiplier));
        _currentShootRate = Mathf.Max(_currentShootRate, MinShootRate);
        
        _currentAttackAnimDuration = Mathf.Max(_currentShootRate / 2f, 0.1f);
        
        _state = LobberState.Approaching;
    }

    protected override void Move()
    {
        if (PlayerTarget == null) return;
        
        float horizontalDist = Mathf.Abs(PlayerTarget.position.x - transform.position.x);
        
        if (_state == LobberState.Approaching && horizontalDist <= FiringRange)
        {
            _state = LobberState.Engaging;
            _nextShootTime = Time.time + FirstShotDelay;
        }
        else if (_state == LobberState.Engaging && horizontalDist > DisengageRange)
        {
            _state = LobberState.Approaching;
        }
        
        switch (_state)
        {
            case LobberState.Approaching:
                MoveTowardPlayer();
                break;
            case LobberState.Engaging:
                StopAndFire(horizontalDist);
                break;
        }
    }
    
    private void MoveTowardPlayer()
    {
        float direction = (PlayerTarget.position.x > transform.position.x) ? 1f : -1f;
        Rb.linearVelocity = new Vector2(direction * CurrentSpeed, Rb.linearVelocity.y);
        
        CheckForWalls(direction);
        SetMovingAnimation(true);
    }
    
    private void StopAndFire(float horizontalDist)
    {
        Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y);
        SetMovingAnimation(false);
        
        if (horizontalDist < MinComfortDistance) return;
        
        if (Time.time >= _nextShootTime)
        {
            FireProjectile();
            _nextShootTime = Time.time + _currentShootRate;
        }
    }
    
    private void FireProjectile()
    {
        // PATCH: null-check AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Ground_Enemy_Shoot");
        }
        
        StartAttackAnimation();
        
        // PATCH: null-check ObjectPooler
        if (ObjectPooler.Instance == null) return;
        
        GameObject projectile = ObjectPooler.Instance.GetBouncyEnemyProjectile();
        if (projectile == null) return;
        
        Vector3 spawnPos = FirePoint != null ? FirePoint.position : transform.position;
        projectile.transform.position = spawnPos;
        
        // PATCH: null-check PlayerTarget for the brief window where the player might be destroyed mid-shot
        if (PlayerTarget == null) return;
        
        Vector2 launchVelocity = CalculateLobVelocity(spawnPos, PlayerTarget.position);
        
        if (InaccuracyAngle > 0)
        {
            float deviation = Random.Range(-InaccuracyAngle, InaccuracyAngle);
            launchVelocity = Quaternion.Euler(0, 0, deviation) * launchVelocity;
        }
        
        BouncyEnemyProjectile bouncy = projectile.GetComponent<BouncyEnemyProjectile>();
        if (bouncy != null)
        {
            bouncy.Launch(launchVelocity);
        }
        
        projectile.SetActive(true);
    }
    
    private void SetMovingAnimation(bool isMoving)
    {
        if (EnemyAnimator == null) return;
        EnemyAnimator.SetBool(AnimIsMoving, isMoving);
    }
    
    private void StartAttackAnimation()
    {
        if (EnemyAnimator == null) return;
        
        EnemyAnimator.SetBool(AnimIsAttacking, true);
        
        float speedMultiplier = BaseAttackAnimationDuration / _currentAttackAnimDuration;
        speedMultiplier = Mathf.Clamp(speedMultiplier, 0.5f, 4f);
        EnemyAnimator.SetFloat(AnimShootSpeed, speedMultiplier);
        
        CancelInvoke(nameof(EndAttackAnimation));
        Invoke(nameof(EndAttackAnimation), _currentAttackAnimDuration);
    }
    
    private void EndAttackAnimation()
    {
        if (EnemyAnimator == null) return;
        EnemyAnimator.SetBool(AnimIsAttacking, false);
    }
    
    private Vector2 CalculateLobVelocity(Vector3 start, Vector3 target)
    {
        Vector2 displacement = target - start;
        float gravity = Mathf.Abs(Physics2D.gravity.y);
        float speed = ProjectileSpeed;
        float speedSq = speed * speed;
        
        float dx = displacement.x;
        float dy = displacement.y;
        float discriminant = (speedSq * speedSq) - gravity * (gravity * dx * dx + 2 * dy * speedSq);
        
        if (discriminant < 0)
        {
            float fallbackAngle = 45f * Mathf.Deg2Rad;
            float xDir = Mathf.Sign(dx);
            return new Vector2(Mathf.Cos(fallbackAngle) * speed * xDir, Mathf.Sin(fallbackAngle) * speed);
        }
        
        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float angleHigh = Mathf.Atan2(speedSq + sqrtDiscriminant, gravity * dx);
        
        float vx = speed * Mathf.Cos(angleHigh);
        float vy = speed * Mathf.Sin(angleHigh);
        
        if (Mathf.Sign(vx) != Mathf.Sign(dx))
        {
            vx = -vx;
        }
        
        return new Vector2(vx, vy);
    }
    
    private void CheckForWalls(float direction)
    {
        if (WallCheckPoint == null) return;
        
        Vector2 origin = WallCheckPoint.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * direction, WallCheckDistance, GroundLayer);

        if (hit.collider != null)
        {
            if (Mathf.Abs(Rb.linearVelocity.y) < 0.01f)
            {
                Rb.AddForce(Vector2.up * JumpForce, ForceMode2D.Impulse);
            }
        }
    }
    
    protected override void Die()
    {
        CancelInvoke();
        
        if (EnemyAnimator != null)
        {
            EnemyAnimator.SetBool(AnimIsMoving, false);
            EnemyAnimator.SetBool(AnimIsAttacking, false);
        }
        
        base.Die();
    }
}