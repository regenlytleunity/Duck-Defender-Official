using System;
using UnityEngine;

public partial class ShopManager
{
    public PlayerData Progress => _playerData;
    public bool CanOpenPackCount(int count) => count == 1 || count == 3 && _playerData.PackOpeningTier >= 1 || count == 6 && _playerData.PackOpeningTier >= 2;
    public int StartingSlotPrice => _playerData.StartingCardSlots < 5 ? 1000 << _playerData.StartingCardSlots : 0;
    public int PackUnlockPrice => _playerData.PackOpeningTier == 0 ? 1000 : _playerData.PackOpeningTier == 1 ? 5000 : 0;
    public int UpgradePrice(PermanentUpgrade stat) => 100 * (_playerData.PermanentUpgrades[(int)stat] + 1);
    public PlayerLoadout Loadout(int seat = -1) => seat < 0 ? _playerData.SoloLoadout : _playerData.CoopLoadouts[Mathf.Clamp(seat, 0, 3)];
    void SaveShopChange()
    {
        _playerData.TotalCoins = CurrentCoins;
        SaveSystem.SaveData(_playerData);
        OnCollectionChanged?.Invoke();
    }
    bool ChargeUpgrade(int price)
    {
        if (TutorialBlocksShopping) return false;
        if (price <= 0 || !CanAfford(price)) return false;
        if (!InfiniteResources) CurrentCoins -= price;
        return true;
    }
    public bool BuyStartingSlot()
    {
        if (!ChargeUpgrade(StartingSlotPrice)) return false;
        _playerData.StartingCardSlots++; SaveShopChange(); return true;
    }
    public bool BuyPackUnlock()
    {
        if (!ChargeUpgrade(PackUnlockPrice)) return false;
        _playerData.PackOpeningTier++; SaveShopChange(); return true;
    }
    public bool BuyPermanentUpgrade(PermanentUpgrade stat) => false;
    public bool RefreshItemShop(long utcSeconds = -1) => false;
    public bool BuyItem(int slot, int expectedItem, long expectedRefresh) => false;
    public int ReservedItems(int id, int seat = -1, int ignoreSlot = -1)
    {
        int count = 0;
        int seats = seat < 0 ? 1 : Mathf.Clamp(LocalCoopSession.RequestedPlayers, 1, 4);
        for (int p = 0; p < seats; p++)
        {
            int currentSeat = seat < 0 ? -1 : p;
            var loadout = Loadout(currentSeat);
            for (int s = 0; s < ItemCatalog.SlotCount; s++) if (!(currentSeat == seat && s == ignoreSlot) && loadout.Items[s] == id) count++;
        }
        return count;
    }
    public bool EquipItem(int id, int slot, int seat = -1)
    {
        if (_playerData.Tutorial.CoreActive || _playerData.Tutorial.ItemActive && (id == 0 || seat >= 0 || _playerData.Tutorial.Items != ItemTutorialStage.Equip)) return false;
        if (slot < 0 || slot >= ItemCatalog.SlotCount || id != 0 && (!ItemCatalog.Valid(id) || _playerData.ItemLevels[id] <= 0)) return false;
        var items = Loadout(seat).Items;
        if (id != 0) for (int i = 0; i < items.Length; i++) if (i != slot && items[i] == id) items[i] = 0;
        items[slot] = id;
        if (_playerData.Tutorial.ItemActive)
        {
            _playerData.Tutorial.Items = ItemTutorialStage.Controls;
            SaveSystem.SaveData(_playerData, updateTutorial: true); OnCollectionChanged?.Invoke();
        }
        else SaveShopChange();
        return true;
    }
    public bool SellItem(int id, int seat = -1, int equippedSlot = -1) => false;
    public bool EquipStartingCard(string id, int slot, int seat = -1)
    {
        if (TutorialBlocksShopping) return false;
        if (slot < 0 || slot >= _playerData.StartingCardSlots) return false;
        var loadout = Loadout(seat);
        if (!string.IsNullOrEmpty(id))
        {
            var card = AllCards.Find(c => c != null && c.ID == id && !c.IsBasic);
            if (card == null || GetCardData(id)?.IsUnlocked != true || seat >= 0 && LocalCoopSession.DisabledCards.Contains(id)) return false;
            for (int i = 0; i < loadout.Cards.Length; i++) if (i != slot && loadout.Cards[i] == id) return false;
        }
        loadout.Cards[slot] = id; SaveShopChange(); return true;
    }
    public void SanitizeLoadouts()
    {
        SanitizeItems(_playerData, false, 1);
        SanitizeItems(_playerData, true, 4);
    }
    public bool MoveEquipment(bool card, int from, int to, int seat = -1)
    {
        if (TutorialBlocksShopping) return false;
        int count = card ? _playerData.StartingCardSlots : ItemCatalog.SlotCount;
        if (from < 0 || to < 0 || from >= count || to >= count) return false;
        var loadout = Loadout(seat);
        if (card) { string previous = loadout.Cards[to]; loadout.Cards[to] = loadout.Cards[from]; loadout.Cards[from] = previous; }
        else { int previous = loadout.Items[to]; loadout.Items[to] = loadout.Items[from]; loadout.Items[from] = previous; }
        SaveShopChange(); return true;
    }
    public static void SanitizeItems(PlayerData data, bool coop, int seats)
    {
        for (int p = 0; p < seats; p++)
        {
            var loadout = coop ? data.CoopLoadouts[p] : data.SoloLoadout;
            for (int s = 0; s < ItemCatalog.SlotCount; s++)
            {
                int id = loadout.Items[s];
                if (!ItemCatalog.Valid(id)) continue;
                if (data.ItemLevels[id] <= 0 || System.Array.IndexOf(loadout.Items, id) != s) loadout.Items[s] = 0;
            }
        }
    }
}
