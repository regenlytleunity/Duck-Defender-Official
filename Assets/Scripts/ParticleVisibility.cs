using UnityEngine;

// Only hides particle renderers; colliders, hazard telegraphs and gameplay scripts
// keep running. OnEnable also handles newly spawned and reused pooled effects.
[RequireComponent(typeof(ParticleSystemRenderer))]
public class ParticleVisibility : MonoBehaviour
{
    public const string PreferenceKey = "DuckDefender_Particles";
    public static bool Enabled { get; private set; } = true;
    static event System.Action Changed;
    ParticleSystemRenderer _renderer;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Load() { Changed = null; Enabled = PlayerPrefs.GetInt(PreferenceKey, 1) != 0; }
    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
    void OnEnable() { _renderer = GetComponent<ParticleSystemRenderer>(); Changed += Apply; Apply(); }
    void OnDisable() { Changed -= Apply; }
    void Apply() { if (_renderer != null) _renderer.forceRenderingOff = !Enabled; }
}
