using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ShopUpdateVerification
{
    static int _checks;
    static void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException("Shop update: " + label); _checks++; }
    static void RequireIsolation()
    { if (!EditorApplication.isPlaying || string.IsNullOrEmpty(SaveSystem.VerificationSavePath)) throw new InvalidOperationException("Use Start Isolated Play Verification first."); }
    [MenuItem("Duck Defender/Shop 2/Start isolated verification")]
    public static void Start() { UIUpdateVerification.StartPlay(); }

    public static string CheckEconomy()
    {
        RequireIsolation(); _checks = 0;
        var shop = ShopManager.Instance; var menu = MainMenuUI.Instance;
        var old = new PlayerData { ProgressionVersion = 2, TotalCoins = 1234 };
        old.CardCollection.Add(new CardSaveData("mun_faster_firing") { Level = 4, Duplicates = 3 });
        SaveSystem.SaveData(old); shop.LoadEconomy();
        Check(shop.CurrentCoins == 1234 && shop.GetCardData("mun_faster_firing").Level == 4 && shop.GetCardData("mun_faster_firing").Duplicates == 3, "existing progression preserved");
        Check(shop.Progress.StartingCardSlots == 0 && shop.Progress.PackOpeningTier == 0 && shop.Loadout(0).Items.Length == 4, "new fields default safely");
        shop.Progress.TotalCoins = 500000; SaveSystem.SaveData(shop.Progress); shop.LoadEconomy();
        var pack = shop.AvailablePacks.First(p => p != null);
        int coins = shop.CurrentCoins;
        Check(shop.TryBuyPacks(pack, 3) == null && shop.CurrentCoins == coins, "locked bulk purchase rejected without charge");
        Check(shop.BuyPackUnlock() && shop.CurrentCoins == coins - 1000 && shop.CanOpenPackCount(3), "open three unlock price");
        Check(shop.BuyPackUnlock() && shop.CurrentCoins == coins - 6000 && shop.CanOpenPackCount(6), "open six unlock price");
        Check(!shop.BuyPackUnlock(), "bulk unlock cap");
        Check(shop.TryBuyPacks(pack, 6).Count == 18, "six packs grant eighteen cards immediately");
        shop.LoadEconomy(); Check(shop.Progress.PackOpeningTier == 2, "bulk unlock persists");
        for (int i = 0; i < 5; i++) { int price = 1000 << i; coins = shop.CurrentCoins; Check(shop.StartingSlotPrice == price && shop.BuyStartingSlot() && shop.CurrentCoins == coins - price, "starting slot price " + i); }
        Check(!shop.BuyStartingSlot(), "starting slot cap");
        for (int i = 0; i < 50; i++) { coins = shop.CurrentCoins; Check(shop.BuyPermanentUpgrade(PermanentUpgrade.Damage) && coins - shop.CurrentCoins == 100 * (i + 1), "damage upgrade price " + (i + 1)); }
        Check(!shop.BuyPermanentUpgrade(PermanentUpgrade.Damage) && shop.Progress.PermanentUpgrades[0] == 50, "permanent upgrade cap");
        shop.RefreshItemShop(); var stock = (int[])shop.Progress.ItemShopStock.Clone(); long expiry = shop.Progress.ItemShopRefreshUtc;
        int id = stock[0]; coins = shop.CurrentCoins;
        Check(shop.BuyItem(0, id, expiry) && coins - shop.CurrentCoins == ItemCatalog.Prices[id], "item purchase charges once");
        Check(!shop.BuyItem(0, id, expiry), "sold out cannot be purchased twice");
        shop.LoadEconomy(); Check(shop.Progress.ItemShopSold[0] && shop.Progress.ItemShopStock.SequenceEqual(stock), "stock and sold state persist");
        shop.RefreshItemShop(expiry + 1);
        Check(!shop.Progress.ItemShopSold.Any(s => s), "ten minute rollover clears sold state");
        Check(!shop.BuyItem(1, stock[1], expiry), "stale purchase cannot charge a replacement slot");
        shop.Progress.ItemInventory[1] = 1; shop.Progress.ItemInventory[2] = 2;
        Check(shop.EquipItem(1, 0) && !shop.EquipItem(1, 1), "solo reserves one copy per slot");
        Check(shop.MoveEquipment(false, 0, 1) && shop.Loadout().Items[1] == 1 && shop.Loadout().Items[0] == 0, "dragging moves one reserved item");
        Check(!shop.SellItem(1), "reserved copy cannot be sold from inventory");
        coins = shop.CurrentCoins; Check(shop.SellItem(1, -1, 1) && shop.CurrentCoins == coins + 50 && shop.Loadout().Items[1] == 0, "selling equipped item clears slot and refunds half");
        LocalCoopSession.RequestedPlayers = 2;
        Check(shop.EquipItem(2, 0, 0) && shop.EquipItem(2, 0, 1) && !shop.EquipItem(2, 1, 1), "co-op reservations cannot oversubscribe host inventory");
        Check(shop.EquipStartingCard("mun_faster_firing", 0) && !shop.EquipStartingCard("mun_faster_firing", 1), "starting cards reject duplicates");
        Check(shop.EquipStartingCard("mun_faster_firing", 0, 0) && shop.EquipStartingCard("mun_faster_firing", 0, 1), "starting cards may be shared across players");
        var basic = ScriptableObject.CreateInstance<CardDefinition>(); basic.ID = "verification_basic"; basic.PackCategory = CardPackType.BaseSet;
        shop.AllCards.Add(basic); shop.Progress.CardCollection.Add(new CardSaveData(basic.ID) { Level = 1 });
        Check(!shop.EquipStartingCard(basic.ID, 1), "Basic cards excluded from starting slots");
        shop.AllCards.Remove(basic); UnityEngine.Object.Destroy(basic);
        LocalCoopSession.RequestedPlayers = 1;
        shop.Progress.TotalCoins = 0; SaveSystem.SaveData(shop.Progress); shop.LoadEconomy();
        Check(!shop.BuyPermanentUpgrade(PermanentUpgrade.Health), "insufficient funds rejected");
        Seed(); menu.OpenShop(); menu.GetComponent<ShopUpdateUI>().ShowPage(2);
        return "PASS: " + _checks + " economy checks";
    }
    public static void Seed()
    {
        RequireIsolation(); var shop = ShopManager.Instance;
        var data = PlayerData.CreateNew(); data.NormalizeShopData(); data.TotalCoins = 500000;
        data.StartingCardSlots = 5; data.PackOpeningTier = 2; data.CompletedRuns = 2;
        data.PermanentUpgrades = new[] { 50, 50, 50, 50, 50 };
        data.CardCollection = shop.AllCards.Where(c => c != null && !c.IsBasic).Select(c => new CardSaveData(c.ID) { Level = 1 }).ToList();
        for (int i = 1; i <= 8; i++) data.ItemInventory[i] = 10;
        data.SoloLoadout.Items = new[] { 1, 2, 3, 4 }; data.SoloLoadout.Cards[0] = "mun_faster_firing";
        SaveSystem.SaveData(data, true); shop.LoadEconomy(); shop.RefreshItemShop(); MainMenuUI.Instance.GetComponent<ShopUpdateUI>().Refresh();
        MainMenuUI.Instance.IndexPanel.GetComponent<IndexLoadoutUI>().Refresh();
    }
    public static string CheckVisibleUI()
    {
        RequireIsolation(); Canvas.ForceUpdateCanvases(); _checks = 0;
        var menu = MainMenuUI.Instance; var scope = menu.NavigationScope;
        var labels = scope.GetComponentsInChildren<TMPro.TMP_Text>();
        foreach (var label in labels)
        {
            label.ForceMeshUpdate();
            Check(label.font == menu.UIFont && label.color.r == 1 && label.color.g == 1 && label.color.b == 1, "original font and white infill: " + label.text);
            Check(!label.isTextOverflowing, "text fits: " + label.text);
        }
        foreach (var control in scope.GetComponentsInChildren<UnityEngine.UI.Selectable>())
        {
            if (!control.IsActive() || !control.IsInteractable()) continue;
            var rect = (RectTransform)control.transform;
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() == control, "unblocked pointer target: " + control.name);
        }
        return "PASS: " + _checks + " visible UI checks";
    }
    public static void StartGameplay()
    {
        RequireIsolation(); Seed();
        var probe = new GameObject("Shop update isolated gameplay checks").AddComponent<ShopUpdatePlayProbe>();
        UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
    }
    public static void StartUI()
    {
        RequireIsolation();
        var probe = new GameObject("Shop update UI checks").AddComponent<ShopUpdatePlayProbe>();
        probe.UIOnly = true; probe.SeedUI = Seed; probe.CheckUI = CheckVisibleUI;
    }
}
