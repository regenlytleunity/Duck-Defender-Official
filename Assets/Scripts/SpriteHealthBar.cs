using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sprite-based health bar using two layered Images.
/// Replaces Unity's default Slider for a cleaner look.
///
/// VISUAL LAYERING (bottom to top):
///   1. Background (red sprite) — always full width, represents lost HP
///   2. Overlay (green sprite) — uses fillAmount to shrink as HP drops
///
/// SETUP:
/// 1. Create an empty GameObject "HealthBar" under your HUD Canvas
/// 2. Add an Image child named "Background" — assign the red sprite
///    - Set Image Type to "Simple" (it's always full width)
/// 3. Add an Image child named "Overlay" on top — assign the green sprite
///    - Set Image Type to "Filled"
///    - Fill Method = Horizontal
///    - Fill Origin = Left
/// 4. Both Images should have the same RectTransform size and position
/// 5. Attach this script to the "HealthBar" parent
/// 6. Drag Background and Overlay into the Inspector fields
/// 7. In GameUI, replace the Slider reference with this component
/// </summary>
public class SpriteHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The red sprite (full width, sits behind the green)")]
    public Image Background;
    
    [Tooltip("The green sprite (fills/shrinks based on HP)")]
    public Image Overlay;

    private float _maxHealth = 1f;

    /// <summary>
    /// Call once when max health is set or changes.
    /// </summary>
    public void SetMaxHealth(float maxHealth)
    {
        _maxHealth = Mathf.Max(maxHealth, 1f);
        if (Overlay != null)
        {
            Overlay.fillAmount = 1f;
        }
    }

    /// <summary>
    /// Call whenever current health changes.
    /// </summary>
    public void SetHealth(float currentHealth)
    {
        if (Overlay != null)
        {
            Overlay.fillAmount = Mathf.Clamp01(currentHealth / _maxHealth);
        }
    }

    /// <summary>
    /// Convenience: set both at once (matches the old Slider pattern).
    /// </summary>
    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        _maxHealth = Mathf.Max(maxHealth, 1f);
        if (Overlay != null)
        {
            Overlay.fillAmount = Mathf.Clamp01(currentHealth / _maxHealth);
        }
    }
}
