using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Pointer/keyboard navigation stays in the EventSystem; the first pad owns shared menus.
public class ControllerMenuNavigation : MonoBehaviour
{
    float _nextMove;
    UnityEngine.InputSystem.UI.InputSystemUIInputModule _module;
    InputActionReference _originalMove, _originalSubmit, _originalCancel;
    InputActionReference _moveReference, _submitReference, _cancelReference;
    InputActionAsset _keyboardAsset;
    InputAction _keyboardMove, _keyboardSubmit, _keyboardCancel;
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
        if (Gamepad.all.Count == 0 || EventSystem.current == null) return;
        if (LocalCoopSession.Multiplayer && LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering) return;
        var pad = Gamepad.all[0]; var events = EventSystem.current;
        var selected = events.currentSelectedGameObject != null ? events.currentSelectedGameObject.GetComponent<UnityEngine.UI.Selectable>() : null;
        if (selected == null || !selected.IsActive() || !selected.IsInteractable())
        {
            selected = null;
            foreach (var item in UnityEngine.UI.Selectable.allSelectablesArray)
                if (item.IsActive() && item.IsInteractable() && item.navigation.mode != UnityEngine.UI.Navigation.Mode.None) { selected = item; break; }
            if (selected != null) events.SetSelectedGameObject(selected.gameObject);
        }
        Vector2 direction = pad.dpad.ReadValue(); if (direction.sqrMagnitude < .1f) direction = pad.leftStick.ReadValue();
        if (direction.sqrMagnitude < .2f) _nextMove = 0;
        else if (Time.unscaledTime >= _nextMove && selected != null)
        {
            _nextMove = Time.unscaledTime + .22f;
            var data = new AxisEventData(events) { moveVector = direction,
                moveDir = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? direction.x > 0 ? MoveDirection.Right : MoveDirection.Left : direction.y > 0 ? MoveDirection.Up : MoveDirection.Down };
            ExecuteEvents.Execute(selected.gameObject, data, ExecuteEvents.moveHandler);
        }
        if (selected != null && pad.buttonEast.wasPressedThisFrame) ExecuteEvents.Execute(selected.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        if (pad.buttonSouth.wasPressedThisFrame && MainMenuUI.Instance != null) MainMenuUI.Instance.BackToMainMenu();
    }
    void OnDestroy()
    {
        if (_module != null) { _module.move = _originalMove; _module.submit = _originalSubmit; _module.cancel = _originalCancel; }
        if (_keyboardAsset != null) { _keyboardAsset.Disable(); Destroy(_keyboardAsset); }
        if (_moveReference != null) Destroy(_moveReference);
        if (_submitReference != null) Destroy(_submitReference);
        if (_cancelReference != null) Destroy(_cancelReference);
    }
}
