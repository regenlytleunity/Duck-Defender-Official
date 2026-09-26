using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance;

    [Header("Player Ammo")]
    public GameObject ProjectilePrefab;
    public int PoolSize = 20;
    private List<GameObject> _playerPool;
    [Tooltip("Prewarm a small set; grow only when needed, up to PoolSize and this safety limit.")]
    public int PlayerPrewarm = 64;
    public int MaximumPlayerProjectiles = 2048;
    [Header("One-shot visual effects")]
    public int MaximumEffectsPerPrefab = 32;
    public int MaximumEffectsTotal = 128;
    readonly Queue<GameObject> _availablePlayers = new Queue<GameObject>();
    readonly HashSet<int> _availablePlayerIDs = new HashSet<int>();
    readonly Dictionary<GameObject, List<PooledVisualEffect>> _effects = new Dictionary<GameObject, List<PooledVisualEffect>>();
    Transform _inactiveSpawnRoot;
    int _effectCount;
    public int PlayerCapacity => Mathf.Max(1, Mathf.Min(PoolSize, MaximumPlayerProjectiles));
    public int PlayerInstances => _playerPool != null ? _playerPool.Count : 0;
    public int EffectInstances => _effectCount;

    [Header("Enemy Ammo")]
    public GameObject EnemyProjectilePrefab;
    public int EnemyPoolSize = 20;
    [Tooltip("Enemy pools grow on demand up to their existing configured capacities.")]
    public int EnemyPrewarm = 32;
    private List<GameObject> _enemyPool;
    
    [Header("Bouncy Enemy Ammo")]
    [Tooltip("The bouncy projectile fired by LobberEnemy.")]
    public GameObject BouncyEnemyProjectilePrefab;
    public int BouncyPoolSize = 15;
    private List<GameObject> _bouncyEnemyPool;

    void Awake()
    {
        Instance = this;
        var staging = new GameObject("Inactive pool staging");
        staging.transform.SetParent(transform, false);
        staging.SetActive(false);
        _inactiveSpawnRoot = staging.transform;
        _playerPool = new List<GameObject>();
        for (int i = 0; i < Mathf.Min(PlayerCapacity, Mathf.Max(0, PlayerPrewarm)); i++)
        {
            var bullet = CreatePlayerProjectile();
            if (bullet == null) break;
            ReturnPlayerProjectile(bullet);
        }
        _enemyPool = CreatePool(EnemyProjectilePrefab, EnemyPoolSize);
        _bouncyEnemyPool = CreatePool(BouncyEnemyProjectilePrefab, BouncyPoolSize);
    }

    private List<GameObject> CreatePool(GameObject prefab, int size)
    {
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < Mathf.Min(size, Mathf.Max(0, EnemyPrewarm)); i++)
        {
            if (prefab != null)
            {
                GameObject obj = CreateInactiveProjectile(prefab);
                list.Add(obj);
            }
        }
        return list;
    }

    public GameObject GetPooledObject() 
    {
        while (_availablePlayers.Count > 0)
        {
            var bullet = _availablePlayers.Dequeue();
            if (bullet == null) continue;
            _availablePlayerIDs.Remove(bullet.GetInstanceID());
            return bullet;
        }
        return _playerPool != null && _playerPool.Count < PlayerCapacity ? CreatePlayerProjectile() : null;
    }

    GameObject CreatePlayerProjectile()
    {
        if (ProjectilePrefab == null) return null;
        var bullet = Instantiate(ProjectilePrefab, _inactiveSpawnRoot);
        bullet.SetActive(false);
        bullet.transform.SetParent(transform, true);
        var projectile = bullet.GetComponent<Projectile>();
        if (projectile == null) { Destroy(bullet); return null; }
        projectile.AssignPool(this);
        _playerPool.Add(bullet);
        return bullet;
    }

    internal void ReturnPlayerProjectile(GameObject bullet)
    {
        if (bullet != null && _availablePlayerIDs.Add(bullet.GetInstanceID())) _availablePlayers.Enqueue(bullet);
    }

    // Only cosmetic one-shot effects use this path; gameplay areas keep their own lifetime.
    public static GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1)
    {
        if (prefab == null) return null;
        rotation *= prefab.transform.localRotation;
        if (Instance == null)
        {
            var standalone = Instantiate(prefab, position, rotation);
            var lifetime = standalone.GetComponent<PooledVisualEffect>();
            if (lifetime == null) lifetime = standalone.AddComponent<PooledVisualEffect>();
            lifetime.Play(position, rotation, scale, false);
            return standalone;
        }
        return Instance.SpawnPooledEffect(prefab, position, rotation, scale);
    }

    GameObject SpawnPooledEffect(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
    {
        if (!_effects.TryGetValue(prefab, out var instances))
        {
            instances = new List<PooledVisualEffect>();
            _effects.Add(prefab, instances);
        }
        // Recover capacity even if an external animation/script destroys a pooled FX.
        for (int i = instances.Count - 1; i >= 0; i--)
            if (instances[i] == null) { instances.RemoveAt(i); _effectCount--; }
        foreach (var effect in instances)
            if (effect != null && !effect.gameObject.activeSelf)
            { effect.Play(position, rotation, scale, true); return effect.gameObject; }
        if (_effectCount >= MaximumEffectsTotal && instances.Count == 0) ReclaimUnusedEffect();
        if (instances.Count >= MaximumEffectsPerPrefab || _effectCount >= MaximumEffectsTotal)
        {
            // Reuse the oldest same-kind visual instead of permanently losing explosions.
            if (instances.Count == 0) return null;
            var oldest = instances[0]; instances.RemoveAt(0); instances.Add(oldest);
            oldest.Play(position, rotation, scale, true);
            return oldest.gameObject;
        }
        var go = Instantiate(prefab, _inactiveSpawnRoot);
        go.SetActive(false);
        go.transform.SetParent(transform, true);
        var created = go.GetComponent<PooledVisualEffect>();
        if (created == null) created = go.AddComponent<PooledVisualEffect>();
        instances.Add(created); _effectCount++;
        created.Play(position, rotation, scale, true);
        return go;
    }

    void ReclaimUnusedEffect()
    {
        foreach (var bucket in _effects.Values)
            for (int i = bucket.Count - 1; i >= 0; i--)
                if (bucket[i] == null || !bucket[i].gameObject.activeSelf)
                {
                    if (bucket[i] != null) Destroy(bucket[i].gameObject);
                    bucket.RemoveAt(i); _effectCount--; return;
                }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public GameObject GetEnemyBullet() 
    {
        return GetObject(_enemyPool, EnemyProjectilePrefab, EnemyPoolSize);
    }
    
    /// <summary>
    /// Returns an inactive bouncy enemy projectile from the pool, or null if all are in use.
    /// </summary>
    public GameObject GetBouncyEnemyProjectile()
    {
        return GetObject(_bouncyEnemyPool, BouncyEnemyProjectilePrefab, BouncyPoolSize);
    }

    GameObject CreateInactiveProjectile(GameObject prefab)
    {
        var obj = Instantiate(prefab, _inactiveSpawnRoot);
        obj.SetActive(false);
        obj.transform.SetParent(transform, true);
        return obj;
    }

    private GameObject GetObject(List<GameObject> pool, GameObject prefab, int capacity)
    {
        if (pool == null) return null;
        
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].activeSelf)
            {
                return pool[i];
            }
        }
        if (prefab == null || pool.Count >= capacity) return null;
        var created = CreateInactiveProjectile(prefab);
        pool.Add(created);
        return created;
    }
}
