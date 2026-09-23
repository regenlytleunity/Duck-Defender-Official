using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// Explicit authoring action: never runs automatically on import or on launch.
public static class CardReworkSetup
{
    static Dictionary<string, CardDefinition> _cards;
    static CardStatModifier M(StatType stat, float first, float growth = 0, bool percent = false)
        => new CardStatModifier { StatType = stat, BaseAmount = first, AmountPerShopLevel = growth,
            StackMode = InRunStackMode.MatchBaseValue, DisplayMultiplier = percent ? 100 : 1 };

    static void Set(string id, string name, string description, params CardStatModifier[] mods)
    {
        if (!_cards.TryGetValue(id, out var card)) throw new System.InvalidOperationException("Missing card: " + id);
        Undo.RecordObject(card, "Apply card rework");
        card.CardName = name;
        card.Description = description;
        card.Modifiers = mods.ToList();
        card.MaxLevel = 6;
        card.AscensionCost = 1000;
        card.Ascension = CardAscension.None;
        card.AscendedName = "";
        card.AscendedDescription = "";
        card.AscensionRetainsBase = false;
        EditorUtility.SetDirty(card);
    }

    static void Asc(string id, CardAscension kind, string name, string description, bool retain = false)
    {
        var card = _cards[id];
        card.Ascension = kind; card.AscendedName = name; card.AscendedDescription = description;
        card.AscensionRetainsBase = retain;
    }

    [MenuItem("Duck Defender/Card Rework/Apply All Card Definitions")]
    public static void Apply()
    {
        _cards = AssetDatabase.FindAssets("t:CardDefinition", new[] { "Assets/Cards/Upgrades" })
            .Select(g => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .ToDictionary(c => c.ID);
        Set("mun_accelerator", "Accelerator", "Increases feather projectile speed by {0}.", M(StatType.ProjectileSpeed, 4, 2));
        Set("mun_faster_firing", "Faster Firing", "Reduces attack cooldown by {0}%.", M(StatType.FireRate, .15f, .15f, true));
        Set("mun_frosty_feathers", "Frosty Feathers", "Every {0} attacks, fires a frosty feather that freezes enemies for {1} seconds.", M(StatType.FrostyFeatherThreshold, 7, -1), M(StatType.FrostyFreezeDuration, 1, .25f));
        Set("mun_razor_sharp", "Razor Sharp Feathers", "Increases feather damage by {0} and all damage by {1}%.", M(StatType.Damage, 1, 1), M(StatType.DamageMultiplier, .05f, .05f, true));
        Set("mun_sharp_eye", "Sharp Eye", "Feathers have a {0}% chance to critically hit for double damage.", M(StatType.CritChance, .25f, .1f, true));
        Set("mun_buckshot", "Buckshot", "Every {0} attacks, fires 4 short-range feathers dealing {1}% feather damage each.", M(StatType.BuckshotThreshold, 8, -1), M(StatType.BuckshotDamageFraction, .4f, .1f, true), M(StatType.BuckshotPelletCount, 4));
        Set("mun_healing_feathers", "Healing Feathers", "Every {0} attacks, fires a feather dealing 50% feather damage and healing 1 health on hit.", M(StatType.HealingFeatherThreshold, 10, -1), M(StatType.HealingFeatherAmount, 1));
        Set("mun_metal_feathers", "Metal Feathers", "Every {0} attacks, fires a heavy feather dealing {1}% feather damage and knocking enemies back.", M(StatType.MetalFeatherThreshold, 7, -1), M(StatType.MetalDamageFraction, 1.5f, .5f, true), M(StatType.MetalFeatherKnockback, 2));
        Set("mun_electric_feathers", "Electric Feathers", "Every {0} attacks, fires an electric feather chaining to {1} additional enemies. Damage halves each jump.", M(StatType.ElectricFeatherThreshold, 10, -1), M(StatType.ElectricFeatherChainCount, 1, 1));
        Set("mun_pierce", "Pierce", "Feathers pierce {0} additional enemies.", M(StatType.PierceCount, 1, 1));
        Set("mun_poison_feathers", "Poison Feathers", "Every {0} attacks, fires a feather poisoning enemies for {1} damage per second for 3 seconds.", M(StatType.PoisonFeatherThreshold, 7, -1), M(StatType.PoisonFeatherDPS, 1, .5f));
        Set("mun_explosive_feathers", "Explosive Feathers", "Every {0} attacks, fires a feather that explodes in a radius of {1}.", M(StatType.ExplosiveFeatherThreshold, 10, -1), M(StatType.ExplosiveFeatherRadius, 1, 1));
        Set("mun_minigun", "Minigun", "Adds a minigun starting at double fire rate and slowing as it heats over {0} seconds. Full recovery takes {1} seconds.", M(StatType.MiniGunOverheatThreshold, 3, 1), M(StatType.MiniGunRecoveryRate, 5, -.5f));
        Set("mun_ricochet", "Ricochet", "Feathers bounce {0} times, losing {1}% damage and gaining 50% speed each bounce.", M(StatType.RicochetCount, 1, 1), M(StatType.RicochetDamageLoss, .5f, -.03f, true));

        Set("mob_blink", "Blink", "While moving, blink every {0} seconds and become invulnerable for {1} seconds.", M(StatType.BlinkInterval, 4, -.5f), M(StatType.BlinkDuration, .2f, .2f));
        Set("mob_strong_legs", "Strong Legs", "Increases jump force by {0}.", M(StatType.JumpForce, 2, 2));
        Set("mob_low_gravity", "Low Gravity", "Sets gravity to {0}% of normal.", M(StatType.PlayerGravity, .9f, -.1f, true));
        Set("mob_swiftness", "Swiftness", "Increases movement speed by {0} and acceleration by {1}.", M(StatType.MoveSpeed, 2, 2), M(StatType.Acceleration, 1, 1));
        Set("mob_dash", "Dash", "Dash in your movement direction for up to {0} seconds. Cooldown: {1} seconds.", M(StatType.DashDuration, 1, .5f), M(StatType.DashCooldown, 7, -1), M(StatType.DashCount, 1));
        Set("mob_double_jump", "Double Jump", "Grants {0} additional jumps.", M(StatType.JumpCount, 1, 1));
        Set("mob_hypersonic", "Hypersonic", "Colliding with enemies at maximum speed deals {0}% feather damage.", M(StatType.MaxSpeedDamage, .5f, .1f, true));
        Set("mob_fire_trail", "Fire Trail", "Moving leaves fire dealing {0} damage per second for {1} seconds.", M(StatType.FireTrailDamage, 1, 1), M(StatType.FireTrailDuration, 1, .5f));
        Set("mob_shockwave", "Shockwave", "Landing after a jump releases a shockwave of radius {0}, dealing {1} damage.", M(StatType.ShockwaveSize, 3, 1), M(StatType.ShockwaveDamage, 5, 2));

        Set("sur_exp_booster", "EXP Booster", "Earn {0}% more EXP.", M(StatType.XPMultiplier, .2f, .2f, true));
        Set("sur_health", "Health", "Increases maximum health by {0}.", M(StatType.MaxHealth, 5, 2));
        Set("sur_interest", "Interest", "Earn {0} additional coins per wave.", M(StatType.CoinsPerWave, 20, 15));
        Set("sur_recovery", "Recovery", "Heal {0} health after each wave.", M(StatType.RegenPerWave, 1, 1));
        Set("sur_syphon", "Syphon", "Enemies drop {0}% more coins.", M(StatType.CoinDropMultiplier, .5f, .2f, true));
        Set("sur_thorns", "Thorns", "Colliding with an enemy deals {0} damage.", M(StatType.ThornsDamage, 1, 1));
        Set("sur_coins_per_second", "COINS!", "Earn {0} additional coins per second.", M(StatType.CoinsPerSecond, 2, .75f));
        Set("sur_coin_magnet", "Coin Magnet", "Increases coin pickup radius by {0}.", M(StatType.PickupRadius, 5, 2));
        Set("sur_coin_meteor", "Coin Meteor", "Every {0} coins collected, a meteor targets the nearest enemy, deals {1} damage and drops 3 coins.", M(StatType.MeteorThreshold, 50, -3), M(StatType.MeteorDamage, 10, 5));
        Set("sur_powerful_profit", "Powerful Profit", "Each collected coin adds {0}% damage for {1} seconds. Each bonus expires independently.", M(StatType.DamagePerCoin, .001f, .001f, true), M(StatType.MoneyHighDuration, 3, .1f));
        Set("sur_second_wind", "Second Wind", "Cheat death, restoring {0}% max health with 5 seconds of invulnerability. Cooldown: {1} seconds.", M(StatType.SecondWindHealthRecovery, .1f, .1f, true), M(StatType.SecondWindCooldown, 120, -5), M(StatType.SecondWindInvulnDuration, 5));
        Set("sur_triple_or_nothing", "Triple or Nothing", "Every {0} coins collected grants triple shot for {1} seconds.", M(StatType.TripleshotThreshold, 30, -2), M(StatType.TripleshotDuration, 2, .1f));

        Set("gad_airburst", "Airburst", "Feather hits release {0} smaller feathers for 33% feather damage; they cannot hit the original enemy.", M(StatType.AirburstFeatherCount, 1, 1));
        Set("gad_big_feathers", "BIG Feathers", "Feathers become {0}% larger and are affected by gravity.", M(StatType.FeatherSize, .5f, .1f, true));
        Set("gad_lucky_talisman", "Lucky Talisman", "Increases the relative chance of Rare and Legendary run offers by {0}%.", M(StatType.LuckPercent, .5f, .1f, true));
        Set("gad_sabotage", "Sabotage", "Enemies spawn missing {0}% of their health.", M(StatType.EnemyHealthMissingPercent, .1f, .05f, true));
        Set("gad_slow_aura", "Slowing Aura", "Slows enemies and projectiles within radius {0} by {1}%.", M(StatType.SlowingAuraRadius, 5, 1), M(StatType.SlowingAuraSlow, .3f, .1f, true));
        Set("gad_marksman_turret", "Hunter", "A turret fires feathers at {0} enemies every {1} seconds.", M(StatType.MarksmanTargets, 1, 1), M(StatType.MarksmanFireRate, 10, -1));
        Set("gad_medic_turret", "Medic", "A turret heals 1 health every {0} seconds.", M(StatType.MedicHealInterval, 10, -1), M(StatType.MedicHealAmount, 1));
        Set("gad_protector_turret", "Protector", "Every {0} seconds, a turret knocks enemies away in radius {1}.", M(StatType.ProtectorShockwaveInterval, 10, -1), M(StatType.ProtectorShockwaveSize, 5, 1));
        Set("gad_damage_aura", "Damaging Aura", "An aura of radius {0} deals {1} damage per second.", M(StatType.AuraRadius, 3, 1), M(StatType.AuraDamage, 1, .5f));
        Set("gad_elemental_turret", "Wizard", "A turret fires random elemental feathers at {0} enemies every {1} seconds.", M(StatType.ElementalTargetCount, 1, 1), M(StatType.ElementalTurretFireRate, 12, -1));
        Set("gad_feather_duplicator", "Feather Duplicator", "Every attack fires an extra feather dealing {0}% less damage.", M(StatType.DuplicatorDamageReduction, .5f, -.05f, true), M(StatType.ParallelProjectileCount, 1));
        Set("gad_homing_feathers", "Homing Feathers", "Feathers home toward enemies with force {0}.", M(StatType.HomingSpeed, .5f, .5f));

        Set("bas_damage", "Damage Upgrade", "Increases feather damage by {0}.", M(StatType.Damage, 1));
        Set("bas_health", "Health Upgrade", "Increases maximum health by {0}.", M(StatType.MaxHealth, 1));
        Set("bas_income", "Coins Upgrade", "Earn {0} additional coins per wave.", M(StatType.CoinsPerWave, 1));
        Set("bas_mobility", "Mobility Upgrade", "Increases movement speed and jump force by {0}.", M(StatType.MoveSpeed, 1), M(StatType.JumpForce, 1));

        Asc("mun_accelerator", CardAscension.QuantumLeap, "Quantum Leap", "Feathers cross their trajectory instantly, preserving hit and penetration effects.");
        Asc("mun_frosty_feathers", CardAscension.AbsoluteZero, "Absolute Zero", "Every 2 attacks, freezes enemies for 2.25 seconds. They remain 75% slower after thawing.", true);
        Asc("mun_healing_feathers", CardAscension.Vampire, "Vampire", "Defeated enemies drop a healing orb restoring 10% of maximum health.");
        Asc("mun_metal_feathers", CardAscension.Tungsten, "100% Tungsten", "Every 2 attacks, fires a heavy cube for 400% feather damage. It bounces 3 times and then lingers for 1 second.", true);
        Asc("mun_electric_feathers", CardAscension.Supercharged, "Supercharged", "Every 5 attacks, fires a feather calling lightning for 100% of the struck enemy's maximum health.", true);
        Asc("mun_poison_feathers", CardAscension.DeadlyToxin, "Deadly Toxin", "Every 2 attacks, poisons enemies for 5 damage per second for 3 seconds. Each second, poison has a 10% chance to spread to the nearest enemy.", true);
        Asc("mun_explosive_feathers", CardAscension.Volcano, "Volcano", "Every 5 attacks, fires an explosive feather of radius 6, followed by a secondary eruption and 3 seconds of lingering fire.", true);
        Asc("mun_minigun", CardAscension.DeathRay, "Death-Ray", "Attacking fires a continuous beam dealing 10 damage per second to every enemy inside.");
        Asc("mob_blink", CardAscension.Wormhole, "Wormhole", "While moving, blink every 3 seconds with 1.2 seconds of invulnerability. Leave a wormhole pulling enemies in and dealing 5 damage per second for 3 seconds.", true);
        Asc("mob_low_gravity", CardAscension.LearnToFly, "Learn to Fly", "Hold jump to fly upward and down to descend. Fly for 7 seconds, then recover for 10 seconds.");
        Asc("mob_swiftness", CardAscension.Untouchable, "Untouchable", "Move at 500% of your otherwise-current maximum movement speed.");
        Asc("mob_fire_trail", CardAscension.ObsidianTrail, "Obsidian Trail", "Moving leaves fire and ice dealing 10 damage per second and slowing by 50%. Patches last 4 seconds.");
        Asc("mob_shockwave", CardAscension.Earthquake, "Earthquake", "Landing releases a radius-8 shockwave for 15 damage, followed by a second for 50% damage.", true);
        Asc("sur_recovery", CardAscension.RecoveryPlus, "Recovery +", "Heal 1 health every 5 seconds. Each completed wave adds 2% movement speed, damage and maximum health for this run.");
        Asc("sur_thorns", CardAscension.Pincushion, "Pincushion", "Taking damage releases infinitely piercing needles left and right for 5 damage each.");
        Asc("sur_coin_meteor", CardAscension.AbsoluteExtinction, "Absolute Extinction", "Every 35 coins, a meteor deals 35 damage and drops 3 coins. These coins each summon a random secondary meteor for 50% damage; secondary meteors drop no coins.", true);
        Asc("sur_powerful_profit", CardAscension.IllegalOperations, "Illegal Operations", "Each collected coin permanently adds 0.01% damage for the remainder of this run.");
        Asc("sur_second_wind", CardAscension.Rebirth, "Rebirth", "Once per run, lethal damage restores full health, defeats on-screen enemies, and grants +200% to beneficial numeric stats (cooldowns become three times faster).");
        Asc("sur_triple_or_nothing", CardAscension.DoubleDown, "Double Down", "Every 20 coins, attacks fire 6 feathers with random damage, speed, homing, piercing, angle and bounces for 2.5 seconds.", true);
        Asc("gad_marksman_turret", CardAscension.Marksman, "Marksman", "A turret fires at 6 enemies every 5 seconds, marking targets. Feather hits on marked enemies always critically hit.", true);
        Asc("gad_medic_turret", CardAscension.Savior, "Savior", "Every 7 seconds, places a healing aura lasting 5 seconds and restoring 2 health per second while inside.");
        Asc("gad_protector_turret", CardAscension.Defender, "Defender", "Every 30 seconds, places two walls with 5 health. Destroyed walls release a knockback shockwave.");
        Asc("gad_damage_aura", CardAscension.CursorAura, "Cursor Aura", "A radius-8 aura follows the cursor or touch aim, dealing 5 damage per second.");
        Asc("gad_elemental_turret", CardAscension.Elemental, "Elemental", "Every 10 seconds, fires random Absolute Zero, Tungsten, Supercharged, Volcano or Deadly Toxin feathers at 6 enemies.");
        Asc("gad_feather_duplicator", CardAscension.DivineDuplicator, "Divine Duplicator", "Each attack calls an additional feather from above for 50% feather damage.");
        AssetDatabase.SaveAssets();
        Debug.Log("[Card Rework] Updated 51 definitions. IDs, artwork, rarity and GUIDs preserved.");
    }
}
