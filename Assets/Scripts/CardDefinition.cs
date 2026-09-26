using UnityEngine;
using System.Collections.Generic;

// === 1.4.11 PACK RENAME ===
// CardPackType.Tech renamed to CardPackType.Gadget per the redistribution.
// Existing saved cards keyed by string ID still work; only the enum value changed.
public enum CardPackType { Munitions, Mobility, Survival, Gadget, BaseSet }

public enum StatType
{
    // ============================================================
    // MUNITIONS PACK
    // ============================================================

    // --- Faster Firing / Damage / Sharp Eye / Accelerator ---
    FireRate,
    Damage,
    DamageMultiplier,
    CritChance,
    ProjectileSpeed,

    // --- Buckshot ---
    BuckshotThreshold,
    BuckshotPelletCount,

    // --- Frosty Feathers ---
    FrostyFeatherThreshold,
    FrostyFreezeDuration,

    // --- Healing Feathers ---
    HealingFeatherThreshold,
    HealingFeatherAmount,

    // --- Metal Feathers ---
    MetalFeatherThreshold,
    MetalFeatherKnockback,

    // --- Pierce / Ricochet ---
    PierceCount,
    RicochetCount,

    // --- Exploding Feathers ---
    ExplosiveFeatherThreshold,
    ExplosiveFeatherRadius,

    // --- Poison Feathers (now Munitions) ---
    PoisonFeatherThreshold,
    PoisonFeatherDPS,

    // --- 1.4.11 NEW: Mini Gun ---
    MiniGunOverheatThreshold,   // Seconds before forced cooldown
    MiniGunRecoveryRate,        // Seconds to fully recover from overheat

    // ============================================================
    // GADGET PACK
    // ============================================================

    // --- 1.4.11 NEW: Lucky Talisman ---
    LuckPercent,                // % bonus to better-rarity rolls on level up

    // --- Slowing Aura ---
    SlowingAuraSlow,            // % slow applied
    SlowingAuraRadius,

    // --- 1.4.11 NEW: Big Feathers ---
    FeatherSize,                // Visual + hitbox multiplier

    // --- 1.4.11 NEW: Airburst Feathers ---
    AirburstFeatherCount,       // # of sub-projectiles spawned on regular-feather hit

    // --- 1.4.11 NEW: Sabotage ---
    EnemyHealthMissingPercent,  // % of HP enemies spawn with already missing (capped 50%)

    // --- Marksman (renamed Flinger turret) ---
    MarksmanTargets,            // Was FlingerTargetCount
    MarksmanFireRate,           // Was FlingerInterval

    // --- Medic ---
    MedicHealAmount,
    MedicHealInterval,

    // --- Protector ---
    ProtectorShockwaveSize,
    ProtectorShockwaveInterval,

    // --- Feather Duplicator (parallel projectiles) ---
    ParallelProjectileCount,

    // --- Heat Seeking ---
    HomingSpeed,

    // --- Aura (the damaging one) ---
    AuraDamage,
    AuraRadius,

    // --- 1.4.11 NEW: Elemental Turret ---
    ElementalTurretFireRate,    // Random feather variant on tick

    // ============================================================
    // MOBILITY PACK
    // ============================================================

    // --- Swiftness / Leg Exercises ---
    MoveSpeed,
    Acceleration,
    JumpForce,

    // --- 1.4.11 NEW: Blink ---
    BlinkInterval,              // Seconds between blinks while moving
    BlinkDuration,              // Seconds each blink lasts

    // --- 1.4.11 NEW: Low Gravity ---
    PlayerGravity,              // Multiplier on default gravity (lower = floatier)

    // --- Double Jump / Dash ---
    JumpCount,
    DashCount,
    DashDuration,
    DashCooldown,

    // --- 1.4.11 NEW: Hypersonic ---
    MaxSpeedDamage,             // Damage on collision when at >=90% max speed

    // --- Shockwave ---
    ShockwaveSize,
    ShockwaveDamage,

    // --- 1.4.11 NEW: Fire Trail ---
    FireTrailDamage,
    FireTrailDuration,

    // ============================================================
    // SURVIVAL PACK
    // ============================================================

    // --- Exp Booster / Interest / Syphon ---
    XPMultiplier,
    CoinsPerWave,
    CoinDropMultiplier,

    // --- Thorns / Recovery / Health ---
    ThornsDamage,
    RegenPerWave,
    MaxHealth,

    // --- Coin Magnet ---
    PickupRadius,

    // --- Coins Coins Coins ---
    CoinsPerSecond,

    // --- Profit Power ---
    DamagePerCoin,              // Damage multiplier per coin collected
    MoneyHighDuration,          // How long the buff lasts per coin

    // --- Coin Meteors ---
    MeteorThreshold,
    MeteorDamage,
    MeteorRadius,

    // --- Triple or Nothing (tripleshot) ---
    TripleshotThreshold,
    TripleshotDuration,

    // --- 1.4.11 NEW: Second Wind ---
    SecondWindCooldown,
    SecondWindHealthRecovery,   // % of MaxHealth restored on trigger
    SecondWindInvulnDuration,   // Seconds of invulnerability after trigger

    // ============================================================
    // INTERNAL / LEGACY (kept for backwards compat with old code paths)
    // ============================================================
    // Some old systems still reference these by name. Their cards no longer 
    // exist but the StatType entries remain so old data doesn't fail to deserialize.
    Knockback,
    SpreadProjectiles,
    Deceleration,
    GravityScale,
    ProximityScaling,
    PassiveIncomeRate,
    InterestRate,
    CoinShotThreshold,
    CoinShotDuration,

    // Append new values to preserve existing serialized StatType indices.
    ElectricFeatherThreshold,
    ElectricFeatherChainCount,
    BuckshotDamageFraction, MetalDamageFraction, RicochetDamageLoss,
    DuplicatorDamageReduction, ElementalTargetCount, NonFeatherDamage,
    DashDistance
}

public enum CardAscension
{
    None, QuantumLeap, AbsoluteZero, Vampire, Tungsten, Supercharged, DeadlyToxin,
    Volcano, DeathRay, Wormhole, LearnToFly, Untouchable, ObsidianTrail, Earthquake,
    RecoveryPlus, Pincushion, AbsoluteExtinction, IllegalOperations, Rebirth,
    DoubleDown, Marksman, Savior, Defender, CursorAura, Elemental, DivineDuplicator
}

public enum CardRarity { Common, Rare, Legendary, Corrupted }

/// <summary>
/// Defines how a card's effect stacks when picked up multiple times in a single run.
/// 
/// MatchBaseValue: every pickup adds the FULL displayed value. Simplest model. 
///                 Example: card shows "+6/sec" at shop level 5 - each pickup gives +6.
/// 
/// MatchShopGrowth: first pickup gives full displayed value, re-picks give only 
///                  AmountPerShopLevel (the shop upgrade growth rate). 
///                  Example: 1st pickup +6, 2nd pickup +1, 3rd pickup +1.
/// 
/// Custom: first pickup gives full displayed value, re-picks give InRunStackAmount. 
///         Set InRunStackAmount to 0 for "no effect on re-pick" (unlocks, configs).
/// </summary>
public enum InRunStackMode
{
    MatchBaseValue,
    MatchShopGrowth,
    Custom
}

[System.Serializable]
public struct CardStatModifier
{
    [Tooltip("Which stat this modifier affects.")]
    public StatType StatType;

    [Tooltip("Value applied at shop level 1.")]
    public float BaseAmount;

    [Tooltip("How much BaseAmount grows per shop upgrade level. " +
             "At shop level N, the displayed value is BaseAmount + (N-1) * AmountPerShopLevel.")]
    public float AmountPerShopLevel;

    [Tooltip("How re-picking the same card in a single run stacks. " +
             "MatchBaseValue: every pickup grants the full shown value. " +
             "MatchShopGrowth: re-picks grant only AmountPerShopLevel. " +
             "Custom: re-picks grant InRunStackAmount.")]
    public InRunStackMode StackMode;

    [Tooltip("Only used when StackMode is Custom. Amount added per in-run re-pick. " +
             "0 = re-picking has no additional effect.")]
    public float InRunStackAmount;

    [Tooltip("Description formatting only: use 100 for fractional percentages.")]
    public float DisplayMultiplier;
}

[CreateAssetMenu(fileName = "NewCard", menuName = "DuckDefender/Card Definition")]
public class CardDefinition : ScriptableObject
{
    [Header("Identity")]
    public string ID;
    public string CardName;
    [TextArea] public string Description;
    public Sprite Icon;
    public CardRarity Rarity;
    public CardPackType PackCategory;

    [Header("Card Effects")]
    public List<CardStatModifier> Modifiers;

    [Header("Leveling Config")]
    [Tooltip("Legacy serialized field; upgrade prices now come from the rarity tables below.")]
    public int BaseUpgradeCost = 50;
    public int MaxLevel = 6;

    [Header("Ascension")]
    public CardAscension Ascension;
    public string AscendedName;
    [TextArea] public string AscendedDescription;
    public Sprite AscendedIcon;
    public int AscensionCost = 1000;
    [Tooltip("Apply level-6 modifiers before the ascended effect. Only for supplementary ascensions.")]
    public bool AscensionRetainsBase;
    public bool IsBasic => PackCategory == CardPackType.BaseSet;

    [Header("Meta")]
    public int BaseCost = 100;

    /// <summary>
    /// Returns the value displayed in the card index at a given shop level.
    /// = BaseAmount + (shopLevel - 1) * AmountPerShopLevel
    /// This is what the player sees on the card before clicking it.
    /// </summary>
    public float GetAmountAtShopLevel(CardStatModifier mod, int shopLevel)
    {
        shopLevel = Mathf.Clamp(shopLevel, 1, MaxLevel);
        if (shopLevel <= 1) return mod.BaseAmount;
        return mod.BaseAmount + ((shopLevel - 1) * mod.AmountPerShopLevel);
    }

    /// <summary>
    /// Returns the value applied for a specific in-run pickup of this modifier.
    /// 
    /// MatchBaseValue: every pickup returns the full shop-level value.
    /// MatchShopGrowth: first pickup returns full value, re-picks return AmountPerShopLevel.
    /// Custom: first pickup returns full value, re-picks return InRunStackAmount.
    /// </summary>
    public float GetAmountForPickup(CardStatModifier mod, int shopLevel, int pickupIndex)
    {
        // MatchBaseValue: simplest case - every pickup is just the full displayed value
        if (mod.StackMode == InRunStackMode.MatchBaseValue)
        {
            return GetAmountAtShopLevel(mod, shopLevel);
        }

        // For the other two modes: first pickup gives full value, re-picks give a smaller delta
        if (pickupIndex <= 0)
        {
            return GetAmountAtShopLevel(mod, shopLevel);
        }

        if (mod.StackMode == InRunStackMode.MatchShopGrowth)
        {
            return mod.AmountPerShopLevel;
        }
        else // Custom
        {
            return mod.InRunStackAmount;
        }
    }

    // === LEGACY COMPATIBILITY ===
    // Old code paths called this with a single "level" int. We map it onto the new system.
    // Treats `level` as shop level. Used by GetDescriptionAtLevel.
    public float GetAmountAtLevel(CardStatModifier mod, int level)
    {
        return GetAmountAtShopLevel(mod, level);
    }

    public int GetUpgradeCost(int currentLevel)
    {
        if (currentLevel < 1 || currentLevel >= MaxLevel) return 0;
        int index = Mathf.Clamp(currentLevel - 1, 0, 4);
        return Rarity == CardRarity.Legendary ? LegendaryCosts[index] : Rarity == CardRarity.Rare ? RareCosts[index] : CommonCosts[index];
    }

    public int GetCardsRequired(int currentLevel)
    {
        if (currentLevel < 1 || currentLevel >= MaxLevel) return 0;
        int index = Mathf.Clamp(currentLevel - 1, 0, 4);
        return Rarity == CardRarity.Legendary ? LegendaryCopies[index] : Rarity == CardRarity.Rare ? RareCopies[index] : CommonCopies[index];
    }

    static readonly int[] CommonCosts = { 100, 200, 350, 500, 750 };
    static readonly int[] RareCosts = { 200, 350, 500, 750, 1250 };
    static readonly int[] LegendaryCosts = { 350, 500, 750, 1250, 2000 };
    static readonly int[] CommonCopies = { 2, 4, 8, 16, 32 };
    static readonly int[] RareCopies = { 2, 4, 6, 10, 16 };
    static readonly int[] LegendaryCopies = { 1, 2, 4, 6, 8 };
    public int EssencePerCopy => Rarity == CardRarity.Legendary ? 4 : Rarity == CardRarity.Rare ? 2 : 1;

    public string GetDescriptionAtLevel(int level)
    {
        if (Modifiers == null || Modifiers.Count == 0) return Description;

        object[] args = new object[Modifiers.Count];
        for (int i = 0; i < Modifiers.Count; i++)
        {
            float scale = Modifiers[i].DisplayMultiplier == 0 ? 1 : Modifiers[i].DisplayMultiplier;
            args[i] = System.Math.Round(GetAmountAtShopLevel(Modifiers[i], level) * scale, 3);
        }

        try
        {
            return string.Format(Description, args);
        }
        catch (System.Exception)
        {
            return Description;
        }
    }
}
