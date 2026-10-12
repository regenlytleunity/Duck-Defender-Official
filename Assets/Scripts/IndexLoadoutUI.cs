using UnityEngine;

public class IndexLoadoutUI : MonoBehaviour
{
    CardIndexUI _index;
    ShopManager _shop;
    LoadoutPanel _equipment;
    RectTransform _slots;
    UnityEngine.UI.Button _itemsTab;
    bool _itemsMode;
    public GameObject NavigationScope => _equipment != null && _equipment.HasModal ? _equipment.NavigationScope : gameObject;
    public void Initialize(MainMenuUI menu)
    {
        _index = GetComponent<CardIndexUI>(); _shop = ShopManager.Instance;
        _equipment = new LoadoutPanel(transform, -1, false, null, this);
        CoopUIElements.Stretch(_equipment.Root, new Vector2(.30f, .10f), new Vector2(.99f, .98f));
        _equipment.Root.gameObject.SetActive(false);
        _slots = CoopUIElements.Panel(transform, "Starting cards", new Vector2(.005f, .03f), new Vector2(.087f, .96f), Color.clear);
        _slots.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _itemsTab = Instantiate(_index.PackButtons[(int)CardPackType.Gadget], transform);
        _itemsTab.name = "ItemsTab"; _itemsTab.onClick.RemoveAllListeners(); _itemsTab.onClick.AddListener(ShowItems);
        _itemsTab.GetComponentInChildren<TMPro.TMP_Text>().text = "Items";
        foreach (var button in _index.PackButtons) button.onClick.AddListener(ShowCards);
        _shop.OnCollectionChanged += Refresh;
        Refresh();
    }
    void OnEnable() { if (_equipment != null) { ShowCards(); Refresh(); } }
    public bool HandleBack() => _equipment != null && _equipment.HandleBack();
    public void ShowItems() { _itemsMode = true; ApplyVisibility(); _equipment.Rebuild(); MainMenuUI.FocusFirstControl(_equipment.Root.gameObject); }
    public void ShowCards() { _itemsMode = false; ApplyVisibility(); BindCards(); }
    public void Refresh()
    {
        if (_equipment == null) return;
        foreach (Transform child in _slots) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        for (int i = 0; i < _shop.Progress.StartingCardSlots; i++)
        {
            float y = 1 - (i + 1) * .2f;
            _equipment.MiniCard(_slots, _shop.Loadout().Cards[i], new Vector2(0, y + .01f), new Vector2(1, y + .195f), i, true);
        }
        if (_itemsMode) _equipment.Rebuild();
        ApplyVisibility(); BindCards();
    }
    void ApplyVisibility()
    {
        if (_equipment == null) return;
        bool host = _index.HostFilterMode, starters = !host && _shop.Progress.StartingCardSlots > 0;
        if (host) _itemsMode = false;
        _slots.gameObject.SetActive(starters);
        _itemsTab.gameObject.SetActive(!host);
        _equipment.Root.gameObject.SetActive(_itemsMode);
        _index.ContentArea.gameObject.SetActive(!_itemsMode);
        _index.PreviousButton.gameObject.SetActive(!_itemsMode); _index.NextButton.gameObject.SetActive(!_itemsMode);
        _index.PageText.gameObject.SetActive(!_itemsMode); _index.LayoutButton.gameObject.SetActive(!_itemsMode);
        _index.CoinsText.gameObject.SetActive(!_itemsMode && !host); _index.EssenceText.gameObject.SetActive(!_itemsMode && !host);
        float center = starters ? .225f : .15f;
        CoopUIElements.Stretch(_index.CoinsText.rectTransform, new Vector2(starters ? .36f : .29f, .90f), new Vector2(.64f, .99f));
        CoopUIElements.Stretch(_index.EssenceText.rectTransform, new Vector2(.65f, .90f), new Vector2(.99f, .99f));
        _index.CoinsText.enableAutoSizing = _index.EssenceText.enableAutoSizing = true;
        _index.CoinsText.fontSizeMin = _index.EssenceText.fontSizeMin = 14;
        var backRect = _index.transform.Find("BackButton") as RectTransform;
        if (backRect != null) { backRect.anchorMin = backRect.anchorMax = new Vector2(center, backRect.anchorMax.y); backRect.anchoredPosition = new Vector2(0, backRect.anchoredPosition.y); }
        int[] order = { 1, 0, 2, 3 };
        for (int i = 0; i < 4; i++)
        {
            var rect = (RectTransform)_index.PackButtons[order[i]].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(center, .73f - i * .13f); rect.anchoredPosition = Vector2.zero;
        }
        var tabRect = (RectTransform)_itemsTab.transform; tabRect.anchorMin = tabRect.anchorMax = new Vector2(center, .20f); tabRect.anchoredPosition = Vector2.zero;
        var layoutRect = (RectTransform)_index.LayoutButton.transform; layoutRect.anchorMin = layoutRect.anchorMax = new Vector2(center, .07f); layoutRect.anchoredPosition = Vector2.zero;
        CoopUIElements.Stretch((RectTransform)_index.ContentArea, new Vector2(starters ? .36f : .29f, .12f), new Vector2(.99f, .88f));
        CoopUIElements.Stretch(_equipment.Root, new Vector2(starters ? .36f : .29f, .10f), new Vector2(.99f, .98f));
        _equipment.Root.SetAsLastSibling();
        _index.ConfigureNavigation();
        var gadget = _index.PackButtons[(int)CardPackType.Gadget]; var nav = gadget.navigation;
        if (!host) { nav.selectOnDown = _itemsTab; gadget.navigation = nav; }
        nav = _itemsTab.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.Explicit; nav.selectOnUp = gadget; nav.selectOnDown = _index.LayoutButton; nav.selectOnRight = _equipment.Root.GetComponentInChildren<UnityEngine.UI.Button>(); _itemsTab.navigation = nav;
    }
    public void BindCards()
    {
        if (_equipment == null || _index.HostFilterMode) return;
        foreach (var display in _index.ContentArea.GetComponentsInChildren<CardDisplay>())
            if (display.AssignedCard != null) _equipment.BindCollectionCard(display, display.AssignedCard);
    }
    void OnDestroy() { if (_shop != null) _shop.OnCollectionChanged -= Refresh; }
}
