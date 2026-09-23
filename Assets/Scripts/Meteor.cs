using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Meteor : MonoBehaviour
{
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
        _hasExploded = true;

        float radius = 4.0f; 
        if (PlayerStats.Instance != null) radius = PlayerStats.Boost(PlayerStats.Instance.MeteorRadius);

        // 1. Visuals
        if (ExplosionPrefab != null)
        {
            GameObject boom = Instantiate(ExplosionPrefab, transform.position, Quaternion.identity);
            boom.transform.localScale = Vector3.one * (radius / 3.0f); 
        }

        // Apply once per enemy, even when it has multiple colliders.
        float flat = PlayerStats.Instance != null ? PlayerStats.Instance.MeteorDamage : 10;
        float damage = PlayerStats.Instance != null
            ? PlayerStats.Instance.CalculateDamage(flat, false, _secondary ? .5f : 1f)
            : flat * (_secondary ? .5f : 1f);
        foreach (var enemy in EnemyBase.ActiveEnemies)
        {
            if (enemy == null || !enemy.IsAlive || Vector2.Distance(transform.position, enemy.transform.position) > radius) continue;
            enemy.TakeFractionalDamage(damage);
            if (GameUI.Instance != null) GameUI.Instance.ShowDamagePopup(enemy.transform.position, Mathf.RoundToInt(damage), false);
        }

        // 3. Drop Coins
        if (CoinPrefab != null)
        {
            for (int i = 0; i < CoinsToDrop; i++)
            {
                var coin = Instantiate(CoinPrefab, transform.position, Quaternion.identity).GetComponent<Coin>();
                if (coin != null) coin.SecondaryMeteorOnPickup = !_secondary && PlayerStats.Instance != null && PlayerStats.Instance.HasAscension(CardAscension.AbsoluteExtinction);
            }
        }

        Destroy(gameObject);
    }
}