using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Pointer/keyboard navigation stays in the EventSystem; the first pad owns shared menus.
public class ControllerMenuNavigation : MonoBehaviour
{
    // Build links only within the visible page/modal. Disabled purchases and
    // hidden pages never become dead ends or steal focus from another pane.
    public static void ConfigureNavigation(GameObject scope)
    {
        if (scope == null) return;
        Canvas.ForceUpdateCanvases();
        var controls = System.Array.FindAll(scope.GetComponentsInChildren<UnityEngine.UI.Button>(), b => b.IsActive() && b.IsInteractable());
        foreach (var control in controls)
            control.navigation = new UnityEngine.UI.Navigation {
                mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnLeft = FindInDirection(control, controls, Vector2.left),
                selectOnRight = FindInDirection(control, controls, Vector2.right),
                selectOnUp = FindInDirection(control, controls, Vector2.up),
                selectOnDown = FindInDirection(control, controls, Vector2.down)
            };
        var events = EventSystem.current;
        var selected = events != null && events.currentSelectedGameObject != null ? events.currentSelectedGameObject.GetComponent<UnityEngine.UI.Button>() : null;
        if (selected != null && selected.transform.IsChildOf(scope.transform) && (!selected.IsActive() || !selected.IsInteractable()) && controls.Length > 0)
        {
            UnityEngine.UI.Button nearest = controls[0]; float distance = float.PositiveInfinity;
            foreach (var control in controls)
            {
                float candidate = (control.transform.position - selected.transform.position).sqrMagnitude;
                if (candidate < distance) { distance = candidate; nearest = control; }
            }
            events.SetSelectedGameObject(nearest.gameObject);
        }
    }
    public static UnityEngine.UI.Button FindInDirection(UnityEngine.UI.Button current, UnityEngine.UI.Button[] controls, Vector2 direction)
    {
        var rect = (RectTransform)current.transform;
        Vector2 origin = rect.TransformPoint(rect.rect.center);
        float best = float.PositiveInfinity;
        UnityEngine.UI.Button next = null;
        foreach (var control in controls)
        {
            if (control == current || !control.IsActive() || !control.IsInteractable()) continue;
            var target = (RectTransform)control.transform;
            Vector2 delta = (Vector2)target.TransformPoint(target.rect.center) - origin;
            float along = Vector2.Dot(delta, direction);
            if (along <= .01f) continue;
            float across = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
            float score = along + across * 2.5f + across * across / along;
            if (score < best) { best = score; next = control; }
        }
        return next;
    }
    float _nextMove;
    UnityEngine.UI.Slider _editingSlider;
    bool _waitForNeutral;
    Transform _lastFocusScope;
    Vector3 _lastFocusPosition;
    bool _hasLastFocus;
    UnityEngine.InputSystem.UI.InputSystemUIInputModule _module;
    InputActionReference _originalMove, _originalSubmit, _originalCancel;
    InputActionReference _moveReference, _submitReference, _cancelReference;
    InputActionAsset _keyboardAsset;
    InputAction _keyboardMove, _keyboardSubmit, _keyboardCancel;
    ControllerSelectionFrame _selectionFrame;
    void Start()
    {
        _module = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (_module == null) return;
        _originalMove = _module.move; _originalSubmit = _module.submit; _originalCancel = _module.cancel;
        _keyboardAsset = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = new InputActionMap("Keyboard UI"); _keyboardAsset.AddActionMap(map);
        _keyboardMove = map.AddAction("Move", InputActionType.Value);
        _keyboardMove.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        _keyboardSubmit = map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
        _keyboardCancel = map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
        _module.move = _moveReference = InputActionReference.Create(_keyboardMove); _module.submit = _submitReference = InputActionReference.Create(_keyboardSubmit); _module.cancel = _cancelReference = InputActionReference.Create(_keyboardCancel);
        _keyboardMove.Enable(); _keyboardSubmit.Enable(); _keyboardCancel.Enable();
    }
    void Update()
    {
        if (Gamepad.all.Count == 0 || EventSystem.current == null) { _editingSlider = null; return; }
        if (CoopLobbyUI.Instance != null && CoopLobbyUI.Instance.IsOpen) return;
        var menu = MainMenuUI.Instance;
        if (menu != null && menu.SettingsPanel.activeInHierarchy && menu.SettingsPanel.GetComponent<SettingsMenuUI>().CapturingInput) return;
        if (LocalCoopSession.Multiplayer && LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering) return;
        Gamepad pad = null;
        foreach (var device in Gamepad.all) if (device.enabled) { pad = device; break; }
        if (pad == null) return;
        if (menu != null && menu.HandlePackControllerInput(pad)) return;
        var events = EventSystem.current;
        var scope = menu != null ? menu.NavigationScope.transform : null;
        var selected = events.currentSelectedGameObject != null ? events.currentSelectedGameObject.GetComponent<UnityEngine.UI.Selectable>() : null;
        if (selected == null || !selected.IsActive() || !selected.IsInteractable() || scope != null && !selected.transform.IsChildOf(scope))
        {
            selected = null;
            float nearest = float.PositiveInfinity;
            foreach (var item in UnityEngine.UI.Selectable.allSelectablesArray)
                if (item.IsActive() && item.IsInteractable() && item.navigation.mode != UnityEngine.UI.Navigation.Mode.None && (scope == null || item.transform.IsChildOf(scope)))
                {
                    if (!_hasLastFocus || _lastFocusScope != scope) { selected = item; break; }
                    float distance = (item.transform.position - _lastFocusPosition).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; selected = item; }
                }
            if (selected != null) events.SetSelectedGameObject(selected.gameObject);
        }
        Vector2 direction = ControllerBindings.MenuMove(pad);
        if (_editingSlider != null && (_editingSlider != selected || !_editingSlider.IsInteractable())) _editingSlider = null;
        if (_editingSlider != null)
        {
            if (ControllerBindings.Pressed(pad, "Confirm") || ControllerBindings.Pressed(pad, "Back"))
            { _editingSlider = null; _waitForNeutral = true; return; }
            float axis = pad.leftStick.ReadValue().x;
            if (Mathf.Abs(axis) > .2f)
                _editingSlider.value += Mathf.Sign(axis) * ((Mathf.Abs(axis) - .2f) / .8f) *
                    (_editingSlider.maxValue - _editingSlider.minValue) * .6f * Time.unscaledDeltaTime;
            return;
        }
        if (_waitForNeutral)
        {
            if (direction.sqrMagnitude < .2f) _waitForNeutral = false;
            else direction = Vector2.zero;
        }
        if (direction.sqrMagnitude < .2f) _nextMove = 0;
        else if (Time.unscaledTime >= _nextMove && selected != null)
        {
            _nextMove = Time.unscaledTime + .22f;
            var data = new AxisEventData(events) { moveVector = direction,
                moveDir = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? direction.x > 0 ? MoveDirection.Right : MoveDirection.Left : direction.y > 0 ? MoveDirection.Up : MoveDirection.Down };
            if (selected is UnityEngine.UI.Slider)
            {
                var next = data.moveDir == MoveDirection.Left ? selected.FindSelectableOnLeft() :
                    data.moveDir == MoveDirection.Right ? selected.FindSelectableOnRight() :
                    data.moveDir == MoveDirection.Up ? selected.FindSelectableOnUp() : selected.FindSelectableOnDown();
                if (next != null) events.SetSelectedGameObject(next.gameObject);
            }
            else ExecuteEvents.Execute(selected.gameObject, data, ExecuteEvents.moveHandler);
            selected = events.currentSelectedGameObject != null ? events.currentSelectedGameObject.GetComponent<UnityEngine.UI.Selectable>() : null;
            if (scope != null && selected != null && !selected.transform.IsChildOf(scope)) { MainMenuUI.FocusFirstControl(scope.gameObject); selected = events.currentSelectedGameObject?.GetComponent<UnityEngine.UI.Selectable>(); }
        }
        if (selected != null)
        {
            // Capture before submission: a purchase can disable or replace its
            // button synchronously, clearing EventSystem selection.
            _lastFocusScope = scope; _lastFocusPosition = selected.transform.position; _hasLastFocus = true;
            if (ControllerBindings.Pressed(pad, "Confirm"))
            {
                if (selected is UnityEngine.UI.Slider slider) { _editingSlider = slider; return; }
                ExecuteEvents.Execute(selected.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            }
        }
        if (ControllerBindings.Pressed(pad, "Back") && menu != null) menu.BackToMainMenu();
    }
    void LateUpdate()
    {
        var events = EventSystem.current;
        var selected = events != null && events.currentSelectedGameObject != null
            ? events.currentSelectedGameObject.GetComponent<UnityEngine.UI.Selectable>() : null;
        bool separateCards = LocalCoopSession.Multiplayer && LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering;
        if (Gamepad.all.Count == 0 || separateCards || CoopLobbyUI.Instance != null && CoopLobbyUI.Instance.IsOpen || selected == null || !selected.IsActive() || !selected.IsInteractable())
        { _selectionFrame?.Hide(); return; }
        if (_selectionFrame == null) _selectionFrame = new ControllerSelectionFrame("Controller menu selection");
        _selectionFrame.Show((RectTransform)selected.transform, _editingSlider != null ? new Color(.35f, 1, .55f) : (Color?)null);
    }
    void OnDisable() { _selectionFrame?.Hide(); _editingSlider = null; }
    void OnDestroy()
    {
        _selectionFrame?.Dispose();
        if (_module != null) { _module.move = _originalMove; _module.submit = _originalSubmit; _module.cancel = _originalCancel; }
        if (_keyboardAsset != null) { _keyboardAsset.Disable(); Destroy(_keyboardAsset); }
        if (_moveReference != null) Destroy(_moveReference);
        if (_submitReference != null) Destroy(_submitReference);
        if (_cancelReference != null) Destroy(_cancelReference);
    }
}

// Draw in screen pixels so scaled cards, sliders and sprite buttons all have the
// same clear focus border. Separate graphics avoid tint/alpha transitions on targets.
public sealed class ControllerSelectionFrame
{
    readonly Canvas _canvas;
    readonly RectTransform _frame;
    readonly RectTransform[] _edges = new RectTransform[8];
    readonly UnityEngine.UI.Image[] _graphics = new UnityEngine.UI.Image[8];
    readonly Vector3[] _corners = new Vector3[4];

    public ControllerSelectionFrame(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        _canvas = root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        var frame = new GameObject("Focus border", typeof(RectTransform));
        _frame = frame.GetComponent<RectTransform>(); _frame.SetParent(root.transform, false);
        _frame.anchorMin = _frame.anchorMax = _frame.pivot = Vector2.zero;
        for (int i = 0; i < _edges.Length; i++)
        {
            var edge = new GameObject(i < 4 ? "Dark border" : "Yellow border", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            _edges[i] = edge.GetComponent<RectTransform>(); _edges[i].SetParent(_frame, false);
            var graphic = edge.GetComponent<UnityEngine.UI.Image>();
            _graphics[i] = graphic;
            graphic.color = i < 4 ? new Color(.025f, .035f, .05f, 1) : new Color(1, .9f, .08f, 1);
            graphic.raycastTarget = false;
        }
        Hide();
    }

    public void Show(RectTransform target, Color? color = null)
    {
        if (target == null || !target.gameObject.activeInHierarchy) { Hide(); return; }
        var targetCanvas = target.GetComponentInParent<Canvas>();
        if (targetCanvas == null) { Hide(); return; }
        var camera = targetCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.rootCanvas.worldCamera;
        target.GetWorldCorners(_corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]), max = min;
        for (int i = 1; i < _corners.Length; i++)
        {
            var point = RectTransformUtility.WorldToScreenPoint(camera, _corners[i]);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        if (max.x - min.x < 1 || max.y - min.y < 1) { Hide(); return; }
        float scale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        float padding = 8 * scale;
        if (!targetCanvas.overrideSorting) targetCanvas = targetCanvas.rootCanvas;
        _canvas.sortingLayerID = targetCanvas.sortingLayerID;
        _canvas.sortingOrder = targetCanvas.sortingOrder + 1;
        _canvas.gameObject.SetActive(true);
        for (int i = 4; i < 8; i++) _graphics[i].color = color ?? new Color(1, .9f, .08f, 1);
        _frame.anchoredPosition = min - Vector2.one * padding;
        _frame.sizeDelta = max - min + Vector2.one * (padding * 2);
        SetRing(0, 0, 8 * scale);
        SetRing(4, 2 * scale, 4 * scale);
    }

    void SetRing(int first, float inset, float width)
    {
        SetEdge(_edges[first], new Vector2(0, 0), new Vector2(1, 0), new Vector2(inset, inset), new Vector2(-inset, inset + width));
        SetEdge(_edges[first + 1], new Vector2(0, 1), new Vector2(1, 1), new Vector2(inset, -inset - width), new Vector2(-inset, -inset));
        SetEdge(_edges[first + 2], new Vector2(0, 0), new Vector2(0, 1), new Vector2(inset, inset), new Vector2(inset + width, -inset));
        SetEdge(_edges[first + 3], new Vector2(1, 0), new Vector2(1, 1), new Vector2(-inset - width, inset), new Vector2(-inset, -inset));
    }
    static void SetEdge(RectTransform edge, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    { edge.anchorMin = min; edge.anchorMax = max; edge.offsetMin = offsetMin; edge.offsetMax = offsetMax; }
    public void Hide() { if (_canvas != null) _canvas.gameObject.SetActive(false); }
    public void Dispose() { if (_canvas != null) Object.Destroy(_canvas.gameObject); }
}
