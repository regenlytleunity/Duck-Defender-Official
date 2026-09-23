using UnityEngine;
using System.Collections;

// Player-owned orchestration for ascended effects that do not belong in a projectile or turret.
public class AscensionEffects : MonoBehaviour
{
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
        if (_stats == null || _health == null || _health.IsDead || Time.timeScale == 0) return;
        if (_stats.HasAscension(CardAscension.RecoveryPlus))
        {
            _recoveryTimer += Time.deltaTime;
            if (_recoveryTimer >= 5) { _recoveryTimer -= 5; _health.Heal(1); }
        }
        bool beam = _stats.HasAscension(CardAscension.DeathRay) && InputHelper.GetShootHeld() && _weapon != null;
        if (DeathRayVisual != null) DeathRayVisual.enabled = beam;
        if (!beam) { _beamTick = 0; return; }
        Vector3 origin = _weapon.FirePoint != null ? _weapon.FirePoint.position : transform.position;
        Vector2 dir = _weapon.AimDirection;
        if (DeathRayVisual != null)
        {
            DeathRayVisual.useWorldSpace = true; DeathRayVisual.positionCount = 2;
            DeathRayVisual.SetPosition(0, origin); DeathRayVisual.SetPosition(1, origin + (Vector3)dir * BeamLength);
            DeathRayVisual.startWidth = DeathRayVisual.endWidth = BeamWidth;
        }
        _beamTick += Time.deltaTime;
        if (_beamTick < .1f) return;
        foreach (var enemy in EnemyBase.ActiveEnemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            Vector2 delta = enemy.transform.position - origin;
            float along = Vector2.Dot(delta, dir);
            if (along >= 0 && along <= BeamLength && Mathf.Abs(delta.x * dir.y - delta.y * dir.x) <= BeamWidth * .5f)
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
        SpawnArea(WormholePrefab, position, WormholeRadius, 3, 5, 0, WormholePull);
    }
    public void SpawnHealingArea(Vector3 position)
    {
        SpawnArea(HealingAreaPrefab, position, HealingAreaRadius, 5, 0, 2);
    }
    static void SpawnArea(AscensionArea prefab, Vector3 position, float radius, float seconds, float damage, float healing = 0, float pull = 0)
    {
        if (prefab == null) { Debug.LogWarning("[Ascension] An area prefab is unassigned on AscensionEffects."); return; }
        Instantiate(prefab, position, Quaternion.identity).Initialize(radius, seconds, damage, healing, pull);
    }
    public void Erupt(Vector3 position, float radius) { StartCoroutine(Eruption(position, radius)); }
    IEnumerator Eruption(Vector3 position, float radius)
    {
        yield return new WaitForSeconds(VolcanoEruptionDelay);
        foreach (var enemy in EnemyBase.ActiveEnemies)
            if (enemy != null && enemy.IsAlive && ((Vector2)enemy.transform.position - (Vector2)position).sqrMagnitude <= radius * radius)
                enemy.TakeFractionalDamage(_stats.CalculateDamage(VolcanoEruptionDamage, false));
        SpawnArea(VolcanoFirePrefab, position, radius, 3, VolcanoFireDPS);
    }
    public void PlaceWalls()
    {
        if (WallPrefab == null) { Debug.LogWarning("[Ascension] Assign WallPrefab on AscensionEffects."); return; }
        for (int i = 0; i < 2; i++)
        {
            if (_walls[i] != null) Destroy(_walls[i].gameObject);
            _walls[i] = Instantiate(WallPrefab, transform.position + Vector3.right * (i == 0 ? -WallOffset : WallOffset), Quaternion.identity);
            _walls[i].Health = 5;
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
        foreach (var enemy in EnemyBase.ActiveEnemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            Vector3 viewport = camera.WorldToViewportPoint(enemy.transform.position);
            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1) enemy.Defeat();
        }
    }
}
