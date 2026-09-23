using UnityEngine;

/// <summary>
/// 1.4.11 ADDS:
/// - SpawnPassiveCoin(): physical coin spawn used by CoinsPerSecond and CoinsPerWave upgrades.
/// - Wave-complete coin gift now spawns physical coins instead of just incrementing the counter.
/// 
/// Per outline (page 4): "Upgrades that give the player coins per second or coins per wave should 
/// spawn in coins on the player instead of just increasing the coin count and give them a small 
/// force pushing them away from the player before the player can collect them, this will make 
/// these upgrades feel more satisfying to use."
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Progression")]
    public int CurrentLevel = 1;
    public int CurrentXP = 0;
    public int TargetXP = 100;
    public float GrowthFactor = 1.2f;

    [Header("Upgrades")]
    public float XPMultiplier = 1.0f;
    public int CoinsPerWave = 0;

    [Header("Economy")]
    public int TotalCoins = 0;

    [Header("Passive Coin Spawning (1.4.11)")]
    [Tooltip("Prefab used for spawning coins when the player gains coins from per-second or per-wave upgrades. " +
             "If null, will fall back to incrementing the counter directly.")]
    public GameObject PassiveCoinPrefab;

    private PlayerData _playerData;
    private PlayerHealth _playerHealth;

    private int _coinsForMeteor = 0;
    private int _coinsForShot = 0;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _playerData = SaveSystem.LoadData();
        TotalCoins = _playerData.TotalCoins;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player) _playerHealth = player.GetComponent<PlayerHealth>();

        UpdateUI();
        UpdateCoinUI();
    }

    public void OnWaveComplete()
    {
        if (PlayerStats.Instance != null) PlayerStats.Instance.CompleteWave();
        // 1.4.11: Wave-end coin gift now spawns physical coins instead of silently adding to counter
        if (CoinsPerWave > 0)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            Vector3 spawnPos = playerObj != null ? playerObj.transform.position : Vector3.zero;
            for (int i = 0; i < PlayerStats.BoostCount(CoinsPerWave); i++)
            {
                SpawnPassiveCoin(spawnPos);
            }
        }

        // Interest still works as a direct counter add (it's an abstract "bank yield" not loot)
        if (PlayerStats.Instance != null && PlayerStats.Instance.InterestRate > 0)
        {
            int interest = Mathf.FloorToInt(TotalCoins * PlayerStats.Instance.InterestRate);
            if (interest > 0) AddCoins(interest);
        }

        if (_playerHealth != null) _playerHealth.ApplyWaveRegen();
    }

    public void AddXP(int amount)
    {
        float totalMultiplier = XPMultiplier;
        if (PlayerStats.Instance != null) totalMultiplier = PlayerStats.Instance.XPMultiplier + PlayerStats.Instance.RebirthStatBonus;

        int finalXP = Mathf.RoundToInt(amount * totalMultiplier);
        CurrentXP = (int)System.Math.Min(int.MaxValue, (long)CurrentXP + Mathf.Max(0, finalXP));

        // Rebirth can defeat an entire screen in one frame. Preserve every earned choice.
        while (TargetXP > 0 && CurrentXP >= TargetXP) LevelUp();
        UpdateUI();
    }

    /// <summary>
    /// Adds coins directly to the counter. Use this for actual pickup collection (the Coin script calls this).
    /// For per-second/per-wave gifts, prefer SpawnPassiveCoin() which physically spawns coins.
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        TotalCoins = (int)System.Math.Min(int.MaxValue, (long)TotalCoins + amount);
        _playerData.TotalCoins = TotalCoins;
        SaveSystem.SaveData(_playerData);
        UpdateCoinUI();

        if (PlayerStats.Instance == null)
        {
            PlayerStats.Instance = FindFirstObjectByType<PlayerStats>();
            if (PlayerStats.Instance == null) return;
        }

        PlayerStats.Instance.ReportCoinsGained(amount);

        if (PlayerController.Instance == null)
        {
            PlayerController.Instance = FindFirstObjectByType<PlayerController>();
        }

        // Coin Meteor trigger
        if (PlayerStats.Instance.HasCoinMeteors)
        {
            _coinsForMeteor += amount;
            int threshold = PlayerStats.Threshold(PlayerStats.Instance.MeteorThreshold);
            if (threshold <= 0) threshold = 10;

            while (_coinsForMeteor >= threshold)
            {
                _coinsForMeteor -= threshold;
                if (PlayerController.Instance != null) PlayerController.Instance.SpawnMeteor();
            }
        }

        // Tripleshot trigger
        if (PlayerStats.Instance.HasTripleshot)
        {
            _coinsForShot += amount;
            int threshold = PlayerStats.Threshold(PlayerStats.Instance.TripleshotThreshold);
            if (threshold <= 0) threshold = 15;

            if (_coinsForShot >= threshold)
            {
                _coinsForShot %= threshold;
                if (PlayerController.Instance != null) PlayerController.Instance.TriggerCoinShotBuff();
            }
        }
    }

    /// <summary>
    /// 1.4.11: Spawns a physical coin at the given position (typically the player's position).
    /// The coin pops outward with a small force, then auto-magnetizes back to the player.
    /// </summary>
    public void SpawnPassiveCoin(Vector3 spawnPos)
    {
        if (PassiveCoinPrefab == null)
        {
            // Fallback - if no prefab assigned, just increment the counter
            AddCoins(1);
            return;
        }

        GameObject coinObj = Instantiate(PassiveCoinPrefab, spawnPos, Quaternion.identity);
        Coin coin = coinObj.GetComponent<Coin>();
        if (coin != null)
        {
            coin.ConfigureAsPassiveCoin();
        }
    }

    void LevelUp()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Level_Up");

        CurrentXP -= TargetXP;
        CurrentLevel++;
        TargetXP = (int)System.Math.Min(int.MaxValue, System.Math.Max(1, System.Math.Round((double)TargetXP * GrowthFactor)));
        if (LevelUpUI.Instance != null) LevelUpUI.Instance.ShowLevelUpOptions();
    }

    void UpdateUI()
    {
        if (GameUI.Instance != null) GameUI.Instance.UpdateXPBar(CurrentXP, TargetXP, CurrentLevel);
    }

    void UpdateCoinUI()
    {
        if (GameUI.Instance != null) GameUI.Instance.UpdateCoinText(TotalCoins);
    }
}
