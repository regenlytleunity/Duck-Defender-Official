using System.Collections.Generic;
using UnityEngine;

public enum RunDifficulty { Easy, Medium, Hard }

// Collection inputs are fixed once per run. Spawn schedules and combat/reward rates stay unchanged.
public static class GameDifficulty
{
    public const float TutorialBaselineProduct = 72f;
    public const float ProgressionDivisor = 53f;
    public const float StartingHealthPerPoint = .03f;
    public const float WaveHealthPerPoint = .02f;

    public readonly struct CollectionScaling
    {
        public readonly int UnlockedCards, CombinedLevels;
        public readonly float Score, StartingMultiplier, WaveMultiplier;
        public CollectionScaling(int unlockedCards, int combinedLevels)
        {
            UnlockedCards = Mathf.Max(0, unlockedCards);
            CombinedLevels = Mathf.Max(0, combinedLevels);
            Score = (float)System.Math.Max(0, ((double)UnlockedCards * CombinedLevels - TutorialBaselineProduct) / ProgressionDivisor);
            StartingMultiplier = 1 + StartingHealthPerPoint * Score;
            WaveMultiplier = 1 + WaveHealthPerPoint * Score;
        }
        public float Health(float baseHealth, int wave) =>
            baseHealth * StartingMultiplier + WaveManager.HealthIncreaseAtWave(wave) * WaveMultiplier;
    }

    public static CollectionScaling Current { get; private set; } = new CollectionScaling(0, 0);

    public static CollectionScaling ReadCollection(PlayerData data, IEnumerable<CardDefinition> registeredCards)
    {
        int count = 0, levels = 0;
        var counted = new HashSet<string>();
        if (data?.CardCollection != null && registeredCards != null)
            foreach (var card in registeredCards)
            {
                if (card == null || card.IsBasic || (int)card.PackCategory < 0 || (int)card.PackCategory > 3 ||
                    string.IsNullOrEmpty(card.ID) || !counted.Add(card.ID)) continue;
                var saved = data.CardCollection.Find(c => c != null && c.CardID == card.ID && c.IsUnlocked);
                if (saved == null) continue;
                count++;
                levels += Mathf.Clamp(saved.Level, 1, card.MaxLevel);
            }
        return new CollectionScaling(count, levels);
    }

    public static void BeginRun(PlayerData data, IEnumerable<CardDefinition> registeredCards) => Current = ReadCollection(data, registeredCards);
    public static float HealthAtWave(float baseHealth, int wave) => Current.Health(baseHealth, wave);

    // Compatibility for serialized callbacks and older tools. Saved choices no longer apply.
    public const string PreferenceKey = "DuckDefender_Difficulty";
    public static RunDifficulty Selected => RunDifficulty.Easy;
    public static float HealthMultiplier => Current.StartingMultiplier;
    public static float DamageMultiplier => 1f;
    public static float SpeedMultiplier => 1f;
    public static float GrowthMultiplier => Current.WaveMultiplier;
    public static float CoinMultiplier => 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load() => Current = new CollectionScaling(0, 0);
    public static void Select(int difficulty, bool save = true) { }
}
