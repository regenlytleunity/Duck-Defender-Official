using UnityEngine;

public enum ItemKind { None, Bomb, HealthPotion, Shield, BattleBanner, PocketCasino, CloneProjector, ExperiencePotion, Flamethrower, Sawblade, Shock, DivineIntervention, Nuke }
public enum PermanentUpgrade { Damage, Health, Speed, FireRate, Luck }

// Stable IDs are save data. Append new items; never reorder existing entries.
public static class ItemCatalog
{
    public const int Count = 12;
    public static readonly string[] Names = { "Empty", "Bomb", "Healing Potion", "Shield", "Battle Banner", "Pocket Casino", "Clone", "Experience Potion", "Flamethrower", "Sawblade", "Shock", "Divine Intervention", "Nuke" };
    public static readonly int[] Prices = { 0, 100, 100, 250, 250, 500, 500, 100, 500, 250, 250, 1000, 1000 };
    public static readonly string[] Descriptions = {
        "", "Lob a bomb that explodes on contact with ground or enemies and instantly kills all enemies in its radius.",
        "Instantly heal to maximum health.",
        "Create an unbreakable shield for 2 waves. Enemies and their shots cannot pass through; enemies inside are pushed out.",
        "Place a banner that gives +15% damage and fire rate to all players inside for 10 waves.",
        "Gain a random positive effect for 10 waves or a negative effect for 2 waves.",
        "Create one AI clone with your current stats for 5 waves.", "Instantly gain a level.",
        "Ignite the world for 3 waves, dealing 10% of enemies' maximum health per second.",
        "Summon a sawblade orbiting the cursor, dealing 1 damage on contact once per enemy per orbit. Up to 4 blades; lasts until death.",
        "Stun all enemies for 10 seconds and damage them for 50% of their maximum health.",
        "For 2 waves, suspend all card effects. Gain flight, 90% less shot delay, lightning dealing 50% maximum health and chaining to 5 more enemies with 10% less damage per chain, and 10 health regeneration per second.",
        "Drop a nuke that instantly kills all enemies and leaves radiation slowing enemies by 50% for 10 waves."
    };
    public static readonly int[] Available = { 1, 2, 4, 7 };
    public const int SlotCount = 2;
    public static readonly int[] ShardCosts = { 0, 10, 15, 20, 30, 40, 50, 65, 80, 100 };
    public static readonly int[] GoldCosts = { 0, 100, 500, 1000, 2000, 3500, 5000, 7000, 9000, 12000 };
    public static bool Valid(int id) => id == 1 || id == 2 || id == 4 || id == 7;
    public static int Charges(int id, int level) => level >= 3 && id != 4 ? (id == 1 && level >= 9 ? 3 : 2) : 1;
    public static float Cooldown(int id, int level) => (id == 4 ? 120 : id == 2 ? 80 : 60) - (level >= 4 ? 10 : 0) - (level >= 8 ? 10 : 0);
    public static readonly string[][] Upgrades = {
        new[] { "Throw an arcing bomb. Its small explosion instantly defeats enemies.", "Explosion radius +1.", "2 charges.", "Cooldown -10 seconds.", "Each explosion releases two mini bombs.", "Explosion radius +1.", "Primary explosions leave radiation for 3 seconds, dealing 5% maximum health each second.", "Cooldown -10 seconds.", "3 charges.", "Primary bombs bounce once and explode at both impacts." },
        new[] { "Heal 2 health.", "Heal 3 health.", "2 charges.", "Cooldown -10 seconds.", "Push nearby enemies away.", "Heal 4 health.", "Create an impenetrable shield for 7 seconds.", "Cooldown -10 seconds.", "Larger shield lasting 10 seconds.", "Heal 75% of maximum health." },
        new[] { "Place a banner for 30 seconds. Gain 10% damage and fire rate inside.", "Damage and fire rate +15%.", "Larger radius.", "Cooldown -10 seconds.", "Gain 30% damage reduction inside.", "Damage and fire rate +20%.", "Damage reduction increases to 50%.", "Cooldown -10 seconds.", "Damage and fire rate +25%.", "Keep the effects after leaving until the banner expires." },
        new[] { "Gain 50% of the experience needed for the next level.", "Gain 65% experience.", "2 charges.", "Cooldown -10 seconds.", "Enemies grant 10% more experience for 5 seconds.", "Gain 75% experience.", "Gain 25% movement speed and jump height for 5 seconds.", "Cooldown -10 seconds.", "Gain 100% experience.", "Experience and movement boosts last 10 seconds." }
    };
    public static string UpgradeDescription(int id, int level) => Upgrades[System.Array.IndexOf(Available, id)][Mathf.Clamp(level, 1, 10) - 1];
    public static bool IsThrowable(int id) => id == (int)ItemKind.Bomb;
    // The four-column sheet follows the stable item IDs from left to right, top to bottom.
    const int IconSize = 32;
    static readonly Sprite[] Icons = new Sprite[Count + 1];
    static Texture2D _sheet;

    public static Sprite Icon(int id)
    {
        if (!Valid(id)) return null;
        if (Icons[id] != null) return Icons[id];
        if (_sheet == null) _sheet = Resources.Load<Texture2D>("ItemIcons");
        if (_sheet == null) return null;
        int index = id - 1;
        var tile = new Rect(index % 4 * IconSize, _sheet.height - (index / 4 + 1) * IconSize, IconSize, IconSize);
        Icons[id] = Sprite.Create(_sheet, tile, Vector2.one * .5f, IconSize, 0, SpriteMeshType.FullRect);
        Icons[id].name = Names[id];
        return Icons[id];
    }
}
