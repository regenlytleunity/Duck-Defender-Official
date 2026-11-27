using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ItemReworkVerification
{
    [MenuItem("Duck Defender/Item rework/Register Key Finder")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene and exit Play Mode first.");
        string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        const string path = "Assets/Cards/Upgrades/Gadget Upgrades/Key Finder.asset";
        var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ID = "gad_key_finder"; card.CardName = "Key Finder";
            card.Description = "Increases enemy key drop chance by {0}%.";
            card.PackCategory = CardPackType.Gadget; card.Rarity = CardRarity.Rare;
            card.MaxLevel = 6; card.Ascension = CardAscension.KeySpecialist;
            card.AscendedName = "Key Specialist"; card.AscensionCost = 10;
            card.AscendedDescription = "Increases key drop chance by 50%. Collecting a key grants +100% damage for 5 seconds.";
            card.Modifiers = new System.Collections.Generic.List<CardStatModifier> {
                new CardStatModifier { StatType = StatType.KeyDropChance, BaseAmount = .05f, AmountPerShopLevel = .05f, DisplayMultiplier = 100, StackMode = InRunStackMode.Custom }
            };
            AssetDatabase.CreateAsset(card, path);
        }
        foreach (string scenePath in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/SampleScene.unity" })
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var shop in root.GetComponentsInChildren<ShopManager>(true))
                    if (!shop.AllCards.Contains(card)) { shop.AllCards.Add(card); EditorUtility.SetDirty(shop); }
                foreach (var manager in root.GetComponentsInChildren<CardManager>(true))
                    if (!manager.AllCards.Contains(card)) { manager.AllCards.Add(card); EditorUtility.SetDirty(manager); }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    public static string CheckEconomy()
    {
        if (!EditorApplication.isPlaying || string.IsNullOrEmpty(SaveSystem.VerificationSavePath))
            throw new InvalidOperationException("Start isolated verification first.");
        var shop = ShopManager.Instance;
        var old = new PlayerData { ProgressionVersion = 2, TotalCoins = 9999 };
        SaveSystem.SaveData(old, true, true);
        Check(SaveSystem.LoadData().ProgressionVersion == 3 && SaveSystem.LoadData().TotalCoins == 0, "Version reset");
        var data = PlayerData.CreateNew(); data.NormalizeShopData();
        data.Tutorial.Stage = TutorialStage.Complete;
        data.TotalCoins = 100000; data.Keys = 100;
        SaveSystem.SaveData(data, true, true); shop.LoadEconomy();
        Check(shop.Loadout().Items.Length == 2 && shop.Loadout(3).Items.Length == 2, "Two slots");
        Check(!shop.EquipItem(1, 0), "Locked item rejected");
        int id = shop.OpenItemBox();
        Check(ItemCatalog.Valid(id) && shop.Progress.Keys == 99 && shop.Progress.ItemLevels[id] == 1, "Box grants unlock");
        foreach (int item in ItemCatalog.Available) shop.Progress.ItemLevels[item] = 1;
        id = shop.OpenItemBox();
        Check(shop.Progress.ItemShards[id] == 10 && shop.Progress.Keys == 98, "Duplicate grants ten shards");
        Check(shop.UpgradeItem(id) && shop.Progress.ItemLevels[id] == 2 && shop.Progress.ItemShards[id] == 0, "Upgrade charges shards");
        Check(!shop.UpgradeItem(id), "Missing shards rejected");
        shop.Progress.ItemShards[id] = 1000;
        for (int level = 2; level < 10; level++)
        {
            int coins = shop.CurrentCoins, shards = shop.Progress.ItemShards[id];
            Check(shop.UpgradeItem(id), "Upgrade through level ten");
            Check(coins - shop.CurrentCoins == ItemCatalog.GoldCosts[level] && shards - shop.Progress.ItemShards[id] == ItemCatalog.ShardCosts[level], "Exact upgrade costs");
        }
        Check(!shop.UpgradeItem(id), "Item cap");
        Check(shop.EquipItem(id, 0) && shop.EquipItem(id, 1) && shop.Loadout().Items[0] == 0, "Move unique equipped item");
        Check(shop.EquipItem(id, 0, 0) && shop.EquipItem(id, 0, 1), "Co-op shares unlocked item");
        for (int i = 0; i < 5; i++) Check(shop.BuyKeyUpgrade(), "Key upgrade");
        Check(!shop.BuyKeyUpgrade() && shop.Progress.KeyDropLevel == 5, "Key upgrade cap");
        shop.RefreshDaily();
        Check(shop.Progress.Daily.Kinds.Distinct().Count() == 3, "Unique daily objectives");
        for (int i = 0; i < 3; i++)
            DailyQuests.Record(shop.Progress, (DailyQuestKind)shop.Progress.Daily.Kinds[i], 10000);
        int bonus = shop.Progress.Daily.Bonus;
        shop.Progress.Daily.Rewards[bonus] = 0;
        int keys = shop.Progress.Keys;
        Check(shop.ClaimQuest(bonus, out var cards, out var text) && shop.Progress.Keys == keys + 1, "Guaranteed bonus key");
        Check(!shop.ClaimQuest(bonus, out cards, out text), "Claim exactly once");
        Check(DailyQuests.Day(18000) == 0 && DailyQuests.Day(17999) == -1, "Midnight EST boundary");
        var finder = shop.AllCards.Single(c => c != null && c.ID == "gad_key_finder");
        shop.AddCardToCollection(finder.ID); var saved = shop.GetCardData(finder.ID); saved.Level = finder.MaxLevel;
        keys = shop.Progress.Keys;
        Check(shop.TryAscendCard(finder.ID) && shop.Progress.Keys == keys - 10, "Ten-key ascension");
        Check(!shop.TryAscendCard(finder.ID), "Ascend exactly once");
        int copies = saved.Duplicates;
        shop.AddCardToCollection(finder.ID);
        Check(saved.Duplicates == copies && shop.GetEssence(CardPackType.Gadget) == 0, "Maxed duplicates grant nothing");
        shop.LoadEconomy();
        Check(shop.Progress.ItemLevels[id] == 10 && shop.GetCardData(finder.ID).IsAscended, "Persistence");
        shop.ExecuteDeveloperCode("1303"); shop.ExecuteDeveloperCode("1302");
        shop.EquipItem(1, 0); shop.EquipItem(2, 1);
        return "PASS item progression, costs, co-op loadouts, daily rewards, reset boundary, ascension and persistence";
    }
}
