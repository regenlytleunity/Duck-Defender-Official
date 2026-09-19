using UnityEngine;

public class XPGem : MonoBehaviour
{
    [Header("Settings")]
    public int XPValue = 10;
    public float MagnetRange = 3.0f;
    public float MoveSpeed = 5.0f;

    private Transform _player;
    private bool _isMagnetized = false;

    void Start()
    {
        // Find player once to save performance
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;
    }

    void Update()
    {
        if (_player == null) return;

        float dist = Vector2.Distance(transform.position, _player.position);

        // 1. Check if player is close enough to pull the gem
        if (dist < MagnetRange)
        {
            _isMagnetized = true;
        }

        // 2. If magnetized, fly towards player
        if (_isMagnetized)
        {
            // Move faster as we get closer (Snappy feel)
            float speed = MoveSpeed + (10f / (dist + 0.1f)); 
            transform.position = Vector3.MoveTowards(transform.position, _player.position, speed * Time.deltaTime);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // Give XP to the LevelManager
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.AddXP(XPValue);
            }
            
            // TODO: Play "Ding" sound effect
            Destroy(gameObject);
        }
    }
}