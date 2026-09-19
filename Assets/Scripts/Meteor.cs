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

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        _rb.linearVelocity = Vector2.down * FallSpeed; 
    }

    void Update()
    {
        transform.Rotate(0, 0, RotationSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (_hasExploded) return;

        if (collision.CompareTag("Ground") || collision.CompareTag("Enemy"))
        {
            Explode();
        }
    }

    void Explode()
    {
        _hasExploded = true;

        float radius = 4.0f; 
        if (PlayerStats.Instance != null) radius = PlayerStats.Instance.MeteorRadius;

        // 1. Visuals
        if (ExplosionPrefab != null)
        {
            GameObject boom = Instantiate(ExplosionPrefab, transform.position, Quaternion.identity);
            boom.transform.localScale = Vector3.one * (radius / 3.0f); 
        }

        // 2. Damage Logic (50% MAX HEALTH)
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    // Logic: Deal 50% of THIS enemy's Max Health
                    int damage = Mathf.CeilToInt(enemy.MaxHealth * 0.5f);
                    
                    enemy.TakeDamage(damage);
                    if (GameUI.Instance != null)
                        GameUI.Instance.ShowDamagePopup(enemy.transform.position, damage, true); 
                }
            }
        }

        // 3. Drop Coins
        if (CoinPrefab != null)
        {
            for (int i = 0; i < CoinsToDrop; i++)
            {
                Instantiate(CoinPrefab, transform.position, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }
}