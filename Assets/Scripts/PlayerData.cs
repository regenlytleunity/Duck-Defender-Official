using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    public int TotalCoins;
    public List<CardSaveData> CardCollection = new List<CardSaveData>();

    public PlayerData()
    {
        TotalCoins = 0;
        CardCollection = new List<CardSaveData>();
    }
}

[System.Serializable]
public class CardSaveData
{
    public string CardID;
    public int Level;      // Current Level (1-5)
    public int Duplicates; // Progress towards next level
    public bool IsUnlocked; 

    public CardSaveData(string id)
    {
        CardID = id;
        Level = 1;
        Duplicates = 0;
        IsUnlocked = true;
    }
}