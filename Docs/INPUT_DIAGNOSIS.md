# Input diagnosis — November 25, 2026

The confirmed problem was Editor input state left behind by earlier automated verification. Two synthetic controllers (device IDs 35 and 36, `native=False`, no hardware interface) remained registered after testing. Solo gameplay selected the first registered controller and bypassed keyboard/mouse input. Shared controller menus also selected the first enabled controller. Consequently, an idle synthetic controller could capture the player's seat and menu controls even when the physical controller was connected.

Both synthetic devices were removed from the running Editor. A separate code defect also allowed disabled controllers to occupy gameplay seats and count toward the hosting requirement. That defect is fixed.

Windows reported an Xbox 360 Controller for Windows and an HID game controller with status OK. A direct Windows XInput read succeeded for controller slot 0. Unity initially listed a native XInput controller as disconnected, then detected the connected device. The final gameplay check assigned the real `XInputControllerWindows` controller, with one available physical controller and zero synthetic controllers. The transient disconnection's cause was not established. No hardware failure was demonstrated.

The existing solo behavior still gives an available controller priority. To test keyboard/mouse gameplay, disconnect the controller. Co-op continues to require an assigned controller per player, except for its existing developer keyboard test mode.

## Changes in this diagnosis

Earlier Update 12 changes remain in the working tree. This table lists the additional source/document changes for the input issue only.

| File | Reason |
| --- | --- |
| `Assets/Scripts/ControllerBindings.cs` | Added one consistent available-controller check and filtered controller count/seat lookup. Disabled or removed controllers return no buttons, movement, aim or menu input. |
| `Assets/Scripts/LocalCoopSession.cs` | Solo startup and hotplug selection skip disabled controllers. Solo falls back to keyboard/mouse when none are available. Initial co-op fallback assignment uses the same filtered list. |
| `Assets/Scripts/LocalPlayer.cs` | A disabled assigned controller now counts as disconnected, allowing the existing co-op pause/reconnect behavior to work. |
| `Assets/Scripts/HostMenuUI.cs` | Hosting requirements and displayed controller counts exclude disabled controllers. |
| `Assets/Scripts/CoopLobbyUI.cs` | Customization seats use available controllers and show reconnect guidance for disabled devices. |
| `Assets/Scripts/ControllerMenuNavigation.cs` | Menu input and focus indicators use the same available-controller selection. |
| `Assets/Scripts/CoopVerificationProbe.cs` | Verification temporarily disables real controllers instead of removing their native registrations. Cleanup re-enables controllers still present without resurrecting unplugged devices. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Isolated verification snapshots gamepad IDs and enabled state. On leaving Play Mode, it removes newly created synthetic gamepads and restores original enabled states, including after an interrupted probe. The snapshot survives script domain reloads. Physical devices connected during testing are preserved. |
| `Docs/ARCHITECTURE.md` | Documents disabled-controller filtering and keyboard fallback. |
| `Docs/INPUT_DIAGNOSIS.md` | Records findings, changes, verification and remaining manual checks. |

Diagnostic artifacts are under `tmp/input-diagnosis/`: `before.patch` and `UIUpdateVerification.before.cs` preserve the starting changes; `routing-check.cs` is the temporary regression driver; `routing-results.txt`, `coop-results.txt`, `cleanup-results.txt` and `physical-routing.txt` record results. They are outside Unity's Assets tree and do not enter player builds.

## Verification performed

- Unity 6000.0.35f1 completed compilation successfully.
- 192 co-op integration checks passed with the physical controller still registered but temporarily disabled. These exercise actual menus, controller remapping, independent movement/aim/fire, four-player ownership, disconnect/reconnect, card selection, gameplay and solo hotplug fallback.
- 25 focused Play Mode checks passed. They cover disabled-controller filtering, actual player movement, weapon input and aim, release handling, keyboard/mouse routing, switching between available controllers, and co-op disable/resume behavior.
- The focused test deliberately left two ad-hoc synthetic devices registered. Stopping Play Mode removed them and restored the original physical controller's enabled state. Verification save/device state was cleared.
- A fresh isolated Play Mode session assigned the real physical controller to the solo player. Movement and weapon components were enabled, the player was alive, and mobile controls were disabled.
- Both input backends remain enabled (`activeInputHandler = 2`). No project settings, packages, scenes, prefabs, serialized fields or asset references were changed for this fix.
- `git diff --check` found no whitespace errors. There were no new captured Console errors during these checks. Historical errors and existing missing-script warnings were not cleared.

Automated controller checks use simulated input. Actual physical key presses, controller buttons/sticks, mobile play and player builds were not tested during this diagnosis. Keyboard fallback was verified to reach the existing saved key-binding methods; this does not prove physical keyboard delivery while the Game view is focused. Native controller detection and assignment were verified, but hands-on controller response remains a manual check.

No Inspector, scene or prefab setup is required. No new Unity assets or scripts need importing. Unity is left stopped in Edit Mode with the MainMenu scene open.

## Hands-on Play Mode checklist

1. Enter Play Mode and click the Game view. Make sure the Editor Pause button is off. Advance any tutorial dialogue or enemy-tip overlay before testing gameplay. Practice intentionally unlocks actions in stages.
2. With the controller connected, test the saved bindings. Defaults are left stick to move/up to jump, right stick to aim, RT to fire, LB to dash, RB to use an item, and D-pad directions to select item slots. Test item use with an equipped item and dash outside tutorial practice.
3. Test menu navigation and confirmation. Defaults are left stick/D-pad navigation, A/bottom face button to confirm, B/right face button to go back.
4. Disconnect the controller, click the Game view, and test keyboard/mouse gameplay. Defaults are A/D or left/right arrows, Space/W to jump, Left Shift to dash, left mouse to shoot, right mouse to use an item, and 1–4 to select an item.
5. Reconnect the controller and confirm it regains solo control. In co-op, unplug an assigned controller and check that the run pauses and resumes after reconnection.
6. Stop and restart Play Mode. Confirm controller behavior persists without synthetic controllers in Window > Analysis > Input Debugger.

If input still fails, check focus first: the installed Input System configuration makes keyboard and pointer input respect Game-view focus. Check saved bindings in Settings and whether a tutorial/modal or co-op disconnect has paused the game. If the physical controller disappears again, compare Windows controller detection with Unity's Input Debugger. A controller missing from Windows suggests a USB/connection/driver issue; one present in Windows but missing from Unity suggests an Editor/device-registration issue. Reconnecting it or restarting the Editor can distinguish a stale session from a persistent connection problem. These are follow-up possibilities, not additional confirmed faults.
