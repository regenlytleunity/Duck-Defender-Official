using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// A shared equipment view used inside the index and each player's lobby pane.
public sealed class LoadoutPanel
{
    public readonly RectTransform Root;
    public GameObject NavigationScope => _modal != null ? _modal.gameObject : Root.gameObject;
    public RectTransform Focused { get; private set; }
    public int Seat { get; }
    public bool CardsMode { get; private set; }
    public bool HasModal => _modal != null;
    readonly ShopManager _shop;
    readonly bool _lobby;
    readonly Action _close;
    readonly object _context;
    RectTransform _body, _modal;
    TMP_Text _message;
    int _page, _focus;
    int _layoutIndex = 1;
    int Columns => _layoutIndex == 0 ? 3 : _layoutIndex == 1 ? (CardsMode ? 3 : 4) : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(EntryCount * 1.6f)));
    int Rows => _layoutIndex == 0 ? 1 : _layoutIndex == 1 ? 2 : Mathf.Max(1, Mathf.CeilToInt(EntryCount / (float)Columns));
    int EntryCount => CardsMode ? _cards.Count : ItemCatalog.Count;
    int PerPage => Columns * Rows;
    public string LayoutLabel => _layoutIndex == 2 ? "LAYOUT: ALL" : "LAYOUT: " + Columns + " x " + Rows;
    UnityEngine.UI.Button _returnFocus;
    string _returnFocusName;
    Vector3 _returnFocusPosition;
    CanvasGroup _indexGroup;
    bool _modalOnly;
    UnityEngine.UI.Button[] _controls = new UnityEngine.UI.Button[0];
    readonly List<CardDefinition> _cards = new List<CardDefinition>();
    readonly Dictionary<int, TMP_Text> _itemAvailability = new Dictionary<int, TMP_Text>();
    public LoadoutPanel(Transform parent, int seat, bool lobby, Action close, object context = null)
    {
        Seat = seat; _lobby = lobby; _shop = ShopManager.Instance; _close = close; _context = context ?? this;
        Root = CoopUIElements.Panel(parent, "Equipment", Vector2.zero, Vector2.one, new Color(.035f, .055f, .09f));
        Rebuild();
    }
    public void ShowCards(bool cards) { CardsMode = cards; _page = 0; Rebuild(); }
    public void CycleLayout()
    {
        int first = _page * PerPage;
        _layoutIndex = (_layoutIndex + 1) % 3;
        _page = first / PerPage;
        Rebuild();
    }
    public void Rebuild()
    {
        string focusedName = Focused != null ? Focused.name : null;
        var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        bool restoreFocus = !_lobby && selected != null && selected.transform.IsChildOf(Root);
        if (restoreFocus) focusedName = selected.name;
        if (_modal != null) focusedName = _returnFocusName;
        CloseModal();
        _itemAvailability.Clear();
        if (_body != null) { _body.gameObject.SetActive(false); UnityEngine.Object.Destroy(_body.gameObject); }
        _body = CoopUIElements.Panel(Root, "Equipment content", Vector2.zero, Vector2.one, Color.clear);
        _body.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        if (_lobby)
        {
            CoopUIElements.Button(_body, "Back", new Vector2(.012f, .856f), new Vector2(.282f, .976f), () => _close?.Invoke());
            var cardsTab = CoopUIElements.Button(_body, "Cards", new Vector2(.012f, .51f), new Vector2(.282f, .62f), () => ShowCards(true));
            var itemsTab = CoopUIElements.Button(_body, "Items", new Vector2(.012f, .37f), new Vector2(.282f, .48f), () => ShowCards(false));
            (CardsMode ? cardsTab : itemsTab).image.color = new Color(.35f, .4f, .25f);
            var layout = CoopUIElements.Button(_body, LayoutLabel, new Vector2(.012f, .035f), new Vector2(.282f, .145f), CycleLayout);
            layout.name = "Layout";
        }
        int slots = CardsMode ? _shop.Progress.StartingCardSlots : 4;
        for (int i = 0; i < slots; i++)
        {
            if (CardsMode) MiniCard(_body, _shop.Loadout(Seat).Cards[i], new Vector2(.91f, .81f - i * .197f), new Vector2(.987f, 1 - i * .197f), i, true);
            else
            {
                float left = _lobby ? .385f : .115f, width = _lobby ? .127f : .197f;
                ItemButton(_body, _shop.Loadout(Seat).Items[i], new Vector2(left + i * width, .765f), new Vector2(left + (i + 1) * width - .009f, .976f), i);
            }
        }
        float gridLeft = _lobby ? .31f : .005f, gridRight = CardsMode && slots > 0 ? .895f : .985f;
        _message = CoopUIElements.Text(_body, CardsMode && slots == 0 ? "Unlock starting card slots in the shop." : "", new Vector2(gridLeft, .145f), new Vector2(gridRight, .18f), _lobby ? 18 : 24);
        if (CardsMode)
        {
            _cards.Clear();
            _cards.AddRange(_shop.AllCards.Where(c => c != null && !c.IsBasic && _shop.GetCardData(c.ID)?.IsUnlocked == true && (Seat < 0 || !LocalCoopSession.DisabledCards.Contains(c.ID))).OrderBy(c => c.PackCategory).ThenBy(c => c.CardName));
        }
        _page = Mathf.Clamp(_page, 0, Mathf.Max(0, (EntryCount - 1) / PerPage));
        float top = CardsMode ? .96f : .735f, height = top - .18f;
        for (int i = 0; i < PerPage && _page * PerPage + i < EntryCount; i++)
        {
            float width = (gridRight - gridLeft) / Columns;
            var min = new Vector2(gridLeft + i % Columns * width + .005f, top - (i / Columns + 1) * height / Rows + .005f);
            var max = new Vector2(gridLeft + (i % Columns + 1) * width - .005f, top - i / Columns * height / Rows - .005f);
            if (CardsMode) MiniCard(_body, _cards[_page * PerPage + i].ID, min, max, -1, false);
            else ItemButton(_body, _page * PerPage + i + 1, min, max, -1);
        }
        float middle = (gridLeft + gridRight) * .5f;
        CoopUIElements.Button(_body, "<", new Vector2(middle - .14f, .02f), new Vector2(middle - .055f, .13f), () => { _page--; Rebuild(); }).interactable = _page > 0;
        CoopUIElements.Text(_body, (_page + 1) + " / " + Mathf.Max(1, Mathf.CeilToInt(EntryCount / (float)PerPage)), new Vector2(middle - .055f, .02f), new Vector2(middle + .055f, .13f), _lobby ? 20 : 28);
        CoopUIElements.Button(_body, ">", new Vector2(middle + .055f, .02f), new Vector2(middle + .14f, .13f), () => { _page++; Rebuild(); }).interactable = (_page + 1) * PerPage < EntryCount;
        RefreshControls();
        RestoreFocus(focusedName, restoreFocus && Root.gameObject.activeInHierarchy);
        (_context as IndexLoadoutUI)?.EquipmentRebuilt();
    }
    void ItemButton(Transform parent, int id, Vector2 min, Vector2 max, int slot)
    {
        var button = CoopUIElements.Button(parent, "", min, max, () => OpenItem(id, slot));
        button.name = slot >= 0 ? "Item slot " + (slot + 1) : "Item " + id;
        var text = button.GetComponentInChildren<TMP_Text>(); text.fontSizeMax = 21; text.fontSizeMin = 12;
        text.text = slot >= 0 ? "Slot " + (slot + 1) + (id == 0 ? "\nEmpty" : "\n" + ItemCatalog.Names[id]) : ItemCatalog.Names[id] + "\nOwned: " + _shop.Progress.ItemInventory[id] + "\nAvailable: " + Mathf.Max(0, _shop.Progress.ItemInventory[id] - _shop.ReservedItems(id, Seat));
        if (slot < 0) _itemAvailability[id] = text;
        CoopUIElements.Stretch(text.rectTransform, new Vector2(.02f, 0), new Vector2(.98f, .38f));
        var icon = CoopUIElements.Panel(button.transform, "Icon", new Vector2(.24f, .39f), new Vector2(.76f, .96f), Color.white).GetComponent<UnityEngine.UI.Image>();
        icon.sprite = ItemCatalog.Icon(id); icon.enabled = icon.sprite != null; icon.preserveAspect = true; icon.raycastTarget = false;
        BindDrag(button.gameObject, false, slot, icon.sprite, () => OpenItem(id, slot), destination => slot >= 0 ? _shop.MoveEquipment(false, slot, destination, Seat) : _shop.EquipItem(id, destination, Seat), () => _shop.EquipItem(0, slot, Seat));
    }
    public void RefreshItemAvailability()
    {
        foreach (var pair in _itemAvailability)
            if (pair.Value != null) pair.Value.text = ItemCatalog.Names[pair.Key] + "\nOwned: " + _shop.Progress.ItemInventory[pair.Key] + "\nAvailable: " + Mathf.Max(0, _shop.Progress.ItemInventory[pair.Key] - _shop.ReservedItems(pair.Key, Seat));
    }
    public UnityEngine.UI.Button MiniCard(Transform parent, string id, Vector2 min, Vector2 max, int slot, bool equipped)
    {
        var definition = _shop.AllCards.Find(c => c != null && c.ID == id);
        var saved = definition != null ? _shop.GetCardData(id) : null;
        var button = CoopUIElements.Button(parent, "", min, max, () => OpenCard(id, slot));
        button.name = equipped ? "Starting slot " + (slot + 1) : "Card " + id;
        // The fitting child retains the authored 5:7 card ratio at every pane size.
        button.image.color = Color.clear;
        var face = CoopUIElements.Panel(button.transform, "Card", Vector2.zero, Vector2.one, new Color(.16f, .23f, .32f));
        var template = MainMenuUI.Instance.CardDisplayPrefab.GetComponent<CardDisplay>();
        if (template != null && template.BackgroundImage != null)
        {
            var background = face.GetComponent<UnityEngine.UI.Image>(); background.sprite = template.BackgroundImage.sprite; background.type = template.BackgroundImage.type;
            background.color = definition == null ? new Color(.4f, .4f, .4f) : definition.Rarity == CardRarity.Legendary ? new Color(1, .8f, .2f) : definition.Rarity == CardRarity.Rare ? new Color(.3f, .5f, 1) : new Color(.6f, .6f, .6f);
        }
        var fitter = face.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>(); fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 5f / 7f;
        AscendedCardTint.Apply(face.GetComponent<UnityEngine.UI.Image>(), saved?.IsAscended == true);
        face.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var text = button.GetComponentInChildren<TMP_Text>(); text.transform.SetParent(face, false);
        CoopUIElements.Stretch(text.rectTransform, new Vector2(.04f, .03f), new Vector2(.96f, .4f)); text.fontSizeMax = _lobby ? 20 : 24; text.fontSizeMin = 10;
        text.text = definition != null ? (saved?.IsAscended == true ? definition.AscendedName : definition.CardName) + "\nLv " + (saved?.Level ?? 1) : "Slot " + (slot + 1) + "\nEmpty";
        var art = CoopUIElements.Panel(face, "Art", new Vector2(.1f, .43f), new Vector2(.9f, .96f), Color.white).GetComponent<UnityEngine.UI.Image>();
        art.sprite = saved?.IsAscended == true && definition.AscendedIcon != null ? definition.AscendedIcon : definition != null ? definition.Icon : null;
        art.enabled = art.sprite != null; art.preserveAspect = true; art.raycastTarget = false;
        if (!art.enabled) CoopUIElements.Stretch(text.rectTransform, new Vector2(.06f, .08f), new Vector2(.94f, .92f));
        BindDrag(button.gameObject, true, slot, art.sprite, () => OpenCard(id, slot), destination => slot >= 0 ? _shop.MoveEquipment(true, slot, destination, Seat) : _shop.EquipStartingCard(id, destination, Seat), () => _shop.EquipStartingCard(null, slot, Seat));
        return button;
    }
    public void BindCollectionCard(CardDisplay display, CardDefinition definition)
    {
        if (_shop.GetCardData(definition.ID)?.IsUnlocked != true) return;
        var hook = display.GetComponent<LoadoutInteraction>();
        if (hook == null) hook = display.gameObject.AddComponent<LoadoutInteraction>();
        ConfigureHook(hook, true, -1, definition.Icon, () => OpenCard(definition.ID, -1), destination => _shop.EquipStartingCard(definition.ID, destination, Seat), null);
        display.ClickButton.onClick.RemoveAllListeners(); display.ClickButton.onClick.AddListener(() => OpenCard(definition.ID, -1));
    }
    void BindDrag(GameObject target, bool card, int slot, Sprite icon, Action menu, Func<int, bool> equip, Func<bool> remove)
    { ConfigureHook(target.AddComponent<LoadoutInteraction>(), card, slot, icon, menu, equip, remove); }
    void ConfigureHook(LoadoutInteraction hook, bool card, int slot, Sprite icon, Action menu, Func<int, bool> equip, Func<bool> remove)
    {
        hook.Context = _context; hook.IsCard = card; hook.Slot = slot; hook.Icon = icon; hook.ContextMenu = menu;
        hook.Equip = destination => { bool result = equip(destination); Rebuild(); return result; };
        hook.Remove = () => { remove?.Invoke(); Rebuild(); };
    }
    public void OpenItem(int id, int slot = -1)
    {
        if (!ItemCatalog.Valid(id)) return;
        OpenModal(ItemCatalog.Names[id] + "\n" + ItemCatalog.Descriptions[id]);
        if (slot >= 0) ModalButton("Unequip", () => Change(() => _shop.EquipItem(0, slot, Seat)));
        else for (int i = 0; i < 4; i++) { int target = i; ModalButton("Equip to slot " + (i + 1), () => Change(() => _shop.EquipItem(id, target, Seat))); }
        ModalButton("Sell - " + ItemCatalog.Prices[id] / 2 + " Coins", () => Change(() => _shop.SellItem(id, Seat, slot)));
        FinishModal();
    }
    public void OpenCard(string id, int slot = -1)
    {
        if (string.IsNullOrEmpty(id)) return;
        var card = _shop.AllCards.Find(c => c != null && c.ID == id); if (card == null) return;
        OpenModal(card.CardName + (slot >= 0 ? "\nStarting card" : "\nChoose an action"));
        if (!_lobby && slot < 0)
        {
            var saved = _shop.GetCardData(id);
            if (saved != null && saved.Level < card.MaxLevel)
                ModalButton("Upgrade - " + card.GetUpgradeCost(saved.Level) + " Coins", () => Change(() => _shop.TryUpgradeCard(id))).interactable =
                    saved.IsUnlocked && (_shop.InfiniteCopies || saved.Duplicates >= card.GetCardsRequired(saved.Level)) && _shop.CanAfford(card.GetUpgradeCost(saved.Level));
            else if (saved != null && !saved.IsAscended && card.Ascension != CardAscension.None)
                ModalButton("Ascend - " + card.AscensionCost + " Essence", () => Change(() => _shop.TryAscendCard(id))).interactable =
                    _shop.InfiniteResources || _shop.GetEssence(card.PackCategory) >= card.AscensionCost;
        }
        if (slot >= 0) ModalButton("Unequip", () => Change(() => _shop.EquipStartingCard(null, slot, Seat)));
        else for (int i = 0; i < _shop.Progress.StartingCardSlots; i++) { int target = i; ModalButton("Equip to slot " + (i + 1), () => Change(() => _shop.EquipStartingCard(id, target, Seat))); }
        if (_shop.Progress.StartingCardSlots == 0) _modal.GetChild(0).GetComponent<TMP_Text>().text = card.CardName + "\nStarting slots unlock in the shop.";
        FinishModal();
    }
    int _modalButtons;
    void OpenModal(string title)
    {
        CloseModal(); _modalButtons = 0;
        _returnFocus = _lobby ? Focused != null ? Focused.GetComponent<UnityEngine.UI.Button>() : null : UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<UnityEngine.UI.Button>();
        _returnFocusName = _returnFocus != null ? _returnFocus.name : null;
        if (_returnFocus != null) _returnFocusPosition = _returnFocus.transform.position;
        _modalOnly = !Root.gameObject.activeSelf;
        if (_modalOnly) { Root.gameObject.SetActive(true); _body.gameObject.SetActive(false); }
        _modal = CoopUIElements.Panel(Root, "Equip menu", new Vector2(.04f, .04f), new Vector2(.96f, .96f), new Color(.03f, .045f, .07f));
        _modal.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
        if (_context is IndexLoadoutUI index)
        {
            _indexGroup = index.GetComponent<CanvasGroup>();
            if (_indexGroup == null) _indexGroup = index.gameObject.AddComponent<CanvasGroup>();
            _indexGroup.interactable = false; _indexGroup.blocksRaycasts = false;
        }
        var group = _body.GetComponent<CanvasGroup>();
        if (group == null) group = _body.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false; group.blocksRaycasts = false;
        CoopUIElements.Text(_modal, title, new Vector2(.05f, .74f), new Vector2(.95f, .97f), 26);
    }
    UnityEngine.UI.Button ModalButton(string name, Action action)
    {
        float top = .72f - _modalButtons++ * .09f;
        return CoopUIElements.Button(_modal, name, new Vector2(.1f, top - .077f), new Vector2(.9f, top), () => action());
    }
    void FinishModal() { ModalButton("Cancel", CloseModal); _focus = 0; RefreshControls(); if (!_lobby) MainMenuUI.FocusFirstControl(_modal.gameObject); }
    void Change(Func<bool> action)
    {
        bool result = action(); Rebuild();
        if (!result) _message.text = "Unavailable: check ownership, reserved copies or duplicate cards.";
    }
    public bool HandleBack()
    {
        if (_modal == null) return false;
        CloseModal(); return true;
    }
    public void CloseModal()
    {
        bool hadModal = _modal != null;
        if (_modal != null) { _modal.gameObject.SetActive(false); UnityEngine.Object.Destroy(_modal.gameObject); _modal = null; }
        if (_indexGroup != null) { _indexGroup.interactable = true; _indexGroup.blocksRaycasts = true; _indexGroup = null; }
        if (_body != null && _body.TryGetComponent<CanvasGroup>(out var group)) { group.interactable = true; group.blocksRaycasts = true; }
        if (_modalOnly) { Root.gameObject.SetActive(false); _body.gameObject.SetActive(true); _modalOnly = false; }
        RefreshControls();
        if (hadModal)
        {
            RestoreFocus(_returnFocusName, false);
            if (!_lobby)
            {
                var target = _returnFocus;
                if (target == null || !target.IsActive() || !target.IsInteractable())
                {
                    // Starting slots live outside Root and are replaced by a
                    // collection refresh. Recover the same slot by its stable name.
                    var scope = (_context as IndexLoadoutUI)?.gameObject ?? Root.gameObject;
                    var controls = scope.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.IsActive() && b.IsInteractable()).ToArray();
                    target = controls.FirstOrDefault(b => b.name == _returnFocusName) ?? controls.OrderBy(b => (b.transform.position - _returnFocusPosition).sqrMagnitude).FirstOrDefault();
                }
                if (target != null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(target.gameObject);
            }
            (_context as IndexLoadoutUI)?.EquipmentRebuilt();
        }
    }
    void RestoreFocus(string name, bool select)
    {
        int found = Array.FindIndex(_controls, b => b.name == name);
        if (found >= 0) _focus = found;
        _focus = Mathf.Clamp(_focus, 0, Mathf.Max(0, _controls.Length - 1));
        Focused = _controls.Length > 0 ? (RectTransform)_controls[_focus].transform : null;
        if (select && Focused != null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(Focused.gameObject);
    }
    void RefreshControls()
    {
        if (Root == null) return;
        _controls = NavigationScope.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.IsActive() && b.IsInteractable()).ToArray();
        ControllerMenuNavigation.ConfigureNavigation(NavigationScope);
        _focus = Mathf.Clamp(_focus, 0, Mathf.Max(0, _controls.Length - 1));
        Focused = _controls.Length > 0 ? (RectTransform)_controls[_focus].transform : null;
    }
    public void ControllerInput(Vector2 move, bool confirm, bool back)
    {
        if (back) { if (!HandleBack()) _close?.Invoke(); return; }
        if (_controls.Length == 0) return;
        if (move.sqrMagnitude > .2f)
        {
            Vector2 direction = Mathf.Abs(move.x) > Mathf.Abs(move.y) ? new Vector2(Mathf.Sign(move.x), 0) : new Vector2(0, Mathf.Sign(move.y));
            var next = ControllerMenuNavigation.FindInDirection(_controls[_focus], _controls, direction);
            if (next != null) _focus = Array.IndexOf(_controls, next);
        }
        if (confirm) _controls[_focus].onClick.Invoke();
        if (_controls.Length > 0) Focused = (RectTransform)_controls[Mathf.Clamp(_focus, 0, _controls.Length - 1)].transform;
    }
}
