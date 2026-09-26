using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class DefenderWall : MonoBehaviour
{
    readonly System.Collections.Generic.List<EnemyBase> _damageTargets = new System.Collections.Generic.List<EnemyBase>();
    public int Health = 5;
    public float ShockwaveRadius = 10;
    public float Knockback = 8;
    public float ContactDamageInterval = 1;
    public GameObject ShockwaveVisual;
    public float GravityScale = 2;
    readonly Dictionary<int, float> _nextHits = new Dictionary<int, float>();
    bool _broken;
    void Awake()
    {
        // Also repairs already-authored prefabs that predate the Rigidbody requirement.
        var body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = Mathf.Max(.1f, GravityScale);
        body.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        GetComponent<BoxCollider2D>().isTrigger = false;
    }
    public void TakeDamage(int damage)
    {
        if (_broken || damage <= 0) return;
        Health -= damage;
        if (Health > 0) return;
        _broken = true;
        ObjectPooler.SpawnEffect(ShockwaveVisual, transform.position, Quaternion.identity);
        EnemyBase.CopyActiveEnemies(_damageTargets);
        foreach (var enemy in _damageTargets)
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
