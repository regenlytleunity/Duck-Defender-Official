using UnityEngine;

[RequireComponent(typeof(Animator))]
public class TankEnemy : EnemyBase
{
    [Header("Ground AI")]
    public float JumpForce = 5f;
    public float WallCheckDistance = 1.0f;
    public LayerMask GroundLayer;
    public Transform WallCheckPoint;

    [Header("Melee Attack")]
    public float AttackRange = 1.5f;
    public float AttackCooldown = 2.5f;
    [Tooltip("Total duration of the attack animation. Match to your animation clip length.")]
    public float AttackDuration = 0.6f;
    [Tooltip("When during the attack the damage lands (0-1). 0.7 = damage at 70% through.")]
    [Range(0f, 1f)]
    public float DamagePoint = 0.7f;
    public Vector2 AttackBoxSize = new Vector2(1.8f, 1.2f);
    public LayerMask PlayerLayer;
    
    [Tooltip("1.4.13 FIX: how much CLOSER than AttackRange the enemy walks before stopping " +
             "to attack. See SwarmerEnemy for the full rationale - same fix applied here. " +
             "Tank uses a slightly larger value than Swarmer because its hitbox is bigger.")]
    public float StopDistanceReduction = 0.5f;
    
    [Header("1.4.9 - Persistent Strike Window")]
    [Tooltip("How wide the strike window is as a fraction of attack duration. " +
             "0.2 means the hitbox is checked every frame for 20% of the animation after the damage point.")]
    [Range(0.05f, 0.5f)]
    public float StrikeWindowFraction = 0.2f;
    
    [Tooltip("Extra horizontal reach added to the attack box during the strike window.")]
    public float StrikeWindowReachBonus = 0.3f;

    [Header("Damage Reduction")]
    [Range(0f, 0.9f)]
    public float MaxDamageReduction = 0.5f;
    public float ZeroReductionDistance = 0f;

    [Header("Visual Feedback")]
    public Color ShieldedColor = new Color(0.5f, 0.5f, 0.7f);
    public Color VulnerableColor = Color.white;

    private Animator _animator;
    private Collider2D _ownCollider;
    private static readonly int AnimIsMoving = Animator.StringToHash("ismoving");
    private static readonly int AnimIsAttacking = Animator.StringToHash("isattacking");

    private bool _isAttacking = false;
    private float _attackTimer = 0f;
    private bool _hasDamaged = false;
    private float _lastAttackTime;
    
    // Strike window state - tracks the time range during which hitbox checks happen
    private float _strikeWindowStart = -1f;
    private float _strikeWindowEnd = -1f;

    private float _spawnDistance;
    private float _halfDistance;
    private bool _distanceInitialized = false;

    public override void Initialize(float wave)
    {
        base.Initialize(wave);
        _animator = GetComponent<Animator>();
        _ownCollider = GetComponent<Collider2D>();
        _lastAttackTime = -AttackCooldown;
    }

    protected override void Move()
    {
        if (PlayerTarget == null) return;

        if (!_distanceInitialized)
        {
            _spawnDistance = Vector2.Distance(transform.position, PlayerTarget.position);
            _halfDistance = (ZeroReductionDistance > 0) ? ZeroReductionDistance : _spawnDistance * 0.5f;
            _distanceInitialized = true;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, PlayerTarget.position);

        // --- ATTACKING STATE ---
        if (_isAttacking)
        {
            if (distanceToPlayer > AttackRange)
            {
                ResetAttackState();
                SetAnimation(true, false);
            }
            else
            {
                Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y);
                _attackTimer += Time.deltaTime;

                // === 1.4.9 PERSISTENT STRIKE WINDOW ===
                // Instead of a single-frame check at DamagePoint, we open a strike 
                // window starting at DamagePoint and lasting StrikeWindowFraction of the duration.
                // Every frame inside this window, we try to land a hit. As soon as a hit 
                // lands, _hasDamaged flips to true and we stop trying.
                if (!_hasDamaged && _attackTimer >= AttackDuration * DamagePoint)
                {
                    // Lazy-init the window bounds the first time we cross the damage point
                    if (_strikeWindowStart < 0)
                    {
                        _strikeWindowStart = _attackTimer;
                        _strikeWindowEnd = _strikeWindowStart + (AttackDuration * StrikeWindowFraction);
                    }
                    
                    if (_attackTimer <= _strikeWindowEnd)
                    {
                        if (TryDealMeleeDamage())
                        {
                            _hasDamaged = true;
                        }
                    }
                }

                if (_attackTimer >= AttackDuration)
                {
                    ResetAttackState();
                    _lastAttackTime = Time.time;
                    
                    float dist = Vector2.Distance(transform.position, PlayerTarget.position);
                    if (dist > AttackRange)
                    {
                        SetAnimation(true, false);
                    }
                    else
                    {
                        SetAnimation(false, false);
                    }
                }
                else
                {
                    UpdateShieldVisual();
                    return;
                }
            }
        }

        // --- MOVEMENT STATE ---
        // 1.4.13 FIX: stop CLOSER than AttackRange (by StopDistanceReduction) so the 
        // attack box overlaps the player hitbox properly. Same fix as SwarmerEnemy.
        // The attack-cancel checks above still use the full AttackRange so the tank 
        // doesn't ping-pong between moving and attacking when the player is at the edge.
        float stopDistance = Mathf.Max(0.1f, AttackRange - StopDistanceReduction);
        
        if (distanceToPlayer > stopDistance)
        {
            float direction = (PlayerTarget.position.x > transform.position.x) ? 1f : -1f;
            Rb.linearVelocity = new Vector2(direction * CurrentSpeed, Rb.linearVelocity.y);
            CheckForWalls(direction);
            SetAnimation(true, false);
        }
        else
        {
            Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y);

            if (Time.time >= _lastAttackTime + AttackCooldown)
            {
                _isAttacking = true;
                _attackTimer = 0f;
                _hasDamaged = false;
                _strikeWindowStart = -1f;
                _strikeWindowEnd = -1f;
                SetAnimation(false, true);
            }
            else
            {
                SetAnimation(false, false);
            }
        }

        UpdateShieldVisual();
    }
    
    void ResetAttackState()
    {
        _isAttacking = false;
        _attackTimer = 0f;
        _hasDamaged = false;
        _strikeWindowStart = -1f;
        _strikeWindowEnd = -1f;
    }

    /// <summary>
    /// 1.4.9: Now returns bool. Uses collider center for vertical anchor and an enlarged
    /// hitbox during the strike window. See SwarmerEnemy.TryDealMeleeDamage() for full
    /// rationale - same fix.
    /// </summary>
    bool TryDealMeleeDamage()
    {
        Vector2 origin = GetAttackBoxOrigin();
        Vector2 boxSize = AttackBoxSize + new Vector2(StrikeWindowReachBonus, 0f);
        
        Collider2D hit = Physics2D.OverlapBox(origin, boxSize, 0f, PlayerLayer);
        
        if (hit == null) return false;
        
        PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
        if (playerHealth == null) return false;
        
        playerHealth.TakeDamage(DamageOnHit);
        return true;
    }
    
    Vector2 GetAttackBoxOrigin()
    {
        float facingDir = -transform.localScale.x;
        
        float yOrigin = transform.position.y;
        if (_ownCollider != null)
        {
            yOrigin = _ownCollider.bounds.center.y;
        }
        
        float xOrigin = transform.position.x + (facingDir * 0.5f);
        return new Vector2(xOrigin, yOrigin);
    }

    private void SetAnimation(bool moving, bool attacking)
    {
        if (_animator == null) return;
        _animator.SetBool(AnimIsMoving, moving);
        _animator.SetBool(AnimIsAttacking, attacking);
    }

    // --- DAMAGE REDUCTION ---

    public override void TakeDamage(int damage)
    {
        float reduction = GetCurrentDamageReduction();
        int reducedDamage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - reduction)));
        base.TakeDamage(reducedDamage);
    }

    public float GetCurrentDamageReduction()
    {
        if (PlayerTarget == null || !_distanceInitialized) return 0f;

        float currentDist = Vector2.Distance(transform.position, PlayerTarget.position);

        if (currentDist >= _spawnDistance)
            return MaxDamageReduction;

        if (currentDist <= _halfDistance)
            return 0f;

        float t = (currentDist - _halfDistance) / (_spawnDistance - _halfDistance);
        return MaxDamageReduction * t;
    }

    private void UpdateShieldVisual()
    {
        if (SpriteRen == null) return;

        float reduction = GetCurrentDamageReduction();

        if (SpriteRen.color == Color.green || SpriteRen.color == Color.cyan)
            return;

        float t = (MaxDamageReduction > 0) ? reduction / MaxDamageReduction : 0f;
        SpriteRen.color = Color.Lerp(VulnerableColor, ShieldedColor, t);
    }

    private void CheckForWalls(float direction)
    {
        Vector2 origin = WallCheckPoint != null ? WallCheckPoint.position : transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * direction, WallCheckDistance, GroundLayer);

        if (hit.collider != null)
        {
            if (Mathf.Abs(Rb.linearVelocity.y) < 0.01f)
            {
                Rb.AddForce(Vector2.up * JumpForce, ForceMode2D.Impulse);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (WallCheckPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(WallCheckPoint.position, WallCheckPoint.position + new Vector3(WallCheckDistance, 0, 0));
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector2 attackOrigin = GetAttackBoxOrigin();
        Vector2 size = AttackBoxSize + new Vector2(StrikeWindowReachBonus, 0f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackOrigin, size);

        if (_distanceInitialized && PlayerTarget != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(PlayerTarget.position, _spawnDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(PlayerTarget.position, _halfDistance);
        }
    }
}
