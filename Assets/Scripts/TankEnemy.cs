using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class TankEnemy : SwarmerEnemy
{
    // Retained for existing prefab serialization.
    [HideInInspector] public float WallCheckDistance = 1;
    [HideInInspector] public float AttackDuration = .6f;
    [HideInInspector] public float DamagePoint = .7f;
    [HideInInspector] public float MaxDamageReduction = .5f;
    [HideInInspector] public float ZeroReductionDistance;
    [HideInInspector] public Color ShieldedColor = new Color(.5f, .5f, .7f);
    [HideInInspector] public Color VulnerableColor = Color.white;

    [Header("Protection Chains")]
    [Min(.1f)] public float ChainRange = 8;
    [Min(.1f)] public float ChainSearchInterval = .5f;
    [Range(0, 1)] public float GroundSpeedMultiplier = .85f;
    public LineRenderer ChainPrefab;
    public Color ChainColor = new Color(.5f, .8f, 1);
    readonly List<EnemyBase> _protected = new List<EnemyBase>(4);
    readonly List<LineRenderer> _chains = new List<LineRenderer>(4);
    int _lifetimeLinks, _defeatedLinks;
    float _nextChainSearch, _maxShield;
    public float ShieldHealth { get; private set; }
    public int LifetimeLinks => _lifetimeLinks;
    public int DefeatedLinks => _defeatedLinks;
    protected override float EliteHealthMultiplier => 1;
    protected override float HealthAtWave(int wave) => 4 * (WaveManager.Instance != null ? WaveManager.Instance.BasicGroundHealth(wave) : Mathf.Floor(2 + WaveManager.HealthIncreaseAtWave(wave)));
    protected override float SpeedAtSpawn => (WaveManager.Instance != null ? WaveManager.Instance.BasicGroundSpeed() : 4) * GroundSpeedMultiplier;
    protected override float RuntimeSpeedMultiplier => 1 + (IsElite ? .1f * _defeatedLinks : 0);
    protected override float StrikeDamage => DamageOnHit + (IsElite ? .25f * _defeatedLinks : 0);
    protected override bool StunOnHit => false;

    protected override void Update()
    {
        base.Update();
        if (!IsAlive) return;
        if (Time.timeScale > 0 && Time.time >= _nextChainSearch)
        {
            _nextChainSearch = Time.time + ChainSearchInterval;
            AcquireChains();
        }
        for (int i = 0; i < _protected.Count; i++)
        {
            var enemy = _protected[i];
            if (_chains[i] == null) continue;
            _chains[i].enabled = enemy != null && enemy.IsAlive && enemy.Protector == this;
            if (!_chains[i].enabled) continue;
            _chains[i].SetPosition(0, transform.position);
            _chains[i].SetPosition(1, enemy.transform.position);
        }
    }

    public void AcquireChains()
    {
        if (!IsAlive) return;
        while (_lifetimeLinks < 4)
        {
            EnemyBase nearest = null;
            float distance = ChainRange * ChainRange;
            foreach (var enemy in ActiveEnemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy is TankEnemy || enemy.Protector != null || _protected.Contains(enemy)) continue;
                float candidate = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude;
                if (candidate <= distance) { nearest = enemy; distance = candidate; }
            }
            if (nearest == null) break;
            nearest.Protector = this;
            _protected.Add(nearest);
            _lifetimeLinks++;
            LineRenderer line = null;
            if (Application.isPlaying)
            {
                line = ChainPrefab != null ? Instantiate(ChainPrefab, transform) : new GameObject("Protection Chain").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.useWorldSpace = true; line.positionCount = 2;
                if (ChainPrefab == null)
                {
                    line.startWidth = line.endWidth = .06f;
                    line.startColor = line.endColor = ChainColor;
                    if (SpriteRen != null) { line.sharedMaterial = SpriteRen.sharedMaterial; line.sortingLayerID = SpriteRen.sortingLayerID; line.sortingOrder = SpriteRen.sortingOrder + 1; }
                }
            }
            _chains.Add(line);
        }
    }

    public void AbsorbDamage(float damage, PlayerStats owner = null)
    {
        if (!CanTakeDamage || damage <= 0) return;
        DamageOwner = owner;
        if (!IsElite) { ApplyHealthDamage(damage); return; }
        ShieldHealth += damage;
        _maxShield = Mathf.Max(_maxShield, ShieldHealth);
        UpdateShield();
    }

    protected override void ReceiveDamage(float damage)
    {
        if (!CanTakeDamage || damage <= 0) return;
        float absorbed = Mathf.Min(ShieldHealth, damage);
        ShieldHealth -= absorbed;
        UpdateShield();
        ApplyHealthDamage(damage - absorbed);
    }

    void UpdateShield() { if (HealthBarInstance != null) HealthBarInstance.UpdateShield(ShieldHealth, _maxShield); }
    public float GetCurrentDamageReduction() => 0; // Distance armor removed.

    public void OnProtectedEnemyKilled(EnemyBase enemy)
    {
        if (enemy == null || enemy.Protector != this || !_protected.Contains(enemy)) return;
        enemy.Protector = null;
        _defeatedLinks = Mathf.Min(4, _defeatedLinks + 1);
    }

    void ReleaseChains()
    {
        foreach (var enemy in _protected) if (enemy != null && enemy.Protector == this) enemy.Protector = null;
        foreach (var line in _chains) if (line != null) line.enabled = false;
    }
    protected override void Die() { ReleaseChains(); base.Die(); }
    protected override void OnDisable() { ReleaseChains(); base.OnDisable(); }
}
