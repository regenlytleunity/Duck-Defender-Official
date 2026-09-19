using UnityEngine;

/// <summary>
/// 1.4.11 NEW: Visual line-renderer ring for the Slowing Aura upgrade.
/// 
/// Parallels AuraController but with a different color and reads from 
/// PlayerStats.HasSlowingAura / SlowingAuraRadius / SlowingAuraSlowPercent.
/// 
/// The slow effect itself is applied in EnemyBase.ApplySlowingAuraIfNearby() - 
/// this script is purely visual.
/// 
/// SETUP:
/// - Add this component to a child of the Player GameObject (next to the existing 
///   AuraController child)
/// - It will draw a circle automatically; no other configuration needed
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class SlowingAuraController : MonoBehaviour
{
    [Header("Visual Settings")]
    public int Segments = 60;
    public float LineWidth = 0.12f;
    
    [Tooltip("Color of the slowing aura ring. Defaults to cyan/blue to match the slow visual.")]
    public Color RingColor = new Color(0.4f, 0.8f, 1f, 0.7f);
    
    [Tooltip("Sorting order - keep at 100+ so it draws above the background.")]
    public int SortingOrder = 99; // Just below damage aura so they don't overlap visually
    
    [Header("Visual vs Damage Mismatch")]
    [Tooltip("Visual radius is drawn at (slowRadius * VisualInsetFactor) so stacked auras don't visually collide.")]
    [Range(0.7f, 1f)]
    public float VisualInsetFactor = 0.95f;

    private LineRenderer _lineRenderer;

    void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.loop = true;
        _lineRenderer.positionCount = Segments;
        _lineRenderer.startWidth = LineWidth;
        _lineRenderer.endWidth = LineWidth;
        _lineRenderer.sortingOrder = SortingOrder;
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    void Update()
    {
        if (PlayerStats.Instance == null || !PlayerStats.Instance.HasSlowingAura)
        {
            _lineRenderer.enabled = false;
            return;
        }

        _lineRenderer.enabled = true;
        DrawCircle(PlayerStats.Instance.SlowingAuraRadius * VisualInsetFactor);

        // Subtle pulse - slightly less aggressive than the damage aura
        float alpha = RingColor.a * (0.7f + Mathf.PingPong(Time.time * 1.5f, 0.3f));
        Color c = RingColor;
        c.a = Mathf.Clamp01(alpha);
        _lineRenderer.startColor = c;
        _lineRenderer.endColor = c;
    }

    void DrawCircle(float radius)
    {
        float angleStep = 360f / Segments;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            _lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
        }
    }
}
