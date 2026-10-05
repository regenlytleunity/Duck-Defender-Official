using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance;

    [Header("Configuration")]
    public GameObject[] EnemyPrefabs; 
    public Transform[] SpawnPoints; 
    public GameUI UI;

    [Header("Wave Authoring")]
    public List<WaveDefinition> WaveDefinitions = new List<WaveDefinition>();
    public bool UseIntroductoryWaves = true;
    [Range(0, 1)] public float EliteChance = .15f;
    [Tooltip("First normal waves: ground 1, flying 3, tank 6, lobber 9. First elite waves: 12, 16, 20, 25.")]
    public Vector4 NormalUnlockWaves = new Vector4(1, 3, 6, 9);
    public Vector4 EliteUnlockWaves = new Vector4(12, 16, 20, 25);
    public bool HasWaveError { get; private set; }
    readonly List<SpawnRequest> _spawnPlan = new List<SpawnRequest>();
    WaveDefinition _definition;
    int _spawnIndex;

    public struct SpawnRequest
    {
        public GameObject Prefab;
        public bool Elite;
        public int SpawnPointIndex;
        public float DelayBefore, Interval;
    }

    public static float HealthIncreaseAtWave(int wave)
    {
        // Wave 1 is base health. Each transition uses the destination wave's bracket.
        int[] ends = { 10, 15, 20, 25, 30, 40, 50, int.MaxValue };
        float[] increments = { .5f, 1, 2, 3, 4, 6, 10, 20 };
        double health = 0;
        int previous = 1;
        for (int i = 0; i < ends.Length; i++)
        {
            int steps = Mathf.Max(0, Mathf.Min(wave, ends[i]) - previous);
            health += (double)steps * increments[i];
            previous = ends[i];
        }
        return (float)health * GameDifficulty.GrowthMultiplier;
    }

    public float BasicGroundHealth(int wave)
    {
        if (EnemyPrefabs != null) foreach (var prefab in EnemyPrefabs)
            if (prefab != null && prefab.TryGetComponent<EnemyBase>(out var enemy) && enemy.EnemyKind == "ground")
                return Mathf.Max(1, Mathf.Floor(enemy.BaseHealth + HealthIncreaseAtWave(wave)));
        return Mathf.Max(1, Mathf.Floor(2 + HealthIncreaseAtWave(wave)));
    }

    public float BasicGroundSpeed()
    {
        if (EnemyPrefabs != null) foreach (var prefab in EnemyPrefabs)
            if (prefab != null && prefab.TryGetComponent<EnemyBase>(out var enemy) && enemy.EnemyKind == "ground") return enemy.BaseSpeed;
        return 4;
    }

    [Header("Wave Settings")]
    public float TimeBetweenWaves = 3.0f;

    [Header("Spawn Rate")]
    [Tooltip("Time between individual enemy spawns on wave 1")]
    public float BaseSpawnInterval = 1.0f;
    [Tooltip("Minimum spawn interval (floor). Spawn rate won't go faster than this.")]
    public float MinSpawnInterval = 0.3f;
    [Tooltip("How much the spawn interval decreases per wave (multiplied by wave number)")]
    public float SpawnIntervalScaling = 0.07f;

    [Header("Multi-Spawn")]
    [Tooltip("Base chance (0-1) for multiple enemies to spawn at once on wave 1")]
    public float BaseMultiSpawnChance = 0.0f;
    [Tooltip("How much multi-spawn chance increases per wave")]
    public float MultiSpawnChanceScaling = 0.03f;
    [Tooltip("Maximum multi-spawn chance (cap)")]
    public float MaxMultiSpawnChance = 0.6f;
    [Tooltip("Base number of extra enemies when multi-spawn triggers (added to the 1 that always spawns)")]
    public int BaseExtraSpawns = 1;
    [Tooltip("Extra spawns gained per this many waves")]
    public int ExtraSpawnWaveInterval = 5;
    [Tooltip("Maximum extra enemies per multi-spawn")]
    public int MaxExtraSpawns = 4;

    [Header("Spawn work budget")]
    [Min(1)] public int MaxConcurrentEnemies = 40;
    [Min(.01f)] public float MinimumTimeBetweenEnemies = .04f;
    public int SpawnedEnemiesAlive => Mathf.Max(0, _enemiesAlive - _enemiesRemainingToSpawn);
    public int QueuedEnemies => _enemiesRemainingToSpawn;
    bool HasSpawnCapacity => SpawnedEnemiesAlive < Mathf.Max(1, MaxConcurrentEnemies);
    
    [Header("Difficulty Tier Scaling")]
    [Tooltip("How many waves per difficulty tier. Every N waves, non-speed scaling multiplies.")]
    public int WavesPerTier = 10;
    
    [Tooltip("The multiplier applied to non-speed scaling (health, shoot rate, etc.) each tier.")]
    [Range(1.0f, 3.0f)]
    public float DifficultyTierMultiplier = 2.0f;

    private int _currentWave = 0;
    private int _enemiesRemainingToSpawn;
    private int _enemiesAlive;
    private bool _isWaveActive = false;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (GetComponent<EnemyTipUI>() == null) gameObject.AddComponent<EnemyTipUI>();
    }

    void Start()
    {
        // Start the random rotation of in-game music tracks.
        // The AudioManager handles picking tracks 1/2/3 randomly, never repeating 
        // the same track twice in a row, and crossfading between them.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayInGameMusicRandom();
        }
        
        StartCoroutine(StartNextWave());
    }

    IEnumerator StartNextWave()
    {
        yield return new WaitForSeconds(TimeBetweenWaves);

        _currentWave++;
        
        _definition = WaveDefinitions.Find(w => w != null && w.WaveNumber == _currentWave);
        if (!TryBuildWave(_currentWave, _definition, _spawnPlan, out string error))
        {
            HasWaveError = true;
            Debug.LogError("[WaveManager] " + error);
            yield break;
        }
        _spawnIndex = 0;
        int enemiesThisWave = _spawnPlan.Count;
        
        _enemiesRemainingToSpawn = enemiesThisWave;
        _enemiesAlive = enemiesThisWave;
        _isWaveActive = true;

        if (UI != null)
        {
            UI.UpdateWaveText(_currentWave);
            UI.UpdateEnemiesLeft(_enemiesAlive);
            UI.ShowWaveBanner(_currentWave);
        }
        
        // Play the wave-start sound to alert the player a new wave is beginning
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("Start_New_Wave");
        
        Debug.Log($"[WaveManager] Starting Wave {_currentWave}: {enemiesThisWave} enemies, basic ground health {BasicGroundHealth(_currentWave)}.");

        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (_enemiesRemainingToSpawn > 0)
        {
            int spawnCount = 1;
            
            float multiChance = Mathf.Min(
                BaseMultiSpawnChance + (_currentWave * MultiSpawnChanceScaling),
                MaxMultiSpawnChance
            );
            
            if ((_definition == null || _definition.AllowMultiSpawn) && Random.value < multiChance)
            {
                int extraSpawnsThisWave = BaseExtraSpawns + (_currentWave / Mathf.Max(1, ExtraSpawnWaveInterval));
                extraSpawnsThisWave = Mathf.Min(extraSpawnsThisWave, MaxExtraSpawns);
                
                int extras = Random.Range(1, extraSpawnsThisWave + 1);
                spawnCount += extras;
            }
            
            spawnCount = Mathf.Min(spawnCount, _enemiesRemainingToSpawn);
            
            for (int i = 0; i < spawnCount; i++)
            {
                // Never catch up with a same-frame burst after a pause or a slow frame.
                while (Time.timeScale == 0 || !HasSpawnCapacity) yield return null;
                SpawnRequest request = _spawnPlan[_spawnIndex];
                if (request.DelayBefore > 0) yield return new WaitForSeconds(request.DelayBefore);
                if (!SpawnEnemy(request))
                {
                    HasWaveError = true;
                    Debug.LogError("[WaveManager] Assign valid enemy prefabs and spawn points. Wave spawning stopped.");
                    yield break;
                }
                _enemiesRemainingToSpawn--;
                _spawnIndex++;
                float interval = request.Interval >= 0 ? request.Interval : _definition != null && _definition.SpawnInterval >= 0 ? _definition.SpawnInterval :
                    i == spawnCount - 1 ? Mathf.Max(MinSpawnInterval, BaseSpawnInterval / (1 + _currentWave * SpawnIntervalScaling)) : MinimumTimeBetweenEnemies;
                yield return new WaitForSeconds(Mathf.Max(MinimumTimeBetweenEnemies, interval));
            }

        }
    }

    bool SpawnEnemy(SpawnRequest request)
    {
        if (SpawnPoints == null || SpawnPoints.Length == 0) return false;

        Transform spawnPoint = SpawnPoints[request.SpawnPointIndex >= 0 ? request.SpawnPointIndex : Random.Range(0, SpawnPoints.Length)];
        GameObject prefabToSpawn = request.Prefab;
        if (spawnPoint == null || prefabToSpawn == null || prefabToSpawn.GetComponent<EnemyBase>() == null) return false;
        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPoint.position, Quaternion.identity);

        EnemyBase enemyScript = newEnemy.GetComponent<EnemyBase>();
        
        if (enemyScript != null)
        {
            enemyScript.IsElite = request.Elite;
            enemyScript.Initialize(_currentWave);
        }
        return true;
    }

    public bool TryBuildWave(int wave, WaveDefinition definition, List<SpawnRequest> plan, out string error)
    {
        plan.Clear(); error = null;
        if (SpawnPoints == null || SpawnPoints.Length == 0 || System.Array.Exists(SpawnPoints, p => p == null))
        { error = "Assign all spawn points before starting waves."; return false; }
        bool Excluded(GameObject prefab) => definition != null && definition.ExcludedEnemies.Contains(prefab);
        bool Valid(GameObject prefab) => prefab != null && prefab.GetComponent<EnemyBase>() != null;
        if (definition != null && definition.Mode != WaveSpawnMode.Random)
        {
            foreach (var group in definition.OrderedSpawns)
            {
                if (group == null || !Valid(group.Prefab) || Excluded(group.Prefab) || group.Count <= 0 || group.SpawnPointIndex < -1 || group.SpawnPointIndex >= SpawnPoints.Length)
                { error = "Wave " + wave + " has an invalid or excluded ordered group."; return false; }
                for (int i = 0; i < group.Count; i++) plan.Add(new SpawnRequest { Prefab = group.Prefab, Elite = group.Elite,
                    SpawnPointIndex = group.SpawnPointIndex, DelayBefore = i == 0 ? group.DelayBefore : 0, Interval = group.Interval });
            }
        }
        if (definition != null && definition.Mode == WaveSpawnMode.Scripted)
        {
            if (plan.Count == 0) { error = "Scripted wave " + wave + " is empty."; return false; }
            return true;
        }
        int total = definition != null && definition.EnemyCount > 0 ? definition.EnemyCount : 2 + wave * 2;
        if (plan.Count > total) { error = "Ordered group count exceeds wave " + wave + " total."; return false; }
        if (plan.Count == total) return true;
        var options = new List<WaveEnemyOption>();
        if (definition != null && definition.RandomEnemies.Count > 0)
        {
            foreach (var option in definition.RandomEnemies)
            {
                if (option == null || !Valid(option.Prefab) || option.Weight < 0 || float.IsNaN(option.Weight) || float.IsInfinity(option.Weight))
                { error = "Wave " + wave + " has an invalid random option."; return false; }
                if (!Excluded(option.Prefab) && option.Weight > 0) options.Add(option);
            }
        }
        else if (EnemyPrefabs != null)
        {
            foreach (var prefab in EnemyPrefabs)
            {
                if (!Valid(prefab) || Excluded(prefab)) continue;
                string kind = prefab.GetComponent<EnemyBase>().EnemyKind;
                int kindIndex = kind == "ground" ? 0 : kind == "flying" ? 1 : kind == "tank" ? 2 : 3;
                if (UseIntroductoryWaves && wave < NormalUnlockWaves[kindIndex]) continue;
                bool eliteUnlocked = !UseIntroductoryWaves || wave >= EliteUnlockWaves[kindIndex];
                options.Add(new WaveEnemyOption { Prefab = prefab, Weight = 1, EliteChance = eliteUnlocked ? EliteChance : 0 });
                // Guarantee the introduction is the first spawn; custom definitions control their own order.
                if (definition == null && UseIntroductoryWaves && (wave == NormalUnlockWaves[kindIndex] || wave == EliteUnlockWaves[kindIndex]))
                    plan.Add(new SpawnRequest { Prefab = prefab, Elite = wave == EliteUnlockWaves[kindIndex], SpawnPointIndex = -1, Interval = -1 });
            }
        }
        float weight = 0;
        foreach (var option in options) weight += option.Weight;
        if (weight <= 0) { error = "Wave " + wave + " has no eligible random enemies after exclusions."; return false; }
        while (plan.Count < total)
        {
            float roll = Random.value * weight;
            WaveEnemyOption selected = options[options.Count - 1];
            foreach (var option in options) { roll -= option.Weight; if (roll <= 0) { selected = option; break; } }
            plan.Add(new SpawnRequest { Prefab = selected.Prefab, Elite = Random.value < selected.EliteChance, SpawnPointIndex = -1, Interval = -1 });
        }
        return true;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void OnEnemyKilled()
    {
        _enemiesAlive--;
        if (_enemiesAlive < 0) _enemiesAlive = 0;

        if (UI != null) UI.UpdateEnemiesLeft(_enemiesAlive);

        if (_enemiesAlive == 0 && _isWaveActive)
        {
            EndWave();
        }
    }

    void EndWave()
    {
        _isWaveActive = false;
        
        if (LevelManager.Instance != null) LevelManager.Instance.OnWaveComplete();

        StartCoroutine(StartNextWave());
    }

    public int GetCurrentWave()
    {
        return _currentWave;
    }
    
    public int GetCurrentDifficultyTier()
    {
        if (WavesPerTier <= 0) return 1;
        return (_currentWave / WavesPerTier) + 1;
    }
    
    public float GetDifficultyScalingMultiplier()
    {
        int tier = GetCurrentDifficultyTier();
        return Mathf.Pow(DifficultyTierMultiplier, tier - 1);
    }
}
