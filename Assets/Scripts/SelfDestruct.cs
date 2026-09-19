using UnityEngine;

public class SelfDestruct : MonoBehaviour
{
    [Tooltip("Time in seconds before this object is destroyed.")]
    public float Lifetime = 1.0f;

    void Start()
    {
        Destroy(gameObject, Lifetime);
    }
}