using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Each seat reads only the controller assigned when the host opens customization.
public class CoopLobbyUI : MonoBehaviour
{
    public static CoopLobbyUI Instance { get; private set; }
    public static readonly Color[] Colors = { new Color(1, .18f, .2f), new Color(1, .5f, .12f), new Color(1, .88f, .08f), new Color(.25f, 1, .4f), new Color(.2f, .55f, 1), new Color(.7f, .3f, 1) };
    static readonly string[] ColorNames = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple" };
    const string Keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public bool IsOpen => _root != null && _root.gameObject.activeSelf;
    RectTransform _root;
    HostMenuUI _host;
    readonly RectTransform[] _keyboards = new RectTransform[4];
    readonly Button[][] _controls = new Button[4][], _keys = new Button[4][];
    readonly TMP_Text[] _names = new TMP_Text[4], _colors = new TMP_Text[4], _status = new TMP_Text[4];
    readonly Image[] _previews = new Image[4];
    readonly Material[] _previewMaterials = new Material[Colors.Length];
    readonly bool[] _ready = new bool[4];
    readonly int[] _focus = new int[4], _key = new int[4];
    readonly float[] _repeat = new float[4];
    readonly ControllerSelectionFrame[] _frames = new ControllerSelectionFrame[4];
    float _inputAfter, _launchAt;
    bool _navigationEvents;

    public void Open(HostMenuUI host)
    {
        Instance = this; _host = host; _launchAt = 0; _inputAfter = Time.unscaledTime + .35f;
        if (_root != null) Destroy(_root.gameObject);
        _root = CoopUIElements.Panel(transform, "Player customization", Vector2.zero, Vector2.one, new Color(.025f, .04f, .07f));
        if (EventSystem.current != null) { _navigationEvents = EventSystem.current.sendNavigationEvents; EventSystem.current.sendNavigationEvents = false; EventSystem.current.SetSelectedGameObject(null); }
        int count = LocalCoopSession.RequestedPlayers;
        for (int i = 0; i < count; i++)
        {
            int seat = i;
            bool keyboard = LocalCoopSession.KeyboardTest && count == 2 && i == 0;
            int device = LocalCoopSession.KeyboardTest && count == 2 ? i - 1 : i;
            LocalCoopSession.PlayerControllers[i] = keyboard ? null : Gamepad.all[device];
            _ready[i] = false; _focus[i] = _key[i] = 0; _repeat[i] = 0;
            var panel = CoopRunUI.PlayerPanel(_root, i, count);
            CoopUIElements.Text(panel, "PLAYER " + (i + 1) + " - CUSTOMIZE", new Vector2(.03f, .9f), new Vector2(.97f, .99f), 32);
            _previews[i] = CoopUIElements.Panel(panel, "Duck color preview", new Vector2(.35f, .68f), new Vector2(.65f, .89f), Color.white).GetComponent<Image>();
            _previews[i].sprite = host.DuckPreviewSprite;
            _previews[i].preserveAspect = true;
            _previews[i].raycastTarget = false;
            _controls[i] = new Button[3];
            _controls[i][0] = Control(panel, seat, "", new Vector2(.15f, .55f), new Vector2(.85f, .66f), () => Activate(seat));
            _colors[i] = _controls[i][0].GetComponentInChildren<TMP_Text>();
            _controls[i][1] = Control(panel, seat, "", new Vector2(.15f, .39f), new Vector2(.85f, .50f), () => Activate(seat));
            _names[i] = _controls[i][1].GetComponentInChildren<TMP_Text>();
            _controls[i][2] = Control(panel, seat, "Ready", new Vector2(.25f, .23f), new Vector2(.75f, .34f), () => Activate(seat));
            _status[i] = CoopUIElements.Text(panel, "", new Vector2(.04f, .025f), new Vector2(.96f, .17f), 24);
            BuildKeyboard(panel, seat);
            _frames[i]?.Dispose(); _frames[i] = new ControllerSelectionFrame("Player " + (i + 1) + " customization focus");
            Refresh(i);
        }
    }

    Button Control(Transform parent, int seat, string text, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        Button button = null;
        button = CoopUIElements.Button(parent, text, min, max, () =>
        {
            if (!IsOpen || Time.unscaledTime < _inputAfter || LocalCoopSession.PlayerControllers[seat] != null) return;
            if (_keyboards[seat] != null && _keyboards[seat].gameObject.activeSelf) _key[seat] = System.Array.IndexOf(_keys[seat], button);
            else _focus[seat] = System.Array.IndexOf(_controls[seat], button);
            action();
        });
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        return button;
    }

    void BuildKeyboard(Transform panel, int seat)
    {
        var keyboard = CoopUIElements.Panel(panel, "Name keyboard", new Vector2(.035f, .18f), new Vector2(.965f, .88f), new Color(.06f, .09f, .14f));
        _keyboards[seat] = keyboard; _keys[seat] = new Button[40];
        CoopUIElements.Text(keyboard, "NAME - MAX 10 CHARACTERS", new Vector2(.03f, .87f), new Vector2(.97f, .99f), 25);
        for (int k = 0; k < 40; k++)
        {
            int key = k, row = k / 8, col = k % 8;
            string label = k < Keys.Length ? Keys[k].ToString() : k == 36 ? "Space" : k == 37 ? "Delete" : k == 38 ? "Clear" : "Done";
            _keys[seat][k] = Control(keyboard, seat, label, new Vector2(.02f + col * .12f, .68f - row * .16f), new Vector2(.13f + col * .12f, .82f - row * .16f), () => TypeKey(seat, key));
            var text = _keys[seat][k].GetComponentInChildren<TMP_Text>();
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSizeMin = 10; if (k >= 36) text.fontSize = text.fontSizeMax = 18;
        }
        keyboard.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!IsOpen) return;
        bool allReady = true;
        for (int i = 0; i < LocalCoopSession.RequestedPlayers; i++)
        {
            var pad = LocalCoopSession.PlayerControllers[i];
            bool keyboard = LocalCoopSession.KeyboardTest && LocalCoopSession.RequestedPlayers == 2 && i == 0;
            if (!keyboard && (pad == null || !pad.added))
            { _ready[i] = false; _status[i].text = "Reconnect your controller"; allReady = false; _frames[i].Hide(); continue; }
            if (Time.unscaledTime >= _inputAfter)
            {
                Vector2 move = keyboard ? KeyboardMove() : ControllerBindings.MenuMove(pad);
                if (move.sqrMagnitude < .2f) _repeat[i] = 0;
                else if (Time.unscaledTime >= _repeat[i] && !_ready[i])
                {
                    _repeat[i] = Time.unscaledTime + .22f;
                    int step = Mathf.Abs(move.x) > Mathf.Abs(move.y) ? (move.x > 0 ? 1 : -1) : (move.y > 0 ? -1 : 1);
                    if (_keyboards[i].gameObject.activeSelf)
                    { if (Mathf.Abs(move.y) >= Mathf.Abs(move.x)) step *= 8; _key[i] = (_key[i] + step + 40) % 40; }
                    else if (Mathf.Abs(move.x) > Mathf.Abs(move.y) && _focus[i] == 0) CycleColor(i, step);
                    else _focus[i] = (_focus[i] + step + 3) % 3;
                }
                bool confirm = keyboard ? Keyboard.current?.enterKey.wasPressedThisFrame == true : ControllerBindings.Pressed(pad, "Confirm");
                bool back = keyboard ? Keyboard.current?.escapeKey.wasPressedThisFrame == true : ControllerBindings.Pressed(pad, "Back");
                if (confirm) { if (_keyboards[i].gameObject.activeSelf) TypeKey(i, _key[i]); else Activate(i); }
                if (back)
                {
                    if (_keyboards[i].gameObject.activeSelf) FinishName(i);
                    else if (_ready[i]) _ready[i] = false;
                    else if (i == 0) { Close(); _host.Open(); return; }
                }
            }
            Refresh(i); allReady &= _ready[i];
            _frames[i].Show((RectTransform)(_keyboards[i].gameObject.activeSelf ? _keys[i][_key[i]] : _controls[i][_ready[i] ? 2 : _focus[i]]).transform, _ready[i] ? Color.green : (Color?)null);
        }
        if (!allReady) _launchAt = 0;
        else if (_launchAt == 0) _launchAt = Time.unscaledTime + 1.25f;
        else if (Time.unscaledTime >= _launchAt)
        {
            Close(); Time.timeScale = 1;
            UnityEngine.SceneManagement.SceneManager.LoadScene(FindFirstObjectByType<MainMenuController>().GameSceneName);
        }
    }
    static Vector2 KeyboardMove()
    {
        var k = Keyboard.current; if (k == null) return Vector2.zero;
        return new Vector2((k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0), (k.upArrowKey.isPressed ? 1 : 0) - (k.downArrowKey.isPressed ? 1 : 0));
    }
    void Activate(int seat)
    {
        if (_ready[seat]) { _ready[seat] = false; return; }
        if (_focus[seat] == 0) CycleColor(seat, 1);
        else if (_focus[seat] == 1)
        {
            LocalCoopSession.PlayerNames[seat] = "";
            _key[seat] = 0;
            _keyboards[seat].gameObject.SetActive(true);
            Refresh(seat);
        }
        else { FinishName(seat); _ready[seat] = true; }
    }
    void CycleColor(int seat, int step) { LocalCoopSession.PlayerColors[seat] = (LocalCoopSession.PlayerColors[seat] + step + Colors.Length) % Colors.Length; }
    void TypeKey(int seat, int key)
    {
        string name = LocalCoopSession.PlayerNames[seat];
        if (key == 39) { FinishName(seat); return; }
        if (key == 38) name = "";
        else if (key == 37) { if (name.Length > 0) name = name.Substring(0, name.Length - 1); }
        else if (name.Length < 10) name += key == 36 ? " " : Keys[key].ToString();
        LocalCoopSession.PlayerNames[seat] = name; Refresh(seat);
    }
    void FinishName(int seat)
    {
        string name = LocalCoopSession.PlayerNames[seat].Trim();
        LocalCoopSession.PlayerNames[seat] = name.Length == 0 ? "PLAYER " + (seat + 1) : name;
        _keyboards[seat].gameObject.SetActive(false);
    }
    void Refresh(int seat)
    {
        _names[seat].text = "Name: " + LocalCoopSession.PlayerNames[seat];
        _colors[seat].text = "Color: " + ColorNames[LocalCoopSession.PlayerColors[seat]] + "   < >";
        int color = LocalCoopSession.PlayerColors[seat];
        if (_previewMaterials[color] == null)
        {
            _previewMaterials[color] = new Material(Resources.Load<Shader>("DuckPreviewPalette"));
            _previewMaterials[color].SetColor("_PlayerColor", Colors[color]);
        }
        _previews[seat].material = _previewMaterials[color];
        _controls[seat][2].GetComponentInChildren<TMP_Text>().text = _ready[seat] ? "READY" : "Ready";
        bool keyboard = LocalCoopSession.PlayerControllers[seat] == null;
        string confirm = keyboard ? "Enter" : ControllerBindings.Label("Confirm");
        _status[seat].text = _ready[seat] ? "Ready - " + confirm + " to undo\nStarts when everyone is ready" : _keyboards[seat].gameObject.activeSelf ? LocalCoopSession.PlayerNames[seat] + "  (" + LocalCoopSession.PlayerNames[seat].Length + "/10)\n" + confirm + " to type" : "Arrows / stick: browse\n" + confirm + ": select";
    }
    public void Close()
    {
        if (_root != null) _root.gameObject.SetActive(false);
        foreach (var frame in _frames) frame?.Hide();
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = _navigationEvents;
    }
    void OnDestroy()
    {
        if (IsOpen) Close();
        foreach (var frame in _frames) frame?.Dispose();
        foreach (var material in _previewMaterials) if (material != null) Destroy(material);
        if (Instance == this) Instance = null;
    }
}
