using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    public const int CurrentVersion = 2;
    public int ProgressionVersion;
    public int TotalCoins;
    public int MunitionsEssence, MobilityEssence, SurvivalEssence, GadgetEssence;
    public bool InfiniteGoldAndEssence;
    public bool InfiniteCopies;
    public bool ShowTips;
    public List<string> SeenTipIDs = new List<string>();
    public List<CardSaveData> CardCollection = new List<CardSaveData>();
    public int StartingCardSlots, PackOpeningTier;
    public int[] PermanentUpgrades = new int[5];
    public int[] ItemInventory = new int[9]; // ItemKind.None is zero, including in old saves.
    public PlayerLoadout SoloLoadout = new PlayerLoadout();
    public PlayerLoadout[] CoopLoadouts = new PlayerLoadout[4];
    public int CompletedRuns;
    public bool FirstItemDropped;
    public bool[] EssenceDiscovered = new bool[4];
    public long ItemShopRefreshUtc;
    public int[] ItemShopStock = new int[6];
    public bool[] ItemShopSold = new bool[6];

    public void NormalizeShopData()
    {
        StartingCardSlots = UnityEngine.Mathf.Clamp(StartingCardSlots, 0, 5);
        PackOpeningTier = UnityEngine.Mathf.Clamp(PackOpeningTier, 0, 2);
        NormalizeArray(ref PermanentUpgrades, 5);
        NormalizeArray(ref ItemInventory, 9);
        NormalizeArray(ref EssenceDiscovered, 4);
        NormalizeArray(ref ItemShopStock, 6);
        NormalizeArray(ref ItemShopSold, 6);
        NormalizeArray(ref CoopLoadouts, 4);
        for (int i = 0; i < PermanentUpgrades.Length; i++) PermanentUpgrades[i] = UnityEngine.Mathf.Clamp(PermanentUpgrades[i], 0, 50);
        for (int i = 0; i < ItemInventory.Length; i++) ItemInventory[i] = UnityEngine.Mathf.Max(0, ItemInventory[i]);
        SoloLoadout ??= new PlayerLoadout(); SoloLoadout.Normalize();
        for (int i = 0; i < CoopLoadouts.Length; i++) { CoopLoadouts[i] ??= new PlayerLoadout(); CoopLoadouts[i].Normalize(); }
        CompletedRuns = UnityEngine.Mathf.Max(0, CompletedRuns);
        for (int i = 0; i < 4; i++) if (GetEssence((CardPackType)i) > 0) EssenceDiscovered[i] = true;
    }
    static void NormalizeArray<T>(ref T[] array, int count)
    { if (array == null) array = new T[count]; else if (array.Length != count) System.Array.Resize(ref array, count); }

    public PlayerData()
    {
        TotalCoins = 0;
        CardCollection = new List<CardSaveData>();
    }

    public static readonly string[] StarterIDs = {
        "mun_faster_firing", "mun_sharp_eye", "mob_strong_legs", "mob_swiftness",
        "sur_exp_booster", "sur_thorns", "gad_big_feathers", "gad_slow_aura"
    };

    public static PlayerData CreateNew()
    {
        var data = new PlayerData { ProgressionVersion = CurrentVersion };
        foreach (string id in StarterIDs) data.CardCollection.Add(new CardSaveData(id));
        return data;
    }

    public int GetEssence(CardPackType pack)
    {
        switch (pack)
        {
            case CardPackType.Munitions: return MunitionsEssence;
            case CardPackType.Mobility: return MobilityEssence;
            case CardPackType.Survival: return SurvivalEssence;
            case CardPackType.Gadget: return GadgetEssence;
            default: return 0;
        }
    }

    public void AddEssence(CardPackType pack, int amount)
    {
        if (amount > 0 && (int)pack >= 0 && (int)pack < 4) { NormalizeArray(ref EssenceDiscovered, 4); EssenceDiscovered[(int)pack] = true; }
        int value = (int)System.Math.Max(0L, System.Math.Min(int.MaxValue, (long)GetEssence(pack) + amount));
        switch (pack)
        {
            case CardPackType.Munitions: MunitionsEssence = value; break;
            case CardPackType.Mobility: MobilityEssence = value; break;
            case CardPackType.Survival: SurvivalEssence = value; break;
            case CardPackType.Gadget: GadgetEssence = value; break;
        }
    }
}

[System.Serializable]
public class PlayerLoadout
{
    public int[] Items = new int[4];
    public string[] Cards = new string[5];
    public void Normalize()
    {
        if (Items == null) Items = new int[4]; else if (Items.Length != 4) System.Array.Resize(ref Items, 4);
        if (Cards == null) Cards = new string[5]; else if (Cards.Length != 5) System.Array.Resize(ref Cards, 5);
        for (int i = 0; i < Items.Length; i++) if (Items[i] < 0 || Items[i] > 8) Items[i] = 0;
    }
}

[System.Serializable]
public class CardSaveData
{
    public string CardID;
    public int Level;      // Current Level (1-6)
    public int Duplicates; // Progress towards next level
    public bool IsUnlocked; 
    public bool IsAscended;

    public CardSaveData(string id)
    {
        CardID = id;
        Level = 1;
        Duplicates = 0;
        IsUnlocked = true;
    }
}
