using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// A second tab within the existing keybind screen; SettingsMenuUI owns its lifetime.
public sealed class ControllerBindingsUI
{
    readonly SettingsMenuUI _settings;
    readonly GameObject _keyboard, _controller;
    readonly TMP_Text[] _labels = new TMP_Text[ControllerBindings.Actions.Length];
    readonly TMP_Text _sticks;
    readonly ControllerBindingDiagram _diagram;
    int _capture = -1;
    float _captureAfter, _captureExpires, _suppressUntil;
    public bool Capturing => _capture >= 0;
    public bool SuppressInput => Capturing || Time.unscaledTime < _suppressUntil;

    public ControllerBindingsUI(SettingsMenuUI settings)
    {
        _settings = settings;
        var panel = settings.KeybindPanel.transform;
        var children = new List<Transform>();
        foreach (Transform child in panel)
            if (child.name.StartsWith("Action") || child.name.StartsWith("Bind") || child.name == "RestoreDefaults") children.Add(child);
        _keyboard = CoopUIElements.Panel(panel, "Keyboard bindings", Vector2.zero, Vector2.one, Color.clear).gameObject;
        _keyboard.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        foreach (var child in children) child.SetParent(_keyboard.transform, true);
        // Retain the authored controls; fit item and tutorial bindings in both columns.
        System.Array.Resize(ref settings.KeybindLabels, SettingsMenuUI.BindingActions.Length);
        for (int i = 0; i < SettingsMenuUI.BindingActions.Length; i++)
        {
            int action = i, column = i / 8, row = i % 8;
            float left = .04f + column * .48f, bottom = .735f - row * .078f;
            var label = _keyboard.transform.Find("Action" + i) as RectTransform;
            if (label == null) label = CoopUIElements.Text(_keyboard.transform, SettingsMenuUI.BindingNames[i], Vector2.zero, Vector2.one, 25).rectTransform;
            label.name = "Action" + i;
            CoopUIElements.Stretch(label, new Vector2(left, bottom), new Vector2(left + .27f, bottom + .065f));
            var binding = _keyboard.transform.Find("Bind" + i) as RectTransform;
            if (binding == null)
                binding = (RectTransform)CoopUIElements.Button(_keyboard.transform, "", Vector2.zero, Vector2.one, () => settings.BeginBinding(action)).transform;
            binding.name = "Bind" + i;
            CoopUIElements.Stretch(binding, new Vector2(left + .275f, bottom), new Vector2(left + .445f, bottom + .065f));
            settings.KeybindLabels[i] = binding.GetComponentInChildren<TextMeshProUGUI>();
            settings.KeybindLabels[i].fontSizeMin = 16; settings.KeybindLabels[i].fontSizeMax = 26;
        }
        var restore = _keyboard.transform.Find("RestoreDefaults") as RectTransform;
        if (restore != null) CoopUIElements.Stretch(restore, new Vector2(.60f, .125f), new Vector2(.94f, .175f));
        _controller = CoopUIElements.Panel(panel, "Controller bindings", new Vector2(.025f, .16f), new Vector2(.975f, .81f), new Color(.035f, .055f, .085f)).gameObject;
        var drawing = new GameObject("Controller diagram", typeof(RectTransform), typeof(CanvasRenderer), typeof(ControllerBindingDiagram));
        drawing.transform.SetParent(_controller.transform, false);
        CoopUIElements.Stretch((RectTransform)drawing.transform, Vector2.zero, Vector2.one);
        _diagram = drawing.GetComponent<ControllerBindingDiagram>(); _diagram.raycastTarget = false;
        var sticks = CoopUIElements.Button(_controller.transform, "", new Vector2(.20f, .905f), new Vector2(.80f, .99f), () => { ControllerBindings.SwapSticks(); Refresh(); });
        _sticks = sticks.GetComponentInChildren<TMP_Text>();
        _sticks.fontSizeMax = 24;
        for (int i = 0; i < _labels.Length; i++)
        {
            int action = i;
            Vector2 anchor = ControllerBindingDiagram.Callout(i);
            float x = anchor.x < .5f ? .012f : .745f;
            var button = CoopUIElements.Button(_controller.transform, "", new Vector2(x, anchor.y - .048f), new Vector2(x + .243f, anchor.y + .048f), () => Begin(action));
            button.name = ControllerBindings.Actions[i] + " binding";
            _labels[i] = button.GetComponentInChildren<TMP_Text>();
            _labels[i].fontSizeMax = 25; _labels[i].fontSizeMin = 17;
        }
        foreach (string control in new[] { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger", "start", "select" })
        {
            Vector2 point = ControllerBindingDiagram.ControlPoint(control);
            string name = control == "select" ? "VIEW" : control == "start" ? "START" : ControllerBindings.ControlLabel(control).Split(' ')[0];
            var label = CoopUIElements.Text(_controller.transform, name, point - new Vector2(.018f, .021f), point + new Vector2(.018f, .021f), 23);
            if (control == "start" || control == "select") { label.fontSizeMin = 9; label.fontSizeMax = 13; label.textWrappingMode = TextWrappingModes.NoWrap; }
        }
        CoopUIElements.Text(_controller.transform, "L", new Vector2(.41f, .30f), new Vector2(.45f, .35f), 24);
        CoopUIElements.Text(_controller.transform, "R", new Vector2(.55f, .30f), new Vector2(.59f, .35f), 24);
        CoopUIElements.Button(_controller.transform, "Restore controller defaults", new Vector2(.29f, .018f), new Vector2(.71f, .10f), () => { ControllerBindings.Reset(); Refresh(); });
        CoopUIElements.Button(panel, "Keyboard", new Vector2(.30f, .82f), new Vector2(.49f, .875f), () => Show(false));
        CoopUIElements.Button(panel, "Controller", new Vector2(.51f, .82f), new Vector2(.70f, .875f), () => Show(true));
        settings.KeyCapturePanel.transform.SetAsLastSibling();
        Show(false);
    }
    public void Show(bool controller)
    {
        Cancel();
        _keyboard.SetActive(!controller); _controller.SetActive(controller); Refresh();
        _settings.KeybindStatus.text = controller ? "Mappings apply to every controller. Menu movement uses D-pad / left stick." : "Select a control to change it. Escape cancels binding.";
        ControllerMenuNavigation.ConfigureNavigation(_settings.KeybindPanel);
        MainMenuUI.FocusFirstControl(controller ? _controller : _keyboard);
    }
    public void Begin(int index)
    {
        if (ControllerBindings.MenuPad == null) { _settings.KeybindStatus.text = "Connect a controller first."; return; }
        _capture = index; _captureAfter = Time.unscaledTime + .35f; _captureExpires = Time.unscaledTime + 15;
        _settings.KeyCapturePanel.SetActive(true); _settings.KeyCapturePanel.transform.SetAsLastSibling();
        _settings.KeyCaptureText.text = "Bind " + ControllerBindings.ActionNames[index] + "\nPress a controller button" +
            (ControllerBindings.Actions[index] == "Jump" ? " or push the move stick up" : "") + "\nEscape cancels (15 second timeout)";
        CoopUIElements.WhiteInfill(_settings.KeyCaptureText);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        _settings.RefreshModalInput();
    }
    public void Update()
    {
        if (_controller.activeInHierarchy)
        {
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            int focused = -1;
            for (int i = 0; i < _labels.Length; i++) if (_labels[i].transform.parent.gameObject == selected) focused = i;
            _diagram.Focus(focused);
        }
        if (!Capturing || Time.unscaledTime < _captureAfter) return;
        var pad = ControllerBindings.MenuPad;
        if (pad == null || Input.GetKeyDown(KeyCode.Escape) || ControllerBindings.Pressed(pad, "Back") || Time.unscaledTime > _captureExpires) { Cancel(); return; }
        if (ControllerBindings.Actions[_capture] == "Jump" && ControllerBindings.Move(pad).y > .7f)
        { Assign("MoveUp"); return; }
        foreach (string control in ControllerBindings.Controls)
        {
            if (pad.TryGetChildControl<ButtonControl>(control)?.wasPressedThisFrame != true) continue;
            Assign(control);
            break;
        }
    }
    void Assign(string control)
    {
        string action = ControllerBindings.Actions[_capture];
        bool saved = ControllerBindings.TrySet(action, control, out string error);
        Cancel(); Refresh();
        _settings.KeybindStatus.text = saved ? action + " saved." : error;
    }
    public void Cancel()
    {
        if (!Capturing) return;
        _capture = -1; _suppressUntil = Time.unscaledTime + .3f;
        _settings.KeyCapturePanel.SetActive(false); _settings.RefreshModalInput();
        ControllerMenuNavigation.ConfigureNavigation(_settings.KeybindPanel);
        MainMenuUI.FocusFirstControl(_controller);
    }
    void Refresh()
    {
        _sticks.text = (ControllerBindings.RightMoveStick ? "Move: RIGHT stick | Aim: LEFT stick" : "Move: LEFT stick | Aim: RIGHT stick") + "  (swap)";
        for (int i = 0; i < _labels.Length; i++) _labels[i].text = ControllerBindings.ActionNames[i] + "\n" + ControllerBindings.Label(ControllerBindings.Actions[i]);
        _diagram.RefreshBindings();
    }
}

// A symmetric, dual-stick Logitech-style controller and live binding callouts.
// Runtime uGUI geometry keeps the diagram sharp and avoids serialized art/wiring.
public sealed class ControllerBindingDiagram : UnityEngine.UI.MaskableGraphic
{
    static readonly int[] LeftActions = { 1, 6, 9, 7, 8, 0 };
    static readonly int[] RightActions = { 2, 5, 10, 4, 3, 11 };
    readonly Vector2[] _targets = new Vector2[ControllerBindings.Actions.Length];
    int _focused = -1;
    public static Vector2 Callout(int action)
    {
        int row = System.Array.IndexOf(LeftActions, action);
        return row >= 0 ? new Vector2(.255f, .81f - row * .128f) :
            new Vector2(.745f, .81f - System.Array.IndexOf(RightActions, action) * .128f);
    }
    public static Vector2 ControlPoint(string control)
    {
        switch (control)
        {
            case "buttonNorth": return new Vector2(.618f, .62f);
            case "buttonEast": return new Vector2(.651f, .54f);
            case "buttonSouth": return new Vector2(.618f, .46f);
            case "buttonWest": return new Vector2(.585f, .54f);
            case "leftShoulder": return new Vector2(.38f, .75f);
            case "rightShoulder": return new Vector2(.62f, .75f);
            case "leftTrigger": return new Vector2(.38f, .835f);
            case "rightTrigger": return new Vector2(.62f, .835f);
            case "select": return new Vector2(.46f, .62f);
            case "start": return new Vector2(.54f, .62f);
            case "leftStickPress": return new Vector2(.43f, .325f);
            case "rightStickPress": return new Vector2(.57f, .325f);
            case "MoveUp": return new Vector2(ControllerBindings.RightMoveStick ? .57f : .43f, .355f);
            case "dpad/up": return new Vector2(.382f, .615f);
            case "dpad/down": return new Vector2(.382f, .465f);
            case "dpad/left": return new Vector2(.351f, .54f);
            case "dpad/right": return new Vector2(.413f, .54f);
            default: return new Vector2(.5f, .51f);
        }
    }
    public void RefreshBindings()
    {
        for (int i = 0; i < _targets.Length; i++) _targets[i] = ControlPoint(ControllerBindings.Get(ControllerBindings.Actions[i]));
        SetVerticesDirty();
    }
    public void Focus(int action) { if (_focused != action) { _focused = action; SetVerticesDirty(); } }
    Vector2 Point(Vector2 point) => rectTransform.rect.min + Vector2.Scale(point, rectTransform.rect.size);
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var edge = new Color(.48f, .55f, .65f); var dark = new Color(.045f, .06f, .09f);
        // Convex silhouette with the two rounded grips layered over the shell.
        Polygon(mesh, new[] { new Vector2(.32f,.24f), new Vector2(.31f,.45f), new Vector2(.33f,.67f), new Vector2(.37f,.74f),
            new Vector2(.43f,.70f), new Vector2(.57f,.70f), new Vector2(.63f,.74f), new Vector2(.67f,.67f), new Vector2(.69f,.45f),
            new Vector2(.68f,.24f), new Vector2(.61f,.21f), new Vector2(.55f,.30f), new Vector2(.45f,.30f), new Vector2(.39f,.21f) }, new Color(.16f,.20f,.29f), edge);
        Disc(mesh, new Vector2(.351f,.29f), new Vector2(.039f,.15f), dark, edge);
        Disc(mesh, new Vector2(.649f,.29f), new Vector2(.039f,.15f), dark, edge);
        Disc(mesh, new Vector2(.382f,.54f), new Vector2(.066f,.178f), new Color(.23f,.28f,.38f), edge);
        Disc(mesh, new Vector2(.618f,.54f), new Vector2(.066f,.178f), new Color(.23f,.28f,.38f), edge);
        Disc(mesh, new Vector2(.43f,.325f), new Vector2(.048f,.122f), dark, edge);
        Disc(mesh, new Vector2(.57f,.325f), new Vector2(.048f,.122f), dark, edge);
        Disc(mesh, new Vector2(.43f,.325f), new Vector2(.036f,.09f), new Color(.2f,.23f,.28f), edge);
        Disc(mesh, new Vector2(.57f,.325f), new Vector2(.036f,.09f), new Color(.2f,.23f,.28f), edge);
        foreach (string control in new[] { "leftTrigger", "rightTrigger", "leftShoulder", "rightShoulder", "select", "start" })
        {
            Vector2 p = ControlPoint(control); Vector2 size = control == "select" || control == "start" ? new Vector2(.017f,.024f) : new Vector2(.037f,.027f);
            Box(mesh, p - size, p + size, dark, edge);
        }
        Box(mesh, new Vector2(.365f,.445f), new Vector2(.399f,.635f), dark, edge);
        Box(mesh, new Vector2(.342f,.51f), new Vector2(.422f,.57f), dark, edge);
        Disc(mesh, ControlPoint("buttonNorth"), new Vector2(.019f,.047f), new Color(.64f,.43f,.04f), edge);
        Disc(mesh, ControlPoint("buttonEast"), new Vector2(.019f,.047f), new Color(.65f,.09f,.10f), edge);
        Disc(mesh, ControlPoint("buttonSouth"), new Vector2(.019f,.047f), new Color(.28f,.48f,.10f), edge);
        Disc(mesh, ControlPoint("buttonWest"), new Vector2(.019f,.047f), new Color(.08f,.25f,.57f), edge);
        for (int i = 0; i < _targets.Length; i++) if (i != _focused) Path(mesh, i, new Color(.57f,.65f,.75f,.65f));
        if (_focused >= 0) Path(mesh, _focused, new Color(1,.9f,.08f));
    }
    void Path(UnityEngine.UI.VertexHelper mesh, int action, Color color)
    {
        Vector2 end = Callout(action), target = _targets[action];
        Vector2 elbow = new Vector2(end.x < .5f ? .29f : .71f, end.y);
        Line(mesh, Point(target), Point(elbow), color, 2); Line(mesh, Point(elbow), Point(end), color, 2);
        Disc(mesh, target, new Vector2(.003f,.008f), color, color);
    }
    void Disc(UnityEngine.UI.VertexHelper mesh, Vector2 center, Vector2 radius, Color fill, Color edge)
    {
        int start = mesh.currentVertCount; mesh.AddVert(Point(center), fill, Vector2.zero);
        const int count = 40;
        for (int i = 0; i <= count; i++)
        {
            float angle = i * Mathf.PI * 2 / count;
            mesh.AddVert(Point(center + Vector2.Scale(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),radius)),fill,Vector2.zero);
            if(i > 0) mesh.AddTriangle(start,start+i,start+i+1);
        }
        for (int i = 0; i < count; i++)
        {
            float a=i*Mathf.PI*2/count, b=(i+1)*Mathf.PI*2/count;
            Line(mesh,Point(center+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),radius)),Point(center+Vector2.Scale(new Vector2(Mathf.Cos(b),Mathf.Sin(b)),radius)),edge,2);
        }
    }
    void Box(UnityEngine.UI.VertexHelper mesh, Vector2 min, Vector2 max, Color fill, Color edge) =>
        Polygon(mesh,new[]{min,new Vector2(min.x,max.y),max,new Vector2(max.x,min.y)},fill,edge);
    void Polygon(UnityEngine.UI.VertexHelper mesh, Vector2[] points, Color fill, Color edge)
    {
        Vector2 center=Vector2.zero; foreach(var p in points) center+=p; center/=points.Length;
        int start=mesh.currentVertCount; mesh.AddVert(Point(center),fill,Vector2.zero);
        foreach(var p in points) mesh.AddVert(Point(p),fill,Vector2.zero);
        for(int i=0;i<points.Length;i++) { mesh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length); Line(mesh,Point(points[i]),Point(points[(i+1)%points.Length]),edge,2); }
    }
    static void Line(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Color color, float width)
    {
        Vector2 normal = new Vector2(a.y-b.y,b.x-a.x).normalized * width * .5f;
        int start=mesh.currentVertCount;
        mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
        mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
    }
}
