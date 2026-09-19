using UnityEngine;

/// <summary>
/// Medic turret: heals the player a flat amount every tick.
/// 1.4.11: No functional changes; pulled into the new turret slot system.
/// </summary>
public class MedicTurret : TurretBase
{
    public override TurretSlotType TurretType => TurretSlotType.Medic;

    [Header("Medic Settings")]
    public GameObject HealEffectPrefab;
    public string HealSoundName = "Feather_Hit_Enemy";
    public bool SuppressEffectAtFullHP = true;

    private PlayerHealth _cachedPlayerHealth;

    protected override void Awake()
    {
        base.Awake();
        _cachedPlayerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    protected override float GetCurrentInterval()
    {
        if (PlayerStats.Instance == null) return 99f;
        return Mathf.Max(0.5f, PlayerStats.Instance.MedicInterval);
    }

    protected override void OnTick()
    {
        if (PlayerStats.Instance == null) return;
        if (_cachedPlayerHealth == null)
        {
            _cachedPlayerHealth = FindFirstObjectByType<PlayerHealth>();
            if (_cachedPlayerHealth == null) return;
        }

        int healAmount = Mathf.Max(1, PlayerStats.Instance.MedicHealAmount);

        bool atFullHealth = false;
        if (SuppressEffectAtFullHP)
        {
            atFullHealth = _cachedPlayerHealth.CurrentHealth >= _cachedPlayerHealth.MaxHealth;
        }

        _cachedPlayerHealth.Heal(healAmount);

        // 1.4.11 PATCH: spawn the heal effect on the TURRET, not the player. 
        // The medic turret is the source of healing visually, so the pulse should 
        // come from there.
        if (HealEffectPrefab != null && !atFullHealth)
        {
            Instantiate(HealEffectPrefab, transform.position, Quaternion.identity);
        }

        if (AudioManager.Instance != null && !string.IsNullOrEmpty(HealSoundName) && !atFullHealth)
        {
            AudioManager.Instance.PlaySFX(HealSoundName);
        }
    }
}