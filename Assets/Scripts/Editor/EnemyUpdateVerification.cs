using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        enemy.Initialize(1);
        EnemyBase.ActiveEnemies.Add(enemy);
        return enemy;
    }
    static void Set(object target, Type type, string field, object value) => type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

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
            Near(elite.MaxHealth, normal.MaxHealth * 1.5f, "elite ground health");
            var tank = (TankEnemy)Enemy(true);
            Near(tank.MaxHealth, 8, "tank uses four times basic ground health");
            tank.transform.position = new Vector3(20, 0);
            normal.transform.position = tank.transform.position + Vector3.right;
            elite.transform.position = tank.transform.position + Vector3.right * 2;
            tank.AcquireChains();
            Check(normal.Protector == tank && elite.Protector == tank, "tank acquires nearest non-tanks");
            var tank2 = (TankEnemy)Enemy(true, true);
            tank2.transform.position = tank.transform.position;
            tank2.AcquireChains();
            Check(tank2.LifetimeLinks == 0 && tank.Protector == null, "exclusive ownership and tanks excluded");
            normal.TakeDamage(1);
            Near(normal.HealthRemaining, 99.5f, "half-damage protection");
            Near(tank.HealthRemaining, 7.5f, "normal tank absorbs half damage exactly");

            tank.enabled = false;
            typeof(TankEnemy).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tank, null);
            tank2.AcquireChains();
            elite.TakeDamage(3);
            Near(tank2.ShieldHealth, 1.5f, "elite converts redirected damage to shield");
            Near(tank2.HealthRemaining, 8, "elite shield gain does not cost health");
            tank2.TakeDamage(2);
            Near(tank2.ShieldHealth, 0, "shield depleted first");
            Near(tank2.HealthRemaining, 7.5f, "shield overflow reaches health");
            tank2.OnProtectedEnemyKilled(normal);
            tank2.OnProtectedEnemyKilled(normal);
            Check(tank2.DefeatedLinks == 1, "ally death cannot grant duplicate stacks");

            EnemyBase.ActiveEnemies.Clear();
            var fourTank = (TankEnemy)Enemy(true, true);
            for (int i = 0; i < 5; i++) { var ally = Enemy(false); ally.transform.position = Vector3.right * (i + 1); }
            fourTank.AcquireChains();
            Check(fourTank.LifetimeLinks == 4, "four lifetime links");
            var linked = EnemyBase.ActiveEnemies.Find(e => e.Protector == fourTank);
            fourTank.OnProtectedEnemyKilled(linked);
            fourTank.AcquireChains();
            Check(fourTank.LifetimeLinks == 4, "no replacement after linked ally dies");

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
            Check(plan.Count == 8 && plan[0].Elite && plan[1].Elite && plan[0].DelayBefore == 2 && plan[1].DelayBefore == 0, "ordered count, variant, timing");
            for (int i = 2; i < plan.Count; i++) Check(plan[i].Prefab == manager.EnemyPrefabs[0], "zero weight excluded");
            def.ExcludedEnemies.Add(manager.EnemyPrefabs[0]);
            Check(!manager.TryBuildWave(99, def, plan, out _), "empty random pool is an explicit error");
            def.Mode = WaveSpawnMode.Scripted;
            Check(manager.TryBuildWave(99, def, plan, out _) && plan.Count == 2, "scripted uses exact group count");
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
