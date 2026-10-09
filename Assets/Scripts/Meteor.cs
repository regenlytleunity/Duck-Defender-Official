using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Meteor : MonoBehaviour
{
    [System.NonSerialized] public PlayerStats OwnerStats;

    readonly System.Collections.Generic.List<EnemyBase> _damageTargets = new System.Collections.Generic.List<EnemyBase>();
    [Header("Movement")]
    public float FallSpeed = 15f; 
    public float RotationSpeed = 300f;
    
    [Header("Payload")]
    public GameObject ExplosionPrefab; 
    public GameObject CoinPrefab; 
    public int CoinsToDrop = 3;   

    private Rigidbody2D _rb;
    private bool _hasExploded = false;
    bool _secondary;
    public void ConfigureSecondary() { _secondary = true; CoinsToDrop = 0; }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        _rb.linearVelocity = Vector2.down * FallSpeed;
        Destroy(gameObject, 20); 
    }

    void Update()
    {
        transform.Rotate(0, 0, RotationSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (_hasExploded) return;

        if (collision.CompareTag("Ground") || collision.GetComponentInParent<EnemyBase>() != null)
        {
            Explode();
        }
    }

    void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        float radius = 4.0f; 
        if (OwnerStats != null) radius = OwnerStats.MeteorRadius;

        // 1. Visuals
        ObjectPooler.SpawnEffect(ExplosionPrefab, transform.position, Quaternion.identity, radius / 3f);

        // Grant the payload before damage callbacks can change enemies/waves.
        if (CoinPrefab != null && !_secondary)
        {
            for (int i = 0; i < CoinsToDrop; i++)
            {
                var coin = Instantiate(CoinPrefab, transform.position, Quaternion.identity).GetComponent<Coin>();
                if (coin != null) coin.SecondaryMeteorOwner = OwnerStats;
                if (coin != null) coin.SecondaryMeteorOnPickup = !_secondary && OwnerStats != null && OwnerStats.HasAscension(CardAscension.AbsoluteExtinction);
            }
        }


        // Apply once per enemy, even when it has multiple colliders.
        float flat = OwnerStats != null ? OwnerStats.MeteorDamage : 10;
        float damage = OwnerStats != null
            ? OwnerStats.CalculateDamage(flat, false, _secondary ? .5f : 1f)
            : flat * (_secondary ? .5f : 1f);
        EnemyBase.CopyActiveEnemies(_damageTargets);
        foreach (var enemy in _damageTargets)
        {
            if (enemy == null || !enemy.IsAlive || Vector2.Distance(transform.position, enemy.transform.position) > radius) continue;
            enemy.TakeFractionalDamage(damage, OwnerStats);
            if (GameUI.Instance != null) GameUI.Instance.ShowDamagePopup(enemy.transform.position, Mathf.RoundToInt(damage), false);
        }

        Destroy(gameObject);
    }
}
