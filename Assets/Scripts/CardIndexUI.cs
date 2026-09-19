using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; 

public class CardIndexUI : MonoBehaviour
{
    [Header("References")]
    public Transform ContentArea; 
    public GameObject CategoryHeaderPrefab; 
    public GameObject CardGridPrefab; 
    public GameObject CardDisplayPrefab; 

    void OnEnable()
    {
        GenerateIndex();
    }

    public void GenerateIndex()
    {
        foreach (Transform child in ContentArea) Destroy(child.gameObject);

        List<CardDefinition> allCards = ShopManager.Instance.AllCards;
        PlayerData playerData = SaveSystem.LoadData();

        // === 1.4.9 PATCH 3: Exclude BaseSet cards from the index ===
        // BaseSet cards are always-unlocked default cards; players don't need to see 
        // them on the collection screen. Filtering them out also prevents the empty
        // "BASESET PACK" header from appearing.
        var groupedCards = allCards
            .Where(card => card.PackCategory != CardPackType.BaseSet)
            .GroupBy(card => card.PackCategory);

        foreach (var group in groupedCards)
        {
            CreateHeader(group.Key.ToString());
            Transform gridTransform = CreateGrid();

            var sortedCards = group.OrderBy(card => GetRaritySortOrder(card.Rarity));

            foreach (CardDefinition card in sortedCards)
            {
                GameObject cardObj = Instantiate(CardDisplayPrefab, gridTransform);
                CardDisplay disp = cardObj.GetComponent<CardDisplay>();

                disp.Setup(card);

                // BaseSet check is now redundant (we filtered them above) but kept 
                // defensively in case future card types are also "always unlocked."
                bool isUnlocked = false;

                if (card.PackCategory == CardPackType.BaseSet)
                {
                    isUnlocked = true;
                }
                else
                {
                    if (playerData.CardCollection.Exists(c => c.CardID == card.ID))
                    {
                        isUnlocked = true;
                    }
                }
                
                disp.SetLockedState(!isUnlocked);
            }
        }
    }

    /// <summary>
    /// Returns a sort order value for card rarity.
    /// Order: Common -> Rare -> Legendary -> Corrupted
    /// </summary>
    private int GetRaritySortOrder(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:
                return 0;
            case CardRarity.Rare:
                return 1;
            case CardRarity.Legendary:
                return 2;
            case CardRarity.Corrupted:
                return 3;
            default:
                return 99;
        }
    }

    void CreateHeader(string title)
    {
        if (CategoryHeaderPrefab != null)
        {
            GameObject header = Instantiate(CategoryHeaderPrefab, ContentArea);
            header.GetComponent<TextMeshProUGUI>().text = title.ToUpper() + " PACK";
        }
    }

    Transform CreateGrid()
    {
        if (CardGridPrefab != null)
        {
            GameObject grid = Instantiate(CardGridPrefab, ContentArea);
            return grid.transform;
        }
        return ContentArea;
    }
}