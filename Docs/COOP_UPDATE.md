# Duck Defender local co-op update

Implemented from `Duck Defender Update (2).pdf`, with the user's confirmations to
credit the host account and extend the existing terrain equally left and right.
XP Booster and Sabotage bonuses combine additively; Sabotage retains its existing
95% cap. This was the stated default while that clarification remained unanswered.

## Included behavior

- Three times the horizontal playable width, repeating the existing terrain and
  background art on both sides. Solo camera follow with saved zoom in Settings.
- A shared co-op camera expands to a cap and displays arrows for offscreen players.
  Enemy spawn distance is independent of zoom, with terrain/boundary/view checks.
- Host opens local co-op for 2–4 controllers. Developer-console code `2048` toggles
  the two-player keyboard/mouse + one-controller test mode.
- D-pad/left stick moves; D-pad up or an upward stick flick jumps. Right stick aims,
  RT fires, LB dashes. A narrow horizontal aim snap helps with horizontal shots.
  Shared menus use the first controller; right face/B confirms, bottom face/A backs
  out. Each co-op card pane reads only its assigned controller.
- Enemy wave counts and shared XP targets multiply by player count. Every player
  gets three card choices; the run resumes after everyone selects. Consecutive
  level-ups remain paused between offers. The host's collection supplies card
  levels and unlocks; each player has independent run pickups and upgrades.
- Default player-one artwork, orange player two, blue player three, green player
  four, with matching aim arrows and optional colored overhead health bars.
- Optional next-wave respawns near a survivor preserve upgrades. A disconnected
  controller pauses the run. All players dead opens individual damage, kill,
  healing, card, death and coin results with a bottom-right return button.
- Host options include player count, Easy-by-default difficulty, respawn/color
  toggles and an enabled-card filter. Disabled cards are red and show Disabled in
  the former copies/cost positions. Currency and purchasing controls are hidden.
  Coins credit the host's persistent save and the collecting player's run counter.

## Scene and Inspector setup

The following setup was performed through Unity Editor APIs and saved:

1. `Assets/Scenes/SampleScene.unity`: LocalCoopSession on WaveManager;
   WorldCamera on the camera with authored boundary values; ground/background/
   platform tilemaps expanded from 38 to 114 horizontal ground cells; side
   boundaries and anti-falloff geometry extended; existing background sprites
   repeated left and right.
2. `Assets/Scenes/MainMenu.unity`: HostMenuUI attached to MainMenuUI and wired to
   the existing Host button; the button enabled; solo camera zoom slider and label
   wired into SettingsMenuUI.
3. Unity imported all new scripts and `Assets/Resources/PlayerPalette.shader` and
   generated their `.meta` files. No existing GUIDs were hand-edited. The palette
   shader lives in Resources so it is included in player builds.

No further Inspector or prefab assignment is required for these saved scenes.
Co-op players, per-player turret managers, aim arrows and co-op UI are created at
runtime from the existing player/card/companion assets. No original sprite or
animation artwork was replaced. The co-op card clones override their layout at
runtime; the solo card prefab is unchanged. New labels use white glyph interiors.

`Duck Defender > Co-op > Apply world and menu setup` is the repeatable Editor
setup entry point. Do not remove WorldCamera and rerun it on an expanded scene:
the camera component is the expansion marker. Save open scene edits first.

Serialization additions: SettingsMenuUI.CameraZoomSlider/CameraZoomText,
HostMenuUI.HostButton and the WorldCamera/LocalCoopSession scene components.
Existing serialized field names and project/package/build settings were retained.

## Verification

Completed in Unity 6000.0.35f1:

- Successful C# compilation and palette shader import with no shader errors.
- **127 Play Mode integration checks** with four simulated Input System gamepads
  and an isolated save. Includes host B-button navigation, card filtering, device
  isolation, separate aiming/firing/upgrades/special feathers, disconnect/reconnect,
  host coin persistence, separate card navigation/selection, queued level-ups,
  card bounds/text fit, fourfold enemy count, capped camera/offscreen arrows,
  30 map-edge spawn samples, projectile owner reuse, death/respawn toggles,
  all-dead results, three-player offers, mixed test mode and solo startup.
- **1,357 card/progression/combat checks**, including 10,000 rarity rolls.
- **1,496 enemy checks**.
- **141 late-game regression checks** and **6 additional isolated physics checks**.
- **3,127 checks total** across the suites above.
- Regular WebGL player build succeeded with **zero errors and three warnings**.
  The warnings are the optional Pipeline runtime configuration and two existing
  missing-script references on CardManager and Tilemap_Background. Their working
  gameplay components remain present; those unrelated references were retained.
  The final rebuild after the zoom-label adjustment also succeeded. Its report
  counts one editor-automation error, `Main thread operation timed out after
  5000ms`, from the invoking Pipeline request; it contains no compiler or linker
  errors. Final output: `%TEMP%/duck-coop-web-20261008-230957`.
- Browser smoke testing confirmed main-menu startup, host player-count changes,
  the controller requirement, Enabled/Disabled card filtering and return routing,
  zoom persistence after reload, solo startup, the first wave, jumping and firing. No browser
  console errors occurred. Warnings included the same missing scripts and Unity's
  existing persistent-filesystem synchronization deprecation notice.
- Screenshots reviewed for host options, filter Enabled/Disabled labels,
  four-player card panes and highlights, player colors/health labels, camera
  arrows and results. Layout and palette bugs found during review were corrected.
  Browser review also identified cramped zoom-label spacing; its saved scene
  layout was adjusted below the Keybindings button, with a dark backing for
  readable white text.
- Source/document whitespace check passed. Unity's scene serializer emits spaces
  on empty YAML values; those generated lines were left intact.

The co-op test report and screenshots are written to
`%TEMP%/duck-coop-verification`. Run the checks using
`Duck Defender > Co-op > Run isolated Play Mode verification`. The harness uses
the existing isolated-save mechanism and restores tested preferences on exit.
The WebGL check is under `Duck Defender > Co-op > Verify current WebGL build in
temporary folder`; it requires WebGL already selected and does not publish.
`web-build.txt` records the latest result and temporary build directory.

A development WebGL build was also attempted. C# compiled, but the installed
Unity development TLS library failed native linking with undefined symbol
`unitytls_ssl_set_client_transport_id(unitytls_tlsctx*, unsigned char const*, unsigned long)`.
The regular player build succeeds. No Unity or package upgrade was performed;
development-build verification remains incomplete because of that link failure.

Not covered by simulated-controller checks: physical controller drivers/mappings,
browser gamepad discovery/reconnection, subjective movement/aim feel, prolonged
four-player late-wave performance, or every possible combination of upgrades.
These checks reduce regressions; they are not a guarantee that no bugs remain.

## Play Mode and browser acceptance checklist

1. Open MainMenu, connect 2–4 physical controllers, press a button on each, and
   verify the Host connected count. Check B to select and A to go back.
2. Toggle a host card off/on, confirm red/normal appearance, then start a run.
   Confirm disabled collection cards never appear and shop balances do not change.
3. Move, jump, dash, aim and fire separately with each controller. Check that one
   controller cannot move, fire or choose upgrades for another player.
4. Choose different damage/special-feather/turret cards. Confirm only their owners
   receive them, while XP Booster/Sabotage combine for the team. Earn consecutive
   level-ups and confirm gameplay waits for every player on every offer.
5. Separate players toward both map ends and vertically. Check the zoom cap,
   arrows, terrain seams, boundaries and enemies spawning outside the camera.
6. Kill one player, finish a wave, verify respawn near a survivor with upgrades.
   Repeat with respawns disabled; kill all players and inspect all result columns.
7. Disconnect/reconnect a controller during gameplay and during a card offer.
   Confirm the same seat returns and no choices or upgrades are duplicated.
8. Enter `2048` in the existing developer console and start two players with one
   controller. Check P1 keyboard/mouse and P2 controller. Enter `2048` again to
   restore the normal requirement.
9. Return to solo Play, change Settings camera zoom and restart; verify saved zoom,
   normal XP targets and the original solo HUD. Repeat the controller checks in
   the WebGL build on the intended browsers. Run a long four-player late-wave
   session to assess performance and difficulty.

## Changed files and reasons

Paths below are relative to the project root. Every new Unity asset also has its
adjacent Unity-generated `.meta`, listed explicitly in the final table.

| File | Reason |
| --- | --- |
| Assets/Scenes/MainMenu.unity | Saved Host button/component and camera zoom wiring. |
| Assets/Scenes/SampleScene.unity | Saved expanded terrain/boundaries/background and session/camera components. |
| Assets/Resources/PlayerPalette.shader | Replace duck body colors without modifying source artwork. |
| Assets/Scripts/LocalPlayer.cs | Player identity, device ownership, aim/jump state, palette and result counters. |
| Assets/Scripts/LocalCoopSession.cs | Player/turret creation, controller availability, team modifiers, death and respawn. |
| Assets/Scripts/WorldCamera.cs | Bounded follow/zoom and legal offscreen terrain spawns. |
| Assets/Scripts/CoopRunUI.cs | Health/arrows, independent card panes, results and shared UI helpers. |
| Assets/Scripts/HostMenuUI.cs | Local game options, device requirements and host card filtering. |
| Assets/Scripts/ControllerMenuNavigation.cs | Host controller navigation without duplicate EventSystem submissions. |
| Assets/Scripts/Inputhelper.cs | Route existing input calls through each player's assigned device. |
| Assets/Scripts/PlayerController.cs | Own stats/input/effects and reset movement state after respawn. |
| Assets/Scripts/WeaponPlayer.cs | Own weapon stats/input and source attribution on every projectile path. |
| Assets/Scripts/PlayerHealth.cs | Own health modifiers, healing count, co-op death and respawn. |
| Assets/Scripts/PlayerStats.cs | Player-one singleton compatibility, individual wave coins and team slow queries. |
| Assets/Scripts/Playeranimator.cs | Independent shoot input, singleton guard and animation revival. |
| Assets/Scripts/CardManager.cs | Independent per-player run history/stat application, special feathers and host exclusions. |
| Assets/Scripts/LevelUpUI.cs | Coordinate co-op offers and preserve queued level-up pauses. |
| Assets/Scripts/LevelManager.cs | Scaled XP targets, recipient rewards, host saving and individual wave income. |
| Assets/Scripts/WaveManager.cs | Scale wave plans/capacity, respawn between waves and retry legal world spawns. |
| Assets/Scripts/EnemyBase.cs | Living-player targeting, team Sabotage, attributed damage/deaths and status effects. |
| Assets/Scripts/TankEnemy.cs | Preserve damage source through redirected tank damage. |
| Assets/Scripts/BuzzerEnemy.cs | Crash attacks affect all living players in range. |
| Assets/Scripts/Projectile.cs | Owner-specific upgrades, healing, damage and pooled reuse. |
| Assets/Scripts/AuraController.cs | Damage aura follows/uses its own player's stats and aim. |
| Assets/Scripts/SlowingAuraController.cs | Own slowing aura; hide it while its player is dead. |
| Assets/Scripts/AscensionEffects.cs | Own ascension input, damage/healing and spawned-area attribution. |
| Assets/Scripts/AscensionArea.cs | Preserve area owner for damage/healing. |
| Assets/Scripts/Meteor.cs | Own damage and secondary meteor coin attribution. |
| Assets/Scripts/FireTrailPatch.cs | Fire trail damage uses the originating player's build. |
| Assets/Scripts/Coin.cs | Living collector selection, personal magnet/rewards and source-safe merging. |
| Assets/Scripts/HealingOrb.cs | Seek/heal the living player who actually collects the orb. |
| Assets/Scripts/XP&Gems.cs | Seek living players and guard against duplicate co-op pickup. |
| Assets/Scripts/TurretBase.cs | Configure owner stats/health and suspend actions for dead owners. |
| Assets/Scripts/TurretManager.cs | Per-player upgrade subscriptions and companion spawning. |
| Assets/Scripts/MarksmanTurret.cs | Owner stats and projectile attribution. |
| Assets/Scripts/MedicTurret.cs | Heal its own player using their upgrades. |
| Assets/Scripts/ProtectorTurret.cs | Use its owner's protection settings. |
| Assets/Scripts/ElementalTurret.cs | Use its owner's elemental settings and attributed damage. |
| Assets/Scripts/SecondWindAngel.cs | Read its owner's Second Wind/Rebirth status. |
| Assets/Scripts/MainMenuUI.cs | Route Host/filter navigation and panel visibility. |
| Assets/Scripts/MenuController.cs | Ordinary Play resets to solo. |
| Assets/Scripts/CardIndexUI.cs | Host eligibility filter and hidden currency counters. |
| Assets/Scripts/CardDisplay.cs | Enabled/Disabled toggles, red disabled cards and white status labels. |
| Assets/Scripts/SettingsMenuUI.cs | Persist/bind solo camera zoom. |
| Assets/Scripts/DeveloperConsoleUI.cs | Toggle mixed keyboard/controller mode with code 2048. |
| Assets/Scripts/EnemyTipUI.cs | Controller B dismissal and white continuation text. |
| Assets/Scripts/CoopVerificationProbe.cs | Editor-only isolated Play Mode controller/gameplay/UI checks. |
| Assets/Scripts/Editor/CoopUpdateSetup.cs | Repeatable Unity API scene/menu authoring. |
| Assets/Scripts/Editor/CoopUpdateVerification.cs | Verification entry points, cleanup and temporary WebGL build. |
| Assets/Scripts/Editor/LateGameFixVerification.cs | Populate the existing spawn-plan fixture before testing its queue. |
| Docs/ARCHITECTURE.md | Document implemented ownership, input, UI, economy, camera and build checks. |
| Docs/COOP_UPDATE.md | Completion report, setup details, file inventory and acceptance checklist. |

| New Unity-generated metadata | Reason |
| --- | --- |
| Assets/Resources/PlayerPalette.shader.meta | Shader asset GUID/import settings. |
| Assets/Scripts/LocalPlayer.cs.meta | Script GUID/import settings. |
| Assets/Scripts/LocalCoopSession.cs.meta | Script GUID/import settings. |
| Assets/Scripts/WorldCamera.cs.meta | Script GUID/import settings. |
| Assets/Scripts/CoopRunUI.cs.meta | Script GUID/import settings. |
| Assets/Scripts/HostMenuUI.cs.meta | Script GUID/import settings. |
| Assets/Scripts/ControllerMenuNavigation.cs.meta | Script GUID/import settings. |
| Assets/Scripts/CoopVerificationProbe.cs.meta | Script GUID/import settings. |
| Assets/Scripts/Editor/CoopUpdateSetup.cs.meta | Script GUID/import settings. |
| Assets/Scripts/Editor/CoopUpdateVerification.cs.meta | Script GUID/import settings. |

The four pre-existing deletions of PerformanceTestRunInfo/Settings JSON and their
metadata were preserved and are not part of this implementation. No commit,
push, deployment, package upgrade or player-setting change was performed.
