using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance;

    [Header("Player Ammo")]
    public GameObject ProjectilePrefab;
    public int PoolSize = 20;
    private List<GameObject> _playerPool;

    [Header("Enemy Ammo")]
    public GameObject EnemyProjectilePrefab;
    public int EnemyPoolSize = 20;
    private List<GameObject> _enemyPool;
    
    [Header("Bouncy Enemy Ammo")]
    [Tooltip("The bouncy projectile fired by LobberEnemy.")]
    public GameObject BouncyEnemyProjectilePrefab;
    public int BouncyPoolSize = 15;
    private List<GameObject> _bouncyEnemyPool;

    void Awake()
    {
        Instance = this;
        _playerPool = CreatePool(ProjectilePrefab, PoolSize);
        _enemyPool = CreatePool(EnemyProjectilePrefab, EnemyPoolSize);
        _bouncyEnemyPool = CreatePool(BouncyEnemyProjectilePrefab, BouncyPoolSize);
    }

    private List<GameObject> CreatePool(GameObject prefab, int size)
    {
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < size; i++)
        {
            if (prefab != null)
            {
                GameObject obj = Instantiate(prefab);
                obj.SetActive(false);
                list.Add(obj);
            }
        }
        return list;
    }

    public GameObject GetPooledObject() 
    {
        return GetObject(_playerPool);
    }

    public GameObject GetEnemyBullet() 
    {
        return GetObject(_enemyPool);
    }
    
    /// <summary>
    /// Returns an inactive bouncy enemy projectile from the pool, or null if all are in use.
    /// </summary>
    public GameObject GetBouncyEnemyProjectile()
    {
        return GetObject(_bouncyEnemyPool);
    }

    private GameObject GetObject(List<GameObject> pool)
    {
        if (pool == null) return null;
        
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].activeInHierarchy)
            {
                return pool[i];
            }
        }
        return null;
    }
}