using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    public const int CurrentVersion = 3;
    public int Keys, KeyDropLevel;
    public int[] ItemLevels = new int[ItemCatalog.Count + 1];
    public int[] ItemShards = new int[ItemCatalog.Count + 1];
    public DailyQuestData Daily = new DailyQuestData();
    public int ProgressionVersion;
    public int TotalCoins;
    // Retired fields remain source-compatible with historical editor probes, never in new saves.
    [System.NonSerialized] public int MunitionsEssence, MobilityEssence, SurvivalEssence, GadgetEssence;
    [System.NonSerialized] public bool InfiniteGoldAndEssence;
    public bool InfiniteCopies;
    public bool ShowTips;
    public List<string> SeenTipIDs = new List<string>();
    public List<CardSaveData> CardCollection = new List<CardSaveData>();
    public int StartingCardSlots, PackOpeningTier;
    [System.NonSerialized] public int[] PermanentUpgrades = new int[5];
    [System.NonSerialized] public int[] ItemInventory = new int[ItemCatalog.Count + 1];
    public PlayerLoadout SoloLoadout = new PlayerLoadout();
    public PlayerLoadout[] CoopLoadouts = new PlayerLoadout[4];
    public int CompletedRuns;
    public bool FirstItemDropped;
    public TutorialProgress Tutorial = new TutorialProgress();
    [System.NonSerialized] public bool[] EssenceDiscovered = new bool[4];
    [System.NonSerialized] public long ItemShopRefreshUtc;
    [System.NonSerialized] public int[] ItemShopStock = new int[6];
    [System.NonSerialized] public bool[] ItemShopSold = new bool[6];

    public void NormalizeShopData()
    {
        Keys = UnityEngine.Mathf.Max(0, Keys);
        KeyDropLevel = UnityEngine.Mathf.Clamp(KeyDropLevel, 0, 5);
        NormalizeArray(ref ItemLevels, ItemCatalog.Count + 1);
        NormalizeArray(ref ItemShards, ItemCatalog.Count + 1);
        for (int i = 0; i < ItemLevels.Length; i++)
        {
            ItemLevels[i] = ItemCatalog.Valid(i) ? UnityEngine.Mathf.Clamp(ItemLevels[i], 0, 10) : 0;
            ItemShards[i] = UnityEngine.Mathf.Max(0, ItemShards[i]);
        }
        Daily ??= new DailyQuestData();
        StartingCardSlots = UnityEngine.Mathf.Clamp(StartingCardSlots, 0, 5);
        PackOpeningTier = UnityEngine.Mathf.Clamp(PackOpeningTier, 0, 2);
        NormalizeArray(ref PermanentUpgrades, 5);
        NormalizeArray(ref ItemInventory, ItemCatalog.Count + 1);
        NormalizeArray(ref EssenceDiscovered, 4);
        NormalizeArray(ref ItemShopStock, 6);
        NormalizeArray(ref ItemShopSold, 6);
        NormalizeArray(ref CoopLoadouts, 4);
        for (int i = 0; i < PermanentUpgrades.Length; i++) PermanentUpgrades[i] = UnityEngine.Mathf.Clamp(PermanentUpgrades[i], 0, 50);
        for (int i = 0; i < ItemInventory.Length; i++) ItemInventory[i] = UnityEngine.Mathf.Max(0, ItemInventory[i]);
        SoloLoadout ??= new PlayerLoadout(); SoloLoadout.Normalize();
        for (int i = 0; i < CoopLoadouts.Length; i++) { CoopLoadouts[i] ??= new PlayerLoadout(); CoopLoadouts[i].Normalize(); }
        CompletedRuns = UnityEngine.Mathf.Max(0, CompletedRuns);
        Tutorial ??= new TutorialProgress();
        Tutorial.Normalize(this);
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
        var data = new PlayerData { ProgressionVersion = CurrentVersion, Tutorial = new TutorialProgress { Version = 1 } };
        foreach (string id in StarterIDs) data.CardCollection.Add(new CardSaveData(id));
        return data;
    }

    public int GetEssence(CardPackType pack) => 0;
    public void AddEssence(CardPackType pack, int amount) { }
}

[System.Serializable]
public class PlayerLoadout
{
    public int[] Items = new int[ItemCatalog.SlotCount];
    public string[] Cards = new string[5];
    public void Normalize()
    {
        if (Items == null) Items = new int[ItemCatalog.SlotCount]; else if (Items.Length != ItemCatalog.SlotCount) System.Array.Resize(ref Items, ItemCatalog.SlotCount);
        if (Cards == null) Cards = new string[5]; else if (Cards.Length != 5) System.Array.Resize(ref Cards, 5);
        for (int i = 0; i < Items.Length; i++) if (Items[i] != 0 && !ItemCatalog.Valid(Items[i])) Items[i] = 0;
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
