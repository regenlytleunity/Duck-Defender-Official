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
    bool _modalOnly;
    UnityEngine.UI.Button[] _controls = new UnityEngine.UI.Button[0];
    readonly List<CardDefinition> _cards = new List<CardDefinition>();
    public LoadoutPanel(Transform parent, int seat, bool lobby, Action close, object context = null)
    {
        Seat = seat; _lobby = lobby; _shop = ShopManager.Instance; _close = close; _context = context ?? this;
        Root = CoopUIElements.Panel(parent, "Equipment", Vector2.zero, Vector2.one, new Color(.035f, .055f, .09f));
        Rebuild();
    }
    public void ShowCards(bool cards) { CardsMode = cards; _page = 0; Rebuild(); }
    public void Rebuild()
    {
        CloseModal();
        if (_body != null) { _body.gameObject.SetActive(false); UnityEngine.Object.Destroy(_body.gameObject); }
        _body = CoopUIElements.Panel(Root, "Equipment content", Vector2.zero, Vector2.one, Color.clear);
        if (_lobby)
        {
            CoopUIElements.Button(_body, "Items", new Vector2(.02f, .92f), new Vector2(.26f, .99f), () => ShowCards(false));
            CoopUIElements.Button(_body, "Starting cards", new Vector2(.28f, .92f), new Vector2(.73f, .99f), () => ShowCards(true));
            CoopUIElements.Button(_body, "Done", new Vector2(.76f, .92f), new Vector2(.98f, .99f), () => _close?.Invoke());
        }
        int slots = CardsMode ? _shop.Progress.StartingCardSlots : 4;
        if (CardsMode && slots == 0) CoopUIElements.Text(_body, "Unlock starting card slots in the shop.", new Vector2(.02f, .79f), new Vector2(.98f, .9f), 24);
        for (int i = 0; i < slots; i++)
        {
            int slot = i; float width = CardsMode ? .19f : .24f;
            var min = new Vector2(.015f + i * width, .72f); var max = new Vector2(.005f + (i + 1) * width, .90f);
            if (CardsMode) MiniCard(_body, _shop.Loadout(Seat).Cards[i], min, max, slot, true);
            else ItemButton(_body, _shop.Loadout(Seat).Items[i], min, max, slot);
        }
        _message = CoopUIElements.Text(_body, CardsMode ? "Choose a card, then a starting slot." : "Choose an item to equip or sell.  1 / 2 / 3 / 4", new Vector2(.02f, .63f), new Vector2(.98f, .70f), 24);
        if (CardsMode)
        {
            _cards.Clear();
            _cards.AddRange(_shop.AllCards.Where(c => c != null && !c.IsBasic && _shop.GetCardData(c.ID)?.IsUnlocked == true && (Seat < 0 || !LocalCoopSession.DisabledCards.Contains(c.ID))).OrderBy(c => c.PackCategory).ThenBy(c => c.CardName));
            _page = Mathf.Clamp(_page, 0, Mathf.Max(0, (_cards.Count - 1) / 8));
            for (int i = 0; i < 8 && _page * 8 + i < _cards.Count; i++)
            {
                float x = i % 4 * .25f, y = .34f - i / 4 * .25f;
                MiniCard(_body, _cards[_page * 8 + i].ID, new Vector2(x + .01f, y), new Vector2(x + .24f, y + .24f), -1, false);
            }
            CoopUIElements.Button(_body, "<", new Vector2(.25f, .005f), new Vector2(.37f, .075f), () => { _page--; Rebuild(); }).interactable = _page > 0;
            CoopUIElements.Text(_body, (_page + 1) + " / " + Mathf.Max(1, Mathf.CeilToInt(_cards.Count / 8f)), new Vector2(.39f, .005f), new Vector2(.61f, .075f), 24);
            CoopUIElements.Button(_body, ">", new Vector2(.63f, .005f), new Vector2(.75f, .075f), () => { _page++; Rebuild(); }).interactable = (_page + 1) * 8 < _cards.Count;
        }
        else
        {
            for (int id = 1; id <= ItemCatalog.Count; id++)
            {
                int index = id - 1; float x = index % 4 * .25f, y = .34f - index / 4 * .28f;
                ItemButton(_body, id, new Vector2(x + .01f, y), new Vector2(x + .24f, y + .26f), -1);
            }
        }
        RefreshControls();
    }
    void ItemButton(Transform parent, int id, Vector2 min, Vector2 max, int slot)
    {
        var button = CoopUIElements.Button(parent, "", min, max, () => OpenItem(id, slot));
        var text = button.GetComponentInChildren<TMP_Text>(); text.fontSizeMax = 21; text.fontSizeMin = 12;
        text.text = slot >= 0 ? "Slot " + (slot + 1) + (id == 0 ? "\nEmpty" : "\n" + ItemCatalog.Names[id]) : ItemCatalog.Names[id] + "\nOwned: " + _shop.Progress.ItemInventory[id] + "  Free: " + Mathf.Max(0, _shop.Progress.ItemInventory[id] - _shop.ReservedItems(id, Seat));
        CoopUIElements.Stretch(text.rectTransform, new Vector2(.02f, 0), new Vector2(.98f, .38f));
        var icon = CoopUIElements.Panel(button.transform, "Icon", new Vector2(.24f, .39f), new Vector2(.76f, .96f), Color.white).GetComponent<UnityEngine.UI.Image>();
        icon.sprite = ItemCatalog.Icon(id); icon.enabled = icon.sprite != null; icon.preserveAspect = true; icon.raycastTarget = false;
        BindDrag(button.gameObject, false, slot, icon.sprite, () => OpenItem(id, slot), destination => slot >= 0 ? _shop.MoveEquipment(false, slot, destination, Seat) : _shop.EquipItem(id, destination, Seat), () => _shop.EquipItem(0, slot, Seat));
    }
    public UnityEngine.UI.Button MiniCard(Transform parent, string id, Vector2 min, Vector2 max, int slot, bool equipped)
    {
        var definition = _shop.AllCards.Find(c => c != null && c.ID == id);
        var saved = definition != null ? _shop.GetCardData(id) : null;
        var button = CoopUIElements.Button(parent, "", min, max, () => OpenCard(id, slot));
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
        OpenModal(card.CardName + "\nChoose a starting slot");
        if (slot >= 0) ModalButton("Unequip", () => Change(() => _shop.EquipStartingCard(null, slot, Seat)));
        else for (int i = 0; i < _shop.Progress.StartingCardSlots; i++) { int target = i; ModalButton("Equip to slot " + (i + 1), () => Change(() => _shop.EquipStartingCard(id, target, Seat))); }
        if (_shop.Progress.StartingCardSlots == 0) _modal.GetChild(0).GetComponent<TMP_Text>().text = "Unlock starting card slots in the shop.";
        FinishModal();
    }
    int _modalButtons;
    void OpenModal(string title)
    {
        CloseModal(); _modalButtons = 0;
        _modalOnly = !Root.gameObject.activeSelf;
        if (_modalOnly) { Root.gameObject.SetActive(true); _body.gameObject.SetActive(false); }
        _modal = CoopUIElements.Panel(Root, "Equip menu", new Vector2(.04f, .04f), new Vector2(.96f, .96f), new Color(.03f, .045f, .07f));
        _modal.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
        var group = _body.GetComponent<CanvasGroup>();
        if (group == null) group = _body.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false; group.blocksRaycasts = false;
        CoopUIElements.Text(_modal, title, new Vector2(.05f, .74f), new Vector2(.95f, .97f), 26);
    }
    void ModalButton(string name, Action action)
    {
        float top = .72f - _modalButtons++ * .105f;
        CoopUIElements.Button(_modal, name, new Vector2(.1f, top - .09f), new Vector2(.9f, top), () => action());
    }
    void FinishModal() { ModalButton("Cancel", CloseModal); RefreshControls(); MainMenuUI.FocusFirstControl(_modal.gameObject); }
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
        if (_modal != null) { _modal.gameObject.SetActive(false); UnityEngine.Object.Destroy(_modal.gameObject); _modal = null; }
        if (_body != null && _body.TryGetComponent<CanvasGroup>(out var group)) { group.interactable = true; group.blocksRaycasts = true; }
        if (_modalOnly) { Root.gameObject.SetActive(false); _body.gameObject.SetActive(true); _modalOnly = false; }
        RefreshControls();
    }
    void RefreshControls()
    {
        if (Root == null) return;
        _controls = NavigationScope.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.IsInteractable()).ToArray();
        if (_lobby) foreach (var control in _controls) control.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
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
            Vector2 origin = _controls[_focus].transform.position; float best = float.PositiveInfinity; int next = _focus;
            for (int i = 0; i < _controls.Length; i++)
            {
                Vector2 delta = (Vector2)_controls[i].transform.position - origin; float along = Vector2.Dot(delta, direction);
                if (along <= 1) continue;
                float score = delta.sqrMagnitude + Mathf.Abs(delta.x * direction.y - delta.y * direction.x) * 1000;
                if (score < best) { best = score; next = i; }
            }
            _focus = next;
        }
        if (confirm) _controls[_focus].onClick.Invoke();
        if (_controls.Length > 0) Focused = (RectTransform)_controls[Mathf.Clamp(_focus, 0, _controls.Length - 1)].transform;
    }
}
