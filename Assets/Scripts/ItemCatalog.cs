using UnityEngine;

public enum ItemKind { None, Bomb, HealthPotion, Shield, BattleBanner, PocketCasino, CloneProjector, ExperiencePotion, Flamethrower }
public enum PermanentUpgrade { Damage, Health, Speed, FireRate, Luck }

// Stable IDs are save data. Append new items; never reorder existing entries.
public static class ItemCatalog
{
    public const int Count = 8;
    public static readonly string[] Names = { "Empty", "Bomb", "Health Potion", "Shield", "Battle Banner", "Pocket Casino", "Clone Projector", "Experience Potion", "Flamethrower" };
    public static readonly int[] Prices = { 0, 100, 100, 250, 250, 500, 500, 100, 250 };
    public static readonly string[] Descriptions = { "", "Lob an explosive that instantly defeats enemies in its blast.", "Instantly restore maximum health.", "Place an unbreakable shield for 30 seconds.", "Place a banner for 10 waves. Nearby players gain 15% damage and fire rate.", "Spin for a positive or negative effect lasting 2 waves.", "Create two AI clones with your current cards and half your fire rate for 2 waves.", "Instantly gain one level.", "Cover the entire map in flames for 30 seconds, dealing 5 damage per second." };
    public static bool Valid(int id) => id > 0 && id <= Count;
    static Sprite[] _icons;
    // Deliberately simple placeholder sprites; no replacement artwork or authored assets.
    public static Sprite Icon(int id)
    {
        if (!Valid(id)) return null;
        if (_icons == null) _icons = new Sprite[Count + 1];
        if (_icons[id] != null) return _icons[id];
        var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = Names[id] + " placeholder" };
        var pixels = new Color32[24 * 24];
        Color fill = Color.HSVToRGB((id - 1) / 8f, .65f, .95f);
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
