using UnityEngine;

public enum ItemKind { None, Bomb, HealthPotion, Shield, BattleBanner, PocketCasino, CloneProjector, ExperiencePotion, Flamethrower, Sawblade, Shock, DivineIntervention, Nuke }
public enum PermanentUpgrade { Damage, Health, Speed, FireRate, Luck }

// Stable IDs are save data. Append new items; never reorder existing entries.
public static class ItemCatalog
{
    public const int Count = 12;
    public static readonly string[] Names = { "Empty", "Bomb", "Health Potion", "Shield", "Battle Banner", "Pocket Casino", "Clone", "Experience Potion", "Flamethrower", "Sawblade", "Shock", "Divine Intervention", "Nuke" };
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
    public static bool Valid(int id) => id > 0 && id <= Count;
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
        int index = id - 1;
        var tile = new Rect(index % 4 * IconSize, _sheet.height - (index / 4 + 1) * IconSize, IconSize, IconSize);
        Icons[id] = Sprite.Create(_sheet, tile, Vector2.one * .5f, IconSize, 0, SpriteMeshType.FullRect);
        Icons[id].name = Names[id];
        return Icons[id];
    }
}
