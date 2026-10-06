using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class CardIndexUI : MonoBehaviour
{
    [Header("References")]
    public Transform ContentArea;
    public GameObject CategoryHeaderPrefab; // Retained for existing serialization.
    public GameObject CardGridPrefab;
    public GameObject CardDisplayPrefab;
    public UnityEngine.UI.Button[] PackButtons; // Munitions, Mobility, Survival, Gadget.
    public UnityEngine.UI.Button PreviousButton, NextButton, LayoutButton;
    public TextMeshProUGUI PageText, CoinsText, EssenceText, LayoutText;
    public CardPackType SelectedPack = CardPackType.Mobility;
    public int LayoutColumns => _layoutIndex == 1 ? 4 : 3;
    public int LayoutRows => _layoutIndex == 0 ? 1 : 2;
    public int CardsPerPage => LayoutColumns * LayoutRows;
    int _layoutIndex; // 3x1, 4x2, 3x2; retained while the menu scene is open.
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
            _cards.AddRange(ShopManager.Instance.AllCards.Where(c => c != null && !c.IsBasic && c.PackCategory == SelectedPack)
                .OrderBy(c => c.Rarity).ThenBy(c => c.CardName));
        CurrentPage = Mathf.Clamp(CurrentPage, 0, PageCount - 1);
        ShowPage();
    }
    void ShowPage()
    {
        if (ContentArea == null || CardDisplayPrefab == null) return;
        foreach (Transform child in ContentArea) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        _displays.Clear();
        for (int i = CurrentPage * CardsPerPage; i < Mathf.Min(_cards.Count, (CurrentPage + 1) * CardsPerPage); i++)
        {
            var card = Instantiate(CardDisplayPrefab, ContentArea);
            card.GetComponent<CardDisplay>().Setup(_cards[i]);
            _displays.Add((RectTransform)card.transform);
        }
        LayoutCards();
        if (LayoutText != null) LayoutText.text = "LAYOUT: " + LayoutColumns + " x " + LayoutRows;
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
