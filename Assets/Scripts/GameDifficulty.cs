using UnityEngine;

public enum RunDifficulty { Easy, Medium, Hard }

// Shared by the menu and existing run systems. Spawn schedules stay unchanged.
public static class GameDifficulty
{
    public const string PreferenceKey = "DuckDefender_Difficulty";
    public static RunDifficulty Selected { get; private set; } = RunDifficulty.Easy;
    public static float HealthMultiplier => Selected == RunDifficulty.Hard ? 2f : Selected == RunDifficulty.Medium ? 1.5f : 1f;
    public static float DamageMultiplier => Selected == RunDifficulty.Hard ? 2f : 1f;
    public static float SpeedMultiplier => Selected == RunDifficulty.Easy ? 1f : 1.1f;
    public static float GrowthMultiplier => Selected == RunDifficulty.Hard ? 2f : 1f;
    public static float CoinMultiplier => Selected == RunDifficulty.Hard ? 1.5f : Selected == RunDifficulty.Medium ? 1.25f : 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load() => Select(PlayerPrefs.GetInt(PreferenceKey, 0), false);

    public static void Select(int difficulty, bool save = true)
    {
        Selected = (RunDifficulty)Mathf.Clamp(difficulty, 0, 2);
        if (!save) return;
        PlayerPrefs.SetInt(PreferenceKey, (int)Selected);
        PlayerPrefs.Save();
    }
}
