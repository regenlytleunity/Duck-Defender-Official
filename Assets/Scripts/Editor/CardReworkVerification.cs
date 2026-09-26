using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Runs against transient objects and a unique temporary save, never the player's save.
public static class CardReworkVerification
{
    static int _checks;
    static readonly List<GameObject> _objects = new List<GameObject>();
    static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException("Card rework check failed: " + message);
    }
    static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message + " (" + actual + " vs " + expected + ")");
    static T Component<T>() where T : Component
    {
        var go = new GameObject("Card rework verification") { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    [MenuItem("Duck Defender/Card Rework/Run Isolated Verification")]
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run verification outside Play Mode.");
        var oldStats = PlayerStats.Instance; var oldShop = ShopManager.Instance;
        var oldManager = CardManager.Instance; var oldController = PlayerController.Instance;
        var oldLevel = LevelManager.Instance; var oldMenu = MainMenuUI.Instance;
        string oldPath = SaveSystem.VerificationSavePath;
        var randomState = UnityEngine.Random.state;
        string testPath = Path.Combine(Path.GetTempPath(), "duck-card-check-" + Guid.NewGuid().ToString("N") + ".json");
        _checks = 0;
        try
        {
            SaveSystem.VerificationSavePath = testPath;
            MainMenuUI.Instance = null; PlayerController.Instance = null; LevelManager.Instance = null;
            PlayerStats.Instance = null; ShopManager.Instance = null; CardManager.Instance = null;
            var cards = AssetDatabase.FindAssets("t:CardDefinition", new[] { "Assets/Cards/Upgrades" })
                .Select(g => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Check(cards.Count == 51 && cards.Select(c => c.ID).Distinct().Count() == 51, "51 unique definitions");
            Check(cards.Count(c => c.Ascension != CardAscension.None) == 25, "25 ascensions");
            Check(cards.Count(c => c.IsBasic) == 4, "4 Basic cards");
            foreach (var card in cards)
            {
                Check(card.MaxLevel == 6, card.ID + " has six levels");
                for (int level = 1; level <= 6; level++)
                    Check(!card.GetDescriptionAtLevel(level).Contains("{"), card.ID + " description placeholders at " + level);
                if (card.Ascension != CardAscension.None)
                    Check(card.AscensionCost == 1000 && !string.IsNullOrEmpty(card.AscendedDescription), card.ID + " ascension metadata");
            }
            string[] starter = { "mun_faster_firing", "mun_sharp_eye", "mob_strong_legs", "mob_swiftness", "sur_exp_booster", "sur_thorns", "gad_big_feathers", "gad_slow_aura" };
            var fresh = PlayerData.CreateNew();
            Check(fresh.CardCollection.Count == 8 && starter.All(id => fresh.CardCollection.Any(c => c.CardID == id && c.Level == 1 && c.IsUnlocked)), "eight specified starter cards");
            File.WriteAllText(testPath, "{\"TotalCoins\":777,\"CardCollection\":[{\"CardID\":\"old\",\"Level\":5}]}");
            var reset = SaveSystem.LoadData();
            Check(reset.TotalCoins == 0 && reset.ProgressionVersion == PlayerData.CurrentVersion && reset.CardCollection.Count == 8, "old schema resets");
            reset.TotalCoins = 123; reset.GadgetEssence = 42; reset.CardCollection[0].Level = 4;
            SaveSystem.SaveData(reset);
            reset = SaveSystem.LoadData();
            Check(reset.TotalCoins == 123 && reset.GadgetEssence == 42 && reset.CardCollection[0].Level == 4, "current schema survives reload");

            var shop = Component<ShopManager>(); ShopManager.Instance = shop; shop.AllCards = cards;
            var stats = Component<PlayerStats>(); PlayerStats.Instance = stats;
            var weapon = stats.gameObject.AddComponent<WeaponPlayer>(); weapon.FireRate = .2f;
            weapon.CurrentStats = new Projectile.BallisticData { Damage = 10, DamageMultiplier = 1, Speed = 20 };
            var health = stats.gameObject.AddComponent<PlayerHealth>(); health.MaxHealth = 5;
            var controller = stats.gameObject.AddComponent<PlayerController>(); PlayerController.Instance = controller;
            var levelManager = Component<LevelManager>(); LevelManager.Instance = levelManager;
            var manager = Component<CardManager>(); CardManager.Instance = manager; manager.AllCards = cards;

            int[][] costs = { new[] {100,200,350,500,750}, new[] {200,350,500,750,1250}, new[] {350,500,750,1250,2000} };
            int[][] copies = { new[] {2,4,8,16,32}, new[] {2,4,6,10,16}, new[] {1,2,4,6,8} };
            foreach (var card in cards.Where(c => !c.IsBasic))
            {
                fresh = PlayerData.CreateNew(); fresh.TotalCoins = 100000;
                fresh.CardCollection.RemoveAll(c => c.CardID == card.ID);
                fresh.CardCollection.Add(new CardSaveData(card.ID) { Duplicates = 100 });
                SaveSystem.SaveData(fresh); shop.LoadEconomy();
                int rarity = (int)card.Rarity;
                int spentCopies = 0, spentGold = 0;
                for (int level = 1; level < 6; level++)
                {
                    Check(card.GetUpgradeCost(level) == costs[rarity][level-1] && card.GetCardsRequired(level) == copies[rarity][level-1], card.ID + " costs at " + level);
                    Check(shop.TryUpgradeCard(card.ID), card.ID + " upgrade " + level);
                    spentCopies += copies[rarity][level-1]; spentGold += costs[rarity][level-1];
                }
                Check(shop.GetCardData(card.ID).Level == 6 && !shop.TryUpgradeCard(card.ID), card.ID + " level cap");
                Check(shop.CurrentCoins == 100000-spentGold, card.ID + " exact spending");
                int essence = (100-spentCopies) * card.EssencePerCopy;
                Check(shop.GetEssence(card.PackCategory) == essence, card.ID + " surplus conversion");
                shop.AddCardToCollection(card.ID);
                Check(shop.GetEssence(card.PackCategory) == essence + card.EssencePerCopy, card.ID + " max duplicate conversion");
                Check(!shop.TryAscendCard(card.ID), card.ID + " insufficient essence");
                var funded = SaveSystem.LoadData(); funded.AddEssence(card.PackCategory, 1000); SaveSystem.SaveData(funded); shop.LoadEconomy();
                bool canAscend = card.Ascension != CardAscension.None;
                int before = shop.GetEssence(card.PackCategory);
                Check(shop.TryAscendCard(card.ID) == canAscend, card.ID + " ascension eligibility");
                Check(shop.GetEssence(card.PackCategory) == before - (canAscend ? 1000 : 0), card.ID + " ascension spending");
                Check(!shop.TryAscendCard(card.ID), card.ID + " cannot ascend twice");
            }
            shop.ResetProgress();
            Check(!shop.TryUpgradeCard("mun_faster_firing") && !shop.TryAscendCard("mun_faster_firing"), "cannot buy without resources");
            Check(!shop.ExecuteDeveloperCode("381") && !shop.ExecuteDeveloperCode("0000"), "invalid developer codes");
            Check(shop.ExecuteDeveloperCode("0381") && shop.InfiniteCopies, "leading-zero code grants infinite copies");
            Check(!shop.TryUpgradeCard("mun_faster_firing"), "infinite copies still needs gold");
            Check(shop.ExecuteDeveloperCode("9845") && shop.InfiniteResources, "infinite resource code");
            Check(shop.TryUpgradeCard("mun_faster_firing") && shop.CurrentCoins == 0, "infinite resources do not overflow/subtract");
            Check(shop.ExecuteDeveloperCode("8672") && shop.GetCardData("mun_faster_firing").Level == 2, "unlock all preserves upgrades");
            Check(shop.ExecuteDeveloperCode("3619") && cards.Where(c => !c.IsBasic).All(c => shop.GetCardData(c.ID).Level == 6 && shop.GetCardData(c.ID).IsAscended == (c.Ascension != CardAscension.None)), "max-all code");
            shop.ResetProgress(); Check(!shop.InfiniteCopies && !shop.InfiniteResources, "reset clears developer flags");
            Check(shop.ExecuteDeveloperCode("5942") && shop.InfiniteResources, "combined code");
            var pack = ScriptableObject.CreateInstance<ShopPackDefinition>();
            try
            {
                pack.PackType = CardPackType.Munitions; pack.Cost = 100;
                Check(shop.TryBuyPacks(pack, 3).Count == 9 && shop.CurrentCoins == 0, "three packs yields nine cards with infinite gold");
                Check(shop.TryBuyPacks(pack, 2) == null, "unsupported pack counts rejected");
                pack.PackType = CardPackType.BaseSet;
                Check(shop.TryBuyPacks(pack, 3) == null, "Basics cannot be bought in packs");
                shop.ResetProgress(); fresh = SaveSystem.LoadData(); fresh.TotalCoins = 300; SaveSystem.SaveData(fresh); shop.LoadEconomy();
                pack.PackType = CardPackType.Gadget;
                Check(shop.TryBuyPacks(pack, 3).Count == 9 && shop.CurrentCoins == 0 && SaveSystem.LoadData().TotalCoins == 0, "paid triple purchase charges exactly once");
                Check(shop.TryBuyPacks(pack, 1) == null, "insufficient funds rejects pack");
            }
            finally { UnityEngine.Object.DestroyImmediate(pack); }

            shop.ResetProgress(); shop.ExecuteDeveloperCode("8672");
            UnityEngine.Random.InitState(90523);
            int[] rolls = new int[4];
            for (int i = 0; i < 10000; i++) { var c = manager.GetRandomCards(1)[0]; rolls[c.IsBasic ? 3 : (int)c.Rarity]++; }
            float[] expected = { .60f, .30f, .03f, .07f };
            for (int i = 0; i < 4; i++) Check(Mathf.Abs(rolls[i]/10000f-expected[i]) < .02f, "run offer weight " + i + " observed " + rolls[i]);
            stats.LuckPercent = 1;
            int uncommon = 0;
            for (int i = 0; i < 2000; i++) { var c = manager.GetRandomCards(1)[0]; if (!c.IsBasic && c.Rarity != CardRarity.Common) uncommon++; }
            Check(uncommon > 1100 && uncommon < 1500, "luck increases rare/legendary offers"); stats.LuckPercent = 0;
            foreach (var card in cards.Where(c => !c.IsBasic)) manager.SetCardLevel(card.ID, 1);
            var offers = manager.GetRandomCards(3);
            Check(offers.Count == 3 && offers.All(c => c.IsBasic) && offers.Distinct().Count() == 3, "exhausted collection fills with distinct Basics");
            manager.SetCardLevel("mun_accelerator", 0); manager.SetCardLevel("mob_dash", 0);
            offers = manager.GetRandomCards(3);
            Check(offers.Count == 3 && offers.Count(c => !c.IsBasic) == 2, "two remaining cards plus one Basic");
            foreach (var card in cards) manager.SetCardLevel(card.ID, 0);
            var firing = cards.Single(c => c.ID == "mun_faster_firing");
            manager.ApplyCardEffect(firing); Near(weapon.FireRate, .16f, "level-one cooldown reduction");
            manager.ApplyCardEffect(firing); Near(weapon.FireRate, .16f, "repeat collection pick rejected");
            var income = cards.Single(c => c.ID == "bas_income"); manager.ApplyCardEffect(income); manager.ApplyCardEffect(income);
            Check(levelManager.CoinsPerWave == 20, "Basic income repeats at original strength");
            var basicHealth = cards.Single(c => c.ID == "bas_health"); manager.ApplyCardEffect(basicHealth); manager.ApplyCardEffect(basicHealth);
            Check(health.MaxHealth == 9 && health.RegenPerWave == 2, "Basic health preserves +2 max/+1 regeneration");
            shop.ExecuteDeveloperCode("3619");
            foreach (var card in cards.Where(c => !c.IsBasic && c.ID != firing.ID)) manager.ApplyCardEffect(card);
            Check(Enum.GetValues(typeof(CardAscension)).Cast<CardAscension>().Where(a => a != CardAscension.None).All(stats.HasAscension), "all 25 ascensions activate");
            Check(!stats.HasMiniGun && stats.HasAscension(CardAscension.DeathRay), "Death-Ray replaces minigun");
            Check(!stats.HasSecondWind && stats.HasAscension(CardAscension.Rebirth), "Rebirth replaces Second Wind");
            Near(controller.ShockwaveDamage, 15, "Earthquake retains level-six shockwave");
            Near(stats.MeteorDamage, 35, "Absolute Extinction retains level-six meteor");
            stats.GlobalDamageBonus = .3f; stats.RunDamageBonus = .1f; stats.NonFeatherFlatDamage = 2; stats.DamagePerCoin = 0;
            Near(stats.CalculateDamage(10, true), 14, "additive percentage damage");
            Near(stats.CalculateDamage(10, false), 16.8f, "non-feather flat damage before percent");
            Near(stats.CalculateDamage(10, true, 1, -.5f), 9, "duplicator penalty shares additive bucket");
            stats.RebirthStatBonus = PlayerStats.RebirthBonus;
            Near(stats.CalculateDamage(10, true), 29, "Rebirth adds 150 percent once to damage");
            Near(PlayerStats.Boost(6), 15, "Rebirth beneficial strength"); Near(PlayerStats.Cooldown(9), 3.6f, "Rebirth cooldown direction");
            Check(PlayerStats.Threshold(7) == 3 && PlayerStats.BoostCount(6) == 15, "Rebirth threshold/count rounding");
            var projectile = Component<Projectile>();
            projectile.Initialize(new Projectile.BallisticData { Damage = 10, Speed = 20, Variant = CardAscension.Tungsten, RicochetCount = 3, IgnoreEnemyID = 123, InfinitePierce = true });
            projectile.ConfigureElectricChain(10, 2, 4, weapon);
            projectile.Initialize(new Projectile.BallisticData { Damage = 10, Speed = 20 });
            Check(projectile.Stats.Variant == CardAscension.None && !projectile.Stats.InfinitePierce && Field<int>(projectile, "_electricChainCount") == 0 && Field<HashSet<int>>(projectile, "_hitEnemyIDs").Count == 0, "pooled projectile clears variant, ignore list, chain and piercing state");
            Near(projectile.Stats.Speed, 50, "projectile stats scaled once on initialization");
            stats.GlobalDamageBonus = stats.RunDamageBonus = stats.RebirthStatBonus = stats.NonFeatherFlatDamage = 0;
            stats.XPMultiplier = 1; levelManager.AddXP(370);
            Check(levelManager.CurrentLevel == 4 && levelManager.CurrentXP == 6 && levelManager.TargetXP == 173, "bulk XP preserves all level thresholds");
            var enemy = Component<CardReworkCombatProbe>(); enemy.SetHealth(100);
            enemy.gameObject.SetActive(true);
            var enemyCollider = enemy.gameObject.AddComponent<BoxCollider2D>();
            var secondCollider = enemy.gameObject.AddComponent<CircleCollider2D>();
            if (!EnemyBase.ActiveEnemies.Contains(enemy)) EnemyBase.ActiveEnemies.Add(enemy);
            try
            {
                projectile.Initialize(new Projectile.BallisticData { Damage = 10, DamageMultiplier = 1, InfinitePierce = true });
                Call(projectile, "OnTriggerEnter2D", enemyCollider);
                Call(projectile, "OnTriggerEnter2D", secondCollider);
                Near(enemy.HealthRemaining, 90, "multiple colliders yield one projectile hit");
                projectile.Initialize(new Projectile.BallisticData { Damage = 10, DamageMultiplier = 1, IgnoreEnemyID = enemy.GetInstanceID() });
                Call(projectile, "OnTriggerEnter2D", enemyCollider);
                Near(enemy.HealthRemaining, 90, "airburst cannot hit its source enemy");
                projectile.Initialize(new Projectile.BallisticData { Damage = 10, DamageMultiplier = 1, ExplosionRadius = 5 });
                Call(projectile, "Explode");
                Near(enemy.HealthRemaining, 85, "explosion damages an enemy once regardless of colliders");
                enemy.IsMarked = true;
                projectile.Initialize(new Projectile.BallisticData { Damage = 10, DamageMultiplier = 1, InfinitePierce = true });
                Call(projectile, "OnTriggerEnter2D", enemyCollider);
                Near(enemy.HealthRemaining, 65, "marked target forces a feather critical");
                projectile.Initialize(new Projectile.BallisticData { Damage = 5, DamageMultiplier = 1, NonFeather = true, InfinitePierce = true });
                Call(projectile, "OnTriggerEnter2D", enemyCollider);
                Near(enemy.HealthRemaining, 60, "needles do not inherit feather criticals");
                enemy.TakeFractionalDamage(.5f); enemy.TakeFractionalDamage(.5f);
                Near(enemy.HealthRemaining, 59, "fractional damage accumulates without rounding every tick upward");
                projectile.Initialize(new Projectile.BallisticData { Damage = 10, Speed = 20 });
                stats.RicochetDamageLoss = .35f; Call(projectile, "ApplyBounceScaling");
                Near(Field<float>(projectile, "_currentDamageMultiplier"), .65f, "level-six ricochet loss");
                Near(Field<float>(projectile, "_currentSpeedMultiplier"), 1.5f, "ricochet speed gain");
                projectile.Initialize(new Projectile.BallisticData { Damage = 10, Speed = 20, Variant = CardAscension.Tungsten });
                Call(projectile, "ApplyBounceScaling");
                Check(Field<bool>(projectile, "_lingering"), "Tungsten lingers after its last bounce");
                Near(Field<float>(projectile, "_lifeTimer"), 1, "Tungsten linger lasts one second");
                stats.RebirthUsed = true;
                Check(!stats.IsSecondWindReady(), "spent Rebirth cannot retrigger as Second Wind");
            }
            finally { EnemyBase.ActiveEnemies.Remove(enemy); }
            VerifyPlaytestFixes(cards, stats, weapon, controller);
            string result = "PASS: " + _checks + " checks. Temporary save and transient objects only. Run weights C/R/L/B: " + string.Join("/", rolls) + " of 10000.";
            Debug.Log("[Card Rework] " + result);
            return result;
        }
        finally
        {
            foreach (var go in _objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _objects.Clear();
            PlayerStats.Instance = oldStats; ShopManager.Instance = oldShop; CardManager.Instance = oldManager;
            PlayerController.Instance = oldController; LevelManager.Instance = oldLevel; MainMenuUI.Instance = oldMenu;
            SaveSystem.VerificationSavePath = oldPath; UnityEngine.Random.state = randomState;
            if (File.Exists(testPath)) File.Delete(testPath);
        }
    }

    static void VerifyPlaytestFixes(List<CardDefinition> cards, PlayerStats stats, WeaponPlayer weapon, PlayerController controller)
    {
        var firing = cards.Single(c => c.ID == "mun_faster_firing");
        var hunter = cards.Single(c => c.ID == "gad_marksman_turret");
        for (int level = 1; level <= 6; level++)
        {
            Near(firing.GetAmountAtShopLevel(firing.Modifiers.First(m => m.StatType == StatType.FireRate), level), .1f + .1f * level, "Faster Firing balance " + level);
            Near(hunter.GetAmountAtShopLevel(hunter.Modifiers.First(m => m.StatType == StatType.MarksmanFireRate), level), 7 - level, "Hunter cooldown " + level);
        }
        Check(cards.Single(c => c.ID == "gad_feather_duplicator").AscensionRetainsBase && weapon.ParallelProjectiles == 2, "Divine Duplicator keeps the parallel pair");
        Near(stats.DuplicatorDamageReduction, .25f, "Divine Duplicator keeps level-six duplicate penalty");
        Near(stats.ProtectorInterval, 20, "Defender cooldown");
        Near(PlayerStats.RebirthBonus, 1.5f, "Rebirth grants 150 percent");

        var area = Component<AscensionArea>();
        var areaCollider = area.gameObject.AddComponent<CircleCollider2D>();
        areaCollider.isTrigger = false;
        area.Initialize(3, 5, 0, 2);
        area.ConfigureCircleVisual(false);
        Check(!areaCollider.enabled, "Savior authored solid collider cannot block the player");
        var line = area.GetComponent<LineRenderer>();
        Check(line != null && line.enabled && line.loop && line.positionCount == 60 && line.sortingOrder == 100, "Savior renders an aura ring above the background");
        Near(line.GetPosition(0).magnitude, 2.85f, "Savior ring matches gameplay radius");
        area.ConfigureCircleVisual(true);
        Check(Field<Transform>(area, "_rotatingVisual") != null, "Wormhole has a rotating circle even without sprite art");

        var wall = Component<DefenderWall>(); Call(wall, "Awake");
        Check(wall.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic && wall.GetComponent<Rigidbody2D>().gravityScale > 0 && !wall.GetComponent<BoxCollider2D>().isTrigger, "Defender has gravity and solid ground contact");
        var orb = Component<HealingOrb>(); Call(orb, "Awake");
        Check(orb.GetComponent<Rigidbody2D>().gravityScale > 0 && !orb.GetComponent<CircleCollider2D>().isTrigger, "Vampire orb falls and rests on terrain");
        Check((orb.GetComponent<Rigidbody2D>().excludeLayers.value & LayerMask.GetMask("Ground")) == 0, "orb ground layer is included");

        Call(controller, "Awake");
        controller.gameObject.SetActive(true); // Rigidbody velocity setters require an active body.
        var body = controller.GetComponent<Rigidbody2D>(); body.position = new Vector2(1000, 1000);
        controller.BlinkDistance = 1.5f;
        stats.RebirthStatBonus = PlayerStats.RebirthBonus;
        Vector2 destination = (Vector2)Call(controller, "GetBlinkDestination", 1f);
        Near(destination.x - body.position.x, 1.5f, "Blink fixed distance is independent of Rebirth/movement speed");
        body.linearVelocity = Vector2.zero;
        typeof(PlayerController).GetField("_moveInput", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, Vector2.right);
        for (int i = 0; i < 150; i++) Call(controller, "ApplyMovement");
        Check(body.linearVelocity.x > 0 && body.linearVelocity.x <= controller.EffectiveMaxSpeed, "high-stat movement accelerates without overshoot: " + body.linearVelocity.x + " / " + controller.EffectiveMaxSpeed);
        controller.gameObject.SetActive(false);
        stats.RebirthStatBonus = 0;

        var first = Component<CardReworkCombatProbe>(); first.SetHealth(100); first.transform.position = Vector3.zero;
        var second = Component<CardReworkCombatProbe>(); second.SetHealth(100); second.transform.position = Vector3.right * 2;
        EnemyBase.ActiveEnemies.Add(first); EnemyBase.ActiveEnemies.Add(second);
        try
        {
            var shot = Component<Projectile>();
            shot.Initialize(new Projectile.BallisticData { Damage = 10, Speed = 20, ExplosionRadius = 5, IgnoreEnemyID = first.GetInstanceID() });
            Check((Transform)Call(shot, "FindNearestEnemyExcluding") == second.transform, "ricochet excludes the previously hit EnemyBase ID");
            first.DisableOnDamage = true;
            Call(shot, "Explode");
            Near(second.HealthRemaining, 95, "area damage continues when a hit removes an enemy from the registry");
        }
        finally { EnemyBase.ActiveEnemies.Remove(first); EnemyBase.ActiveEnemies.Remove(second); }

        var oldPool = ObjectPooler.Instance;
        try
        {
            var pool = Component<ObjectPooler>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Object Prefabs/PlayerBullet.prefab");
            pool.ProjectilePrefab = prefab; pool.PoolSize = 10000; pool.PlayerPrewarm = 4;
            Call(pool, "Awake");
            Check(pool.PlayerInstances == 4 && pool.PlayerCapacity == 2048, "large saved pool is lazy and bounded");
            var bullet = pool.GetPooledObject();
            var projectile = bullet.GetComponent<Projectile>();
            projectile.Initialize(new Projectile.BallisticData { Speed = 20, Variant = CardAscension.Tungsten });
            Check(projectile.GetComponentInChildren<SpriteRenderer>().sprite == projectile.TungstenSprite && !projectile.GetComponent<Animator>().enabled, "Tungsten keeps its assigned sprite instead of the feather animation");
            projectile.Initialize(new Projectile.BallisticData { Speed = 20 });
            Check(projectile.GetComponent<Animator>().enabled && projectile.GetComponentInChildren<SpriteRenderer>().sprite != projectile.TungstenSprite, "normal pooled reuse restores feather animation and sprite");
            Call(pool, "ReturnPlayerProjectile", bullet);
            for (int i = 0; i < 10000; i++) { var reused = pool.GetPooledObject(); Call(pool, "ReturnPlayerProjectile", reused); }
            Check(pool.PlayerInstances == 4, "ten thousand projectile rentals reuse the prewarm without clone growth");
            var hitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/Feather Hit Effect.prefab");
            var effect = ObjectPooler.SpawnEffect(hitPrefab, Vector3.zero, Quaternion.identity);
            Check(effect != null && effect.activeSelf, "inactive hit-effect prefab is explicitly activated");
            var lifetime = effect.GetComponent<PooledVisualEffect>();
            typeof(PooledVisualEffect).GetField("_remaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lifetime, -1f);
            Call(lifetime, "Update");
            Check(!effect.activeSelf, "hit effect returns to pool after its finite lifetime");
            Check(ObjectPooler.SpawnEffect(hitPrefab, Vector3.zero, Quaternion.identity) == effect && pool.EffectInstances == 1, "hit effect is reused");
            pool.MaximumEffectsPerPrefab = 1;
            Check(ObjectPooler.SpawnEffect(hitPrefab, Vector3.zero, Quaternion.identity) == effect && pool.EffectInstances == 1, "visual saturation recycles within its bound");
        }
        finally { ObjectPooler.Instance = oldPool; }
    }

    [MenuItem("Duck Defender/Card Rework/Run Isolated Physics Verification")]
    public static string RunPhysics()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run verification outside Play Mode.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var oldController = PlayerController.Instance;
        int previousChecks = _checks;
        try
        {
            var physics = scene.GetPhysicsScene2D();
            Check(physics.IsValid() && physics != Physics2D.defaultPhysicsScene, "physics check uses a separate preview world");
            var ground = new GameObject("Verification ground");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            ground.layer = LayerMask.NameToLayer("Ground");
            ground.AddComponent<BoxCollider2D>().size = new Vector2(30, 1);
            var wallObject = new GameObject("Falling wall");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wallObject, scene);
            wallObject.transform.position = new Vector3(-3, 8, 0);
            var wall = wallObject.AddComponent<DefenderWall>(); Call(wall, "Awake");
            wall.GetComponent<BoxCollider2D>().size = new Vector2(.5f, 2);
            var orbObject = new GameObject("Falling orb");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(orbObject, scene);
            orbObject.transform.position = new Vector3(3, 8, 0);
            var orb = orbObject.AddComponent<HealingOrb>(); Call(orb, "Awake");
            orb.GetComponent<CircleCollider2D>().radius = .25f;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 250; i++) physics.Simulate(.02f);
            Check(Mathf.Abs(wall.GetComponent<Rigidbody2D>().position.y - 1.5f) < .05f, "wall falls and settles on Ground");
            Check(Mathf.Abs(orb.GetComponent<Rigidbody2D>().position.y - .75f) < .05f, "orb falls and settles on Ground");
            Check(Mathf.Abs(wall.GetComponent<Rigidbody2D>().linearVelocity.y) < .05f && Mathf.Abs(orb.GetComponent<Rigidbody2D>().linearVelocity.y) < .05f, "falling effects remain at rest on the ground");

            var playerObject = new GameObject("Blink body");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerObject, scene);
            playerObject.transform.position = new Vector3(-3, 3, 0);
            playerObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            var player = playerObject.AddComponent<PlayerController>(); Call(player, "Awake");
            player.GetComponent<Rigidbody2D>().gravityScale = 0;
            player.BlinkDistance = 4;
            player.BlinkBlockerLayer = LayerMask.GetMask("Ground");
            var blocker = new GameObject("Blink wall");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(blocker, scene);
            blocker.layer = ground.layer;
            blocker.transform.position = new Vector3(0, 3, 0);
            blocker.AddComponent<BoxCollider2D>().size = new Vector2(1, 3);
            Physics2D.SyncTransforms();
            var destination = (Vector2)Call(player, "GetBlinkDestination", 1f);
            Check(destination.x > -1.1f && destination.x < -1f, "Blink sweeps the player body and stops before the wall");
            player.GetComponent<Rigidbody2D>().position = new Vector2(-1.02f, 3);
            Physics2D.SyncTransforms();
            destination = (Vector2)Call(player, "GetBlinkDestination", 1f);
            Check(destination.x >= -1.021f, "Blink cannot teleport backwards when touching a wall");
            string result = "PASS: " + (_checks - previousChecks) + " isolated preview-physics checks; no Play Mode or user scene simulation.";
            Debug.Log("[Card Rework] " + result);
            return result;
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            PlayerController.Instance = oldController;
        }
    }
}
