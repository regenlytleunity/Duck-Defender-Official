using System;
using System.Collections.Generic;
using UnityEngine;

public enum WaveSpawnMode { Random, Scripted, Mixed }

[Serializable]
public class WaveEnemyOption
{
    public GameObject Prefab;
    [Min(0)] public float Weight = 1;
    [Range(0, 1)] public float EliteChance;
}

[Serializable]
public class WaveSpawnGroup
{
    public GameObject Prefab;
    [Min(1)] public int Count = 1;
    public bool Elite;
    [Tooltip("-1 chooses a random spawn point; otherwise uses the WaveManager index.")]
    public int SpawnPointIndex = -1;
    [Min(0)] public float DelayBefore;
    [Tooltip("-1 uses the wave interval.")]
    public float Interval = -1;
}

[CreateAssetMenu(menuName = "Duck Defender/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    [Min(1)] public int WaveNumber = 1;
    public WaveSpawnMode Mode;
    [Tooltip("Random/mixed total, including ordered groups. 0 uses the normal wave count. Scripted uses only group counts.")]
    [Min(0)] public int EnemyCount;
    [Tooltip("-1 uses WaveManager's normal interval.")]
    public float SpawnInterval = -1;
    public bool AllowMultiSpawn;
    [Tooltip("Spawned in list order before any random fill. Exclusions also apply to these groups.")]
    public List<WaveSpawnGroup> OrderedSpawns = new List<WaveSpawnGroup>();
    [Tooltip("Empty uses WaveManager's unlocked enemies. Otherwise these are the allowed random enemies. Weights are normalized (e.g. 70/30 = 70%/30%).")]
    public List<WaveEnemyOption> RandomEnemies = new List<WaveEnemyOption>();
    public List<GameObject> ExcludedEnemies = new List<GameObject>();
}
