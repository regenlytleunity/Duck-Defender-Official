using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float Speed = 10f;
    public int Damage = 1;
    public float Lifetime = 4f;

    private float _timer;

    void OnEnable() => _timer = Lifetime;

    void Update()
    {
        transform.Translate(Vector2.right * Speed * Time.deltaTime);

        _timer -= Time.deltaTime;
        if (_timer <= 0) gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // FIX: Get the health component and apply damage
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                Debug.Log("OUCH! Player hit!");
                playerHealth.TakeDamage(Damage);
            }
            
            gameObject.SetActive(false);
        }
        else if (collision.CompareTag("Ground"))
        {
            gameObject.SetActive(false);
        }
    }
}