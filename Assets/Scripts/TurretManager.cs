using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 1.4.11 ADDS:
/// - Elemental turret as 4th slot type
/// - 2x2 layout when all 4 main turrets active (Marksman/Medic/Protector/Elemental)
/// - Second Wind angel as a separate hovering visual (below the main turret row)
/// </summary>
public class TurretManager : MonoBehaviour
{
    public static TurretManager Instance;

    [Header("Prefabs")]
    public GameObject MarksmanPrefab;       // Renamed from FlingerPrefab
    public GameObject MedicPrefab;
    public GameObject ProtectorPrefab;
    public GameObject ElementalPrefab;      // 1.4.11 NEW
    public GameObject SecondWindAngelPrefab; // 1.4.11 NEW

    [Header("Layout")]
    [Tooltip("Horizontal distance between turret slots when in linear layout (1-3 turrets).")]
    public float SlotSpacing = 1.5f;

    [Tooltip("Vertical spacing between top and bottom rows in the 2x2 layout (4 turrets).")]
    public float Layout2x2VerticalSpacing = 1.0f;

    [Tooltip("Horizontal spacing between left/right columns in the 2x2 layout.")]
    public float Layout2x2HorizontalSpacing = 1.5f;

    [Header("Second Wind Angel Placement")]
    [Tooltip("Vertical offset of the angel relative to the player. Should be ABOVE the player " +
             "but BELOW where the main turrets hover (turret HoverHeight is usually 2.5).")]
    public float AngelVerticalOffset = 1.6f;

    [Header("Player Reference")]
    public bool AutoFindPlayer = true;
    public Transform PlayerTransform;

    private Dictionary<TurretBase.TurretSlotType, TurretBase> _activeTurrets =
        new Dictionary<TurretBase.TurretSlotType, TurretBase>();

    private SecondWindAngel _activeAngel;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (AutoFindPlayer && PlayerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) PlayerTransform = playerObj.transform;
        }

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnTurretsChanged += RefreshTurrets;
        }

        RefreshTurrets();
    }

    void OnDestroy()
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnTurretsChanged -= RefreshTurrets;
        }
    }

    void RefreshTurrets()
    {
        if (PlayerStats.Instance == null) return;
        if (PlayerTransform == null) return;

        // Spawn each turret if unlocked and not already spawned
        TrySpawnTurret(PlayerStats.Instance.HasMarksman, MarksmanPrefab, TurretBase.TurretSlotType.Marksman);
        TrySpawnTurret(PlayerStats.Instance.HasMedic, MedicPrefab, TurretBase.TurretSlotType.Medic);
        TrySpawnTurret(PlayerStats.Instance.HasProtector, ProtectorPrefab, TurretBase.TurretSlotType.Protector);
        TrySpawnTurret(PlayerStats.Instance.HasElementalTurret, ElementalPrefab, TurretBase.TurretSlotType.Elemental);

        // Second Wind angel: spawn if Second Wind is unlocked.
        // Visibility (alpha) is handled by SecondWindAngel itself based on cooldown.
        if (PlayerStats.Instance.HasSecondWind && _activeAngel == null && SecondWindAngelPrefab != null)
        {
            GameObject angelObj = Instantiate(SecondWindAngelPrefab);
            _activeAngel = angelObj.GetComponent<SecondWindAngel>();
            if (_activeAngel != null)
            {
                _activeAngel.HoverHeight = AngelVerticalOffset;
                _activeAngel.Configure(PlayerTransform, Vector3.zero);
            }
        }

        AssignSlots();
    }

    void TrySpawnTurret(bool shouldExist, GameObject prefab, TurretBase.TurretSlotType slotType)
    {
        if (!shouldExist) return;
        if (_activeTurrets.ContainsKey(slotType)) return;
        if (prefab == null)
        {
            Debug.LogError($"[TurretManager] '{slotType}' is unlocked but its prefab isn't assigned!");
            return;
        }

        GameObject turretObj = Instantiate(prefab);
        TurretBase turret = turretObj.GetComponent<TurretBase>();
        if (turret == null)
        {
            Debug.LogError($"[TurretManager] '{prefab.name}' has no TurretBase-derived component!");
            Destroy(turretObj);
            return;
        }

        turret.Configure(PlayerTransform, Vector3.zero);
        _activeTurrets[slotType] = turret;
    }

    /// <summary>
    /// Distributes active turrets above the player.
    /// 1-3 turrets: linear horizontal layout (centered).
    /// 4 turrets: 2x2 box layout.
    /// </summary>
    void AssignSlots()
    {
        // Build the ordered list: Marksman, Medic, Protector, Elemental
        List<TurretBase> ordered = new List<TurretBase>();
        if (_activeTurrets.TryGetValue(TurretBase.TurretSlotType.Marksman, out var m)) ordered.Add(m);
        if (_activeTurrets.TryGetValue(TurretBase.TurretSlotType.Medic, out var md)) ordered.Add(md);
        if (_activeTurrets.TryGetValue(TurretBase.TurretSlotType.Protector, out var p)) ordered.Add(p);
        if (_activeTurrets.TryGetValue(TurretBase.TurretSlotType.Elemental, out var e)) ordered.Add(e);

        if (ordered.Count == 0) return;

        // 1.4.11: 2x2 box layout when all 4 are present
        if (ordered.Count == 4)
        {
            // 2x2 grid offsets (x, y):
            // (-h, +v)  (+h, +v)    <- top row
            // (-h, -v)  (+h, -v)    <- bottom row
            float h = Layout2x2HorizontalSpacing * 0.5f;
            float v = Layout2x2VerticalSpacing * 0.5f;

            Vector3[] gridOffsets = new Vector3[]
            {
                new Vector3(-h, +v, 0),  // top-left
                new Vector3(+h, +v, 0),  // top-right
                new Vector3(-h, -v, 0),  // bottom-left
                new Vector3(+h, -v, 0),  // bottom-right
            };

            for (int i = 0; i < 4; i++)
            {
                ordered[i].UpdateSlotOffset(gridOffsets[i]);
            }
            return;
        }

        // Linear horizontal layout for 1-3 turrets
        float center = (ordered.Count - 1) * 0.5f;
        for (int i = 0; i < ordered.Count; i++)
        {
            float xOffset = (i - center) * SlotSpacing;
            ordered[i].UpdateSlotOffset(new Vector3(xOffset, 0f, 0f));
        }
    }
}