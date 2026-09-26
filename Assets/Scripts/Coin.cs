using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Coin : MonoBehaviour
{
    [Header("Settings")]
    public int CoinValue = 1;
    public float ExplosionForce = 5f;
    public float DelayBeforeMagnet = .5f;
    public float FlySpeed = 10f;
    [Header("Physics")]
    [Range(0, 1)] public float Bounciness = .3f;
    [Range(0, 1)] public float Friction = .4f;
    [Header("Passive Coin Mode (1.4.11)")]
    public bool IsPassiveCoin;
    public float PassivePopForce = 2f;
    public float PassiveForceMagnetDelay = 1.2f;
    public float PassiveCoinPickupImmunity = 1f;
    public bool SecondaryMeteorOnPickup;

    // One spatial pass for all coins, scheduled by the existing LevelManager.
    const float MergeRadius = 2f;
    const float MergeDuration = .18f;
    static readonly HashSet<Coin> Active = new HashSet<Coin>();
    static readonly List<Coin> Snapshot = new List<Coin>();
    static readonly List<Coin> Group = new List<Coin>(10);
    static readonly Dictionary<Vector3Int, List<Coin>> Cells = new Dictionary<Vector3Int, List<Coin>>();
    static readonly Stack<List<Coin>> FreeCells = new Stack<List<Coin>>();
    static int _scanStart;
    class CoinMaterial { public PhysicsMaterial2D Material; public int Users; }
    static readonly Dictionary<Vector2, CoinMaterial> Materials = new Dictionary<Vector2, CoinMaterial>();

    Transform _player;
    Rigidbody2D _rb;
    Collider2D _collider;
    Vector3 _baseScale;
    Vector2 _materialKey;
    bool _ownsSharedMaterial;
    bool _isFlyingToPlayer, _collected, _started;
    float _spawnTime, _mergeUntil;
    Coin _mergeTarget;
    bool _merging;
    Vector3 _mergeOrigin;
    int _extraSecondaryMeteors;
    public int SecondaryMeteorCount => _extraSecondaryMeteors + (SecondaryMeteorOnPickup ? 1 : 0);
    bool CanMerge => _started && !_collected && !_merging && !_isFlyingToPlayer &&
        Time.time >= _mergeUntil && Time.time - _spawnTime >= DelayBeforeMagnet && (CoinValue == 1 || CoinValue == 10);

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _baseScale = transform.localScale;
        _collider.isTrigger = false;
        // Coins settle on terrain, but never become tiny solid steps under the player.
        int ground = LayerMask.GetMask("Ground");
        if (ground != 0) _rb.excludeLayers = ~ground;
        if (_rb.sharedMaterial == null)
        {
            _materialKey = new Vector2(Bounciness, Friction);
            if (!Materials.TryGetValue(_materialKey, out var material))
            {
                material = new CoinMaterial { Material = new PhysicsMaterial2D("Shared coin material")
                    { bounciness = Bounciness, friction = Friction } };
                Materials.Add(_materialKey, material);
            }
            material.Users++;
            _ownsSharedMaterial = true;
            _rb.sharedMaterial = _collider.sharedMaterial = material.Material;
        }
    }
    void OnEnable() { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }
    void OnDestroy()
    {
        if (!_ownsSharedMaterial || !Materials.TryGetValue(_materialKey, out var material)) return;
        _ownsSharedMaterial = false;
        if (--material.Users == 0)
        {
            Materials.Remove(_materialKey);
            if (Application.isPlaying) Destroy(material.Material);
            else DestroyImmediate(material.Material);
        }
    }
    public void ConfigureAsPassiveCoin() { IsPassiveCoin = true; }
    bool IsPickupImmune() => IsPassiveCoin && Time.time - _spawnTime < PassiveCoinPickupImmunity;

    void Start()
    {
        _started = true;
        _spawnTime = Time.time;
        if (PlayerController.Instance != null) _player = PlayerController.Instance.transform;
        Vector2 direction = Random.insideUnitCircle.normalized;
        direction.y = Mathf.Abs(direction.y);
        _rb.AddForce(direction * (IsPassiveCoin ? PassivePopForce : ExplosionForce), ForceMode2D.Impulse);
        UpdateStackScale();
    }
    void UpdateStackScale() { transform.localScale = _baseScale * (CoinValue >= 100 ? 1.36f : CoinValue >= 10 ? 1.18f : 1f); }

    void Update()
    {
        if (Time.timeScale == 0) return;
        if (_merging)
        {
            if (_mergeTarget == null) { Destroy(gameObject); return; }
            float t = Mathf.Clamp01(1 - (_mergeUntil - Time.time) / MergeDuration);
            transform.position = Vector3.Lerp(_mergeOrigin, _mergeTarget.transform.position, t * t);
            transform.localScale = _baseScale * Mathf.Lerp(1, .3f, t);
            if (t >= 1) Destroy(gameObject);
            return;
        }
        if (_collected || Time.time < _mergeUntil) return;
        if (_player == null && PlayerController.Instance != null) _player = PlayerController.Instance.transform;
        if (_player == null) return;
        if (!_isFlyingToPlayer && !_rb.simulated) _rb.simulated = true;
        if (IsPickupImmune()) return;
        float distance = Vector2.Distance(transform.position, _player.position);
        // Proximity pickup works without a solid player/coin collision or a trigger callback.
        if (distance <= .65f) { Collect(); return; }
        float delay = IsPassiveCoin ? Mathf.Min(.15f, DelayBeforeMagnet) : DelayBeforeMagnet;
        if (!_isFlyingToPlayer && Time.time - _spawnTime >= delay)
        {
            float range = PlayerStats.Instance != null ? PlayerStats.Boost(PlayerStats.Instance.MagnetRange) : 3f;
            if (distance <= range || (IsPassiveCoin && Time.time - _spawnTime >= PassiveForceMagnetDelay))
            {
                _isFlyingToPlayer = true;
                _rb.linearVelocity = Vector2.zero;
                _rb.simulated = false;
            }
        }
        if (_isFlyingToPlayer)
            transform.position = Vector3.MoveTowards(transform.position, _player.position,
                (FlySpeed + 5 / (distance + .1f)) * Time.deltaTime);
    }

    // 10 equal denominations in a true two-unit neighborhood, including neighboring cells.
    // Bounded work and rotating start position prevent a dense/offscreen pile monopolizing a frame.
    public static int MergeNearby()
    {
        foreach (var cell in Cells.Values) { cell.Clear(); FreeCells.Push(cell); }
        Cells.Clear(); Snapshot.Clear();
        foreach (var coin in Active)
        {
            if (coin == null || !coin.CanMerge) continue;
            Snapshot.Add(coin);
            var key = coin.CellKey();
            if (!Cells.TryGetValue(key, out var bucket))
            {
                bucket = FreeCells.Count > 0 ? FreeCells.Pop() : new List<Coin>();
                Cells.Add(key, bucket);
            }
            bucket.Add(coin);
        }
        int checks = 0, merged = 0, visited = 0, count = Snapshot.Count;
        for (; visited < count && checks < 4096 && merged < 32; visited++)
        {
            var target = Snapshot[(_scanStart + visited) % count];
            if (!target.CanMerge) continue;
            Group.Clear(); Group.Add(target);
            var key = target.CellKey();
            for (int x = -1; x <= 1 && Group.Count < 10 && checks < 4096; x++)
                for (int y = -1; y <= 1 && Group.Count < 10 && checks < 4096; y++)
                {
                    if (!Cells.TryGetValue(key + new Vector3Int(x, y, 0), out var bucket)) continue;
                    foreach (var candidate in bucket)
                    {
                        checks++;
                        if (candidate != target && candidate.CanMerge && candidate.CoinValue == target.CoinValue &&
                            ((Vector2)candidate.transform.position - (Vector2)target.transform.position).sqrMagnitude <= MergeRadius * MergeRadius)
                            Group.Add(candidate);
                        if (Group.Count == 10 || checks >= 4096) break;
                    }
                }
            if (Group.Count == 10) { target.AbsorbGroup(); merged++; }
        }
        _scanStart = count > 0 ? (_scanStart + visited) % count : 0;
        return merged;
    }
    Vector3Int CellKey() => new Vector3Int(Mathf.FloorToInt(transform.position.x / MergeRadius),
        Mathf.FloorToInt(transform.position.y / MergeRadius), IsPassiveCoin ? -CoinValue : CoinValue);

    void AbsorbGroup()
    {
        int meteors = SecondaryMeteorCount;
        for (int i = 1; i < Group.Count; i++)
        {
            var coin = Group[i];
            meteors += coin.SecondaryMeteorCount;
            CoinValue += coin.CoinValue;
            _spawnTime = Mathf.Max(_spawnTime, coin._spawnTime); // Preserve the newest passive immunity.
            coin.CoinValue = 0;
            coin.SecondaryMeteorOnPickup = false; coin._extraSecondaryMeteors = 0;
            coin._merging = true; coin._mergeTarget = this;
            coin._mergeOrigin = coin.transform.position;
            coin._mergeUntil = Time.time + MergeDuration;
            coin._rb.simulated = false;
        }
        SecondaryMeteorOnPickup = false; _extraSecondaryMeteors = meteors;
        _mergeUntil = Time.time + MergeDuration;
        _rb.linearVelocity = Vector2.zero; _rb.simulated = false;
        UpdateStackScale();
    }

    void Collect()
    {
        if (TryCollect()) Destroy(gameObject);
    }
    bool TryCollect()
    {
        if (_collected || _merging || IsPickupImmune() || Time.time < _mergeUntil || LevelManager.Instance == null) return false;
        _collected = true;
        LevelManager.Instance.QueueSecondaryMeteors(SecondaryMeteorCount);
        LevelManager.Instance.AddCoins(CoinValue);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Coin_Collection");
        return true;
    }
}
