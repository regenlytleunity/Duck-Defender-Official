using UnityEngine;

public partial class TutorialDirector
{
    bool _indexPackSelected;
    public static bool AllowPanel(GameObject panel)
    {
        if (!MenuLocked || Instance == null || Instance._menu == null) return true;
        var menu = Instance._menu;
        if (CapturesContinue) return false;
        if (menu.OpeningOverlay.activeSelf)
            return Progress.Stage == TutorialStage.ShopReturn && panel == menu.ShopPanel && _continueConsumedFrame == Time.frameCount;
        if (Progress.CoreActive)
        {
            if (Progress.Stage == TutorialStage.Basics || Progress.Stage == TutorialStage.Waves) return panel == menu.MenuPanel;
            if (Progress.Stage == TutorialStage.PackPurchased) return panel == menu.ShopPanel;
            return panel == menu.MenuPanel || panel == menu.ShopPanel || panel == menu.IndexPanel;
        }
        return panel == menu.MenuPanel || panel == menu.IndexPanel;
    }
    public static bool AllowItemTab => !CoreActive && Progress != null && Progress.HasObtainedItem && !CapturesContinue;
    public static bool AllowCardMenu(string id) => !CapturesContinue && (!MenuLocked || CoreActive || Progress != null && Progress.ItemActive);
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
                case TutorialStage.Play: Prompt(_menu.MenuPanel.activeSelf ? "Press Play to enter a game. You can also browse your collection." : "Explore your collection, then return to the main menu and press Play."); break;
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
                        SaveStage(TutorialStage.Upgrade);
                        Prompt("Browse packs, pages and cards freely. Duplicate copies let you upgrade cards. Follow the green glow to upgrade your tutorial card.");
                    }
                    else Prompt("Browse packs, pages and cards freely. Follow the green glow to your tutorial card and choose Upgrade.");
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
        if (CapturesContinue) return false;
        if (!MenuLocked) return true;
        if (_menu.OpeningOverlay.activeSelf) return false;
        if (_menu.ConfirmPanel.activeSelf)
            return CoreActive && Progress.Stage == TutorialStage.Shop && (button == _menu.YesButton || button.name == "NoButton");
        if (button.name == "Cancel" || button.name == "BackButton") return true;
        if (_menu.MenuPanel.activeSelf)
            return button.name == "IndexButton" || CoreActive && (button.name == "ShopButton" || button.name == "PlayButton" && Progress.Stage == TutorialStage.Play);
        if (_menu.ShopPanel.activeSelf)
            return CoreActive && Progress.Stage == TutorialStage.Shop && button.transform.IsChildOf(_menu.PackContainer);
        if (_menu.IndexPanel.activeSelf)
        {
            var index = _menu.IndexPanel.GetComponent<CardIndexUI>();
            if (button == index.PreviousButton || button == index.NextButton || button == index.LayoutButton || System.Array.IndexOf(index.PackButtons, button) >= 0) return true;
            if (button.name == "ItemsTab") return AllowItemTab;
            if (button.GetComponentInParent<CardDisplay>() != null) return true;
            // The modal's affordability and tutorial checks disable unrelated upgrades.
            if (CoreActive) return Progress.Stage == TutorialStage.Upgrade && button.name.StartsWith("Upgrade - ");
            if (Progress.ItemActive && Progress.Items == ItemTutorialStage.Equip)
            {
                if (button.name == "<" || button.name == ">" || button.name.StartsWith("Equip to slot ")) return true;
                if (button.name.StartsWith("Item ") && int.TryParse(button.name.Substring(5), out int id))
                    return ItemCatalog.Valid(id) && ShopManager.Instance.Progress.ItemInventory[id] > 0;
            }
        }
        return false;
    }
    RectTransform GuidanceTarget(UnityEngine.UI.Button fallback)
    {
        var scope = _menu.NavigationScope;
        var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        if (_menu.MenuPanel.activeSelf)
        {
            string name = !CoreActive || Progress.Stage >= TutorialStage.Index ? "IndexButton" : Progress.Stage == TutorialStage.Play ? "PlayButton" : "ShopButton";
            return _menu.MenuPanel.transform.Find(name) as RectTransform;
        }
        if (_menu.ConfirmPanel.activeSelf) return _menu.YesButton.transform as RectTransform;
        if (_menu.ShopPanel.activeSelf && CoreActive && Progress.Stage == TutorialStage.Shop)
        {
            // Guidance follows the chosen pack; it must not leave a second focus border on pack one.
            if (selected != null && selected.transform.IsChildOf(_menu.PackContainer)) return selected.transform as RectTransform;
            foreach (var button in _menu.PackContainer.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.IsInteractable()) return button.transform as RectTransform;
        }
        if (_menu.IndexPanel.activeSelf && CoreActive && (Progress.Stage == TutorialStage.Index || Progress.Stage == TutorialStage.Upgrade))
        {
            var index = _menu.IndexPanel.GetComponent<CardIndexUI>();
            if (scope == _menu.IndexPanel)
            {
                if (index.SelectedPack != Progress.Pack) return index.PackButtons[(int)Progress.Pack].transform as RectTransform;
                int page = index.PageForCard(Progress.UpgradeCardID);
                if (index.CurrentPage != page) return (index.CurrentPage < page ? index.NextButton : index.PreviousButton).transform as RectTransform;
                index.ContentArea.GetComponentsInChildren(_visibleCards);
                foreach (var card in _visibleCards)
                    if (card.AssignedCard != null && card.AssignedCard.ID == Progress.UpgradeCardID && card.BackgroundImage != null) return card.BackgroundImage.rectTransform;
            }
        }
        if (_menu.IndexPanel.activeSelf && scope == _menu.IndexPanel && (!CoreActive || Progress.Stage != TutorialStage.Index && Progress.Stage != TutorialStage.Upgrade))
        {
            if (!CoreActive && Progress.ItemActive)
            {
                if (!_menu.IndexPanel.GetComponent<IndexLoadoutUI>().ItemsMode) return _menu.IndexPanel.transform.Find("ItemsTab") as RectTransform;
                foreach (var button in scope.GetComponentsInChildren<UnityEngine.UI.Button>())
                    if (button.name.StartsWith("Item ") && int.TryParse(button.name.Substring(5), out int id) && ShopManager.Instance.Progress.ItemInventory[id] > 0) return button.transform as RectTransform;
            }
            return _menu.IndexPanel.transform.Find("BackButton") as RectTransform;
        }
        return fallback != null ? fallback.transform as RectTransform : null;
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
        RectTransform highlight = CapturesContinue ? null : GuidanceTarget(first);
        if (highlight != null) _highlight?.Show(highlight); else _highlight?.Hide();
    }
}

// A green upward gradient marks the tutorial objective. Yellow borders remain controller focus.
// Lives with the tutorial owner and reuses its generated marker texture.
public sealed class TutorialGuidance
{
    UnityEngine.UI.Image _image;
    public static UnityEngine.UI.Image Attach(RectTransform target)
    {
        var existing = target.Find("Tutorial green underglow");
        if (existing != null) return existing.GetComponent<UnityEngine.UI.Image>();
        var go = new GameObject("Tutorial green underglow", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
        var rect = (RectTransform)go.transform; rect.SetParent(target, false);
        CoopUIElements.Stretch(rect, new Vector2(-.035f, -.06f), new Vector2(1.035f, .32f));
        go.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = TutorialDirector.GuidanceGradient;
        image.color = new Color(.25f, 1, .12f, .65f); image.raycastTarget = false;
        rect.SetAsFirstSibling();
        return image;
    }
    public void Show(RectTransform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) { Hide(); return; }
        if (_image == null) _image = Attach(target);
        else if (_image.transform.parent != target) _image.transform.SetParent(target, false);
        _image.gameObject.SetActive(true);
        _image.transform.SetAsFirstSibling();
    }
    public void Hide() { if (_image != null) _image.gameObject.SetActive(false); }
    public void Dispose() { if (_image != null) Object.Destroy(_image.gameObject); }
}
