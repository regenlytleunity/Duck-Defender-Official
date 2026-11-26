# Guided tutorial implementation

Implemented from `Duck Defender Update (7).pdf` (three pages), with the refinements
in `Duck Defender Update (8).pdf` (one page) and Updates (9) through (12). These documents supply the gameplay
outline; repository rules govern implementation and Unity assets.

## Behavior and recovery

- A fresh save automatically enters practice. Existing version-2 saves keep their
  coins/cards and skip the core tutorial. Reset Game Data creates a fresh tutorial.
- Practice teaches movement, then jumping, then aiming and shooting together.
  Touching both pads with the player collider, jumping onto the top of the box
  and destroying all five targets with feathers complete the objectives. Instructions reflect the current keyboard/controller bindings and
  move/aim stick assignment. Touch practice instructions use the existing sticks.
- Walking and box-landing goals fade upward from green to transparent. Five
  dartboards appear farther from the player for aiming and shooting. These visuals
  are generated once at runtime; there are no new serialized textures or sprites.
- Practice has no **Restart step** button or Back shortcut. Falling below or
  wandering outside the area returns the player to its origin and retains completed
  objectives. Closing the game still restarts stage 1 from its saved checkpoint.
- Two existing ground-enemy prefabs enter from outside the camera and move toward
  fixed stop points, then freeze. They cannot take damage during the approach.
  They have 2 HP, give exactly half a level each and drop their usual coins.
  Coins fly to the duck from any distance and the coin HUD appears below XP. All three Basic
  offers can be focused/hovered; only Health can be activated. XP and health HUDs
  appear at their lessons. Sibling decorations are hidden with the locked HUD.
- Finishing practice awards 200 coins in addition to collected drops. The reward
  and Shop checkpoint are saved together. Saves at the retired Play and Waves
  checkpoints receive the same reward once and resume at Shop. Practice does
  not count toward normal-run item-drop eligibility.
- Menu controls and underlying purchase/equipment methods both enforce the
  tutorial restrictions. The guaranteed pack has two copies of one starter and
  one previously locked Common in the selected pack. Pack purchase and the later
  upgrade each cost 100 with the current assets. Unrelated purchases cannot spend
  that budget. Card grants, charge and checkpoint save in the same write.
- Quitting before slicing a paid pack resumes its opening without a second charge.
  Quitting during/after reveal preserves all cards and resumes the index lesson.
  The index initially selects the purchased pack, then allows all pack tabs, page
  arrows, layouts and card previews. A soft green underglow directs the player back to
  the required pack/page/card. Only that upgrade can spend coins; the action itself
  advances the lesson without an extra confirmation. Upgrade and completion save together.
- Tutorial prompts, completed objectives, pack reveals, completion banners and
  protected-run enemy tips require Interact. Prompts display the mapped controller
  button, Enter for keyboard, or a touch hint, plus a Continue button outlined by the shared yellow selection frame. A short
  input grace and release-before-press prevent carryover, with menu submission
  suppressed while informational prompts own Interact. Learned movement/jump
  controls remain available during later practice lessons. Enemy approaches and
  completed purchase/upgrade actions advance their own checkpoints directly.
- Movement goals and the box landing glow both fade after completion. Tutorial
  card/button guidance uses a soft green underglow, separate from yellow
  controller focus. Shop guidance follows the chosen pack instead of leaving a
  second focus frame on the first one. The controller aim arrow is a closed, consistently outlined silhouette.
- Speech-box prompts use one fixed top position with a 76%-opaque background.
  The existing duck is 44% of canvas height, anchored at (94%, 53%), so its complete
  head and eye remain onscreen.
  Full text is laid out before the typewriter reveal; the duck bobs three pixels while
  speaking. After a 5–11 second reading window, the bubble collapses right toward the duck
  over 0.2 seconds, then both hide. Bubble and tail retain matching 76% opacity
  throughout the collapse; the tail outline has no opaque backing triangle.
  Completing an objective reopens it; Show tutorial (T or controller Y by default)
  restores it without advancing. A first Interact press on a hidden completed prompt
  also restores it. The popup stays in the same position in every menu.
  New messages restore the normal background color after a
  chromatic completion banner, including the subsequent item tutorial.
- A bottom-right Skip tutorial button opens a paused confirmation modal with Keep
  playing selected. Controller X opens it directly; holding the keyboard Show
  tutorial key for 0.85 seconds also opens the modal. B cancels; A confirms the
  selected option. Controller Y only recalls text, even when held.
  Confirming preserves earned coins/cards/items, skips both tutorial tracks without
  granting rewards, and returns to MainMenu. Cancel resumes the current step.
- First item purchase starts the item lesson in the shop; first pickup waits until
  MainMenu. Items stays hidden in the index until acquisition, with sidebar spacing
  adjusted when it appears. Owned items are highlighted; any of four solo slots is
  valid. Selling and unequipping are blocked until the controls lesson is finished.
  A legacy acquired-item checkpoint with empty inventory receives one Bomb when
  reaching Equip so it has an item to teach with. An already-equipped valid item
  resumes at the controls explanation.
- The wheel is available only if the run started with an equipped item. Acquiring
  an inventory item mid-run does not turn an empty wheel on. Normal gameplay
  resumes its ordinary health, waves, economy and equipment behavior afterward.

Checkpoints use the existing player save. Ordinary cached economy/tip writes
preserve newer tutorial progress. Practice objectives are intentionally ephemeral:
closing the application during stage 1 restarts practice from Welcome. Finishing
practice or migrating an old second-stage save resumes the shop lesson.

## Unity setup

No additional Unity Editor, Inspector, scene or prefab setup is required for the
current MainMenu and SampleScene. The director, prompts, practice markers, box and
targets are created at runtime. No serialized assets or ProjectSettings were edited.

The implementation reuses the assigned ground enemy prefab, Basic card assets,
shop packs, existing UI controls and `Assets/Fonts/DuckDefenderTestFontv2.asset`.
The serialized-reference check verifies those dependencies in the existing scenes.
Unity generated the metadata for the tutorial scripts, including the Update (10) dialogue partial.

The simple stick illustration, markers and box are functional runtime geometry.
Artist-authored visuals and device/resolution-specific visual tuning remain the
user's responsibility. The solo health and XP bars share the longer classic silhouette, sit farther below
the top edge, and retain white text. XP uses blue fill and no orb decoration.

## Original tutorial implementation files

All paths below are relative to the repository root.

| File | Reason |
| --- | --- |
| `Assets/Scripts/TutorialProgress.cs` | Serializable core/item checkpoints, legacy compatibility, one-time reward. |
| `Assets/Scripts/TutorialDirector.cs` | Scene bootstrap, prompts, input gates, pause ownership, stage restart and reward transition. |
| `Assets/Scripts/TutorialDirector.Dialogue.cs` | Speech box, fixed-layout text reveal, duck portrait, collapse/continue controls and rejected-action feedback. |
| `Assets/Scripts/TutorialDirector.Practice.cs` | Practice objectives, runtime geometry/enemies, exact XP and forced card lesson. |
| `Assets/Scripts/TutorialDirector.Menus.cs` | Guided menus, safe button gating, highlights and item lesson. |
| `Assets/Scripts/TutorialTarget.cs` | Actual feather hits notify the shooting objective. |
| `Assets/Scripts/ShopManager.Tutorial.cs` | Guaranteed pack transaction and shared service-level restrictions. |
| `Assets/Scripts/Editor/TutorialVerification.cs` | Isolated persistence/economy and serialized-reference checks. |
| `Assets/Scripts/TutorialPlayProbe.cs` | Editor-only isolated controller, interruption and gameplay probe. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Existing UI/co-op verification starts from a completed tutorial; explicit fresh-tutorial option. |
| `Assets/Scripts/Editor/ShopUpdateVerification.cs` | Seed completed tutorial state for ordinary shop/equipment regression checks. |
| `Assets/Scripts/Editor/CardReworkVerification.cs` | Seed completed tutorial state for ordinary upgrade/developer-code checks; set the existing bulk unlock prerequisite in triple-pack fixtures. |
| `Assets/Scripts/PlayerData.cs` | Add tutorial data; fresh profiles opt in, old profiles normalize safely. |
| `Assets/Scripts/SaveSystem.cs` | Preserve independently updated checkpoints during cached saves. |
| `Assets/Scripts/ShopManager.cs` | Guard purchases/upgrades, atomically advance the required upgrade, reset tutorial with a new save. |
| `Assets/Scripts/ShopManager.Upgrades.cs` | Guard alternate spending/equipment; record acquisition and equip checkpoint. |
| `Assets/Scripts/MainMenuUI.cs` | Enforce panel routes, resume paid packs, save reveal checkpoint and focus tutorial prompts. |
| `Assets/Scripts/MenuController.cs` | Prevent starting a run from a locked menu route. |
| `Assets/Scripts/HostMenuUI.cs` | Prevent hosting during the guided solo tutorial. |
| `Assets/Scripts/CardIndexUI.cs` | Locate the required card's page and prevent switching away from its pack. |
| `Assets/Scripts/IndexLoadoutUI.cs` | Hide/reveal Items, adjust sidebar spacing and guard the tab. |
| `Assets/Scripts/LoadoutPanel.cs` | Guard context menus, highlight owned tutorial items and disable Sell. |
| `Assets/Scripts/Inputhelper.cs` | Apply tutorial gates within the existing input abstraction. |
| `Assets/Scripts/LocalPlayer.cs` | Reveal controller aim indicator only when aiming is taught. |
| `Assets/Scripts/PlayerHealth.cs` | Clamp damage to leave 1 HP during protected tutorial runs. |
| `Assets/Scripts/LocalCoopSession.cs` | Enforce one player/no loadouts or drops during core lessons; preserve pause and normal-run accounting. |
| `Assets/Scripts/WaveManager.cs` | Hold tutorial wave start and stop after wave 5. |
| `Assets/Scripts/LevelManager.cs` | Award exact tutorial XP without ordinary multipliers. |
| `Assets/Scripts/LevelUpUI.cs` | Offer the three Basic cards, enforce Health and preserve pause ownership. |
| `Assets/Scripts/EnemyBase.cs` | Configure/freeze practice enemies and bypass ordinary death rewards. |
| `Assets/Scripts/Projectile.cs` | Recognize practice targets before ordinary enemy handling. |
| `Assets/Scripts/EnemyTipUI.cs` | Suppress practice tips and preserve tutorial-owned pauses. |
| `Assets/Scripts/PlayerItems.cs` | Gate use and record whether a run began with equipped items. |
| `Assets/Scripts/ItemPickup.cs` | Suppress core drops and persist first acquisition. |
| `Assets/Scripts/ItemWheelUI.cs` | Hide wheel/touch item controls for runs without starting items. |
| `Assets/Scripts/GameUI.cs` | Classic numeric solo health, tutorial HUD decoration visibility and wave x/5 text. |
| `Docs/ARCHITECTURE.md` | Document the implemented tutorial/save integration. |
| `Docs/TUTORIAL_IMPLEMENTATION.md` | Behavior, recovery, file manifest, setup and verification checklist. |

New Unity-generated metadata: `Assets/Scripts/TutorialProgress.cs.meta`,
`Assets/Scripts/TutorialDirector.cs.meta`, `Assets/Scripts/TutorialDirector.Practice.cs.meta`,
`Assets/Scripts/TutorialDirector.Menus.cs.meta`, `Assets/Scripts/TutorialDirector.Dialogue.cs.meta`, `Assets/Scripts/TutorialTarget.cs.meta`,
`Assets/Scripts/ShopManager.Tutorial.cs.meta`, `Assets/Scripts/TutorialPlayProbe.cs.meta`,
`Assets/Scripts/Editor/TutorialVerification.cs.meta`. Existing metadata was preserved.

### Update (8) changed files

This follow-up changes these files from the original tutorial implementation:

| File | Reason |
| --- | --- |
| `Assets/Scripts/TutorialDirector.cs` | Timed automatic transitions, remove Restart step/Continue controls, reset prompt color, and place prompts clear of current UI. |
| `Assets/Scripts/TutorialDirector.Practice.cs` | Fading green goals, farther dartboards, off-screen enemy starts, retain partial progress during fall recovery, and allow card focus. |
| `Assets/Scripts/TutorialDirector.Menus.cs` | Automatically advance the pack reveal and index explanations while retaining required player actions. |
| `Assets/Scripts/EnemyBase.cs` | Bound practice movement to stop points and prevent damage during arrival. |
| `Assets/Scripts/WaveManager.cs` | Halve tutorial spawn plans before initializing wave counters; preserve introductions. |
| `Assets/Scripts/LevelUpUI.cs` | Allow inspection of all three cards while retaining the Health-only activation guard. |
| `Assets/Scripts/GameUI.cs` | Hide locked HUD elements and sibling icons before practice renders. |
| `Assets/Scripts/MainMenuUI.cs` | Expose reveal completion and hide its manual Continue button during the core tutorial. |
| `Assets/Scripts/EnemyTipUI.cs` | Advance enemy explanations automatically only in protected tutorial runs. |
| `Assets/Scripts/Editor/TutorialVerification.cs` | Check normal wave totals, odd-count rounding and introduction retention. |
| `Assets/Scripts/TutorialPlayProbe.cs` | Exercise automatic progression, actual controller focus/activation, hover inspection, enemy stops, half waves and color reset. |
| `Docs/ARCHITECTURE.md` | Update the current tutorial behavior. |
| `Docs/TUTORIAL_IMPLEMENTATION.md` | Record the refinements, file manifest and verification checklist. |

No additional scripts, serialized fields, assets or `.meta` files are introduced
by Update (8). No Inspector, scene or prefab setup is required.

## Verification and limitations

Verification uses temporary saves and restores tested input preferences when Play
Mode exits. The real player save is not used. Results are generated under
`tmp/tutorial/` when running the checks.

From the Unity menu, run **Duck Defender > Tutorial > Verify save and economy
safeguards** outside Play Mode. For the automated playthrough, run **Duck Defender
> Tutorial > Run isolated Play Mode verification** from a clean saved scene.
Wait for the PASS/FAILED Console message, then exit Play Mode to restore input
preferences. The probe uses a virtual controller and temporarily disables real
gamepads; it restores them when finished. The existing UI, shop and co-op check
commands use completed tutorials, so onboarding does not hijack their fixtures.

- Static review of the modified owners and tutorial state transitions.
- Unity 6000.0.35f1 compilation in the already-running Editor.
- On November 4, 2026, 65 isolated save/economy/serialized-reference assertions
  passed, including all four packs, cached saves, repeated reward/purchase/upgrade
  calls, item restrictions, unchanged normal wave totals and reduced-wave rounding.
- All 128 automated Play Mode checks passed in the final run. They exercise actual
  controller input for movement, jumping, aiming, shooting, card navigation and
  menu submissions, plus off-screen arrivals/stopping, half waves, automatic tips
  and progression, interruption recovery and normal-run behavior afterward.
  Combat/wave kills use existing damage methods to accelerate testing; this is
  not a manual combat run. The Editor Console reported no errors for the run.
  It still reports missing-script warnings on `SampleScene/Tilemap_Background`
  and `SampleScene/CardManager` (one missing component each); these serialized
  scene objects were not changed by this tutorial update.
- Final 1920×1080 game-view screenshots were inspected for gradients, dartboards,
  HUD visibility, prompt/font placement, card/reveal clearance, shop prices,
  index pagination, purchase controls and the item prompt's restored color.
- During the original implementation, the existing regressions passed 1,357 card
  checks, 79 ordinary shop economy checks and 13 item/developer-code checks. Those
  broader suites were not rerun for Update (8).

The latest generated verification artifacts are `tmp/tutorial/verification.txt`,
`tmp/tutorial/play-verification.txt`, and the screenshots `welcome.png`,
`movement.png`, `jump.png`, `aim.png`, `cards.png`, `health.png`,
`confirmation.png`, `pack.png`, `index.png`, and `item.png` in `tmp/tutorial/`.
They record test results and the views inspected above.

Manual hardware controller testing, keyboard/mouse/touch end-to-end testing, broad
resolution/aspect-ratio testing, release builds and browser persistence were not
verified. The existing SaveSystem writes and WebGL IndexedDB synchronization are
retained; abrupt storage failure or closing before browser synchronization completes
is outside the verified checkpoint guarantees. Missing/changed game assets still
require fixing their assignments; the current required references were checked.

## Play Mode checklist

For Update (12), use the checklist and file manifest at the end of this document.
Earlier update reports record the behavior and verification for their respective versions.

### Update (10) original changes and manual checks (historical)

- `TutorialDirector.cs`: shared prompt timing, fresh-input protection and dialogue delegation.
- `TutorialDirector.Dialogue.cs` and Unity-generated `.meta`: speech-box layout, fixed-size circular stick preview, authored duck portrait, prelaid-out typewriter text, three-second linger/collapse, clickable Continue, and a 0.18-second four-pixel rejection shake with restoration.
- `TutorialDirector.Menus.cs`: four-sided green guidance inset five canvas units from the target, with three-unit edges and no raycast blocking.
- `TutorialDirector.Practice.cs`: farther raised target semicircle, changed-aim requirement, colored hit bursts and live binding text routed through the reveal layout.
- `LocalPlayer.cs`: one closed outlined arrow, with per-player color and facing compensation.
- `GameUI.cs`: longer/lower solo health and XP, health-style XP art and blue fill, no XP orb.
- `CoopRunUI.cs`: lower shared XP and longer/lower classic co-op health bars.
- `LevelUpUI.cs`: rejection feedback when a different Basic card is activated.
- `TutorialPlayProbe.cs`: isolated controller regression checks plus dialogue layout, collapse, circle dimensions, idle aim, bursts, shake restoration and HUD checks; output in `tmp/update10/`.
- `Docs/ARCHITECTURE.md` and this document: current behavior and verification guidance.

No scene/prefab edits or new Inspector assignments are needed. Menu portraits use
HostMenuUI.DuckPreviewSprite; gameplay portraits use the configured player's SpriteRenderer.
The portrait stays halfway off the right edge, centered at 53% of screen height,
and measures 58% of the canvas height. It retains that size on card offers, index
pages and collapsed dialogue; overlap with underlying UI is intentional.
The exact existing DuckDefenderTestFontv2 asset/material remains in use.

Manual Play Mode checklist:

1. Run practice using controller, then keyboard/mouse or touch. Check circular stick
   motion, stable text wrapping, gentle duck bob, three-second linger and collapse.
   The large duck should remain about half outside the right edge throughout.
2. Click the duck to reread, finish an objective, and use both Continue and the mapped
   Interact input. Holding Interact must not skip multiple prompts.
3. Leave aim neutral: no target should turn green. Aim at all five raised targets,
   then shoot them and check the small red/green/cream/dark bursts.
4. Hover/focus all three Basic cards. Try Damage/Mobility: expect a brief shake and
   no upgrade. Choose Health to continue. Check inset green borders on menu controls.
5. Check health and XP at partial/full values in solo and co-op. XP has no orb, uses
   the health-bar shape and blue fill, and all labels retain white interiors.
6. Check shop confirmation, pack reveal, index/card modal and item equipment at the
   intended display resolutions for readable text and reachable controls. Duck/UI
   overlap at the right edge is intentional.

### Update (10) verification

- Unity 6000.0.35f1 compiled successfully. Static review and `git diff --check` passed.
  The final run produced no new Console errors. Play Mode was stopped afterward;
  MainMenu is clean, temporary save routing is cleared and input preferences restored.
- The isolated Play Mode regression passed 212 checks, including a fresh run after
  the November 21 portrait-size follow-up. It exercises tutorial progression, fresh
  controller input and pointer
  Continue, text layout/reveal/collapse, target spacing and hit particles, arrow
  directions with either facing, rejection feedback, HUD visibility, shop/index
  progression and normal gameplay after the tutorial. Enemy kills are accelerated
  through existing damage methods; this is not a manual combat playthrough.
- A separate two-player classic co-op HUD check passed, including the longer/lower
  health bars, half-full blue shared XP and removed orb.
- The original UI passes reviewed welcome, movement, aiming, cards, health, shop,
  confirmation, pack reveal, index and item screens at 1920 by 1080. Corrections
  included speech-tail rendering, Continue text clearance and flipped aim angles.
- The November 21 portrait follow-up changes `TutorialDirector.Dialogue.cs` to
  retain the enlarged, half-offscreen portrait across gameplay, menus and collapsed
  prompts. `Docs/ARCHITECTURE.md` and this file document that behavior. At 1920 by
  1080, its runtime rectangle is 626.4 pixels square, from x=1606.8 to x=2233.2;
  exactly half lies beyond the right edge. Fresh welcome and card-offer captures
  show the requested crop and intentional UI overlap.
- Existing uGUI was retained. The installed package's `class-RectTransform.md`
  and `UIBasicLayout.md` were consulted for sizing and anchors. No packages,
  serialized fields, scenes, prefabs or ProjectSettings were changed.

Verification reports and captures are in `tmp/update10/`. Manual physical-device
controller tests, end-to-end keyboard/mouse/touch runs, broad aspect-ratio checks,
standalone/WebGL builds and browser persistence remain unverified. No new Inspector,
scene or prefab setup is required.

### November 22 popup and skip refinement

This supersedes the historical Update (10) popup placement, portrait crop, collapsed
reminder and inset highlights above. Runtime uGUI remains the existing UI framework;
the authored DuckDefenderTestFontv2 asset/material and white glyph infill are retained.

- The duck is now 44% of canvas height at (94%, 53%); its full head and eye fit onscreen.
- Every popup uses anchors (22%, 77%) to (80%, 98%). It never relocates for menus.
- Backdrop simulations at 62%, 76% and 86% opacity selected 76% to retain visibility
  of the underlying UI and readable white text with a small dark shadow. The old
  full-panel Outline effect was removed because its duplicate quads defeated transparency.
- Full text is still laid out before the 42-character/second reveal. The reading
  window after reveal is `clamp(2 + characterCount / 22, 5, 11)` seconds, followed
  by a 0.2-second fade. The whole popup and duck then hide until completion or recall.
- Show tutorial uses T / View-Select by default and is remappable in the existing
  keyboard/controller settings. A pointer/touch button provides the same action.
  Recall shows complete text and restarts its reading window without advancing,
  including the final completion message.
- Bottom-right Skip tutorial opens a paused confirmation, also accessible by
  holding Show tutorial for 0.85 seconds. Corner controls remain above enemy-tip
  click blockers. Keep playing is selected by default;
  opening/held input cannot confirm. Cancel restores the current step and pause
  state. Confirm preserves earned resources, marks core and item lessons complete
  without awarding tutorial rewards, and returns to MainMenu.
- Soft green glows are siblings behind the target graphic, excluded from layout
  and raycasts. Transparent pack click areas resolve to their visible icon so the
  glow follows the artwork. Yellow controller focus remains independent.

Timing examples from the implemented character-count rule (including reveal and fade):

| Text length | Reveal | Fully readable window | Time until completely hidden |
| --- | --- | --- | --- |
| 60 characters | 1.43 s | 5.00 s | 6.63 s |
| 130 characters | 3.10 s | 7.91 s | 11.20 s |
| 220 characters | 5.24 s | 11.00 s | 16.44 s |

These are calculated timing scenarios; the Play Mode probe separately waits for real
automatic hiding and uses the controller recall input. Holding recall opens confirmation;
it never directly skips.

Files changed in this refinement (paths relative to the repository root):

| File | Purpose |
| --- | --- |
| `Assets/Scripts/TutorialDirector.cs` | Update hidden-prompt lifecycle and pause/input ownership for confirmation. |
| `Assets/Scripts/TutorialDirector.Dialogue.cs` | Fixed translucent popup, readable text, full hide/recall, head-visible portrait. |
| `Assets/Scripts/TutorialDirector.Controls.cs` and Unity-generated `.meta` | Runtime recall/skip controls and guarded modal/save/return behavior. |
| `Assets/Scripts/TutorialDirector.Menus.cs` | Prevent navigation under the skip modal and draw/follow glows behind actual graphics. |
| `Assets/Scripts/LocalCoopSession.cs` | Remember tutorial-run eligibility so skipping during scene teardown cannot increment normal completed runs. |
| `Assets/Scripts/InputManager.cs` | Persist/reset the keyboard Show tutorial binding without adding a serialized field. |
| `Assets/Scripts/ControllerBindings.cs` | Add Show tutorial to existing controller mappings and prevent binding conflicts. |
| `Assets/Scripts/SettingsMenuUI.cs` | Expose Show tutorial in keyboard settings. |
| `Assets/Scripts/ControllerBindingsUI.cs` | Fit the added keyboard/controller actions in existing panels. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Restore the new keyboard preference after isolated tests. |
| `Assets/Scripts/TutorialPlayProbe.cs` | Verify full hide, recall, modal cancellation, behind-graphic highlights and skip persistence across checkpoints. |
| `Docs/ARCHITECTURE.md` and this document | Describe the current behavior, setup, verification and checks. |

No serialized assets, Inspector fields, scenes, prefabs, settings or packages were
changed. Unity generated the new Controls partial's `.meta`. No additional Editor
setup is required. Backups, simulations and regression artifacts use
`tmp/tutorial-refinement/`; `tmp/update10/` retains the earlier version's captures.

Verification completed November 22, 2026:

- Unity 6000.0.35f1 compiled successfully. The final isolated Play Mode run passed
  **250 checks**, with no new Unity console errors. The full results are in
  `tmp/tutorial-refinement/play-verification.txt`.
- Actual virtual-controller events verified timed hiding, recall without advancing,
  hold-to-open confirmation, safe default cancellation, and completion-message recall.
  Pointer raycasts/click handlers verified both skip buttons at six checkpoints:
  practice, waves, shop, purchased pack, upgrade, and the item lesson.
- Skip persistence retained 432 coins, two completed normal runs, the existing
  level-three card, and two bombs in every checkpoint fixture. No tutorial reward
  was granted by skipping. Pause, practice and menu gates were released.
- Keyboard/controller rebinding, saved values, collision rejection and Restore
  defaults passed. Pointer checks reached all 20 keyboard-panel controls and all
  16 controller-panel controls after layout settled. Captures verify that the added
  rows and Restore defaults do not overlap the status text.
- Visual inspection at 1920 by 1080 covered the complete head/eye, fixed translucent
  prompt, full hidden state, skip confirmation, behind-art glows, and binding panels.
  The three opacity comparisons and final captures are in `tmp/tutorial-refinement/`.
- The probe now retains the authored three-second inter-wave gap. Its previous
  0.02-second override could let the next test wave start before the old spawn
  coroutine finished; that test-only acceleration produced an exception in one
  earlier run. No WaveManager or authored wave settings were changed.
- Static diff checks passed. No Inspector, scene or prefab assignments are required.
  The Editor was returned to clean MainMenu Edit Mode, background execution was
  restored to false, the preference snapshot was restored/cleared, and the isolated
  save override was cleared.
  Physical-controller/touch checks, a complete keyboard/mouse run, broader aspect
  ratios, standalone/WebGL builds and browser persistence remain unverified.

Manual Play Mode checks:

1. Start a fresh tutorial. Confirm the full head/eye is visible and the popup stays
   in one position during practice, card offers, shop, pack reveal and index.
2. Read a short and a long message. Confirm text remains readable, does not reflow,
   and fully fades away after its reading window. Check that gameplay/UI behind it
   remains visible and usable as intended.
3. Press T or View/Select, or click Show tutorial: full text returns without skipping
   a step. Complete an objective while hidden: the prompt returns automatically.
4. Rebind Show tutorial in keyboard/controller settings; verify recall, displayed
   hints, persistence and Restore defaults.
5. Open Skip tutorial by click/tap and by holding the recall binding. Keep playing
   must be selected first. Cancel with the button or Back/Escape and resume the same step.
6. Confirm skip from practice, a wave, the shop, a purchased pack and the item lesson.
   Verify MainMenu is usable, earned inventory/coins remain, no reward is added,
   and reloading does not restart either tutorial track.
7. Check green glows behind menu buttons, packs, required Health and owned items;
   they should follow their graphic and never obscure text or intercept clicks.
8. Repeat on physical controller/touch hardware and target aspect ratios. Release
   builds and those hardware/resolution checks are not covered by the automated probe.


## Update (11): controller menus and tutorial presentation

Source: `Duck Defender Update (11).pdf`, two pages. Its numbered item 5 is the
controller reference image; there is no separate text requirement for that item.

Implemented in the existing uGUI runtime owners, on Unity 6000.0.35f1. No package,
ProjectSettings, serialized field, scene, prefab, font, sprite or metadata changes
were required. Existing uncommitted work was retained.

Files changed for this update (paths relative to the project):

| File | Reason |
| --- | --- |
| `Assets/Scripts/ControllerBindings.cs` | A/B defaults, Y/X tutorial shortcuts, one-time default migration, conflict checks and enabled-controller selection. |
| `Assets/Scripts/ControllerBindingsUI.cs` | Logitech-style controller geometry, live binding pathways, focus feedback and Back cancellation during capture. |
| `Assets/Scripts/ControllerMenuNavigation.cs` | Include sliders/toggles in navigation; require a double Interact press within 0.4 seconds to edit sliders. |
| `Assets/Scripts/SettingsMenuUI.cs` | Explain slider editing, rebuild modal navigation and support controller Back during keyboard capture. |
| `Assets/Scripts/TutorialDirector.Controls.cs` | X opens skip confirmation; Y recalls text without a hold-to-skip action. Keyboard hold behavior remains. |
| `Assets/Scripts/TutorialDirector.Dialogue.cs` | Shared yellow Continue frame, rightward bubble collapse and translucent tail geometry. |
| `Assets/Scripts/TutorialDirector.cs` | Update and dispose the Continue frame with the existing prompt lifecycle. |
| `Assets/Scripts/TutorialPlayProbe.cs` | New A/X/Y inputs, collapse/tail checks and separate Update (11) capture output. |
| `Assets/Scripts/CoopVerificationProbe.cs` | Use A to confirm and a free button for the remapping check. |
| `Assets/Scripts/ShopUpdatePlayProbe.cs` | Use A/B defaults and View/Select for the remapped Interact check. |
| `Assets/Scripts/ItemUpdatePlayProbe.cs` | Use a free button for item-remapping checks, respecting X as Skip tutorial. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Snapshot/restore the new menu-binding migration preference in isolated tests. |
| `Docs/ARCHITECTURE.md` | Document the current bindings, diagram, slider interaction and tutorial presentation. |
| `Docs/TUTORIAL_IMPLEMENTATION.md` | Record this update, its setup requirements, changed files and verification. |

Verification artifacts are under `tmp/update11/`: source-page renders, before-edit
backups, an external live-Editor controller check, screenshots and test reports.
The external check is not imported into the game and creates no Unity asset.

No additional Inspector or scene/prefab setup is required. In Play Mode, check:

1. Navigate Settings with the D-pad and left stick; reach each volume slider,
   camera zoom, Show tips and Particles. A changes either toggle.
2. On a slider, one A press leaves navigation active. Two quick A presses enable
   adjustment (green frame); the left stick changes its value. A or B finishes;
   a subsequent B goes back. Moving away clears the first press.
3. Open Keybinds, switch tabs, and focus each controller callout. Change a binding
   and swap the sticks; verify the pathways follow. B cancels capture or goes back.
4. In a tutorial, inspect the yellow Continue frame. Wait for the bubble to
   collapse toward the duck; Y restores it. X opens confirmation; B cancels it.
5. Confirm Skip from a disposable profile; verify it returns to MainMenu and
   preserves already-earned resources.

Verified for Update (11):

- Unity 6000.0.35f1 compilation completed successfully. The final Console snapshot
  reported zero errors and warnings. An intermediate verification-code API error
  was corrected before running the successful tests.
- 95 live Play Mode controller/settings checks passed with a simulated gamepad
  and a temporary save. These exercised default migration, reachability across
  repeated tab changes, toggles, all four sliders, timed double presses, actual
  rebinding, pathway updates, keyboard/controller capture cancellation and Back.
- 256 tutorial Play Mode checks passed, including unchanged lesson progression,
  rightward collapse without fading, tail vertex transparency, A/B/Y/X defaults,
  skip confirmation and preservation of resources from six tutorial checkpoints.
- Two controller-diagram visual passes were reviewed at 1920x1080. The second
  corrected the small Start label and confirmed the focused yellow pathway.
  Settings and tutorial captures were also reviewed in their real scenes; the
  settings hint no longer overlaps the existing code-entry controls.
- Static diff review completed. uGUI package documentation consulted: Basic
  Layout, Rect Transform, Event System, Navigation Options, Slider and Toggle;
  the Unity CLI play-mode verification guide was used for Editor checks.

The co-op, shop and item probes were updated for the new input defaults and
compiled, but their full suites were not rerun. No physical-controller acceptance,
mobile device or WebGL/player build verification was performed. Final artwork
approval and behavior on other screen sizes remain user checks. The temporary
Play Mode sessions restore saved preferences on exit; the Editor is left stopped.

## Update (12) implementation and acceptance

The coin icon and amount sit below XP in solo and co-op. Enemy spawning floors
maximum health after collection, wave, type and elite scaling. Sabotage also
floors the resulting starting health, with a minimum of 1 HP. Combat damage
still supports fractions.

Sawblades and Cursor Aura share a per-player adjustable center. Processed aim
pressure from 0.2 to 0.5 retracts it at 8 world units per second. Pressure from
0.6 to 0.92 extends it. The neutral, middle and outer bands hold the radius.
The radius spans 0 to 10 units from the duck. Full tilt changes direction at the
selected reach. The assigned aiming stick is respected after swapping sticks.
These thresholds and speed are implementation choices for physical playtesting.

The item sheet contains twelve distinct 32-pixel illustrations with transparent
backgrounds. ItemCatalog uses the sheet for shop, inventory, pickups, the item
wheel and applicable world effects. Unity imported it with point filtering,
uncompressed color and no mipmaps.

Practice combines aiming and shooting into five targets. Its two ground enemies
drop their usual 4–6 coins each and the coins automatically fly to the player.
Completing the health lesson and collecting the drops unlocks the end prompt.
Continue saves 200 additional coins and the Shop checkpoint together, then
returns to MainMenu. Older Play/Waves saves receive that reward once and resume
the shop. The serialized stage numbers remain stable.

Movement pads accept the player's collider edge. The box requires an actual jump
event followed by upward-facing contact with its top. The button glow extends
20 canvas units beyond each edge and uses a soft falloff sized consistently for
both authored and runtime canvases.

### Files changed for Update (12)

| File | Reason |
| --- | --- |
| Assets/Scripts/GameUI.cs | Place solo coins below XP and remove the retired five-wave suffix. |
| Assets/Scripts/CoopRunUI.cs | Place co-op coins below the shared XP bar. |
| Assets/Scripts/EnemyBase.cs | Floor spawn health and share ordinary coin drops with practice enemies. |
| Assets/Scripts/LocalPlayer.cs | Store and adjust each controller player's effect reach. |
| Assets/Scripts/Inputhelper.cs | Route adjustable effect positions through existing input ownership. |
| Assets/Scripts/AuraController.cs | Use the adjustable position for Cursor Aura. |
| Assets/Scripts/PlayerItems.cs | Center sawblade orbits at the adjustable position. |
| Assets/Scripts/ItemCatalog.cs | Load and cache the twelve item sprites from the sheet. |
| Assets/Resources/ItemIcons.png | Distinct placeholder illustrations for every item. |
| Assets/Resources/ItemIcons.png.meta | Unity-generated texture metadata and import settings. |
| Assets/Scripts/PlayerController.cs | Notify the tutorial when a jump actually occurs. |
| Assets/Scripts/TutorialDirector.Practice.cs | Combined shooting lesson, forgiving pad contacts, top landings and practice coin lesson. |
| Assets/Scripts/TutorialDirector.cs | End practice at the shop and transact its completion reward. |
| Assets/Scripts/TutorialDirector.Dialogue.cs | Match the stick demonstration to the combined lesson. |
| Assets/Scripts/TutorialDirector.Menus.cs | Remove retired Play guidance and enlarge/soften the green glow. |
| Assets/Scripts/TutorialProgress.cs | Grant the practice reward once while retaining serialized checkpoint values. |
| Assets/Scripts/SaveSystem.cs | Migrate retired Play/Waves checkpoints into the funded shop. |
| Assets/Scripts/Coin.cs | Vacuum practice coins and expose pending pickups for completion. |
| Assets/Scripts/WaveManager.cs | Remove the retired tutorial wave reduction and wave-five completion. |
| Assets/Scripts/Editor/EnemyUpdateVerification.cs | Assert floored normal, elite and Sabotage spawn health. |
| Assets/Scripts/Editor/TutorialVerification.cs | Verify the practice reward and old-checkpoint migration. |
| Assets/Scripts/Editor/UIUpdateVerification.cs | Expect floored health in gameplay verification. |
| Assets/Scripts/TutorialPlayProbe.cs | Exercise the shortened tutorial, edge contacts, coins and direct shop transition. |
| Assets/Scripts/ItemUpdatePlayProbe.cs | Exercise adjustable sawblade/aura reach and current controller UI behavior. |
| Assets/Scripts/CoopVerificationProbe.cs | Verify card upgrade focus in the current modal before continuing co-op checks. |
| Docs/ARCHITECTURE.md | Describe current health, tutorial, icons and controller behavior. |
| Docs/TUTORIAL_IMPLEMENTATION.md | Record implementation choices, file reasons, setup and acceptance checks. |

### Unity setup and manual acceptance

The existing MainMenu and SampleScene references support this update. No extra
Inspector assignments or scene/prefab setup are required. The item texture and
its Unity-generated metadata must accompany the source changes.

1. Start a disposable fresh profile. Graze each movement pad, then jump onto an
   edge of the box. Walking against its side must leave the objective incomplete.
2. Aim and shoot all five targets in one lesson. Test keyboard/mouse and a physical
   controller, including remapped fire and swapped sticks.
3. Defeat both enemies. Check the coin popup, coins flying to the duck and the
   counter below XP. Finish the health lesson, collect the 200-coin bonus and
   proceed directly to the shop.
4. Complete the pack and upgrade lessons. Reload the menu and confirm purchases,
   rewards and the completed tutorial persist.
5. With sawblades and Cursor Aura, use light tilt to retract, firm partial tilt
   to extend, and full tilt to aim at a fixed reach. Release the stick, pause,
   reconnect and test a second player controlling their own effects.
6. Inspect all twelve item icons in inventory, shop and the item wheel. Check the
   green glow and white text at the intended game resolution.
7. On a fresh collection, confirm ordinary ground enemies on wave 2 take two
   points of damage to defeat. Repeat with elites and collection upgrades.

### Verification completed for Update (12)

- Unity 6000.0.35f1 successfully compiled the final scripts.
- 69 isolated save, economy and serialized-reference checks passed, including
  the one-time 200-coin reward and both retired-checkpoint migrations.
- 184 collection difficulty checks passed for normal enemies, elites, Sabotage,
  collection scaling and repeated initialization.
- 217 tutorial Play Mode checks passed with a simulated controller and a temporary
  save. These covered a real jump/top landing, a collider grazing a movement pad,
  five real feather hits, ordinary coin drops and vacuum collection, the 200-coin
  shop handoff, purchases, upgrades, reload recovery, item guidance and skipping.
- 23 targeted gameplay checks passed for two-hit wave-two enemies, all twelve icon
  resources, co-op coin layout, two independent effect cursors, full-tilt hold,
  release hold, swapped sticks and actual aura/sawblade centers.
- 218 item integration checks passed. These covered item consumption, bomb impact,
  Shock, clone expiry, fire damage, Divine Intervention/restoration, Nuke radiation,
  sawblade damage per orbit and adjustable reach, plus menu/input integration.
- 95 controller/settings checks passed for navigation, toggles, slider double
  presses, rebinding, capture cancellation and return paths.
- 192 co-op integration checks passed for device ownership, movement, shooting,
  disconnection/reconnection, independent card offers, respawn, game-over results,
  2–4-player sessions, and solo keyboard fallback/controller hotplug.
- Reviewed item artwork, the solo HUD, combined tutorial prompt, card guidance and
  shop glow at 1920x1080. A second glow pass corrected its sliced-border scale for
  the authored canvas. Static diff review and whitespace checks passed.

The older item/co-op probes assumed an earlier controller layout, single-press
slider editing and inline card upgrades. Their assertions now follow the current
UI. The sawblade assertion now checks all four positions instead of assuming a
scene-search enumeration order. The final suites passed after these corrections.
The Console retains an earlier corrected probe failure and historical command
errors. Its captured log has no subsequent error from the successful final runs.
Existing missing-script import warnings and device-disconnection warnings remain.

Tests used isolated saves and restored saved preferences on exit. The Editor is
stopped with the clean MainMenu scene open. Physical-controller feel, mobile
device behavior, other resolutions and standalone/WebGL builds have not been
verified for this update. The broader card/combat suite was not rerun.

Verification records are under tmp/update12. The saved reports are
save-verification.txt, difficulty-verification.txt, gameplay.txt,
controller-verification.txt, coop-verification.txt, tutorial/play-verification.txt
and items/verification.txt. The folder also contains the external
gameplay-check.cs and controller-check.cs verification scripts and PNG/JPEG
captures used for visual review. These supporting files are outside Assets.
The final glow capture is glow-final.png. Older tutorial screenshots show the
first glow pass.

Local uGUI Basic Layout and Rect Transform documentation, and the installed
Unity Pipeline scripts/build-and-compilation documentation informed the checks.
