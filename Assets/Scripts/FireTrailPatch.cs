using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A single patch of fire dropped by the player when the Fire Trail upgrade is active.
/// Damages enemies that overlap it on a tick interval, then despawns after a duration.
/// 
/// 1.4.13 BUGFIX (round 1): switched from OnTriggerEnter2D/Exit tracking to per-tick 
/// Physics2D.OverlapCircleAll polling. Trigger events missed enemies that were already 
/// overlapping the patch when it spawned, and missed everything entirely if the prefab's 
/// collider wasn't flagged as a trigger.
/// 
/// 1.4.13 BUGFIX (round 2):
///   - The patch was spawning CENTERED at the player's feet, which put its bottom half 
///     below the ground. Now self-corrects: on spawn, it shifts upward by half its sprite 
///     height so the BOTTOM of the visual sits at the spawn position. Spawn coordinates 
///     from the player-side code now semantically mean "where the bottom of the fire goes."
///   - Damage radius was a fixed default (0.75) that didn't match the visual sprite size, 
///     so enemies inside the visible flames at the edges took no damage. Now auto-derives 
///     from the sprite's actual bounds at spawn time, with an OverrideDamageRadius escape 
///     hatch for fine tuning.
/// 
/// SETUP:
/// - Create a prefab with a SpriteRenderer (or ParticleSystem) for the visual
/// - The sprite's PIVOT determines how the auto-position works. For best results, set 
///   the sprite's pivot to CENTER in its import settings. The script will then offset 
///   upward by half the sprite height to put the bottom on the ground.
/// - Attach this component
/// - Leave OverrideDamageRadius unchecked to auto-match the visual; check it and set 
///   DamageRadius manually if you want a tighter or looser hitbox than the visual
/// - Drag into PlayerController.FireTrailPatchPrefab
/// </summary>
public class FireTrailPatch : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("How often (in seconds) damage is applied to enemies inside the patch.")]
    public float DamageTickInterval = 0.5f;

    [Tooltip("If true, uses the value of DamageRadius below. If false, auto-detects radius " +
             "from the sprite's bounds at spawn time so the damage circle matches the visual.")]
    public bool OverrideDamageRadius = false;
    
    [Tooltip("Used only when OverrideDamageRadius is true. The radius of the damage circle " +
             "in world units. Default is auto-detected from the sprite size.")]
    public float DamageRadius = 0.75f;

    [Tooltip("If true, prints damage events to the console. Use for debugging.")]
    public bool DebugLog = false;

    [Header("Spawn Position")]
    [Tooltip("Vertical offset added to the patch's spawn position after auto-grounding. " +
             "Use a small positive value (~0.02) to ensure the sprite renders slightly above " +
             "the ground tile and doesn't z-fight. Set to 0 for exact ground contact.")]
    public float GroundOffset = 0.02f;

    [Tooltip("If true, the patch self-adjusts on spawn so the BOTTOM of its sprite sits at " +
             "the spawn position (rather than the center). Keep this on - it's why the spawn " +
             "doesn't sink into the ground.")]
    public bool AnchorBottomToSpawn = true;

    [Header("Visual")]
    [Tooltip("Optional sprite renderer for fade-out at end of life. Auto-detected if left empty.")]
    public SpriteRenderer Renderer;

    [Tooltip("If true, shows damage popups when the patch hits an enemy.")]
    public bool ShowDamagePopups = true;

    private int _damagePerTick;
    private float _duration;
    private float _timeSpawned;
    private float _nextTickTime;
    private float _resolvedDamageRadius;

    void Awake()
    {
        if (Renderer == null) Renderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// Called by PlayerController right after instantiation. Sets damage, duration, and 
    /// self-adjusts spawn position so the patch sits ON the ground rather than sunk into it.
    /// </summary>
    public void Initialize(int damagePerTick, float duration)
    {
        _damagePerTick = Mathf.Max(1, damagePerTick);
        _duration = Mathf.Max(0.5f, duration);
        _timeSpawned = Time.time;

        // Tick immediately on spawn so enemies standing right where the patch dropped 
        // get hit before they can walk away.
        _nextTickTime = Time.time;

        ResolveDamageRadius();
        AdjustSpawnPosition();
    }

    /// <summary>
    /// Decides what the actual damage radius will be for this patch. Either honors the 
    /// inspector override or auto-derives it from the sprite's world-space width.
    /// 
    /// Using sprite bounds gives us the visual size in world units after Transform scaling, 
    /// so the damage area always matches what the player sees regardless of how the prefab 
    /// is scaled. For a roughly-circular flame sprite, that's a good match. For elongated 
    /// sprites, you'll want to override this and pick a value that makes sense.
    /// </summary>
    void ResolveDamageRadius()
    {
        if (OverrideDamageRadius)
        {
            _resolvedDamageRadius = Mathf.Max(0.1f, DamageRadius);
            return;
        }

        if (Renderer != null && Renderer.sprite != null)
        {
            // Use the wider of x/y extents as the radius. Most flame sprites are taller 
            // than wide; using the larger dimension makes sure the damage circle covers 
            // the visible flames rather than cutting off the edges.
            Bounds b = Renderer.bounds;
            float radius = Mathf.Max(b.extents.x, b.extents.y);
            _resolvedDamageRadius = Mathf.Max(0.1f, radius);
        }
        else
        {
            // Sprite not found yet - fall back to the inspector value.
            _resolvedDamageRadius = Mathf.Max(0.1f, DamageRadius);
        }
    }

    /// <summary>
    /// Shifts the patch upward so the bottom of its sprite sits at the original spawn 
    /// position, rather than the sprite's center. This is what stops the patch from 
    /// looking like it's sinking into the ground.
    /// 
    /// The math:
    ///   - Renderer.bounds.min.y is the world-Y of the sprite's bottom edge AFTER its 
    ///     pivot is applied.
    ///   - transform.position.y is the pivot's world Y (where we spawned it).
    ///   - Difference = how far below the pivot the sprite's bottom currently is.
    ///   - Adding that difference + GroundOffset to the position puts the bottom edge 
    ///     at the original spawn Y plus a tiny render-clearance.
    /// 
    /// Works regardless of whether your sprite's pivot is set to Center, Bottom, or 
    /// anything else, because we're measuring against the actual rendered bounds.
    /// </summary>
    void AdjustSpawnPosition()
    {
        if (!AnchorBottomToSpawn) return;
        if (Renderer == null || Renderer.sprite == null) return;

        Vector3 pos = transform.position;
        float bottomY = Renderer.bounds.min.y;
        float pivotY = pos.y;
        float bottomBelowPivot = pivotY - bottomY;

        // Shift the whole transform up so the bottom edge ends up at the original Y 
        // (plus a small upward nudge to avoid z-fighting with ground tiles).
        pos.y = pivotY + bottomBelowPivot + GroundOffset;
        transform.position = pos;
    }

    void Update()
    {
        float age = Time.time - _timeSpawned;
        if (age >= _duration)
        {
            Destroy(gameObject);
            return;
        }

        // Fade out in the last 30% of life
        if (Renderer != null && age > _duration * 0.7f)
        {
            float fadeT = (age - _duration * 0.7f) / (_duration * 0.3f);
            Color c = Renderer.color;
            c.a = Mathf.Lerp(1f, 0f, fadeT);
            Renderer.color = c;
        }

        // Damage tick
        if (Time.time >= _nextTickTime)
        {
            _nextTickTime = Time.time + DamageTickInterval;
            TickDamage();
        }
    }

    /// <summary>
    /// Queries physics for all enemies overlapping the patch's damage circle and applies 
    /// damage to each. Matches AuraController's pattern - works regardless of how the 
    /// prefab's collider is configured (or if it has one at all).
    /// </summary>
    void TickDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _resolvedDamageRadius);
        int damaged = 0;
        
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (!hit.CompareTag("Enemy")) continue;

            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null) continue;

            enemy.TakeDamage(_damagePerTick);
            damaged++;

            if (ShowDamagePopups && GameUI.Instance != null)
            {
                GameUI.Instance.ShowDamagePopup(enemy.transform.position, _damagePerTick, false);
            }
        }

        if (DebugLog)
        {
            Debug.Log($"[FireTrailPatch] Tick at {transform.position} radius={_resolvedDamageRadius:F2}: " +
                      $"damaged {damaged} enem{(damaged == 1 ? "y" : "ies")} for {_damagePerTick}");
        }
    }

    /// <summary>
    /// Draws the damage radius in the scene view ALWAYS (not just when selected) so you 
    /// can see during playtest whether the damage circle matches the visual flames. The 
    /// orange wireframe lets you tune OverrideDamageRadius confidently.
    /// </summary>
    void OnDrawGizmos()
    {
        // Use _resolvedDamageRadius if it's been set (in play mode), otherwise fall back 
        // to the inspector value so the gizmo shows something useful in the editor too.
        float r = _resolvedDamageRadius > 0f ? _resolvedDamageRadius : DamageRadius;
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, r);
    }
}