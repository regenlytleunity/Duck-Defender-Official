using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 1.4.11 NEW: Elemental Turret.
/// Fires ONE feather per tick at a random elemental variant: Frosty, Poison, Metal, or Explosive.
/// Per outline (page 4): "Elemental turret fires only 1 feather at a time no matter how many enemies are on screen."
/// 
/// Targets the nearest enemy in range (uses LOS like Marksman). 
/// Per outline (clarification 13): the 4 elemental feathers are Frosty / Poison / Metal / Explosive (not Healing or Buckshot).
/// 
/// Each elemental variant deals 50% of the player's damage (same as Marksman convention).
/// Effect strength (freeze duration, poison DPS, etc.) is borrowed from the player's existing 
/// SpecialFeatherInstances if any; otherwise sensible defaults are used.
/// </summary>
public class ElementalTurret : TurretBase
{
    public override TurretSlotType TurretType => TurretSlotType.Elemental;

    [Header("Elemental Turret Settings")]
    public float TargetingRange = 12f;
    public LayerMask LineOfSightBlockers;
    public float ProjectileSpeed = 18f;
    public string FireSoundName = "Feather_Hit_Enemy";

    [Header("Elemental Defaults")]
    [Tooltip("Used when no player special-feather instance of that type exists, so the turret " +
             "still has an effect strength to fall back on.")]
    public float DefaultFreezeDuration = 1.5f;
    public float DefaultPoisonDPS = 2f;
    public float DefaultMetalKnockback = 4f;
    public float DefaultExplosionRadius = 1.5f;

    [Header("Visual")]
    public Color FrostyColor = new Color(0.5f, 0.85f, 1f);
    public Color PoisonColor = new Color(0.5f, 1f, 0.3f);
    public Color MetalColor = new Color(0.7f, 0.7f, 0.75f);
    public Color ExplosiveColor = new Color(1f, 0.5f, 0.2f);

    protected override float GetCurrentInterval()
    {
        if (PlayerStats.Instance == null) return 99f;
        return Mathf.Max(0.5f, PlayerStats.Instance.ElementalTurretInterval);
    }

    protected override void OnTick()
    {
        Transform target = FindNearestVisibleEnemy();
        if (target == null) return;

        // Pick a random elemental type (4 options)
        PlayerStats.FeatherType pick = PickRandomElement();
        FireElementalAt(target, pick);
    }

    PlayerStats.FeatherType PickRandomElement()
    {
        int roll = Random.Range(0, 4);
        switch (roll)
        {
            case 0: return PlayerStats.FeatherType.Frosty;
            case 1: return PlayerStats.FeatherType.Poison;
            case 2: return PlayerStats.FeatherType.Metal;
            default: return PlayerStats.FeatherType.Explosive;
        }
    }

    Transform FindNearestVisibleEnemy()
    {
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        Camera cam = Camera.main;
        
        return allEnemies
            .Where(e => e != null && e.activeInHierarchy)
            .Select(e => new
            {
                T = e.transform,
                D = Vector2.Distance(transform.position, e.transform.position)
            })
            .Where(x => x.D <= TargetingRange)
            .Where(x => IsOnScreen(cam, x.T))   // 1.4.11 PATCH
            .Where(x => HasLineOfSight(x.T))
            .OrderBy(x => x.D)
            .Select(x => x.T)
            .FirstOrDefault();
    }

    /// <summary>
    /// 1.4.11: Returns true only if the target is within the camera viewport.
    /// </summary>
    bool IsOnScreen(Camera cam, Transform target)
    {
        if (cam == null) return true;
        Vector3 vp = cam.WorldToViewportPoint(target.position);
        const float margin = 0.05f;
        return vp.x >= -margin && vp.x <= 1f + margin
            && vp.y >= -margin && vp.y <= 1f + margin
            && vp.z > 0f;
    }

    bool HasLineOfSight(Transform target)
    {
        Vector2 origin = transform.position;
        Vector2 direction = ((Vector2)target.position - origin);
        float distance = direction.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction.normalized, distance, LineOfSightBlockers);
        return hit.collider == null;
    }

    void FireElementalAt(Transform target, PlayerStats.FeatherType type)
    {
        if (ObjectPooler.Instance == null) return;

        GameObject bullet = ObjectPooler.Instance.GetPooledObject();
        if (bullet == null) return;

        Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        bullet.transform.position = transform.position;
        bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

        Projectile p = bullet.GetComponent<Projectile>();
        if (p == null) return;

        int playerDamage = 1;
        float playerDamageMult = 1f;
        float playerCrit = 0f;

        var weapon = FindFirstObjectByType<WeaponPlayer>();
        if (weapon != null)
        {
            playerDamage = weapon.CurrentStats.Damage;
            playerDamageMult = weapon.CurrentStats.DamageMultiplier > 0 ? weapon.CurrentStats.DamageMultiplier : 1f;
            playerCrit = weapon.CurrentStats.CritChance;
        }

        Projectile.BallisticData stats = new Projectile.BallisticData();
        stats.Damage = Mathf.Max(1, Mathf.RoundToInt(playerDamage * 0.5f));
        stats.DamageMultiplier = playerDamageMult;
        stats.Speed = ProjectileSpeed;
        stats.Knockback = 1f;
        stats.PierceCount = 0;
        stats.RicochetCount = 0;
        stats.HomingSpeed = 0;
        stats.CritChance = playerCrit;
        stats.ProximityScaling = 0;
        stats.CanAirburst = false;

        Color elementColor = Color.white;

        switch (type)
        {
            case PlayerStats.FeatherType.Frosty:
                stats.IsFrostyFeather = true;
                stats.FreezeDuration = GetEffectStrength(type, DefaultFreezeDuration);
                elementColor = FrostyColor;
                break;

            case PlayerStats.FeatherType.Poison:
                stats.IsPoisonFeather = true;
                stats.PoisonDPS = GetEffectStrength(type, DefaultPoisonDPS);
                elementColor = PoisonColor;
                break;

            case PlayerStats.FeatherType.Metal:
                stats.IsMetalFeather = true;
                stats.BonusKnockback = GetEffectStrength(type, DefaultMetalKnockback);
                stats.ProjectileGravity = 0.5f;
                elementColor = MetalColor;
                break;

            case PlayerStats.FeatherType.Explosive:
                stats.IsExplosiveFeather = true;
                stats.ExplosionRadius = GetEffectStrength(type, DefaultExplosionRadius);
                elementColor = ExplosiveColor;
                break;
        }

        p.Initialize(stats);
        p.SetColor(elementColor);

        bullet.SetActive(true);

        if (AudioManager.Instance != null && !string.IsNullOrEmpty(FireSoundName))
        {
            AudioManager.Instance.PlaySFX(FireSoundName);
        }
    }

    /// <summary>
    /// If the player has a special feather of this type, use its effect strength. 
    /// Otherwise fall back to the turret's default.
    /// </summary>
    float GetEffectStrength(PlayerStats.FeatherType type, float defaultValue)
    {
        if (PlayerStats.Instance == null) return defaultValue;
        var feathers = PlayerStats.Instance.GetSpecialFeathersByType(type);
        if (feathers.Count == 0) return defaultValue;

        // Use the strongest active instance
        float strongest = defaultValue;
        foreach (var f in feathers)
        {
            switch (type)
            {
                case PlayerStats.FeatherType.Frosty:
                    strongest = Mathf.Max(strongest, f.FreezeDuration);
                    break;
                case PlayerStats.FeatherType.Poison:
                    strongest = Mathf.Max(strongest, f.PoisonDPS);
                    break;
                case PlayerStats.FeatherType.Metal:
                    strongest = Mathf.Max(strongest, f.BonusKnockback);
                    break;
                case PlayerStats.FeatherType.Explosive:
                    strongest = Mathf.Max(strongest, f.ExplosionRadius);
                    break;
            }
        }
        return strongest;
    }
}