using UnityEngine;

/// <summary>
/// 1.4.11 NEW: A small angel companion that hovers directly above the player (BELOW the 
/// regular turret row). Visible only when Second Wind is unlocked AND off cooldown.
/// Disappears when Second Wind is on cooldown.
/// 
/// Per outline (clarification 23): "Lets use the same turret hover mechanic to spawn a 
/// angle [angel] turret that appears if second wind is available but disappears when 
/// its on cooldown. Lets have this guy hover directly above the player but bellow where 
/// the turrets hover so that its not grouped in with them."
/// 
/// Uses TurretBase machinery for hovering but does nothing on tick - it's purely visual.
/// </summary>
public class SecondWindAngel : TurretBase
{
    public override TurretSlotType TurretType => TurretSlotType.AngelGuardian;

    [Header("Angel Visual")]
    [Tooltip("How quickly the angel fades in/out when its visibility changes.")]
    public float FadeSpeed = 5f;

    private SpriteRenderer _renderer;
    private bool _shouldBeVisible = true;
    private float _currentAlpha = 0f;

    protected override void Awake()
    {
        base.Awake();
        _renderer = TurretRenderer != null ? TurretRenderer : GetComponentInChildren<SpriteRenderer>();
        if (_renderer != null)
        {
            Color c = _renderer.color;
            c.a = 0f;
            _renderer.color = c;
        }
    }

    void OnEnable()
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnSecondWindChanged += UpdateVisibility;
        }
        UpdateVisibility();
    }

    void OnDisable()
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnSecondWindChanged -= UpdateVisibility;
        }
    }

    void LateUpdate()
    {
        // Re-check visibility each frame because the cooldown ticks down over time
        // without triggering any event - we need to detect when it becomes ready again.
        UpdateVisibility();

        // Fade alpha toward target
        if (_renderer != null)
        {
            float targetAlpha = _shouldBeVisible ? 1f : 0f;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, FadeSpeed * Time.deltaTime);
            Color c = _renderer.color;
            c.a = _currentAlpha;
            _renderer.color = c;
        }
    }

    void UpdateVisibility()
    {
        if (PlayerStats.Instance == null)
        {
            _shouldBeVisible = false;
            return;
        }

        _shouldBeVisible = PlayerStats.Instance.IsSecondWindReady();
    }

    protected override float GetCurrentInterval()
    {
        // No tick action - purely visual.
        return 0f;
    }

    protected override void OnTick()
    {
        // No-op
    }
}
