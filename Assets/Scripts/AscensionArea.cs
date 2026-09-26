using UnityEngine;

// Shared lifetime/area behavior. These effects never use physics to apply damage/healing.
public class AscensionArea : MonoBehaviour
{
    readonly System.Collections.Generic.List<EnemyBase> _damageTargets = new System.Collections.Generic.List<EnemyBase>();
    float _radius, _damage, _healing, _pull, _remaining, _healRemainder;
    float _tick;
    PlayerHealth _health;
    Transform _rotatingVisual;
    static Material _ringMaterial;
    public void Initialize(float radius, float seconds, float damage = 0, float healing = 0, float pull = 0)
    {
        _radius = radius; _remaining = seconds; _damage = damage; _healing = healing; _pull = pull;
        _health = PlayerStats.Instance != null ? PlayerStats.Instance.GetComponent<PlayerHealth>() : null;
        transform.localScale = Vector3.one * radius;
        // Prefab colliders used for authoring must not push the player or trap enemies.
        foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (var body in GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
        if (damage > 0)
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
            { particles.gameObject.SetActive(true); particles.Play(true); }
    }

    public void ConfigureCircleVisual(bool wormhole)
    {
        transform.localScale = Vector3.one;
        foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_ringMaterial == null)
            _ringMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        SpriteRenderer circle = null;
        foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprite.enabled = false;
            if (circle == null && sprite.sprite != null) circle = sprite;
        }
        if (wormhole && circle != null)
        {
            circle.gameObject.SetActive(true);
            circle.enabled = true;
            circle.sharedMaterial = _ringMaterial;
            circle.sortingOrder = 100;
            circle.color = new Color(.55f, .3f, 1f, .65f);
            circle.transform.localPosition = Vector3.zero;
            float diameter = Mathf.Max(circle.sprite.bounds.size.x, circle.sprite.bounds.size.y);
            circle.transform.localScale = Vector3.one * (_radius * 2 / Mathf.Max(.01f, diameter));
            _rotatingVisual = circle.transform;
            return;
        }
        var ring = GetComponent<LineRenderer>();
        if (ring == null) ring = gameObject.AddComponent<LineRenderer>();
        ring.enabled = true;
        ring.sharedMaterial = _ringMaterial;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 60;
        ring.startWidth = ring.endWidth = .12f;
        ring.sortingOrder = 100;
        ring.startColor = ring.endColor = wormhole ? new Color(.55f, .3f, 1f, .8f) : new Color(.3f, 1f, .5f, .75f);
        for (int i = 0; i < 60; i++)
        {
            float angle = i * Mathf.PI * 2 / 60;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (_radius * .95f));
        }
        if (wormhole) _rotatingVisual = ring.transform;
    }
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, _remaining);
        if (dt <= 0) return;
        if (_rotatingVisual != null) _rotatingVisual.Rotate(0, 0, 20 * dt);
        _remaining -= dt;
        _tick += dt;
        if (_tick >= .1f || _remaining <= 0)
        {
            if (_damage > 0 || _pull > 0) EnemyBase.CopyActiveEnemies(_damageTargets);
            foreach (var enemy in _damageTargets)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                Vector2 delta = (Vector2)transform.position - (Vector2)enemy.transform.position;
                if (delta.sqrMagnitude > _radius * _radius) continue;
                if (_damage > 0)
                    enemy.TakeFractionalDamage(PlayerStats.Instance != null ? PlayerStats.Instance.CalculateDamage(_damage, false) * _tick : _damage * _tick);
                if (_pull > 0)
                {
                    var rb = enemy.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.AddForce(delta.normalized * (_pull * _tick), ForceMode2D.Impulse);
                }
            }
            if (_health != null && ((Vector2)_health.transform.position - (Vector2)transform.position).sqrMagnitude <= _radius * _radius)
            {
                _healRemainder += _healing * _tick;
                int heal = Mathf.FloorToInt(_healRemainder);
                if (heal > 0) { _healRemainder -= heal; _health.Heal(heal); }
            }
            _tick = 0;
        }
        if (_remaining <= 0) Destroy(gameObject);
    }
}
