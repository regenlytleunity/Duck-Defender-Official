using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float Speed = 10f;
    public int Damage = 1;
    public float Lifetime = 4f;

    private float _timer;
    bool _elite;
    float _slowDuration;
    Vector3 _baseScale;
    void Awake() { if (_baseScale == Vector3.zero) _baseScale = transform.localScale; }
    public void Configure(bool elite, float slowDuration = 2)
    {
        if (_baseScale == Vector3.zero) _baseScale = transform.localScale;
        _elite = elite;
        _slowDuration = slowDuration;
        transform.localScale = _baseScale * (elite ? 1.25f : 1);
    }
    void OnDisable() { _elite = false; transform.localScale = _baseScale; }

    void OnEnable() => _timer = Lifetime;

    void Update()
    {
        transform.Translate(Vector2.right * Speed * PlayerStats.ProjectileSpeedFactor(transform.position) * Time.deltaTime);

        _timer -= Time.deltaTime;
        if (_timer <= 0) gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        var wall = collision.GetComponentInParent<DefenderWall>();
        if (wall != null) { wall.TakeDamage(Damage); gameObject.SetActive(false); return; }
        if (collision.GetComponentInParent<PlayerHealth>() != null)
        {
            // FIX: Get the health component and apply damage
            PlayerHealth playerHealth = collision.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                if (playerHealth.TryTakeDamage(Damage) && _elite) playerHealth.ApplySlow(_slowDuration);
            }
            
            gameObject.SetActive(false);
        }
        else if (collision.CompareTag("Ground"))
        {
            gameObject.SetActive(false);
        }
    }
}
