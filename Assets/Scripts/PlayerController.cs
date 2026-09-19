using UnityEngine;
using System.Collections;

/// <summary>
/// 1.4.11 ADDS:
/// - Blink: periodic short instant jumps forward while moving (invulnerable, stops at walls)
/// - Low Gravity: scales base gravity by PlayerStats.PlayerGravityMultiplier
/// - Hypersonic: deals Max Speed Damage on enemy collision when at >=90% max run speed
/// - Fire Trail: spawns damaging trail particles behind the player
/// - Jump-off-enemies: landing on top of enemy resets jump count (side collisions don't)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    [Header("Visual FX")]
    public GameObject CloudBurstPrefab;
    public GameObject GroundSlamPrefab;
    public float MinSlamHeight = 1.5f;
    public Transform FeetPos;

    [Header("Stats - Progression")]
    public int MaxJumps = 2;
    public int MaxDashes = 1;

    [Header("Stats - Movement")]
    [Range(4.0f, 25.0f)] public float MaxRunSpeed = 8.0f;
    [Range(25.0f, 200.0f)] public float Acceleration = 90.0f;
    public float GroundDeceleration = 90.0f;

    [Header("Stats - Abilities")]
    public float DashSpeed = 25f;
    public float DashDuration = 0.2f;
    public float DashCooldown = 0.8f;
    public float JumpForce = 14.0f;

    public float ShockwaveDamage = 0.0f;
    public float ShockwaveRadius = 2.5f;

    [Header("Stats - Tech (Aura)")]
    public AuraController AuraChild;
    public float AuraRadius = 3.0f;
    public float AuraDamage = 0.0f;

    [Header("Stats - Tech (Meteor & Tripleshot)")]
    public GameObject CoinMeteorPrefab;
    public bool HasCoinMeteors = false;
    public int MeteorThreshold = 10;
    public int MeteorDamage = 50;
    public float MeteorRadius = 4.0f;

    public bool HasCoinShot = false;
    public int CoinShotThreshold = 15;
    public float CoinShotDuration = 5.0f;

    [Header("Stats - Physics Feel")]
    public float FallGravityMultiplier = 1.1f;
    public float JumpCutMultiplier = 2.0f;

    [Header("Checks")]
    public Transform GroundCheck;
    public float GroundCheckRadius = 0.2f;
    public LayerMask GroundLayer;

    // ============================================================
    // 1.4.11 BLINK
    // ============================================================

    [Header("1.4.11 - Blink")]
    [Tooltip("Distance the player teleports forward during a blink (world units).")]
    public float BlinkDistance = 1.5f;
    [Tooltip("LayerMask used to stop blink at walls / map borders. Set to Ground layer.")]
    public LayerMask BlinkBlockerLayer;
    [Tooltip("Optional VFX prefab spawned at the start of each blink.")]
    public GameObject BlinkFXPrefab;

    private float _nextBlinkTime = 0f;
    private bool _isBlinking = false;

    // ============================================================
    // 1.4.11 FIRE TRAIL
    // ============================================================

    [Header("1.4.11 - Fire Trail")]
    [Tooltip("Prefab spawned periodically behind the player when HasFireTrail is true. " +
             "Should have a FireTrailPatch component (provided).")]
    public GameObject FireTrailPatchPrefab;
    [Tooltip("Seconds between fire trail patches being dropped.")]
    public float FireTrailDropInterval = 0.2f;

    private float _nextFireTrailDropTime = 0f;

    // ============================================================
    // RUNTIME
    // ============================================================

    private Rigidbody2D _rb;
    private WeaponPlayer _weapon;
    private Animator _animator;

    private Vector2 _moveInput;
    private bool _isFacingRight = false;
    private bool _isGrounded;
    private bool _isDashing;
    private float _defaultGravity;
    private Vector2 _dashDir;
    private Vector2 _dashStartPosition;
    private bool _isDead = false;
    private bool _isStandingOnEnemy = false;  // 1.4.11: tracks if we landed on top of an enemy

    private Coroutine _coinShotCoroutine;
    private float _coinAccumulator;
    private bool _isCoinShotActive = false;

    private float _dashTimeLeft;
    private float _lastDashTime;
    private int _currentJumpCount;
    private int _currentDashCount;

    private float _peakY;

    private static readonly int AnimIsMoving = Animator.StringToHash("isMoving");
    private static readonly int AnimIsIdle = Animator.StringToHash("isIdle");
    private static readonly int AnimIsAttacking = Animator.StringToHash("isAttacking");
    private static readonly int AnimIsDead = Animator.StringToHash("isDead");

    /// <summary>True while the player is invulnerable (during blink or after second wind).</summary>
    public bool IsInvulnerable { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _weapon = GetComponent<WeaponPlayer>();
        _animator = GetComponent<Animator>();
        _defaultGravity = _rb.gravityScale;

        _currentDashCount = MaxDashes;

        if (AuraChild == null) AuraChild = GetComponentInChildren<AuraController>();

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.AuraRadius = AuraRadius;
            PlayerStats.Instance.AuraDamage = AuraDamage;
            PlayerStats.Instance.HasCoinMeteors = HasCoinMeteors;
            PlayerStats.Instance.MeteorThreshold = MeteorThreshold;
            PlayerStats.Instance.MeteorDamage = MeteorDamage;
            PlayerStats.Instance.MeteorRadius = MeteorRadius;
            PlayerStats.Instance.HasTripleshot = HasCoinShot;
            PlayerStats.Instance.TripleshotThreshold = CoinShotThreshold;
            PlayerStats.Instance.TripleshotDuration = CoinShotDuration;
        }

        if (_weapon != null) _weapon.BonusSpreadProjectiles = 0;
    }

    void Update()
    {
        if (_isDead) return;

        if (InputHelper.GetDashDown()) OnDashKeyPressed();

        if (AuraChild != null)
        {
            float rad = (PlayerStats.Instance != null) ? PlayerStats.Instance.AuraRadius : AuraRadius;
            float dmg = (PlayerStats.Instance != null) ? PlayerStats.Instance.AuraDamage : AuraDamage;
            AuraChild.UpdateAura(rad, dmg);
        }

        // 1.4.13: Removed `if (Input.GetKeyDown(KeyCode.M)) SpawnMeteor();` here. That was 
        // a dev-only test shortcut for triggering meteors instantly during balancing, and 
        // it shipped accidentally. Coin meteors should only trigger from the actual coin-
        // threshold path in LevelManager.AddCoins (which still works correctly).

        // Coins-per-second now spawns physical coins via LevelManager.SpawnPassiveCoin()
        if (PlayerStats.Instance != null && PlayerStats.Instance.CoinsPerSecond > 0)
        {
            _coinAccumulator += PlayerStats.Instance.CoinsPerSecond * Time.deltaTime;
            if (_coinAccumulator >= 1.0f)
            {
                int coinsToAdd = Mathf.FloorToInt(_coinAccumulator);
                _coinAccumulator -= coinsToAdd;
                if (LevelManager.Instance != null)
                {
                    for (int i = 0; i < coinsToAdd; i++)
                    {
                        LevelManager.Instance.SpawnPassiveCoin(transform.position);
                    }
                }
            }
        }

        float xInput = InputHelper.GetHorizontal();
        float yInput = InputHelper.GetVertical();
        _moveInput = new Vector2(xInput, 0);

        CheckGround();
        HandleJump();
        HandleAttackAnimation();

        // 1.4.11: Blink check (must be moving)
        HandleBlink(xInput);

        // 1.4.11: Fire Trail (drop patches while moving)
        HandleFireTrail(xInput);

        if (xInput > 0 && !_isFacingRight) Flip();
        else if (xInput < 0 && _isFacingRight) Flip();

        if (_isDashing) HandleDashPhysics();

        // 1.4.13 FIX: gate the down+jump ground-slam behind the Shockwave upgrade.
        // The slam launches the player downward and spawns the shockwave VFX on impact, 
        // so without the upgrade it visually pretended to deal damage but didn't 
        // (PerformShockwaveDamage is gated on ShockwaveDamage > 0, but the prefab 
        // spawned regardless). Now the whole slam routine only runs if the player 
        // actually has the upgrade that grants it.
        bool hasShockwaveUpgrade = PlayerStats.Instance != null &&
                                   PlayerStats.Instance.HasShockwaveDownDash;
        if (hasShockwaveUpgrade && !_isGrounded && yInput < -0.5f && InputHelper.GetJumpDown())
            StartCoroutine(GroundSlamRoutine());

        UpdateAnimationState();
    }

    void FixedUpdate()
    {
        if (_isDead) return;
        if (_isDashing) return;
        ApplyMovement();
        ApplyGravityModifiers();
    }

    // ============================================================
    // 1.4.11 BLINK
    // ============================================================

    void HandleBlink(float xInput)
    {
        if (PlayerStats.Instance == null || !PlayerStats.Instance.HasBlink) return;
        if (_isBlinking) return;
        if (Mathf.Abs(xInput) < 0.1f) return; // only blinks while moving
        if (Time.time < _nextBlinkTime) return;

        StartCoroutine(BlinkRoutine(xInput));
    }

    IEnumerator BlinkRoutine(float xInput)
    {
        _isBlinking = true;
        IsInvulnerable = true;

        float blinkDirection = xInput > 0 ? 1f : -1f;
        Vector3 targetPos = transform.position + new Vector3(BlinkDistance * blinkDirection, 0, 0);

        // Wall check: raycast in the blink direction, stop at first wall hit
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            Vector2.right * blinkDirection,
            BlinkDistance,
            BlinkBlockerLayer
        );

        if (hit.collider != null)
        {
            // Stop just before the wall
            targetPos = (Vector2)transform.position + Vector2.right * blinkDirection * (hit.distance - 0.1f);
        }

        // VFX at start position
        if (BlinkFXPrefab != null)
        {
            Instantiate(BlinkFXPrefab, transform.position, Quaternion.identity);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Player_Dash");
        }

        transform.position = targetPos;

        // Player is invulnerable for BlinkDuration seconds
        float duration = PlayerStats.Instance.BlinkDuration;
        yield return new WaitForSeconds(duration);

        IsInvulnerable = false;
        _isBlinking = false;

        // Schedule next blink
        _nextBlinkTime = Time.time + PlayerStats.Instance.BlinkInterval;
    }

    // ============================================================
    // 1.4.11 FIRE TRAIL
    // ============================================================

    void HandleFireTrail(float xInput)
    {
        if (PlayerStats.Instance == null || !PlayerStats.Instance.HasFireTrail) return;
        if (FireTrailPatchPrefab == null) return;
        if (Mathf.Abs(xInput) < 0.1f && _isGrounded == false) return; // need movement or air motion
        if (Time.time < _nextFireTrailDropTime) return;

        Vector3 dropPos = FeetPos != null ? FeetPos.position : transform.position;
        GameObject patch = Instantiate(FireTrailPatchPrefab, dropPos, Quaternion.identity);
        FireTrailPatch ft = patch.GetComponent<FireTrailPatch>();
        if (ft != null)
        {
            ft.Initialize(
                PlayerStats.Instance.FireTrailDamage,
                PlayerStats.Instance.FireTrailDuration
            );
        }

        _nextFireTrailDropTime = Time.time + FireTrailDropInterval;
    }

    // ============================================================
    // DASH
    // ============================================================

    private void OnDashKeyPressed()
    {
        if (_isDead || _isDashing) return;
        if (Time.time < _lastDashTime + DashCooldown) return;
        if (!CanPlayerDash()) return;

        bool isShockwaveDash = PlayerStats.Instance != null &&
                               PlayerStats.Instance.HasShockwaveDownDash &&
                               !PlayerStats.Instance.HasFullDash &&
                               MaxDashes <= 0;

        if (!isShockwaveDash && _currentDashCount <= 0) return;

        bool downwardOnly = isShockwaveDash;
        float x = InputHelper.GetHorizontal();
        float y = InputHelper.GetVertical();
        Vector2 dashDirection = new Vector2(x, y).normalized;

        if (downwardOnly) dashDirection = Vector2.down;
        else if (dashDirection == Vector2.zero)
            dashDirection = new Vector2(_isFacingRight ? 1f : -1f, 0f);

        if (!isShockwaveDash) _currentDashCount--;

        StartCoroutine(DashRoutine(dashDirection));
    }

    private void UpdateAnimationState()
    {
        if (_animator == null || _isDead) return;
        bool isMoving = Mathf.Abs(_moveInput.x) > 0.1f;
        _animator.SetBool(AnimIsMoving, isMoving);
        _animator.SetBool(AnimIsIdle, !isMoving);
    }

    private void HandleAttackAnimation()
    {
        if (_animator == null) return;
        _animator.SetBool(AnimIsAttacking, InputHelper.GetShootHeld());
    }

    public void TriggerDeathAnimation()
    {
        _isDead = true;
        if (_animator != null)
        {
            _animator.SetBool(AnimIsDead, true);
            _animator.SetBool(AnimIsMoving, false);
            _animator.SetBool(AnimIsIdle, false);
            _animator.SetBool(AnimIsAttacking, false);
        }
    }

    public void TriggerCoinShotBuff()
    {
        // Renamed to Tripleshot internally, kept legacy entry point
        if (_weapon != null) _weapon.TriggerTripleshot();
    }

    public void SpawnMeteor()
    {
        if (CoinMeteorPrefab == null) return;
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Vector3 targetPos = enemies.Length > 0
            ? enemies[Random.Range(0, enemies.Length)].transform.position
            : transform.position;
        Instantiate(CoinMeteorPrefab, targetPos + new Vector3(0, 10, 0), Quaternion.identity);
    }

    void HandleDashPhysics()
    {
        _dashTimeLeft -= Time.deltaTime;
        if (_dashDir.y < -0.1f && _isGrounded)
        {
            float dist = Vector2.Distance(_dashStartPosition, transform.position);
            if (dist > MinSlamHeight)
            {
                if (GroundSlamPrefab != null) Instantiate(GroundSlamPrefab, GroundCheck.position, Quaternion.identity);
                if (ShockwaveDamage > 0) PerformShockwaveDamage();
            }
            EndDash();
        }
        else if (_dashTimeLeft <= 0) EndDash();
    }

    void PerformShockwaveDamage()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Shockwave_Ground_Impact");

        Collider2D[] hits = Physics2D.OverlapCircleAll(GroundCheck.position, ShockwaveRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    enemy.TakeDamage(Mathf.CeilToInt(ShockwaveDamage));
                    Vector2 dir = (enemy.transform.position - GroundCheck.position).normalized;
                    enemy.ApplyKnockback(dir * 10f);
                }
            }
        }
    }

    private void HandleJump()
    {
        if (InputHelper.GetJumpDown())
        {
            // 1.4.11: standing on an enemy counts as grounded for jump purposes
            if (_isGrounded || _isStandingOnEnemy || _currentJumpCount < MaxJumps) Jump();
        }
    }

    private void Jump()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Player_Jump");

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0);
        _rb.AddForce(Vector2.up * JumpForce, ForceMode2D.Impulse);
        _currentJumpCount++;
        if (!_isGrounded && CloudBurstPrefab != null && FeetPos != null)
            Instantiate(CloudBurstPrefab, FeetPos.position, Quaternion.identity);
    }

    private bool CanPlayerDash()
    {
        if (MaxDashes > 0) return true;
        if (PlayerStats.Instance != null && PlayerStats.Instance.HasShockwaveDownDash) return true;
        return false;
    }

    private IEnumerator DashRoutine(Vector2 direction)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Player_Dash");

        _isDashing = true;
        _lastDashTime = Time.time;
        _dashTimeLeft = DashDuration;
        _dashStartPosition = transform.position;
        _dashDir = direction;
        _rb.gravityScale = 0;
        _rb.linearVelocity = _dashDir * DashSpeed;
        yield return null;
    }

    private void EndDash()
    {
        _isDashing = false;
        _rb.gravityScale = _defaultGravity;
        _rb.linearVelocity = Vector2.zero;
    }

    private IEnumerator GroundSlamRoutine()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale = 0;
        yield return new WaitForSeconds(0.1f);
        _rb.gravityScale = _defaultGravity * 2;
        _rb.AddForce(Vector2.down * DashSpeed * 2, ForceMode2D.Impulse);
        while (!_isGrounded) yield return null;
        if (ShockwaveDamage > 0) PerformShockwaveDamage();
        if (GroundSlamPrefab != null) Instantiate(GroundSlamPrefab, GroundCheck.position, Quaternion.identity);
        _rb.gravityScale = _defaultGravity;
    }

    private void ApplyMovement()
    {
        float targetSpeed = _moveInput.x * MaxRunSpeed;
        float speedDif = targetSpeed - _rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Acceleration : GroundDeceleration;
        if (!_isGrounded) accelRate *= 0.8f;
        _rb.AddForce(speedDif * accelRate * Vector2.right, ForceMode2D.Force);
    }

    /// <summary>
    /// 1.4.11: Applies PlayerStats.PlayerGravityMultiplier as a multiplicative factor
    /// on top of the existing fall/jump-cut modifiers.
    /// </summary>
    private void ApplyGravityModifiers()
    {
        float playerGravMult = PlayerStats.Instance != null ? PlayerStats.Instance.PlayerGravityMultiplier : 1f;

        if (_rb.linearVelocity.y < 0)
            _rb.gravityScale = _defaultGravity * FallGravityMultiplier * playerGravMult;
        else if (_rb.linearVelocity.y > 0 && !InputHelper.GetJumpHeld())
            _rb.gravityScale = _defaultGravity * JumpCutMultiplier * playerGravMult;
        else
            _rb.gravityScale = _defaultGravity * playerGravMult;
    }

    private void CheckGround()
    {
        bool wasGrounded = _isGrounded;
        _isGrounded = Physics2D.OverlapCircle(GroundCheck.position, GroundCheckRadius, GroundLayer);

        // 1.4.11: also check for enemies underfoot for the "jump off enemies" mechanic.
        // Standing-on-enemy is true when an enemy collider overlaps the GroundCheck point AND
        // we're moving downward or stationary (so side collisions don't count).
        _isStandingOnEnemy = CheckStandingOnEnemy();

        if ((_isGrounded || _isStandingOnEnemy) && !wasGrounded)
        {
            _peakY = transform.position.y;
            _currentJumpCount = 0;
            _currentDashCount = MaxDashes;
        }
    }

    /// <summary>
    /// True if the player is genuinely standing on TOP of an enemy (not just colliding from the side).
    /// 
    /// 1.4.11 PATCH: the previous version used OverlapCircle on GroundCheck which counted 
    /// side collisions as "standing on" because enemy colliders are tall - the enemy's lower 
    /// half overlapped the player's GroundCheck (at the feet) when running into one laterally.
    /// 
    /// New logic: check via a SHORT downward raycast from the player's center. This only 
    /// returns true if there's an enemy directly underneath, which is what "standing on" 
    /// actually means.
    /// </summary>
    bool CheckStandingOnEnemy()
    {
        if (GroundCheck == null) return false;
        // Must be moving downward or stationary - upward motion means we're not landing.
        if (_rb.linearVelocity.y > 0.1f) return false;

        // Cast a short ray straight down from the player's GroundCheck.
        // We pass ~no layer mask filter; we'll filter results by Enemy tag.
        // Distance is small so we only count enemies the player is genuinely on top of.
        float castDistance = GroundCheckRadius * 1.5f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(GroundCheck.position, Vector2.down, castDistance);

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            // Skip self
            if (hit.collider.gameObject == gameObject) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (!hit.collider.CompareTag("Enemy")) continue;
            
            // Found an enemy directly below the player. Standing on it.
            return true;
        }

        return false;
    }

    /// <summary>
    /// 1.4.11: Detect "Hypersonic" hits - when player runs into an enemy at >= 90% max speed,
    /// deal MaxSpeedDamage to that enemy.
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (PlayerStats.Instance == null) return;
        if (PlayerStats.Instance.MaxSpeedDamage <= 0) return;

        if (!collision.collider.CompareTag("Enemy")) return;

        float currentSpeed = Mathf.Abs(_rb.linearVelocity.x);
        if (currentSpeed < MaxRunSpeed * 0.9f) return;

        EnemyBase enemy = collision.collider.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            int dmg = PlayerStats.Instance.MaxSpeedDamage;
            enemy.TakeDamage(dmg);

            // Knock the enemy away from the player
            Vector2 awayDir = (enemy.transform.position - transform.position).normalized;
            enemy.ApplyKnockback(awayDir * 5f);

            if (GameUI.Instance != null)
            {
                GameUI.Instance.ShowDamagePopup(enemy.transform.position, dmg, true);
            }
        }
    }

    private void Flip()
    {
        _isFacingRight = !_isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    private void OnDrawGizmos()
    {
        if (GroundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GroundCheck.position, GroundCheckRadius);
            if (ShockwaveDamage > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(GroundCheck.position, ShockwaveRadius);
            }
        }
    }
}