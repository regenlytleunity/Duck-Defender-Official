using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 1.4.11: No structural changes. CardPackType.Tech is gone, replaced by CardPackType.Gadget.
/// Since cards are keyed by string ID in PlayerData, existing collection saves still load fine
/// (the enum lives on the CardDefinition asset, not in the save file).
/// 
/// IMPORTANT: After update, re-author your ShopPackDefinition assets to point at the new Gadget 
/// pack type if they were previously set to Tech.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("Database")]
    public List<CardDefinition> AllCards;
    public List<ShopPackDefinition> AvailablePacks;

    [Header("State")]
    public int CurrentCoins;

    private PlayerData _playerData;

    void Awake()
    {
        Instance = this;
        LoadEconomy();
    }

    public void LoadEconomy()
    {
        _playerData = SaveSystem.LoadData();
        CurrentCoins = _playerData.TotalCoins;
        UnlockBaseSet();
    }

    private void UnlockBaseSet()
    {
        if (AllCards == null) return;

        bool changed = false;
        foreach (var card in AllCards)
        {
            if (card != null && card.PackCategory == CardPackType.BaseSet)
            {
                if (!_playerData.CardCollection.Exists(c => c.CardID == card.ID))
                {
                    _playerData.CardCollection.Add(new CardSaveData(card.ID));
                    changed = true;
                }
            }
        }
        if (changed) SaveSystem.SaveData(_playerData);
    }

    public bool CanAfford(int cost) => CurrentCoins >= cost;

    public void SpendCoins(int amount)
    {
        CurrentCoins -= amount;
        _playerData.TotalCoins = CurrentCoins;
        SaveSystem.SaveData(_playerData);
        if (MainMenuUI.Instance != null) MainMenuUI.Instance.UpdateCoinDisplay(CurrentCoins);
    }

    public List<CardDefinition> OpenPack(CardPackType type)
    {
        if (AllCards == null || AllCards.Count == 0) return new List<CardDefinition>();

        List<CardDefinition> pulledCards = new List<CardDefinition>();

        for (int i = 0; i < 3; i++)
        {
            float roll = Random.value;
            CardRarity targetRarity = CardRarity.Common;

            if (roll > 0.98f) targetRarity = CardRarity.Corrupted;
            else if (roll > 0.95f) targetRarity = CardRarity.Legendary;
            else if (roll > 0.60f) targetRarity = CardRarity.Rare;

            List<CardDefinition> validPool = AllCards
                .Where(c => c != null && c.PackCategory == type && c.Rarity == targetRarity)
                .ToList();

            if (validPool.Count == 0)
            {
                Debug.LogWarning($"[ShopManager] Pack '{type}' has no {targetRarity} cards - falling back to any rarity.");
                validPool = AllCards
                    .Where(c => c != null && c.PackCategory == type)
                    .ToList();
            }

            if (validPool.Count == 0)
            {
                Debug.LogError($"[ShopManager] Pack '{type}' has NO cards assigned. Check your card database!");
                continue;
            }

            CardDefinition pick = validPool[Random.Range(0, validPool.Count)];
            pulledCards.Add(pick);
            AddCardToCollection(pick.ID);
        }

        SaveSystem.SaveData(_playerData);
        return pulledCards;
    }

    public void AddCardToCollection(string cardID)
    {
        CardSaveData savedCard = _playerData.CardCollection.Find(c => c.CardID == cardID);

        if (savedCard == null)
        {
            _playerData.CardCollection.Add(new CardSaveData(cardID));
        }
        else
        {
            if (savedCard.Level < 5) savedCard.Duplicates++;
            else CurrentCoins += 10;
        }
    }

    public bool TryUpgradeCard(string cardID)
    {
        CardSaveData savedCard = _playerData.CardCollection.Find(c => c.CardID == cardID);
        CardDefinition def = AllCards.Find(c => c.ID == cardID);

        if (savedCard != null && def != null)
        {
            int cost = def.GetUpgradeCost(savedCard.Level);
            int required = def.GetCardsRequired(savedCard.Level);

            if (savedCard.Duplicates >= required && CurrentCoins >= cost)
            {
                CurrentCoins -= cost;
                savedCard.Duplicates -= required;
                savedCard.Level++;

                _playerData.TotalCoins = CurrentCoins;
                SaveSystem.SaveData(_playerData);
                if (MainMenuUI.Instance != null) MainMenuUI.Instance.UpdateCoinDisplay(CurrentCoins);

                return true;
            }
        }
        return false;
    }

    public CardSaveData GetCardData(string id)
    {
        if (_playerData == null) return null;
        return _playerData.CardCollection.Find(c => c.CardID == id);
    }

    public void ResetProgress()
    {
        _playerData = new PlayerData();
        SaveSystem.SaveData(_playerData);
        LoadEconomy();
        if (MainMenuUI.Instance != null) MainMenuUI.Instance.UpdateCoinDisplay(CurrentCoins);
    }
}