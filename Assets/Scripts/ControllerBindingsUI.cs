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
        // Retain the authored controls; add the five item actions and fit both columns.
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
        if (restore != null) CoopUIElements.Stretch(restore, new Vector2(.60f, .16f), new Vector2(.94f, .22f));
        _controller = CoopUIElements.Panel(panel, "Controller bindings", new Vector2(.04f, .17f), new Vector2(.96f, .81f), new Color(.1f, .15f, .22f)).gameObject;
        var sticks = CoopUIElements.Button(_controller.transform, "", new Vector2(.04f, .83f), new Vector2(.96f, .98f), () => { ControllerBindings.SwapSticks(); Refresh(); });
        _sticks = sticks.GetComponentInChildren<TMP_Text>();
        for (int i = 0; i < _labels.Length; i++)
        {
            int action = i;
            float y = .67f - i % 5 * .125f, x = i / 5 * .5f;
            CoopUIElements.Text(_controller.transform, ControllerBindings.ActionNames[i], new Vector2(x + .015f, y), new Vector2(x + .225f, y + .105f), 26);
            _labels[i] = CoopUIElements.Button(_controller.transform, "", new Vector2(x + .23f, y), new Vector2(x + .485f, y + .105f), () => Begin(action)).GetComponentInChildren<TMP_Text>();
        }
        CoopUIElements.Button(_controller.transform, "Restore controller defaults", new Vector2(.16f, .015f), new Vector2(.84f, .12f), () => { ControllerBindings.Reset(); Refresh(); });
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
        MainMenuUI.FocusFirstControl(controller ? _controller : _keyboard);
    }
    public void Begin(int index)
    {
        if (Gamepad.all.Count == 0) { _settings.KeybindStatus.text = "Connect a controller first."; return; }
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
        if (!Capturing || Time.unscaledTime < _captureAfter) return;
        var pad = Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
        if (pad == null || Input.GetKeyDown(KeyCode.Escape) || Time.unscaledTime > _captureExpires) { Cancel(); return; }
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
        MainMenuUI.FocusFirstControl(_controller);
    }
    void Refresh()
    {
        _sticks.text = ControllerBindings.RightMoveStick ? "Move: RIGHT stick | Aim: LEFT stick" : "Move: LEFT stick | Aim: RIGHT stick";
        for (int i = 0; i < _labels.Length; i++) _labels[i].text = ControllerBindings.Label(ControllerBindings.Actions[i]);
    }
}
