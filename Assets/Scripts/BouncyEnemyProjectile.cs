using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class BouncyEnemyProjectile : MonoBehaviour
{
    public int Damage = 1;
    public LayerMask GroundLayer;
    public float GroundCheckDistance = .3f;
    [HideInInspector] public float Bounciness = .7f;
    public int MaxBounces = 2;
    [HideInInspector] public float MinBounceSpeed = 1.5f;
    public float MaxLifetime = 8;
    public float VisualRotationFactor = 30;
    public bool DebugMode;
    [Header("Poison")]
    [Range(0, 1)] public float PoisonChance = .5f;
    public float CloudDuration = 1;
    public float CloudRadius = 1;
    public Color PoisonColor = new Color(.5f, 1, .3f);
    public Color BounceColor = new Color(1, .7f, .3f);

    Rigidbody2D _rb;
    CircleCollider2D _circleCollider;
    SpriteRenderer _sprite;
    Vector3 _baseScale;
    float _baseRadius, _lifetimeRemaining, _slowFactor = 1;
    int _bouncesRemaining;
    bool _isActive, _bouncing, _cloud, _hasPendingLaunch;
    Vector2 _pendingLaunchVelocity;
    public bool IsCloud => _cloud;
    public int BouncesRemaining => _bouncesRemaining;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _circleCollider = GetComponent<CircleCollider2D>();
        _sprite = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        _baseRadius = _circleCollider != null ? _circleCollider.radius : .2f;
    }

    public void Launch(Vector2 velocity) { Launch(velocity, false); }
    public void Launch(Vector2 velocity, bool bouncing)
    {
        _pendingLaunchVelocity = velocity;
        _bouncing = bouncing;
        _hasPendingLaunch = true;
    }

    void OnEnable()
    {
        _isActive = _hasPendingLaunch;
        _hasPendingLaunch = false;
        _cloud = false;
        _slowFactor = 1;
        _bouncesRemaining = Mathf.Clamp(MaxBounces, 0, 2);
        _lifetimeRemaining = MaxLifetime;
        transform.localScale = _baseScale;
        transform.rotation = Quaternion.identity;
        if (_circleCollider != null) _circleCollider.radius = _baseRadius;
        if (_sprite != null) _sprite.color = _bouncing ? BounceColor : PoisonColor;
        _rb.gravityScale = 1;
        _rb.angularVelocity = 0;
        _rb.linearVelocity = _isActive ? _pendingLaunchVelocity : Vector2.zero;
    }

    void Update()
    {
        if (!_isActive || Time.timeScale == 0) return;
        _lifetimeRemaining -= Time.deltaTime;
        if (_lifetimeRemaining <= 0) { Deactivate(); return; }
        if (!_cloud) transform.Rotate(0, 0, -_rb.linearVelocity.x * VisualRotationFactor * Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (!_isActive || _cloud) return;
        float factor = Mathf.Max(.01f, PlayerStats.ProjectileSpeedFactor(transform.position));
        _rb.linearVelocity *= factor / _slowFactor;
        _rb.gravityScale = factor * factor;
        _slowFactor = factor;
        Vector2 velocity = _rb.linearVelocity;
        if (velocity.sqrMagnitude < .0001f) return;
        float radius = _baseRadius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        var hit = Physics2D.CircleCast(_rb.position, radius, velocity.normalized,
            Mathf.Max(GroundCheckDistance, velocity.magnitude * Time.fixedDeltaTime), GroundLayer);
        if (hit.collider == null) return;
        if (!_bouncing) { BecomeCloud(hit.point); return; }
        if (_bouncesRemaining <= 0) { Deactivate(); return; }
        _bouncesRemaining--;
        transform.localScale *= 1.25f;
        radius *= 1.25f;
        _rb.position = hit.point + hit.normal * (radius + .03f);
        _rb.linearVelocity = Vector2.Reflect(velocity, hit.normal) * 1.25f;
    }

    void BecomeCloud(Vector2 position)
    {
        _cloud = true;
        _rb.position = position + Vector2.up * .1f;
        _rb.linearVelocity = Vector2.zero;
        _rb.gravityScale = 0;
        _lifetimeRemaining = Mathf.Max(.01f, CloudDuration);
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(_baseScale.x * 3, _baseScale.y * 1.5f, _baseScale.z);
        if (_circleCollider != null) _circleCollider.radius = CloudRadius / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x));
        if (_sprite != null) _sprite.color = new Color(PoisonColor.r, PoisonColor.g, PoisonColor.b, .55f);
    }

    void OnTriggerEnter2D(Collider2D other) { Hit(other); }
    void OnTriggerStay2D(Collider2D other) { if (_cloud) Hit(other); }
    void OnCollisionEnter2D(Collision2D other) { Hit(other.collider); }
    void Hit(Collider2D other)
    {
        if (!_isActive || Time.timeScale == 0) return;
        var wall = other.GetComponentInParent<DefenderWall>();
        if (wall != null) { wall.TakeDamage(Damage); Deactivate(); return; }
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;
        if (health.TryTakeDamage(Damage) && !_bouncing && !_cloud && Random.value < PoisonChance)
            health.ApplyLobberPoison();
        Deactivate();
    }

    void Deactivate() { gameObject.SetActive(false); }
    void OnDisable()
    {
        _isActive = _hasPendingLaunch = _cloud = false;
        if (_rb != null) { _rb.linearVelocity = Vector2.zero; _rb.gravityScale = 0; }
        transform.localScale = _baseScale;
        if (_circleCollider != null) _circleCollider.radius = _baseRadius;
    }
}
