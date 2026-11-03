using System;

public enum TutorialStage { Basics, Play, Waves, Shop, PackPurchased, ShopReturn, Index, Upgrade, IndexReturn, Complete }
public enum ItemTutorialStage { None, Index, Tab, Equip, Controls, Complete }

// Only durable checkpoints live in the account save. Practice objectives restart safely.
[Serializable]
public class TutorialProgress
{
    public int Version;
    public TutorialStage Stage;
    public ItemTutorialStage Items;
    public bool HasObtainedItem;
    public string UpgradeCardID;
    public string NewCardID;
    public CardPackType Pack;
    public bool CoreActive => Version == 1 && Stage < TutorialStage.Complete;
    public bool ItemActive => !CoreActive && HasObtainedItem && Items != ItemTutorialStage.Complete;

    public void Normalize(PlayerData data)
    {
        if (!Enum.IsDefined(typeof(TutorialStage), Stage)) Stage = TutorialStage.Basics;
        if (!Enum.IsDefined(typeof(ItemTutorialStage), Items)) Items = ItemTutorialStage.None;
        if (data.ItemInventory != null)
            for (int i = 1; i < data.ItemInventory.Length; i++) HasObtainedItem |= data.ItemInventory[i] > 0;
    }

    public static bool GrantWaveReward(PlayerData data)
    {
        if (!data.Tutorial.CoreActive || data.Tutorial.Stage != TutorialStage.Waves) return false;
        data.TotalCoins = (int)Math.Min(int.MaxValue, (long)data.TotalCoins + 200);
        data.Tutorial.Stage = TutorialStage.Shop;
        return true;
    }
}
