using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 1.4.11 REWRITE + 1.4.12 BUGFIX.
/// 
/// CardManager is the single source of truth for applying card effects.
/// For each stat type, it knows where to write the value (PlayerStats, WeaponPlayer, PlayerController).
///
/// =====================================================================
/// 1.4.12 BUGFIX: SET-style stats now correctly handle re-picks.
/// =====================================================================
/// 
/// THE OLD BUG: SET-style stats (interval/threshold/cooldown values that live as 
/// literal seconds or counts) were doing `field = amount` every pickup. That works 
/// fine for the FIRST pickup (because GetAmountForPickup returns the full displayed 
/// value), but breaks on re-picks (which return just a delta like -0.3 seconds).
/// 
/// The classic symptom: Mini Gun overheat threshold of 5s on first pickup, then 
/// dropping to ~1s on the second pickup because the SET semantics overwrote 5s 
/// with the re-pick delta of 1.
/// 
/// THE FIX: track which stats have been "initialized" this run in 
/// _statsInitializedThisRun. The first card pickup that touches a SET-style stat 
/// REPLACES the default value (which is the polite term for "writes the displayed 
/// value into the field"); subsequent pickups ADD the delta on top.
/// 
/// ADDITIVE stats (Damage, FireRate, MoveSpeed, etc.) are unchanged - they always 
/// just `+=` regardless of pickup index.
/// 
/// Spec recap (for reference when re-reading this file in 6 months):
///   - MatchBaseValue: every pickup adds the full displayed value (e.g. each Faster
///     Firing pickup reduces FireRate by 0.05). Pure additive.
///   - MatchShopGrowth: first pickup writes the full shop-level value (e.g. sets 
///     MarksmanInterval to 2.7s); re-picks add AmountPerShopLevel (-0.3s each time).
///   - Custom: first pickup writes the full shop-level value; re-picks add 
///     InRunStackAmount. If InRunStackAmount = 0, re-picks have no effect (the 
///     `Mathf.Approximately(amount, 0f)` early-return handles that case).
/// </summary>
public class CardManager : MonoBehaviour
{
    public static CardManager Instance;

    [Header("Card Pool")]
    [Tooltip("All cards eligible to be offered. Filter by what's unlocked in the player's collection.")]
    public List<CardDefinition> AllCards = new List<CardDefinition>();

    [Header("Rarity Roll Weights (no luck)")]
    [Range(0f, 1f)] public float CommonRollWeight = 0.60f;
    [Range(0f, 1f)] public float RareRollWeight = 0.30f;
    [Range(0f, 1f)] public float LegendaryRollWeight = 0.08f;
    [Range(0f, 1f)] public float CorruptedRollWeight = 0.02f;

    void Awake()
    {
        Instance = this;
    }

    // ============================================================
    // RANDOM CARD SELECTION (Level Up Offers)
    // ============================================================

    /// <summary>
    /// Returns N random cards to offer the player on level up.
    /// Applies Lucky Talisman as a roll bias toward higher rarities.
    /// </summary>
    public List<CardDefinition> GetRandomCards(int count)
    {
        var unlocked = GetUnlockedCardPool();
        if (unlocked.Count == 0) return new List<CardDefinition>();

        List<CardDefinition> picks = new List<CardDefinition>();
        HashSet<string> chosenIDs = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            CardRarity targetRarity = RollRarity();
            var candidates = unlocked
                .Where(c => c.Rarity == targetRarity && !chosenIDs.Contains(c.ID))
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = unlocked.Where(c => !chosenIDs.Contains(c.ID)).ToList();
            }
            if (candidates.Count == 0) break;

            CardDefinition pick = candidates[Random.Range(0, candidates.Count)];
            picks.Add(pick);
            chosenIDs.Add(pick.ID);
        }

        return picks;
    }

    List<CardDefinition> GetUnlockedCardPool()
    {
        if (AllCards == null || AllCards.Count == 0) return new List<CardDefinition>();
        PlayerData data = SaveSystem.LoadData();

        return AllCards
            .Where(c => c != null)
            .Where(c => c.PackCategory == CardPackType.BaseSet ||
                        (data.CardCollection != null && data.CardCollection.Exists(s => s.CardID == c.ID)))
            .ToList();
    }

    /// <summary>
    /// LUCKY TALISMAN: shifts weight from Common into higher rarities based on LuckPercent.
    /// </summary>
    CardRarity RollRarity()
    {
        float luck = 0f;
        if (PlayerStats.Instance != null)
        {
            luck = Mathf.Clamp01(PlayerStats.Instance.LuckPercent);
        }

        float common = CommonRollWeight * (1f - luck * 0.7f);
        float commonStolen = CommonRollWeight - common;

        float upperTotal = RareRollWeight + LegendaryRollWeight + CorruptedRollWeight;
        if (upperTotal <= 0) upperTotal = 1f;
        float rare = RareRollWeight + commonStolen * (RareRollWeight / upperTotal);
        float legendary = LegendaryRollWeight + commonStolen * (LegendaryRollWeight / upperTotal);
        float corrupted = CorruptedRollWeight + commonStolen * (CorruptedRollWeight / upperTotal);

        float totalWeight = common + rare + legendary + corrupted;
        float roll = Random.value * totalWeight;

        if (roll < common) return CardRarity.Common;
        if (roll < common + rare) return CardRarity.Rare;
        if (roll < common + rare + legendary) return CardRarity.Legendary;
        return CardRarity.Corrupted;
    }

    // ============================================================
    // CARD LEVEL TRACKING
    // ============================================================

    private Dictionary<string, int> _cardRunPickups = new Dictionary<string, int>();

    /// <summary>
    /// 1.4.12: tracks which StatTypes have already been written to this run. Used to 
    /// implement "first pick of any card touching this stat REPLACES the default; 
    /// re-picks ADD the delta."
    /// 
    /// Per-stat granularity (not per-card) because multiple different cards can touch 
    /// the same stat (e.g. several Marksman variants).
    /// 
    /// Also tracks special-feather field initialization, keyed by "cardID:field" so 
    /// different cards that both touch a Frosty feather threshold are independent 
    /// (each special-feather instance is per-card, not per-stat).
    /// </summary>
    private HashSet<StatType> _statsInitializedThisRun = new HashSet<StatType>();
    private HashSet<string> _featherFieldsInitializedThisRun = new HashSet<string>();

    private PlayerData _cachedPlayerData = null;

    void EnsurePlayerDataLoaded()
    {
        if (_cachedPlayerData == null)
        {
            _cachedPlayerData = SaveSystem.LoadData();
        }
    }

    /// <summary>
    /// Returns the player's saved (shop) level for this card. 1 = base, 5 = fully upgraded.
    /// Reads from ShopManager when available (main menu scene), falls back to SaveSystem 
    /// (game scene where ShopManager isn't present).
    /// </summary>
    public int GetShopLevel(string cardID)
    {
        if (ShopManager.Instance != null)
        {
            var data = ShopManager.Instance.GetCardData(cardID);
            if (data != null) return data.Level;
        }
        
        EnsurePlayerDataLoaded();
        if (_cachedPlayerData != null && _cachedPlayerData.CardCollection != null)
        {
            var saved = _cachedPlayerData.CardCollection.Find(c => c.CardID == cardID);
            if (saved != null) return saved.Level;
        }
        
        return 1;
    }

    /// <summary>
    /// How many times this card has been picked up this run (not including the upcoming pickup).
    /// </summary>
    public int GetRunPickups(string cardID)
    {
        if (string.IsNullOrEmpty(cardID)) return 0;
        return _cardRunPickups.TryGetValue(cardID, out int n) ? n : 0;
    }

    public int GetCardLevel(string cardID)
    {
        return GetRunPickups(cardID);
    }

    /// <summary>
    /// The level shown on the card during in-game level-up offers. Always returns shop 
    /// level so the description matches the card index.
    /// </summary>
    public int GetNextPickupLevel(string cardID)
    {
        return GetShopLevel(cardID);
    }

    public void SetCardLevel(string cardID, int level)
    {
        _cardRunPickups[cardID] = level;
    }

    // ============================================================
    // APPLY CARD EFFECT
    // ============================================================

    public void ApplyCardEffect(CardDefinition card)
    {
        if (card == null) return;

        int shopLevel = GetShopLevel(card.ID);
        int pickupIndex = GetRunPickups(card.ID); // 0 on first pickup, 1 on second, etc.

        _cardRunPickups[card.ID] = pickupIndex + 1;

        if (card.Modifiers == null) return;

        foreach (var mod in card.Modifiers)
        {
            float amount = card.GetAmountForPickup(mod, shopLevel, pickupIndex);
            ApplyStat(card, mod.StatType, amount);
        }

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.NotifyTurretsChanged();
            PlayerStats.Instance.NotifySecondWindChanged();
        }
    }

    // ============================================================
    // SET-STYLE STAT HELPERS
    // ============================================================
    //
    // For stats that represent a LITERAL value (interval seconds, cooldown seconds,
    // threshold counts, etc.), the first pickup of any card touching that stat must 
    // REPLACE the default, and subsequent pickups must ADD the delta.
    //
    // SetOrAdd handles this transparently: pass the stat type and the amount, get 
    // back the value to write. Internally tracks which stats have been claimed.
    //
    // The optional `minClamp` and `maxClamp` keep the result in legal range.

    /// <summary>
    /// First call for a given stat returns `amount` (treating it as the full displayed value).
    /// Subsequent calls return `currentValue + amount` (treating amount as a delta).
    /// </summary>
    float SetOrAddSetStyle(StatType stat, float currentValue, float amount, float minClamp = float.NegativeInfinity, float maxClamp = float.PositiveInfinity)
    {
        float result;
        if (!_statsInitializedThisRun.Contains(stat))
        {
            // First write of this run: replace whatever default was there with the 
            // displayed value the card promises.
            result = amount;
            _statsInitializedThisRun.Add(stat);
        }
        else
        {
            // Subsequent write: amount is a delta. Add it on top of the current value.
            result = currentValue + amount;
        }
        return Mathf.Clamp(result, minClamp, maxClamp);
    }

    /// <summary>
    /// Same as SetOrAddSetStyle but for int-typed stats.
    /// </summary>
    int SetOrAddSetStyleInt(StatType stat, int currentValue, float amount, int minClamp = int.MinValue, int maxClamp = int.MaxValue)
    {
        int result;
        if (!_statsInitializedThisRun.Contains(stat))
        {
            result = Mathf.RoundToInt(amount);
            _statsInitializedThisRun.Add(stat);
        }
        else
        {
            result = currentValue + Mathf.RoundToInt(amount);
        }
        return Mathf.Clamp(result, minClamp, maxClamp);
    }

    /// <summary>
    /// Same model as SetOrAddSetStyle but where lower-is-better. Used by stats like 
    /// FireRate, MarksmanInterval, MedicInterval, etc., where the card data uses 
    /// negative AmountPerShopLevel to represent "shoots faster as you upgrade."
    /// 
    /// We don't actually do anything special here — addition with negative deltas 
    /// just works. This is purely a semantic alias for code clarity at call sites.
    /// </summary>
    float SetOrAddInterval(StatType stat, float currentValue, float amount, float minClamp = 0.05f)
    {
        return SetOrAddSetStyle(stat, currentValue, amount, minClamp);
    }

    /// <summary>
    /// Routes a single stat modifier to its target system.
    /// 
    /// 1.4.12 PATCH: SET-style stats now use SetOrAddSetStyle to correctly handle 
    /// first-pick-replaces / re-pick-adds semantics. The amount=0 early return is 
    /// preserved so Custom-stack-mode "no change on re-pick" still works.
    /// </summary>
    void ApplyStat(CardDefinition card, StatType stat, float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;

        WeaponPlayer weapon = GameObject.FindFirstObjectByType<WeaponPlayer>();
        PlayerController controller = PlayerController.Instance;
        PlayerHealth health = GameObject.FindFirstObjectByType<PlayerHealth>();
        PlayerStats ps = PlayerStats.Instance;

        switch (stat)
        {
            // ============================================================
            // MUNITIONS - mostly ADDITIVE (each pick stacks)
            // ============================================================
            
            // FireRate is additive: every pick subtracts `amount` from the player's 
            // current FireRate (card data uses positive values representing magnitude 
            // of reduction). The base default lives on the WeaponPlayer prefab.
            case StatType.FireRate:
                if (weapon != null) weapon.FireRate = Mathf.Max(0.05f, weapon.FireRate - amount);
                break;

            case StatType.Damage:
                if (weapon != null) weapon.CurrentStats.Damage += Mathf.RoundToInt(amount);
                break;

            case StatType.DamageMultiplier:
                if (weapon != null) weapon.CurrentStats.DamageMultiplier += amount;
                break;

            case StatType.CritChance:
                if (weapon != null) weapon.CurrentStats.CritChance += amount;
                break;

            case StatType.ProjectileSpeed:
                if (weapon != null) weapon.CurrentStats.Speed += amount;
                break;

            case StatType.PierceCount:
                if (weapon != null) weapon.CurrentStats.PierceCount += Mathf.RoundToInt(amount);
                break;

            case StatType.RicochetCount:
                if (weapon != null) weapon.CurrentStats.RicochetCount += Mathf.RoundToInt(amount);
                break;

            // --- Special feathers ---
            case StatType.BuckshotThreshold:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Buckshot, "threshold", amount);
                break;
            case StatType.BuckshotPelletCount:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Buckshot, "pellets", amount);
                break;

            case StatType.FrostyFeatherThreshold:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Frosty, "threshold", amount);
                break;
            case StatType.FrostyFreezeDuration:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Frosty, "freeze", amount);
                break;

            case StatType.HealingFeatherThreshold:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Healing, "threshold", amount);
                if (ps != null) ps.HasHealingFeather = true;
                break;
            case StatType.HealingFeatherAmount:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Healing, "heal", amount);
                break;

            case StatType.MetalFeatherThreshold:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Metal, "threshold", amount);
                break;
            case StatType.MetalFeatherKnockback:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Metal, "knockback", amount);
                break;

            case StatType.ExplosiveFeatherThreshold:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Explosive, "threshold", amount);
                break;
            case StatType.ExplosiveFeatherRadius:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Explosive, "radius", amount);
                break;

            case StatType.PoisonFeatherThreshold:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Poison, "threshold", amount);
                break;
            case StatType.PoisonFeatherDPS:
                ApplySpecialFeather(card, PlayerStats.FeatherType.Poison, "dps", amount);
                break;

            // --- Mini Gun ---
            // SET-STYLE: literal seconds. First pick replaces default; re-picks add delta.
            case StatType.MiniGunOverheatThreshold:
                if (ps != null)
                {
                    ps.HasMiniGun = true;
                    ps.MiniGunOverheatThreshold = SetOrAddSetStyle(
                        StatType.MiniGunOverheatThreshold,
                        ps.MiniGunOverheatThreshold,
                        amount,
                        minClamp: 0.5f
                    );
                }
                break;
            case StatType.MiniGunRecoveryRate:
                if (ps != null)
                {
                    ps.HasMiniGun = true;
                    ps.MiniGunRecoveryRate = SetOrAddSetStyle(
                        StatType.MiniGunRecoveryRate,
                        ps.MiniGunRecoveryRate,
                        amount,
                        minClamp: 0.5f
                    );
                }
                break;

            // ============================================================
            // GADGET
            // ============================================================
            case StatType.LuckPercent:
                // Additive: each pick adds to luck.
                if (ps != null) ps.LuckPercent += amount;
                break;

            case StatType.SlowingAuraSlow:
                // Additive: each pick increases the slow %.
                if (ps != null)
                {
                    ps.HasSlowingAura = true;
                    ps.SlowingAuraSlowPercent = Mathf.Clamp(ps.SlowingAuraSlowPercent + amount, 0f, 0.95f);
                }
                break;
            case StatType.SlowingAuraRadius:
                // Additive: radius grows.
                if (ps != null)
                {
                    ps.HasSlowingAura = true;
                    ps.SlowingAuraRadius += amount;
                }
                break;

            case StatType.FeatherSize:
                if (ps != null) ps.FeatherSize += amount;
                break;

            case StatType.AirburstFeatherCount:
                if (ps != null) ps.AirburstFeatherCount += Mathf.RoundToInt(amount);
                break;

            case StatType.EnemyHealthMissingPercent:
                if (ps != null)
                {
                    ps.EnemyHealthMissingPercent = Mathf.Clamp(ps.EnemyHealthMissingPercent + amount, 0f, 0.5f);
                }
                break;

            // Marksman targets is additive count.
            case StatType.MarksmanTargets:
                if (ps != null)
                {
                    ps.HasMarksman = true;
                    ps.MarksmanTargetCount += Mathf.RoundToInt(amount);
                }
                break;
            
            // SET-STYLE: literal interval seconds.
            case StatType.MarksmanFireRate:
                if (ps != null)
                {
                    ps.HasMarksman = true;
                    ps.MarksmanInterval = SetOrAddInterval(
                        StatType.MarksmanFireRate,
                        ps.MarksmanInterval,
                        amount,
                        minClamp: 0.3f
                    );
                }
                break;

            case StatType.MedicHealAmount:
                if (ps != null)
                {
                    ps.HasMedic = true;
                    ps.MedicHealAmount += Mathf.RoundToInt(amount);
                }
                break;
            
            // SET-STYLE: literal interval seconds.
            case StatType.MedicHealInterval:
                if (ps != null)
                {
                    ps.HasMedic = true;
                    ps.MedicInterval = SetOrAddInterval(
                        StatType.MedicHealInterval,
                        ps.MedicInterval,
                        amount,
                        minClamp: 0.5f
                    );
                }
                break;

            case StatType.ProtectorShockwaveSize:
                if (ps != null)
                {
                    ps.HasProtector = true;
                    ps.ProtectorRadius += amount;
                }
                break;
            
            // SET-STYLE.
            case StatType.ProtectorShockwaveInterval:
                if (ps != null)
                {
                    ps.HasProtector = true;
                    ps.ProtectorInterval = SetOrAddInterval(
                        StatType.ProtectorShockwaveInterval,
                        ps.ProtectorInterval,
                        amount,
                        minClamp: 0.5f
                    );
                }
                break;

            case StatType.ParallelProjectileCount:
                if (weapon != null) weapon.ParallelProjectiles += Mathf.RoundToInt(amount);
                break;

            case StatType.HomingSpeed:
                if (ps != null)
                {
                    ps.HomingProjectiles = true;
                    ps.HomingSpeedBonus += amount;
                }
                if (weapon != null) weapon.CurrentStats.HomingSpeed += amount;
                break;

            case StatType.AuraDamage:
                if (ps != null) ps.AuraDamage += amount;
                if (controller != null) controller.AuraDamage += amount;
                break;
            case StatType.AuraRadius:
                if (ps != null) ps.AuraRadius += amount;
                if (controller != null) controller.AuraRadius += amount;
                break;

            // SET-STYLE.
            case StatType.ElementalTurretFireRate:
                if (ps != null)
                {
                    ps.HasElementalTurret = true;
                    ps.ElementalTurretInterval = SetOrAddInterval(
                        StatType.ElementalTurretFireRate,
                        ps.ElementalTurretInterval,
                        amount,
                        minClamp: 0.5f
                    );
                }
                break;

            // ============================================================
            // MOBILITY
            // ============================================================
            case StatType.MoveSpeed:
                if (controller != null) controller.MaxRunSpeed += amount;
                break;
            case StatType.Acceleration:
                if (controller != null) controller.Acceleration += amount;
                break;
            case StatType.JumpForce:
                if (controller != null) controller.JumpForce += amount;
                break;
            case StatType.JumpCount:
                if (controller != null) controller.MaxJumps += Mathf.RoundToInt(amount);
                break;
            case StatType.DashCount:
                if (controller != null)
                {
                    controller.MaxDashes += Mathf.RoundToInt(amount);
                    if (ps != null) ps.OnDashSelected();
                }
                break;
            case StatType.DashDuration:
                if (controller != null) controller.DashDuration += amount;
                break;
            
            // SET-STYLE: literal cooldown seconds.
            case StatType.DashCooldown:
                if (controller != null)
                {
                    controller.DashCooldown = SetOrAddInterval(
                        StatType.DashCooldown,
                        controller.DashCooldown,
                        amount,
                        minClamp: 0.1f
                    );
                }
                break;

            // SET-STYLE.
            case StatType.BlinkInterval:
                if (ps != null)
                {
                    ps.HasBlink = true;
                    ps.BlinkInterval = SetOrAddInterval(
                        StatType.BlinkInterval,
                        ps.BlinkInterval,
                        amount,
                        minClamp: 0.5f
                    );
                }
                break;
            
            // BlinkDuration is additive (longer invuln window with more upgrades).
            case StatType.BlinkDuration:
                if (ps != null)
                {
                    ps.HasBlink = true;
                    ps.BlinkDuration = Mathf.Max(0.05f, ps.BlinkDuration + amount);
                }
                break;

            // SET-STYLE for gravity multiplier - lower = floatier.
            // Card values represent the target multiplier on first pick, delta on re-pick.
            case StatType.PlayerGravity:
                if (ps != null)
                {
                    ps.PlayerGravityMultiplier = SetOrAddSetStyle(
                        StatType.PlayerGravity,
                        ps.PlayerGravityMultiplier,
                        amount,
                        minClamp: 0.1f,
                        maxClamp: 2f
                    );
                }
                break;

            case StatType.MaxSpeedDamage:
                if (ps != null) ps.MaxSpeedDamage += Mathf.RoundToInt(amount);
                break;

            case StatType.ShockwaveSize:
                if (controller != null)
                {
                    controller.ShockwaveRadius += amount;
                    if (ps != null) ps.OnShockwaveSelected();
                }
                break;
            case StatType.ShockwaveDamage:
                if (controller != null)
                {
                    controller.ShockwaveDamage += amount;
                    if (ps != null) ps.OnShockwaveSelected();
                }
                break;

            case StatType.FireTrailDamage:
                if (ps != null)
                {
                    ps.HasFireTrail = true;
                    ps.FireTrailDamage += Mathf.RoundToInt(amount);
                }
                break;
            case StatType.FireTrailDuration:
                if (ps != null)
                {
                    ps.HasFireTrail = true;
                    ps.FireTrailDuration += amount;
                }
                break;

            // ============================================================
            // SURVIVAL
            // ============================================================
            case StatType.XPMultiplier:
                if (ps != null) ps.XPMultiplier += amount;
                if (LevelManager.Instance != null) LevelManager.Instance.XPMultiplier = ps != null ? ps.XPMultiplier : 1f;
                break;
            case StatType.CoinsPerWave:
                if (LevelManager.Instance != null) LevelManager.Instance.CoinsPerWave += Mathf.RoundToInt(amount);
                break;
            case StatType.CoinDropMultiplier:
                if (ps != null) ps.CoinDropMultiplier += amount;
                break;
            case StatType.ThornsDamage:
                if (health != null) health.ThornsDamage += Mathf.RoundToInt(amount);
                break;
            case StatType.RegenPerWave:
                if (health != null) health.RegenPerWave += Mathf.RoundToInt(amount);
                break;
            case StatType.MaxHealth:
                if (health != null)
                {
                    int add = Mathf.RoundToInt(amount);
                    health.MaxHealth += add;
                    health.Heal(add);
                }
                break;
            case StatType.PickupRadius:
                if (ps != null) ps.MagnetRange += amount;
                break;
            case StatType.CoinsPerSecond:
                if (ps != null) ps.CoinsPerSecond += amount;
                break;

            case StatType.DamagePerCoin:
                if (ps != null) ps.DamagePerCoin += amount;
                break;
            case StatType.MoneyHighDuration:
                if (ps != null) ps.MoneyHighDuration += amount;
                break;

            // SET-STYLE: literal coin count. First pick replaces default; re-picks add delta.
            case StatType.MeteorThreshold:
                if (ps != null)
                {
                    ps.HasCoinMeteors = true;
                    ps.MeteorThreshold = SetOrAddSetStyleInt(
                        StatType.MeteorThreshold,
                        ps.MeteorThreshold,
                        amount,
                        minClamp: 1
                    );
                }
                if (controller != null) controller.HasCoinMeteors = true;
                break;
            case StatType.MeteorDamage:
                if (ps != null) ps.MeteorDamage += Mathf.RoundToInt(amount);
                if (controller != null) controller.MeteorDamage += Mathf.RoundToInt(amount);
                break;
            case StatType.MeteorRadius:
                if (ps != null) ps.MeteorRadius += amount;
                if (controller != null) controller.MeteorRadius += amount;
                break;

            // SET-STYLE: literal coin count.
            case StatType.TripleshotThreshold:
                if (ps != null)
                {
                    ps.HasTripleshot = true;
                    ps.TripleshotThreshold = SetOrAddSetStyleInt(
                        StatType.TripleshotThreshold,
                        ps.TripleshotThreshold,
                        amount,
                        minClamp: 1
                    );
                }
                if (controller != null) controller.HasCoinShot = true;
                break;
            case StatType.TripleshotDuration:
                if (ps != null) ps.TripleshotDuration += amount;
                break;

            // SET-STYLE: literal seconds.
            case StatType.SecondWindCooldown:
                if (ps != null)
                {
                    ps.HasSecondWind = true;
                    ps.SecondWindCooldown = SetOrAddSetStyle(
                        StatType.SecondWindCooldown,
                        ps.SecondWindCooldown,
                        amount,
                        minClamp: 5f
                    );
                    ps.LastSecondWindUseTime = -999f;
                }
                break;
            case StatType.SecondWindHealthRecovery:
                if (ps != null)
                {
                    ps.HasSecondWind = true;
                    // SET-STYLE: first pick writes recovery %; re-picks tune it.
                    ps.SecondWindHealthRecovery = SetOrAddSetStyle(
                        StatType.SecondWindHealthRecovery,
                        ps.SecondWindHealthRecovery,
                        amount,
                        minClamp: 0f,
                        maxClamp: 1f
                    );
                }
                break;
            case StatType.SecondWindInvulnDuration:
                if (ps != null)
                {
                    ps.HasSecondWind = true;
                    // SET-STYLE: first pick writes the duration; re-picks add.
                    ps.SecondWindInvulnDuration = SetOrAddSetStyle(
                        StatType.SecondWindInvulnDuration,
                        ps.SecondWindInvulnDuration,
                        amount,
                        minClamp: 0.5f
                    );
                }
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Applies a stat to a special feather instance, creating it if it doesn't exist yet.
    /// Each card creates its own SpecialFeatherInstance.
    /// 
    /// 1.4.12: now uses first-pick-replaces / re-pick-adds semantics for threshold and 
    /// similar SET-style fields. Tracked per-card-per-field so two different cards that 
    /// both modify a Frosty threshold each get their own "first pick" treatment on their 
    /// own special-feather instance.
    /// </summary>
    void ApplySpecialFeather(CardDefinition card, PlayerStats.FeatherType type, string field, float amount)
    {
        if (PlayerStats.Instance == null) return;
        if (Mathf.Approximately(amount, 0f)) return;

        var instance = PlayerStats.Instance.GetSpecialFeatherByCardID(card.ID);
        if (instance == null)
        {
            // First time this card has been picked - create a fresh instance.
            // Initial values are placeholders; the actual values come from the 
            // card's stat modifiers being applied below.
            instance = new PlayerStats.SpecialFeatherInstance(card.ID, type, 10);
            instance.HealAmount = 1;
            instance.FreezeDuration = 1f;
            instance.PoisonDPS = 1f;
            instance.BonusKnockback = 2f;
            instance.ExplosionRadius = 1.5f;
            instance.BuckshotPellets = 5;
            PlayerStats.Instance.SpecialFeathers.Add(instance);
        }

        // Per-card-per-field initialization key. A given card's frosty threshold is 
        // independent from another card's frosty threshold.
        string initKey = card.ID + ":" + field;
        bool isFirstWrite = !_featherFieldsInitializedThisRun.Contains(initKey);
        if (isFirstWrite) _featherFieldsInitializedThisRun.Add(initKey);

        switch (field)
        {
            case "threshold":
                // SET-STYLE: first pick replaces the placeholder threshold; re-picks 
                // ADD the delta (typically a negative number to fire more often).
                if (isFirstWrite)
                {
                    instance.Threshold = Mathf.Max(1, Mathf.RoundToInt(amount));
                }
                else
                {
                    instance.Threshold = Mathf.Max(1, instance.Threshold + Mathf.RoundToInt(amount));
                }
                break;
            
            case "heal":
                // Additive count.
                instance.HealAmount = Mathf.Max(1, instance.HealAmount + Mathf.RoundToInt(amount));
                break;
            
            case "freeze":
                // SET-STYLE: literal seconds of freeze.
                if (isFirstWrite)
                {
                    instance.FreezeDuration = amount;
                }
                else
                {
                    instance.FreezeDuration += amount;
                }
                break;
            
            case "dps":
                // SET-STYLE: literal damage-per-second.
                if (isFirstWrite)
                {
                    instance.PoisonDPS = amount;
                }
                else
                {
                    instance.PoisonDPS += amount;
                }
                break;
            
            case "knockback":
                // SET-STYLE.
                if (isFirstWrite)
                {
                    instance.BonusKnockback = amount;
                }
                else
                {
                    instance.BonusKnockback += amount;
                }
                break;
            
            case "radius":
                // SET-STYLE.
                if (isFirstWrite)
                {
                    instance.ExplosionRadius = amount;
                }
                else
                {
                    instance.ExplosionRadius += amount;
                }
                break;
            
            case "pellets":
                // Additive count.
                instance.BuckshotPellets = Mathf.Max(1, instance.BuckshotPellets + Mathf.RoundToInt(amount));
                break;
        }
    }
}