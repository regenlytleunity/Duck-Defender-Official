using UnityEngine;
using UnityEngine.InputSystem;

// Identity and device ownership; gameplay remains in the existing player components.
[DefaultExecutionOrder(-100)]
public class LocalPlayer : MonoBehaviour
{
    public int Index { get; private set; }
    public Gamepad Controller { get; private set; }
    public bool UsesGamepad { get; private set; }
    public PlayerStats Stats { get; private set; }
    public PlayerHealth Health { get; private set; }
    public PlayerController Movement { get; private set; }
    public WeaponPlayer Weapon { get; private set; }
    public SpriteRenderer Sprite { get; private set; }
    public Color Color { get; private set; } = Color.white;
    public string DisplayName { get; private set; }
    public bool Alive => Health != null && !Health.IsDead;
    public bool Connected => !UsesGamepad || Controller != null && Controller.added;
    public double DamageDealt;
    public long CoinsCollected;
    public int Kills, Healing, CardsPicked, Deaths;
    public int CoinsForMeteor, CoinsForShot;
    public long PendingMeteors, PendingSecondaryMeteors;
    public Vector2 Aim { get; private set; } = Vector2.right;
    public bool JumpDown { get; private set; }
    public bool JumpHeld => Connected && Controller != null && (ControllerBindings.Get("Jump") == "MoveUp" ? Move.y > .65f : ControllerBindings.Held(Controller, "Jump"));
    bool _stickUp;
    LineRenderer _aimArrow;
    static Material _arrowMaterial;
    Material _paletteMaterial;

    public void Configure(int index, Gamepad controller, bool usesGamepad)
    {
        Index = index;
        Stats = GetComponent<PlayerStats>(); Health = GetComponent<PlayerHealth>();
        Movement = GetComponent<PlayerController>(); Weapon = GetComponent<WeaponPlayer>();
        Sprite = GetComponent<SpriteRenderer>();
        bool multiplayer = LocalCoopSession.RequestedPlayers > 1;
        DisplayName = multiplayer ? LocalCoopSession.PlayerNames[index] : "PLAYER 1";
        Color = multiplayer ? CoopLobbyUI.Colors[LocalCoopSession.PlayerColors[index]] : Color.white;
        if (multiplayer)
        {
            // Replace the yellow body palette, preserving the authored outline and beak.
            _paletteMaterial = new Material(Resources.Load<Shader>("PlayerPalette"));
            _paletteMaterial.SetColor("_PlayerColor", Color);
            Sprite.sharedMaterial = _paletteMaterial;
        }
        AssignController(controller, usesGamepad);
    }

    public void AssignController(Gamepad controller, bool usesGamepad)
    {
        Controller = controller; UsesGamepad = usesGamepad; _stickUp = false; JumpDown = false;
        if (!usesGamepad || _aimArrow != null) return;
        if (_arrowMaterial == null) _arrowMaterial = new Material(Shader.Find("Sprites/Default"));
        _aimArrow = new GameObject("Aim direction").AddComponent<LineRenderer>();
        _aimArrow.transform.SetParent(transform, false);
        _aimArrow.sharedMaterial = _arrowMaterial; _aimArrow.positionCount = 5;
        _aimArrow.startWidth = _aimArrow.endWidth = .06f; _aimArrow.sortingOrder = 110;
        _aimArrow.startColor = _aimArrow.endColor = Color;
    }

    void Update()
    {
        JumpDown = false;
        if (!UsesGamepad || !Connected) return;
        float up = Move.y;
        JumpDown = ControllerBindings.Get("Jump") == "MoveUp" ? up > .7f && !_stickUp : ControllerBindings.Pressed(Controller, "Jump");
        if (up < .4f) _stickUp = false;
        else if (up > .7f) _stickUp = true;
        Vector2 stick = ControllerBindings.Aim(Controller);
        if (stick.sqrMagnitude > .04f)
        {
            Aim = stick.normalized;
            // A narrow horizontal snap matches the touch controls, without target lock-on.
            if (Mathf.Abs(Aim.y) < .12f) Aim = new Vector2(Mathf.Sign(Aim.x), 0);
        }
    }

    void LateUpdate()
    {
        if (_aimArrow == null) return;
        _aimArrow.enabled = Alive && UsesGamepad && Connected;
        Vector3 start = transform.position + Vector3.up * .15f;
        Vector3 tip = start + (Vector3)Aim * 1.5f;
        Vector3 side = new Vector3(-Aim.y, Aim.x) * .18f;
        _aimArrow.SetPosition(0, start + (Vector3)Aim * .85f);
        _aimArrow.SetPosition(1, tip); _aimArrow.SetPosition(2, tip - (Vector3)Aim * .3f + side);
        _aimArrow.SetPosition(3, tip); _aimArrow.SetPosition(4, tip - (Vector3)Aim * .3f - side);
    }

    public Vector2 Move => !Connected || Controller == null ? Vector2.zero :
        ControllerBindings.Move(Controller);
    public Vector3 AimScreenPosition()
    {
        var camera = Camera.main;
        var origin = Weapon != null && Weapon.FirePoint != null ? Weapon.FirePoint.position : transform.position;
        return camera != null ? camera.WorldToScreenPoint(origin + (Vector3)Aim * 10) : Vector3.zero;
    }
    void OnDestroy() { if (_paletteMaterial != null) Destroy(_paletteMaterial); }
}
