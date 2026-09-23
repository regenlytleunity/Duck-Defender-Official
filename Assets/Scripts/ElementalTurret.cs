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
        var player = PlayerStats.Instance;
        if (player == null) return;
        var weapon = player.GetComponent<WeaponPlayer>();
        if (weapon == null) return;
        bool ascended = player.HasAscension(CardAscension.Elemental);
        _targets.Clear();
        foreach (var enemy in EnemyBase.ActiveEnemies)
        {
            if (enemy == null || !enemy.IsAlive || Vector2.Distance(transform.position, enemy.transform.position) > TargetingRange) continue;
            if (IsOnScreen(Camera.main, enemy.transform) && HasLineOfSight(enemy.transform)) _targets.Add(enemy);
        }
        _targets.Sort((a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
        int count = Mathf.Min(player.ElementalTargetCount, _targets.Count);
        for (int i = 0; i < count; i++)
        {
            var type = ascended && Random.Range(0, 5) == 4 ? PlayerStats.FeatherType.Electric : PickRandomElement();
            weapon.FireTurretElement(transform.position, _targets[i].transform, type, ascended);
        }
    }
    readonly List<EnemyBase> _targets = new List<EnemyBase>();

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

}
