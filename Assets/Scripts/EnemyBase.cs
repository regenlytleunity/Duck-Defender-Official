using UnityEngine;
using System.Collections;

/// <summary>
/// 1.4.11 ADDS:
/// - Sabotage: enemies spawn with up to 50% of their HP already missing if PlayerStats.EnemyHealthMissingPercent > 0
/// </summary>
public abstract class EnemyBase : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<EnemyBase> ActiveEnemies = new System.Collections.Generic.List<EnemyBase>();
    protected PlayerStats DamageOwner;
    PlayerStats _poisonOwner, _toxinOwner;
    float _nextRetarget;
    public bool IsMarked { get; set; }
    public float HealthRemaining => CurrentHealth;
    float _permanentSlow;
    float _zoneSlow;
    float _zoneSlowUntil;
    float _fractionalDamage;
    bool _absoluteZero;
    void OnEnable() { if (!ActiveEnemies.Contains(this)) ActiveEnemies.Add(this); }
    protected virtual void OnDisable() { ActiveEnemies.Remove(this); }

    // Damage/death callbacks can disable enemies and mutate the registry synchronously.
    // Callers that deal area damage keep their own reusable snapshot, without per-tick allocations.
    public static void CopyActiveEnemies(System.Collections.Generic.List<EnemyBase> destination)
    {
        destination.Clear();
        destination.AddRange(ActiveEnemies);
    }

    public static EnemyBase Nearest(Vector2 position, EnemyBase exclude = null)
    {
        EnemyBase nearest = null;
        float distance = float.PositiveInfinity;
        foreach (var enemy in ActiveEnemies)
        {
            if (enemy == null || enemy == exclude || !enemy.IsAlive) continue;
            float d = ((Vector2)enemy.transform.position - position).sqrMagnitude;
            if (d < distance) { distance = d; nearest = enemy; }
        }
        return nearest;
    }

    public void TakeFractionalDamage(float damage, PlayerStats owner = null)
    {
        if (!CanTakeDamage) return;
        _fractionalDamage += Mathf.Max(0, damage);
        int whole = Mathf.FloorToInt(_fractionalDamage + .00001f);
        if (whole <= 0) return;
        _fractionalDamage -= whole;
        TakeDamage(whole, owner);
    }

    public void ApplyZoneSlow(float fraction, float seconds = .3f)
    {
        if (Time.time >= _zoneSlowUntil) _zoneSlow = 0;
        _zoneSlow = Mathf.Max(_zoneSlow, Mathf.Clamp01(fraction));
        _zoneSlowUntil = Time.time + seconds;
    }

    public void Defeat(PlayerStats owner = null) { if (CanTakeDamage) { DamageOwner = owner; RecordDamage(CurrentHealth); Die(); } }
    [Header("Enemy Variants")]
    public bool IsElite;
    [Min(1)] public float EliteSizeMultiplier = 1.15f;
    public Color EliteTint = new Color(.65f, .65f, .7f);
    [Tooltip("Off-screen spawns are protected until their sprite first enters the gameplay camera. No-camera fallback in seconds.")]
    [Min(0)] public float SpawnProtectionSeconds = 3f;
    public bool IsSpawnProtected { get; private set; }
    public bool CanTakeDamage => IsAlive && !IsSpawnProtected;
    public TankEnemy Protector { get; internal set; }
    public Collider2D BodyCollider { get; private set; }
    protected PlayerHealth TargetHealth;
    protected Collider2D TargetCollider;
    protected Camera GameplayCamera;
    Color _normalColor = Color.white;
    float _spawnProtectionUntil;
    Vector3 _spawnScale;
    public virtual string EnemyKind => this is TankEnemy ? "tank" : this is LobberEnemy ? "lobber" :
        this is BuzzerEnemy || this is FlyingEnemy ? "flying" : "ground";
    public string TipID => (IsElite ? "elite_" : "") + EnemyKind;
    protected virtual float HealthAtWave(int wave) => Mathf.Floor(BaseHealth + WaveManager.HealthIncreaseAtWave(wave));
    protected virtual float EliteHealthMultiplier => EnemyKind == "ground" ? 1.5f : EnemyKind == "lobber" ? 1.25f : 1f;
    protected virtual float SpeedAtSpawn => BaseSpeed * (IsElite && EnemyKind == "ground" ? .85f : 1f);
    [Header("Base Stats")]
    public float BaseSpeed = 3f;
    public float BaseHealth = 2f;
    public int DamageOnHit = 1;

    [Header("Rewards")]
    public int XPValue = 10;
    public GameObject CoinPrefab;
    [Tooltip("Min and Max coins to drop per kill")]
    public Vector2Int CoinDropRange = new Vector2Int(4, 6);

    [Header("Legacy Scaling (unused; health uses the wave table)")]
    public float HealthScaling = 0.1f;
    public float SpeedScaling = 0.05f;

    [Header("UI")]
    public GameObject HealthBarPrefab;

    [Header("Knockback Tuning")]
    public float KnockbackLinearThreshold = 4f;
    public float KnockbackSoftCapBonus = 8f;
    public float KnockbackSoftCapScale = 6f;
    [Range(0f, 1f)]
    public float KnockbackVerticalDamping = 0.2f;

    [Header("Freeze Visuals")]
    public Color FrozenColor = new Color(0.5f, 0.9f, 1f);

    public float MaxHealth { get; private set; }
    public bool IsAlive => !_isDead && CurrentHealth > 0f;

    protected float CurrentSpeed;
    protected float CurrentHealth;
    protected Transform PlayerTarget;
    protected Rigidbody2D Rb;
    protected SpriteRenderer SpriteRen;

    private bool _isDead = false;
    protected EnemyHealthBar HealthBarInstance;

    protected bool IsKnockedBack = false;
    public bool IsFrozen { get; private set; } = false;
    private Coroutine _freezeRoutine;

    private bool _isPoisoned = false;
    private int _slowStacks = 0;
    private float _originalSpeed;

    // Transform-driven fliers must use the same slow rules as Rigidbody movers.
    protected float MovementSpeedFactor
    {
        get
        {
            float factor = _originalSpeed > 0 ? CurrentSpeed / _originalSpeed : 1f;
            if (Time.time < _zoneSlowUntil) factor *= 1f - _zoneSlow;
            return factor * PlayerStats.ProjectileSpeedFactor(transform.position);
        }
    }

    public virtual void Initialize(float waveDifficulty)
    {
        MaxHealth = Mathf.Max(1, Mathf.Floor(HealthAtWave(Mathf.Max(1, (int)waveDifficulty)) * (IsElite ? EliteHealthMultiplier : 1f))) * GameDifficulty.HealthMultiplier;
        CurrentHealth = MaxHealth;

        // === 1.4.11 SABOTAGE ===
        // If the player has Sabotage, enemies spawn with up to 50% HP missing.
        // Capped at 50% per outline (page 4): "Enemy missing health on spawn should get capped at 50%."
        if (LocalCoopSession.TeamSabotage() > 0f)
        {
            float missingPct = Mathf.Clamp(LocalCoopSession.TeamSabotage(), 0f, 0.95f);
            int missingHP = Mathf.RoundToInt(MaxHealth * missingPct);
            CurrentHealth = Mathf.Max(1, (int)MaxHealth - missingHP);
        }

        _originalSpeed = SpeedAtSpawn * GameDifficulty.SpeedMultiplier;
        CurrentSpeed = _originalSpeed;

        Rb = GetComponent<Rigidbody2D>();
        SpriteRen = GetComponent<SpriteRenderer>();
        BodyCollider = GetComponent<Collider2D>();
        GameplayCamera = Camera.main;
        _spawnScale = transform.localScale * (IsElite ? EliteSizeMultiplier : 1f);
        transform.localScale = _spawnScale;
        _normalColor = SpriteRen != null ? SpriteRen.color : Color.white;
        if (IsElite) _normalColor *= EliteTint;
        UpdateColor();
        IsSpawnProtected = SpawnProtectionSeconds > 0 && !IsInView(false);
        _spawnProtectionUntil = Time.time + SpawnProtectionSeconds;

        var nearestPlayer = LocalCoopSession.NearestAlive(transform.position);
        GameObject playerObj = nearestPlayer != null ? nearestPlayer.gameObject : null;
        if (playerObj != null)
        {
            PlayerTarget = playerObj.transform;
            TargetHealth = playerObj.GetComponent<PlayerHealth>();
            TargetCollider = playerObj.GetComponent<Collider2D>();
        }

        if (HealthBarPrefab != null)
        {
            GameObject barObj = Instantiate(HealthBarPrefab, transform.position, Quaternion.identity);
            barObj.transform.SetParent(transform.parent);

            HealthBarInstance = barObj.GetComponent<EnemyHealthBar>();
            if (HealthBarInstance != null) HealthBarInstance.Initialize(transform, MaxHealth);
            // 1.4.11: also set the bar to the (potentially reduced) starting HP
            if (HealthBarInstance != null) HealthBarInstance.UpdateHealth(CurrentHealth);
        }
    }

    protected virtual void Update()
    {
        if (!IsAlive) return;
        if (Time.time >= _nextRetarget || TargetHealth == null || TargetHealth.IsDead)
        {
            _nextRetarget = Time.time + .2f;
            var target = LocalCoopSession.NearestAlive(transform.position);
            PlayerTarget = target != null ? target.transform : null;
            TargetHealth = target != null ? target.GetComponent<PlayerHealth>() : null;
            TargetCollider = target != null ? target.GetComponent<Collider2D>() : null;
        }
        if (IsSpawnProtected && (IsInView(false) || (GameplayCamera == null && Time.time >= _spawnProtectionUntil)))
            IsSpawnProtected = false;
        if (EnemyTipUI.Instance != null) EnemyTipUI.Instance.Observe(this);
    }

    public bool IsInView(bool fully)
    {
        if (GameplayCamera == null) return false;
        Bounds bounds = SpriteRen != null ? SpriteRen.bounds : BodyCollider != null ? BodyCollider.bounds : new Bounds(transform.position, Vector3.zero);
        Vector3 min = GameplayCamera.WorldToViewportPoint(bounds.min);
        Vector3 max = GameplayCamera.WorldToViewportPoint(bounds.max);
        if (min.z <= 0 || max.z <= 0) return false;
        return fully ? min.x >= 0 && max.x <= 1 && min.y >= 0 && max.y <= 1 :
            max.x >= .01f && min.x <= .99f && max.y >= .01f && min.y <= .99f;
    }

    // Collider-edge distance agrees with solid-body contact, even when the player stands still.
    protected bool PlayerInMeleeRange(float reach, float height)
    {
        if (TargetHealth == null || TargetHealth.IsDead || TargetCollider == null || BodyCollider == null) return false;
        Bounds own = BodyCollider.bounds, target = TargetCollider.bounds;
        float horizontalGap = Mathf.Max(0, Mathf.Abs(own.center.x - target.center.x) - own.extents.x - target.extents.x);
        float verticalGap = Mathf.Max(0, Mathf.Abs(own.center.y - target.center.y) - own.extents.y - target.extents.y);
        return horizontalGap <= Mathf.Max(0, reach) && verticalGap <= Mathf.Max(.05f, height * .25f);
    }

    void FixedUpdate()
    {
        if (_isDead || IsKnockedBack) return;

        if (IsFrozen)
        {
            if (Rb != null) Rb.linearVelocity = Vector2.zero;
            return;
        }

        Move();
        FacePlayer();
        if (Rb != null && Time.time < _zoneSlowUntil) Rb.linearVelocity *= 1f - _zoneSlow;

        // 1.4.11: Apply slowing aura passively if player has it
        ApplySlowingAuraIfNearby();
    }

    /// <summary>
    /// 1.4.11: Slowing Aura is a passive zone around the player that slows enemies inside.
    /// Implemented here in EnemyBase rather than a separate aura controller so every enemy 
    /// type respects it automatically.
    /// </summary>
    void ApplySlowingAuraIfNearby()
    {
        float factor = PlayerStats.ProjectileSpeedFactor(transform.position);
        if (Rb != null) Rb.linearVelocity = new Vector2(Rb.linearVelocity.x * factor, Rb.linearVelocity.y);
    }

    protected abstract void Move();

    protected void FacePlayer()
    {
        if (PlayerTarget == null) return;

        if (PlayerTarget.position.x > transform.position.x)
            transform.localScale = new Vector3(-Mathf.Abs(_spawnScale.x), _spawnScale.y, _spawnScale.z);
        else
            transform.localScale = new Vector3(Mathf.Abs(_spawnScale.x), _spawnScale.y, _spawnScale.z);
    }

    public virtual void TakeDamage(int damage) { TakeDamage(damage, PlayerStats.Instance); }
    public void TakeDamage(int damage, PlayerStats owner)
    {
        if (!CanTakeDamage || damage <= 0) return;
        DamageOwner = owner;
        ReceiveDamage(damage);
    }
    void RecordDamage(float amount)
    {
        var player = DamageOwner != null ? DamageOwner.GetComponent<LocalPlayer>() : null;
        if (player != null) player.DamageDealt += Mathf.Max(0, amount);
    }

    protected virtual void ReceiveDamage(float damage)
    {
        if (!CanTakeDamage || damage <= 0) return;
        if (Protector != null && Protector.CanTakeDamage && Protector != this)
        {
            float redirected = damage * .5f;
            damage -= redirected;
            Protector.AbsorbDamage(redirected, DamageOwner);
        }
        ApplyHealthDamage(damage);
    }

    protected void ApplyHealthDamage(float damage)
    {
        if (!CanTakeDamage || damage <= 0) return;
        RecordDamage(Mathf.Min(CurrentHealth, damage));
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        if (HealthBarInstance != null) HealthBarInstance.UpdateHealth(CurrentHealth);
        if (gameObject.activeInHierarchy) StartCoroutine(FlashColor(Color.white));
        if (CurrentHealth <= 0) Die();
    }

    public void ApplyKnockback(Vector2 forceVector)
    {
        if (!CanTakeDamage || Rb == null) return;
        if (IsFrozen) return;

        float rawKnockback = forceVector.magnitude;
        if (rawKnockback < 0.001f) return;

        Vector2 direction = forceVector.normalized;
        direction = new Vector2(direction.x, direction.y * KnockbackVerticalDamping);

        if (direction.sqrMagnitude > 0.001f)
        {
            direction = direction.normalized;
        }
        else
        {
            if (PlayerTarget != null)
            {
                direction = new Vector2(Mathf.Sign(transform.position.x - PlayerTarget.position.x), 0f);
            }
            else
            {
                direction = Vector2.right;
            }
        }

        float finalMagnitude;
        if (rawKnockback <= KnockbackLinearThreshold)
        {
            finalMagnitude = rawKnockback;
        }
        else
        {
            float excess = rawKnockback - KnockbackLinearThreshold;
            float bonusKnockback = KnockbackSoftCapBonus * (excess / (excess + KnockbackSoftCapScale));
            finalMagnitude = KnockbackLinearThreshold + bonusKnockback;
        }

        Vector2 finalForce = direction * finalMagnitude;

        Rb.linearVelocity = Vector2.zero;
        Rb.AddForce(finalForce, ForceMode2D.Impulse);

        StartCoroutine(KnockbackRoutine());
    }

    public void ApplyPoison(float totalDamage, PlayerStats owner = null)
    {
        if (!CanTakeDamage || _isPoisoned) return;
        _poisonOwner = owner != null ? owner : PlayerStats.Instance;
        StartCoroutine(PoisonRoutine(totalDamage));
    }

    Coroutine _toxinRoutine;
    float _toxinUntil;
    public void ApplyDeadlyToxin(PlayerStats owner = null)
    {
        if (!CanTakeDamage) return;
        _toxinOwner = owner != null ? owner : PlayerStats.Instance;
        _toxinUntil = Time.time + 3;
        if (_toxinRoutine == null) _toxinRoutine = StartCoroutine(DeadlyToxinRoutine());
    }
    IEnumerator DeadlyToxinRoutine()
    {
        while (IsAlive && Time.time < _toxinUntil - .001f)
        {
            yield return new WaitForSeconds(1);
            if (!IsAlive) break;
            if (Random.value < .1f) Nearest(transform.position, this)?.ApplyDeadlyToxin(_toxinOwner);
            TakeFractionalDamage(_toxinOwner != null ? _toxinOwner.CalculateDamage(5, false) : 5, _toxinOwner);
        }
        _toxinRoutine = null;
    }

    public void ApplySlow(float slowFactor)
    {
        if (!CanTakeDamage) return;
        StartCoroutine(SlowStackRoutine(slowFactor));
    }

    public void ApplyFreeze(float duration)
    {
        if (!CanTakeDamage) return;
        if (duration <= 0f) return;

        if (_freezeRoutine != null) StopCoroutine(_freezeRoutine);
        _freezeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    public void ApplyAbsoluteZero(float duration)
    {
        if (!CanTakeDamage) return;
        _absoluteZero = true;
        ApplyFreeze(duration);
    }

    IEnumerator FreezeRoutine(float duration)
    {
        IsFrozen = true;
        if (Rb != null) Rb.linearVelocity = Vector2.zero;
        UpdateColor();
        yield return new WaitForSeconds(duration);
        IsFrozen = false;
        if (_absoluteZero) { _permanentSlow = .75f; RecalculateSlowedSpeed(); }
        _freezeRoutine = null;
        UpdateColor();
    }

    IEnumerator PoisonRoutine(float totalDamage)
    {
        _isPoisoned = true;

        float duration = 3.0f;
        float interval = 0.5f;
        int ticks = Mathf.FloorToInt(duration / interval);
        float damagePerTick = totalDamage / 3f * interval;

        for (int i = 0; i < ticks; i++)
        {
            if (_isDead) break;

            float damage = _poisonOwner != null ? _poisonOwner.CalculateDamage(damagePerTick, false) : damagePerTick;
            TakeFractionalDamage(damage, _poisonOwner);
            UpdateColor();
            yield return new WaitForSeconds(0.1f);
            UpdateColor();
            yield return new WaitForSeconds(interval - 0.1f);
        }

        _isPoisoned = false;
        UpdateColor();
    }

    IEnumerator SlowStackRoutine(float slowFactor)
    {
        _slowStacks++;
        RecalculateSlowedSpeed();
        UpdateColor();

        yield return new WaitForSeconds(3.0f);

        _slowStacks--;
        RecalculateSlowedSpeed();
        UpdateColor();
    }

    private void RecalculateSlowedSpeed()
    {
        if (_slowStacks <= 0)
        {
            _slowStacks = 0;
            CurrentSpeed = _originalSpeed * (1f - _permanentSlow);
        }
        else
        {
            float multiplier = Mathf.Pow(0.8f, _slowStacks);
            multiplier = Mathf.Max(multiplier, 0.1f);
            CurrentSpeed = _originalSpeed * Mathf.Min(multiplier, 1f - _permanentSlow);
        }
    }

    protected void UpdateColor()
    {
        if (SpriteRen == null) return;

        if (IsFrozen) SpriteRen.color = FrozenColor;
        else if (_isPoisoned) SpriteRen.color = Color.green;
        else if (_slowStacks > 0) SpriteRen.color = Color.cyan;
        else SpriteRen.color = _normalColor;
    }

    IEnumerator KnockbackRoutine()
    {
        IsKnockedBack = true;
        yield return new WaitForSeconds(0.2f);
        IsKnockedBack = false;
        if (Rb != null) Rb.linearVelocity = Vector2.zero;
    }

    IEnumerator FlashColor(Color flashColor)
    {
        if (SpriteRen != null)
        {
            Color before = SpriteRen.color;
            SpriteRen.color = flashColor;
            yield return new WaitForSeconds(0.1f);
            UpdateColor();
        }
    }

    protected virtual void Die()
    {
        if (!BeginDeath()) return;
        CompleteDeath();
    }

    protected bool BeginDeath()
    {
        if (_isDead) return false;
        _isDead = true;
        var killer = DamageOwner != null ? DamageOwner.GetComponent<LocalPlayer>() : null;
        if (killer != null) killer.Kills++;
        CurrentHealth = 0;
        ActiveEnemies.Remove(this);
        StopAllCoroutines();
        CancelInvoke();
        if (Protector != null) Protector.OnProtectedEnemyKilled(this);
        Protector = null;
        if (HealthBarInstance != null) Destroy(HealthBarInstance.gameObject);
        return true;
    }

    protected void CompleteDeath()
    {

        if (DamageOwner != null && DamageOwner.HasAscension(CardAscension.Vampire))
            DamageOwner.GetComponent<AscensionEffects>()?.DropHealingOrb(transform.position);

        if (LevelManager.Instance != null)
            LevelManager.Instance.AddXP(XPValue * (IsElite ? 1.25f : 1f));

        if (DamageOwner != null)
            DamageOwner.RegisterEnemyKill();

        if (CoinPrefab != null)
        {
            int baseAmount = Random.Range(CoinDropRange.x, CoinDropRange.y + 1);

            float mult = 1.0f;
            if (DamageOwner != null) mult = DamageOwner.CoinDropMultiplier;

            int finalAmount = Mathf.FloorToInt(baseAmount * mult * (IsElite ? 2f : 1f));
            if (finalAmount < 1) finalAmount = 1;

            for (int i = 0; i < finalAmount; i++)
                Instantiate(CoinPrefab, transform.position, Quaternion.identity);
        }

        if (WaveManager.Instance != null)
            WaveManager.Instance.OnEnemyKilled();

        Destroy(gameObject);
    }
}
