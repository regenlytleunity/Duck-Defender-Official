# Guided tutorial implementation

Implemented from `Duck Defender Update (7).pdf` (three pages), with the refinements
in `Duck Defender Update (8).pdf` (one page) and Updates (9) and (10). These documents supply the gameplay
outline; repository rules govern implementation and Unity assets.

## Behavior and recovery

- A fresh save automatically enters practice. Existing version-2 saves keep their
  coins/cards and skip the core tutorial. Reset Game Data creates a fresh tutorial.
- Practice unlocks movement, jumping, aiming and shooting in order. Both movement
  goals, landing on the box, all five aim goals and five real feather hits are
  required. Instructions reflect the current keyboard/controller bindings and
  move/aim stick assignment. Touch practice instructions use the existing sticks.
- Walking and box-landing goals fade upward from green to transparent. Five
  dartboards appear farther from the player for aiming and shooting. These visuals
  are generated once at runtime; there are no new serialized textures or sprites.
- Practice has no **Restart step** button or Back shortcut. Falling below or
  wandering outside the area returns the player to its origin and retains completed
  objectives. Closing the game still restarts stage 1 from its saved checkpoint.
- Two existing ground-enemy prefabs enter from outside the camera and move toward
  fixed stop points, then freeze. They cannot take damage during the approach.
  They have 2 HP, give exactly half a level each and no coins. All three Basic
  offers can be focused/hovered; only Health can be activated. XP and health HUDs
  appear at their lessons. Sibling decorations are hidden with the locked HUD.
- Stage 2 holds waves until the introduction's Interact press, runs waves 1–5 with
  half the normal enemies (2, 3, 4, 5, 6 with current assets), keeps enemy tips, disables
  items/drops and floors incoming damage at 1 HP. There is no Restart stage button.
  Returning to MainMenu during it returns to the Play checkpoint.
  Wave 5 saves the 200-coin reward and next checkpoint together before pausing;
  repeat completion calls cannot grant it again. These tutorial sessions do not
  count toward the existing normal-run item-drop eligibility.
- Menu controls and underlying purchase/equipment methods both enforce the
  tutorial restrictions. The guaranteed pack has two copies of one starter and
  one previously locked Common in the selected pack. Pack purchase and the later
  upgrade each cost 100 with the current assets. Unrelated purchases cannot spend
  that budget. Card grants, charge and checkpoint save in the same write.
- Quitting before slicing a paid pack resumes its opening without a second charge.
  Quitting during/after reveal preserves all cards and resumes the index lesson.
  The index initially selects the purchased pack, then allows all pack tabs, page
  arrows, layouts and card previews. An inset green border directs the player back to
  the required pack/page/card. Only that upgrade can spend coins; the action itself
  advances the lesson without an extra confirmation. Upgrade and completion save together.
- Tutorial prompts, completed objectives, pack reveals, completion banners and
  protected-run enemy tips require Interact. Prompts display the mapped controller
  button, Enter for keyboard, or a touch hint, plus a highlighted Continue button. A short
  input grace and release-before-press prevent carryover, with menu submission
  suppressed while informational prompts own Interact. Learned movement/jump
  controls remain available during later practice lessons. Enemy approaches and
  completed purchase/upgrade actions advance their own checkpoints directly.
- Movement goals and the box landing glow both fade after completion. Tutorial
  card/button guidance uses an inset green border, separate from yellow
  controller focus. Shop guidance follows the chosen pack instead of leaving a
  second focus frame on the first one. The controller aim arrow is a closed, consistently outlined silhouette.
- Speech-box prompts default above gameplay with the existing duck at the right edge.
  Full text is laid out before the typewriter reveal; the duck bobs three pixels while
  speaking. After three seconds of lingering, dialogue collapses to a small reminder.
  Completing an objective reopens it; clicking the duck reopens instructions.
  Continue remains available on collapsed completed/informational prompts. Card offers use a shorter top panel; reveals
  use the empty footer, the shop uses the gap above packs, and the index uses its
  upper-right space. New messages restore the normal background color after a
  chromatic completion banner, including the subsequent item tutorial.
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
closing the application during stage 1 restarts practice from Welcome. Stage 2
restarts at wave 1, preserving coins already saved and the one-time reward rule.

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

## Changed files

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

The current Update (9) file manifest, verified results, balance assumptions and
manual checklist are in [UPDATE_9_IMPLEMENTATION.md](UPDATE_9_IMPLEMENTATION.md).
The Update (8) results above are historical; the current probe writes to
`tmp/update9/` to preserve the earlier screenshots and verification records.


### Update (10) changes and manual checks

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
The portrait moves into spare corner space on card offers and index pages to keep
card text and controls unobscured.
The exact existing DuckDefenderTestFontv2 asset/material remains in use.

Manual Play Mode checklist:

1. Run practice using controller, then keyboard/mouse or touch. Check circular stick
   motion, stable text wrapping, gentle duck bob, three-second linger and collapse.
2. Click the duck to reread, finish an objective, and use both Continue and the mapped
   Interact input. Holding Interact must not skip multiple prompts.
3. Leave aim neutral: no target should turn green. Aim at all five raised targets,
   then shoot them and check the small red/green/cream/dark bursts.
4. Hover/focus all three Basic cards. Try Damage/Mobility: expect a brief shake and
   no upgrade. Choose Health to continue. Check inset green borders on menu controls.
5. Check health and XP at partial/full values in solo and co-op. XP has no orb, uses
   the health-bar shape and blue fill, and all labels retain white interiors.
6. Check shop confirmation, pack reveal, index/card modal and item equipment at the
   intended display resolutions for overlap, readable text and reachable controls.
