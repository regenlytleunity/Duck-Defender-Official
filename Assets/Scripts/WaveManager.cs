using UnityEngine;
using System.Collections;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance;

    [Header("Configuration")]
    public GameObject[] EnemyPrefabs; 
    public Transform[] SpawnPoints; 
    public GameUI UI;

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
        
        int enemiesThisWave = 2 + (_currentWave * 2);
        
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
        
        int currentTier = GetCurrentDifficultyTier();
        Debug.Log($"[WaveManager] Starting Wave {_currentWave} (Difficulty Tier {currentTier}, scaling mult: {GetDifficultyScalingMultiplier():F2}x)");

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
            
            if (Random.value < multiChance)
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
                if (!SpawnEnemy())
                {
                    Debug.LogError("[WaveManager] Assign valid enemy prefabs and spawn points. Wave spawning stopped.");
                    yield break;
                }
                _enemiesRemainingToSpawn--;
                yield return new WaitForSeconds(Mathf.Max(.01f, MinimumTimeBetweenEnemies));
            }

            float currentInterval = BaseSpawnInterval * (1f / (1f + (_currentWave * SpawnIntervalScaling)));
            currentInterval = Mathf.Max(currentInterval, MinSpawnInterval);
            
            yield return new WaitForSeconds(currentInterval);
        }
    }

    bool SpawnEnemy()
    {
        if (SpawnPoints == null || EnemyPrefabs == null || SpawnPoints.Length == 0 || EnemyPrefabs.Length == 0) return false;

        Transform spawnPoint = SpawnPoints[Random.Range(0, SpawnPoints.Length)];
        GameObject prefabToSpawn = EnemyPrefabs[Random.Range(0, EnemyPrefabs.Length)];
        if (spawnPoint == null || prefabToSpawn == null || prefabToSpawn.GetComponent<EnemyBase>() == null) return false;
        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPoint.position, Quaternion.identity);

        EnemyBase enemyScript = newEnemy.GetComponent<EnemyBase>();
        
        if (enemyScript != null)
        {
            enemyScript.Initialize(_currentWave);
        }
        return true;
    }

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
