using System;
using TMPro;
using UnityEngine;

// Extends the authored shop and index. Existing art, font, and pack callbacks remain in use.
public class ShopUpdateUI : MonoBehaviour
{
    MainMenuUI _menu;
    ShopManager _shop;
    RectTransform _items, _upgrades;
    TMP_Text _timer;
    readonly UnityEngine.UI.Button[] _stock = new UnityEngine.UI.Button[6], _upgradeButtons = new UnityEngine.UI.Button[7];
    readonly UnityEngine.UI.Button[] _tabs = new UnityEngine.UI.Button[3];
    int _page;
    float _nextRefresh;
    public int Page => _page;
    public void Initialize(MainMenuUI menu)
    {
        _menu = menu; _shop = ShopManager.Instance;
        var background = menu.ShopPanel.transform.Find("BalancesBackground"); if (background != null) background.gameObject.SetActive(false);
        if (menu.EssenceBalancesText != null) menu.EssenceBalancesText.gameObject.SetActive(false);
        CoopUIElements.Stretch(menu.TotalCoinsText.rectTransform, new Vector2(.08f, .72f), new Vector2(.42f, .79f));
        _timer = CoopUIElements.Text(menu.ShopPanel.transform, "", new Vector2(.55f, .72f), new Vector2(.95f, .79f), 30);
        string[] names = { "Card packs", "Items", "Permanent upgrades" };
        for (int i = 0; i < 3; i++)
        {
            int page = i; _tabs[i] = CoopUIElements.Button(menu.ShopPanel.transform, names[i], new Vector2(.15f + i * .24f, .79f), new Vector2(.37f + i * .24f, .85f), () => ShowPage(page));
        }
        CoopUIElements.Stretch((RectTransform)menu.PackContainer, new Vector2(.045f, .08f), new Vector2(.955f, .69f));
        var grid = menu.PackContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter; grid.spacing = new Vector2(16, 0); grid.cellSize = new Vector2(390, 490);
        }
        for (int i = 0; i < _shop.AvailablePacks.Count && i < menu.PackContainer.childCount; i++)
        {
            var pack = _shop.AvailablePacks[i]; if (pack == null || (int)pack.PackType >= 4) continue;
            var text = menu.PackEssenceTexts[(int)pack.PackType]; if (text == null) continue;
            text.transform.SetParent(menu.PackContainer.GetChild(i), false);
            CoopUIElements.Stretch(text.rectTransform, new Vector2(0, .86f), new Vector2(1, .99f));
            var icon = menu.PackContainer.GetChild(i).Find("PackIcon") as RectTransform;
            if (icon != null) { icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(.5f, .5f); icon.anchoredPosition = Vector2.zero; icon.sizeDelta = new Vector2(234, 342); }
            var price = menu.PackContainer.GetChild(i).Find("Price") as RectTransform;
            if (price != null) CoopUIElements.Stretch(price, new Vector2(.05f, .01f), new Vector2(.95f, .12f));
        }
        _items = CoopUIElements.Panel(menu.ShopPanel.transform, "Item shop", new Vector2(.08f, .08f), new Vector2(.92f, .70f), Color.clear);
        _upgrades = CoopUIElements.Panel(menu.ShopPanel.transform, "Permanent upgrades", new Vector2(.1f, .07f), new Vector2(.9f, .70f), Color.clear);
        for (int i = 0; i < 6; i++)
        {
            int slot = i; float x = i % 3 / 3f, y = 1 - (i / 3 + 1) * .5f;
            var button = CoopUIElements.Button(_items, "", new Vector2(x + .02f, y + .04f), new Vector2(x + .313f, y + .46f), () => BuyStock(slot));
            _stock[i] = button;
            var icon = CoopUIElements.Panel(button.transform, "Icon", new Vector2(.28f, .24f), new Vector2(.72f, .9f), Color.white).GetComponent<UnityEngine.UI.Image>();
            icon.preserveAspect = true; icon.raycastTarget = false;
            CoopUIElements.Stretch(button.GetComponentInChildren<TMP_Text>().rectTransform, new Vector2(.02f, .015f), new Vector2(.98f, .23f));
        }
        for (int i = 0; i < 7; i++)
        {
            int upgrade = i; float y = .875f - i * .14f;
            _upgradeButtons[i] = CoopUIElements.Button(_upgrades, "", new Vector2(.03f, y), new Vector2(.97f, y + .115f), () =>
            {
                if (upgrade == 0) _shop.BuyStartingSlot(); else if (upgrade == 1) _shop.BuyPackUnlock(); else _shop.BuyPermanentUpgrade((PermanentUpgrade)(upgrade - 2));
            });
        }
        menu.BuySixButton = CoopUIElements.Button(menu.ConfirmPanel.transform, "Open 6", new Vector2(.55f, .32f), new Vector2(.77f, .43f), menu.BuySixPacks);
        var confirmButtons = menu.ConfirmPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        for (int i = 0; i < confirmButtons.Length; i++) CoopUIElements.Stretch((RectTransform)confirmButtons[i].transform, new Vector2(.04f + i * .24f, .3f), new Vector2(.26f + i * .24f, .43f));
        (menu.IndexPanel.GetComponent<IndexLoadoutUI>() ?? menu.IndexPanel.AddComponent<IndexLoadoutUI>()).Initialize(menu);
        _shop.OnCollectionChanged += Refresh;
        _shop.RefreshItemShop(); ShowPage(0);
    }
    public void ShowPage(int page)
    {
        _page = Mathf.Clamp(page, 0, 2);
        _menu.PackContainer.gameObject.SetActive(_page == 0); _items.gameObject.SetActive(_page == 1); _upgrades.gameObject.SetActive(_page == 2);
        _timer.gameObject.SetActive(_page == 1);
        for (int i = 0; i < 3; i++) _tabs[i].image.color = i == _page ? new Color(.35f, .4f, .25f) : new Color(.16f, .23f, .32f);
        Refresh(); MainMenuUI.FocusFirstControl(_page == 0 ? _menu.PackContainer.gameObject : _page == 1 ? _items.gameObject : _upgrades.gameObject);
    }
    void BuyStock(int slot)
    {
        // The displayed rotation must still match at the instant of purchase.
        _shop.BuyItem(slot, _displayedStock[slot], _displayedRefresh); Refresh();
    }
    readonly int[] _displayedStock = new int[6];
    long _displayedRefresh;
    void Update()
    {
        if (_shop == null || !_menu.ShopPanel.activeInHierarchy || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .25f; _shop.RefreshItemShop();
        long seconds = Math.Max(0, _shop.Progress.ItemShopRefreshUtc - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _timer.text = "Refresh in " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }
    public void Refresh()
    {
        if (_shop == null) return;
        _menu.UpdateCoinDisplay(_shop.CurrentCoins);
        var data = _shop.Progress; _displayedRefresh = data.ItemShopRefreshUtc;
        for (int i = 0; i < 6; i++)
        {
            int id = data.ItemShopStock[i]; _displayedStock[i] = id;
            _stock[i].transform.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite = ItemCatalog.Icon(id);
            _stock[i].GetComponentInChildren<TMP_Text>().text = data.ItemShopSold[i] ? "SOLD OUT" : ItemCatalog.Prices[id] + " Coins";
            _stock[i].interactable = !data.ItemShopSold[i] && _shop.CanAfford(ItemCatalog.Prices[id]);
        }
        _upgradeButtons[0].GetComponentInChildren<TMP_Text>().text = "Starting card slots  " + data.StartingCardSlots + "/5    " + (_shop.StartingSlotPrice == 0 ? "MAXED" : _shop.StartingSlotPrice + " Coins");
        _upgradeButtons[0].interactable = _shop.StartingSlotPrice > 0 && _shop.CanAfford(_shop.StartingSlotPrice);
        _upgradeButtons[1].GetComponentInChildren<TMP_Text>().text = data.PackOpeningTier >= 2 ? "Multiple pack opening    MAXED" : "Open " + (data.PackOpeningTier == 0 ? 3 : 6) + " packs    " + _shop.PackUnlockPrice + " Coins";
        _upgradeButtons[1].interactable = _shop.PackUnlockPrice > 0 && _shop.CanAfford(_shop.PackUnlockPrice);
        for (int i = 0; i < 5; i++)
        {
            int level = data.PermanentUpgrades[i], price = _shop.UpgradePrice((PermanentUpgrade)i);
            _upgradeButtons[i + 2].GetComponentInChildren<TMP_Text>().text = ((PermanentUpgrade)i) + "    +" + level + "%    " + (level >= 50 ? "MAXED" : "+1% - " + price + " Coins");
            _upgradeButtons[i + 2].interactable = level < 50 && _shop.CanAfford(price);
        }
    }
    void OnDestroy() { if (_shop != null) _shop.OnCollectionChanged -= Refresh; }
}
