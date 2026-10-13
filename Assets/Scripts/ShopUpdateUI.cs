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
    readonly TMP_Text[] _totals = new TMP_Text[5];
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
        var title = menu.ShopPanel.transform.Find("The Shop Text"); if (title != null) title.gameObject.SetActive(false);
        CoopUIElements.Stretch((RectTransform)menu.ShopPanel.transform.Find("BackButton"), new Vector2(.012f, .856f), new Vector2(.282f, .976f));
        CoopUIElements.Stretch(menu.TotalCoinsText.rectTransform, new Vector2(.34f, .856f), new Vector2(.72f, .976f));
        _timer = CoopUIElements.Text(menu.ShopPanel.transform, "", new Vector2(.81f, .015f), new Vector2(.99f, .10f), 30);
        string[] names = { "Cards", "Items", "Permanent upgrades" };
        for (int i = 0; i < 3; i++)
        {
            int page = i; _tabs[i] = CoopUIElements.Button(menu.ShopPanel.transform, names[i], new Vector2(.085f + i * .28f, .71f), new Vector2(.355f + i * .28f, .833f), () => ShowPage(page));
        }
        CoopUIElements.Stretch((RectTransform)menu.PackContainer, new Vector2(.05f, .055f), new Vector2(.95f, .65f));
        var grid = menu.PackContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (grid != null)
        {
            // Proportional cells below retain the art's aspect ratio at every window size.
            grid.enabled = false;
        }
        for (int i = 0; i < _shop.AvailablePacks.Count && i < menu.PackContainer.childCount; i++)
        {
            var pack = _shop.AvailablePacks[i]; if (pack == null || (int)pack.PackType >= 4) continue;
            CoopUIElements.Stretch((RectTransform)menu.PackContainer.GetChild(i), new Vector2(i * .25f + .005f, 0), new Vector2((i + 1) * .25f - .005f, 1));
            var text = menu.PackEssenceTexts[(int)pack.PackType]; if (text == null) continue;
            text.transform.SetParent(menu.PackContainer.GetChild(i), false);
            CoopUIElements.Stretch(text.rectTransform, new Vector2(0, .86f), new Vector2(1, 1));
            var icon = menu.PackContainer.GetChild(i).Find("PackIcon") as RectTransform;
            if (icon != null) CoopUIElements.Stretch(icon, new Vector2(.045f, .10f), new Vector2(.955f, .85f));
            var price = menu.PackContainer.GetChild(i).Find("Price") as RectTransform;
            if (price != null) CoopUIElements.Stretch(price, new Vector2(.05f, .01f), new Vector2(.95f, .12f));
        }
        _items = CoopUIElements.Panel(menu.ShopPanel.transform, "Item shop", new Vector2(.21f, .02f), new Vector2(.79f, .70f), Color.clear);
        _upgrades = CoopUIElements.Panel(menu.ShopPanel.transform, "Permanent upgrades", new Vector2(.215f, .03f), new Vector2(.96f, .65f), Color.clear);
        _items.GetComponent<UnityEngine.UI.Image>().raycastTarget = _upgrades.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        for (int i = 0; i < 6; i++)
        {
            int slot = i; float x = i % 3 / 3f, y = 1 - (i / 3 + 1) * .5f;
            var button = CoopUIElements.Button(_items, "", new Vector2(x + .01f, y + .012f), new Vector2(x + .323f, y + .488f), () => BuyStock(slot));
            button.name = "Item offer " + (i + 1);
            _stock[i] = button;
            var icon = CoopUIElements.Panel(button.transform, "Icon", new Vector2(.25f, .28f), new Vector2(.75f, .72f), Color.white).GetComponent<UnityEngine.UI.Image>();
            icon.preserveAspect = true; icon.raycastTarget = false;
            CoopUIElements.Stretch(button.GetComponentInChildren<TMP_Text>().rectTransform, new Vector2(.03f, .035f), new Vector2(.97f, .24f));
            var name = CoopUIElements.Text(button.transform, "", new Vector2(.03f, .76f), new Vector2(.97f, .97f), 30);
            name.name = "Item name";
        }
        for (int i = 0; i < 7; i++)
        {
            int upgrade = i;
            Vector2 min = i < 2 ? new Vector2(0, i == 0 ? .51f : 0) : new Vector2(.38f, .82f - (i - 2) * .205f);
            Vector2 max = i < 2 ? new Vector2(.36f, i == 0 ? 1 : .485f) : new Vector2(.745f, 1 - (i - 2) * .205f);
            _upgradeButtons[i] = CoopUIElements.Button(_upgrades, "", min, max, () =>
            {
                if (upgrade == 0) _shop.BuyStartingSlot(); else if (upgrade == 1) _shop.BuyPackUnlock(); else _shop.BuyPermanentUpgrade((PermanentUpgrade)(upgrade - 2));
            });
            _upgradeButtons[i].name = "Permanent upgrade " + i;
            if (i >= 2) _totals[i - 2] = CoopUIElements.Text(_upgrades, "", new Vector2(.755f, min.y), new Vector2(1, max.y), 28);
        }
        menu.BuySixButton = CoopUIElements.Button(menu.ConfirmPanel.transform, "Open 6", new Vector2(.55f, .32f), new Vector2(.77f, .43f), menu.BuySixPacks);
        var cancel = menu.ConfirmPanel.transform.Find("NoButton").GetComponent<UnityEngine.UI.Button>();
        cancel.GetComponentInChildren<TMP_Text>().text = "Cancel";
        var confirmButtons = new[] { menu.YesButton, menu.BuyThreeButton, menu.BuySixButton, cancel };
        for (int i = 0; i < confirmButtons.Length; i++) CoopUIElements.Stretch((RectTransform)confirmButtons[i].transform, new Vector2(.365f, .59f - i * .14f), new Vector2(.635f, .70f - i * .14f));
        CoopUIElements.Stretch(menu.ConfirmText.rectTransform, new Vector2(.20f, .75f), new Vector2(.80f, .88f));
        menu.ConfirmText.margin = Vector4.zero;
        var confirmBackground = menu.ConfirmPanel.GetComponent<UnityEngine.UI.Image>();
        if (confirmBackground != null) { var color = confirmBackground.color; color.a = 1; confirmBackground.color = color; }
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
            _stock[i].transform.Find("Item name").GetComponent<TMP_Text>().text = ItemCatalog.Names[id];
            _stock[i].GetComponentInChildren<TMP_Text>().text = data.ItemShopSold[i] ? "SOLD OUT" : ItemCatalog.Prices[id] + " Coins";
            _stock[i].interactable = !data.ItemShopSold[i] && _shop.CanAfford(ItemCatalog.Prices[id]);
        }
        _upgradeButtons[0].GetComponentInChildren<TMP_Text>().text = "Starting card slots +1\n" + data.StartingCardSlots + "/5\n" + (_shop.StartingSlotPrice == 0 ? "MAXED" : _shop.StartingSlotPrice + " Coins");
        _upgradeButtons[0].interactable = _shop.StartingSlotPrice > 0 && _shop.CanAfford(_shop.StartingSlotPrice);
        _upgradeButtons[1].GetComponentInChildren<TMP_Text>().text = data.PackOpeningTier >= 2 ? "Multiple pack opening\nMAXED" : "Open " + (data.PackOpeningTier == 0 ? 3 : 6) + " packs\n" + _shop.PackUnlockPrice + " Coins";
        _upgradeButtons[1].interactable = _shop.PackUnlockPrice > 0 && _shop.CanAfford(_shop.PackUnlockPrice);
        for (int i = 0; i < 5; i++)
        {
            int level = data.PermanentUpgrades[i], price = _shop.UpgradePrice((PermanentUpgrade)i);
            _upgradeButtons[i + 2].interactable = level < 50 && _shop.CanAfford(price);
            _upgradeButtons[i + 2].GetComponentInChildren<TMP_Text>().text = "+1% " + ((PermanentUpgrade)i) + "\n" + (level >= 50 ? "MAXED" : price + " Coins");
            _totals[i].text = "+" + level + "% total";
        }
        ControllerMenuNavigation.ConfigureNavigation(_menu.ShopPanel);
    }
    void OnDestroy() { if (_shop != null) _shop.OnCollectionChanged -= Refresh; }
}
