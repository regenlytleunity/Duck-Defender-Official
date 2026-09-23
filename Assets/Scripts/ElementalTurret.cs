using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Wizard targets multiple visible enemies. Elemental replaces its projectiles with
/// ascended variants; non-ascended effects use owned card strengths or Inspector defaults.
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
            if (enemy == null || !enemy.IsAlive || Vector2.Distance(transform.position, enemy.transform.position) > PlayerStats.Boost(TargetingRange)) continue;
            if (IsOnScreen(Camera.main, enemy.transform) && HasLineOfSight(enemy.transform)) _targets.Add(enemy);
        }
        _targets.Sort((a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
        int count = Mathf.Min(PlayerStats.BoostCount(player.ElementalTargetCount), _targets.Count);
        if (count > 0 && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(FireSoundName);
        for (int i = 0; i < count; i++)
        {
            var type = ascended && Random.Range(0, 5) == 4 ? PlayerStats.FeatherType.Electric : PickRandomElement();
            weapon.FireTurretElement(transform.position, _targets[i].transform, type, ascended, this);
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
