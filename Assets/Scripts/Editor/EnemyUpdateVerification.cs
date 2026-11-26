using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyUpdateVerification
{
    static EnemyUpdateVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                SessionState.EraseString(EnemyUpdatePlayProbe.SessionKey);
        };
    }

    [MenuItem("Duck Defender/Enemies/Run Play Mode Verification")]
    public static void RunPlayMode()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save or discard scene changes before running verification.");
        SessionState.SetString(EnemyUpdatePlayProbe.SessionKey, Path.Combine(Path.GetTempPath(), "duck-enemy-play-" + Guid.NewGuid().ToString("N") + ".json"));
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }
    static int _checks;
    static readonly List<GameObject> _objects = new List<GameObject>();
    static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new InvalidOperationException("Enemy update verification: " + message);
    }
    static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message + " (" + actual + " vs " + expected + ")");
    static T Make<T>() where T : Component
    {
        var go = new GameObject("Enemy verification") { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
    static EnemyBase Enemy(bool tank, bool elite = false)
    {
        EnemyBase enemy = tank ? Make<TankEnemy>() : Make<SwarmerEnemy>();
        enemy.IsElite = elite; enemy.SpawnProtectionSeconds = 0; enemy.BaseHealth = 100;
        enemy.gameObject.AddComponent<Rigidbody2D>();
        enemy.gameObject.AddComponent<BoxCollider2D>();
        enemy.gameObject.AddComponent<SpriteRenderer>();
        enemy.Initialize(1);
        EnemyBase.ActiveEnemies.Add(enemy);
        return enemy;
    }
    static void Set(object target, Type type, string field, object value) => type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [MenuItem("Duck Defender/Enemies/Verify collection difficulty")]
    public static string VerifyCollectionDifficulty()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
        var oldWave = WaveManager.Instance; var oldStats = PlayerStats.Instance;
        var oldRegistry = new List<EnemyBase>(EnemyBase.ActiveEnemies);
        _checks = 0;
        try
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var cards = UnityEngine.Object.FindFirstObjectByType<CardManager>().AllCards;
            var waves = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
            WaveManager.Instance = waves; PlayerStats.Instance = null;
            var eligible = cards.Where(c => c != null && !c.IsBasic).GroupBy(c => c.ID).Select(g => g.First()).ToList();
            Check(eligible.Count == 47 && cards.Count(c => c != null && c.IsBasic) == 4, "Actual gameplay registry has 47 collection cards and four Basics (51 total).");
            var fresh = PlayerData.CreateNew();
            var scaling = GameDifficulty.ReadCollection(fresh, cards);
            Check(scaling.UnlockedCards == 8 && scaling.CombinedLevels == 8 && scaling.Score == 0, "Fresh starter collection is below the tutorial baseline.");
            fresh.CardCollection[0].Level = 2;
            foreach (var card in cards.Where(c => c != null && c.IsBasic)) fresh.CardCollection.Add(new CardSaveData(card.ID) { Level = 6 });
            fresh.CardCollection.Add(new CardSaveData("unregistered_card") { Level = 6 });
            var locked = eligible.First(c => fresh.CardCollection.All(d => d.CardID != c.ID));
            fresh.CardCollection.Add(new CardSaveData(locked.ID) { IsUnlocked = false, Level = 6 });
            fresh.CardCollection.Add(new CardSaveData(fresh.CardCollection[0].CardID) { Level = 6 });
            scaling = GameDifficulty.ReadCollection(fresh, cards.Concat(cards));
            Check(scaling.UnlockedCards == 8 && scaling.CombinedLevels == 9 && scaling.Score == 0, "Basics, locked/unregistered and duplicate IDs cannot inflate the 8/9 baseline.");
            GameDifficulty.BeginRun(fresh, cards);
            fresh.CardCollection[0].Level = 6;
            Near(GameDifficulty.HealthAtWave(2, 1), 2, "Changing saved levels after capture does not change the current run.");
            int[] counts = { 8, 51, 51, 51, 30, 30, 30, 51 };
            int[] totals = { 9, 51, 76, 102, 120, 135, 150, 204 };
            float[] first = { 2, 4.86f, 6.31f, 7.81f, 5.99f, 6.50f, 7.01f, 13.70f };
            float[] twentieth = { 21.50f, 42.97f, 53.80f, 65.06f, 51.45f, 55.28f, 59.10f, 109.22f };
            for (int i = 0; i < counts.Length; i++)
            {
                scaling = new GameDifficulty.CollectionScaling(counts[i], totals[i]);
                Check(Mathf.Abs(scaling.Health(2, 1) - first[i]) <= .0051f, "PDF wave 1 reference " + i);
                Check(Mathf.Abs(scaling.Health(2, 20) - twentieth[i]) <= .0051f, "PDF wave 20 reference " + i);
            }
            Near(new GameDifficulty.CollectionScaling(20, 60).Health(2, 20), new GameDifficulty.CollectionScaling(30, 40).Health(2, 20), "Equal count-times-level products give equal difficulty.");
            int[] boundary = { 1, 2, 10, 11, 15, 16, 20, 21, 25, 26, 30, 31, 40, 41, 50, 51, 100 };
            float[] sum = { 0, .5f, 4.5f, 5.5f, 9.5f, 11.5f, 19.5f, 22.5f, 34.5f, 38.5f, 54.5f, 60.5f, 114.5f, 124.5f, 214.5f, 234.5f, 1214.5f };
            var high = new PlayerData();
            foreach (var card in eligible) high.CardCollection.Add(new CardSaveData(card.ID) { Level = 4 });
            GameDifficulty.BeginRun(high, cards);
            Check(GameDifficulty.Current.UnlockedCards == 47 && GameDifficulty.Current.CombinedLevels == 188, "Collection levels come from registered persistent data.");
            for (int i = 0; i < boundary.Length; i++) Near(WaveManager.HealthIncreaseAtWave(boundary[i]), sum[i], "Unscaled boundary " + boundary[i]);
            var parent = new GameObject("Collection verification"); parent.SetActive(false); _objects.Add(parent);
            var stats = Make<PlayerStats>(); PlayerStats.Instance = stats;
            foreach (var prefab in waves.EnemyPrefabs)
            {
                var enemy = UnityEngine.Object.Instantiate(prefab, parent.transform).GetComponent<EnemyBase>();
                enemy.HealthBarPrefab = null; enemy.SpawnProtectionSeconds = 0;
                float authoredHealth = enemy.BaseHealth;
                foreach (int wave in new[] { 1, 2, 20, 51 })
                {
                    stats.EnemyHealthMissingPercent = 0;
                    enemy.IsElite = false; enemy.Initialize(wave);
                    float rawHealth = enemy is TankEnemy ? 3 * GameDifficulty.HealthAtWave(2, wave) : GameDifficulty.HealthAtWave(enemy.EnemyKind == "ground" ? 2 : 1, wave);
                    float expected = Mathf.Max(1, Mathf.Floor(rawHealth));
                    Near(enemy.MaxHealth, expected, "Spawn health " + enemy.EnemyKind + " wave " + wave);
                    enemy.TakeItemDamage(.5f, null);
                    Near(enemy.HealthRemaining, expected - .5f, "Fractional damage retained.");
                    enemy.gameObject.SetActive(false); enemy.Initialize(wave);
                    Near(enemy.HealthRemaining, expected, "Reinitialization uses unscaled values, not remaining HP.");
                    stats.EnemyHealthMissingPercent = .37f; enemy.Initialize(wave);
                    Near(enemy.MaxHealth, expected, "Sabotage does not change maximum HP.");
                    Near(enemy.HealthRemaining, Mathf.Max(1, Mathf.Floor(expected * .63f)), "Sabotage spawn HP rounds down.");
                    enemy.Initialize(wave);
                    Near(enemy.HealthRemaining, Mathf.Max(1, Mathf.Floor(expected * .63f)), "Sabotage does not compound on reuse.");
                    enemy.IsElite = true; enemy.Initialize(wave);
                    float elite = enemy.EnemyKind == "ground" ? 1.5f : enemy.EnemyKind == "lobber" ? 1.25f : 1;
                    Near(enemy.MaxHealth, Mathf.Max(1, Mathf.Floor(rawHealth * elite)), "Elite health rounds after its multiplier.");
                    var size = enemy.transform.localScale; var color = enemy.GetComponent<SpriteRenderer>().color;
                    enemy.Initialize(wave);
                    Check(enemy.transform.localScale == size && enemy.GetComponent<SpriteRenderer>().color == color, "Repeated elite initialization preserves size and tint.");
                    Near(enemy.BaseHealth, authoredHealth, "Authored base health never mutated.");
                }
            }
            GameDifficulty.Select(2, false);
            Check(GameDifficulty.DamageMultiplier == 1 && GameDifficulty.SpeedMultiplier == 1 && GameDifficulty.CoinMultiplier == 1, "Legacy Hard selection cannot change combat or economy.");
            string result = "PASS: " + _checks + " collection difficulty checks.";
            Directory.CreateDirectory("tmp/update9"); File.WriteAllText("tmp/update9/difficulty-verification.txt", result);
            return result;
        }
        finally
        {
            foreach (var go in _objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _objects.Clear(); EnemyBase.ActiveEnemies.Clear(); EnemyBase.ActiveEnemies.AddRange(oldRegistry);
            GameDifficulty.Load(); WaveManager.Instance = oldWave; PlayerStats.Instance = oldStats;
            UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    [MenuItem("Duck Defender/Enemies/Run Isolated Verification")]
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var oldWave = WaveManager.Instance; var oldStats = PlayerStats.Instance;
        var oldLevel = LevelManager.Instance; var oldAudio = AudioManager.Instance;
        var oldRegistry = new List<EnemyBase>(EnemyBase.ActiveEnemies);
        var oldRandom = UnityEngine.Random.state;
        string oldPath = SaveSystem.VerificationSavePath;
        string path = Path.Combine(Path.GetTempPath(), "duck-enemy-check-" + Guid.NewGuid().ToString("N") + ".json");
        var definitions = new List<WaveDefinition>();
        _checks = 0;
        try
        {
            WaveManager.Instance = null; PlayerStats.Instance = null; LevelManager.Instance = null; AudioManager.Instance = null;
            EnemyBase.ActiveEnemies.Clear(); SaveSystem.VerificationSavePath = path;
            int[] waves = { 1, 2, 3, 10, 11, 15, 16, 20, 21, 25, 26, 30, 31, 40, 41, 50, 51, 100 };
            float[] increases = { 0, .5f, 1, 4.5f, 5.5f, 9.5f, 11.5f, 19.5f, 22.5f, 34.5f, 38.5f, 54.5f, 60.5f, 114.5f, 124.5f, 214.5f, 234.5f, 1214.5f };
            for (int i = 0; i < waves.Length; i++) Near(WaveManager.HealthIncreaseAtWave(waves[i]), increases[i], "health table wave " + waves[i]);

            var normal = Enemy(false);
            var elite = Enemy(false, true);
            Near(elite.MaxHealth, Mathf.Floor(normal.MaxHealth * 1.5f), "elite ground health");
            var tank = (TankEnemy)Enemy(true);
            Near(tank.MaxHealth, 6, "tank uses three times basic ground health");
            tank.transform.position = new Vector3(20, 0);
            normal.transform.position = tank.transform.position + Vector3.right;
            elite.transform.position = tank.transform.position + Vector3.right * 2;
            tank.AcquireChains();
            Check(normal.Protector == tank && elite.Protector == tank, "tank acquires nearest non-tanks");
            Check(normal.GetComponent<SpriteRenderer>().color.b > normal.GetComponent<SpriteRenderer>().color.r, "protected allies have a blue tint");
            var tank2 = (TankEnemy)Enemy(true, true);
            tank2.transform.position = tank.transform.position;
            tank2.AcquireChains();
            Check(tank2.LifetimeLinks == 0 && tank.Protector == null, "exclusive ownership and tanks excluded");
            normal.TakeDamage(1);
            Near(normal.HealthRemaining, 99.5f, "half-damage protection");
            Near(tank.HealthRemaining, 5.5f, "normal tank absorbs half damage exactly");

            tank.enabled = false;
            typeof(TankEnemy).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tank, null);
            Check(normal.GetComponent<SpriteRenderer>().color == Color.white, "releasing a chain restores the normal tint");
            tank2.AcquireChains();
            elite.TakeDamage(3);
            Near(tank2.ShieldHealth, 1.5f, "elite converts redirected damage to shield");
            Near(tank2.HealthRemaining, 6, "elite shield gain does not cost health");
            tank2.TakeDamage(2);
            Near(tank2.ShieldHealth, 0, "shield depleted first");
            Near(tank2.HealthRemaining, 5.5f, "shield overflow reaches health");
            tank2.OnProtectedEnemyKilled(normal);
            tank2.OnProtectedEnemyKilled(normal);
            Check(tank2.DefeatedLinks == 1, "ally death cannot grant duplicate stacks");

            EnemyBase.ActiveEnemies.Clear();
            var threeTank = (TankEnemy)Enemy(true, true);
            for (int i = 0; i < 5; i++) { var ally = Enemy(false); ally.transform.position = Vector3.right * (i + 1); }
            threeTank.AcquireChains();
            Check(threeTank.LifetimeLinks == 3, "three lifetime links");
            var linked = EnemyBase.ActiveEnemies.Find(e => e.Protector == threeTank);
            threeTank.OnProtectedEnemyKilled(linked);
            threeTank.AcquireChains();
            Check(threeTank.LifetimeLinks == 3, "no replacement after linked ally dies");

            var manager = Make<WaveManager>();
            manager.SpawnPoints = new[] { manager.transform };
            manager.EnemyPrefabs = new[] {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Fast Enemy.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Flying Enemy.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Tank Enemy.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Lobber Enemy.prefab")
            };
            var plan = new List<WaveManager.SpawnRequest>();
            for (int wave = 1; wave <= 25; wave++)
            {
                Check(manager.TryBuildWave(wave, null, plan, out string error), "intro wave valid: " + error);
                Check(plan.Count == 2 + wave * 2, "exact default enemy count");
                Check(plan[0].Prefab.GetComponent<TankEnemy>() == null, "tanks never spawn first");
                foreach (var spawn in plan)
                {
                    string kind = spawn.Prefab.GetComponent<EnemyBase>().EnemyKind;
                    int k = kind == "ground" ? 0 : kind == "flying" ? 1 : kind == "tank" ? 2 : 3;
                    Check(wave >= manager.NormalUnlockWaves[k], "normal never before introduction");
                    Check(!spawn.Elite || wave >= manager.EliteUnlockWaves[k], "elite never before introduction");
                }
            }
            var def = ScriptableObject.CreateInstance<WaveDefinition>(); definitions.Add(def);
            def.Mode = WaveSpawnMode.Mixed; def.EnemyCount = 8;
            def.OrderedSpawns.Add(new WaveSpawnGroup { Prefab = manager.EnemyPrefabs[2], Count = 2, Elite = true, SpawnPointIndex = 0, DelayBefore = 2, Interval = .7f });
            def.RandomEnemies.Add(new WaveEnemyOption { Prefab = manager.EnemyPrefabs[0], Weight = 100 });
            def.RandomEnemies.Add(new WaveEnemyOption { Prefab = manager.EnemyPrefabs[1], Weight = 0 });
            Check(manager.TryBuildWave(99, def, plan, out _), "mixed wave valid");
            Check(plan.Count == 8 && plan[0].Prefab == manager.EnemyPrefabs[0] && plan[1].Elite && plan[2].Elite && plan[1].DelayBefore == 2 && plan[2].DelayBefore == 0, "ordered count, variant, timing");
            for (int i = 3; i < plan.Count; i++) Check(plan[i].Prefab == manager.EnemyPrefabs[0], "zero weight excluded");
            def.ExcludedEnemies.Add(manager.EnemyPrefabs[0]);
            Check(!manager.TryBuildWave(99, def, plan, out _), "empty random pool is an explicit error");
            def.Mode = WaveSpawnMode.Scripted;
            Check(!manager.TryBuildWave(99, def, plan, out _), "tank-only scripted wave needs a lead enemy");
            def.ExcludedEnemies.Clear();
            def.OrderedSpawns.Add(new WaveSpawnGroup { Prefab = manager.EnemyPrefabs[0], Count = 1 });
            Check(manager.TryBuildWave(99, def, plan, out _) && plan.Count == 3 && plan[0].Prefab == manager.EnemyPrefabs[0], "scripted tanks follow a non-tank while preserving counts");
            def.OrderedSpawns[0].SpawnPointIndex = 2;
            Check(!manager.TryBuildWave(99, def, plan, out _), "invalid spawn point rejected");

            var data = PlayerData.CreateNew(); data.TotalCoins = 345;
            SaveSystem.SaveData(data);
            var stale = SaveSystem.LoadData();
            data.SeenTipIDs.Add("elite_tank"); data.ShowTips = true;
            SaveSystem.SaveData(data, true);
            stale.TotalCoins = 350;
            SaveSystem.SaveData(stale);
            var loaded = SaveSystem.LoadData();
            Check(loaded.TotalCoins == 350 && loaded.ShowTips && loaded.SeenTipIDs.Contains("elite_tank"), "cached economy saves preserve tips");
            loaded.ShowTips = false; SaveSystem.SaveData(loaded, true);
            Check(!SaveSystem.LoadData().ShowTips, "tips can be disabled without deleting history");
            File.WriteAllText(path, "{\"ProgressionVersion\":2,\"TotalCoins\":777}");
            loaded = SaveSystem.LoadData();
            Check(loaded.TotalCoins == 777 && loaded.SeenTipIDs != null && !loaded.ShowTips, "existing saves load without migration/reset");

            var level = Make<LevelManager>(); level.TargetXP = int.MaxValue;
            level.AddXP(31.25f); level.AddXP(31.25f); level.AddXP(31.25f); level.AddXP(31.25f);
            Check(level.CurrentXP == 125, "25 percent bonus XP retains fractions");
            return _checks + " enemy update checks passed.";
        }
        finally
        {
            foreach (var go in _objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _objects.Clear();
            foreach (var definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
            EnemyBase.ActiveEnemies.Clear(); EnemyBase.ActiveEnemies.AddRange(oldRegistry);
            WaveManager.Instance = oldWave; PlayerStats.Instance = oldStats; LevelManager.Instance = oldLevel; AudioManager.Instance = oldAudio;
            UnityEngine.Random.state = oldRandom; SaveSystem.VerificationSavePath = oldPath;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
