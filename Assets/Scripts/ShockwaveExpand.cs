using UnityEngine;

/// <summary>
/// Drives the expanding-ring animation on the Protector shockwave VFX.
/// Scales up from 0 to TargetScale over Duration seconds, fading out alpha as it grows.
/// 
/// This is the simplest way to get a clean expanding-ring effect - particles don't 
/// easily produce a smooth ring shape, but a single sprite scaled over time looks 
/// great with minimal setup.
/// 
/// SETUP:
/// - Attach to the ProtectorShockwave VFX root (has a SpriteRenderer)
/// - The ProtectorTurret passes radius via its existing scaling line:
///       fx.transform.localScale = Vector3.one * (radius / 3.0f);
///   This script reads that initial scale as the TARGET, then animates from 0 to that.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ShockwaveExpand : MonoBehaviour
{
    [Tooltip("How long the expansion lasts in seconds.")]
    public float Duration = 0.4f;
    
    [Tooltip("Animation curve for the expansion. Ease-out (fast at start, slow at end) feels punchy.")]
    public AnimationCurve ExpansionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Tooltip("Starting alpha at scale=0.")]
    [Range(0f, 1f)]
    public float StartAlpha = 0.8f;

    private SpriteRenderer _sr;
    private Vector3 _targetScale;
    private float _elapsed = 0f;
    private Color _baseColor;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
        
        _baseColor = _sr.color;
        Color c = _baseColor;
        c.a = StartAlpha;
        _sr.color = c;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / Duration);
        float curved = ExpansionCurve.Evaluate(t);

        // Scale up from 0 to target
        transform.localScale = _targetScale * curved;

        // Fade alpha from StartAlpha to 0
        Color c = _baseColor;
        c.a = Mathf.Lerp(StartAlpha, 0f, t);
        _sr.color = c;

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
