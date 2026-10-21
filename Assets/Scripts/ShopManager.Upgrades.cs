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
    public bool BuyPermanentUpgrade(PermanentUpgrade stat)
    {
        if ((int)stat < 0 || (int)stat >= 5 || _playerData.PermanentUpgrades[(int)stat] >= 50 || !ChargeUpgrade(UpgradePrice(stat))) return false;
        _playerData.PermanentUpgrades[(int)stat]++; SaveShopChange(); return true;
    }
    public bool RefreshItemShop(long utcSeconds = -1)
    {
        if (utcSeconds < 0) utcSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool valid = Array.TrueForAll(_playerData.ItemShopStock, ItemCatalog.Valid);
        if (valid && utcSeconds < _playerData.ItemShopRefreshUtc) return false;
        for (int i = 0; i < 6; i++) { _playerData.ItemShopStock[i] = UnityEngine.Random.Range(1, ItemCatalog.Count + 1); _playerData.ItemShopSold[i] = false; }
        _playerData.ItemShopRefreshUtc = (utcSeconds / 600 + 1) * 600;
        SaveShopChange(); return true;
    }
    public bool BuyItem(int slot, int expectedItem, long expectedRefresh)
    {
        RefreshItemShop();
        if (slot < 0 || slot >= 6 || expectedRefresh != _playerData.ItemShopRefreshUtc || _playerData.ItemShopStock[slot] != expectedItem || _playerData.ItemShopSold[slot] || !ItemCatalog.Valid(expectedItem)) return false;
        if (_playerData.ItemInventory[expectedItem] == int.MaxValue || !ChargeUpgrade(ItemCatalog.Prices[expectedItem])) return false;
        _playerData.ItemInventory[expectedItem]++; _playerData.ItemShopSold[slot] = true; SaveShopChange(); return true;
    }
    public int ReservedItems(int id, int seat = -1, int ignoreSlot = -1)
    {
        int count = 0;
        int seats = seat < 0 ? 1 : Mathf.Clamp(LocalCoopSession.RequestedPlayers, 1, 4);
        for (int p = 0; p < seats; p++)
        {
            int currentSeat = seat < 0 ? -1 : p;
            var loadout = Loadout(currentSeat);
            for (int s = 0; s < 4; s++) if (!(currentSeat == seat && s == ignoreSlot) && loadout.Items[s] == id) count++;
        }
        return count;
    }
    public bool EquipItem(int id, int slot, int seat = -1)
    {
        if (slot < 0 || slot >= 4 || id != 0 && (!ItemCatalog.Valid(id) || ReservedItems(id, seat, slot) >= _playerData.ItemInventory[id])) return false;
        Loadout(seat).Items[slot] = id; SaveShopChange(); return true;
    }
    public bool SellItem(int id, int seat = -1, int equippedSlot = -1)
    {
        if (!ItemCatalog.Valid(id) || _playerData.ItemInventory[id] <= ReservedItems(id, seat, equippedSlot)) return false;
        if (equippedSlot >= 0) { if (equippedSlot >= 4 || Loadout(seat).Items[equippedSlot] != id) return false; Loadout(seat).Items[equippedSlot] = 0; }
        _playerData.ItemInventory[id]--;
        CurrentCoins = (int)Math.Min(int.MaxValue, (long)CurrentCoins + ItemCatalog.Prices[id] / 2);
        SanitizeLoadouts(); SaveShopChange(); return true;
    }
    public bool EquipStartingCard(string id, int slot, int seat = -1)
    {
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
        int count = card ? _playerData.StartingCardSlots : 4;
        if (from < 0 || to < 0 || from >= count || to >= count) return false;
        var loadout = Loadout(seat);
        if (card) { string previous = loadout.Cards[to]; loadout.Cards[to] = loadout.Cards[from]; loadout.Cards[from] = previous; }
        else { int previous = loadout.Items[to]; loadout.Items[to] = loadout.Items[from]; loadout.Items[from] = previous; }
        SaveShopChange(); return true;
    }
    public static void SanitizeItems(PlayerData data, bool coop, int seats)
    {
        var used = new int[ItemCatalog.Count + 1];
        for (int p = 0; p < seats; p++)
        {
            var loadout = coop ? data.CoopLoadouts[p] : data.SoloLoadout;
            for (int s = 0; s < 4; s++)
            {
                int id = loadout.Items[s];
                if (!ItemCatalog.Valid(id)) continue;
                if (used[id] >= data.ItemInventory[id]) loadout.Items[s] = 0;
                else used[id]++;
            }
        }
    }
}
