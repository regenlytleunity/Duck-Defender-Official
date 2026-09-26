using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 1.4.11: Per outline (clarification 8): "Each aura is its own independent aura, we can make 
/// the damaging auras default size slightly smaller so that the edges of each aura don't overlap."
/// 
/// The visual ring is drawn slightly INSIDE the actual damage radius now, so when stacked with 
/// the slowing aura they don't visually collide.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class AuraController : MonoBehaviour
{
    readonly System.Collections.Generic.List<EnemyBase> _damageTargets = new System.Collections.Generic.List<EnemyBase>();
    [Header("Visual Settings")]
    public int Segments = 60;
    public float LineWidth = 0.15f;
    public Color ActiveColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    public int SortingOrder = 100;

    [Header("Visual vs Damage Mismatch (1.4.11)")]
    [Tooltip("Visual radius is drawn at (damageRadius * VisualInsetFactor) so two stacked auras don't overlap visually. " +
             "Damage still applies at the true radius.")]
    [Range(0.7f, 1f)]
    public float VisualInsetFactor = 0.9f;

    private LineRenderer _lineRenderer;
    private float _tickTimer;
    private float _currentRadius;
    private float _currentDamage;

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

    public void UpdateAura(float radius, float damage)
    {
        if (PlayerStats.Instance != null && PlayerStats.Instance.HasAscension(CardAscension.CursorAura))
        {
            Camera cam = Camera.main;
            if (cam != null) { Vector3 point = cam.ScreenToWorldPoint(InputHelper.GetMousePosition()); point.z = 0; transform.position = point; }
        }
        else if (PlayerStats.Instance != null) transform.position = PlayerStats.Instance.transform.position;
        _currentRadius = radius;
        _currentDamage = damage;

        if (_currentDamage > 0)
        {
            _lineRenderer.enabled = true;
            // 1.4.11: visual is slightly inset
            DrawCircle(_currentRadius * VisualInsetFactor);

            float alpha = 0.3f + Mathf.PingPong(Time.time * 2f, 0.3f);
            Color c = ActiveColor;
            c.a = alpha;
            _lineRenderer.startColor = c;
            _lineRenderer.endColor = c;
        }
        else
        {
            _lineRenderer.enabled = false;
        }

        if (_currentDamage > 0)
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= 1.0f)
            {
                _tickTimer -= 1f;
                PulseDamage();
            }
        }
    }

    void PulseDamage()
    {
        float damage = PlayerStats.Instance != null ? PlayerStats.Instance.CalculateDamage(_currentDamage, false) : _currentDamage;
        EnemyBase.CopyActiveEnemies(_damageTargets);
        foreach (var enemy in _damageTargets)
            if (enemy != null && enemy.IsAlive && Vector2.Distance(transform.position, enemy.transform.position) <= _currentRadius)
                enemy.TakeFractionalDamage(damage);
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
