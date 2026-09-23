using UnityEngine;

// Shared lifetime/area behavior for Wormhole, Volcano and Savior. Visuals are prefab-owned.
public class AscensionArea : MonoBehaviour
{
    float _radius, _damage, _healing, _pull, _remaining, _healRemainder;
    float _tick;
    PlayerHealth _health;
    public void Initialize(float radius, float seconds, float damage = 0, float healing = 0, float pull = 0)
    {
        _radius = radius; _remaining = seconds; _damage = damage; _healing = healing; _pull = pull;
        _health = PlayerStats.Instance != null ? PlayerStats.Instance.GetComponent<PlayerHealth>() : null;
        transform.localScale = Vector3.one * radius;
    }
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, _remaining);
        if (dt <= 0) return;
        _remaining -= dt;
        _tick += dt;
        if (_tick >= .1f || _remaining <= 0)
        {
            foreach (var enemy in EnemyBase.ActiveEnemies)
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
