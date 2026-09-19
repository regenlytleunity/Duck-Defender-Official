using UnityEngine;
using System.Collections;

/// <summary>
/// POST-1.4.11 PATCH: null-checked AudioManager call in PerformAttack.
/// 
/// Only difference from the previous version is the AudioManager.Instance null-check
/// at the start of the strike window phase. All other behavior unchanged.
/// </summary>
public class SwarmerEnemy : EnemyBase
{
    [Header("Melee Settings")]
    public float AttackRange = 1.5f;
    public float AttackCooldown = 2.0f;
    public Vector2 AttackBoxSize = new Vector2(1.5f, 1f); 
    public LayerMask PlayerLayer;
    
    [Tooltip("1.4.13 FIX: how much CLOSER than AttackRange the enemy walks before stopping " +
             "to attack. Without this, enemies stop at the outer edge of their range and the " +
             "attack box barely clips the player hitbox, so a still-standing player would never " +
             "actually take damage. With StopDistanceReduction = 0.5, enemies walk 0.5 units " +
             "deeper into the player before stopping, putting the attack box firmly inside the " +
             "player hitbox. Tweak per enemy as needed; usually 0.4-0.6 feels right.")]
    public float StopDistanceReduction = 0.5f;
    
    [Header("1.4.9 - Persistent Strike Window")]
    [Range(0.05f, 0.5f)]
    public float StrikeWindowFraction = 0.2f;
    public float StrikeWindowReachBonus = 0.3f;
    
    [Header("Ground AI")]
    public float JumpForce = 5f;
    public Transform WallCheckPoint;
    public LayerMask GroundLayer;

    [Header("Swarm Physics")]
    public float SeparationRadius = 1.0f;
    public float SeparationForce = 2.0f;
    
    [Header("Speed Limits")]
    public float MaxSpeedMultiplier = 1.3f;
    
    [Header("Animation")]
    public Animator EnemyAnimator;
    public float BaseAttackAnimationDuration = 0.5f;
    [Range(0f, 1f)]
    public float AttackWindupFraction = 0.5f;
    
    private static readonly int AnimIsAttacking = Animator.StringToHash("isattacking");
    private static readonly int AnimAttackSpeed = Animator.StringToHash("AttackSpeed");

    private float _lastAttackTime;
    private bool _isAttacking = false;
    private float _currentAttackAnimDuration;
    private Collider2D _ownCollider;

    public override void Initialize(float wave)
    {
        base.Initialize(wave);
        CurrentSpeed *= Random.Range(0.9f, 1.1f);
        
        if (EnemyAnimator == null) EnemyAnimator = GetComponent<Animator>();
        if (_ownCollider == null) _ownCollider = GetComponent<Collider2D>();
        
        _currentAttackAnimDuration = Mathf.Max(AttackCooldown, 0.2f);
    }

    protected override void Move()
    {
        if (PlayerTarget == null)
        {
            if (_isAttacking)
            {
                StopAllCoroutines();
                _isAttacking = false;
                EndAttackAnimation();
                SpriteRen.color = Color.white;
            }
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, PlayerTarget.position);

        // 1.4.13 FIX: cancel attack only if player has moved well outside the original 
        // attack range. Uses the full AttackRange (not the tightened stop distance) so we 
        // don't ping-pong cancel/start mid-attack when the player wiggles.
        if (_isAttacking && distanceToPlayer > AttackRange * 1.5f)
        {
            StopAllCoroutines();
            _isAttacking = false;
            EndAttackAnimation();
            SpriteRen.color = Color.white;
        }

        if (_isAttacking) return;

        // 1.4.13 FIX: stop CLOSER than AttackRange so the attack box overlaps the player 
        // hitbox properly. The old code stopped at the outer edge of AttackRange, which 
        // meant the attack box barely clipped the player and a still player took no damage.
        float stopDistance = Mathf.Max(0.1f, AttackRange - StopDistanceReduction);

        if (distanceToPlayer > stopDistance)
        {
            float dir = (PlayerTarget.position.x > transform.position.x) ? 1f : -1f;
            float separationPush = GetHorizontalSeparation();
            
            float baseVelocity = dir * CurrentSpeed;
            float finalVelocityX = baseVelocity + separationPush;
            
            float maxSpeed = CurrentSpeed * MaxSpeedMultiplier;
            finalVelocityX = Mathf.Clamp(finalVelocityX, -maxSpeed, maxSpeed);

            Rb.linearVelocity = new Vector2(finalVelocityX, Rb.linearVelocity.y);
            CheckForWalls(dir);
        }
        else
        {
            Rb.linearVelocity = new Vector2(0, Rb.linearVelocity.y); 
            if (Time.time >= _lastAttackTime + AttackCooldown)
            {
                StartCoroutine(PerformAttack());
            }
        }
    }

    private float GetHorizontalSeparation()
    {
        float push = 0f;
        Collider2D[] neighbors = Physics2D.OverlapCircleAll(transform.position, SeparationRadius, 1 << 8);

        foreach (var neighbor in neighbors)
        {
            if (neighbor.gameObject != gameObject)
            {
                if (neighbor.transform.position.x > transform.position.x) push -= 1f;
                else push += 1f;
            }
        }
        return push * SeparationForce;
    }

    IEnumerator PerformAttack()
    {
        _isAttacking = true;
        
        StartAttackAnimation();
        SpriteRen.color = Color.white;
        
        float windupTime = _currentAttackAnimDuration * AttackWindupFraction;
        yield return new WaitForSeconds(windupTime);

        // PATCH: null-check AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Ground_Enemy_Slash");
        }
        
        float strikeWindowDuration = _currentAttackAnimDuration * StrikeWindowFraction;
        float strikeWindowEnd = Time.time + strikeWindowDuration;
        bool hasDamaged = false;
        
        SpriteRen.color = Color.red;
        
        while (Time.time < strikeWindowEnd && !hasDamaged)
        {
            if (TryDealMeleeDamage())
            {
                hasDamaged = true;
                break;
            }
            yield return null;
        }

        float consumedTime = AttackWindupFraction + StrikeWindowFraction;
        float recoveryTime = _currentAttackAnimDuration * Mathf.Max(0f, 1f - consumedTime);
        yield return new WaitForSeconds(recoveryTime);

        SpriteRen.color = Color.white;
        EndAttackAnimation();

        _lastAttackTime = Time.time;
        _isAttacking = false;
    }
    
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
    
    private void StartAttackAnimation()
    {
        if (EnemyAnimator == null) return;
        
        float speedMultiplier = BaseAttackAnimationDuration / _currentAttackAnimDuration;
        speedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 4f);
        EnemyAnimator.SetFloat(AnimAttackSpeed, speedMultiplier);
        
        EnemyAnimator.SetBool(AnimIsAttacking, true);
    }
    
    private void EndAttackAnimation()
    {
        if (EnemyAnimator == null) return;
        EnemyAnimator.SetBool(AnimIsAttacking, false);
    }

    private void CheckForWalls(float direction)
    {
        if (WallCheckPoint == null) return;
        RaycastHit2D hit = Physics2D.Raycast(WallCheckPoint.position, Vector2.right * direction, 0.5f, GroundLayer);
        if (hit.collider != null && Mathf.Abs(Rb.linearVelocity.y) < 0.1f)
        {
            Rb.AddForce(Vector2.up * JumpForce, ForceMode2D.Impulse);
        }
    }
    
    protected override void Die()
    {
        if (EnemyAnimator != null)
        {
            EnemyAnimator.SetBool(AnimIsAttacking, false);
        }
        
        base.Die();
    }
}