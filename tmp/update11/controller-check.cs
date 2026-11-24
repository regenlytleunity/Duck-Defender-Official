if (!EditorApplication.isPlaying || string.IsNullOrEmpty(SaveSystem.VerificationSavePath)) throw new Exception("An isolated Play Mode session is required.");
System.IO.File.WriteAllText("tmp/update11/controller-verification.txt","RUNNING");
var nav = UnityEngine.Object.FindFirstObjectByType<ControllerMenuNavigation>();
var menu = MainMenuUI.Instance;
var settings = menu.SettingsPanel.GetComponent<SettingsMenuUI>();
var events = UnityEngine.EventSystems.EventSystem.current;
var realPads = UnityEngine.InputSystem.Gamepad.all.Where(p => p.enabled).ToArray();
foreach (var device in realPads) UnityEngine.InputSystem.InputSystem.DisableDevice(device);
var pad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
var checks = new List<string>();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add(message); }
void State(UnityEngine.InputSystem.LowLevel.GamepadState state) => UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad,state);
System.Collections.IEnumerator Press(UnityEngine.InputSystem.LowLevel.GamepadButton button)
{
    State(new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(button));
    yield return new WaitForSecondsRealtime(.06f);
    State(new UnityEngine.InputSystem.LowLevel.GamepadState());
    yield return new WaitForSecondsRealtime(.06f);
}
System.Collections.IEnumerator Tests()
{
    ControllerBindings.Reset();
    // Exercise the one-time old-default migration and preserve unrelated choices.
    PlayerPrefs.DeleteKey(ControllerBindings.Prefix + "MenuBindingsVersion");
    PlayerPrefs.SetString(ControllerBindings.Prefix + "Confirm", "buttonEast");
    PlayerPrefs.SetString(ControllerBindings.Prefix + "Back", "buttonSouth");
    PlayerPrefs.SetString(ControllerBindings.Prefix + "Tutorial", "select");
    PlayerPrefs.SetString(ControllerBindings.Prefix + "Shoot", "leftTrigger");
    Check(ControllerBindings.Get("Confirm") == "buttonSouth" && ControllerBindings.Get("Back") == "buttonEast", "Old B/A default pair migrates to A/B.");
    Check(ControllerBindings.Get("Tutorial") == "buttonNorth" && ControllerBindings.Get("SkipTutorial") == "buttonWest" && ControllerBindings.Get("Shoot") == "leftTrigger", "Y/X tutorial defaults preserve a custom gameplay binding.");
    ControllerBindings.Reset();
    menu.OpenSettings(); yield return new WaitForSecondsRealtime(.5f);
    int frame = Time.frameCount; yield return null; Check(Time.frameCount > frame, "Play Mode frames advance.");
    var all = settings.GetComponentsInChildren<UnityEngine.UI.Selectable>().Where(s => s.IsActive() && s.IsInteractable() && (s is UnityEngine.UI.Button || s is UnityEngine.UI.Slider || s is UnityEngine.UI.Toggle)).ToArray();
    var visited = new HashSet<UnityEngine.UI.Selectable>(); var queue = new Queue<UnityEngine.UI.Selectable>(); queue.Enqueue(all[0]);
    while (queue.Count > 0)
    {
        var control = queue.Dequeue(); if (!visited.Add(control)) continue;
        var n = control.navigation;
        foreach (var next in new[] { n.selectOnLeft,n.selectOnRight,n.selectOnUp,n.selectOnDown }) if (next != null && !visited.Contains(next)) queue.Enqueue(next);
    }
    Check(all.All(visited.Contains), "Every settings button, slider and toggle is reachable through the navigation graph.");
    foreach (var toggle in new[] { settings.ShowTipsToggle, settings.ParticlesToggle })
    {
        bool before = toggle.isOn; events.SetSelectedGameObject(toggle.gameObject);
        yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
        Check(toggle.isOn != before, "A toggles " + toggle.name + ".");
        yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
        Check(toggle.isOn == before, "A restores " + toggle.name + ".");
    }
    foreach (var slider in new[] { settings.MasterSlider, settings.SfxSlider, settings.MusicSlider, settings.CameraZoomSlider })
    {
        slider.value = .5f; events.SetSelectedGameObject(slider.gameObject);
        yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
        Check(typeof(ControllerMenuNavigation).GetField("_editingSlider",flags).GetValue(nav) == null, "Single A does not edit " + slider.name + ".");
        yield return new WaitForSecondsRealtime(.5f);
        yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
        Check(typeof(ControllerMenuNavigation).GetField("_editingSlider",flags).GetValue(nav) == null, "A slow second press does not edit " + slider.name + ".");
        yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
        Check(typeof(ControllerMenuNavigation).GetField("_editingSlider",flags).GetValue(nav) == slider, "Double A edits " + slider.name + ".");
        State(new UnityEngine.InputSystem.LowLevel.GamepadState { leftStick = Vector2.right }); yield return new WaitForSecondsRealtime(.25f);
        Check(slider.value > .5f && events.currentSelectedGameObject == slider.gameObject, "Stick adjusts the focused " + slider.name + ".");
        State(new UnityEngine.InputSystem.LowLevel.GamepadState()); yield return null;
        yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.East);
        Check(menu.SettingsPanel.activeInHierarchy && typeof(ControllerMenuNavigation).GetField("_editingSlider",flags).GetValue(nav) == null, "B exits editing without leaving settings.");
        float value = slider.value;
        yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.DpadRight);
        Check(Mathf.Approximately(slider.value,value), "Right navigates without changing " + slider.name + ".");
    }
    events.SetSelectedGameObject(settings.MasterSlider.gameObject);
    ScreenCapture.CaptureScreenshot("tmp/update11/settings.png"); yield return new WaitForEndOfFrame();
    settings.OpenKeybinds(); yield return null;
    var bindings = (ControllerBindingsUI)typeof(SettingsMenuUI).GetField("_controllerBindings",flags).GetValue(settings);
    bindings.Show(true); yield return null;
    foreach (bool controllerTab in new[] { false, true, false, true })
    {
        bindings.Show(controllerTab); yield return null;
        var controls = settings.KeybindPanel.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.IsActive() && b.IsInteractable()).ToArray();
        var reached = new HashSet<UnityEngine.UI.Selectable>(); var pending = new Queue<UnityEngine.UI.Selectable>(); pending.Enqueue(controls[0]);
        while (pending.Count > 0)
        {
            var control = pending.Dequeue(); if (!reached.Add(control)) continue;
            var n = control.navigation;
            foreach (var next in new[] { n.selectOnLeft,n.selectOnRight,n.selectOnUp,n.selectOnDown })
                if (next != null && next.IsActive() && next.IsInteractable() && !reached.Contains(next)) pending.Enqueue(next);
        }
        Check(controls.All(reached.Contains), "All bindings remain reachable after switching to " + (controllerTab ? "controller" : "keyboard") + ".");
    }
    bindings.Show(false); settings.BeginBinding(0); yield return new WaitForSecondsRealtime(.35f);
    yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.East);
    Check(!settings.KeyCapturePanel.activeInHierarchy && settings.KeybindPanel.activeInHierarchy, "B cancels keyboard capture without closing keybinds.");
    yield return new WaitForSecondsRealtime(.4f);
    bindings.Show(true); yield return null;
    bindings.Begin(0); yield return new WaitForSecondsRealtime(.4f);
    yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.East);
    Check(!bindings.Capturing && settings.KeybindPanel.activeInHierarchy, "B cancels controller capture without closing the bindings page.");
    yield return new WaitForSecondsRealtime(.4f);
    bindings.Begin(0); yield return new WaitForSecondsRealtime(.4f);
    yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.LeftStick);
    Check(ControllerBindings.Get("Jump") == "leftStickPress", "Controller callout rebinds through actual gamepad input.");
    var diagram = settings.KeybindPanel.GetComponentInChildren<ControllerBindingDiagram>();
    var points = (Vector2[])typeof(ControllerBindingDiagram).GetField("_targets", flags).GetValue(diagram);
    Check(points[0] == ControllerBindingDiagram.ControlPoint("leftStickPress"), "Remapping moves the pathway endpoint to the actual button.");
    Check(!ControllerBindings.TrySet("Dash","buttonWest",out _) && !ControllerBindings.TrySet("Shoot","buttonNorth",out _), "Gameplay cannot overlap tutorial shortcuts.");
    ControllerBindings.Reset(); bindings.Show(true); yield return new WaitForSecondsRealtime(.4f);
    Canvas.ForceUpdateCanvases();
    foreach (var label in settings.KeybindPanel.transform.Find("Controller bindings").GetComponentsInChildren<TMPro.TMP_Text>())
    {
        label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "Diagram label fits: " + label.text.Replace('\n',' '));
        Check(label.color == Color.white && label.font == menu.UIFont, "Diagram text uses the original white bitmap font.");
    }
    events.SetSelectedGameObject(settings.KeybindPanel.transform.Find("Controller bindings/Jump binding").gameObject); yield return null;
    ScreenCapture.CaptureScreenshot("tmp/update11/controller.png"); yield return new WaitForEndOfFrame();
    yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.East);
    Check(!settings.KeybindPanel.activeInHierarchy && menu.SettingsPanel.activeInHierarchy, "B returns from keybinds to settings.");
    yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.East);
    Check(menu.MenuPanel.activeInHierarchy, "B returns from settings to the main menu.");
}
System.Collections.IEnumerator Run()
{
    var stack = new Stack<System.Collections.IEnumerator>(); stack.Push(Tests());
    string result = "";
    while (stack.Count > 0)
    {
        object current;
        try
        {
            var top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; }
            current = top.Current; if (current is System.Collections.IEnumerator nested) { stack.Push(nested); continue; }
        }
        catch (Exception ex) { result = "FAIL: " + ex; break; }
        yield return current;
    }
    if (result == "") result = "PASS: " + checks.Count + " controller/settings checks.";
    System.IO.File.WriteAllText("tmp/update11/controller-verification.txt",result + "\n" + string.Join("\n",checks));
    UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
    foreach (var device in realPads) if (device.added) UnityEngine.InputSystem.InputSystem.EnableDevice(device);
    Debug.Log(result);
}
nav.StartCoroutine(Run());
return "Controller verification started.";
