using UnityEngine;
using System.Collections;

/// <summary>
/// 1.4.11 ADDS:
/// - Sabotage: enemies spawn with up to 50% of their HP already missing if PlayerStats.EnemyHealthMissingPercent > 0
/// </summary>
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Base Stats")]
    public float BaseSpeed = 3f;
    public float BaseHealth = 2f;
    public int DamageOnHit = 1;

    [Header("Rewards")]
    public int XPValue = 10;
    public GameObject CoinPrefab;
    [Tooltip("Min and Max coins to drop per kill")]
    public Vector2Int CoinDropRange = new Vector2Int(1, 3);

    [Header("Wave Scaling")]
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
    private EnemyHealthBar _healthBarInstance;

    protected bool IsKnockedBack = false;
    public bool IsFrozen { get; private set; } = false;
    private Coroutine _freezeRoutine;

    private bool _isPoisoned = false;
    private int _slowStacks = 0;
    private float _originalSpeed;

    public virtual void Initialize(float waveDifficulty)
    {
        float tierMultiplier = 1f;
        if (WaveManager.Instance != null)
        {
            tierMultiplier = WaveManager.Instance.GetDifficultyScalingMultiplier();
        }

        MaxHealth = Mathf.Round(BaseHealth * (1f + (waveDifficulty * HealthScaling * tierMultiplier)));
        CurrentHealth = MaxHealth;

        // === 1.4.11 SABOTAGE ===
        // If the player has Sabotage, enemies spawn with up to 50% HP missing.
        // Capped at 50% per outline (page 4): "Enemy missing health on spawn should get capped at 50%."
        if (PlayerStats.Instance != null && PlayerStats.Instance.EnemyHealthMissingPercent > 0f)
        {
            float missingPct = Mathf.Clamp(PlayerStats.Instance.EnemyHealthMissingPercent, 0f, 0.5f);
            int missingHP = Mathf.RoundToInt(MaxHealth * missingPct);
            CurrentHealth = Mathf.Max(1, (int)MaxHealth - missingHP);
        }

        _originalSpeed = BaseSpeed * (1f + (waveDifficulty * SpeedScaling));
        CurrentSpeed = _originalSpeed;

        Rb = GetComponent<Rigidbody2D>();
        SpriteRen = GetComponent<SpriteRenderer>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) PlayerTarget = playerObj.transform;

        if (HealthBarPrefab != null)
        {
            GameObject barObj = Instantiate(HealthBarPrefab, transform.position, Quaternion.identity);
            barObj.transform.SetParent(transform.parent);

            _healthBarInstance = barObj.GetComponent<EnemyHealthBar>();
            if (_healthBarInstance != null) _healthBarInstance.Initialize(transform, MaxHealth);
            // 1.4.11: also set the bar to the (potentially reduced) starting HP
            if (_healthBarInstance != null) _healthBarInstance.UpdateHealth(CurrentHealth);
        }
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
        if (PlayerStats.Instance == null) return;
        if (!PlayerStats.Instance.HasSlowingAura) return;
        if (PlayerTarget == null) return;

        float dist = Vector2.Distance(transform.position, PlayerTarget.position);
        if (dist > PlayerStats.Instance.SlowingAuraRadius) return;

        // Inside the slowing aura - apply the slow as a velocity damp
        float slowPct = Mathf.Clamp(PlayerStats.Instance.SlowingAuraSlowPercent, 0f, 0.95f);
        if (Rb != null)
        {
            Rb.linearVelocity = new Vector2(Rb.linearVelocity.x * (1f - slowPct), Rb.linearVelocity.y);
        }
    }

    protected abstract void Move();

    protected void FacePlayer()
    {
        if (PlayerTarget == null) return;

        if (PlayerTarget.position.x > transform.position.x)
            transform.localScale = new Vector3(-1, 1, 1);
        else
            transform.localScale = new Vector3(1, 1, 1);
    }

    public virtual void TakeDamage(int damage)
    {
        if (_isDead) return;

        CurrentHealth -= damage;

        if (_healthBarInstance != null) _healthBarInstance.UpdateHealth(CurrentHealth);

        StartCoroutine(FlashColor(Color.white));

        if (CurrentHealth <= 0) Die();
    }

    public void ApplyKnockback(Vector2 forceVector)
    {
        if (_isDead) return;
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

    public void ApplyPoison(float totalDamage)
    {
        if (_isDead || _isPoisoned) return;
        StartCoroutine(PoisonRoutine(totalDamage));
    }

    public void ApplySlow(float slowFactor)
    {
        if (_isDead) return;
        StartCoroutine(SlowStackRoutine(slowFactor));
    }

    public void ApplyFreeze(float duration)
    {
        if (_isDead) return;
        if (duration <= 0f) return;

        if (_freezeRoutine != null) StopCoroutine(_freezeRoutine);
        _freezeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    IEnumerator FreezeRoutine(float duration)
    {
        IsFrozen = true;
        if (Rb != null) Rb.linearVelocity = Vector2.zero;
        UpdateColor();
        yield return new WaitForSeconds(duration);
        IsFrozen = false;
        _freezeRoutine = null;
        UpdateColor();
    }

    IEnumerator PoisonRoutine(float totalDamage)
    {
        _isPoisoned = true;

        float duration = 3.0f;
        float interval = 0.5f;
        int ticks = Mathf.FloorToInt(duration / interval);
        int damagePerTick = Mathf.CeilToInt(totalDamage / ticks);
        if (damagePerTick < 1) damagePerTick = 1;

        for (int i = 0; i < ticks; i++)
        {
            if (_isDead) break;

            TakeDamage(damagePerTick);
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
            CurrentSpeed = _originalSpeed;
        }
        else
        {
            float multiplier = Mathf.Pow(0.8f, _slowStacks);
            multiplier = Mathf.Max(multiplier, 0.1f);
            CurrentSpeed = _originalSpeed * multiplier;
        }
    }

    private void UpdateColor()
    {
        if (SpriteRen == null) return;

        if (IsFrozen) SpriteRen.color = FrozenColor;
        else if (_isPoisoned) SpriteRen.color = Color.green;
        else if (_slowStacks > 0) SpriteRen.color = Color.cyan;
        else SpriteRen.color = Color.white;
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
        if (_isDead) return;
        _isDead = true;

        if (LevelManager.Instance != null)
            LevelManager.Instance.AddXP(XPValue);

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.RegisterEnemyKill();

        if (CoinPrefab != null)
        {
            int baseAmount = Random.Range(CoinDropRange.x, CoinDropRange.y + 1);

            float mult = 1.0f;
            if (PlayerStats.Instance != null) mult = PlayerStats.Instance.CoinDropMultiplier;

            int finalAmount = Mathf.FloorToInt(baseAmount * mult);
            if (finalAmount < 1) finalAmount = 1;

            for (int i = 0; i < finalAmount; i++)
                Instantiate(CoinPrefab, transform.position, Quaternion.identity);
        }

        if (WaveManager.Instance != null)
            WaveManager.Instance.OnEnemyKilled();

        Destroy(gameObject);
    }
}
