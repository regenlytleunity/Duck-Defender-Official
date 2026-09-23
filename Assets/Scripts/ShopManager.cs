using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Owns pack purchases, the level-1 through level-6 collection, pack-specific
/// essence, one-time ascensions and persistent developer resource flags.
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
    public event System.Action OnCollectionChanged;
    public bool InfiniteResources => _playerData != null && _playerData.InfiniteGoldAndEssence;
    public bool InfiniteCopies => _playerData != null && _playerData.InfiniteCopies;
    public int GetEssence(CardPackType pack) => _playerData.GetEssence(pack);

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

    public bool CanAfford(int cost) => cost >= 0 && (InfiniteResources || CurrentCoins >= cost);

    public void SpendCoins(int amount)
    {
        if (!CanAfford(amount)) return;
        if (!InfiniteResources) CurrentCoins -= amount;
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

            if (roll > 0.95f) targetRarity = CardRarity.Legendary;
            else if (roll > 0.60f) targetRarity = CardRarity.Rare;

            List<CardDefinition> validPool = AllCards
                .Where(c => c != null && !c.IsBasic && c.PackCategory == type && c.Rarity == targetRarity)
                .ToList();

            if (validPool.Count == 0)
            {
                Debug.LogWarning($"[ShopManager] Pack '{type}' has no {targetRarity} cards - falling back to any rarity.");
                validPool = AllCards
                    .Where(c => c != null && !c.IsBasic && c.PackCategory == type)
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
        CardDefinition definition = AllCards.Find(c => c != null && c.ID == cardID);
        if (definition == null || definition.IsBasic) return;
        CardSaveData savedCard = _playerData.CardCollection.Find(c => c.CardID == cardID);

        if (savedCard == null)
        {
            _playerData.CardCollection.Add(new CardSaveData(cardID));
        }
        else
        {
            if (!savedCard.IsUnlocked) { savedCard.IsUnlocked = true; savedCard.Level = 1; return; }
            if (savedCard.Level < definition.MaxLevel && !savedCard.IsAscended)
                savedCard.Duplicates = (int)System.Math.Min(int.MaxValue, (long)savedCard.Duplicates + 1);
            else _playerData.AddEssence(definition.PackCategory, definition.EssencePerCopy);
        }
    }

    public bool TryUpgradeCard(string cardID)
    {
        CardSaveData savedCard = _playerData.CardCollection.Find(c => c.CardID == cardID);
        CardDefinition def = AllCards.Find(c => c != null && c.ID == cardID);

        if (savedCard != null && def != null && !def.IsBasic && savedCard.IsUnlocked && !savedCard.IsAscended && savedCard.Level < def.MaxLevel)
        {
            int cost = def.GetUpgradeCost(savedCard.Level);
            int required = def.GetCardsRequired(savedCard.Level);

            if ((InfiniteCopies || savedCard.Duplicates >= required) && CanAfford(cost))
            {
                if (!InfiniteResources) CurrentCoins -= cost;
                if (!InfiniteCopies) savedCard.Duplicates -= required;
                savedCard.Level++;
                if (savedCard.Level >= def.MaxLevel)
                {
                    // Convert banked surplus too; no stranded copies at max level.
                    long essence = (long)savedCard.Duplicates * def.EssencePerCopy;
                    _playerData.AddEssence(def.PackCategory, (int)System.Math.Min(int.MaxValue, essence));
                    savedCard.Duplicates = 0;
                }

                _playerData.TotalCoins = CurrentCoins;
                SaveSystem.SaveData(_playerData);
                if (MainMenuUI.Instance != null) MainMenuUI.Instance.UpdateCoinDisplay(CurrentCoins);
                OnCollectionChanged?.Invoke();

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
        _playerData = PlayerData.CreateNew();
        SaveSystem.SaveData(_playerData);
        LoadEconomy();
        if (MainMenuUI.Instance != null) MainMenuUI.Instance.UpdateCoinDisplay(CurrentCoins);
        OnCollectionChanged?.Invoke();
    }

    public bool TryAscendCard(string id)
    {
        var card = AllCards.Find(c => c != null && c.ID == id);
        var saved = GetCardData(id);
        if (card == null || saved == null || !saved.IsUnlocked || saved.IsAscended ||
            saved.Level < card.MaxLevel || card.Ascension == CardAscension.None) return false;
        if (!InfiniteResources && GetEssence(card.PackCategory) < card.AscensionCost) return false;
        if (!InfiniteResources) _playerData.AddEssence(card.PackCategory, -card.AscensionCost);
        saved.IsAscended = true;
        SaveSystem.SaveData(_playerData);
        OnCollectionChanged?.Invoke();
        return true;
    }

    public List<CardDefinition> TryBuyPacks(ShopPackDefinition pack, int count)
    {
        if (pack == null || (count != 1 && count != 3) || pack.Cost < 0 ||
            (long)pack.Cost * count > int.MaxValue ||
            AllCards == null || !AllCards.Any(c => c != null && !c.IsBasic && c.PackCategory == pack.PackType)) return null;
        int price = pack.Cost * count;
        if (!CanAfford(price)) return null;
        // Charge and grant together, before the cosmetic opening animation.
        if (!InfiniteResources) CurrentCoins -= price;
        _playerData.TotalCoins = CurrentCoins;
        var cards = new List<CardDefinition>();
        for (int i = 0; i < count; i++) cards.AddRange(OpenPack(pack.PackType));
        SaveSystem.SaveData(_playerData);
        OnCollectionChanged?.Invoke();
        return cards;
    }

    public bool ExecuteDeveloperCode(string code)
    {
        bool max = code == "3619" || code == "5942";
        bool unlock = code == "8672";
        if (code != "9845" && code != "0381" && !max && !unlock) return false;
        if (code == "9845" || code == "5942") _playerData.InfiniteGoldAndEssence = true;
        if (code == "0381") _playerData.InfiniteCopies = true;
        if (max || unlock || code == "0381")
        {
            foreach (var card in AllCards)
            {
                if (card == null || card.IsBasic) continue;
                var saved = GetCardData(card.ID);
                if (saved == null) { saved = new CardSaveData(card.ID); _playerData.CardCollection.Add(saved); }
                saved.IsUnlocked = true;
                if (max) { saved.Level = card.MaxLevel; saved.IsAscended = card.Ascension != CardAscension.None; saved.Duplicates = 0; }
                // Unlock-all intentionally never downgrades already upgraded cards.
            }
        }
        SaveSystem.SaveData(_playerData);
        if (MainMenuUI.Instance != null) MainMenuUI.Instance.UpdateCoinDisplay(CurrentCoins);
        OnCollectionChanged?.Invoke();
        return true;
    }
}
