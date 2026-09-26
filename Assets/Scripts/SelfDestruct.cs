using UnityEngine;

public class SelfDestruct : MonoBehaviour
{
    [Tooltip("Time in seconds before this object is destroyed.")]
    public float Lifetime = 1.0f;

    void Start()
    {
        // Pooled FX own their lifetime, including child objects with this helper.
        if (GetComponentInParent<PooledVisualEffect>() != null) return;
        Destroy(gameObject, Lifetime);
    }
}
