using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Central runtime stat store. Every card writes to this; every gameplay system reads from this.
/// 
/// 1.4.11 changes:
/// - Renamed Flinger* to Marksman* (kept legacy aliases for compatibility)
/// - Added MiniGun, Airburst, Blink, Hypersonic, FireTrail, SecondWind, Sabotage, Luck, ElementalTurret, FeatherSize
/// - Removed the old "CoinShot" system (still has fields for legacy compatibility)
/// 
/// 1.4.13 fix:
/// - Money High stacks now use ABSOLUTE expiration timestamps instead of a 
///   per-frame countdown. The previous decrement-each-Update approach could leave 
///   stacks permanently alive if Update was ever skipped (e.g. timeScale lingering 
///   at 0 across a scene boundary). With absolute times, the expiration check is 
///   stateless and pruning happens both in Update AND lazily on every read of the 
///   multiplier, so no path through the code can leave stacks stuck.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    // ============================================================
    // TECH / GADGET STATS
    // ============================================================

    [Header("Aura (damaging)")]
    public float AuraDamage = 0.0f;
    public float AuraRadius = 3.0f;

    [Header("Homing")]
    public bool HomingProjectiles = false;
    public float HomingSpeedBonus = 0f;

    [Header("Slowing Aura")]
    public bool HasSlowingAura = false;
    public float SlowingAuraSlowPercent = 0f;       // 0-1
    public float SlowingAuraRadius = 3.5f;

    [Header("Lucky Talisman")]
    [Tooltip("0-1 range. Boosts the roll chance for higher rarities on level up.")]
    public float LuckPercent = 0f;

    [Header("Big Feathers")]
    [Tooltip("Visual scale multiplier for normal feathers. 1.0 = default size.")]
    public float FeatherSize = 1.0f;

    [Header("Airburst Feathers")]
    [Tooltip("Number of mini sub-feathers spawned behind enemies on regular-feather hits. 0 = disabled.")]
    public int AirburstFeatherCount = 0;

    [Header("Sabotage")]
    [Tooltip("Fraction of HP enemies spawn with already missing. Capped at 0.5 in EnemyBase.")]
    public float EnemyHealthMissingPercent = 0f;

    // ============================================================
    // ECONOMY
    // ============================================================

    [Header("Economy Stats")]
    public float CoinsPerSecond = 0.0f;
    public float PassiveIncomeRate = 0.0f;
    public float InterestRate = 0.0f;
    public float MagnetRange = 3.0f;
    public float XPMultiplier = 1.0f;
    public float CoinDropMultiplier = 1.0f;

    [Header("Meteor")]
    public bool HasCoinMeteors = false;
    public int MeteorThreshold = 10;
    public float MeteorRadius = 4.0f;
    public int MeteorDamage = 50;

    [Header("Tripleshot (was Coin Shot)")]
    public bool HasTripleshot = false;
    public int TripleshotThreshold = 15;
    public float TripleshotDuration = 5.0f;

    // === Legacy aliases for CoinShot, used by old code paths ===
    public bool HasCoinShot { get { return HasTripleshot; } set { HasTripleshot = value; } }
    public int CoinShotThreshold { get { return TripleshotThreshold; } set { TripleshotThreshold = value; } }
    public float CoinShotDuration { get { return TripleshotDuration; } set { TripleshotDuration = value; } }

    [Header("Profit Power (Money High)")]
    public float DamagePerCoin = 0.0f;
    public float MoneyHighDuration = 0.0f;

    [Tooltip("If true, prints Money High stack add/expire events to the console. " +
             "Useful for verifying that the buff fades when it should.")]
    public bool LogMoneyHighEvents = false;

    /// <summary>
    /// 1.4.13 REWORK: each active Money High stack stores its ABSOLUTE expiration time 
    /// (Time.time + duration) instead of a remaining-time countdown. The previous 
    /// decrementing design could fail silently if Update missed ticks for any reason 
    /// (long pauses, scene transitions, timeScale weirdness). With absolute times, the 
    /// expiration check is stateless — we just compare to Time.time when needed.
    /// 
    /// Stacks are pruned on read AND in Update, so even if Update somehow stalled, the 
    /// next damage roll would clean expired stacks out automatically.
    /// </summary>
    private List<float> _moneyHighExpirations = new List<float>();

    // ============================================================
    // 1.4.11 MINI GUN
    // ============================================================

    [Header("Mini Gun")]
    [Tooltip("True if the player has the Mini Gun upgrade.")]
    public bool HasMiniGun = false;

    [Tooltip("Seconds of continuous firing before forced overheat.")]
    public float MiniGunOverheatThreshold = 5.0f;

    [Tooltip("Seconds for the overheat meter to fully recover from max.")]
    public float MiniGunRecoveryRate = 4.0f;

    // ============================================================
    // 1.4.11 BLINK
    // ============================================================

    [Header("Blink")]
    public bool HasBlink = false;
    [Tooltip("Seconds between blinks while moving.")]
    public float BlinkInterval = 3.0f;
    [Tooltip("Seconds each blink lasts (player is invulnerable and clips through enemies).")]
    public float BlinkDuration = 0.15f;

    // ============================================================
    // 1.4.11 LOW GRAVITY
    // ============================================================

    [Header("Player Gravity")]
    [Tooltip("Multiplier on the player's default gravity. 1.0 = default, 0.5 = half gravity (floatier).")]
    public float PlayerGravityMultiplier = 1.0f;

    // ============================================================
    // 1.4.11 HYPERSONIC
    // ============================================================

    [Header("Hypersonic (Max Speed Damage)")]
    [Tooltip("Damage dealt to enemies on collision when player is at >= 90% max run speed. 0 = disabled.")]
    public int MaxSpeedDamage = 0;

    // ============================================================
    // 1.4.11 FIRE TRAIL
    // ============================================================

    [Header("Fire Trail")]
    public bool HasFireTrail = false;
    public int FireTrailDamage = 0;
    public float FireTrailDuration = 2.0f;

    // ============================================================
    // 1.4.11 SECOND WIND
    // ============================================================

    [Header("Second Wind")]
    public bool HasSecondWind = false;
    [Tooltip("Seconds before Second Wind can save the player again.")]
    public float SecondWindCooldown = 60f;
    [Tooltip("Percentage of MaxHealth restored when Second Wind triggers. 0-1 range.")]
    public float SecondWindHealthRecovery = 0.5f;
    [Tooltip("Seconds of invulnerability after Second Wind triggers.")]
    public float SecondWindInvulnDuration = 2.0f;

    [Tooltip("Runtime: when Second Wind was last triggered. -999 means it's currently available.")]
    public float LastSecondWindUseTime = -999f;

    // ============================================================
    // 1.4.11 SPECIAL FEATHER SYSTEM (unified from 1.4.9)
    // ============================================================

    public enum FeatherType
    {
        Healing,
        Frosty,
        Poison,
        Metal,
        Explosive,
        Buckshot
    }

    [System.Serializable]
    public class SpecialFeatherInstance
    {
        public string SourceCardID;
        public FeatherType Type;
        public int Threshold;
        public int ShotCounter;

        public int HealAmount;
        public float FreezeDuration;
        public float PoisonDPS;
        public float BonusKnockback;
        public float ExplosionRadius;
        public int BuckshotPellets;

        public SpecialFeatherInstance(string cardID, FeatherType type, int threshold)
        {
            SourceCardID = cardID;
            Type = type;
            Threshold = threshold;
            ShotCounter = 0;
        }
    }

    [Header("Special Feather Instances")]
    public List<SpecialFeatherInstance> SpecialFeathers = new List<SpecialFeatherInstance>();

    public bool HasHealingFeather = false;

    // ============================================================
    // 1.4.11 TURRETS (Marksman / Medic / Protector / Elemental)
    // ============================================================

    [Header("Marksman (was Flinger)")]
    public bool HasMarksman = false;
    public float MarksmanInterval = 3.0f;
    public int MarksmanTargetCount = 1;

    // === Legacy aliases for Flinger, used by old code paths ===
    public bool HasFlinger { get { return HasMarksman; } set { HasMarksman = value; } }
    public float FlingerInterval { get { return MarksmanInterval; } set { MarksmanInterval = value; } }
    public int FlingerTargetCount { get { return MarksmanTargetCount; } set { MarksmanTargetCount = value; } }

    [Header("Medic")]
    public bool HasMedic = false;
    public float MedicInterval = 5.0f;
    public int MedicHealAmount = 1;

    [Header("Protector")]
    public bool HasProtector = false;
    public float ProtectorInterval = 4.0f;
    public float ProtectorRadius = 3.5f;
    public float ProtectorKnockback = 8.0f;

    [Header("Elemental Turret (1.4.11)")]
    public bool HasElementalTurret = false;
    [Tooltip("Seconds between elemental turret shots. Fires 1 random elemental feather variant.")]
    public float ElementalTurretInterval = 4.0f;

    // ============================================================
    // DASH GRANT FLAGS (preserved from 1.4.9)
    // ============================================================

    [Header("Shockwave Dash Grant")]
    public bool HasShockwaveDownDash = false;
    public bool HasFullDash = false;

    // ============================================================
    // RUNTIME STATE
    // ============================================================

    [HideInInspector] public int CoinsCollectedRun = 0;

    public event Action<int> OnCoinsGained;
    public event Action OnEnemyKilled;
    public event Action OnTurretsChanged;
    public event Action OnSecondWindChanged;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // 1.4.13: prune expired Money High stacks. Each stack stores its absolute 
        // expiration time, so a stack is "alive" if Time.time < stack.expirationTime.
        // Doing this in Update keeps stack counts low (useful when many coins are 
        // collected), but the same prune is also done lazily in GetCurrentMoneyHighMultiplier 
        // so the math is correct even if Update is somehow skipped.
        PruneExpiredMoneyHighStacks();
    }

    void PruneExpiredMoneyHighStacks()
    {
        if (_moneyHighExpirations.Count == 0) return;

        float now = Time.time;
        for (int i = _moneyHighExpirations.Count - 1; i >= 0; i--)
        {
            if (_moneyHighExpirations[i] <= now)
            {
                _moneyHighExpirations.RemoveAt(i);
                if (LogMoneyHighEvents)
                {
                    Debug.Log($"[MoneyHigh] Stack expired. Remaining: {_moneyHighExpirations.Count}");
                }
            }
        }
    }

    public void ReportCoinsGained(int amount)
    {
        CoinsCollectedRun += amount;
        OnCoinsGained?.Invoke(amount);

        // 1.4.13: Each coin adds ONE stack with its own absolute expiration time.
        // Stacks don't reset each other - if you collect 10 coins in 1 second, you 
        // have 10 active stacks, each expiring after MoneyHighDuration seconds from 
        // ITS collection time.
        //
        // Per design: collecting coin (a) gives the boost; collecting coin (b) while 
        // (a) is still active does NOT extend (a)'s duration. (b) gets its own 
        // independent timer.
        if (MoneyHighDuration > 0 && DamagePerCoin > 0 && amount > 0)
        {
            float expiration = Time.time + MoneyHighDuration;
            for (int i = 0; i < amount; i++)
            {
                _moneyHighExpirations.Add(expiration);
            }
            if (LogMoneyHighEvents)
            {
                Debug.Log($"[MoneyHigh] Added {amount} stack(s), each expires at " +
                          $"{expiration:F2} (in {MoneyHighDuration:F2}s). Total active: {_moneyHighExpirations.Count}");
            }
        }
    }

    public void RegisterEnemyKill()
    {
        OnEnemyKilled?.Invoke();
    }

    /// <summary>
    /// 1.4.13 REWORK: Money High multiplier is driven entirely by active stacks. Each 
    /// active stack contributes DamagePerCoin to the multiplier. Stacks are pruned both 
    /// in Update() AND right here on read, guaranteeing correctness even if Update 
    /// somehow misses ticks.
    /// 
    /// Per spec:
    ///   - Collecting coin (a) adds a stack with duration MoneyHighDuration
    ///   - Collecting coin (b) while (a) is active adds ANOTHER independent stack; 
    ///     (a) keeps ticking on its own clock, (b) gets its own clock
    ///   - When (a) expires, the multiplier drops by exactly DamagePerCoin
    ///   - When ALL stacks expire, multiplier returns to 1.0
    /// </summary>
    public float GetCurrentMoneyHighMultiplier()
    {
        // Prune-on-read guarantees correctness even if Update was skipped this frame.
        PruneExpiredMoneyHighStacks();

        float mult = 1.0f;
        if (DamagePerCoin > 0 && _moneyHighExpirations.Count > 0)
        {
            mult += (DamagePerCoin * _moneyHighExpirations.Count);
        }
        return mult;
    }

    /// <summary>
    /// 1.4.13: Public read-only access to active stack count. Useful for HUD displays 
    /// or debug overlays that want to show how much Money High the player has stacked up.
    /// </summary>
    public int ActiveMoneyHighStacks
    {
        get
        {
            PruneExpiredMoneyHighStacks();
            return _moneyHighExpirations.Count;
        }
    }

    public void OnShockwaveSelected()
    {
        if (!HasFullDash) HasShockwaveDownDash = true;
    }

    public void OnDashSelected()
    {
        HasFullDash = true;
        HasShockwaveDownDash = false;
    }

    public SpecialFeatherInstance GetSpecialFeatherByCardID(string cardID)
    {
        return SpecialFeathers.Find(f => f.SourceCardID == cardID);
    }

    public List<SpecialFeatherInstance> GetSpecialFeathersByType(FeatherType type)
    {
        return SpecialFeathers.FindAll(f => f.Type == type);
    }

    public void ResetSpecialFeatherCounters()
    {
        foreach (var feather in SpecialFeathers)
        {
            feather.ShotCounter = 0;
        }
    }

    public void NotifyTurretsChanged()
    {
        OnTurretsChanged?.Invoke();
    }

    public void NotifySecondWindChanged()
    {
        OnSecondWindChanged?.Invoke();
    }

    // === Legacy shims ===
    public SpecialFeatherInstance GetFeatherByCardID(string cardID) => GetSpecialFeatherByCardID(cardID);
    public void ResetFeatherCounters() => ResetSpecialFeatherCounters();

    // === Second Wind helpers ===
    /// <summary>
    /// True if Second Wind is currently off cooldown and can save the player.
    /// </summary>
    public bool IsSecondWindReady()
    {
        if (!HasSecondWind) return false;
        return Time.time >= LastSecondWindUseTime + SecondWindCooldown;
    }

    /// <summary>
    /// Marks Second Wind as used; starts its cooldown.
    /// </summary>
    public void ConsumeSecondWind()
    {
        LastSecondWindUseTime = Time.time;
        NotifySecondWindChanged();
    }
}