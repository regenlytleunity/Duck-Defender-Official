using UnityEngine;

public class IndexLoadoutUI : MonoBehaviour
{
    CardIndexUI _index;
    ShopManager _shop;
    LoadoutPanel _equipment;
    RectTransform _slots;
    UnityEngine.UI.Button _itemsTab;
    bool _itemsMode;
    public bool ItemsMode => _itemsMode;
    public GameObject NavigationScope => _equipment != null && _equipment.HasModal ? _equipment.NavigationScope : gameObject;
    public void Initialize(MainMenuUI menu)
    {
        _index = GetComponent<CardIndexUI>(); _shop = ShopManager.Instance;
        _equipment = new LoadoutPanel(transform, -1, false, null, this);
        CoopUIElements.Stretch(_equipment.Root, new Vector2(.305f, .015f), new Vector2(.975f, .98f));
        _equipment.Root.gameObject.SetActive(false);
        _slots = CoopUIElements.Panel(transform, "Starting cards", new Vector2(.91f, .015f), new Vector2(.987f, .985f), Color.clear);
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
    public void ShowItems() { _equipment.CloseModal(); _itemsMode = true; ApplyVisibility(); _equipment.Rebuild(); }
    public void ShowCards() { _equipment.CloseModal(); _itemsMode = false; ApplyVisibility(); BindCards(); }
    public void CycleItemsLayout() { _equipment.CycleLayout(); }
    public void EquipmentRebuilt()
    {
        if (_itemsMode && _index.LayoutText != null) _index.LayoutText.text = _equipment.LayoutLabel;
        _index.ConfigureNavigation();
    }
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
        _slots.gameObject.SetActive(starters && !_itemsMode);
        _itemsTab.gameObject.SetActive(!host);
        _equipment.Root.gameObject.SetActive(_itemsMode || _equipment.HasModal);
        _index.ContentArea.gameObject.SetActive(!_itemsMode);
        _index.PreviousButton.gameObject.SetActive(!_itemsMode); _index.NextButton.gameObject.SetActive(!_itemsMode);
        _index.PageText.gameObject.SetActive(!_itemsMode); _index.LayoutButton.gameObject.SetActive(true);
        _index.CoinsText.gameObject.SetActive(!_itemsMode && !host); _index.EssenceText.gameObject.SetActive(!_itemsMode && !host);
        CoopUIElements.Stretch(_index.CoinsText.rectTransform, new Vector2(.305f, .866f), new Vector2(.78f, .976f));
        CoopUIElements.Stretch(_index.EssenceText.rectTransform, new Vector2(.305f, .79f), new Vector2(.89f, .866f));
        _index.CoinsText.alignment = _index.EssenceText.alignment = TMPro.TextAlignmentOptions.Left;
        _index.CoinsText.enableAutoSizing = _index.EssenceText.enableAutoSizing = true;
        _index.CoinsText.fontSizeMin = _index.EssenceText.fontSizeMin = 14;
        var backRect = _index.transform.Find("BackButton") as RectTransform;
        if (backRect != null) CoopUIElements.Stretch(backRect, new Vector2(.012f, .856f), new Vector2(.282f, .976f));
        int[] order = { 1, 0, 2, 3 };
        for (int i = 0; i < 4; i++)
        {
            var rect = (RectTransform)_index.PackButtons[order[i]].transform;
            CoopUIElements.Stretch(rect, new Vector2(.012f, .717f - i * .137f), new Vector2(.282f, .827f - i * .137f));
        }
        CoopUIElements.Stretch((RectTransform)_itemsTab.transform, new Vector2(.012f, .169f), new Vector2(.282f, .279f));
        CoopUIElements.Stretch((RectTransform)_index.LayoutButton.transform, new Vector2(.012f, .033f), new Vector2(.282f, .143f));
        float right = starters ? .895f : .978f;
        CoopUIElements.Stretch((RectTransform)_index.ContentArea, new Vector2(.296f, .175f), new Vector2(right, .756f));
        float middle = (.296f + right) * .5f;
        CoopUIElements.Stretch((RectTransform)_index.PreviousButton.transform, new Vector2(middle - .105f, .035f), new Vector2(middle - .04f, .143f));
        CoopUIElements.Stretch((RectTransform)_index.NextButton.transform, new Vector2(middle + .04f, .035f), new Vector2(middle + .105f, .143f));
        CoopUIElements.Stretch(_index.PageText.rectTransform, new Vector2(middle - .04f, .035f), new Vector2(middle + .04f, .143f));
        CoopUIElements.Stretch(_equipment.Root, new Vector2(.305f, .015f), new Vector2(.975f, .98f));
        _equipment.Root.SetAsLastSibling();
        _index.RefreshLayout();
        _index.ConfigureNavigation();
        foreach (var button in _index.PackButtons)
        {
            bool selected = !_itemsMode && button == _index.PackButtons[(int)_index.SelectedPack];
            var colors = button.colors; colors.normalColor = colors.selectedColor = selected ? new Color(1, .82f, .28f) : Color.white; button.colors = colors;
            var outline = button.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null) outline.enabled = selected;
        }
        var itemColors = _itemsTab.colors; itemColors.normalColor = itemColors.selectedColor = _itemsMode ? new Color(1, .82f, .28f) : Color.white; _itemsTab.colors = itemColors;
        var itemsOutline = _itemsTab.GetComponent<UnityEngine.UI.Outline>(); if (itemsOutline != null) itemsOutline.enabled = _itemsMode;
        if (_itemsMode && _index.LayoutText != null) _index.LayoutText.text = _equipment.LayoutLabel;
    }
    public void BindCards()
    {
        if (_equipment == null || _index.HostFilterMode) return;
        foreach (var display in _index.ContentArea.GetComponentsInChildren<CardDisplay>())
            if (display.AssignedCard != null) _equipment.BindCollectionCard(display, display.AssignedCard);
    }
    void OnDestroy() { if (_shop != null) _shop.OnCollectionChanged -= Refresh; }
}
