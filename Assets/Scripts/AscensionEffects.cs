using UnityEngine;
using System.Collections;

// Player-owned orchestration for ascended effects that do not belong in a projectile or turret.
public class AscensionEffects : MonoBehaviour
{
    readonly System.Collections.Generic.List<EnemyBase> _damageTargets = new System.Collections.Generic.List<EnemyBase>();
    [Header("Prefabs (visual appearance is authored in Unity)")]
    public HealingOrb HealingOrbPrefab;
    public AscensionArea WormholePrefab;
    public AscensionArea VolcanoFirePrefab;
    public AscensionArea HealingAreaPrefab;
    public DefenderWall WallPrefab;
    public LineRenderer DeathRayVisual;
    [Header("Ascension tuning where the outline leaves values open")]
    public float WormholeRadius = 5;
    public float WormholePull = 10;
    public float HealingAreaRadius = 3;
    public float VolcanoEruptionDelay = .25f;
    public float VolcanoEruptionDamage = 10;
    public float VolcanoFireDPS = 5;
    public float BeamLength = 30;
    public float BeamWidth = .6f;
    public float WallOffset = 2;
    PlayerStats _stats;
    PlayerHealth _health;
    WeaponPlayer _weapon;
    float _recoveryTimer, _beamTick;
    readonly DefenderWall[] _walls = new DefenderWall[2];

    void Start()
    {
        _stats = GetComponent<PlayerStats>(); _health = GetComponent<PlayerHealth>(); _weapon = GetComponent<WeaponPlayer>();
    }
    void Update()
    {
        if (_stats == null || _health == null || _health.IsDead || Time.timeScale == 0)
        { if (DeathRayVisual != null) DeathRayVisual.enabled = false; return; }
        if (_stats.HasAscension(CardAscension.RecoveryPlus))
        {
            _recoveryTimer += Time.deltaTime;
            if (_recoveryTimer >= PlayerStats.Cooldown(5)) { _recoveryTimer -= PlayerStats.Cooldown(5); _health.Heal(PlayerStats.BoostCount(1)); }
        }
        bool beam = _stats.HasAscension(CardAscension.DeathRay) && InputHelper.GetShootHeld() && _weapon != null;
        if (DeathRayVisual != null) DeathRayVisual.enabled = beam;
        if (!beam) { _beamTick = 0; return; }
        Vector3 origin = _weapon.FirePoint != null ? _weapon.FirePoint.position : transform.position;
        Vector2 dir = _weapon.AimDirection;
        if (DeathRayVisual != null)
        {
            DeathRayVisual.useWorldSpace = true; DeathRayVisual.positionCount = 2;
            DeathRayVisual.SetPosition(0, origin); DeathRayVisual.SetPosition(1, origin + (Vector3)dir * PlayerStats.Boost(BeamLength));
            DeathRayVisual.startWidth = DeathRayVisual.endWidth = PlayerStats.Boost(BeamWidth);
        }
        _beamTick += Time.deltaTime;
        if (_beamTick < .1f) return;
        EnemyBase.CopyActiveEnemies(_damageTargets);
        foreach (var enemy in _damageTargets)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            Vector2 delta = enemy.transform.position - origin;
            float along = Vector2.Dot(delta, dir);
            if (along >= 0 && along <= PlayerStats.Boost(BeamLength) && Mathf.Abs(delta.x * dir.y - delta.y * dir.x) <= PlayerStats.Boost(BeamWidth) * .5f)
                enemy.TakeFractionalDamage(_stats.CalculateDamage(10, false) * _beamTick);
        }
        _beamTick = 0;
    }
    void OnDisable() { if (DeathRayVisual != null) DeathRayVisual.enabled = false; }

    public void DropHealingOrb(Vector3 position)
    {
        if (HealingOrbPrefab != null) Instantiate(HealingOrbPrefab, position, Quaternion.identity);
        else Debug.LogWarning("[Ascension] Assign HealingOrbPrefab on the player's AscensionEffects.");
    }
    public void SpawnWormhole(Vector3 position)
    {
        var area = SpawnArea(WormholePrefab, position, PlayerStats.Boost(WormholeRadius), PlayerStats.Boost(3), 5, 0, PlayerStats.Boost(WormholePull), true);
        area.ConfigureCircleVisual(true);
    }
    public void SpawnHealingArea(Vector3 position)
    {
        var area = SpawnArea(HealingAreaPrefab, position, PlayerStats.Boost(HealingAreaRadius), PlayerStats.Boost(5), 0, PlayerStats.Boost(2), 0, true);
        area.ConfigureCircleVisual(false);
    }
    static AscensionArea SpawnArea(AscensionArea prefab, Vector3 position, float radius, float seconds, float damage, float healing = 0, float pull = 0, bool circleFallback = false)
    {
        if (prefab == null && !circleFallback) { Debug.LogWarning("[Ascension] An area prefab is unassigned on AscensionEffects."); return null; }
        var area = prefab != null ? Instantiate(prefab, position, Quaternion.identity) : new GameObject("Ascension circle").AddComponent<AscensionArea>();
        area.transform.position = position;
        area.gameObject.SetActive(true);
        area.Initialize(radius, seconds, damage, healing, pull);
        return area;
    }
    public void Erupt(Vector3 position, float radius, GameObject explosionVisual = null) { StartCoroutine(Eruption(position, radius, explosionVisual)); }
    IEnumerator Eruption(Vector3 position, float radius, GameObject explosionVisual)
    {
        yield return new WaitForSeconds(VolcanoEruptionDelay);
        ObjectPooler.SpawnEffect(explosionVisual, position, Quaternion.identity, radius);
        EnemyBase.CopyActiveEnemies(_damageTargets);
        foreach (var enemy in _damageTargets)
            if (enemy != null && enemy.IsAlive && ((Vector2)enemy.transform.position - (Vector2)position).sqrMagnitude <= radius * radius)
                enemy.TakeFractionalDamage(_stats.CalculateDamage(VolcanoEruptionDamage, false));
        SpawnArea(VolcanoFirePrefab, position, radius, PlayerStats.Boost(3), VolcanoFireDPS);
    }
    public void PlaceWalls()
    {
        if (WallPrefab == null) { Debug.LogWarning("[Ascension] Assign WallPrefab on AscensionEffects."); return; }
        for (int i = 0; i < 2; i++)
        {
            if (_walls[i] != null) Destroy(_walls[i].gameObject);
            _walls[i] = Instantiate(WallPrefab, transform.position + Vector3.right * (i == 0 ? -WallOffset : WallOffset), Quaternion.identity);
            _walls[i].Health = PlayerStats.BoostCount(5);
            var playerColliders = GetComponentsInChildren<Collider2D>();
            var wallCollider = _walls[i].GetComponent<Collider2D>();
            foreach (var collider in playerColliders) Physics2D.IgnoreCollision(collider, wallCollider);
        }
    }
    public void ReleaseNeedles()
    {
        if (_weapon == null) _weapon = GetComponent<WeaponPlayer>();
        if (_weapon != null) { _weapon.FireNeedle(Vector2.left); _weapon.FireNeedle(Vector2.right); }
    }
    public void Rebirth()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        EnemyBase.CopyActiveEnemies(_damageTargets);
        foreach (var enemy in _damageTargets)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            Vector3 viewport = camera.WorldToViewportPoint(enemy.transform.position);
            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1) enemy.Defeat();
        }
    }
}
