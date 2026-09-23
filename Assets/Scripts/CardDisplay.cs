using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class CardDisplay : MonoBehaviour, IPointerEnterHandler
{
    [Header("Basic UI")]
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI DescriptionText;
    public Image IconImage;
    public Image BackgroundImage;
    public Button ClickButton;

    [Header("Leveling UI (Shop Only)")]
    public GameObject LevelGroup;
    public Slider ProgressSlider;
    public TextMeshProUGUI ProgressText;
    public TextMeshProUGUI LevelText;
    public Button UpgradeButton;
    public TextMeshProUGUI UpgradeCostText;
    public GameObject AscensionGroup;
    public UnityEngine.UI.Button AscendButton;
    public TextMeshProUGUI EssenceText;
    ShopManager _subscribedShop;
    
    [Header("Hover Flip Animation")]
    [Tooltip("Duration of the full 360-degree flip in seconds. Shorter = snappier.")]
    public float FlipDuration = 0.4f;
    
    [Tooltip("Animation easing curve. Defaults to ease-in-out for a natural feel. " +
             "Click in the Inspector to customize the curve visually.")]
    public AnimationCurve FlipCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private CardDefinition _assignedCard;
    private bool _isLocked = false;
    
    // True while the flip animation is playing. New hovers during this time are 
    // ignored (per design choice - prevents choppy interruption mid-flip).
    private bool _isFlipping = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Locked cards: no hover sound, no flip
        if (_isLocked) return;
        
        // If already flipping from a previous hover, ignore this one - 
        // wait for the current flip to finish first
        if (_isFlipping) return;
        
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("Card_Hover");
        
        StartCoroutine(FlipAnimation());
    }
    
    /// <summary>
    /// Rotates the card 360 degrees around the Y-axis (vertical flip), like a 
    /// physical card spinning in place. The card briefly shows its edge in the 
    /// middle of the flip, then returns to facing forward.
    /// 
    /// Uses Time.unscaledDeltaTime so it works during paused game states 
    /// (e.g., the level-up screen which sets Time.timeScale = 0).
    /// </summary>
    private IEnumerator FlipAnimation()
    {
        _isFlipping = true;
        
        float elapsed = 0f;
        Vector3 startEuler = transform.localEulerAngles;
        
        // Ensure we start from clean Y rotation
        transform.localEulerAngles = new Vector3(startEuler.x, 0f, startEuler.z);
        
        while (elapsed < FlipDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / FlipDuration);
            float curvedT = FlipCurve.Evaluate(t);
            
            // Y-axis rotation creates the "vertical spin" - card flips around 
            // its vertical center line, showing back at 180 degrees
            float currentY = curvedT * 360f;
            transform.localEulerAngles = new Vector3(startEuler.x, currentY, startEuler.z);
            
            yield return null;
        }
        
        // Snap back to 0 Y rotation when done (card faces forward again)
        transform.localEulerAngles = new Vector3(startEuler.x, 0f, startEuler.z);
        _isFlipping = false;
    }

    public void Setup(CardDefinition card)
    {
        _assignedCard = card;
        if (_subscribedShop != ShopManager.Instance)
        {
            if (_subscribedShop != null) _subscribedShop.OnCollectionChanged -= Refresh;
            _subscribedShop = ShopManager.Instance;
            if (_subscribedShop != null) _subscribedShop.OnCollectionChanged += Refresh;
        }

        // 1.4.11 PATCH: distinguish two display contexts.
        // 
        // Context A - In-game level-up offer (LevelUpUI.Instance != null):
        //   Show the level the card WILL BE AT after this pickup, so the description 
        //   matches the stat boost the player is about to get. This is shop level + 
        //   run pickups + 1.
        //
        // Context B - Shop/index display (no LevelUpUI active):
        //   Show the current shop level, since the player is browsing what they own.
        
        int displayLevel = 1;
        
        if (LevelUpUI.Instance != null && CardManager.Instance != null)
        {
            // In-game offer: show level after this pickup
            displayLevel = CardManager.Instance.GetNextPickupLevel(card.ID);
        }
        else if (ShopManager.Instance != null)
        {
            var data = ShopManager.Instance.GetCardData(card.ID);
            if (data != null) displayLevel = data.Level;
        }

        bool ascended = CardManager.Instance != null && LevelUpUI.Instance != null
            ? CardManager.Instance.IsAscended(card.ID) : ShopManager.Instance != null && ShopManager.Instance.GetCardData(card.ID)?.IsAscended == true;
        if (NameText) NameText.text = ascended ? card.AscendedName : card.CardName;
        if (DescriptionText) DescriptionText.text = ascended ? card.AscendedDescription : card.GetDescriptionAtLevel(displayLevel);
        if (IconImage) IconImage.sprite = ascended && card.AscendedIcon != null ? card.AscendedIcon : card.Icon;
        if (AscensionGroup != null) AscensionGroup.SetActive(false);
        if (AscendButton != null) AscendButton.gameObject.SetActive(false);

        if (BackgroundImage != null)
        {
            switch (card.Rarity)
            {
                case CardRarity.Common: BackgroundImage.color = new Color(0.6f, 0.6f, 0.6f); break;
                case CardRarity.Rare: BackgroundImage.color = new Color(0.3f, 0.5f, 1.0f); break;
                case CardRarity.Legendary: BackgroundImage.color = new Color(1.0f, 0.8f, 0.2f); break;
                case CardRarity.Corrupted: BackgroundImage.color = new Color(0.7f, 0.1f, 0.8f); break;
            }
        }

        if (ClickButton != null)
        {
            ClickButton.onClick.RemoveAllListeners();
            if (LevelUpUI.Instance != null)
            {
                ClickButton.interactable = true;
                ClickButton.onClick.AddListener(() => LevelUpUI.Instance.SelectCard(_assignedCard));
            }
            else
            {
                ClickButton.interactable = false;
            }
        }

        if (ShopManager.Instance != null)
        {
            CardSaveData data = ShopManager.Instance.GetCardData(card.ID);
            if (data != null && LevelGroup != null)
            {
                if (LevelUpUI.Instance != null)
                {
                    LevelGroup.SetActive(false);
                }
                else
                {
                    LevelGroup.SetActive(true);
                    UpdateLevelUI(data);
                }
            }
        }

        bool locked = !card.IsBasic && ShopManager.Instance != null && ShopManager.Instance.GetCardData(card.ID)?.IsUnlocked != true;
        SetLockedState(locked);
    }

    void UpdateLevelUI(CardSaveData data)
    {
        bool ascensionAvailable = data.Level >= _assignedCard.MaxLevel && !data.IsAscended && _assignedCard.Ascension != CardAscension.None;
        if (AscensionGroup) AscensionGroup.SetActive(ascensionAvailable);
        if (AscendButton)
        {
            AscendButton.gameObject.SetActive(ascensionAvailable);
            AscendButton.onClick.RemoveAllListeners();
            AscendButton.onClick.AddListener(() => ShopManager.Instance.TryAscendCard(_assignedCard.ID));
            AscendButton.interactable = ascensionAvailable && (ShopManager.Instance.InfiniteResources || ShopManager.Instance.GetEssence(_assignedCard.PackCategory) >= _assignedCard.AscensionCost);
        }
        if (EssenceText) EssenceText.text = (ShopManager.Instance.InfiniteResources ? "∞" : ShopManager.Instance.GetEssence(_assignedCard.PackCategory).ToString()) + "/" + _assignedCard.AscensionCost + " " + _assignedCard.PackCategory + " Essence";
        if (data.Level >= _assignedCard.MaxLevel)
        {
            if (LevelText) LevelText.text = data.IsAscended ? "ASCENDED" : "MAX · Lv 6";
            if (UpgradeButton) UpgradeButton.gameObject.SetActive(false);
            if (ProgressSlider) ProgressSlider.gameObject.SetActive(false);
            if (ProgressText) ProgressText.gameObject.SetActive(false);
        }
        else
        {
            if (LevelText) LevelText.text = "Lv " + data.Level;

            int required = _assignedCard.GetCardsRequired(data.Level);
            int current = data.Duplicates;
            int cost = _assignedCard.GetUpgradeCost(data.Level);

            if (ProgressSlider)
            {
                ProgressSlider.gameObject.SetActive(true);
                ProgressSlider.maxValue = required;
                ProgressSlider.value = current;
            }
            if (ProgressText) { ProgressText.gameObject.SetActive(true); ProgressText.text = ShopManager.Instance.InfiniteCopies ? $"∞/{required}" : $"{current}/{required}"; }
            if (UpgradeCostText) UpgradeCostText.text = cost + " G";

            bool canUpgrade = (ShopManager.Instance.InfiniteCopies || current >= required) && ShopManager.Instance.CanAfford(cost);

            if (UpgradeButton)
            {
                UpgradeButton.gameObject.SetActive(true);
                UpgradeButton.interactable = canUpgrade;
                UpgradeButton.onClick.RemoveAllListeners();
                UpgradeButton.onClick.AddListener(TryUpgrade);
            }
        }
    }

    void TryUpgrade()
    {
        bool success = ShopManager.Instance.TryUpgradeCard(_assignedCard.ID);
        if (success)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("UI_button_Click");
            Setup(_assignedCard);
        }
        else
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("Button_Error");
        }
    }

    public void SetLockedState(bool isLocked)
    {
        _isLocked = isLocked;
        
        if (isLocked)
        {
            if (BackgroundImage != null) BackgroundImage.color = new Color(0.2f, 0.2f, 0.2f);
            if (IconImage != null) IconImage.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            if (NameText != null) NameText.text = "???";
            if (DescriptionText != null) DescriptionText.text = "Locked";
            if (LevelGroup != null) LevelGroup.SetActive(false);
        }
        else
        {
            if (IconImage != null) IconImage.color = Color.white;
        }
    }

    void Refresh() { if (_assignedCard != null) Setup(_assignedCard); }
    void OnDestroy() { if (_subscribedShop != null) _subscribedShop.OnCollectionChanged -= Refresh; }
    public void HideShopControls()
    {
        if (LevelGroup) LevelGroup.SetActive(false);
        if (AscensionGroup) AscensionGroup.SetActive(false);
        if (AscendButton) AscendButton.gameObject.SetActive(false);
        if (ClickButton) ClickButton.interactable = false;
    }
}
