if (!EditorApplication.isPlaying || string.IsNullOrEmpty(SaveSystem.VerificationSavePath)) throw new Exception("Isolated Play Mode required.");
var session = LocalCoopSession.Instance;
if (session.Players.Count != 1) throw new Exception("Solo test scene required.");
var player = session.Players[0];
var checks = new List<string>();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }
var padsBefore = UnityEngine.InputSystem.Gamepad.all.ToArray();
foreach (var device in padsBefore) if (device.enabled) UnityEngine.InputSystem.InputSystem.DisableDevice(device);
var disabled = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
UnityEngine.InputSystem.InputSystem.DisableDevice(disabled);
var active = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
void State(UnityEngine.InputSystem.LowLevel.GamepadState value) => UnityEngine.InputSystem.InputSystem.QueueStateEvent(active, value);
System.Collections.IEnumerator Tests()
{
    WaveManager.Instance.StopAllCoroutines();
    EnemyTipUI.Instance.Dismiss(); EnemyTipUI.Instance.enabled = false;
    foreach (var enemy in EnemyBase.ActiveEnemies.ToArray()) { enemy.gameObject.SetActive(false); UnityEngine.Object.Destroy(enemy.gameObject); }
    ControllerBindings.Reset(); Time.timeScale = 1;
    yield return null; yield return null;
    Check(ControllerBindings.ConnectedCount == 1, "Only the enabled controller is counted.");
    Check(ControllerBindings.MenuPad == active, "Menu routing skips disabled controllers ahead of the active controller.");
    Check(ControllerBindings.GamepadAt(-1) == null && ControllerBindings.GamepadAt(1) == null, "Invalid enabled-controller seat indexes safely return null.");
    Check(player.UsesGamepad && player.Connected && player.Controller == active, "Solo auto assignment skips disabled controllers.");
    Check(!MobileInputController.Instance.IsMobileEnabled, "Desktop scene has mobile input disabled.");
    Check(TutorialDirector.AllowMovement && TutorialDirector.AllowJump && TutorialDirector.AllowShooting, "Tutorial gates permit normal gameplay in a completed profile.");
    Check(player.Movement.enabled && player.Weapon.enabled && player.Alive, "Player movement and weapon components are enabled and alive.");
    var start = player.transform.position;
    State(new UnityEngine.InputSystem.LowLevel.GamepadState { leftStick = Vector2.right, rightStick = Vector2.up, rightTrigger = 1 });
    yield return new WaitForSeconds(.16f);
    Check(InputHelper.GetHorizontal(player) > .9f && player.transform.position.x > start.x + .1f, "Controller input moves the actual player body.");
    Check(InputHelper.GetShootHeld(player), "Controller trigger reaches the weapon input route.");
    Check(Vector2.Dot(player.Weapon.AimDirection, Vector2.up) > .99f, "Controller aim reaches the weapon.");
    State(new UnityEngine.InputSystem.LowLevel.GamepadState()); yield return null; yield return null;
    Check(Mathf.Abs(InputHelper.GetHorizontal(player)) < .01f && !InputHelper.GetShootHeld(player), "Releasing the controller clears movement and fire.");
    UnityEngine.InputSystem.InputSystem.DisableDevice(active);
    Check(!player.Connected, "An assigned disabled controller is immediately disconnected.");
    Check(ControllerBindings.Button(active, "Shoot") == null && ControllerBindings.Move(active) == Vector2.zero && ControllerBindings.Aim(active) == Vector2.zero && ControllerBindings.MenuMove(active) == Vector2.zero, "Disabled devices cannot supply buttons, movement, aim or menu state.");
    yield return null; yield return null;
    Check(!player.UsesGamepad && player.Controller == null && player.Connected, "Solo falls back to keyboard when every controller is disabled.");
    Check(ControllerBindings.MenuPad == null && ControllerBindings.ConnectedCount == 0, "Disabled gamepads do not occupy menus or count as connected.");
    Check(InputHelper.GetHorizontal(player) == InputManager.Instance.GetHorizontalInput() && InputHelper.GetVertical(player) == InputManager.Instance.GetVerticalInput(), "Movement routes to saved keyboard bindings after fallback.");
    Check(InputHelper.GetJumpHeld(player) == InputManager.Instance.IsJumpHeld() && InputHelper.GetShootHeld(player) == InputManager.Instance.IsShootHeld(), "Jump and fire route to saved keyboard/mouse bindings after fallback.");
    Check(InputHelper.GetMousePosition(player) == Input.mousePosition, "Aim routes to the mouse after fallback.");
    UnityEngine.InputSystem.InputSystem.EnableDevice(active); yield return null; yield return null;
    Check(player.UsesGamepad && player.Controller == active, "Re-enabling a controller restores solo controller control.");
    UnityEngine.InputSystem.InputSystem.RemoveDevice(active); yield return null; yield return null;
    Check(!player.UsesGamepad, "Removing the only enabled controller restores keyboard control.");
    UnityEngine.InputSystem.InputSystem.AddDevice(active); yield return null; yield return null;
    Check(player.Controller == active && player.Connected, "Reconnecting restores controller control.");
    UnityEngine.InputSystem.InputSystem.EnableDevice(disabled); yield return null; yield return null;
    Check(player.Controller == active && ControllerBindings.ConnectedCount == 2, "A second controller does not steal an existing solo assignment.");
    UnityEngine.InputSystem.InputSystem.DisableDevice(active); yield return null; yield return null;
    Check(player.Controller == disabled && player.Connected, "Disabling the assigned controller switches solo to another available controller.");
    UnityEngine.InputSystem.InputSystem.DisableDevice(disabled); yield return null; yield return null;
    // Exercise the actual co-op Update gate with two distinct owners.
    player.AssignController(active, true);
    var otherObject = new GameObject("Temporary input regression seat");
    var other = otherObject.AddComponent<LocalPlayer>(); other.AssignController(disabled, true);
    session.Players.Add(other); yield return null; yield return null;
    Check(session.DevicesMissing && Time.timeScale == 0, "Disabled co-op controllers pause the run.");
    UnityEngine.InputSystem.InputSystem.EnableDevice(active); UnityEngine.InputSystem.InputSystem.EnableDevice(disabled);
    yield return null; yield return null;
    Check(!session.DevicesMissing && Time.timeScale == 1, "Re-enabled co-op controllers resume the run.");
    session.Players.Remove(other); UnityEngine.Object.Destroy(otherObject);
    // Deliberately leave both ad-hoc devices and disabled physical hardware in
    // place. The Editor verification snapshot must clean up on Stop, without
    // relying on this coroutine or a probe's OnDestroy.
}
System.Collections.IEnumerator Run()
{
    var tests = Tests(); string result = null;
    while (true)
    {
        object current;
        try { if (!tests.MoveNext()) break; current = tests.Current; }
        catch (Exception e) { result = "FAIL: " + e; break; }
        yield return current;
    }
    if (result == null) result = "PASS: " + checks.Count + " input-routing regression checks";
    System.IO.File.WriteAllText("tmp/input-diagnosis/routing-results.txt", result + "\n" + string.Join("\n", checks));
    Debug.Log(result); Time.timeScale = 0;
}
session.StartCoroutine(Run());
return "Input routing regression started";
