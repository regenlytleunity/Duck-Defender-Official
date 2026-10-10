using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class CardIndexUI : MonoBehaviour
{
    [System.NonSerialized] public bool HostFilterMode;
    [Header("References")]
    public Transform ContentArea;
    public GameObject CategoryHeaderPrefab; // Retained for existing serialization.
    public GameObject CardGridPrefab;
    public GameObject CardDisplayPrefab;
    public UnityEngine.UI.Button[] PackButtons; // Munitions, Mobility, Survival, Gadget.
    public UnityEngine.UI.Button PreviousButton, NextButton, LayoutButton;
    public TextMeshProUGUI PageText, CoinsText, EssenceText, LayoutText;
    public CardPackType SelectedPack = CardPackType.Mobility;
    public int LayoutColumns => _layoutIndex == 2 ? Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(_cards.Count * 1.6f))) : _layoutIndex == 1 ? 4 : 3;
    public int LayoutRows => _layoutIndex == 2 ? Mathf.Max(1, Mathf.CeilToInt(_cards.Count / (float)LayoutColumns)) : _layoutIndex == 0 ? 1 : 2;
    public int CardsPerPage => LayoutColumns * LayoutRows;
    int _layoutIndex; // 3x1, 4x2, ALL (the selected category).
    public int CurrentPage { get; private set; }
    public int PageCount => Mathf.Max(1, Mathf.CeilToInt(_cards.Count / (float)CardsPerPage));
    readonly List<CardDefinition> _cards = new List<CardDefinition>();
    readonly List<RectTransform> _displays = new List<RectTransform>();
    ShopManager _shop;

    void OnEnable()
    {
        _shop = ShopManager.Instance;
        if (_shop != null) _shop.OnCollectionChanged += RefreshBalances;
        GenerateIndex();
    }
    void Start() { if (_shop == null && ShopManager.Instance != null) { _shop = ShopManager.Instance; _shop.OnCollectionChanged += RefreshBalances; GenerateIndex(); } }
    void OnDisable() { if (_shop != null) _shop.OnCollectionChanged -= RefreshBalances; _shop = null; }

    public void SelectPack(int pack)
    {
        if (pack < 0 || pack > 3) return;
        SelectedPack = (CardPackType)pack;
        CurrentPage = 0;
        GenerateIndex();
    }
    public void CycleLayout()
    {
        int firstCard = CurrentPage * CardsPerPage;
        _layoutIndex = (_layoutIndex + 1) % 3;
        CurrentPage = Mathf.Clamp(firstCard / CardsPerPage, 0, PageCount - 1);
        ShowPage();
    }
    public void PreviousPage() { if (CurrentPage > 0) { CurrentPage--; ShowPage(); } }
    public void NextPage() { if (CurrentPage + 1 < PageCount) { CurrentPage++; ShowPage(); } }
    public void GenerateIndex()
    {
        _cards.Clear();
        if (ShopManager.Instance != null && ShopManager.Instance.AllCards != null)
            _cards.AddRange(ShopManager.Instance.AllCards.Where(c => c != null && !c.IsBasic && c.PackCategory == SelectedPack && (!HostFilterMode || ShopManager.Instance.GetCardData(c.ID)?.IsUnlocked == true))
                .OrderBy(c => c.Rarity).ThenBy(c => c.CardName));
        CurrentPage = Mathf.Clamp(CurrentPage, 0, PageCount - 1);
        ShowPage();
    }
    void ShowPage()
    {
        if (ContentArea == null || CardDisplayPrefab == null) return;
        var events = UnityEngine.EventSystems.EventSystem.current;
        bool restoreCardFocus = events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(ContentArea);
        foreach (Transform child in ContentArea) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        _displays.Clear();
        for (int i = CurrentPage * CardsPerPage; i < Mathf.Min(_cards.Count, (CurrentPage + 1) * CardsPerPage); i++)
        {
            var card = Instantiate(CardDisplayPrefab, ContentArea);
            var display = card.GetComponent<CardDisplay>();
            display.SetupIndex(_cards[i], HostFilterMode);
            _displays.Add((RectTransform)card.transform);
        }
        if (CoinsText != null) CoinsText.gameObject.SetActive(!HostFilterMode);
        if (EssenceText != null) EssenceText.gameObject.SetActive(!HostFilterMode);
        LayoutCards();
        if (LayoutText != null) LayoutText.text = _layoutIndex == 2 ? "LAYOUT: ALL" : "LAYOUT: " + LayoutColumns + " x " + LayoutRows;
        if (PageText != null) PageText.text = (CurrentPage + 1) + " / " + PageCount;
        if (PreviousButton != null) PreviousButton.interactable = CurrentPage > 0;
        if (NextButton != null) NextButton.interactable = CurrentPage + 1 < PageCount;
        if (PackButtons != null) for (int i = 0; i < PackButtons.Length; i++)
        {
            var button = PackButtons[i];
            if (button == null) continue;
            var colors = button.colors;
            colors.normalColor = i == (int)SelectedPack ? new Color(1, .82f, .28f) : Color.white;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            var outline = button.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null) outline.enabled = i == (int)SelectedPack;
        }
        RefreshBalances();
        ConfigureNavigation();
        if (restoreCardFocus && _displays.Count > 0) events.SetSelectedGameObject(_displays[0].GetComponent<CardDisplay>().ClickButton.gameObject);
    }
    public void ConfigureNavigation()
    {
        for (int i = 0; i < _displays.Count; i++)
        {
            var card = _displays[i].GetComponent<CardDisplay>();
            var root = card.ClickButton;
            var action = card.UpgradeButton != null && card.UpgradeButton.gameObject.activeSelf && card.UpgradeButton.IsInteractable() ? card.UpgradeButton : card.AscendButton;
            if (action != null && (!action.gameObject.activeInHierarchy || !action.IsInteractable())) action = null;
            var nav = root.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            nav.selectOnLeft = i % LayoutColumns > 0 ? _displays[i - 1].GetComponent<CardDisplay>().ClickButton : PackButtons[(int)SelectedPack];
            nav.selectOnRight = i % LayoutColumns < LayoutColumns - 1 && i + 1 < _displays.Count ? _displays[i + 1].GetComponent<CardDisplay>().ClickButton : null;
            nav.selectOnUp = i >= LayoutColumns ? _displays[i - LayoutColumns].GetComponent<CardDisplay>().ClickButton : PackButtons[(int)SelectedPack];
            nav.selectOnDown = action != null ? action : i + LayoutColumns < _displays.Count ? _displays[i + LayoutColumns].GetComponent<CardDisplay>().ClickButton : LayoutButton;
            root.navigation = nav;
            if (action != null)
            {
                var actionNav = nav; actionNav.selectOnUp = root;
                actionNav.selectOnDown = i + LayoutColumns < _displays.Count ? _displays[i + LayoutColumns].GetComponent<CardDisplay>().ClickButton : LayoutButton;
                action.navigation = actionNav;
            }
        }
        // PackButtons follows enum IDs for category selection, not screen order.
        var sidebar = new[] {
            transform.Find("BackButton")?.GetComponent<UnityEngine.UI.Button>(),
            PackButtons[(int)CardPackType.Mobility], PackButtons[(int)CardPackType.Munitions],
            PackButtons[(int)CardPackType.Survival], PackButtons[(int)CardPackType.Gadget], LayoutButton
        };
        for (int i = 0; i < sidebar.Length; i++)
        {
            var button = sidebar[i];
            if (button == null) continue;
            var nav = button.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? sidebar[i - 1] : null;
            nav.selectOnDown = i + 1 < sidebar.Length ? sidebar[i + 1] : null;
            nav.selectOnLeft = null;
            // Retain geometric rightward entry from Back/Layout to cards or paging.
            nav.selectOnRight = i == 0 || i == sidebar.Length - 1 ? button.FindSelectable(Vector3.right) :
                _displays.Count > 0 ? _displays[0].GetComponent<CardDisplay>().ClickButton : LayoutButton;
            button.navigation = nav;
        }
    }
    void OnRectTransformDimensionsChange() { LayoutCards(); }
    void LayoutCards()
    {
        if (ContentArea == null) return;
        var area = (RectTransform)ContentArea;
        float cellWidth = area.rect.width / LayoutColumns, cellHeight = area.rect.height / LayoutRows;
        float scale = Mathf.Max(.01f, Mathf.Min((cellWidth - 24) / 500f, (cellHeight - 18) / 700f));
        for (int i = 0; i < _displays.Count; i++)
        {
            var card = _displays[i];
            if (card == null) continue;
            card.anchorMin = card.anchorMax = new Vector2((i % LayoutColumns + .5f) / LayoutColumns, 1 - (i / LayoutColumns + .5f) / LayoutRows);
            card.pivot = new Vector2(.5f, .5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(500, 700);
            card.localScale = Vector3.one * scale;
        }
    }
    public void RefreshBalances()
    {
        var shop = ShopManager.Instance;
        if (shop == null) return;
        if (CoinsText != null) CoinsText.text = (shop.InfiniteResources ? "Infinite" : shop.CurrentCoins.ToString("N0")) + " Coins";
        if (EssenceText != null) EssenceText.text = (shop.InfiniteResources ? "Infinite" : shop.GetEssence(SelectedPack).ToString("N0")) + " " + SelectedPack + " Essence";
    }
}
