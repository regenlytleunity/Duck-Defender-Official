using UnityEngine;
using System.Collections;

/// <summary>
/// 1.4.11: Adds support for "passive coins" - coins spawned by CoinsPerSecond / CoinsPerWave 
/// upgrades that pop off the player with a small force, then auto-magnetize quickly so they 
/// don't escape the player's pickup range.
/// 
/// Per outline (clarification 25): "The coins shouldn't go that far away from the player so 
/// by the time they are able to be picked up they should just go auto magnetized to the player."
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Coin : MonoBehaviour
{
    [Header("Settings")]
    public int CoinValue = 1;
    public float ExplosionForce = 5.0f;
    public float DelayBeforeMagnet = 0.5f;
    public float FlySpeed = 10.0f;

    [Header("Physics")]
    [Range(0f, 1f)]
    public float Bounciness = 0.3f;
    [Range(0f, 1f)]
    public float Friction = 0.4f;

    [Header("Passive Coin Mode (1.4.11)")]
    [Tooltip("If true, this coin uses tighter behavior: weaker pop-out force and short delay before forced auto-magnet.")]
    public bool IsPassiveCoin = false;

    [Tooltip("Force applied to passive coins on spawn (weaker than enemy-drop coins).")]
    public float PassivePopForce = 2.0f;

    [Tooltip("Seconds after spawn before passive coins force-magnetize regardless of distance. " +
             "Should be LONGER than PassiveCoinPickupImmunity so the player can see the coin " +
             "before it starts flying toward them.")]
    public float PassiveForceMagnetDelay = 1.2f;

    [Tooltip("1.4.11 PATCH: how long passive coins are IMMUNE from being collected. " +
             "This makes per-second/per-wave coin gifts visible to the player so they feel " +
             "satisfying rather than instantly absorbed.")]
    public float PassiveCoinPickupImmunity = 1.0f;

    private Transform _player;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private SpriteRenderer _renderer;
    private bool _isFlyingToPlayer = false;
    private bool _readyForMagnet = false;
    private float _spawnTime;
    private Color _baseColor;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer != null) _baseColor = _renderer.color;
        _collider.isTrigger = false;

        if (_rb.sharedMaterial == null)
        {
            PhysicsMaterial2D mat = new PhysicsMaterial2D("CoinMaterial");
            mat.bounciness = Bounciness;
            mat.friction = Friction;
            _rb.sharedMaterial = mat;
            _collider.sharedMaterial = mat;
        }
    }

    /// <summary>
    /// Called by LevelManager.SpawnPassiveCoin() before activating.
    /// </summary>
    public void ConfigureAsPassiveCoin()
    {
        IsPassiveCoin = true;
    }

    /// <summary>
    /// 1.4.11 PATCH: while the coin is in pickup immunity, physically ignore the player's 
    /// collider so the player can walk straight through it. Ground collision still works.
    /// Re-enables collision once immunity ends.
    /// </summary>
    void UpdatePlayerCollisionIgnore()
    {
        if (!IsPassiveCoin) return;
        if (_player == null || _collider == null) return;

        Collider2D playerCol = _player.GetComponent<Collider2D>();
        if (playerCol == null) return;

        bool shouldIgnore = IsPickupImmune();
        Physics2D.IgnoreCollision(_collider, playerCol, shouldIgnore);
    }

    /// <summary>
    /// 1.4.11 PATCH: True while this coin is in its pickup-immunity window. 
    /// Used by both magnet and collision paths to ignore the player.
    /// </summary>
    bool IsPickupImmune()
    {
        if (!IsPassiveCoin) return false;
        return Time.time - _spawnTime < PassiveCoinPickupImmunity;
    }

    void Start()
    {
        _spawnTime = Time.time;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        // 1.4.11 PATCH: ignore player collision during immunity
        UpdatePlayerCollisionIgnore();
        _playerCollisionIgnored = IsPickupImmune();

        // Initial pop-out burst
        float popForce = IsPassiveCoin ? PassivePopForce : ExplosionForce;
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        randomDir.y = Mathf.Abs(randomDir.y);
        _rb.AddForce(randomDir * popForce, ForceMode2D.Impulse);

        StartCoroutine(SettleRoutine());
    }

    private bool _playerCollisionIgnored = false;

    void Update()
    {
        if (_player == null) return;

        // 1.4.11 PATCH: re-enable player collision once immunity ends
        bool currentlyImmune = IsPickupImmune();
        if (_playerCollisionIgnored != currentlyImmune)
        {
            _playerCollisionIgnored = currentlyImmune;
            UpdatePlayerCollisionIgnore();
        }

        // 1.4.11 PATCH 2: removed alpha pulse - was distracting. Coins now stay 
        // at full opacity during immunity. The coin physically can't be picked up 
        // for the immunity window, but visually it just looks like a normal coin.

        if (_readyForMagnet && !_isFlyingToPlayer)
        {
            // 1.4.11 PATCH: don't fly toward the player while pickup-immune. 
            // The coin pops, sits visible for ~1s, then magnetizes.
            if (IsPickupImmune()) return;

            // Passive coins force-magnetize once immunity ends, regardless of distance
            if (IsPassiveCoin && Time.time - _spawnTime > PassiveForceMagnetDelay)
            {
                BeginFlyToPlayer();
                return;
            }

            float magnetRange = 3.0f;
            if (PlayerStats.Instance != null) magnetRange = PlayerStats.Instance.MagnetRange;

            float dist = Vector2.Distance(transform.position, _player.position);
            if (dist <= magnetRange)
            {
                BeginFlyToPlayer();
            }
        }

        if (_isFlyingToPlayer)
        {
            float dist = Vector2.Distance(transform.position, _player.position);
            float currentSpeed = FlySpeed + (5f / (dist + 0.1f));
            transform.position = Vector3.MoveTowards(transform.position, _player.position, currentSpeed * Time.deltaTime);
        }
    }

    IEnumerator SettleRoutine()
    {
        // Passive coins skip most of the settle delay since they're meant to feel snappy
        float delay = IsPassiveCoin ? Mathf.Min(0.15f, DelayBeforeMagnet) : DelayBeforeMagnet;
        yield return new WaitForSeconds(delay);
        _readyForMagnet = true;
    }

    void BeginFlyToPlayer()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.isKinematic = true;
        _collider.isTrigger = true;
        _isFlyingToPlayer = true;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // 1.4.11 PATCH: passive coins ignore the player during immunity window
        if (IsPickupImmune()) return;
        if (collision.CompareTag("Player")) Collect();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 1.4.11 PATCH: passive coins ignore the player during immunity window
        if (IsPickupImmune()) return;
        if (collision.collider.CompareTag("Player")) Collect();
    }

    public bool SecondaryMeteorOnPickup;
    bool _collected;
    void Collect()
    {
        if (_collected) return;
        _collected = true;
        if (SecondaryMeteorOnPickup && PlayerController.Instance != null) PlayerController.Instance.SpawnSecondaryMeteor();
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Coin_Collection");

        if (LevelManager.Instance != null)
            LevelManager.Instance.AddCoins(CoinValue);

        Destroy(gameObject);
    }
}