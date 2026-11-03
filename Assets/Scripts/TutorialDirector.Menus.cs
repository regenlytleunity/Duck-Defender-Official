using UnityEngine;

public partial class TutorialDirector
{
    bool _indexPackSelected;
    public static bool AllowPanel(GameObject panel)
    {
        if (!MenuLocked || Instance == null || Instance._menu == null) return true;
        var menu = Instance._menu;
        if (Progress.CoreActive)
        {
            if (panel == menu.MenuPanel) return !menu.OpeningOverlay.activeSelf &&
                (menu.MenuPanel.activeSelf || Progress.Stage == TutorialStage.ShopReturn || Progress.Stage == TutorialStage.IndexReturn);
            if (panel == menu.ShopPanel) return Progress.Stage == TutorialStage.Shop || Progress.Stage == TutorialStage.PackPurchased || Progress.Stage == TutorialStage.ShopReturn;
            if (panel == menu.IndexPanel) return Progress.Stage == TutorialStage.Index || Progress.Stage == TutorialStage.Upgrade || Progress.Stage == TutorialStage.IndexReturn;
            return false;
        }
        return panel == menu.MenuPanel && !menu.IndexPanel.activeSelf || panel == menu.IndexPanel;
    }
    public static bool AllowItemTab => !CoreActive && Progress != null && Progress.HasObtainedItem &&
        (!Progress.ItemActive || Progress.Items <= ItemTutorialStage.Equip);
    public static bool AllowCardMenu(string id) => !MenuLocked || CoreActive && Progress.Stage == TutorialStage.Upgrade && id == Progress.UpgradeCardID;
    public static bool AllowItemMenu(int id) => !MenuLocked || Progress != null && Progress.ItemActive && Progress.Items == ItemTutorialStage.Equip &&
        ItemCatalog.Valid(id) && ShopManager.Instance.Progress.ItemInventory[id] > 0;

    void UpdateMenu()
    {
        if (_transitioning || Progress == null) return;
        if (CoreActive)
        {
            switch (Progress.Stage)
            {
                case TutorialStage.Basics: StartGame(); return;
                case TutorialStage.Play: Prompt("Press Play to enter a game."); break;
                case TutorialStage.Shop:
                    Prompt(_menu.ShopPanel.activeSelf ? "Unlock new upgrades by purchasing card packs. Each pack gives 3 upgrades. Choose a pack to buy and open." : "Enter the shop."); break;
                case TutorialStage.PackPurchased: Prompt("Open your card pack. Swipe across it or press " + ControllerBindings.Label("Confirm") + "."); break;
                case TutorialStage.ShopReturn:
                    if (_menu.MenuPanel.activeSelf && !_menu.OpeningOverlay.activeSelf) SaveStage(TutorialStage.Index);
                    else if (_menu.OpeningOverlay.activeSelf)
                    {
                        if (_menu.TutorialPackRevealed) Prompt("These cards are now saved in your collection.", () => _menu.ShowPanel(_menu.ShopPanel));
                        else Prompt("Opening your pack...");
                    }
                    else Prompt("Return to the main menu.");
                    break;
                case TutorialStage.Index:
                case TutorialStage.Upgrade:
                    if (!_menu.IndexPanel.activeSelf) { Prompt("View and upgrade unlocked cards in the index. Open the index."); break; }
                    var index = _menu.IndexPanel.GetComponent<CardIndexUI>();
                    if (!_indexPackSelected) { _indexPackSelected = true; index.SelectPack((int)Progress.Pack); }
                    var saved = ShopManager.Instance.GetCardData(Progress.UpgradeCardID);
                    if (saved != null && saved.Level >= 2) { SaveStage(TutorialStage.IndexReturn); break; }
                    if (Progress.Stage == TutorialStage.Index)
                    {
                        Prompt("The index shows each pack's cards. Duplicate copies let you upgrade them. Find your highlighted card using the page arrows.", () => SaveStage(TutorialStage.Upgrade), false);
                        _ready = index.PageForCard(Progress.UpgradeCardID) == index.CurrentPage;
                    }
                    else Prompt("Select the highlighted card and choose Upgrade. Upgrading cards improves their stats.");
                    break;
                case TutorialStage.IndexReturn:
                    if (_menu.MenuPanel.activeSelf)
                    {
                        SaveStage(TutorialStage.Complete);
                        Prompt("TUTORIAL COMPLETE! Keep collecting coins and buying cards to reach higher waves!", HidePrompt);
                    }
                    else Prompt("Card upgraded! Return to the main menu.");
                    break;
            }
            return;
        }
        if (!Progress.ItemActive) return;
        if (!_menu.IndexPanel.activeSelf)
        {
            Prompt("You obtained an item! Items are powerful, one-time abilities. " + (_menu.ShopPanel.activeSelf ? "Return to the main menu, then open the index to equip it." : "Open the index to view and equip your item."));
            return;
        }
        var equipment = _menu.IndexPanel.GetComponent<IndexLoadoutUI>();
        if (Progress.Items == ItemTutorialStage.None || Progress.Items == ItemTutorialStage.Index) SaveItems(ItemTutorialStage.Tab);
        if (Progress.Items == ItemTutorialStage.Tab)
        {
            if (equipment.ItemsMode) SaveItems(ItemTutorialStage.Equip);
            else Prompt("Enter the Items menu using the highlighted Items tab.");
        }
        if (Progress.Items == ItemTutorialStage.Equip)
        {
            if (!equipment.ItemsMode) equipment.ShowItems();
            bool owned = false, equipped = false;
            var data = ShopManager.Instance.Progress;
            for (int i = 1; i < data.ItemInventory.Length; i++) owned |= data.ItemInventory[i] > 0;
            foreach (int id in data.SoloLoadout.Items) equipped |= ItemCatalog.Valid(id) && data.ItemInventory[id] > 0;
            // A legacy/stale save can have consumed its last item before this tutorial existed.
            if (!owned)
            {
                data.ItemInventory[(int)ItemKind.Bomb] = 1; SaveSystem.SaveData(data, updateTutorial: true); equipment.Refresh();
            }
            if (equipped) SaveItems(ItemTutorialStage.Controls);
            else Prompt("Equip an owned item to 1 of the 4 available slots. Select an item, then choose a slot. Selling is disabled during this lesson.");
        }
        if (Progress.Items == ItemTutorialStage.Controls)
        {
            bool pad = UnityEngine.InputSystem.Gamepad.all.Count > 0;
            string keys = "";
            foreach (string action in InputHelper.ItemActions) keys += (keys.Length > 0 ? ", " : "") + (pad ? ControllerBindings.Label(action) : InputManager.Instance.GetKeybind(action).ToString());
            string fire = pad ? ControllerBindings.Label("UseItem") : InputManager.Instance != null ? InputManager.Instance.UseItemKey.ToString() : "Mouse1";
            Prompt("During a run, select items with " + keys + ". Press " + fire + " to use the selected item. Throwable items use hold, aim, then release.", () =>
            {
                SaveItems(ItemTutorialStage.Complete);
                Prompt("TUTORIAL COMPLETE!", HidePrompt);
            });
        }
    }
    bool AllowedButton(UnityEngine.UI.Button button)
    {
        if (CompletionPrompt) return false;
        if (!MenuLocked) return true;
        if (CoreActive)
        {
            switch (Progress.Stage)
            {
                case TutorialStage.Play: return button.name == "PlayButton" && button.transform.IsChildOf(_menu.MenuPanel.transform);
                case TutorialStage.Shop:
                    if (_menu.MenuPanel.activeSelf) return button.name == "ShopButton";
                    if (_menu.ConfirmPanel.activeSelf) return button == _menu.YesButton || button.name == "NoButton";
                    return button.transform.IsChildOf(_menu.PackContainer);
                case TutorialStage.PackPurchased: return false;
                case TutorialStage.ShopReturn: return !_menu.OpeningOverlay.activeSelf && button.name == "BackButton";
                case TutorialStage.Index:
                case TutorialStage.Upgrade:
                    if (_menu.MenuPanel.activeSelf) return button.name == "IndexButton";
                    var index = _menu.IndexPanel.GetComponent<CardIndexUI>();
                    int page = index.PageForCard(Progress.UpgradeCardID);
                    if (page < 0) return button.name == "BackButton";
                    if (button == index.PreviousButton) return index.CurrentPage > page;
                    if (button == index.NextButton) return index.CurrentPage < page;
                    if (Progress.Stage == TutorialStage.Index) return false;
                    var card = button.GetComponentInParent<CardDisplay>();
                    return card != null && card.AssignedCard != null && card.AssignedCard.ID == Progress.UpgradeCardID ||
                        button.name.StartsWith("Upgrade - ") || button.name == "Cancel";
                case TutorialStage.IndexReturn: return button.name == "BackButton" || button.name == "Cancel";
                default: return false;
            }
        }
        if (_menu.ShopPanel.activeSelf) return button.name == "BackButton";
        if (_menu.MenuPanel.activeSelf) return button.name == "IndexButton";
        if (Progress.Items <= ItemTutorialStage.Tab) return button.name == "ItemsTab";
        if (Progress.Items == ItemTutorialStage.Equip)
        {
            if (button.name == "<" || button.name == ">" || button.name == "Cancel" || button.name.StartsWith("Equip to slot ")) return true;
            if (button.name.StartsWith("Item ") && int.TryParse(button.name.Substring(5), out int id)) return ItemCatalog.Valid(id) && ShopManager.Instance.Progress.ItemInventory[id] > 0;
        }
        return false;
    }
    void GateMenu()
    {
        // Separate CanvasGroups preserve each owner's affordability/disabled state.
        // Re-evaluate after callbacks rebuild pages, so pointer and controller see the same gate.
        UnityEngine.UI.Button first = null;
        bool changed = false;
        _menu.GetComponentsInChildren(true, _menuButtons);
        foreach (var button in _menuButtons)
        {
            bool allowed = AllowedButton(button);
            if (!_gates.TryGetValue(button, out var group) && !allowed)
            { group = button.gameObject.AddComponent<CanvasGroup>(); _gates.Add(button, group); }
            if (group != null) { changed |= group.interactable != allowed; group.interactable = allowed; }
            if (allowed && button.IsActive() && button.IsInteractable() && first == null) first = button;
        }
        if (!MenuLocked) { _highlight?.Hide(); return; }
        if (changed) ControllerMenuNavigation.ConfigureNavigation(_menu.NavigationScope);
        var events = UnityEngine.EventSystems.EventSystem.current;
        var selected = events != null ? events.currentSelectedGameObject : null;
        if (first != null && (selected == null || selected.GetComponent<UnityEngine.UI.Selectable>()?.IsInteractable() != true)) events?.SetSelectedGameObject(first.gameObject);
        RectTransform highlight = first != null ? (RectTransform)first.transform : null;
        if (_menu.IndexPanel.activeSelf && CoreActive && _menu.IndexPanel.GetComponent<IndexLoadoutUI>().NavigationScope == _menu.IndexPanel && (Progress.Stage == TutorialStage.Index || Progress.Stage == TutorialStage.Upgrade))
        {
            _menu.IndexPanel.GetComponent<CardIndexUI>().ContentArea.GetComponentsInChildren(_visibleCards);
            foreach (var card in _visibleCards)
                if (card.AssignedCard != null && card.AssignedCard.ID == Progress.UpgradeCardID && card.BackgroundImage != null) highlight = card.BackgroundImage.rectTransform;
        }
        if (highlight != null) _highlight?.Show(highlight); else _highlight?.Hide();
    }
}
