using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public class HealingOrb : MonoBehaviour
{
    public float Lifetime = 20;
    public float PickupRadius = .6f;
    public float AttractionRadius = 4;
    public float AttractionSpeed = 7;
    public float GravityScale = 2;
    PlayerHealth _health;
    Rigidbody2D _body;
    CircleCollider2D _collider;
    bool _attracting;
    bool _collected;
    void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        if (_body == null) _body = gameObject.AddComponent<Rigidbody2D>();
        _collider = GetComponent<CircleCollider2D>();
        _collider.isTrigger = false;
        _body.bodyType = RigidbodyType2D.Dynamic;
        _body.gravityScale = Mathf.Max(.1f, GravityScale);
        _body.constraints = RigidbodyConstraints2D.FreezeRotation;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // Fall onto terrain without blocking the player or resting on flying enemies.
        _body.excludeLayers = ~LayerMask.GetMask("Ground");
    }
    void Start()
    {
        _health = PlayerStats.Instance != null ? PlayerStats.Instance.GetComponent<PlayerHealth>() : null;
        Destroy(gameObject, Lifetime);
    }
    void FixedUpdate()
    {
        if (_health == null || _collected || _health.IsDead) return;
        float distance = Vector2.Distance(transform.position, _health.transform.position);
        if (distance <= AttractionRadius) _attracting = true;
        if (_attracting)
        {
            _body.gravityScale = 0;
            _collider.isTrigger = true;
            _body.linearVelocity = ((Vector2)_health.transform.position - _body.position).normalized * Mathf.Min(AttractionSpeed, distance / Time.fixedDeltaTime);
        }
        if (distance <= PickupRadius) Collect();
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerHealth>() != null) Collect();
    }
    void Collect()
    {
        if (_health == null || _collected) return;
        _collected = true;
        _health.Heal(Mathf.Max(1, Mathf.CeilToInt(_health.MaxHealth * .1f)));
        Destroy(gameObject);
    }
}
