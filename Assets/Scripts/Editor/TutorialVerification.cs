using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TutorialVerification
{
    [MenuItem("Duck Defender/Tutorial/Run isolated Play Mode verification")]
    public static void StartPlay()
    {
        UIUpdateVerification.StartPlay(true);
        SessionState.SetBool(TutorialPlayProbe.SessionKey, true);
    }

    static int _checks;
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); _checks++; }

    [MenuItem("Duck Defender/Tutorial/Verify save and economy safeguards")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run this check outside Play Mode.");
        string previous = SaveSystem.VerificationSavePath;
        string path = Path.GetFullPath("tmp/update9/isolated-save.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            _checks = 0; SaveSystem.VerificationSavePath = path;
            var old = new PlayerData { ProgressionVersion = PlayerData.CurrentVersion, TotalCoins = 321 };
            old.CardCollection.Add(new CardSaveData("mun_faster_firing") { Level = 4 });
            SaveSystem.SaveData(old, true, true);
            Check(!SaveSystem.LoadData().Tutorial.CoreActive && SaveSystem.LoadData().TotalCoins == 321, "Existing saves must not be forced into the core tutorial or reset.");
            var fresh = PlayerData.CreateNew();
            Check(fresh.Tutorial.CoreActive && fresh.Tutorial.Stage == TutorialStage.Basics, "New saves start the tutorial.");
            SaveSystem.SaveData(fresh, true, true);
            var stale = SaveSystem.LoadData();
            fresh.Tutorial.Stage = TutorialStage.Waves;
            SaveSystem.SaveData(fresh, updateTutorial: true);
            stale.TotalCoins = 23; SaveSystem.SaveData(stale);
            Check(SaveSystem.LoadData().Tutorial.Stage == TutorialStage.Waves, "Cached economy writes cannot roll back the tutorial.");
            var data = SaveSystem.LoadData();
            Check(TutorialProgress.GrantWaveReward(data) && data.TotalCoins == 223, "Wave reward is exactly 200.");
            Check(!TutorialProgress.GrantWaveReward(data) && data.TotalCoins == 223, "Duplicate wave completion cannot pay twice.");
            SaveSystem.SaveData(data, updateTutorial: true);
            stale.ShowTips = true; SaveSystem.SaveData(stale, true);
            Check(SaveSystem.LoadData().Tutorial.Stage == TutorialStage.Shop, "Tip updates preserve tutorial progress.");

            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var shop = UnityEngine.Object.FindFirstObjectByType<ShopManager>(); ShopManager.Instance = shop;
            Check(shop != null && shop.AvailablePacks.Count == 4, "All four tutorial packs are assigned.");
            foreach (var pack in shop.AvailablePacks)
            {
                data = PlayerData.CreateNew(); data.TotalCoins = 257; data.Tutorial.Stage = TutorialStage.Shop;
                SaveSystem.SaveData(data, true, true); shop.LoadEconomy();
                Check(shop.TryBuyPacks(pack, 3) == null && shop.CurrentCoins == 257, "Bulk purchase blocked during tutorial.");
                var cards = shop.TryBuyPacks(pack, 1);
                Check(cards != null && cards.Count == 3 && cards[0] == cards[1] && cards[2] != cards[0], "Pack grants two starter duplicates and one new card: " + pack.PackName);
                string id = shop.Progress.Tutorial.UpgradeCardID;
                Check(shop.GetCardData(id).Duplicates == 2 && shop.GetCardData(cards[2].ID).IsUnlocked, "Guaranteed copies and unlock applied.");
                Check(shop.CurrentCoins == 157 && shop.Progress.Tutorial.Stage == TutorialStage.PackPurchased, "Pack charge/checkpoint saved together.");
                Check(shop.TryBuyPacks(pack, 1) == null && shop.CurrentCoins == 157, "Repeat purchase rejected after interruption.");
                shop.LoadEconomy();
                Check(shop.Progress.Tutorial.UpgradeCardID == id && shop.GetCardData(id).Duplicates == 2, "Pack recovery survives reload.");
                Check(!shop.TryUpgradeCard(id) && !shop.BuyStartingSlot(), "Cannot spend reserved coins before upgrade lesson.");
                shop.Progress.Tutorial.Stage = TutorialStage.Upgrade;
                SaveSystem.SaveData(shop.Progress, updateTutorial: true);
                Check(!shop.TryUpgradeCard(PlayerData.StarterIDs.First(s => s != id)), "Unrelated upgrade rejected.");
                Check(shop.TryUpgradeCard(id) && shop.CurrentCoins == 57 && shop.GetCardData(id).Level == 2, "Pack and upgrade spend exactly the 200-coin grant.");
                Check(!shop.TryUpgradeCard(id) && shop.Progress.Tutorial.Stage == TutorialStage.IndexReturn, "Upgrade cannot be applied twice.");
                var collection = GameDifficulty.ReadCollection(shop.Progress, shop.AllCards);
                Check(collection.UnlockedCards == 9 && collection.CombinedLevels == 10, "Actual post-tutorial collection has 9 cards / 10 levels: " + pack.PackName);
            }
            data = PlayerData.CreateNew(); data.Tutorial.Stage = TutorialStage.Complete;
            data.Tutorial.HasObtainedItem = true; data.Tutorial.Items = ItemTutorialStage.Equip;
            data.ItemInventory[(int)ItemKind.Bomb] = 1;
            SaveSystem.SaveData(data, true, true); shop.LoadEconomy();
            Check(!shop.SellItem((int)ItemKind.Bomb), "Last tutorial item cannot be sold.");
            Check(shop.EquipItem((int)ItemKind.Bomb, 3), "Item can equip to any of the four slots.");
            shop.LoadEconomy();
            Check(shop.Progress.Tutorial.Items == ItemTutorialStage.Controls && shop.Loadout().Items[3] == (int)ItemKind.Bomb, "Equip and item checkpoint survive reload together.");
            Check(!shop.EquipItem(0, 3) && !shop.SellItem((int)ItemKind.Bomb, -1, 3), "Cannot remove/sell item before completing controls lesson.");
            Check(!shop.BuyPermanentUpgrade(PermanentUpgrade.Damage), "Other purchases blocked during item tutorial.");
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
            Check(menu.UIFont == AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/DuckDefenderTestFontv2.asset"), "Tutorial inherits the original bitmap font.");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var cardsManager = UnityEngine.Object.FindFirstObjectByType<CardManager>();
            foreach (string id in new[] { "bas_health", "bas_damage", "bas_mobility" }) Check(cardsManager.AllCards.Any(c => c != null && c.ID == id), "Required practice card assigned: " + id);
            var waves = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
            Check(waves.EnemyPrefabs.Any(p => p != null && p.GetComponent<EnemyBase>()?.EnemyKind == "ground"), "Ground practice enemy prefab assigned.");
            var plan = new System.Collections.Generic.List<WaveManager.SpawnRequest>();
            for (int wave = 1; wave <= 5; wave++)
                Check(waves.TryBuildWave(wave, null, plan, out _) && plan.Count == 2 + 2 * wave, "Normal wave totals remain unchanged: " + wave);
            var reduce = typeof(WaveManager).GetMethod("ReduceTutorialWave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            foreach (int count in new[] { 1, 3, 6 })
            {
                plan.Clear();
                for (int i = 0; i < count; i++) plan.Add(new WaveManager.SpawnRequest { Introduction = i == 0 });
                reduce.Invoke(null, new object[] { plan });
                Check(plan.Count == (count + 1) / 2 && plan[0].Introduction, "Tutorial reduction retains the introduction and rounds up: " + count);
            }
            File.WriteAllText(Path.GetFullPath("tmp/update9/verification.txt"), "PASS: " + _checks + " tutorial save, economy and serialized-reference checks.");
            Debug.Log("PASS: " + _checks + " tutorial checks.");
        }
        finally
        {
            SaveSystem.VerificationSavePath = previous;
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            ShopManager.Instance = UnityEngine.Object.FindFirstObjectByType<ShopManager>();
        }
    }
}
