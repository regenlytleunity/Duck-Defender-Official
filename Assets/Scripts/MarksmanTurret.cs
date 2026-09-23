using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 1.4.11: Renamed from FlingerTurret. Same behavior - auto-fires feathers at nearest enemies.
/// Pulls fire rate and target count from PlayerStats.MarksmanInterval / MarksmanTargetCount.
/// </summary>
public class MarksmanTurret : TurretBase
{
    public override TurretSlotType TurretType => TurretSlotType.Marksman;

    [Header("Marksman Settings")]
    public float TargetingRange = 12f;
    public LayerMask LineOfSightBlockers;
    public float ProjectileSpeed = 18f;
    public string FireSoundName = "Feather_Hit_Enemy";

    [Header("Safety Caps")]
    public int MaxTargetsPerVolleyHardCap = 10;
    public bool DebugLogTargetCount = false;

    protected override float GetCurrentInterval()
    {
        if (PlayerStats.Instance == null) return 99f;
        return Mathf.Max(0.3f, PlayerStats.Instance.MarksmanInterval);
    }

    protected override void OnTick()
    {
        if (PlayerStats.Instance == null) return;

        int targetCount = Mathf.Clamp(
            PlayerStats.BoostCount(PlayerStats.Instance.MarksmanTargetCount),
            1,
            PlayerStats.BoostCount(MaxTargetsPerVolleyHardCap)
        );

        List<Transform> targets = FindVisibleEnemies(targetCount);

        if (DebugLogTargetCount)
        {
            Debug.Log($"[Marksman] stat count: {PlayerStats.Instance.MarksmanTargetCount}, " +
                      $"clamped: {targetCount}, found: {targets.Count}, interval: {GetCurrentInterval():F2}s");
        }

        if (targets.Count == 0) return;

        foreach (Transform target in targets)
        {
            FireAt(target);
        }
    }

    List<Transform> FindVisibleEnemies(int maxCount)
    {
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (maxCount <= 0) return new List<Transform>();

        // 1.4.11 PATCH: cache the camera once per call for the on-screen check.
        Camera cam = Camera.main;

        var visible = allEnemies
            .Where(e => e != null && e.activeInHierarchy)
            .Select(e => new
            {
                Obj = e,
                Dist = Vector2.Distance(transform.position, e.transform.position)
            })
            .Where(x => x.Dist <= PlayerStats.Boost(TargetingRange))
            .Where(x => IsOnScreen(cam, x.Obj.transform))   // 1.4.11 PATCH
            .Where(x => HasLineOfSight(x.Obj.transform))
            .OrderBy(x => x.Dist)
            .Take(maxCount)
            .Select(x => x.Obj.transform)
            .ToList();

        return visible;
    }

    /// <summary>
    /// 1.4.11: Returns true only if the target's world position is within the camera's
    /// viewport (i.e. visible on screen). Prevents turrets from firing at off-screen 
    /// enemies when the player is centered.
    /// </summary>
    bool IsOnScreen(Camera cam, Transform target)
    {
        if (cam == null) return true; // fail open if no camera
        Vector3 vp = cam.WorldToViewportPoint(target.position);
        // Small margin so enemies right at the edge still count
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

    void FireAt(Transform target)
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
            playerDamageMult = weapon.CurrentStats.DamageMultiplier > 0
                ? weapon.CurrentStats.DamageMultiplier : 1f;
            playerCrit = weapon.CurrentStats.CritChance;
        }

        Projectile.BallisticData stats = new Projectile.BallisticData();
        stats.Damage = playerDamage;
        stats.DamageRatio = .5f;
        stats.DamageMultiplier = playerDamageMult;
        stats.Speed = ProjectileSpeed;
        stats.Knockback = 1f;
        stats.PierceCount = 0;
        stats.RicochetCount = 0;
        stats.HomingSpeed = 0;
        stats.CritChance = playerCrit;
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
        stats.MarkTarget = PlayerStats.Instance != null && PlayerStats.Instance.HasAscension(CardAscension.Marksman);

        p.Initialize(stats);
        p.SetColor(new Color(0.7f, 0.9f, 1f, 1f));

        bullet.SetActive(true);

        if (AudioManager.Instance != null && !string.IsNullOrEmpty(FireSoundName))
        {
            AudioManager.Instance.PlaySFX(FireSoundName);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.9f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, TargetingRange);
    }
}
