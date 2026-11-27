using System;
using UnityEngine;

public enum DailyQuestKind { Time, Kills, Gold, Boxes, Packs, Movement }

[Serializable]
public class DailyQuestData
{
    public long Day = -1;
    public int[] Kinds = new int[3];
    public int[] Rewards = new int[3];
    public float[] Progress = new float[3];
    public bool[] Claimed = new bool[3];
    public int Bonus;
}

public static class DailyQuests
{
    public static readonly int[] Targets = { 600, 100, 1000, 1, 5, 250 };
    public static readonly string[] Titles = { "Play for 10 minutes", "Defeat 100 enemies", "Collect 1000 gold", "Open an item box", "Purchase 5 card packs", "Move 250 units" };
    public static readonly string[] Rewards = { "500 gold", "4 common cards", "2 rare cards", "1 legendary card", "1000 gold", "16 common cards", "8 rare cards", "1 key", "4 legendary cards", "Maxed common card", "2500 gold", "3 keys", "10000 gold", "Maxed rare card", "Maxed legendary card", "10 keys" };
    public static readonly float[] Weights = { 28, 14, 14, 6, 6, 6, 6, 6, 3, 3, 3, 3, .5f, .5f, .5f, .5f };
    static readonly float[] _pending = new float[6];
    static long _pendingDay = -1;
    public static long Day(long seconds) => (long)Math.Floor((seconds - 18000) / 86400.0);
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static long ResetAt => (Day(Now) + 1) * 86400 + 18000;

    public static bool Ensure(PlayerData data, long seconds = -1, bool force = false)
    {
        if (data.Tutorial.CoreActive) return false;
        long day = Day(seconds < 0 ? Now : seconds);
        var daily = data.Daily;
        if (!force && daily != null && daily.Day >= day && daily.Kinds?.Length == 3 && daily.Rewards?.Length == 3 &&
            daily.Progress?.Length == 3 && daily.Claimed?.Length == 3 &&
            daily.Bonus >= 0 && daily.Bonus < 3 &&
            Array.TrueForAll(daily.Kinds, value => value >= 0 && value < Targets.Length) &&
            Array.TrueForAll(daily.Rewards, value => value >= 0 && value < Rewards.Length)) return false;
        daily = data.Daily = new DailyQuestData { Day = day, Bonus = UnityEngine.Random.Range(0, 3) };
        var kinds = new int[] { 0, 1, 2, 3, 4, 5 };
        for (int i = 0; i < 3; i++)
        {
            int pick = UnityEngine.Random.Range(i, kinds.Length);
            int swap = kinds[i]; kinds[i] = kinds[pick]; kinds[pick] = swap;
            daily.Kinds[i] = kinds[i];
            float roll = UnityEngine.Random.value * 100;
            int reward = 0;
            while (reward < Weights.Length - 1 && roll >= Weights[reward]) roll -= Weights[reward++];
            daily.Rewards[i] = reward;
        }
        return true;
    }
    public static void Record(PlayerData data, DailyQuestKind kind, float amount)
    {
        if (data.Tutorial.CoreActive || amount <= 0) return;
        Ensure(data);
        for (int i = 0; i < 3; i++)
            if (data.Daily.Kinds[i] == (int)kind && !data.Daily.Claimed[i])
                data.Daily.Progress[i] = Mathf.Min(Targets[(int)kind], data.Daily.Progress[i] + amount);
    }
    public static void RecordRun(DailyQuestKind kind, float amount)
    {
        if (TutorialDirector.CoreActive || amount <= 0) return;
        long day = Day(Now);
        if (_pendingDay != day) { Flush(); _pendingDay = day; }
        _pending[(int)kind] += amount;
    }
    public static void Flush()
    {
        if (!Array.Exists(_pending, value => value > 0)) return;
        var data = SaveSystem.LoadData();
        if (_pendingDay == Day(Now))
            for (int i = 0; i < _pending.Length; i++) Record(data, (DailyQuestKind)i, _pending[i]);
        Array.Clear(_pending, 0, _pending.Length);
        SaveSystem.SaveData(data);
    }
}
