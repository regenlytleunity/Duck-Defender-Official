using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class ShopManager
{
    static readonly int[] KeyUpgradePrices = { 250, 1000, 2500, 5000, 10000 };
    public int KeyUpgradePrice => _playerData.KeyDropLevel >= 5 ? 0 : KeyUpgradePrices[_playerData.KeyDropLevel];
    public bool BuyKeyUpgrade()
    {
        if (!ChargeUpgrade(KeyUpgradePrice)) return false;
        _playerData.KeyDropLevel++;
        SaveShopChange();
        return true;
    }
    public int OpenItemBox()
    {
        if (TutorialBlocksShopping || _playerData.Keys <= 0) return 0;
        float total = 0;
        foreach (int id in ItemCatalog.Available) total += _playerData.ItemLevels[id] == 10 ? .5f : 1;
        float roll = UnityEngine.Random.value * total;
        int selected = ItemCatalog.Available[ItemCatalog.Available.Length - 1];
        foreach (int id in ItemCatalog.Available)
        {
            roll -= _playerData.ItemLevels[id] == 10 ? .5f : 1;
            if (roll < 0) { selected = id; break; }
        }
        _playerData.Keys--;
        if (_playerData.ItemLevels[selected] == 0) _playerData.ItemLevels[selected] = 1;
        else _playerData.ItemShards[selected] = (int)Math.Min(int.MaxValue, (long)_playerData.ItemShards[selected] + 10);
        _playerData.Tutorial.HasObtainedItem = true;
        DailyQuests.Record(_playerData, DailyQuestKind.Boxes, 1);
        SaveSystem.SaveData(_playerData, updateTutorial: true);
        OnCollectionChanged?.Invoke();
        return selected;
    }
    public bool CanUpgradeItem(int id)
    {
        if (!ItemCatalog.Valid(id)) return false;
        int level = _playerData.ItemLevels[id];
        return !TutorialBlocksShopping && level > 0 && level < 10 && _playerData.ItemShards[id] >= ItemCatalog.ShardCosts[level] && CanAfford(ItemCatalog.GoldCosts[level]);
    }
    public bool UpgradeItem(int id)
    {
        if (!CanUpgradeItem(id)) return false;
        int level = _playerData.ItemLevels[id];
        if (!ChargeUpgrade(ItemCatalog.GoldCosts[level])) return false;
        _playerData.ItemShards[id] -= ItemCatalog.ShardCosts[level];
        _playerData.ItemLevels[id]++;
        SaveShopChange();
        return true;
    }
    CardDefinition PickWeightedCard(List<CardDefinition> pool)
    {
        float total = pool.Sum(c => GetCardData(c.ID)?.Level >= c.MaxLevel ? .5f : 1);
        float roll = UnityEngine.Random.value * total;
        foreach (var card in pool)
        {
            roll -= GetCardData(card.ID)?.Level >= card.MaxLevel ? .5f : 1;
            if (roll < 0) return card;
        }
        return pool[pool.Count - 1];
    }
    public bool RefreshDaily()
    {
        if (!DailyQuests.Ensure(_playerData)) return false;
        SaveShopChange();
        return true;
    }
    public bool ClaimQuest(int index, out List<CardDefinition> cards, out string maxed)
    {
        cards = new List<CardDefinition>(); maxed = null;
        RefreshDaily();
        var daily = _playerData.Daily;
        if (TutorialBlocksShopping || index < 0 || index >= 3 || daily.Claimed[index] ||
            daily.Progress[index] < DailyQuests.Targets[daily.Kinds[index]]) return false;
        int reward = daily.Rewards[index];
        int gold = reward == 0 ? 500 : reward == 4 ? 1000 : reward == 10 ? 2500 : reward == 12 ? 10000 : 0;
        int keys = reward == 7 ? 1 : reward == 11 ? 3 : reward == 15 ? 10 : 0;
        if (index == daily.Bonus) keys++;
        CurrentCoins = (int)Math.Min(int.MaxValue, (long)CurrentCoins + gold);
        _playerData.Keys = (int)Math.Min(int.MaxValue, (long)_playerData.Keys + keys);
        bool maximum = reward == 9 || reward == 13 || reward == 14;
        int count = reward == 1 || reward == 8 ? 4 : reward == 2 ? 2 : reward == 5 ? 16 : reward == 6 ? 8 : reward == 3 || maximum ? 1 : 0;
        var rarity = reward == 2 || reward == 6 || reward == 13 ? CardRarity.Rare : reward == 3 || reward == 8 || reward == 14 ? CardRarity.Legendary : CardRarity.Common;
        var pool = AllCards.Where(c => c != null && !c.IsBasic && c.Rarity == rarity &&
            (!maximum || GetCardData(c.ID)?.Level < c.MaxLevel || GetCardData(c.ID) == null)).ToList();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var card = pool[UnityEngine.Random.Range(0, pool.Count)];
            AddCardToCollection(card.ID);
            if (maximum) { GetCardData(card.ID).Level = card.MaxLevel; GetCardData(card.ID).Duplicates = 0; maxed = card.CardName + " maxed"; }
            cards.Add(card);
        }
        daily.Claimed[index] = true;
        SaveShopChange();
        return true;
    }
    bool ExecuteReworkCode(string code)
    {
        if (code == "1301") DailyQuests.Ensure(_playerData, force: true);
        else if (code == "1302") _playerData.Keys = (int)Math.Min(int.MaxValue, (long)_playerData.Keys + 1000);
        else if (code == "1303" || code == "1304")
            foreach (int id in ItemCatalog.Available) _playerData.ItemLevels[id] = Math.Max(_playerData.ItemLevels[id], code == "1303" ? 10 : 1);
        else return false;
        SaveShopChange();
        return true;
    }
}
