# Duck Defender Update 3

Implemented the 13 issues in `Duck Defender Update (3).pdf`, including the later
instruction to use the exact `Assets/Fonts/DuckDefenderTestFontv2.asset` font.
The existing bitmap atlas/material are retained; no regenerated font is used.

## Implemented behavior

1. The card index exposes focusable cards and explicit paths to their upgrade or
   ascension controls. Confirm on a card focuses its available action; confirm
   again activates it. Focus remains visible with the yellow/dark selection frame.
   Sidebar navigation follows Back, Mobility, Munitions, Survival, Gadget, Layout
   from top to bottom, and reverses that order when moving up.
2. Purchase confirmation locks the underlying shop and owns controller focus.
   No/Back restores shop interaction. Purchase opening also restricts focus.
3. Host options offer **Health bar style: Classic / Above player**. Classic
   stacks copies of the existing solo bar artwork at the left; its fill uses the
   player's selected color. Above player retains overhead colored bars.
4. The co-op XP bar spans 60% of the top width, with level, wave, enemy and coin
   counters laid out around it.
5. Solo camera zoom sits below the volume controls and is hidden in keybindings
   and reset confirmation.
6. A connected controller takes priority for solo gameplay. Disconnecting it
   restores keyboard/mouse; reconnecting switches back automatically.
7. The index cycles **3 x 1 / 4 x 2 / ALL**. ALL fits every card in the selected
   category on one page; category tabs remain available.
8. Co-op card confirmation toggles selection. Browsing is yellow; selected is
   green. Upgrades apply once only after everyone stays ready for 1.25 seconds.
   Deselecting during that window cancels the countdown without changing stats.
9. New runtime labels use the authored DuckDefenderTestFontv2 asset through the
   menu/game UI font references. AGENTS.md requires this exact font for future UI.
10. Start co-op opens one customization pane per player. Each assigned controller
    owns its own color selector, name keyboard and ready state. Available colors:
    red, orange, yellow, green, blue, purple. Each selection previews the existing
    duck sprite in that color, retaining the beak and outline. Duplicate colors
    are allowed. Selecting Name clears the current name automatically. Names
    support up to 10 letters/numbers/spaces; blank names become PLAYER N. Everyone
    must remain ready for 1.25 seconds before launch. Names appear above the duck
    in Classic mode and above the health bar in Above player mode.
11. Ground/background terrain continues beyond both player walls. A 30-unit
    reserve allows offscreen spawns on either side even at the map edges. Enemy
    collision ignores the four side blockers while players remain bounded.
    Ground collision geometry was regenerated through Unity's Editor APIs.
12. Keybinds includes Keyboard and Controller tabs. Controller settings remap
    Jump, Dash, Shoot, Confirm and Back, and swap the move/aim sticks. Conflicting
    mappings within gameplay or menu actions are rejected. Menu movement remains
    on D-pad/left stick. Start/Escape cancels capture; capture also times out.
13. Co-op death plays the existing death animation with the chosen palette.
    Dead players stop gameplay actions without invoking solo game-over. The last
    death pauses the run, allows two seconds of unscaled animation, then shows
    team results. Optional next-wave respawn restores animation and movement.

Profiles and health style are session options. Controller bindings and solo
zoom persist in PlayerPrefs. The account-save schema is unchanged.

## Files changed and why

| File | Change |
| --- | --- |
| `AGENTS.md` | Require the exact authored v2 font and retain white text infill rules. |
| `Assets/Scenes/MainMenu.unity` | Save zoom placement, cancel-purchase wiring, the exact font reference, and gameplay duck preview sprite. |
| `Assets/Scenes/SampleScene.unity` | Save spawn-reserve terrain/backgrounds, rebuilt collision, player-wall references, and exact font reference. |
| `Assets/Scripts/CardDisplay.cs` | Focusable index cards/action controls, navigation refresh after upgrades, and shared authored font. |
| `Assets/Scripts/CardIndexUI.cs` | ALL layout, explicit controller navigation and focus restoration. |
| `Assets/Scripts/ControllerBindings.cs` | Saved controller action mappings, stick assignment and conflict validation. |
| `Assets/Scripts/ControllerBindingsUI.cs` | Controller tab, capture, cancel/timeout, defaults and mapping feedback. |
| `Assets/Scripts/ControllerMenuNavigation.cs` | Restrict navigation to active modal, suppress during capture/customization, and support green selection frames. |
| `Assets/Scripts/CoopLobbyUI.cs` | Independent player customization, colored duck previews, automatic name clearing, onscreen keyboards and ready coordination. |
| `Assets/Scripts/CoopRunUI.cs` | Both health styles, wide XP, names, reversible card selection, exact font helper and delayed results. |
| `Assets/Scripts/CoopVerificationProbe.cs` | Isolated controller, modal, layout, profile, spawn, death, font and hotplug integration coverage. |
| `Assets/Scripts/Editor/CoopAdjustmentSetup.cs` | Repeatable saved scene/font/terrain setup using Unity APIs. |
| `Assets/Scripts/Editor/CoopUpdateVerification.cs` | Reset transient profiles/device ownership after verification. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Preserve controller/zoom preferences during tests; update ALL and host-menu expectations. |
| `Assets/Scripts/EnemyBase.cs` | Allow enemies through the player-only side blockers. |
| `Assets/Scripts/EnemyTipUI.cs` | Honor remapped confirm and use the authored UI font. |
| `Assets/Scripts/GameUI.cs` | Serialized reference to the exact UI font and shared runtime initialization. |
| `Assets/Scripts/HostMenuUI.cs` | Health style option, current binding prompts, preview sprite reference, and launch customization. |
| `Assets/Scripts/Inputhelper.cs` | Route gameplay dash/shoot through controller mappings. |
| `Assets/Scripts/LocalCoopSession.cs` | Session profiles/devices, health style, solo controller priority and delayed team results. |
| `Assets/Scripts/LocalPlayer.cs` | Chosen name/color, remapped movement/jump/aim and controller reassignment. |
| `Assets/Scripts/MainMenuUI.cs` | Shop modal locking/focus/cancel routing and exact font reference. |
| `Assets/Scripts/PlayerHealth.cs` | Visible co-op death animation and clean respawn renderer state. |
| `Assets/Scripts/Playeranimator.cs` | Unscaled co-op death and restored normal animation after respawn. |
| `Assets/Scripts/SettingsMenuUI.cs` | Controller tab integration, input-capture scope and correct zoom visibility. |
| `Assets/Scripts/WorldCamera.cs` | Spawn reserve, actual-terrain raycasts and precision-safe distance checks. |
| `Assets/Resources/HealthPalette.shader` | Recolor the classic health fill while preserving its border. |
| `Assets/Resources/DuckPreviewPalette.shader` | Display the existing duck in UI with the gameplay body palette and unchanged beak/outline. |
| `Docs/ARCHITECTURE.md` | Document current profiles, input, UI, fonts and spawn bounds. |
| `Docs/COOP_UPDATE.md` | Link the older report to this superseding update. |
| `Docs/UPDATE_3_REPORT.md` | Implementation, setup, verification and acceptance record. |

Unity generated these new metadata files during import:

- `Assets/Scripts/ControllerBindings.cs.meta`
- `Assets/Scripts/ControllerBindingsUI.cs.meta`
- `Assets/Scripts/CoopLobbyUI.cs.meta`
- `Assets/Scripts/Editor/CoopAdjustmentSetup.cs.meta`
- `Assets/Resources/HealthPalette.shader.meta`
- `Assets/Resources/DuckPreviewPalette.shader.meta`

## Unity setup

The required setup was performed and saved. No additional Inspector or prefab
steps are required for MainMenu and SampleScene.

- MainMenuUI.UIFont and GameUI.UIFont reference the original
  `Assets/Fonts/DuckDefenderTestFontv2.asset`.
- WorldCamera.SpawnPadding is 30. PlayerWalls contains both side map borders and
  both side anti-falloff colliders. The playable bounds remain unchanged.
- Ground terrain expands from 114 to 190 horizontal cells; collision covers it.
- The existing confirmation No button calls MainMenuUI.CancelPurchase.
- Runtime customization and HUD objects require no prefab changes.
- HostMenuUI.DuckPreviewSprite references Duck_0 from the existing gameplay sprite
  sheet, `Assets/Sprites/Player Sprites/Duck.png`; its Point filtering is retained.

For a fresh copy of an earlier scene, save open edits and run
**Duck Defender > Co-op > Apply update 3 adjustments**. The terrain expansion is
guarded by SpawnPadding, so repeat application does not extend it again.
New serialized fields are UIFont on MainMenuUI/GameUI, DuckPreviewSprite on
HostMenuUI, and SpawnPadding/PlayerWalls on WorldCamera. No existing serialized
fields were renamed.

## Verification

Unity compilation succeeded. Completed checks:

- 192 isolated co-op Play Mode integration checks with four simulated controllers,
  including the exact authored font, independent duck previews for all six colors,
  name clearing on selection, and empty/whitespace-name fallback.
- 129 menu Play Mode checks, including purchases, upgrading, ascension, keyboard
  capture, settings and reset modal behavior.
- 584 index checks across every category and layout, including pagination,
  complete ALL pages, bounds and progression.
- 1,357 card regression checks.
- 1,496 enemy regression checks.
- Original font identity checked at runtime and in the saved scene references.
- Screenshots reviewed for settings, controller bindings, ALL index, customization,
  colored duck previews, yellow/green card states, overhead HUD, Classic HUD and results.

Tests use temporary account saves and restore preferences on Play Mode exit.
Reports/screenshots are under `%TEMP%/duck-coop-verification`.
The final co-op rerun passed. A standard WebGL build also succeeded with **zero
errors and four existing warnings**: the editor Pipeline connection is disabled
in player builds, PlayerController._isCoinShotActive is assigned but unused,
and the two known missing scripts in SampleScene. The first
build exposed a GLES shader precision error in the Classic health palette; using
full precision and shader target 3.0 resolved it, and the rebuild had no shader
errors. The latest build includes the duck previews and automatic name clearing.
Final build output:
`C:/Users/regen/AppData/Local/Temp/duck-coop-web-20261010-120924`.

Static review and source/document/shader whitespace checks passed. Unity-authored
scene YAML retains Unity's normal empty-value trailing spaces. Build-generated
pipeline reference-ID churn was reverted after verifying it changed no settings.
No package, project-setting, original-font or existing metadata changes remain.

## Hands-on Play Mode checklist and limits

1. Connect 2–4 physical controllers. Browse the index, move from a card to its
   upgrade button, and confirm that the yellow border always identifies focus.
2. Open a shop confirmation, try moving/clicking the shop behind it, then cancel.
3. Change controller bindings, restart the game, and verify saved actions and
   current prompts. Test solo unplug/replug and keyboard fallback.
4. Start co-op. Edit names/colors with different controllers simultaneously. Open
   multiple keyboards and verify each controller affects only its own panel.
   Check all six colored duck previews. Selecting Name should clear it; finishing
   without typing (or with only spaces) should restore PLAYER N.
5. Try Classic and Above player health styles. Check names, health colors, shared
   XP, and text at your normal window size/fullscreen resolution.
6. Select/deselect cards with B (or remapped Confirm), including during the final
   all-ready delay. Verify each player receives one upgrade per completed offer.
7. Walk to both player boundaries and confirm enemies arrive from both directions.
8. Die while a teammate survives, respawn next wave, then lose the entire team.
   Verify colored death animations and one team-results screen.

Physical controller drivers, browser-specific gamepad mapping, simultaneous
human input and extended play sessions have not been tested hands-on. Automated
passes do not establish that every possible gameplay interaction is bug-free.
The two previously recorded missing-script warnings in SampleScene remain outside
this update's scope.

## Follow-up: index sidebar controller order

Changed `Assets/Scripts/CardIndexUI.cs` to route all six sidebar buttons in visible
order: Back > Mobility > Munitions > Survival > Gadget > Layout. Category IDs and
their serialized button array remain unchanged. Up follows the reverse order;
navigation stops at the top and bottom. Rightward navigation from Back/Layout
retains geometric entry to the cards or page controls.

Changed `Docs/UPDATE_3_REPORT.md` to record this correction and its verification.
Unity compilation and 312 isolated Play Mode checks passed, covering both
directions, the endpoints, category callbacks, every layout, and normal/host-filter
modes using Unity's actual selection move handlers. Source whitespace checks
passed. No Inspector, scene or prefab setup is required. Physical controller
input was not tested for this follow-up, and the earlier WebGL build predates it.

Hands-on check: open the index and press Down through the full sidebar, then Up
back to Back. Repeat after changing the layout and in the host's Enabled cards
screen; Mobility must always move down to Munitions and Munitions up to Mobility.
