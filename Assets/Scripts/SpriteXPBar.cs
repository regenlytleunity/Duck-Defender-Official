using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sprite-based XP bar using two layered Images.
/// Same approach as SpriteHealthBar.
///
/// VISUAL LAYERING (bottom to top):
///   1. Background sprite — always full width, represents remaining XP needed
///   2. Overlay sprite — uses fillAmount to grow as XP is gained
///
/// SETUP:
/// 1. Create an empty GameObject "XPBar" under your HUD Canvas
/// 2. Add an Image child named "Background" — assign your background sprite
///    - Set Image Type to "Simple"
/// 3. Add an Image child named "Overlay" on top — assign your fill sprite
///    - Set Image Type to "Filled"
///    - Fill Method = Horizontal
///    - Fill Origin = Left
/// 4. Both Images should have the same RectTransform size and position
/// 5. Attach this script to the "XPBar" parent
/// 6. Drag Background and Overlay into the Inspector fields
/// </summary>
public class SpriteXPBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The background sprite (full width, sits behind the fill)")]
    public Image Background;
    
    [Tooltip("The fill sprite (grows based on XP progress)")]
    public Image Overlay;

    private float _targetXP = 1f;

    public void SetMaxXP(float targetXP)
    {
        _targetXP = Mathf.Max(targetXP, 1f);
        if (Overlay != null)
        {
            Overlay.fillAmount = 0f;
        }
    }

    public void SetXP(float currentXP)
    {
        if (Overlay != null)
        {
            Overlay.fillAmount = Mathf.Clamp01(currentXP / _targetXP);
        }
    }

    public void UpdateXP(float currentXP, float targetXP)
    {
        _targetXP = Mathf.Max(targetXP, 1f);
        if (Overlay != null)
        {
            Overlay.fillAmount = Mathf.Clamp01(currentXP / _targetXP);
        }
    }
}
