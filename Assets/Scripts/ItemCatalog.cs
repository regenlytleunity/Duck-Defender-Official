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
    static Sprite[] _icons;
    // Deliberately simple placeholder sprites; no replacement artwork or authored assets.
    public static Sprite Icon(int id)
    {
        if (!Valid(id)) return null;
        if (_icons == null) _icons = new Sprite[Count + 1];
        if (_icons[id] != null) return _icons[id];
        var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = Names[id] + " placeholder" };
        var pixels = new Color32[24 * 24];
        Color fill = Color.HSVToRGB(Mathf.Repeat((id - 1) / 8f, 1), .65f, .95f);
        for (int y = 2; y < 22; y++) for (int x = 2; x < 22; x++)
        {
            bool body = id == 1 ? (x - 12) * (x - 12) + (y - 10) * (y - 10) < 70 :
                id == 2 || id == 7 ? (y < 14 && x > 5 && x < 19) || (y >= 14 && x > 9 && x < 15) :
                id == 3 ? Mathf.Abs(x - 12) < (y + 1) / 2 :
                id == 4 ? x < 5 || y > 11 && x < 20 :
                id == 5 ? (x - 12) * (x - 12) + (y - 12) * (y - 12) < 95 :
                id == 6 ? x < 10 || x > 13 : Mathf.Abs(x - 12) < 10 - Mathf.Abs(y - 10) / 2;
            if (body) pixels[y * 24 + x] = (x + y) % 9 == 0 ? Color.white : fill;
        }
        texture.SetPixels32(pixels); texture.Apply();
        _icons[id] = Sprite.Create(texture, new Rect(0, 0, 24, 24), Vector2.one * .5f, 24);
        return _icons[id];
    }
}
