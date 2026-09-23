using UnityEngine;
using System.Collections;

/// <summary>
/// 1.4.11 — REBUILT ON TOP OF ORIGINAL.
/// 
/// All the visual/movement logic from the original is preserved exactly:
/// - velocity applied in OnEnable from transform.right (this is what made the original work)
/// - sprite rotation updated every frame in Update() based on velocity (preserves correct facing)
/// - SetVisualScale uses _defaultLocalScale baseline (preserves Initialize-resets-scale behavior)
/// 
/// 1.4.11 ADDITIONS (surgical):
/// - BallisticData.CanAirburst flag (defaults false; WeaponPlayer sets true for normal feathers only)
/// - Airburst callback to WeaponPlayer.SpawnAirburst on enemy hit if CanAirburst is true
/// - All AudioManager.Instance calls null-checked
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [System.Serializable]
    public struct BallisticData
    {
        public int Damage;
        public float DamageMultiplier;
        public float Speed;
        public float Knockback;
        public int PierceCount;
        public int RicochetCount;
        public float HomingSpeed;
        public float CritChance;
        public float ExplosionRadius;

        public float ProximityScaling;
        public float PoisonDamage;
        public float IceSlowFactor;
        public float ProjectileGravity;

        public bool IsHealingFeather;
        public int HealAmount;

        public bool IsFrostyFeather;
        public float FreezeDuration;

        public bool IsPoisonFeather;
        public float PoisonDPS;

        public bool IsMetalFeather;
        public float BonusKnockback;

        public bool IsExplosiveFeather;
        public bool IsBuckshotPellet;

        public float Lifetime;

        // 1.4.11: True only for "regular" player feathers. Triggers airburst on enemy hit
        // if PlayerStats.AirburstFeatherCount > 0. Always false for special feathers, buckshot,
        // turret shots, mini gun shots, and airburst sub-projectiles (prevents infinite cascade).
        public bool CanAirburst;
        public CardAscension Variant;
        public float DamageRatio;
        public bool NonFeather;
        public bool MarkTarget;
        public bool InfinitePierce;
        public int IgnoreEnemyID;
    }

    public BallisticData Stats;

    [Header("Default Lifetime")]
    [Tooltip("Lifetime in seconds if BallisticData.Lifetime is 0 (the standard case).")]
    public float DefaultLifetime = 6.0f;

    [Header("Visual FX")]
    public GameObject HitEffectPrefab;
    public GameObject ExplosionPrefab;
    public Sprite TungstenSprite;
    public Sprite NeedleSprite;
    Sprite _defaultSprite;

    [Header("Sprite Orientation")]
    public float SpriteAngleOffset = -90f;

    [Header("Ricochet Falloff")]
    [Range(0f, 1f)]
    public float RicochetDamageFalloff = 0.5f;

    [Tooltip("Speed multiplier applied each bounce. 1.0 = full speed preserved.")]
    [Range(0f, 1f)]
    public float RicochetSpeedFalloff = 1.0f;

    [Header("Homing Settings")]
    public float HomingForwardBias = 2f;

    private float _lifeTimer;
    private int _pierceLeft;
    private int _ricochetLeft;

    private float _currentDamageMultiplier = 1f;
    private float _currentSpeedMultiplier = 1f;

    private Transform _target;
    private Rigidbody2D _rb;
    private Vector3 _spawnPosition;
    private SpriteRenderer _spriteRenderer;

    private Vector3 _defaultLocalScale = Vector3.one;

    private bool _needsDelayedRetarget = false;

    private System.Collections.Generic.HashSet<int> _hitEnemyIDs = new System.Collections.Generic.HashSet<int>();

    // Runtime-only electric state. Initialize clears it before every pooled reuse.
    private int _electricDamage;
    private int _electricChainCount;
    private float _electricChainRadius;
    private bool _electricHitResolved;
    private WeaponPlayer _electricOwner;
    bool _lingering;
    bool _eruptionSpawned;
    readonly System.Collections.Generic.List<RaycastHit2D> _instantHits = new System.Collections.Generic.List<RaycastHit2D>(32);
    private readonly System.Collections.Generic.List<Collider2D> _electricOverlapResults =
        new System.Collections.Generic.List<Collider2D>(32);

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _defaultSprite = _spriteRenderer.sprite;
        _defaultLocalScale = transform.localScale;
    }

    public void Initialize(BallisticData incomingStats)
    {
        Stats = incomingStats;
        if (_spriteRenderer != null)
            _spriteRenderer.sprite = Stats.Variant == CardAscension.Tungsten && TungstenSprite != null ? TungstenSprite
                : Stats.NonFeather && NeedleSprite != null ? NeedleSprite : _defaultSprite;
        _pierceLeft = Stats.PierceCount;
        _ricochetLeft = Stats.RicochetCount;

        _lifeTimer = (Stats.Lifetime > 0f) ? Stats.Lifetime : DefaultLifetime;

        _spawnPosition = transform.position;
        _hitEnemyIDs.Clear();
        if (Stats.IgnoreEnemyID != 0) _hitEnemyIDs.Add(Stats.IgnoreEnemyID);
        _lingering = false;
        _eruptionSpawned = false;
        _electricDamage = 0;
        _electricChainCount = 0;
        _electricChainRadius = 0f;
        _electricHitResolved = false;
        _electricOwner = null;
        _electricOverlapResults.Clear();

        _target = null;
        _currentDamageMultiplier = 1f;
        _currentSpeedMultiplier = 1f;
        _needsDelayedRetarget = false;

        _rb.gravityScale = Stats.ProjectileGravity;

        // Reset scale to prefab default. SetVisualScale() may be called after to override.
        transform.localScale = _defaultLocalScale;

        if (Stats.HomingSpeed > 0 && !Stats.IsBuckshotPellet)
        {
            FindNearestTargetInFront();

            if (_target == null)
            {
                _needsDelayedRetarget = true;
            }
        }
    }

    /// <summary>
    /// Sets visual scale. Must be called AFTER Initialize() since Initialize resets scale.
    /// Used by WeaponPlayer for buckshot pellets, airburst sub-feathers, mini gun feathers,
    /// and 1.4.11 Big Feathers.
    /// </summary>
    public void SetVisualScale(float scale)
    {
        transform.localScale = _defaultLocalScale * scale;
    }

    void OnEnable()
    {
        // This is the key line that makes the original work:
        // velocity is applied here, AFTER the GameObject is active AND the rotation has been set by WeaponPlayer.
        if (_rb != null && Stats.Speed > 0)
        {
            _rb.linearVelocity = transform.right * Stats.Speed;
        }

        if (_needsDelayedRetarget)
        {
            _needsDelayedRetarget = false;
            StartCoroutine(DelayedTargetAcquisition());
        }
        if (PlayerStats.Instance != null && !Stats.NonFeather && PlayerStats.Instance.HasAscension(CardAscension.QuantumLeap))
            TraceInstant();
    }

    IEnumerator DelayedTargetAcquisition()
    {
        yield return new WaitForSeconds(0.1f);

        if (_target == null && Stats.HomingSpeed > 0 && gameObject.activeInHierarchy)
        {
            FindNearestTargetByProximity();
        }
    }

    void Update()
    {
        if (_lingering) { _lifeTimer -= Time.deltaTime; if (_lifeTimer <= 0) Deactivate(); return; }
        if (Stats.HomingSpeed > 0 && _target != null && !Stats.IsBuckshotPellet)
        {
            if (_target.gameObject.activeInHierarchy)
            {
                Vector2 direction = ((Vector2)_target.position - _rb.position).normalized;
                float currentSpeed = Stats.Speed * _currentSpeedMultiplier;
                Vector2 newVelocity = Vector3.RotateTowards(
                    _rb.linearVelocity,
                    direction,
                    Stats.HomingSpeed * Time.deltaTime,
                    0f
                );
                _rb.linearVelocity = newVelocity.normalized * currentSpeed;
            }
            else
            {
                _target = null;
            }
        }

        // Visual rotation: update sprite rotation every frame from velocity direction.
        // This is what makes the feather visually point where it's flying. PRESERVED FROM ORIGINAL.
        if (_rb.linearVelocity != Vector2.zero)
        {
            float angle = Mathf.Atan2(_rb.linearVelocity.y, _rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle + SpriteAngleOffset, Vector3.forward);
        }

        _lifeTimer -= Time.deltaTime;
        if (_lifeTimer <= 0) Deactivate();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("OutOfBounds"))
        {
            SilentDeactivate();
            return;
        }

        if (_electricChainCount > 0 && !collision.CompareTag("Ground"))
        {
            // Resolve by EnemyBase, so multiple child colliders cannot produce duplicate hits.
            EnemyBase electricTarget = collision.GetComponentInParent<EnemyBase>();
            if (electricTarget != null && electricTarget.isActiveAndEnabled &&
                electricTarget.CompareTag("Enemy") && electricTarget.IsAlive)
                HitElectricChain(electricTarget);
            return;
        }

        if (collision.CompareTag("Enemy"))
        {
            EnemyBase hitOwner = collision.GetComponentInParent<EnemyBase>();
            int enemyID = hitOwner != null ? hitOwner.GetInstanceID() : collision.gameObject.GetInstanceID();
            if (_hitEnemyIDs.Contains(enemyID)) return;

            EnemyBase enemy = hitOwner;
            if (enemy != null)
            {
                _hitEnemyIDs.Add(enemyID);
                HitEnemy(enemy);
            }
        }
        else if (collision.CompareTag("Ground"))
        {
            HandleGroundCollision();
        }
    }

    void HitEnemy(EnemyBase enemy)
    {
        // 1.4.11 PATCH: null-check AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Feather_Hit_Enemy");
        }

        float baseMult = Stats.DamageMultiplier > 0 ? Stats.DamageMultiplier : 1.0f;

        float ratio = (Stats.DamageRatio > 0 ? Stats.DamageRatio : 1f) * _currentDamageMultiplier;
        float damage = PlayerStats.Instance != null
            ? PlayerStats.Instance.CalculateDamage(Stats.Damage, !Stats.NonFeather, ratio, baseMult - 1f)
            : Stats.Damage * baseMult * ratio;
        int baseDamage = Mathf.Max(1, Mathf.RoundToInt(damage));

        int damageToDeal = baseDamage;

        bool isCrit = !Stats.NonFeather && (enemy.IsMarked || Random.value < Stats.CritChance);
        if (isCrit) damageToDeal *= 2;

        if (Stats.MarkTarget) enemy.IsMarked = true;
        enemy.TakeDamage(damageToDeal);

        float knockbackForce = Stats.Knockback;
        if (Stats.IsMetalFeather) knockbackForce += Stats.BonusKnockback;
        enemy.ApplyKnockback(_rb.linearVelocity.normalized * knockbackForce);

        if (Stats.IsFrostyFeather && Stats.FreezeDuration > 0)
        {
            if (Stats.Variant == CardAscension.AbsoluteZero) enemy.ApplyAbsoluteZero(Stats.FreezeDuration);
            else enemy.ApplyFreeze(Stats.FreezeDuration);
        }

        if (Stats.IsPoisonFeather && Stats.PoisonDPS > 0)
        {
            float totalPoisonDamage = Stats.PoisonDPS * 3f;
            if (Stats.Variant == CardAscension.DeadlyToxin) enemy.ApplyDeadlyToxin();
            else enemy.ApplyPoison(totalPoisonDamage);
        }

        if (!Stats.IsPoisonFeather && Stats.PoisonDamage > 0) enemy.ApplyPoison(Stats.PoisonDamage);
        if (!Stats.IsFrostyFeather && Stats.IceSlowFactor > 0) enemy.ApplySlow(Stats.IceSlowFactor);

        if (Stats.IsHealingFeather && Stats.HealAmount > 0)
        {
            PlayerHealth ph = FindFirstObjectByType<PlayerHealth>();
            if (ph != null)
            {
                ph.Heal(Stats.HealAmount);
            }
        }

        SpawnEffect();

        if (Stats.ExplosionRadius > 0)
        {
            Explode();
        }

        // === 1.4.11 AIRBURST ===
        // Spawn airburst sub-feathers behind the enemy on hit, if this is a normal feather
        // and the player has the Airburst upgrade. Only normal feathers airburst (CanAirburst).
        if (Stats.CanAirburst && PlayerStats.Instance != null && PlayerStats.Instance.AirburstFeatherCount > 0)
        {
            WeaponPlayer wp = FindFirstObjectByType<WeaponPlayer>();
            if (wp != null)
            {
                Vector3 incomingDir = _rb.linearVelocity.normalized;
                if (incomingDir.sqrMagnitude < 0.01f) incomingDir = transform.right;
                wp.SpawnAirburst(enemy.transform.position, incomingDir, Stats.Damage, baseMult, enemy.GetInstanceID());
            }
        }

        if (Stats.IsBuckshotPellet)
        {
            Deactivate();
            if (GameUI.Instance != null)
            {
                GameUI.Instance.ShowDamagePopup(enemy.transform.position, damageToDeal, isCrit);
            }
            return;
        }

        if (_lingering || Stats.InfinitePierce) return;
        if (_pierceLeft > 0)
        {
            _pierceLeft--;
        }
        else if (_ricochetLeft > 0)
        {
            _ricochetLeft--;

            ApplyBounceScaling();
            if (_lingering) return;

            Transform nextTarget = FindNearestEnemyExcluding();

            if (nextTarget != null)
            {
                Vector2 bounceDir = ((Vector2)nextTarget.position - _rb.position).normalized;
                _rb.linearVelocity = bounceDir * (Stats.Speed * _currentSpeedMultiplier);
                _target = nextTarget;
            }
            else
            {
                Vector2 v = _rb.linearVelocity;
                float bounceSpeed = Stats.Speed * _currentSpeedMultiplier;
                _rb.linearVelocity = new Vector2(-v.x, Mathf.Abs(v.y) + bounceSpeed * 0.5f).normalized * bounceSpeed;
                _target = null;
            }
        }
        else
        {
            Deactivate();
        }

        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowDamagePopup(enemy.transform.position, damageToDeal, isCrit);
        }
    }

    public void ConfigureElectricChain(int damage, int chainCount, float radius, WeaponPlayer owner)
    {
        _electricDamage = Mathf.Max(1, damage);
        _electricChainCount = Mathf.Clamp(chainCount, 1, 6);
        _electricChainRadius = Mathf.Max(0.1f, radius);
        _electricOwner = owner;
    }

    void HitElectricChain(EnemyBase firstTarget)
    {
        if (_electricHitResolved) return;
        _electricHitResolved = true;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Feather_Hit_Enemy");
        if (Stats.Variant == CardAscension.Supercharged)
        {
            if (_electricOwner != null) _electricOwner.ShowElectricChain(firstTarget.transform.position + Vector3.up * 15, firstTarget.transform.position);
            firstTarget.TakeDamage(Mathf.CeilToInt(firstTarget.MaxHealth));
            Deactivate();
            return;
        }

        EnemyBase target = firstTarget;
        float damage = _electricDamage;
        Vector3 previousPosition = target.transform.position;

        // The initial hit plus up to N ADDITIONAL enemies, all resolved in this call (no travel delay).
        for (int hitIndex = 0; hitIndex <= _electricChainCount && target != null; hitIndex++)
        {
            Vector3 hitPosition = target.transform.position;
            _hitEnemyIDs.Add(target.GetInstanceID());

            if (hitIndex > 0 && _electricOwner != null)
                _electricOwner.ShowElectricChain(previousPosition, hitPosition);

            // Keep the unrounded falloff for the next hop. EnemyBase's damage API uses whole HP.
            int hitDamage = Mathf.Max(1, Mathf.RoundToInt(damage));
            bool critical = target.IsMarked || Random.value < Stats.CritChance;
            if (critical) hitDamage *= 2;
            target.TakeDamage(hitDamage);
            if (GameUI.Instance != null) GameUI.Instance.ShowDamagePopup(hitPosition, hitDamage, false);

            if (hitIndex == _electricChainCount) break;
            previousPosition = hitPosition; // Captured before damage, even if this enemy died.
            target = FindElectricChainTarget(hitPosition);
            damage *= 0.5f;
        }

        SpawnEffect();
        Deactivate();
    }

    EnemyBase FindElectricChainTarget(Vector3 origin)
    {
        // Reuse a growable result list: crowded waves are not truncated by a fixed-size buffer.
        ContactFilter2D filter = new ContactFilter2D().NoFilter();
        Physics2D.OverlapCircle(origin, _electricChainRadius, filter, _electricOverlapResults);
        EnemyBase nearest = null;
        float nearestDistance = _electricChainRadius * _electricChainRadius;

        foreach (var collider in _electricOverlapResults)
        {
            EnemyBase candidate = collider.GetComponentInParent<EnemyBase>();
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsAlive) continue;
            if (!candidate.CompareTag("Enemy") || _hitEnemyIDs.Contains(candidate.GetInstanceID())) continue;

            float distance = ((Vector2)candidate.transform.position - (Vector2)origin).sqrMagnitude;
            if (distance > nearestDistance) continue;
            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    void HandleGroundCollision()
    {
        if (_lingering) return;
        // 1.4.11 PATCH: null-check AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Feather_Hit_Ground");
        }

        if (Stats.IsBuckshotPellet)
        {
            SpawnEffect();
            Deactivate();
            return;
        }

        if (_ricochetLeft > 0)
        {
            _ricochetLeft--;

            ApplyBounceScaling();
            if (_lingering) return;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, -_rb.linearVelocity, 1.0f, LayerMask.GetMask("Ground"));
            if (hit.collider != null)
            {
                Vector2 reflected = Vector2.Reflect(_rb.linearVelocity, hit.normal);
                _rb.linearVelocity = reflected.normalized * (Stats.Speed * _currentSpeedMultiplier);
            }
            else
            {
                Vector2 v = _rb.linearVelocity;
                _rb.linearVelocity = new Vector2(v.x, -v.y).normalized * (Stats.Speed * _currentSpeedMultiplier);
            }
        }
        else
        {
            if (Stats.ExplosionRadius > 0) Explode();
            SpawnEffect();
            Deactivate();
        }
    }

    void Explode()
    {
        float actualRadius = Stats.ExplosionRadius;
        if (Stats.Variant == CardAscension.Volcano && !_eruptionSpawned)
        {
            _eruptionSpawned = true;
            if (PlayerStats.Instance != null) PlayerStats.Instance.GetComponent<AscensionEffects>()?.Erupt(transform.position, actualRadius);
        }

        if (ExplosionPrefab != null)
        {
            GameObject boom = Instantiate(ExplosionPrefab, transform.position, Quaternion.identity);
            boom.transform.localScale = Vector3.one * actualRadius;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, actualRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase e = hit.GetComponent<EnemyBase>();

                float moneyHighMult = (PlayerStats.Instance != null) ? PlayerStats.Instance.GetCurrentMoneyHighMultiplier() : 1.0f;
                float baseMult = Stats.DamageMultiplier > 0 ? Stats.DamageMultiplier : 1.0f;

                int boomDamage = Mathf.CeilToInt(PlayerStats.Instance != null
                    ? PlayerStats.Instance.CalculateDamage(Stats.Damage, true, .5f * _currentDamageMultiplier, baseMult - 1)
                    : Stats.Damage * .5f * baseMult * _currentDamageMultiplier);

                if (e) e.TakeDamage(boomDamage);
            }
        }
    }

    void ApplyBounceScaling()
    {
        if (Stats.Variant == CardAscension.Tungsten)
        {
            if (_ricochetLeft <= 0)
            {
                _lingering = true; _lifeTimer = 1f;
                _rb.linearVelocity = Vector2.zero; _rb.gravityScale = 0;
            }
            return;
        }
        float loss = PlayerStats.Instance != null ? PlayerStats.Instance.RicochetDamageLoss : .5f;
        _currentDamageMultiplier *= 1f - Mathf.Clamp01(loss);
        _currentSpeedMultiplier *= 1.5f;
    }

    void TraceInstant()
    {
        Vector2 origin = transform.position;
        Vector2 direction = transform.right;
        float remaining = Mathf.Max(30, Stats.Speed * _lifeTimer);
        ContactFilter2D filter = new ContactFilter2D().NoFilter();
        for (int segment = 0; segment < 32 && gameObject.activeSelf && !_lingering; segment++)
        {
            Physics2D.Raycast(origin, direction, filter, _instantHits, remaining);
            bool found = false;
            foreach (var hit in _instantHits)
            {
                if (hit.collider == null || hit.collider.gameObject == gameObject) continue;
                var enemy = hit.collider.GetComponentInParent<EnemyBase>();
                bool ground = hit.collider.CompareTag("Ground");
                if (!ground && (enemy == null || !enemy.IsAlive || _hitEnemyIDs.Contains(enemy.GetInstanceID()))) continue;
                transform.position = hit.point;
                _rb.linearVelocity = direction * Stats.Speed * _currentSpeedMultiplier;
                if (ground)
                {
                    if (_ricochetLeft > 0)
                    {
                        _ricochetLeft--; ApplyBounceScaling();
                        direction = Vector2.Reflect(direction, hit.normal);
                    }
                    else { if (Stats.ExplosionRadius > 0) Explode(); Deactivate(); }
                }
                else if (_electricChainCount > 0) HitElectricChain(enemy);
                else
                {
                    _hitEnemyIDs.Add(enemy.GetInstanceID()); HitEnemy(enemy);
                    if (_rb.linearVelocity.sqrMagnitude > 0) direction = _rb.linearVelocity.normalized;
                }
                remaining -= hit.distance + .02f;
                origin = hit.point + direction * .02f;
                found = true;
                break;
            }
            if (!found || remaining <= 0) break;
        }
        if (!_lingering) Deactivate();
    }

    void FindNearestTargetInFront()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float bestScore = Mathf.Infinity;
        GameObject best = null;

        Vector2 forward = transform.right;

        foreach (var e in enemies)
        {
            if (e == null || !e.activeInHierarchy) continue;

            Vector2 toEnemy = (Vector2)e.transform.position - (Vector2)transform.position;
            float distance = toEnemy.magnitude;

            if (distance < 0.001f) continue;

            float alignment = Vector2.Dot(toEnemy.normalized, forward);

            if (alignment < 0f) continue;

            float alignmentPenalty = (1f - alignment) * HomingForwardBias;
            float score = distance + alignmentPenalty;

            if (score < bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        _target = (best != null) ? best.transform : null;
    }

    void FindNearestTargetByProximity()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float closestDist = Mathf.Infinity;
        GameObject closest = null;

        foreach (var e in enemies)
        {
            if (e == null || !e.activeInHierarchy) continue;

            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = e;
            }
        }

        _target = (closest != null) ? closest.transform : null;
    }

    Transform FindNearestEnemyExcluding()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float closestDist = Mathf.Infinity;
        Transform closest = null;

        foreach (var e in enemies)
        {
            if (e == null || !e.activeInHierarchy) continue;
            if (_hitEnemyIDs.Contains(e.GetInstanceID())) continue;

            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = e.transform;
            }
        }

        return closest;
    }

    void SpawnEffect()
    {
        if (HitEffectPrefab != null) Instantiate(HitEffectPrefab, transform.position, Quaternion.identity);
    }

    void Deactivate()
    {
        _target = null;
        gameObject.SetActive(false);
    }

    void SilentDeactivate()
    {
        _target = null;
        gameObject.SetActive(false);
    }

    public void SetTarget(Transform t) => _target = t;

    public void SetColor(Color color)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = color;
        }
    }
}
