using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 1.4.11 REWRITE + POST-RELEASE PATCHES.
/// 
/// PATCHES (post-release):
/// 1. Aim direction now computed from FirePoint.position rather than transform.position.
///    Old behavior: aim was correct at long range but increasingly inaccurate near the player,
///    because the bullet spawned at FirePoint but aimed from player center, so the offset
///    skewed close-range shots. Now both the angle source and spawn position match.
/// 2. All AudioManager.Instance calls are null-checked.
/// </summary>
public class WeaponPlayer : MonoBehaviour
{
    [Header("Gun Stats")]
    public Transform FirePoint;
    public float FireRate = 0.2f;

    [Header("Ballistic Stats (Modified by Cards)")]
    public Projectile.BallisticData CurrentStats;

    [Header("Spread Configuration")]
    public int ParallelProjectiles = 1;
    public float ParallelSpacing = 0.3f;

    [Header("Parallel Spacing Fix")]
    public float MinParallelDistance = 0.25f;

    [Header("Ground-Aware Parallel Stacking")]
    public LayerMask GroundLayer;
    public float GroundClearanceDistance = 0.5f;

    public int SpreadProjectiles = 0;
    public float SpreadAngleStep = 10f;

    [Header("Sprite Orientation")]
    public float ProjectileSpriteOffset = -90f;

    [Header("Special Feather Visuals")]
    public Color NormalFeatherColor = Color.white;
    public Color HealingFeatherColor = Color.green;
    public Color FrostyFeatherColor = new Color(0.5f, 0.85f, 1f);
    public Color PoisonFeatherColor = new Color(0.5f, 1f, 0.3f);
    public Color MetalFeatherColor = new Color(0.7f, 0.7f, 0.75f);
    public Color ExplosiveFeatherColor = new Color(1f, 0.5f, 0.2f);
    public Color BuckshotFeatherColor = new Color(0.9f, 0.8f, 0.5f);

    [Header("Electric Feathers")]
    public Color ElectricFeatherColor = new Color(0.5f, 0.9f, 1f);
    [Min(0.1f)] public float ElectricChainRadius = 4f;
    [Tooltip("Optional LineRenderer prefab. Damage works without it; instances are reused for chain links.")]
    public LineRenderer ElectricChainEffectPrefab;
    [Min(0.01f)] public float ElectricChainEffectDuration = 0.08f;

    private readonly List<LineRenderer> _electricChainLines = new List<LineRenderer>();
    private readonly List<float> _electricChainExpirations = new List<float>();

    [Header("Airburst Visuals (1.4.11)")]
    public Color AirburstFeatherColor = new Color(1f, 0.85f, 0.4f);
    public float AirburstConeAngle = 60f;
    public float AirburstSpeedMultiplier = 1.0f;
    public float AirburstLifetime = 0.3f;
    [Range(0.1f, 1f)]
    public float AirburstScale = 0.5f;

    [Header("Buckshot Configuration")]
    public float BuckshotConeAngle = 45f;
    public float BuckshotSpeedMultiplier = 1.2f;
    public float BuckshotLifetime = 0.2f;
    [Range(0.1f, 1f)]
    public float BuckshotPelletScale = 0.5f;

    [Header("Mini Gun (1.4.11)")]
    public Color MiniGunFeatherColor = new Color(0.85f, 0.85f, 1f);
    [Range(0.3f, 1f)]
    public float MiniGunFeatherScale = 0.7f;
    public GameObject OverheatPopupPrefab;

    [Header("Special Feather Stagger")]
    [Range(0f, 0.2f)]
    public float SpecialFeatherStaggerDelay = 0.04f;

    [HideInInspector] public int BonusSpreadProjectiles = 0;

    float _baseFireInterval;
    float _fireReduction;
    public Vector2 AimDirection => ComputeAimDir();
    public float EffectiveFireInterval => Mathf.Max(.005f, FireRate / (PlayerStats.Instance != null ? PlayerStats.Instance.BeneficialStatMultiplier : 1f));
    public void ApplyFireRateReduction(float fraction)
    {
        if (_baseFireInterval <= 0) _baseFireInterval = FireRate;
        _fireReduction += fraction;
        FireRate = Mathf.Max(.005f, _baseFireInterval * (1f - Mathf.Clamp(_fireReduction, 0f, .99f)));
    }
    public float FeatherDamage(float fraction = 1f)
    {
        return PlayerStats.Instance != null ? PlayerStats.Instance.CalculateDamage(CurrentStats.Damage, true, fraction, Mathf.Max(1, CurrentStats.DamageMultiplier) - 1)
            : CurrentStats.Damage * fraction;
    }
    private float _nextNormalFireTime;
    private float _nextMiniGunFireTime;
    private Camera _mainCam;

    // === Mini Gun overheat state ===
    private float _miniGunHeat = 0f;
    private bool _miniGunOverheated = false;
    bool _miniGunStarted;
    private float _lastMiniGunFireTime = -999f;

    public float MiniGunHeatNormalized
    {
        get
        {
            if (PlayerStats.Instance == null || PlayerStats.Instance.MiniGunOverheatThreshold <= 0) return 0f;
            return Mathf.Clamp01(_miniGunHeat / PlayerStats.Boost(PlayerStats.Instance.MiniGunOverheatThreshold));
        }
    }

    public bool MiniGunOverheated => _miniGunOverheated;

    private Coroutine _tripleshotCoroutine;
    private bool _isTripleshotActive = false;

    void Start()
    {
        _mainCam = Camera.main;
        _baseFireInterval = FireRate;
        _nextNormalFireTime = _nextMiniGunFireTime = Time.time;

        if (CurrentStats.Speed == 0) CurrentStats.Speed = 20f;
        if (CurrentStats.Damage == 0) CurrentStats.Damage = 1;
        if (CurrentStats.DamageMultiplier <= 0) CurrentStats.DamageMultiplier = 1.0f;

        BonusSpreadProjectiles = 0;
    }

    void Update()
    {
        UpdateElectricChainVisuals();
        if (Time.timeScale == 0) return;
        bool shootHeld = InputHelper.GetShootHeld();

        if (!shootHeld) _nextNormalFireTime = Time.time;
        int shotsThisFrame = 0;
        while (shootHeld && Time.time >= _nextNormalFireTime && shotsThisFrame++ < 16)
        {
            ShootNormal();
            _nextNormalFireTime += EffectiveFireInterval;
        }
        if (shotsThisFrame > 16) _nextNormalFireTime = Time.time + EffectiveFireInterval;

        if (PlayerStats.Instance != null && PlayerStats.Instance.HasMiniGun)
        {
            if (!_miniGunStarted) { _nextMiniGunFireTime = Time.time; _miniGunStarted = true; }
            UpdateMiniGun(shootHeld);
        }
    }

    /// <summary>
    /// PATCH: Returns the world-space source position for aim calculations.
    /// Now uses FirePoint instead of player transform so the aim direction matches
    /// where bullets actually spawn from. This fixes inaccurate shots when the
    /// mouse cursor is near the player.
    /// </summary>
    Vector3 GetAimSourcePosition()
    {
        return FirePoint != null ? FirePoint.position : transform.position;
    }

    /// <summary>
    /// PATCH: Computes aim angle from current mouse position relative to FirePoint.
    /// </summary>
    float ComputeAimAngle()
    {
        if (_mainCam == null) _mainCam = Camera.main;
        if (_mainCam == null) return 0f;

        Vector3 mouseScreen = InputHelper.GetMousePosition();
        Vector3 mouseWorld = _mainCam.ScreenToWorldPoint(mouseScreen);
        mouseWorld.z = 0;

        Vector3 source = GetAimSourcePosition();
        Vector3 aimDir = (mouseWorld - source);
        aimDir.z = 0;

        if (aimDir.sqrMagnitude < 0.0001f) return 0f;

        return Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
    }

    Vector3 ComputeAimDir()
    {
        if (_mainCam == null) _mainCam = Camera.main;
        if (_mainCam == null) return Vector3.right;

        Vector3 mouseScreen = InputHelper.GetMousePosition();
        Vector3 mouseWorld = _mainCam.ScreenToWorldPoint(mouseScreen);
        mouseWorld.z = 0;

        Vector3 source = GetAimSourcePosition();
        Vector3 aimDir = (mouseWorld - source);
        aimDir.z = 0;

        if (aimDir.sqrMagnitude < 0.0001f) return Vector3.right;
        return aimDir.normalized;
    }

    // ============================================================
    // MINI GUN
    // ============================================================

    void UpdateMiniGun(bool shootHeld)
    {
        var stats = PlayerStats.Instance;
        if (stats == null) return;

        float threshold = Mathf.Max(0.5f, PlayerStats.Boost(stats.MiniGunOverheatThreshold));
        float recoveryRate = Mathf.Max(0.1f, PlayerStats.Cooldown(stats.MiniGunRecoveryRate));

        bool isFiringNow = shootHeld && !_miniGunOverheated;
        if (!isFiringNow)
        {
            _nextMiniGunFireTime = Time.time;
            float coolPerSec = threshold / recoveryRate;
            _miniGunHeat -= coolPerSec * Time.deltaTime;
            if (_miniGunHeat < 0) _miniGunHeat = 0;

            if (_miniGunOverheated && _miniGunHeat <= 0f)
            {
                _miniGunOverheated = false;
            }
        }

        if (_miniGunOverheated) return;
        if (!shootHeld) return;
        _miniGunHeat += Time.deltaTime;
        if (_miniGunHeat >= threshold) { TriggerOverheat(); return; }

        float baseInterval = EffectiveFireInterval * 0.5f;
        float heatPercent = _miniGunHeat / threshold;
        float intervalScale = 1f + (heatPercent * 2f);
        float currentInterval = baseInterval * intervalScale;

        int shotsThisFrame = 0;
        while (Time.time >= _nextMiniGunFireTime && shotsThisFrame++ < 16)
        {
            FireMiniGunFeather();
            _nextMiniGunFireTime += currentInterval;
            _lastMiniGunFireTime = Time.time;
        }
        if (shotsThisFrame > 16) _nextMiniGunFireTime = Time.time + currentInterval;
    }

    void TriggerOverheat()
    {
        _miniGunOverheated = true;
        _miniGunHeat = PlayerStats.Boost(PlayerStats.Instance.MiniGunOverheatThreshold);

        if (OverheatPopupPrefab != null)
        {
            GameObject popup = Instantiate(
                OverheatPopupPrefab,
                transform.position + Vector3.up * 1.5f,
                Quaternion.identity
            );
        }
        else if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowDamagePopup(transform.position + Vector3.up * 1.5f, 0, true);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Button_Error");
        }
    }

    void FireMiniGunFeather()
    {
        if (FirePoint == null) return;
        // 1.4.13: Mini Gun shares the Player_Shoot SFX. Plays once per individual 
        // mini gun feather, so a continuous mini-gun burst becomes a rapid stream 
        // of shot sounds (which sounds great with slice randomization to avoid 
        // hearing the same sample on repeat).
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Player_Shoot");
        }

        float angle = ComputeAimAngle();

        GameObject bulletObj = ObjectPooler.Instance != null ? ObjectPooler.Instance.GetPooledObject() : null;
        if (bulletObj == null) return;

        bulletObj.transform.position = FirePoint.position;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);

        Projectile p = bulletObj.GetComponent<Projectile>();
        if (p == null) return;

        p.SpriteAngleOffset = ProjectileSpriteOffset;

        Projectile.BallisticData stats = CurrentStats;
        stats.Damage = CurrentStats.Damage;
        stats.DamageRatio = .5f;
        stats.PierceCount = 0;
        stats.RicochetCount = 0;
        stats.ExplosionRadius = 0;
        stats.ProximityScaling = 0;
        stats.PoisonDamage = 0;
        stats.IceSlowFactor = 0;
        stats.ProjectileGravity = 0;
        stats.IsHealingFeather = false;
        stats.IsFrostyFeather = false;
        stats.IsPoisonFeather = false;
        stats.IsMetalFeather = false;
        stats.IsExplosiveFeather = false;
        stats.IsBuckshotPellet = false;
        stats.CanAirburst = true;
        stats.Lifetime = 0;

        p.Initialize(stats);
        p.SetColor(MiniGunFeatherColor);
        p.SetVisualScale(MiniGunFeatherScale * GetCurrentFeatherSize());
        bulletObj.SetActive(true);
        for (int i = 1; i < PlayerStats.BoostCount(ParallelProjectiles); i++)
            SpawnNormalFeather(angle, Vector3.up * (ParallelSpacing * i), true);
        OnAttackFired(angle);
    }

    // ============================================================
    // NORMAL SHOOTING
    // ============================================================

    void ShootNormal()
    {
        // 1.4.13: Play player shoot SFX. Hooked here (the entry point for the normal 
        // fire path) so it plays once per volley regardless of how many parallel/spread 
        // projectiles get spawned. AudioManager handles random slice selection if the 
        // Player_Shoot sound has slicing configured.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Player_Shoot");
        }

        float baseAngle = ComputeAimAngle();
        Vector3 aimDir = ComputeAimDir();

        if (FireNormalPattern(baseAngle, aimDir)) OnAttackFired(baseAngle);
    }

    // A volley or a minigun shot counts once. Bonus feathers and turrets never recurse.
    void OnAttackFired(float angle)
    {
        var ready = TickAndCollectReadySpecials();
        var others = new List<PlayerStats.SpecialFeatherInstance>();
        foreach (var feather in ready)
        {
            if (feather.Type == PlayerStats.FeatherType.Buckshot) FireBuckshotCone(angle, feather);
            else others.Add(feather);
        }
        if (others.Count == 1) SpawnSpecialFeather(angle, Vector3.zero, others[0]);
        else if (others.Count > 1) StartCoroutine(FireStaggeredSpecials(others, angle));
        TickElectricFeathers(angle);
        if (PlayerStats.Instance != null && PlayerStats.Instance.HasAscension(CardAscension.DivineDuplicator)) FireDivineFeather();
    }

    List<PlayerStats.SpecialFeatherInstance> TickAndCollectReadySpecials()
    {
        var ready = new List<PlayerStats.SpecialFeatherInstance>();
        if (PlayerStats.Instance == null) return ready;

        foreach (var feather in PlayerStats.Instance.SpecialFeathers)
        {
            // Electric feathers count successful attacks, including the separate Mini Gun path.
            if (feather.Type == PlayerStats.FeatherType.Electric) continue;
            if (feather.Threshold <= 0) continue;
            feather.ShotCounter++;
            if (feather.ShotCounter >= PlayerStats.Threshold(feather.Threshold))
            {
                feather.ShotCounter = 0;
                ready.Add(feather);
            }
        }
        return ready;
    }

    IEnumerator FireStaggeredSpecials(List<PlayerStats.SpecialFeatherInstance> specials, float originalBaseAngle)
    {
        for (int i = 0; i < specials.Count; i++)
        {
            float angle = originalBaseAngle;
            if (i > 0)
            {
                yield return new WaitForSeconds(SpecialFeatherStaggerDelay);
                // Re-aim for each staggered shot since the mouse may have moved
                angle = ComputeAimAngle();
            }

            SpawnSpecialFeather(angle, Vector3.zero, specials[i]);
        }
    }

    bool FireNormalPattern(float baseAngle, Vector3 aimDir)
    {
        if (FirePoint == null) return false;
        if (_isTripleshotActive && PlayerStats.Instance != null && PlayerStats.Instance.HasAscension(CardAscension.DoubleDown))
            return FireRandomVolley();
        bool fired = false;
        Vector3 perpendicular = new Vector3(-aimDir.y, aimDir.x, 0).normalized;
        float effectiveSpacing = Mathf.Max(ParallelSpacing, MinParallelDistance);

        int parallelCount = PlayerStats.BoostCount(ParallelProjectiles);

        List<Vector3> parallelOffsets = CalculateGroundAwareParallelOffsets(
            FirePoint.position,
            perpendicular,
            effectiveSpacing,
            parallelCount
        );

        int totalSpread = PlayerStats.BoostCount(SpreadProjectiles + BonusSpreadProjectiles);

        List<float> spreadAngles = new List<float>();
        for (int i = 1; i <= totalSpread; i++)
        {
            float sign = (i % 2 == 0) ? 1f : -1f;
            int tier = Mathf.CeilToInt(i / 2.0f);
            spreadAngles.Add(sign * tier * SpreadAngleStep);
        }

        for (int i = 0; i < parallelOffsets.Count; i++)
            fired |= SpawnNormalFeather(baseAngle, parallelOffsets[i], i > 0);

        foreach (float angleOffset in spreadAngles)
        {
            fired |= SpawnNormalFeather(baseAngle + angleOffset, Vector3.zero);
        }
        return fired;
    }

    List<Vector3> CalculateGroundAwareParallelOffsets(Vector3 firePointPos, Vector3 perpendicular, float spacing, int count)
    {
        List<Vector3> offsets = new List<Vector3>();

        float featherSize = GetCurrentFeatherSize();
        float adjustedClearance = GroundClearanceDistance * Mathf.Max(1f, featherSize);

        if (count <= 1)
        {
            // 1.4.11 PATCH: even a single feather needs ground-clearance protection 
            // when feather size is large. If the spawn point would clip the ground, 
            // offset upward.
            Vector3 offset = GetGroundClearanceOffset(firePointPos, adjustedClearance);
            offsets.Add(offset);
            return offsets;
        }

        float adjustedSpacing = spacing * Mathf.Max(1f, featherSize * 0.85f);

        List<Vector3> symmetricOffsets = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            float offsetMultiplier = i - (count - 1) / 2.0f;
            symmetricOffsets.Add(perpendicular * (offsetMultiplier * adjustedSpacing));
        }

        bool wouldClipGround = false;
        foreach (Vector3 offset in symmetricOffsets)
        {
            Vector3 spawnPos = firePointPos + offset;
            RaycastHit2D hit = Physics2D.Raycast(spawnPos, Vector2.down, adjustedClearance, GroundLayer);
            if (hit.collider != null)
            {
                wouldClipGround = true;
                break;
            }
        }

        if (!wouldClipGround)
        {
            offsets.AddRange(symmetricOffsets);
        }
        else
        {
            float stackDirection = perpendicular.y >= 0 ? 1f : -1f;
            for (int i = 0; i < count; i++)
            {
                offsets.Add(perpendicular * (i * adjustedSpacing * stackDirection));
            }
        }

        return offsets;
    }

    /// <summary>
    /// 1.4.11 PATCH: returns a Y-offset that lifts the feather above the ground if the spawn 
    /// position would be too close to it. Used for single-feather spawns where there's no 
    /// parallel-stacking fallback to handle ground clipping.
    /// </summary>
    Vector3 GetGroundClearanceOffset(Vector3 spawnPos, float clearance)
    {
        RaycastHit2D hit = Physics2D.Raycast(spawnPos, Vector2.down, clearance, GroundLayer);
        if (hit.collider == null) return Vector3.zero;
        
        // Lift the spawn point so the feather has at least `clearance` of room above the ground
        float distanceToGround = hit.distance;
        float liftAmount = clearance - distanceToGround;
        return new Vector3(0, liftAmount + 0.05f, 0); // small extra margin
    }

    float GetCurrentFeatherSize()
    {
        if (PlayerStats.Instance == null) return 1f;
        return Mathf.Max(0.5f, PlayerStats.Instance.FeatherSize + PlayerStats.Instance.RebirthStatBonus);
    }

    // === Spawn helpers ===

    bool SpawnNormalFeather(float angle, Vector3 positionOffset, bool duplicate = false)
    {
        if (ObjectPooler.Instance == null) return false;
        GameObject bulletObj = ObjectPooler.Instance.GetPooledObject();
        if (bulletObj == null) return false;

        bulletObj.transform.position = FirePoint.position + positionOffset;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);

        Projectile p = bulletObj.GetComponent<Projectile>();
        if (p == null) return false;

        p.SpriteAngleOffset = ProjectileSpriteOffset;

        Projectile.BallisticData stats = CurrentStats;

        if (PlayerStats.Instance != null && PlayerStats.Instance.HomingProjectiles)
        {
            stats.HomingSpeed = PlayerStats.Instance.HomingSpeedBonus;
        }

        stats.IsHealingFeather = false;
        stats.IsFrostyFeather = false;
        stats.IsPoisonFeather = false;
        stats.IsMetalFeather = false;
        stats.IsExplosiveFeather = false;
        stats.IsBuckshotPellet = false;
        stats.CanAirburst = true;
        stats.PoisonDamage = 0;
        stats.IceSlowFactor = 0;
        stats.ExplosionRadius = 0;
        stats.ProjectileGravity = PlayerStats.Instance != null && PlayerStats.Instance.FeatherSize > 1 ? 1 : 0;
        stats.DamageRatio = 1f;
        stats.LocalDamageBonus = duplicate && PlayerStats.Instance != null ? -PlayerStats.Instance.DuplicatorDamageReduction : 0f;
        stats.Lifetime = 0;

        p.Initialize(stats);
        p.SetColor(NormalFeatherColor);
        p.SetVisualScale(GetCurrentFeatherSize());
        bulletObj.SetActive(true);
        return true;
    }

    // ============================================================
    // ELECTRIC FEATHERS
    // ============================================================

    void TickElectricFeathers(float angle)
    {
        if (PlayerStats.Instance == null) return;

        foreach (var feather in PlayerStats.Instance.SpecialFeathers)
        {
            if (feather.Type != PlayerStats.FeatherType.Electric || feather.Threshold <= 0) continue;

            int threshold = PlayerStats.Threshold(feather.Threshold);
            feather.ShotCounter = Mathf.Min(feather.ShotCounter + 1, threshold);
            if (feather.ShotCounter >= threshold && SpawnElectricFeather(angle, feather))
                feather.ShotCounter = 0;
            // If the pool is exhausted, keep one pending activation until an attack can emit it.
        }
    }

    bool SpawnElectricFeather(float angle, PlayerStats.SpecialFeatherInstance feather)
    {
        if (FirePoint == null || ObjectPooler.Instance == null) return false;
        GameObject bulletObj = ObjectPooler.Instance.GetPooledObject();
        if (bulletObj == null) return false;
        Projectile projectile = bulletObj.GetComponent<Projectile>();
        if (projectile == null) return false;

        bulletObj.transform.position = FirePoint.position;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);
        projectile.SpriteAngleOffset = ProjectileSpriteOffset;

        // Same non-critical damage formula as a normal feather, sampled now rather than on impact.
        int damage = Mathf.Max(1, Mathf.RoundToInt(FeatherDamage()));

        Projectile.BallisticData stats = new Projectile.BallisticData();
        stats.Damage = damage;
        stats.DamageMultiplier = 1f;
        stats.Speed = CurrentStats.Speed;
        stats.CritChance = CurrentStats.CritChance;
        stats.Variant = PlayerStats.Instance.HasAscension(CardAscension.Supercharged) ? CardAscension.Supercharged : CardAscension.None;
        // No pierce/ricochet, secondary statuses, airburst or independent critical rolls on this chain.
        projectile.Initialize(stats);
        projectile.ConfigureElectricChain(damage, feather.ElectricChainCount, ElectricChainRadius, this);
        projectile.SetColor(ElectricFeatherColor);
        projectile.SetVisualScale(GetCurrentFeatherSize());
        bulletObj.SetActive(true);
        return true;
    }

    public void FireNeedle(Vector2 direction)
    {
        var stats = new Projectile.BallisticData { Damage = 5, DamageMultiplier = 1, Speed = CurrentStats.Speed,
            NonFeather = true, InfinitePierce = true };
        Emit(transform.position, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, stats, Color.white, .5f);
    }

    void FireDivineFeather()
    {
        var enemy = EnemyBase.Nearest(transform.position);
        Vector3 target = enemy != null ? enemy.transform.position : transform.position + (Vector3)AimDirection * 5;
        var stats = CurrentStats;
        stats.DamageRatio = .5f; stats.CanAirburst = true; stats.ProjectileGravity = 0;
        Emit(target + Vector3.up * 10, -90, stats, Color.white, GetCurrentFeatherSize());
    }

    bool FireRandomVolley()
    {
        bool fired = false;
        for (int i = 0; i < PlayerStats.BoostCount(6); i++)
        {
            var stats = CurrentStats;
            stats.DamageRatio = Random.Range(.25f, 2f);
            stats.Speed *= Random.Range(.5f, 2f);
            stats.HomingSpeed = Random.Range(0f, 3f);
            stats.PierceCount = Random.Range(0, 7); stats.RicochetCount = Random.Range(0, 7);
            stats.CanAirburst = true;
            float angle = Random.Range(-180f, 180f);
            if (PlayerController.Instance != null && PlayerController.Instance.IsGrounded && Mathf.Sin(angle * Mathf.Deg2Rad) < -.95f)
                angle = -angle;
            fired |= Emit(FirePoint.position, angle, stats, NormalFeatherColor, GetCurrentFeatherSize());
        }
        return fired;
    }

    public bool Emit(Vector3 position, float angle, Projectile.BallisticData stats, Color color, float scale = 1)
    {
        if (ObjectPooler.Instance == null) return false;
        var bullet = ObjectPooler.Instance.GetPooledObject();
        if (bullet == null) return false;
        var projectile = bullet.GetComponent<Projectile>();
        if (projectile == null) return false;
        bullet.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
        projectile.SpriteAngleOffset = ProjectileSpriteOffset;
        projectile.Initialize(stats); projectile.SetColor(color); projectile.SetVisualScale(scale);
        if (stats.Variant == CardAscension.Supercharged)
            projectile.ConfigureElectricChain(Mathf.RoundToInt(FeatherDamage()), 1, ElectricChainRadius, this);
        bullet.SetActive(true);
        return true;
    }

    public void FireTurretElement(Vector3 position, Transform target, PlayerStats.FeatherType type, bool ascended, ElementalTurret turret)
    {
        var stats = new Projectile.BallisticData { Damage = CurrentStats.Damage, DamageMultiplier = CurrentStats.DamageMultiplier,
            DamageRatio = .5f, Speed = turret.ProjectileSpeed, CritChance = CurrentStats.CritChance, CanAirburst = true };
        var owned = PlayerStats.Instance.SpecialFeathers.Find(f => f.Type == type);
        Color color = NormalFeatherColor;
        switch (type)
        {
            case PlayerStats.FeatherType.Frosty:
                stats.IsFrostyFeather = true; stats.FreezeDuration = ascended ? 2.25f : owned != null ? owned.FreezeDuration : turret.DefaultFreezeDuration; color = turret.FrostyColor;
                if (ascended) stats.Variant = CardAscension.AbsoluteZero; break;
            case PlayerStats.FeatherType.Metal:
                stats.IsMetalFeather = true; stats.ProjectileGravity = 1; stats.BonusKnockback = owned != null ? owned.BonusKnockback : turret.DefaultMetalKnockback; stats.DamageRatio = ascended ? 4 : .5f;
                color = turret.MetalColor;
                if (ascended) { stats.Variant = CardAscension.Tungsten; stats.RicochetCount = 3; } break;
            case PlayerStats.FeatherType.Poison:
                stats.IsPoisonFeather = true; stats.PoisonDPS = owned != null ? owned.PoisonDPS : turret.DefaultPoisonDPS; color = turret.PoisonColor;
                if (ascended) stats.Variant = CardAscension.DeadlyToxin; break;
            case PlayerStats.FeatherType.Explosive:
                stats.IsExplosiveFeather = true; stats.ExplosionRadius = ascended ? 6 : owned != null ? owned.ExplosionRadius : turret.DefaultExplosionRadius; color = turret.ExplosiveColor;
                if (ascended) stats.Variant = CardAscension.Volcano; break;
            case PlayerStats.FeatherType.Electric:
                stats.Variant = CardAscension.Supercharged; color = ElectricFeatherColor; break;
        }
        Vector2 dir = target.position - position;
        Emit(position, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, stats, color);
    }

    public void ShowElectricChain(Vector3 from, Vector3 to)
    {
        if (ElectricChainEffectPrefab == null || !isActiveAndEnabled) return;

        int index = 0;
        while (index < _electricChainLines.Count && _electricChainLines[index].enabled) index++;
        if (index == _electricChainLines.Count)
        {
            LineRenderer line = Instantiate(ElectricChainEffectPrefab, transform);
            line.gameObject.SetActive(true);
            _electricChainLines.Add(line);
            _electricChainExpirations.Add(0f);
        }

        LineRenderer effect = _electricChainLines[index];
        effect.useWorldSpace = true;
        effect.loop = false;
        effect.positionCount = 5;
        Vector3 direction = to - from;
        Vector3 bend = new Vector3(-direction.y, direction.x, 0f).normalized * Mathf.Min(0.12f, direction.magnitude * 0.1f);
        effect.SetPosition(0, from);
        effect.SetPosition(1, Vector3.Lerp(from, to, 0.25f) + bend);
        effect.SetPosition(2, Vector3.Lerp(from, to, 0.5f) - bend);
        effect.SetPosition(3, Vector3.Lerp(from, to, 0.75f) + bend);
        effect.SetPosition(4, to);
        effect.enabled = true;
        _electricChainExpirations[index] = Time.time + Mathf.Max(0.01f, ElectricChainEffectDuration);
    }

    void UpdateElectricChainVisuals()
    {
        for (int i = 0; i < _electricChainLines.Count; i++)
        {
            if (_electricChainLines[i].enabled && Time.time >= _electricChainExpirations[i])
                _electricChainLines[i].enabled = false;
        }
    }

    void OnDisable()
    {
        foreach (var line in _electricChainLines)
        {
            if (line != null) line.enabled = false;
        }
    }

    void SpawnSpecialFeather(float angle, Vector3 positionOffset, PlayerStats.SpecialFeatherInstance feather)
    {
        if (ObjectPooler.Instance == null) return;
        GameObject bulletObj = ObjectPooler.Instance.GetPooledObject();
        if (bulletObj == null) return;

        bulletObj.transform.position = FirePoint.position + positionOffset;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);

        Projectile p = bulletObj.GetComponent<Projectile>();
        if (p == null) return;

        p.SpriteAngleOffset = ProjectileSpriteOffset;

        Projectile.BallisticData stats = CurrentStats;
        stats.PoisonDamage = 0;
        stats.IceSlowFactor = 0;
        stats.ExplosionRadius = 0;
        stats.ProjectileGravity = 0;
        stats.Lifetime = 0;
        stats.IsHealingFeather = false;
        stats.IsFrostyFeather = false;
        stats.IsPoisonFeather = false;
        stats.IsMetalFeather = false;
        stats.IsExplosiveFeather = false;
        stats.IsBuckshotPellet = false;
        stats.CanAirburst = true;

        Color featherColor = NormalFeatherColor;

        switch (feather.Type)
        {
            case PlayerStats.FeatherType.Healing:
                stats.IsHealingFeather = true;
                stats.HealAmount = feather.HealAmount;
                stats.DamageRatio = .5f;
                // 1.4.11 PATCH: healing feathers no longer inherit pierce/ricochet/homing 
                // for balance reasons. They're a single-hit utility, not a damage tool.
                stats.PierceCount = 0;
                stats.RicochetCount = 0;
                stats.HomingSpeed = 0;
                featherColor = HealingFeatherColor;
                break;
            case PlayerStats.FeatherType.Frosty:
                stats.IsFrostyFeather = true;
                stats.FreezeDuration = feather.FreezeDuration;
                if (PlayerStats.Instance.HasAscension(CardAscension.AbsoluteZero)) stats.Variant = CardAscension.AbsoluteZero;
                featherColor = FrostyFeatherColor;
                break;
            case PlayerStats.FeatherType.Poison:
                stats.IsPoisonFeather = true;
                stats.PoisonDPS = feather.PoisonDPS;
                if (PlayerStats.Instance.HasAscension(CardAscension.DeadlyToxin)) stats.Variant = CardAscension.DeadlyToxin;
                featherColor = PoisonFeatherColor;
                break;
            case PlayerStats.FeatherType.Metal:
                stats.IsMetalFeather = true;
                stats.BonusKnockback = feather.BonusKnockback;
                stats.ProjectileGravity = 1.0f;
                stats.DamageRatio = PlayerStats.Instance.MetalDamageFraction;
                if (PlayerStats.Instance.HasAscension(CardAscension.Tungsten))
                { stats.Variant = CardAscension.Tungsten; stats.DamageRatio = 4; stats.RicochetCount = 3; stats.PierceCount = 0; }
                featherColor = MetalFeatherColor;
                break;
            case PlayerStats.FeatherType.Explosive:
                stats.IsExplosiveFeather = true;
                stats.ExplosionRadius = feather.ExplosionRadius;
                if (PlayerStats.Instance.HasAscension(CardAscension.Volcano)) stats.Variant = CardAscension.Volcano;
                featherColor = ExplosiveFeatherColor;
                break;
        }

        p.Initialize(stats);
        p.SetColor(featherColor);
        p.SetVisualScale(GetCurrentFeatherSize());
        bulletObj.SetActive(true);
    }

    void FireBuckshotCone(float baseAngle, PlayerStats.SpecialFeatherInstance buckshot)
    {
        int pelletCount = Mathf.Max(1, PlayerStats.BoostCount(buckshot.BuckshotPellets));
        float angleStep = (pelletCount > 1) ? BuckshotConeAngle / (pelletCount - 1) : 0f;
        float startAngle = baseAngle - (BuckshotConeAngle * 0.5f);

        for (int i = 0; i < pelletCount; i++)
        {
            float pelletAngle = (pelletCount == 1) ? baseAngle : startAngle + (angleStep * i);
            SpawnBuckshotPellet(pelletAngle);
        }
    }

    void SpawnBuckshotPellet(float angle)
    {
        if (ObjectPooler.Instance == null) return;
        GameObject bulletObj = ObjectPooler.Instance.GetPooledObject();
        if (bulletObj == null) return;

        bulletObj.transform.position = FirePoint.position;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);

        Projectile p = bulletObj.GetComponent<Projectile>();
        if (p == null) return;

        p.SpriteAngleOffset = ProjectileSpriteOffset;

        Projectile.BallisticData stats = new Projectile.BallisticData();
        stats.Damage = CurrentStats.Damage;
        stats.DamageRatio = PlayerStats.Instance != null ? PlayerStats.Instance.BuckshotDamageFraction : .4f;
        stats.DamageMultiplier = CurrentStats.DamageMultiplier;
        stats.Speed = CurrentStats.Speed * BuckshotSpeedMultiplier;
        stats.Knockback = CurrentStats.Knockback * 0.5f;
        stats.PierceCount = 0;
        stats.RicochetCount = 0;
        stats.HomingSpeed = 0;
        stats.CritChance = CurrentStats.CritChance;
        stats.ExplosionRadius = 0;
        stats.ProximityScaling = 0;
        stats.PoisonDamage = 0;
        stats.IceSlowFactor = 0;
        stats.ProjectileGravity = 0;
        stats.IsBuckshotPellet = true;
        stats.CanAirburst = true;
        stats.Lifetime = BuckshotLifetime;

        p.Initialize(stats);
        p.SetColor(BuckshotFeatherColor);
        p.SetVisualScale(BuckshotPelletScale);
        bulletObj.SetActive(true);
    }

    // ============================================================
    // AIRBURST API
    // ============================================================

    public void SpawnAirburst(Vector3 enemyPos, Vector3 incomingDirection, int sourceDamage, float sourceDamageMult, int ignoredEnemy = 0)
    {
        if (PlayerStats.Instance == null) return;
        int count = PlayerStats.BoostCount(PlayerStats.Instance.AirburstFeatherCount);
        if (count <= 0) return;

        Vector3 spawnPos = enemyPos + incomingDirection.normalized * 0.2f;
        float baseAngle = Mathf.Atan2(incomingDirection.y, incomingDirection.x) * Mathf.Rad2Deg;

        float angleStep = (count > 1) ? AirburstConeAngle / (count - 1) : 0f;
        float startAngle = baseAngle - (AirburstConeAngle * 0.5f);

        for (int i = 0; i < count; i++)
        {
            float angle = (count == 1) ? baseAngle : startAngle + (angleStep * i);
            SpawnAirburstPellet(spawnPos, angle, sourceDamage, sourceDamageMult, ignoredEnemy);
        }
    }

    void SpawnAirburstPellet(Vector3 spawnPos, float angle, int sourceDamage, float sourceDamageMult, int ignoredEnemy = 0)
    {
        if (ObjectPooler.Instance == null) return;
        GameObject bulletObj = ObjectPooler.Instance.GetPooledObject();
        if (bulletObj == null) return;

        bulletObj.transform.position = spawnPos;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);

        Projectile p = bulletObj.GetComponent<Projectile>();
        if (p == null) return;

        p.SpriteAngleOffset = ProjectileSpriteOffset;

        Projectile.BallisticData stats = new Projectile.BallisticData();
        stats.Damage = sourceDamage;
        stats.DamageRatio = .33f;
        stats.IgnoreEnemyID = ignoredEnemy;
        stats.DamageMultiplier = sourceDamageMult;
        stats.Speed = CurrentStats.Speed * AirburstSpeedMultiplier;
        stats.Knockback = 0;
        stats.PierceCount = 0;
        stats.RicochetCount = 0;
        stats.HomingSpeed = 0;
        stats.CritChance = CurrentStats.CritChance;
        stats.ExplosionRadius = 0;
        stats.IsBuckshotPellet = false;
        stats.CanAirburst = false;
        stats.Lifetime = AirburstLifetime;

        p.Initialize(stats);
        p.SetColor(AirburstFeatherColor);
        p.SetVisualScale(AirburstScale);
        bulletObj.SetActive(true);
    }

    // ============================================================
    // TRIPLESHOT
    // ============================================================

    public void TriggerTripleshot()
    {
        if (_tripleshotCoroutine != null) StopCoroutine(_tripleshotCoroutine);
        _tripleshotCoroutine = StartCoroutine(TripleshotRoutine());
    }

    public void TriggerCoinShotBuff() => TriggerTripleshot();

    IEnumerator TripleshotRoutine()
    {
        if (!_isTripleshotActive)
        {
            BonusSpreadProjectiles = 2;
            _isTripleshotActive = true;
        }

        float duration = 5.0f;
        if (PlayerStats.Instance != null) duration = PlayerStats.Boost(PlayerStats.Instance.TripleshotDuration);

        yield return new WaitForSeconds(duration);

        BonusSpreadProjectiles = 0;
        _isTripleshotActive = false;
        _tripleshotCoroutine = null;
    }
}
