using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Owns pack purchases, the level-1 through level-6 collection, keys,
/// one-time ascensions, reusable items and daily rewards.
/// </summary>
public partial class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("Database")]
    public List<CardDefinition> AllCards;
    public List<ShopPackDefinition> AvailablePacks;

    [Header("State")]
    public int CurrentCoins;

    private PlayerData _playerData;
    public event System.Action OnCollectionChanged;
    public bool InfiniteResources => false; // Retired resource code, retained for existing UI/tool callers.
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
        _playerData.NormalizeShopData();
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

            CardDefinition pick = PickWeightedCard(validPool);
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
        }
    }

    public bool TryUpgradeCard(string cardID)
    {
        if (!TutorialCanUpgrade(cardID) || _playerData.Tutorial.ItemActive) return false;
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
                    // Maximum-level duplicates no longer grant currency.
                    savedCard.Duplicates = 0;
                }

                _playerData.TotalCoins = CurrentCoins;
                if (_playerData.Tutorial.CoreActive) _playerData.Tutorial.Stage = TutorialStage.IndexReturn;
                SaveSystem.SaveData(_playerData, updateTutorial: true);
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
        SaveSystem.SaveData(_playerData, true, true);
        LoadEconomy();
        if (MainMenuUI.Instance != null) MainMenuUI.Instance.UpdateCoinDisplay(CurrentCoins);
        OnCollectionChanged?.Invoke();
    }

    public bool TryAscendCard(string id)
    {
        if (TutorialBlocksShopping) return false;
        var card = AllCards.Find(c => c != null && c.ID == id);
        var saved = GetCardData(id);
        if (card == null || saved == null || !saved.IsUnlocked || saved.IsAscended ||
            saved.Level < card.MaxLevel || card.Ascension == CardAscension.None) return false;
        if (!InfiniteResources && _playerData.Keys < 10) return false;
        if (!InfiniteResources) _playerData.Keys -= 10;
        saved.IsAscended = true;
        SaveSystem.SaveData(_playerData);
        OnCollectionChanged?.Invoke();
        return true;
    }

    public List<CardDefinition> TryBuyPacks(ShopPackDefinition pack, int count)
    {
        if (_playerData.Tutorial.CoreActive) return BuyTutorialPack(pack, count);
        if (_playerData.Tutorial.ItemActive) return null;
        if (pack == null || !CanOpenPackCount(count) || pack.Cost < 0 ||
            (long)pack.Cost * count > int.MaxValue ||
            AllCards == null || !AllCards.Any(c => c != null && !c.IsBasic && c.PackCategory == pack.PackType)) return null;
        int price = pack.Cost * count;
        if (!CanAfford(price)) return null;
        // Charge and grant together, before the cosmetic opening animation.
        if (!InfiniteResources) CurrentCoins -= price;
        _playerData.TotalCoins = CurrentCoins;
        var cards = new List<CardDefinition>();
        for (int i = 0; i < count; i++) cards.AddRange(OpenPack(pack.PackType));
        DailyQuests.Record(_playerData, DailyQuestKind.Packs, count);
        SaveSystem.SaveData(_playerData);
        OnCollectionChanged?.Invoke();
        return cards;
    }

    public bool ExecuteDeveloperCode(string code)
    {
        if (TutorialBlocksShopping) return false;
        if (ExecuteReworkCode(code)) return true;
        bool max = code == "3619";
        bool unlock = code == "8672";
        bool levelTwoAndGold = code == "2002";
        if (code != "0381" && !max && !unlock && !levelTwoAndGold) return false;
        if (code == "0381") _playerData.InfiniteCopies = true;
        if (levelTwoAndGold)
        {
            CurrentCoins = (int)System.Math.Min(int.MaxValue, (long)CurrentCoins + 1000000);
            _playerData.TotalCoins = CurrentCoins;
        }
        if (max || unlock || code == "0381" || levelTwoAndGold)
        {
            foreach (var card in AllCards)
            {
                if (card == null || card.IsBasic) continue;
                var saved = GetCardData(card.ID);
                if (saved == null) { saved = new CardSaveData(card.ID); _playerData.CardCollection.Add(saved); }
                saved.IsUnlocked = true;
                if (max) { saved.Level = card.MaxLevel; saved.IsAscended = card.Ascension != CardAscension.None; saved.Duplicates = 0; }
                else if (levelTwoAndGold) saved.Level = Mathf.Max(saved.Level, Mathf.Min(2, card.MaxLevel));
                // Unlock-all intentionally never downgrades already upgraded cards.
            }
        }
        SaveSystem.SaveData(_playerData);
        if (MainMenuUI.Instance != null) MainMenuUI.Instance.UpdateCoinDisplay(CurrentCoins);
        OnCollectionChanged?.Invoke();
        return true;
    }
}
