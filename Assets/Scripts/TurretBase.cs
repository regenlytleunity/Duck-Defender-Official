using UnityEngine;

/// <summary>
/// 1.4.11: Added Elemental slot type and AngelGuardian (Second Wind visual).
/// AngelGuardian is positioned separately (above the player but below the main turret row)
/// and is handled by TurretManager / SecondWindAngel.
/// </summary>
public abstract class TurretBase : MonoBehaviour
{
    public enum TurretSlotType { Marksman, Medic, Protector, Elemental, AngelGuardian }

    public abstract TurretSlotType TurretType { get; }

    [Header("Flight Feel")]
    public float HoverHeight = 2.5f;
    public float FollowSmoothing = 0.15f;
    public float BobAmplitude = 0.15f;
    public float BobFrequency = 1.2f;
    public float BobPhaseOffset = 0f;
    public float MaxBankAngle = 15f;
    public float BankSmoothing = 5f;

    [Header("Visual")]
    public SpriteRenderer TurretRenderer;

    protected Transform PlayerTransform { get; private set; }
    protected Vector3 SlotOffset { get; private set; }

    private float _tickTimer = 0f;
    private Vector3 _smoothVelocity;
    private float _currentBankAngle;
    private Vector3 _lastPlayerPos;

    protected virtual void Awake()
    {
        if (TurretRenderer == null) TurretRenderer = GetComponentInChildren<SpriteRenderer>();

        if (BobPhaseOffset == 0f)
        {
            BobPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    public void Configure(Transform player, Vector3 slotOffset)
    {
        PlayerTransform = player;
        SlotOffset = slotOffset;
        _lastPlayerPos = player != null ? player.position : Vector3.zero;

        if (player != null)
        {
            transform.position = player.position + slotOffset + Vector3.up * HoverHeight;
        }
    }

    public void UpdateSlotOffset(Vector3 newOffset)
    {
        SlotOffset = newOffset;
    }

    void Update()
    {
        if (PlayerTransform == null) return;

        FollowPlayer();
        TickAction();
    }

    void FollowPlayer()
    {
        Vector3 targetPos = PlayerTransform.position + SlotOffset + Vector3.up * HoverHeight;

        float bob = Mathf.Sin((Time.time * BobFrequency * Mathf.PI * 2f) + BobPhaseOffset) * BobAmplitude;
        targetPos.y += bob;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref _smoothVelocity,
            FollowSmoothing
        );

        float playerDeltaX = PlayerTransform.position.x - _lastPlayerPos.x;
        float targetBank = -Mathf.Clamp(playerDeltaX * 40f, -1f, 1f) * MaxBankAngle;
        _currentBankAngle = Mathf.Lerp(_currentBankAngle, targetBank, BankSmoothing * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, _currentBankAngle);

        _lastPlayerPos = PlayerTransform.position;
    }

    void TickAction()
    {
        float interval = GetCurrentInterval() / (PlayerStats.Instance != null ? PlayerStats.Instance.BeneficialStatMultiplier : 1f);
        if (interval <= 0f) return;

        _tickTimer += Time.deltaTime;
        if (_tickTimer >= interval)
        {
            _tickTimer = 0f;
            OnTick();
        }
    }

    protected abstract float GetCurrentInterval();
    protected abstract void OnTick();
}
