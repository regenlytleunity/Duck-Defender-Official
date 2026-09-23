using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class HealingOrb : MonoBehaviour
{
    public float Lifetime = 20;
    public float PickupRadius = .6f;
    public float AttractionRadius = 4;
    public float AttractionSpeed = 7;
    PlayerHealth _health;
    bool _collected;
    void Start()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;
        _health = PlayerStats.Instance != null ? PlayerStats.Instance.GetComponent<PlayerHealth>() : null;
        Destroy(gameObject, Lifetime);
    }
    void Update()
    {
        if (_health == null || _collected || _health.IsDead) return;
        float distance = Vector2.Distance(transform.position, _health.transform.position);
        if (distance <= AttractionRadius)
            transform.position = Vector3.MoveTowards(transform.position, _health.transform.position, AttractionSpeed * Time.deltaTime);
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
