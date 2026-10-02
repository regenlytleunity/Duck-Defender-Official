using UnityEngine;

public class SwarmerEnemy : EnemyBase
{
    [Header("Melee Settings")]
    public float AttackRange = 1.5f;
    public float AttackCooldown = 1f;
    public Vector2 AttackBoxSize = new Vector2(1.5f, 1f);
    public LayerMask PlayerLayer;
    public float StopDistanceReduction = .5f;
    [Range(.05f, .5f)] public float StrikeWindowFraction = .2f;
    public float StrikeWindowReachBonus = .3f;
    [Tooltip("Reach from the body collider, also used to decide when to stop walking.")]
    [Min(.05f)] public float MeleeReach = .35f;
    [Min(.05f)] public float WindupSeconds = .25f;
    [Min(.05f)] public float RecoverySeconds = .15f;
    [Header("Ground AI")]
    public float JumpForce = 5f;
    public Transform WallCheckPoint;
    public LayerMask GroundLayer;
    [Header("Swarm Physics")]
    public float SeparationRadius = 1;
    public float SeparationForce = 2;
    public float MaxSpeedMultiplier = 1.3f;
    [Header("Animation")]
    public Animator EnemyAnimator;
    public float BaseAttackAnimationDuration = .5f;
    [Range(0, 1)] public float AttackWindupFraction = .5f;

    float _nextAttackTime, _attackElapsed, _attackDirection;
    bool _isAttacking, _hasStruck;
    bool _hasAttackSpeed;
    readonly Collider2D[] _neighbors = new Collider2D[16];
    protected virtual float StrikeDamage => IsElite ? 2 : DamageOnHit;
    protected virtual bool StunOnHit => IsElite;
    protected virtual float RuntimeSpeedMultiplier => 1;
    float Reach => MeleeReach * (IsElite && EnemyKind == "ground" ? 1.5f : 1);

    public override void Initialize(float wave)
    {
        base.Initialize(wave);
        if (EnemyAnimator == null) EnemyAnimator = GetComponent<Animator>();
        if (EnemyAnimator != null) foreach (var parameter in EnemyAnimator.parameters)
            _hasAttackSpeed |= parameter.name == "AttackSpeed";
        _nextAttackTime = Time.time;
    }

    protected override void Move()
    {
        if (PlayerTarget == null || Rb == null) return;
        if (this is TankEnemy && EnemyAnimator != null) EnemyAnimator.SetBool("ismoving", !_isAttacking && !PlayerInMeleeRange(Reach * .85f, AttackBoxSize.y));
        float duration = WindupSeconds + RecoverySeconds;
        if (_isAttacking)
        {
            Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y);
            _attackElapsed += Time.fixedDeltaTime;
            if (!_hasStruck && _attackElapsed >= WindupSeconds)
            {
                // One attempt at impact: moving out of reach during windup dodges the attack.
                _hasStruck = true;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Ground_Enemy_Slash");
                bool sameSide = Mathf.Sign(PlayerTarget.position.x - transform.position.x) == _attackDirection;
                if (sameSide && PlayerInMeleeRange(Reach, AttackBoxSize.y) && TargetHealth.TryTakeDamage(StrikeDamage))
                    if (StunOnHit) TargetHealth.ApplyStun(.5f);
            }
            if (_attackElapsed < duration) return;
            _isAttacking = false;
            SetAnimation(false);
            _nextAttackTime = Time.time + Mathf.Max(0, AttackCooldown);
        }
        if (PlayerInMeleeRange(Reach * .85f, AttackBoxSize.y))
        {
            Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y);
            if (Time.time >= _nextAttackTime)
            {
                _isAttacking = true;
                _hasStruck = false;
                _attackElapsed = 0;
                _attackDirection = Mathf.Sign(PlayerTarget.position.x - transform.position.x);
                SetAnimation(true);
            }
            return;
        }
        float direction = PlayerTarget.position.x > transform.position.x ? 1 : -1;
        float speed = CurrentSpeed * RuntimeSpeedMultiplier;
        float separation = HorizontalSeparation();
        // Separation must not prevent a body from reaching melee range.
        float velocity = direction * Mathf.Clamp(speed + separation * direction, speed * .5f, speed * MaxSpeedMultiplier);
        Rb.linearVelocity = new Vector2(velocity, Rb.linearVelocity.y);
        Vector2 origin = WallCheckPoint != null ? WallCheckPoint.position : transform.position;
        if (Physics2D.Raycast(origin, Vector2.right * direction, .5f, GroundLayer) && Mathf.Abs(Rb.linearVelocity.y) < .1f)
            Rb.AddForce(Vector2.up * JumpForce, ForceMode2D.Impulse);
    }

    float HorizontalSeparation()
    {
        if (SeparationForce <= 0) return 0;
        var filter = new ContactFilter2D(); filter.SetLayerMask(1 << gameObject.layer);
        int count = Physics2D.OverlapCircle(transform.position, SeparationRadius, filter, _neighbors);
        float push = 0;
        for (int i = 0; i < count; i++)
            if (_neighbors[i] != BodyCollider) push += _neighbors[i].transform.position.x > transform.position.x ? -1 : 1;
        return push * SeparationForce;
    }

    void SetAnimation(bool attacking)
    {
        if (EnemyAnimator == null) return;
        EnemyAnimator.SetBool("isattacking", attacking);
        if (_hasAttackSpeed) EnemyAnimator.SetFloat("AttackSpeed", BaseAttackAnimationDuration / Mathf.Max(.1f, WindupSeconds + RecoverySeconds));
    }

    protected override void Die()
    {
        SetAnimation(false);
        base.Die();
    }
}
