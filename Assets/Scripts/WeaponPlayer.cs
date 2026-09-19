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

    private float _nextNormalFireTime;
    private float _nextMiniGunFireTime;
    private Camera _mainCam;

    // === Mini Gun overheat state ===
    private float _miniGunHeat = 0f;
    private bool _miniGunOverheated = false;
    private float _lastMiniGunFireTime = -999f;

    public float MiniGunHeatNormalized
    {
        get
        {
            if (PlayerStats.Instance == null || PlayerStats.Instance.MiniGunOverheatThreshold <= 0) return 0f;
            return Mathf.Clamp01(_miniGunHeat / PlayerStats.Instance.MiniGunOverheatThreshold);
        }
    }

    public bool MiniGunOverheated => _miniGunOverheated;

    private Coroutine _tripleshotCoroutine;
    private bool _isTripleshotActive = false;

    void Start()
    {
        _mainCam = Camera.main;

        if (CurrentStats.Speed == 0) CurrentStats.Speed = 20f;
        if (CurrentStats.Damage == 0) CurrentStats.Damage = 1;
        if (CurrentStats.DamageMultiplier <= 0) CurrentStats.DamageMultiplier = 1.0f;

        BonusSpreadProjectiles = 0;
    }

    void Update()
    {
        bool shootHeld = InputHelper.GetShootHeld();

        if (shootHeld && Time.time >= _nextNormalFireTime)
        {
            ShootNormal();
            _nextNormalFireTime = Time.time + FireRate;
        }

        if (PlayerStats.Instance != null && PlayerStats.Instance.HasMiniGun)
        {
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

        float threshold = Mathf.Max(0.5f, stats.MiniGunOverheatThreshold);
        float recoveryRate = Mathf.Max(0.5f, stats.MiniGunRecoveryRate);

        bool isFiringNow = shootHeld && !_miniGunOverheated;
        if (!isFiringNow)
        {
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

        float baseInterval = FireRate * 0.5f;
        float heatPercent = _miniGunHeat / threshold;
        float intervalScale = 1f + (heatPercent * 2f);
        float currentInterval = baseInterval * intervalScale;

        if (Time.time < _nextMiniGunFireTime) return;

        FireMiniGunFeather();
        _nextMiniGunFireTime = Time.time + currentInterval;
        _lastMiniGunFireTime = Time.time;

        _miniGunHeat += currentInterval;

        if (_miniGunHeat >= threshold)
        {
            TriggerOverheat();
        }
    }

    void TriggerOverheat()
    {
        _miniGunOverheated = true;
        _miniGunHeat = PlayerStats.Instance.MiniGunOverheatThreshold;

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
        stats.Damage = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.Damage * 0.5f));
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
        stats.CanAirburst = false;
        stats.Lifetime = 0;

        p.Initialize(stats);
        p.SetColor(MiniGunFeatherColor);
        p.SetVisualScale(MiniGunFeatherScale * GetCurrentFeatherSize());
        bulletObj.SetActive(true);
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

        var readyThisShot = TickAndCollectReadySpecials();

        List<PlayerStats.SpecialFeatherInstance> readyBuckshots = new List<PlayerStats.SpecialFeatherInstance>();
        List<PlayerStats.SpecialFeatherInstance> readyOthers = new List<PlayerStats.SpecialFeatherInstance>();
        foreach (var feather in readyThisShot)
        {
            if (feather.Type == PlayerStats.FeatherType.Buckshot)
                readyBuckshots.Add(feather);
            else
                readyOthers.Add(feather);
        }

        FireNormalPattern(baseAngle, aimDir);

        foreach (var buck in readyBuckshots)
        {
            FireBuckshotCone(baseAngle, buck);
        }

        if (readyOthers.Count == 1)
        {
            SpawnSpecialFeather(baseAngle, Vector3.zero, readyOthers[0]);
        }
        else if (readyOthers.Count > 1)
        {
            StartCoroutine(FireStaggeredSpecials(readyOthers, baseAngle));
        }
    }

    List<PlayerStats.SpecialFeatherInstance> TickAndCollectReadySpecials()
    {
        var ready = new List<PlayerStats.SpecialFeatherInstance>();
        if (PlayerStats.Instance == null) return ready;

        foreach (var feather in PlayerStats.Instance.SpecialFeathers)
        {
            if (feather.Threshold <= 0) continue;
            feather.ShotCounter++;
            if (feather.ShotCounter >= feather.Threshold)
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

    void FireNormalPattern(float baseAngle, Vector3 aimDir)
    {
        Vector3 perpendicular = new Vector3(-aimDir.y, aimDir.x, 0).normalized;
        float effectiveSpacing = Mathf.Max(ParallelSpacing, MinParallelDistance);

        int parallelCount = ParallelProjectiles;

        List<Vector3> parallelOffsets = CalculateGroundAwareParallelOffsets(
            FirePoint.position,
            perpendicular,
            effectiveSpacing,
            parallelCount
        );

        int totalSpread = SpreadProjectiles + BonusSpreadProjectiles;

        List<float> spreadAngles = new List<float>();
        for (int i = 1; i <= totalSpread; i++)
        {
            float sign = (i % 2 == 0) ? 1f : -1f;
            int tier = Mathf.CeilToInt(i / 2.0f);
            spreadAngles.Add(sign * tier * SpreadAngleStep);
        }

        foreach (Vector3 posOffset in parallelOffsets)
        {
            SpawnNormalFeather(baseAngle, posOffset);
        }

        foreach (float angleOffset in spreadAngles)
        {
            SpawnNormalFeather(baseAngle + angleOffset, Vector3.zero);
        }
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
        return Mathf.Max(0.5f, PlayerStats.Instance.FeatherSize);
    }

    // === Spawn helpers ===

    void SpawnNormalFeather(float angle, Vector3 positionOffset)
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

        if (PlayerStats.Instance != null && PlayerStats.Instance.HomingProjectiles)
        {
            stats.HomingSpeed = Mathf.Max(stats.HomingSpeed, 1f + PlayerStats.Instance.HomingSpeedBonus);
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
        stats.ProjectileGravity = 0;
        stats.Lifetime = 0;

        p.Initialize(stats);
        p.SetColor(NormalFeatherColor);
        p.SetVisualScale(GetCurrentFeatherSize());
        bulletObj.SetActive(true);
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
        stats.CanAirburst = false;

        Color featherColor = NormalFeatherColor;

        switch (feather.Type)
        {
            case PlayerStats.FeatherType.Healing:
                stats.IsHealingFeather = true;
                stats.HealAmount = feather.HealAmount;
                stats.Damage = Mathf.Max(1, stats.Damage / 3);
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
                featherColor = FrostyFeatherColor;
                break;
            case PlayerStats.FeatherType.Poison:
                stats.IsPoisonFeather = true;
                stats.PoisonDPS = feather.PoisonDPS;
                featherColor = PoisonFeatherColor;
                break;
            case PlayerStats.FeatherType.Metal:
                stats.IsMetalFeather = true;
                stats.BonusKnockback = feather.BonusKnockback;
                stats.ProjectileGravity = 1.0f;
                featherColor = MetalFeatherColor;
                break;
            case PlayerStats.FeatherType.Explosive:
                stats.IsExplosiveFeather = true;
                stats.ExplosionRadius = feather.ExplosionRadius;
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
        int pelletCount = Mathf.Max(1, buckshot.BuckshotPellets);
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
        stats.Damage = Mathf.Max(1, Mathf.RoundToInt(CurrentStats.Damage * 0.5f));
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
        stats.CanAirburst = false;
        stats.Lifetime = BuckshotLifetime;

        p.Initialize(stats);
        p.SetColor(BuckshotFeatherColor);
        p.SetVisualScale(BuckshotPelletScale);
        bulletObj.SetActive(true);
    }

    // ============================================================
    // AIRBURST API
    // ============================================================

    public void SpawnAirburst(Vector3 enemyPos, Vector3 incomingDirection, int sourceDamage, float sourceDamageMult)
    {
        if (PlayerStats.Instance == null) return;
        int count = PlayerStats.Instance.AirburstFeatherCount;
        if (count <= 0) return;

        Vector3 spawnPos = enemyPos + incomingDirection.normalized * 0.2f;
        float baseAngle = Mathf.Atan2(incomingDirection.y, incomingDirection.x) * Mathf.Rad2Deg;

        float angleStep = (count > 1) ? AirburstConeAngle / (count - 1) : 0f;
        float startAngle = baseAngle - (AirburstConeAngle * 0.5f);

        for (int i = 0; i < count; i++)
        {
            float angle = (count == 1) ? baseAngle : startAngle + (angleStep * i);
            SpawnAirburstPellet(spawnPos, angle, sourceDamage, sourceDamageMult);
        }
    }

    void SpawnAirburstPellet(Vector3 spawnPos, float angle, int sourceDamage, float sourceDamageMult)
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
        stats.Damage = Mathf.Max(1, Mathf.RoundToInt(sourceDamage * 0.33f));
        stats.DamageMultiplier = sourceDamageMult;
        stats.Speed = CurrentStats.Speed * AirburstSpeedMultiplier;
        stats.Knockback = 0;
        stats.PierceCount = 0;
        stats.RicochetCount = 0;
        stats.HomingSpeed = 0;
        stats.CritChance = CurrentStats.CritChance * 0.5f;
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
        if (PlayerStats.Instance != null) duration = PlayerStats.Instance.TripleshotDuration;

        yield return new WaitForSeconds(duration);

        BonusSpreadProjectiles = 0;
        _isTripleshotActive = false;
        _tripleshotCoroutine = null;
    }
}