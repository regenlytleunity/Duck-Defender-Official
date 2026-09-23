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
    public List<CardSaveData> CardCollection = new List<CardSaveData>();

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
