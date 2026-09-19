using UnityEngine;
using System.Collections.Generic;

public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance;

    [Header("UI References")]
    [Tooltip("The root panel that shows/hides during level-up.")]
    public GameObject Panel;

    [Header("Card Spawning")]
    [Tooltip("The LevelUpCard prefab. Must have a CardDisplay component.")]
    public GameObject CardPrefab;

    [Tooltip("The parent transform (with HorizontalLayoutGroup) where cards spawn.")]
    public Transform CardContainer;

    [Tooltip("How many cards to offer on level up.")]
    public int CardsToOffer = 3;

    void Awake()
    {
        Instance = this;
        if (Panel != null) Panel.SetActive(false);
    }

    public void ShowLevelUpOptions()
    {
        if (CardPrefab == null || CardContainer == null)
        {
            Debug.LogError("LevelUpUI: CardPrefab or CardContainer is not assigned!");
            return;
        }

        Time.timeScale = 0f;
        Panel.SetActive(true);

        ClearContainer();

        List<CardDefinition> options = CardManager.Instance.GetRandomCards(CardsToOffer);

        foreach (CardDefinition option in options)
        {
            GameObject cardObj = Instantiate(CardPrefab, CardContainer);
            CardDisplay display = cardObj.GetComponent<CardDisplay>();

            if (display != null)
            {
                display.Setup(option);
            }
            else
            {
                Debug.LogError("LevelUpUI: Spawned CardPrefab is missing a CardDisplay component!");
            }
        }
    }

    /// <summary>
    /// Called by CardDisplay.ClickButton when the player picks a card.
    /// Plays the card-selection sound, applies the effect, cleans up the UI, 
    /// and resumes the game.
    /// </summary>
    public void SelectCard(CardDefinition card)
    {
        // Play the level-up card-pick sound
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("Selected_Card");
        
        CardManager.Instance.ApplyCardEffect(card);
        ClearContainer();
        Panel.SetActive(false);
        Time.timeScale = 1f;
    }

    void ClearContainer()
    {
        if (CardContainer == null) return;

        foreach (Transform child in CardContainer)
        {
            Destroy(child.gameObject);
        }
    }
}