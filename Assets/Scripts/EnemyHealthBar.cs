using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    public Slider HealthSlider;
    public Vector3 Offset = new Vector3(0, 1.2f, 0); // Height above enemy head

    private Transform _target;

    public void Initialize(Transform target, float maxHealth)
    {
        _target = target;
        HealthSlider.maxValue = maxHealth;
        HealthSlider.value = maxHealth;
    }

    public void UpdateHealth(float currentHealth)
    {
        HealthSlider.value = currentHealth;
    }

    void LateUpdate()
    {
        if (_target == null)
        {
            Destroy(gameObject); // If enemy dies, destroy bar
            return;
        }

        // 1. Follow Position
        transform.position = _target.position + Offset;

        // 2. Prevent Rotation (Billboarding)
        // Even if the enemy flips left (Scale X -1), we want the bar to stay normal
        transform.rotation = Quaternion.identity;
    }
}