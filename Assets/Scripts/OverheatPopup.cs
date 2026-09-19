using UnityEngine;
using TMPro;

/// <summary>
/// 1.4.11 NEW: "Overheat!" text popup that briefly appears above the player when 
/// the Mini Gun is forced to cool down. Auto-destroys after a short duration.
/// 
/// SETUP (two options):
/// 
/// Option A - World Space (recommended for game-world text):
///   1. Create a 3D Text Object: GameObject > 3D Object > Text - TextMeshPro
///   2. Set its text to "Overheat!" (will be overwritten)
///   3. Attach this script to the SAME GameObject
///   4. Drag the TextMeshPro into LabelWorld field
///   5. Save as prefab, drag into WeaponPlayer.OverheatPopupPrefab
/// 
/// Option B - UI Canvas (if you want it on the HUD):
///   1. Create a UI Canvas set to World Space, child to the prefab root
///   2. Add a TextMeshProUGUI inside it
///   3. Attach this script to the prefab ROOT
///   4. Drag the TextMeshProUGUI into LabelUI field
///   5. Save as prefab, drag into WeaponPlayer.OverheatPopupPrefab
/// 
/// You only need to fill ONE of LabelWorld or LabelUI. The script uses whichever is assigned.
/// If neither is assigned, the script auto-finds a TMP component in children.
/// </summary>
public class OverheatPopup : MonoBehaviour
{
    [Header("Text - Use ONE of these")]
    [Tooltip("For world-space 3D Text (TextMeshPro component). Most common for in-world popups.")]
    public TextMeshPro LabelWorld;
    
    [Tooltip("For UI canvas text (TextMeshProUGUI component). Only fill if your popup uses a canvas.")]
    public TextMeshProUGUI LabelUI;
    
    [Header("Display")]
    public string Message = "Overheat!";

    [Header("Motion")]
    public float UpwardSpeed = 1.5f;
    public float Lifetime = 1.2f;

    [Header("Visual")]
    public Color StartColor = new Color(1f, 0.4f, 0.2f);

    private float _spawnTime;

    void Awake()
    {
        // Auto-find if neither label was assigned in inspector
        if (LabelWorld == null && LabelUI == null)
        {
            LabelWorld = GetComponentInChildren<TextMeshPro>();
            if (LabelWorld == null)
            {
                LabelUI = GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        SetText(Message);
        SetColor(StartColor);
    }

    void Start()
    {
        _spawnTime = Time.time;
    }

    void Update()
    {
        transform.position += Vector3.up * UpwardSpeed * Time.deltaTime;

        float age = Time.time - _spawnTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Fade out in the last 40% of life
        if (age > Lifetime * 0.6f)
        {
            float fadeT = (age - Lifetime * 0.6f) / (Lifetime * 0.4f);
            float alpha = Mathf.Lerp(1f, 0f, fadeT);
            SetAlpha(alpha);
        }
    }

    void SetText(string text)
    {
        if (LabelWorld != null) LabelWorld.text = text;
        if (LabelUI != null) LabelUI.text = text;
    }

    void SetColor(Color c)
    {
        if (LabelWorld != null) LabelWorld.color = c;
        if (LabelUI != null) LabelUI.color = c;
    }

    void SetAlpha(float a)
    {
        if (LabelWorld != null)
        {
            Color c = LabelWorld.color;
            c.a = a;
            LabelWorld.color = c;
        }
        if (LabelUI != null)
        {
            Color c = LabelUI.color;
            c.a = a;
            LabelUI.color = c;
        }
    }
}