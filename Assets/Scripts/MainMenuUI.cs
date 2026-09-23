using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance;

    [Header("Main Panels")]
    public GameObject MenuPanel;
    public GameObject ShopPanel;
    public GameObject SettingsPanel;
    public GameObject IndexPanel; 

    [Header("Shop Components")]
    public Transform PackContainer;
    public GameObject PackButtonPrefab; 
    public TextMeshProUGUI TotalCoinsText;

    [Header("Confirmation Overlay")]
    public GameObject ConfirmPanel;
    public TextMeshProUGUI ConfirmText;
    public Button YesButton;
    private ShopPackDefinition _selectedPack;
    [Header("Multiple packs")]
    public UnityEngine.UI.Button BuyThreeButton;
    public TextMeshProUGUI BuyThreeText;
    public UnityEngine.UI.Image[] AdditionalPackImages;
    public UnityEngine.UI.GridLayoutGroup RevealGrid;
    public Vector2 SingleCardSize = new Vector2(260, 360);
    public Vector2 TripleCardSize = new Vector2(160, 220);
    [Tooltip("Full-size card canvas before fitting it into a reveal grid cell. Keeps fonts and fixed child artwork proportional.")]
    public Vector2 CardReferenceSize = new Vector2(500, 700);
    public TextMeshProUGUI EssenceBalancesText;
    int _packCount = 1;
    bool _purchaseInProgress;
    List<CardDefinition> _purchasedCards;

    [Header("Pack Opening Minigame")]
    public GameObject OpeningOverlay;
    public Image PackImage; 
    public GameObject SliceZone; 
    public Transform CardRevealCenter; 
    public GameObject CardDisplayPrefab; 
    
    public GameObject SliceHelpText; 
    public Button ContinueButton;    
    
    private bool _isSlicingMode = false;
    private Vector2 _lastMousePos;
    private float _sliceProgress = 0f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // --- DEBUG: UI Safety Checks ---
        if (CardDisplayPrefab == null) Debug.LogError("<color=red>MainMenuUI Error: 'Card Display Prefab' is missing in Inspector.</color>");
        if (CardRevealCenter == null) Debug.LogError("<color=red>MainMenuUI Error: 'Card Reveal Center' is missing in Inspector.</color>");

        ShowPanel(MenuPanel);
        if (ShopManager.Instance != null)
        {
            UpdateCoinDisplay(ShopManager.Instance.CurrentCoins);
            GenerateShopButtons();
            ShopManager.Instance.OnCollectionChanged += RefreshBalances;
        }
    }

    void Update()
    {
        if (_isSlicingMode && Input.GetMouseButton(0))
        {
            PerformSlice();
        }
        if (Input.GetMouseButtonUp(0))
        {
            _lastMousePos = Vector2.zero;
        }
    }

    // --- NAVIGATION ---
public void ShowPanel(GameObject panel)
{
    StopAllCoroutines();
    MenuPanel.SetActive(false);
    ShopPanel.SetActive(false);
    SettingsPanel.SetActive(false);
    IndexPanel.SetActive(false);
    ConfirmPanel.SetActive(false);
    OpeningOverlay.SetActive(false);
    _purchaseInProgress = false;
    _isSlicingMode = false;

    panel.SetActive(true);
    
    // Switch music to match the panel being shown.
    // AudioManager.PlayMusic() crossfades smoothly and ignores duplicate calls 
    // (won't restart if the same track is already playing).
    if (AudioManager.Instance != null)
    {
        if (panel == MenuPanel)
            AudioManager.Instance.PlayMusic("Main_Menu_Track_1");
        else if (panel == ShopPanel)
            AudioManager.Instance.PlayMusic("Shop_Menu_Track_1");
        else if (panel == SettingsPanel)
            AudioManager.Instance.PlayMusic("Settings_Menu_Track_1");
        else if (panel == IndexPanel)
            AudioManager.Instance.PlayMusic("Card_Index_Menu_Track_1");
        // ConfirmPanel and OpeningOverlay don't trigger music changes - 
        // they overlay on top of an existing panel and inherit its music.
    }
}

    public void OpenMenu()
    {
        ShowPanel(MenuPanel);
    }
    
    public void BackToMainMenu()
    {
        ShowPanel(MenuPanel);
    }

public void OpenShop()
{
    ShowPanel(ShopPanel);
}

    public void OpenSettings()
    {
        ShowPanel(SettingsPanel);
    }

    public void OpenIndex()
    {
        ShowPanel(IndexPanel);
    }

    public void UpdateCoinDisplay(int coins)
    {
        if (TotalCoinsText != null) TotalCoinsText.text = ShopManager.Instance != null && ShopManager.Instance.InfiniteResources ? "Infinite Coins" : coins + " Coins";
        RefreshEssence();
    }

    // --- SHOP GENERATION ---
    void GenerateShopButtons()
    {
        foreach (Transform child in PackContainer) Destroy(child.gameObject);

        if (ShopManager.Instance == null) return;

        foreach (ShopPackDefinition pack in ShopManager.Instance.AvailablePacks)
        {
            if (pack == null) continue;

            GameObject btnObj = Instantiate(PackButtonPrefab, PackContainer);
            
            TextMeshProUGUI[] texts = btnObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = pack.PackName; 
            if (texts.Length > 1) texts[1].text = pack.Cost + " G";

            Transform iconTr = btnObj.transform.Find("Icon");
            if (iconTr != null && iconTr.GetComponent<Image>())
            {
                iconTr.GetComponent<Image>().sprite = pack.ClosedPackIcon;
            }
            else
            {
                Image[] images = btnObj.GetComponentsInChildren<Image>();
                foreach(var img in images)
                {
                    if (img.gameObject != btnObj)
                    {
                        img.sprite = pack.ClosedPackIcon;
                        break; 
                    }
                }
            }

            Button btn = btnObj.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.AddListener(() => OnPackClicked(pack));
            }
        }
    }

void OnPackClicked(ShopPackDefinition pack)
{
    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("UI_button_Click");
    
    _selectedPack = pack;
    ConfirmPanel.SetActive(true);
    ConfirmText.text = $"Buy {pack.PackName} for {pack.Cost} Coins?";
    
    YesButton.onClick.RemoveAllListeners();
    YesButton.onClick.AddListener(BuyPack);
    YesButton.interactable = ShopManager.Instance.CanAfford(pack.Cost);
    if (BuyThreeButton != null)
    {
        BuyThreeButton.onClick.RemoveAllListeners();
        BuyThreeButton.onClick.AddListener(BuyThreePacks);
        BuyThreeButton.interactable = (long)pack.Cost * 3 <= int.MaxValue && ShopManager.Instance.CanAfford(pack.Cost * 3);
    }
    if (BuyThreeText != null) BuyThreeText.text = "Open 3 - " + ((long)pack.Cost * 3) + " Coins";
}

public void BuyPack() { Purchase(1); }
public void BuyThreePacks() { Purchase(3); }
void Purchase(int count)
{
    if (_purchaseInProgress || _selectedPack == null) return;
    var cards = ShopManager.Instance.TryBuyPacks(_selectedPack, count);
    if (cards == null) { ConfirmText.text = "Not enough coins or no cards assigned."; return; }
    _purchaseInProgress = true;
    _packCount = count;
    _purchasedCards = cards;
    UpdateCoinDisplay(ShopManager.Instance.CurrentCoins);
    StartPackOpening(_selectedPack);
    ConfirmPanel.SetActive(false);
}

    // --- MINIGAME LOGIC ---
    void StartPackOpening(ShopPackDefinition pack)
    {
        OpeningOverlay.SetActive(true);
        ShopPanel.SetActive(false);

        PackImage.sprite = pack.ClosedPackIcon;
        PackImage.transform.rotation = Quaternion.identity;
        PackImage.gameObject.SetActive(true);
        if (AdditionalPackImages != null)
            for (int i = 0; i < AdditionalPackImages.Length; i++)
                if (AdditionalPackImages[i] != null)
                {
                    AdditionalPackImages[i].sprite = pack.ClosedPackIcon;
                    AdditionalPackImages[i].gameObject.SetActive(i < _packCount - 1);
                }
        if (RevealGrid != null)
        {
            RevealGrid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            RevealGrid.constraintCount = 3;
            RevealGrid.cellSize = _packCount == 3 ? TripleCardSize : SingleCardSize;
        }
        SliceZone.SetActive(true);
        SliceHelpText.SetActive(true);
        ContinueButton.gameObject.SetActive(false);

        foreach (Transform child in CardRevealCenter) Destroy(child.gameObject);

        _isSlicingMode = true;
        _sliceProgress = 0f;
        _lastMousePos = Input.mousePosition;
    }

    void PerformSlice()
    {
        Vector2 currentPos = Input.mousePosition;
        
        if (_lastMousePos == Vector2.zero)
        {
            _lastMousePos = currentPos;
            return;
        }

        float dist = Vector2.Distance(currentPos, _lastMousePos);
        _lastMousePos = currentPos;
        if (!PointerOverPack(currentPos)) return;
        _sliceProgress += dist;

        PackImage.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 50) * 5);

        if (_sliceProgress > 500f) 
        {
            CompleteSlice();
        }
    }

    bool PointerOverPack(Vector2 position)
    {
        Canvas canvas = PackImage.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.RectangleContainsScreenPoint(PackImage.rectTransform, position, camera)) return true;
        if (AdditionalPackImages != null)
            foreach (var image in AdditionalPackImages)
                if (image != null && image.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, position, camera)) return true;
        return false;
    }

    void CompleteSlice()
    {
        _isSlicingMode = false;
        SliceZone.SetActive(false);
        SliceHelpText.SetActive(false);
        
        if (_selectedPack.OpenedPackIcon != null)
        {
            PackImage.sprite = _selectedPack.OpenedPackIcon;
            if (AdditionalPackImages != null)
                foreach (var image in AdditionalPackImages) if (image != null) image.sprite = _selectedPack.OpenedPackIcon;
        }

        StartCoroutine(RevealCardsRoutine());
    }

    /// <summary>
    /// Opens the pack using ShopManager's centralized, rarity-weighted logic.
    /// ShopManager.OpenPack() handles:
    ///   - Rarity rolling (5% Legendary, 35% Rare, 60% Common)
    ///   - Filtering by PackCategory
    ///   - Saving cards to the player's collection
    ///   - Saving to disk
    /// This method is now purely responsible for visual reveal + animation.
    /// </summary>
    IEnumerator RevealCardsRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        PackImage.gameObject.SetActive(false);
        if (AdditionalPackImages != null) foreach (var image in AdditionalPackImages) if (image != null) image.gameObject.SetActive(false);

        // --- 1. GET CARDS (uses proper rarity-weighted pull) ---
        if (ShopManager.Instance == null)
        {
            Debug.LogError("<color=red>ShopManager.Instance is null - cannot open pack.</color>");
            yield break;
        }

        List<CardDefinition> pulledCards = _purchasedCards;

        if (pulledCards == null || pulledCards.Count == 0)
        {
            Debug.LogError($"<color=red>No cards returned from pack type: {_selectedPack.PackType}. Check ShopManager's card list.</color>");
            
            // Show continue button anyway so the player isn't stuck
            if (ContinueButton != null)
            {
                ContinueButton.gameObject.SetActive(true);
                ContinueButton.onClick.RemoveAllListeners();
                ContinueButton.onClick.AddListener(() => ShowPanel(ShopPanel));
            }
            yield break;
        }

        // --- 2. SPAWN VISUALS ---
        List<GameObject> spawnedCards = new List<GameObject>();

        foreach (CardDefinition picked in pulledCards)
        {
            if (CardDisplayPrefab != null)
            {
                Transform parent = CardRevealCenter;
                if (RevealGrid != null)
                {
                    var slot = new GameObject("RevealSlot", typeof(RectTransform));
                    slot.transform.SetParent(CardRevealCenter, false);
                    parent = slot.transform;
                }
                GameObject cardObj = Instantiate(CardDisplayPrefab, parent);
                if (RevealGrid != null)
                {
                    var rect = cardObj.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = CardReferenceSize;
                }
                CardDisplay disp = cardObj.GetComponent<CardDisplay>();
                if (disp != null)
                {
                    disp.Setup(picked);
                    disp.HideShopControls();
                    // Disable clicking during reveal animation
                    Button cardButton = disp.GetComponent<Button>();
                    if (cardButton != null) cardButton.interactable = false;
                }

                cardObj.transform.localScale = Vector3.zero;
                spawnedCards.Add(cardObj);
            }
        }

        // --- 3. REFRESH ECONOMY DISPLAY ---
        // ShopManager.OpenPack() already saved the collection changes,
        // but we reload to ensure the coin display and any cached data syncs.
        ShopManager.Instance.LoadEconomy();
        UpdateCoinDisplay(ShopManager.Instance.CurrentCoins);

        yield return null; 

        // --- 4. POP-IN ANIMATION ---
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            float fit = RevealGrid != null ? Mathf.Min(RevealGrid.cellSize.x / Mathf.Max(1, CardReferenceSize.x), RevealGrid.cellSize.y / Mathf.Max(1, CardReferenceSize.y)) : 1;
            StartCoroutine(AnimatePop(spawnedCards[i].transform, fit));
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(1.0f);
        
        if (ContinueButton != null)
        {
            ContinueButton.gameObject.SetActive(true);
            ContinueButton.onClick.RemoveAllListeners();
            ContinueButton.onClick.AddListener(() => ShowPanel(ShopPanel));
        }
    }
    
    IEnumerator AnimatePop(Transform target, float scale)
    {
        float timer = 0f;
        while(timer < 0.3f)
        {
            timer += Time.deltaTime;
            float progress = timer / 0.3f;
            target.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * scale, progress);
            yield return null;
        }
        target.localScale = Vector3.one * scale;
    }

    public void ResetGameData()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ResetProgress();
            UpdateCoinDisplay(ShopManager.Instance.CurrentCoins);
        }
    }
    void RefreshBalances() { if (ShopManager.Instance != null) UpdateCoinDisplay(ShopManager.Instance.CurrentCoins); }
    void RefreshEssence()
    {
        if (EssenceBalancesText == null || ShopManager.Instance == null) return;
        var shop = ShopManager.Instance;
        EssenceBalancesText.text = "Munitions " + (shop.InfiniteResources ? "Infinite" : shop.GetEssence(CardPackType.Munitions).ToString()) +
            "  Mobility " + (shop.InfiniteResources ? "Infinite" : shop.GetEssence(CardPackType.Mobility).ToString()) +
            "  Survival " + (shop.InfiniteResources ? "Infinite" : shop.GetEssence(CardPackType.Survival).ToString()) +
            "  Gadget " + (shop.InfiniteResources ? "Infinite" : shop.GetEssence(CardPackType.Gadget).ToString());
    }
    void OnDestroy() { if (ShopManager.Instance != null) ShopManager.Instance.OnCollectionChanged -= RefreshBalances; }
}
