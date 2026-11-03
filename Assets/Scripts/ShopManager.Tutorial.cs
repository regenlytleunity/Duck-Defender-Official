using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class ShopManager
{
    // This transaction writes the charge, all three cards and checkpoint in one save.
    public List<CardDefinition> BuyTutorialPack(ShopPackDefinition pack, int count)
    {
        var progress = _playerData.Tutorial;
        if (!progress.CoreActive || progress.Stage != TutorialStage.Shop || pack == null || count != 1 || pack.Cost < 0 || !CanAfford(pack.Cost)) return null;
        var starter = AllCards.Find(c => c != null && c.PackCategory == pack.PackType && c.Rarity == CardRarity.Common &&
            System.Array.IndexOf(PlayerData.StarterIDs, c.ID) >= 0 && GetCardData(c.ID)?.Level == 1);
        var locked = AllCards.Where(c => c != null && !c.IsBasic && c.PackCategory == pack.PackType && c.Rarity == CardRarity.Common && GetCardData(c.ID)?.IsUnlocked != true).ToList();
        if (starter == null || locked.Count == 0) return null;
        var newCard = locked[Random.Range(0, locked.Count)];
        if (!InfiniteResources) CurrentCoins -= pack.Cost;
        _playerData.TotalCoins = CurrentCoins;
        AddCardToCollection(starter.ID); AddCardToCollection(starter.ID); AddCardToCollection(newCard.ID);
        progress.Pack = pack.PackType; progress.UpgradeCardID = starter.ID; progress.NewCardID = newCard.ID;
        progress.Stage = TutorialStage.PackPurchased;
        SaveSystem.SaveData(_playerData, updateTutorial: true);
        OnCollectionChanged?.Invoke();
        return new List<CardDefinition> { starter, starter, newCard };
    }
    public bool TutorialCanUpgrade(string id) => !_playerData.Tutorial.CoreActive ||
        _playerData.Tutorial.Stage == TutorialStage.Upgrade && id == _playerData.Tutorial.UpgradeCardID;
    public bool TutorialBlocksShopping => _playerData.Tutorial.CoreActive || _playerData.Tutorial.ItemActive;
}
