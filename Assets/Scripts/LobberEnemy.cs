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
    [Min(.1f)] public float LobHeight = 4;
    [Min(.05f)] public float ShotWindup = .35f;
    [Min(.05f)] public float EliteShotDelay = .3f;
    bool _volleyActive, _secondShot;
    float _volleyTimer;
    Vector2 _aimPosition;
    
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
    bool _hasShootSpeed;

    public override void Initialize(float wave)
    {
        base.Initialize(wave);
        
        if (EnemyAnimator == null) EnemyAnimator = GetComponent<Animator>();
        if (EnemyAnimator != null) foreach (var parameter in EnemyAnimator.parameters)
            _hasShootSpeed |= parameter.nameHash == AnimShootSpeed;
        
        _currentShootRate = Mathf.Max(.1f, BaseShootRate);
        
        _currentAttackAnimDuration = Mathf.Max(_currentShootRate / 2f, 0.1f);
        
        _state = LobberState.Approaching;
    }

    protected override void Move()
    {
        if (PlayerTarget == null) return;
        if (_volleyActive)
        {
            Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y);
            _volleyTimer += Time.fixedDeltaTime;
            if (_volleyTimer >= (_secondShot ? EliteShotDelay : ShotWindup))
            {
                FireProjectile(_secondShot);
                if (IsElite && !_secondShot)
                {
                    _secondShot = true; _volleyTimer = 0;
                    _aimPosition = TargetCollider != null ? TargetCollider.bounds.center : PlayerTarget.position;
                }
                else { _volleyActive = false; _nextShootTime = Time.time + _currentShootRate; }
            }
            return;
        }
        
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
        
        if (Time.time >= _nextShootTime)
        {
            _volleyActive = true; _secondShot = false; _volleyTimer = 0;
            _aimPosition = TargetCollider != null ? TargetCollider.bounds.center : PlayerTarget.position;
            StartAttackAnimation();
        }
    }
    
    private void FireProjectile(bool bouncing)
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
        
        Vector2 launchVelocity = CalculateLobVelocity(spawnPos, _aimPosition);
        
        BouncyEnemyProjectile bouncy = projectile.GetComponent<BouncyEnemyProjectile>();
        if (bouncy != null)
        {
            bouncy.Launch(launchVelocity, bouncing);
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
        if (_hasShootSpeed) EnemyAnimator.SetFloat(AnimShootSpeed, speedMultiplier);
        
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
        float gravity = Mathf.Max(.01f, Mathf.Abs(Physics2D.gravity.y));
        float apex = Mathf.Max(start.y, target.y) + Mathf.Max(.1f, LobHeight);
        float vy = Mathf.Sqrt(2 * gravity * (apex - start.y));
        float flightTime = vy / gravity + Mathf.Sqrt(2 * (apex - target.y) / gravity);
        return new Vector2((target.x - start.x) / flightTime, vy);
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
