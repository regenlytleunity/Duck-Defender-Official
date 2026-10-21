if (!Application.isPlaying || string.IsNullOrEmpty(SaveSystem.VerificationSavePath)) throw new System.Exception("Isolated Play Mode required");
var checks = new System.Collections.Generic.List<string>();
System.Action<bool, string> check = (ok, message) => { if (!ok) throw new System.Exception(message); checks.Add(message); };
var objects = new System.Collections.Generic.List<GameObject>();
var oldWave = WaveManager.Instance;
int oldPlayers = LocalCoopSession.RequestedPlayers;
float oldTimeScale = Time.timeScale;
try
{
    var root = new GameObject("Update 6 extra verification"); objects.Add(root);
    var wave = root.AddComponent<WaveManager>(); wave.enabled = false;
    wave.SpawnPoints = new[] { root.transform };
    wave.GetComponent<EnemyTipUI>().enabled = false;
    wave.EnemyPrefabs = new[] { "Fast", "Flying", "Tank", "Lobber" }.Select(n => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/" + n + " Enemy.prefab")).ToArray();
    check(wave.EnemyPrefabs.All(p => p != null), "all four real enemy prefabs resolve");
    // Stop before SpawnRoutine does any work, while executing the real multiplayer plan expansion.
    Time.timeScale = 0;
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    LocalCoopSession.RequestedPlayers = 4;
    foreach (int number in new[] { 1, 3, 6, 9, 12, 16, 20, 25 })
    {
        typeof(WaveManager).GetField("_currentWave", flags).SetValue(wave, number - 1);
        var routine = (System.Collections.IEnumerator)typeof(WaveManager).GetMethod("StartNextWave", flags).Invoke(wave, null);
        routine.MoveNext(); routine.MoveNext(); wave.StopAllCoroutines();
        var plan = (System.Collections.Generic.List<WaveManager.SpawnRequest>)typeof(WaveManager).GetField("_spawnPlan", flags).GetValue(wave);
        var intro = plan.Single(p => p.Introduction);
        check(plan.Count(p => p.Prefab == intro.Prefab && p.Elite == intro.Elite) == 1, "four-player introduction remains unique at wave " + number);
    }
    foreach (var enemy in EnemyBase.ActiveEnemies.ToArray()) if (enemy != null) { enemy.gameObject.SetActive(false); UnityEngine.Object.Destroy(enemy.gameObject); }
    wave.StopAllCoroutines();
    typeof(WaveManager).GetField("_isWaveActive", flags).SetValue(wave, false);
    var targetObject = new GameObject("Chain target"); objects.Add(targetObject); targetObject.transform.position = Vector3.zero;
    targetObject.AddComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
    targetObject.AddComponent<BoxCollider2D>();
    var target = targetObject.AddComponent<SwarmerEnemy>(); target.SpawnProtectionSeconds = 0; target.Initialize(1);
    foreach (bool authored in new[] { false, true })
    {
        var tankObject = UnityEngine.Object.Instantiate(wave.EnemyPrefabs[2], Vector3.right, Quaternion.identity); objects.Add(tankObject);
        var tank = tankObject.GetComponent<TankEnemy>(); tank.SpawnProtectionSeconds = 0;
        if (authored)
        {
            var template = new GameObject("Verification chain template"); objects.Add(template);
            var line = template.AddComponent<LineRenderer>(); line.startColor = line.endColor = new Color(.5f, .8f, 1, .6f);
            tank.ChainPrefab = line;
        }
        else tank.ChainPrefab = null;
        tank.Initialize(1); tank.AcquireChains();
        var chain = tank.GetComponentInChildren<LineRenderer>();
        check(chain != null && Mathf.Abs(chain.startColor.a - (authored ? .24f : tank.ChainColor.a * .4f)) < .001f, "translucent " + (authored ? "authored" : "fallback") + " tank chain");
        tank.gameObject.SetActive(false);
    }
    var result = "PASS: " + checks.Count + " focused checks\n" + string.Join("\n", checks);
    System.IO.File.WriteAllText("tmp/item-update/extra-verification.txt", result);
    return result;
}
finally
{
    foreach (var obj in objects) if (obj != null) { obj.SetActive(false); UnityEngine.Object.Destroy(obj); }
    WaveManager.Instance = oldWave; LocalCoopSession.RequestedPlayers = oldPlayers; Time.timeScale = oldTimeScale;
}
