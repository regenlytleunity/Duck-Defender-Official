using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Transient objects, isolated physics and a disposable save. Never purchases from the real shop.
public static class LateGameFixVerification
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int _checks;
    static Scene _scene;
    static readonly List<Coin> _createdCoins = new List<Coin>();
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException("Late game regression: " + message);
    }
    static GameObject Make(string name)
    {
        var go = new GameObject("Late game verification " + name);
        go.SetActive(false); SceneManager.MoveGameObjectToScene(go, _scene); return go;
    }
    static Coin MakeCoin(Vector2 position, bool passive = false, bool meteor = false)
    {
        var go = Make("coin"); go.transform.position = position;
        go.AddComponent<Rigidbody2D>(); go.AddComponent<CircleCollider2D>();
        var coin = go.AddComponent<Coin>(); coin.ExplosionForce = coin.PassivePopForce = 0;
        _createdCoins.Add(coin);
        coin.IsPassiveCoin = passive; coin.SecondaryMeteorOnPickup = meteor;
        Call(coin, "Awake"); go.SetActive(true); Call(coin, "OnEnable"); Call(coin, "Start");
        Set(coin, "_spawnTime", Time.time - 2); return coin;
    }

    [MenuItem("Duck Defender/Late Game Fixes/Run Isolated Regression Checks")]
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var oldLevel=LevelManager.Instance; var oldStats=PlayerStats.Instance;
        var oldController=PlayerController.Instance; var oldPool=ObjectPooler.Instance;
        var oldWave=WaveManager.Instance; var oldAudio=AudioManager.Instance; var oldUI=GameUI.Instance;
        var oldRandom=UnityEngine.Random.state; float oldTimeScale=Time.timeScale;
        string oldPath=SaveSystem.VerificationSavePath;
        string path=Path.Combine(Path.GetTempPath(), "duck-late-check-"+Guid.NewGuid().ToString("N")+".json");
        _scene=EditorSceneManager.NewPreviewScene(); _checks=0; _createdCoins.Clear();
        try
        {
            LevelManager.Instance=null; PlayerStats.Instance=null; PlayerController.Instance=null;
            ObjectPooler.Instance=null; WaveManager.Instance=null; AudioManager.Instance=null; GameUI.Instance=null;
            SaveSystem.VerificationSavePath=path;
            VerifyCoins(); VerifySpawnQueue(); VerifyEffects(); VerifyPackLayout(); VerifyDash();
            string result="PASS: "+_checks+" late-game regression assertions, including isolated physics. No Play Mode or frame-time profiling.";
            Debug.Log(result); return result;
        }
        finally
        {
            // Ordinary MonoBehaviour lifecycle callbacks do not run consistently in
            // Edit Mode. Release manually registered coins/materials before closing.
            foreach(var coin in _createdCoins)
                if(coin!=null) {Call(coin,"OnDisable"); Call(coin,"OnDestroy");}
            _createdCoins.Clear();
            EditorSceneManager.ClosePreviewScene(_scene);
            // Spawned wave clones may be assigned to the active scene by Instantiate.
            foreach(var probe in Resources.FindObjectsOfTypeAll<CardReworkCombatProbe>())
                if(probe != null && probe.name.StartsWith("Late game verification enemy")) Object.DestroyImmediate(probe.gameObject);
            LevelManager.Instance=oldLevel; PlayerStats.Instance=oldStats; PlayerController.Instance=oldController;
            ObjectPooler.Instance=oldPool; WaveManager.Instance=oldWave; AudioManager.Instance=oldAudio; GameUI.Instance=oldUI;
            UnityEngine.Random.state=oldRandom; Time.timeScale=oldTimeScale;
            SaveSystem.VerificationSavePath=oldPath; if(File.Exists(path)) File.Delete(path);
        }
    }

    static void VerifyCoins()
    {
        var coins=new List<Coin>();
        for(int i=0;i<100;i++) coins.Add(MakeCoin(new Vector2(1.9f+(i%10)*.02f,(i/10)*.02f), meteor:i%3==0));
        Check(coins.Select(c=>c.GetComponent<Rigidbody2D>().sharedMaterial).Distinct().Count()==1,"coin materials shared");
        Check((coins[0].GetComponent<Rigidbody2D>().excludeLayers.value & (1<<LayerMask.NameToLayer("Player"))) != 0,"coins exclude player collision");
        Check(Coin.MergeNearby()==10,"100 singles form ten nearby stacks across cell boundary");
        Check(coins.Sum(c=>c.CoinValue)==100 && coins.Sum(c=>c.SecondaryMeteorCount)==34,"first merge preserves value and meteor entitlements");
        var animating=coins.First(c=>c.CoinValue==0 && (c.transform.position-Get<Coin>(c,"_mergeTarget").transform.position).sqrMagnitude>.001f);
        var origin=animating.transform.position;
        Set(animating,"_mergeUntil",Time.time+.09f); Call(animating,"Update");
        Check(animating.transform.position!=origin && !animating.GetComponent<Rigidbody2D>().simulated,"vacuum animation moves donors without physics");
        foreach(var donor in coins.Where(c=>c.CoinValue==0))
        {
            Check(!(bool)Call(donor,"TryCollect"),"reserved donor cannot collect");
            Call(donor,"OnDisable"); Call(donor,"OnDestroy");
            Object.DestroyImmediate(donor.gameObject);
        }
        var tens=coins.Where(c=>c!=null).ToList();
        Check(tens.Count==10 && tens.All(c=>c.CoinValue==10 && Mathf.Abs(c.transform.localScale.x-1.18f)<.001f),"ten-value size tier");
        foreach(var coin in tens) Set(coin,"_mergeUntil",Time.time-1);
        Check(Coin.MergeNearby()==1,"ten tens merge into a hundred");
        var hundred=tens.Single(c=>c.CoinValue==100);
        Check(hundred.SecondaryMeteorCount==34 && Mathf.Abs(hundred.transform.localScale.x-1.36f)<.001f,"hundred size and rewards");
        Set(hundred,"_mergeUntil",Time.time-1);
        Check(Coin.MergeNearby()==0,"hundred is final merge tier");

        var level=Make("economy").AddComponent<LevelManager>(); LevelManager.Instance=level;
        Set(level,"_playerData",PlayerData.CreateNew());
        var stats=Make("stats").AddComponent<PlayerStats>(); PlayerStats.Instance=stats;
        stats.MoneyHighDuration=3; stats.DamagePerCoin=.01f;
        stats.HasCoinMeteors=true; stats.MeteorThreshold=10;
        Check((bool)Call(hundred,"TryCollect") && !(bool)Call(hundred,"TryCollect"),"combined pickup commits exactly once");
        Check(level.TotalCoins==100 && stats.CoinsCollectedRun==100,"stack credits every coin");
        Check(stats.ActiveMoneyHighStacks==100 && Mathf.Abs(stats.GetCurrentMoneyHighMultiplier()-2)<.001f,"stack retains per-coin damage bonuses");
        Check(Get<long>(level,"_pendingMeteors")==10 && Get<long>(level,"_pendingSecondaryMeteors")==34,"all meteor rewards queued");
        Check(!File.Exists(SaveSystem.VerificationSavePath),"pickup does not synchronously write save");
        level.FlushCoinSave(); Check(SaveSystem.LoadData().TotalCoins==100,"pending coins flush to isolated save");
        stats.ReportCoinsGained(5);
        var batches=Get<IList>(stats,"_moneyHighExpirations");
        var expired=batches[0]; expired.GetType().GetField("Expiration").SetValue(expired,Time.time-1); batches[0]=expired;
        Set(stats,"_nextMoneyHighExpiration",Time.time-1);
        Check(stats.ActiveMoneyHighStacks==5 && Mathf.Abs(stats.GetCurrentMoneyHighMultiplier()-1.05f)<.001f,"batched coins expire without extending newer bonuses");
        var smaller=new List<Coin>();
        for(int i=0;i<9;i++) smaller.Add(MakeCoin(new Vector2(30+i*.02f,0)));
        Check(Coin.MergeNearby()==0,"nine coins remain singles");
        smaller.Add(MakeCoin(new Vector2(30,0),true));
        Check(Coin.MergeNearby()==0,"passive immunity groups remain separate");
        var immune=smaller.Last(); Set(immune,"_spawnTime",Time.time);
        Check(!(bool)Call(immune,"TryCollect"),"passive immunity preserved");
        for(int i=0;i<10;i++) MakeCoin(new Vector2(100+i*3,0));
        Check(Coin.MergeNearby()==0,"distant coins do not merge");
        PlayerStats.Instance=null; LevelManager.Instance=null;
    }

    static void VerifySpawnQueue()
    {
        var wave=Make("wave").AddComponent<WaveManager>(); WaveManager.Instance=wave;
        var template=Make("enemy"); template.AddComponent<CardReworkCombatProbe>();
        wave.EnemyPrefabs=new[]{template}; wave.SpawnPoints=new[]{Make("spawn").transform};
        wave.MaxConcurrentEnemies=1; wave.BaseMultiSpawnChance=wave.MaxMultiSpawnChance=1;
        wave.BaseExtraSpawns=2; wave.MaxExtraSpawns=2;
        Set(wave,"_enemiesRemainingToSpawn",3); Set(wave,"_enemiesAlive",3);
        var spawn=(IEnumerator)Call(wave,"SpawnRoutine");
        Time.timeScale=0; spawn.MoveNext(); Check(wave.QueuedEnemies==3,"paused wave does not spawn");
        Time.timeScale=1; spawn.MoveNext();
        Check(wave.QueuedEnemies==2 && wave.SpawnedEnemiesAlive==1 && spawn.Current is WaitForSeconds,"batch yields after one spawn");
        spawn.MoveNext(); Check(wave.QueuedEnemies==2 && spawn.Current==null,"live cap blocks queued spawn");
        wave.OnEnemyKilled(); Check(Get<int>(wave,"_enemiesAlive")==2,"queued enemies remain in wave accounting");
        spawn.MoveNext(); Check(wave.QueuedEnemies==1 && wave.SpawnedEnemiesAlive==1,"kill releases next queued enemy");
        wave.OnEnemyKilled();
        for (int i=0;i<5 && wave.QueuedEnemies>0;i++) spawn.MoveNext();
        Check(wave.QueuedEnemies==0 && wave.SpawnedEnemiesAlive==1,"all promised enemies eventually spawned");
        wave.OnEnemyKilled(); Check(Get<int>(wave,"_enemiesAlive")==0,"all wave rewards require all kills");
        WaveManager.Instance=null;
    }

    static void VerifyEffects()
    {
        var pool=Make("pool").AddComponent<ObjectPooler>(); pool.PlayerPrewarm=0;
        pool.MaximumEffectsPerPrefab=2; pool.MaximumEffectsTotal=8;
        pool.ProjectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Object Prefabs/PlayerBullet.prefab");
        pool.EnemyProjectilePrefab=Make("enemy bullet"); pool.BouncyEnemyProjectilePrefab=Make("bouncy bullet");
        pool.EnemyPoolSize=pool.BouncyPoolSize=10000; pool.EnemyPrewarm=4;
        Call(pool,"Awake");
        pool.gameObject.SetActive(true);
        Check(Get<List<GameObject>>(pool,"_enemyPool").Count==4 && Get<List<GameObject>>(pool,"_bouncyEnemyPool").Count==4,"large enemy pools prewarm only the budget");
        GameObject rented=null;
        for(int i=0;i<5;i++){rented=pool.GetEnemyBullet(); rented.SetActive(true);}
        Check(Get<List<GameObject>>(pool,"_enemyPool").Count==5,"enemy pool grows on demand");
        rented.SetActive(false); Check(pool.GetEnemyBullet()==rented,"enemy pool reuses returned ammunition");
        var weapon=Make("weapon").AddComponent<WeaponPlayer>();
        Call(weapon,"SpawnAirburstPellet",Vector3.zero,0f,10,1f,0);
        var bullet=Get<List<GameObject>>(pool,"_playerPool")[0].GetComponent<Projectile>();
        Check(bullet.Stats.SuppressHitEffect && !bullet.Stats.CanAirburst,"real Airburst spawn suppresses hit FX and recursion");
        Call(bullet,"SpawnEffect"); Check(pool.EffectInstances==0,"Airburst collision produces no hit effect");
        bullet.Initialize(new Projectile.BallisticData{Speed=10}); Call(bullet,"SpawnEffect");
        Check(pool.EffectInstances==1 && !bullet.Stats.SuppressHitEffect,"pooled normal feather restores hit FX");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/Explosion Effect.prefab");
        GameObject effect=null;
        for(int i=0;i<80;i++) effect=ObjectPooler.SpawnEffect(prefab,Vector3.zero,Quaternion.identity);
        Check(effect!=null && effect.activeSelf && pool.EffectInstances==3,"80 explosions remain visible within cap");
        Check(!effect.GetComponent<SelfDestruct>().enabled,"explosion destruction helper disabled in pool");
        Call(effect.GetComponent<SelfDestruct>(),"Start");
        Check(Quaternion.Angle(effect.transform.rotation,prefab.transform.rotation)<.01f,"authored particle orientation preserved");
        var particles=effect.GetComponent<ParticleSystem>(); particles.Simulate(.1f,true,true,false);
        Check(particles.particleCount>0,"explosion burst emits particles");
        var renderer=effect.GetComponent<ParticleSystemRenderer>();
        Check(renderer.sharedMaterial.shader.name=="Universal Render Pipeline/Particles/Unlit" && renderer.sharedMaterial.mainTexture!=null && renderer.sortingOrder==100,"explosion material has texture and foreground sorting");
        Object.DestroyImmediate(effect); effect=ObjectPooler.SpawnEffect(prefab,Vector3.zero,Quaternion.identity);
        Check(effect!=null && pool.EffectInstances==3,"destroyed effects do not permanently consume slots");
        var volcano=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/Volcano fire.prefab");
        var fire=Object.Instantiate(volcano,Make("fire parent").transform);
        fire.GetComponent<AscensionArea>().Initialize(3,3,5);
        var fireParticles=fire.GetComponentInChildren<ParticleSystem>(true);
        Check(fireParticles.gameObject.activeSelf && fireParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name=="Universal Render Pipeline/Particles/Unlit","Volcano fire child activated with particle material");
        foreach(var fx in pool.GetComponentsInChildren<PooledVisualEffect>(true)) fx.gameObject.SetActive(false);
        var eruptionOwner=Make("eruption").AddComponent<AscensionEffects>();
        eruptionOwner.VolcanoFirePrefab=Make("eruption template").AddComponent<AscensionArea>();
        var eruption=(IEnumerator)Call(eruptionOwner,"Eruption",new Vector3(2000,2000,0),3f,prefab);
        Check(eruption.MoveNext() && eruption.Current is WaitForSeconds,"Volcano waits before eruption");
        eruption.MoveNext();
        foreach(var created in Resources.FindObjectsOfTypeAll<AscensionArea>())
            if(created.name=="Late game verification eruption template(Clone)") SceneManager.MoveGameObjectToScene(created.gameObject,_scene);
        Check(pool.GetComponentsInChildren<PooledVisualEffect>(true).Any(fx=>fx.gameObject.activeSelf),"delayed eruption spawns explosion visual");
        ObjectPooler.Instance=null;
    }

    static void VerifyPackLayout()
    {
        var canvasGo=Make("canvas"); var canvas=canvasGo.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var root=(RectTransform)canvasGo.transform; root.sizeDelta=new Vector2(1920,1080);
        var rowGo=Make("row"); var row=rowGo.AddComponent<RectTransform>(); row.SetParent(root,false);
        row.anchoredPosition=new Vector2(-825,0); row.sizeDelta=new Vector2(100,100);
        var layout=rowGo.AddComponent<HorizontalLayoutGroup>(); layout.spacing=120; layout.childControlWidth=false; layout.childControlHeight=false;
        var ui=Make("menu").AddComponent<MainMenuUI>();
        var images=new Image[3];
        for(int i=0;i<3;i++)
        {
            var go=Make("pack"); var image=go.AddComponent<Image>(); image.transform.SetParent(row,false);
            image.rectTransform.sizeDelta=new Vector2(250,350); image.transform.localScale=Vector3.one*2;
            images[i]=image; go.SetActive(i==0);
        }
        ui.PackImage=images[0]; canvasGo.SetActive(true); rowGo.SetActive(true);
        Call(ui,"CenterPackRow");
        float one=row.InverseTransformPoint(images[0].rectTransform.TransformPoint(images[0].rectTransform.rect.center)).x;
        Check(Mathf.Abs(one)<.01f && row.anchoredPosition.x==0,"single pack centered in opening row");
        images[1].gameObject.SetActive(true); images[2].gameObject.SetActive(true); Call(ui,"CenterPackRow");
        float middle=row.InverseTransformPoint(images[1].rectTransform.TransformPoint(images[1].rectTransform.rect.center)).x;
        Check(Mathf.Abs(middle)<.01f,"three-pack middle slot remains centered");
    }

    static void VerifyDash()
    {
        var go=Make("dash player"); go.AddComponent<BoxCollider2D>();
        var pc=go.AddComponent<PlayerController>(); Call(pc,"Awake"); go.SetActive(true);
        var rb=go.GetComponent<Rigidbody2D>(); rb.gravityScale=0;
        pc.DashDistance=3; pc.BlinkBlockerLayer=pc.GroundLayer=LayerMask.GetMask("Ground");
        var physics=_scene.GetPhysicsScene2D();
        var boosts=Make("dash boosts").AddComponent<PlayerStats>(); boosts.RebirthStatBonus=PlayerStats.RebirthBonus;
        PlayerStats.Instance=boosts; pc.MaxRunSpeed=200;
        foreach(float speed in new[]{10f,100f})
        {
            rb.position=new Vector2(-40,40); rb.linearVelocity=Vector2.zero; pc.DashSpeed=speed;
            Physics2D.SyncTransforms(); var dash=(IEnumerator)Call(pc,"DashRoutine",Vector2.right); dash.MoveNext();
            for(int i=0;i<100 && Get<bool>(pc,"_isDashing");i++){Call(pc,"HandleDashPhysics");physics.Simulate(Time.fixedDeltaTime);}
            Check(Mathf.Abs(rb.position.x+37)<.02f,"dash covers three units at speed "+speed);
            Check(!Get<bool>(pc,"_isDashing") && rb.linearVelocity.sqrMagnitude<.001f,"dash clears velocity at end");
        }
        var wall=Make("dash wall"); wall.layer=LayerMask.NameToLayer("Ground"); wall.transform.position=new Vector2(-38,40);
        wall.AddComponent<BoxCollider2D>().size=new Vector2(1,10); wall.SetActive(true);
        rb.position=new Vector2(-40,40); Physics2D.SyncTransforms();
        ((IEnumerator)Call(pc,"DashRoutine",Vector2.right)).MoveNext();
        for(int i=0;i<100 && Get<bool>(pc,"_isDashing");i++){Call(pc,"HandleDashPhysics");physics.Simulate(Time.fixedDeltaTime);}
        Check(rb.position.x < -39 && rb.position.x > -39.1f,"fast dash stops before wall");
        var floor=Make("flat floor"); floor.layer=LayerMask.NameToLayer("Ground"); floor.transform.position=new Vector2(-40,10);
        floor.AddComponent<BoxCollider2D>().size=new Vector2(20,1); floor.SetActive(true);
        rb.position=new Vector2(-40,10.99f); Physics2D.SyncTransforms();
        var destination=(Vector2)Call(pc,"GetBlinkDestination",1f);
        Check(destination.x > -39,"slight resting floor overlap cannot block horizontal blink");
        var area=Make("healing area"); area.AddComponent<BoxCollider2D>(); area.AddComponent<Rigidbody2D>();
        var healing=area.AddComponent<AscensionArea>(); healing.Initialize(3,5,0,1);
        Check(!area.GetComponent<Collider2D>().enabled && !area.GetComponent<Rigidbody2D>().simulated,"Savior area has no solid physics");
        PlayerController.Instance=null;
        PlayerStats.Instance=null;
    }

    [MenuItem("Duck Defender/Late Game Fixes/Check Saved Ground Physics")]
    public static string RunGround()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var oldController=PlayerController.Instance; var oldStats=PlayerStats.Instance;
        _scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/SampleScene.unity");
        int before=_checks;
        try
        {
            PlayerStats.Instance=null;
            var roots=_scene.GetRootGameObjects();
            foreach(var original in roots.SelectMany(r=>r.GetComponentsInChildren<PlayerController>(true))) original.gameObject.SetActive(false);
            var tile=roots.SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapCollider2D>(true)).First();
            var composite=tile.GetComponent<CompositeCollider2D>();
            Check(tile.compositeOperation==Collider2D.CompositeOperation.Merge && composite.pathCount==1,"saved ground has one merged collision outline");
            Check(Mathf.Abs(composite.bounds.max.y+2.3f)<.001f,"ground repair preserves original surface height");
            var go=Make("terrain traversal"); go.AddComponent<BoxCollider2D>();
            var pc=go.AddComponent<PlayerController>(); Call(pc,"Awake"); go.SetActive(true);
            var body=go.GetComponent<Rigidbody2D>(); body.gravityScale=3; body.constraints=RigidbodyConstraints2D.FreezeRotation;
            body.position=new Vector2(-15,-1.75f); pc.MaxRunSpeed=8; pc.Acceleration=100;
            pc.BlinkBlockerLayer=pc.GroundLayer=LayerMask.GetMask("Ground");
            Set(pc,"_moveInput",Vector2.right); Set(pc,"_isGrounded",true);
            Physics2D.SyncTransforms(); var physics=_scene.GetPhysicsScene2D();
            for(int i=0;i<100;i++)
            {
                Call(pc,"ApplyMovement");
                if(i==30 || i==60) body.position=(Vector2)Call(pc,"GetBlinkDestination",1f);
                physics.Simulate(.02f);
            }
            Check(body.position.x>3 && Mathf.Abs(body.position.y+1.8f)<.08f,"player crosses merged tile boundaries with Blink and remains grounded");
            string result="PASS: "+(_checks-before)+" saved-ground preview physics checks.";
            Debug.Log(result); return result;
        }
        finally {EditorSceneManager.ClosePreviewScene(_scene); PlayerController.Instance=oldController; PlayerStats.Instance=oldStats;}
    }
}
