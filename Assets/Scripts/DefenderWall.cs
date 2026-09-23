using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider2D))]
public class DefenderWall : MonoBehaviour
{
    public int Health = 5;
    public float ShockwaveRadius = 10;
    public float Knockback = 8;
    public float ContactDamageInterval = 1;
    public GameObject ShockwaveVisual;
    readonly Dictionary<int, float> _nextHits = new Dictionary<int, float>();
    bool _broken;
    public void TakeDamage(int damage)
    {
        if (_broken || damage <= 0) return;
        Health -= damage;
        if (Health > 0) return;
        _broken = true;
        if (ShockwaveVisual != null) Instantiate(ShockwaveVisual, transform.position, Quaternion.identity);
        foreach (var enemy in EnemyBase.ActiveEnemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            Vector2 direction = enemy.transform.position - transform.position;
            if (direction.sqrMagnitude <= ShockwaveRadius * ShockwaveRadius)
                enemy.ApplyKnockback(direction.normalized * Knockback);
        }
        Destroy(gameObject);
    }
    void OnCollisionEnter2D(Collision2D collision) { Contact(collision.collider); }
    void OnCollisionStay2D(Collision2D collision) { Contact(collision.collider); }
    void Contact(Collider2D other)
    {
        var enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy == null || !enemy.IsAlive) return;
        int id = enemy.GetInstanceID();
        if (_nextHits.TryGetValue(id, out float next) && Time.time < next) return;
        _nextHits[id] = Time.time + ContactDamageInterval;
        TakeDamage(enemy.DamageOnHit);
    }
}
