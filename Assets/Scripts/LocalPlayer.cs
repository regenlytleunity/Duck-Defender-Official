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
    SpriteRenderer _aimArrow;
    static Sprite _arrowSprite;
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
        _aimArrow = new GameObject("Aim direction").AddComponent<SpriteRenderer>();
        _aimArrow.transform.SetParent(transform, false);
        _aimArrow.sprite = AimArrowSprite(); _aimArrow.sortingOrder = 110;
        _aimArrow.color = Color;
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
        _aimArrow.enabled = Alive && UsesGamepad && Connected && TutorialDirector.AllowAim;
        Vector3 start = transform.position + Vector3.up * .15f;
        // Cancel the duck's facing/size transform, keeping one crisp closed silhouette.
        var scale = transform.lossyScale;
        _aimArrow.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1);
        _aimArrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Aim.y, Aim.x) * Mathf.Rad2Deg);
        _aimArrow.transform.position = start + (Vector3)Aim * 1.15f;
    }

    static Sprite AimArrowSprite()
    {
        if (_arrowSprite != null) return _arrowSprite;
        const int width = 256, height = 176;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Outlined aim arrow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[width * height];
        var points = new[] { new Vector2(12, 56), new Vector2(146, 56), new Vector2(146, 10),
            new Vector2(244, 88), new Vector2(146, 166), new Vector2(146, 120), new Vector2(12, 120) };
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            Vector2 p = new Vector2(x + .5f, y + .5f);
            bool inside = false; float distance = float.PositiveInfinity;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[j], b = points[i], edge = b - a;
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                distance = Mathf.Min(distance, Vector2.Distance(p, a + edge * Mathf.Clamp01(Vector2.Dot(p - a, edge) / edge.sqrMagnitude)));
            }
            // Distance to the closed contour gives the whole outline the same thickness.
            var color = UnityEngine.Color.Lerp(UnityEngine.Color.black, UnityEngine.Color.white, Mathf.Clamp01(distance - 9));
            color.a = inside ? Mathf.Clamp01(distance) : 0; pixels[y * width + x] = color;
        }
        texture.SetPixels(pixels); texture.Apply(false, true);
        _arrowSprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, 300);
        return _arrowSprite;
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
