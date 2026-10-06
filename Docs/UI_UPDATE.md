# October UI update — implementation and verification

Implemented from `C:/Users/regen/Downloads/Duck Defender Update (1).pdf`, including the
layout sketches on pages 2–3 and the user's clarification: **both Medium and Hard
use +10% enemy movement speed; Hard doubles stat growth**.

## Result

- Main menu: left-side navigation, difficulty selection directly below Play,
  exclusive selected border, and disabled Host, Join, Ranked and Leaderboard.
- Shop: vertical coin/essence balances and four packs in a 2x2 grid.
- Index: pack tabs on the left, selected pack essence and coins above, a layout
  button cycling 3x1, 4x2 and 3x2, and bounded previous/next controls.
- Settings: master, SFX and music sliders; saved tips and particle switches;
  primary/alternate key bindings; restore defaults; and progress reset confirmation.
- Existing pixel art, font, pack definitions, collection rules and card controls
  are reused. The existing codes field retains its developer/release availability.

| Difficulty | Coins | Enemy health | Enemy damage | Movement speed | Additive wave-health growth |
| --- | --- | --- | --- | --- | --- |
| Easy | 1x | 1x | 1x | 1x | 1x |
| Medium | 1.25x | 1.5x | 1x | 1.1x | 1x |
| Hard | 1.5x | 2x | 2x | 1.1x | 2x |

Growth means the existing piecewise additive health table: Hard doubles that
addition before existing rounding and the 2x health multiplier. The active enemy
prefabs do not grow speed or firing rate per wave. Spawn counts and schedules are
unchanged. Tank health still derives from four times rounded ground health.

Positive run coin awards, including pickups and existing passive/interest awards,
are multiplied once in LevelManager. Fractional rewards carry between awards
within a run: four individual pickups pay the same as one four-coin stack.
Starting saved balances and shop costs are unchanged.

Reset confirmation clears progression, collection, essence, developer resources
and tutorial history. Audio, key bindings, particles and difficulty preferences
are kept. Particle suppression only hides ParticleSystemRenderer output;
gameplay scripts, hazards, colliders and sprite indicators continue running.

## Every changed file and purpose

Paths below are relative to the repository root.

| File | Purpose |
| --- | --- |
| `Assets/Scenes/MainMenu.unity` | Saved menu/shop/index/settings layouts, references, persistent callbacks, modal groups and InputManager, authored with Unity APIs. |
| `Assets/Prefabs/UI Prefabs/ShopPackButton.prefab` | Pack art, name and price placement for the 2x2 shop. |
| `Assets/Prefabs/UI Prefabs/Prefab_UI_Card_Reward.prefab` | Readable index text and upgrade button labels; existing card behavior retained. |
| `Assets/Scripts/MainMenuUI.cs` | Difficulty selection, separate essence balances, correct pack child binding, reset prompt routing. |
| `Assets/Scripts/CardIndexUI.cs` | Pack filtering, selectable grid pagination, responsive placement and live balances. |
| `Assets/Scripts/SettingsMenuUI.cs` | Sliders, preferences, binding capture/conflicts/defaults, confirmation and modal keyboard isolation. |
| `Assets/Scripts/AudioManager.cs` | Saved master volume and live SFX/music gain updates. |
| `Assets/Scripts/InputManager.cs` | Save/load and assignment of existing alternate movement bindings. |
| `Assets/Scripts/Inputhelper.cs` | Desktop gameplay uses saved InputManager bindings; mobile retains priority. |
| `Assets/Scripts/GameDifficulty.cs` (new) | Saved selection and shared difficulty multipliers. |
| `Assets/Scripts/EnemyBase.cs` | Apply health and movement multipliers at spawn. |
| `Assets/Scripts/WaveManager.cs` | Apply Hard's doubled additive health growth. |
| `Assets/Scripts/PlayerHealth.cs` | Scale incoming enemy contact, ammunition and poison damage. |
| `Assets/Scripts/DefenderWall.cs` | Apply the same enemy damage multiplier to defensive walls. |
| `Assets/Scripts/LevelManager.cs` | Multiply earned coins with fractional carry. |
| `Assets/Scripts/ShopManager.cs` | Confirmed reset explicitly resets tutorial state with progression. |
| `Assets/Scripts/ParticleVisibility.cs` (new) | Persisted particle renderer visibility, including newly spawned and pooled effects. |
| `Assets/Scripts/Editor/UIUpdateSetup.cs` (new) | Repeatable Unity Editor layout and prefab authoring. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` (new) | Isolated menu/gameplay checks and pointer hit testing; preference restoration. |
| `Assets/Scripts/UIUpdatePlayProbe.cs` (new, Editor-guarded) | Apply the disposable save path before Play Mode scene initialization. |
| `Docs/ARCHITECTURE.md` | Document actual UI, input, audio and difficulty ownership. |
| `Docs/UI_UPDATE.md` (new) | This implementation, setup and verification report. |

Each of these existing particle prefabs gained ParticleVisibility through Unity
PrefabUtility, with existing GUIDs preserved:

- `Assets/Prefabs/Effects/Double Jump Effect.prefab`
- `Assets/Prefabs/Effects/Explosion Effect.prefab`
- `Assets/Prefabs/Effects/Feather Hit Effect.prefab`
- `Assets/Prefabs/Effects/FireTrail.prefab`
- `Assets/Prefabs/Effects/MedicHeal.prefab`
- `Assets/Prefabs/Effects/Obsidian Train.prefab`
- `Assets/Prefabs/Effects/ProtectorShockwave.prefab`
- `Assets/Prefabs/Effects/Savior area.prefab`
- `Assets/Prefabs/Effects/Shockwave Effect.prefab`
- `Assets/Prefabs/Effects/Volcano fire.prefab`
- `Assets/Prefabs/Effects/Wormhole.prefab`

Unity imported the five new scripts and generated these metadata files:

- `Assets/Scripts/GameDifficulty.cs.meta`
- `Assets/Scripts/ParticleVisibility.cs.meta`
- `Assets/Scripts/UIUpdatePlayProbe.cs.meta`
- `Assets/Scripts/Editor/UIUpdateSetup.cs.meta`
- `Assets/Scripts/Editor/UIUpdateVerification.cs.meta`

No metadata or scene/prefab YAML was hand-authored. Existing serialized field names
were retained. New UI references and callbacks were assigned in MainMenu;
the former index scroll hierarchy remains inactive for compatibility. MainMenu's
MenuPanel now references the complete MenuContent group. No packages or project
settings were changed, and no commit or push was made.

## Unity setup

**No additional Inspector, scene or prefab setup is required.** MainMenu and the
two UI/eleven particle prefabs were modified, saved and inspected in Unity.
Final state: MainMenu open in Edit Mode, scene clean, no missing scripts.

For deliberate future re-authoring, open MainMenu and choose
**Duck Defender > UI > Apply October Layout**. This writes the layout, so it is
not necessary for normal play and should not be run after custom visual tuning
unless reapplying this layout is intended.

Future ParticleSystem prefabs should include ParticleVisibility on each particle
renderer. No component is needed on pure sprite effects.

## Verification actually performed

- Static review of owners, serialization references, damage/coin callers, input
  routing, lifecycle/event cleanup, and guarded Editor-only code.
- Unity 6000.0.35f1 compilation passed. Final Editor status: not compiling,
  compilationFailed=false, current Console errors=0 and warnings=0.
- **129 automated Play Mode UI assertions passed**: difficulty buttons and
  selection, disabled online controls, one/three pack purchases, duplicate purchase
  guard, affordability, every pack/page boundary, all 47 collection cards and
  description overflow, actual upgrade/ascension callbacks and balances, audio/tips/
  particle settings, key persistence/conflicts/defaults, reset/cancel, and modal
  keyboard interaction isolation.
- **62 automated gameplay assertions passed** after invoking the scene's wired
  Play button: selected Hard survives transition, one active InputManager,
  all four active enemy prefabs at waves 1 and 20 for all difficulties, health and
  speed values, player damage, fractional/stacked coin rewards, saved coins and HUD.
- Returning from SampleScene to MainMenu retained Hard selection and exactly one
  active InputManager and one AudioManager.
- Graphics raycaster checks reached the active controls on menu, shop, index,
  settings and modal screens. Full-row tips/particle targets are clickable.
- Actual Play Mode UI rendered and visually inspected at **1920x1080** and
  **960x600**. Screenshots were captured through a temporary camera/render target;
  original Canvas render mode and camera state were restored afterward.
- All particle prefabs under Assets/Prefabs were inspected: none lack the
  visibility component.
- Automated purchases/resets used disposable saves through the existing
  VerificationSavePath. Tested PlayerPrefs were restored when leaving Play Mode;
  the verification override/session path were verified cleared.
- Source/document whitespace checks passed. Temporary screenshots and their
  imported metadata were removed from Assets via AssetDatabase.

Checks invoke real callbacks and gameplay methods in Play Mode. They are not a
substitute for a human playthrough or physical keyboard/touch testing.

## Manual Play Mode checklist

1. Open MainMenu and enter Play Mode. Select each difficulty; check the border,
   description and resulting run. Return and check selection persistence.
2. Open Shop, buy one or three packs when affordable, and check balances/reveal.
3. Open Index, visit every pack and page, then upgrade or ascend an eligible card.
4. Adjust all three volumes; leave/reopen Settings. Toggle tips and particles.
5. Rebind movement, jump, dash and shooting, including alternates; exercise the
   physical controls in gameplay. Try a duplicate, cancel capture with Escape,
   and restore defaults.
6. Open Reset Data and cancel. To test confirmation without real progress loss,
   use **Duck Defender > UI > Start Isolated Play Verification** first. Exiting
   that session restores the saved preference snapshot.
7. Repeat pointer/navigation checks at the intended shipping window size.

For automated reruns in an isolated session, the callable Editor methods are
UIUpdateVerification.CheckMenuFlows(), CheckCurrentPointers() and CheckGameplay().
The gameplay check expects SampleScene with Hard selected; menu checks reset the
disposable profile. Menu and gameplay checks require the isolated save; pointer checks are read-only.

## Remaining unverified items

- No Windows/WebGL player build or deployed-browser test was performed.
- No physical keyboard/mouse capture, touch-device, gamepad, or extended balance
  playthrough was performed. Binding assignment/persistence and gameplay routing
  were verified through code and Play Mode callbacks.
- Audio volume values and source behavior were inspected; subjective audio mix
  was not evaluated by listening.
- Portrait/mobile safe-area and other aspect ratios were not visually evaluated.
  Desktop 16:9 and the project's 960x600 Web resolution were checked.

## Follow-up: mobile controls appearing on PC

Confirmed the reported regression in Play Mode: after starting from MainMenu,
SampleScene's mobile canvas was visible on Windows and MobileInputController was
missing. InputManager's duplicate handler destroyed the entire shared InputSystem
GameObject, preventing the co-located mobile controller from hiding the canvas.

Files changed for this follow-up:

| File | Change |
| --- | --- |
| `Assets/Scripts/InputManager.cs` | Disable/destroy only the duplicate InputManager component, preserving the shared mobile controller. |
| `Assets/Scripts/MobileInputController.cs` | Set device mode and canvas visibility in Awake. Player builds always use Application.isMobilePlatform; Editor auto detection is always desktop, with an explicit Editor-only preview override. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Add CheckMobileControls to check controller survival, hidden/visible canvas, joystick references, singleton count and matching InputHelper routing. |
| `AGENTS.md` | Require white glyph infill for every future UI text addition, including material/outline usage. |
| `Docs/ARCHITECTURE.md` | Describe input singleton lifetime and mobile-device policy. |
| `Docs/UI_UPDATE.md` | Record the regression, correction and verification. |

Verification: reproduced the failure before the fix; Unity compilation passed
afterward. Seven mobile-visibility assertions passed for menu-to-game startup,
gameplay reload, loading gameplay without a pre-existing menu input manager,
and reloading with scene-owned persistent input. Explicit Editor mobile preview
and returning to automatic desktop mode also passed those checks. Tests used an
isolated save. The user's already-dirty MainMenu editing state was preserved;
no scene/prefab assets or Inspector values were changed for this follow-up.

No additional Unity setup is required. Leave AutoDetectPlatform enabled for
normal Editor play. To deliberately test the joysticks in the Editor, disable
AutoDetectPlatform and enable IsMobileEnabled before Play Mode. Those flags
cannot force touch UI in a desktop player build.

Manual checklist: start from MainMenu on PC and confirm both joysticks are hidden;
restart the run and return through the menu; confirm keyboard/mouse control still
works. On a real mobile build/browser, confirm both joysticks appear and respond.
Real mobile hardware, standalone player builds and mobile Web browsers were not
tested in this follow-up.

## Follow-up: larger cards and native sprite sizing

The button dimensions and fixed three-card capacity in this section were
superseded by the 2x sizing and selectable-grid follow-up below.

The index now displays at most three cards in a single row. At 1920x1080, each
card is approximately 472x660 pixels, around 64% larger in each dimension than
the previous two-row layout. Every collection card remains accessible through
the existing pack tabs and bounded page controls; upgrade and ascension retain
their existing behavior.

Play, Shop, Index, Host, Join, Ranked, Settings, Leaderboard, every Back button,
the four index pack selectors, Keybinds and Reset Data use the original 244x48
sprite at native size. The logo is 442x157. Image aspect ratios are preserved,
local scale is one, and stretching anchors are removed. These are dimensions
at the existing 1920x1080 reference resolution; the CanvasScaler continues to
scale the entire interface uniformly for smaller windows.

Difficulty buttons, page arrows, binding assignments, restore defaults, code
submission and confirmation controls use Unity's built-in UI button sprite.
Their dark backgrounds preserve contrast with white text. The three difficulty
buttons fit directly below Play. Existing pack/card artwork is retained.

Files changed for this follow-up:

| File | Purpose |
| --- | --- |
| `Assets/Scenes/MainMenu.unity` | Save native button/logo dimensions, default secondary sprites, fitted white labels, compact difficulty buttons and wider index content through Unity Editor APIs. |
| `Assets/Scripts/CardIndexUI.cs` | Share a three-card capacity between pagination and the larger proportional single-row layout. |
| `Assets/Scripts/Editor/UIUpdateSetup.cs` | Add targeted native sizing authoring; include it in future full layout authoring; default generated text to white. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Update page expectations and ascension navigation; verify native dimensions, sprite assignments and the larger three-card row. |
| `Docs/ARCHITECTURE.md` | Document the current index capacity and sprite sizing policy. |
| `Docs/UI_UPDATE.md` | Record these changes, verification, setup and remaining checks. |

The user's existing unsaved MainMenu edits were backed up before the targeted
operation and retained when saving the scene, including the white text,
version label and keybind wording. The full layout rebuild was not run.
No serialized fields were renamed, no callbacks were replaced, and no prefab,
package or project setting changes were required for this follow-up.

Verification actually performed:

- Unity compilation passed: compilationFailed=false and not compiling.
- 129 menu-flow Play Mode assertions passed, including all 47 collection cards,
  description overflow, pack/page boundaries, actual upgrade/ascension callbacks,
  difficulty selection, shop purchases, settings, key binding and reset behavior.
- 93 native sprite and three-card layout assertions passed at 1920x1080.
- Rendered menu, index, settings and keybind screens were visually checked at
  1920x1080 and 960x600. Shop and reset confirmation were also checked at 960x600.
- Graphics raycaster checks reached active controls on these screens, including
  the resized Back buttons, pack tabs, page arrows and secondary controls.
- Tests used disposable progression data. Exiting Play Mode restored preferences;
  the save override, preference snapshot and session key were verified cleared.
- MainMenu was left saved and clean in Edit Mode. The temporary Game View size
  was removed and the original 1920x1080 selection restored.
- Source/document whitespace checks passed. Preview PNGs were copied outside
  Assets and their temporary asset folder removed through AssetDatabase.

**Required Inspector, scene or prefab setup: none.** The saved scene is ready.
The optional menu command **Duck Defender > UI > Apply Button Sizing and Index Layout**
reapplies only this sizing/style operation; normal play does not require it.

Play Mode checklist for visual approval:

1. Open MainMenu: compare the logo and all main buttons with their original art;
   select each difficulty and open Shop, Index and Settings.
2. In Index, visit all four packs, advance to the last page and return; confirm
   no page has more than three cards and descriptions are readable.
3. Try an affordable upgrade/ascension and confirm the card and balances refresh.
4. Open Keybinds and the reset confirmation; check secondary button readability,
   navigation and Back buttons at the intended shipping window size.

No player build, deployed WebGL session, physical input test or mobile-device
visual test was performed for this sizing follow-up. Gameplay balance was
unchanged; its earlier checks above were not rerun for this UI-only change.


## Follow-up: 2x button sprites and selectable index layouts

Main pixel-art buttons now use twice the sprite's native width and height:
488x96 at the reference resolution, preserving the original 244:48 proportions.
This applies to Play, Shop, Index, Host, Join, Ranked, Settings, Leaderboard,
Back, the index pack selectors, Keybinds and Reset Data. Labels and the compact
difficulty controls were resized to suit. Back, pack tabs, Settings and
Leaderboard were positioned to keep the larger controls inside the screen and
clear of adjacent elements. The index content area leaves room for the wider
tabs. Secondary controls retain Unity's default button sprite and white text.
The logo retains its undistorted size from the previous update.

The new **LAYOUT** button beneath the pack tabs cycles:

1. **3 x 1**: three cards (default).
2. **4 x 2**: eight cards.
3. **3 x 2**: six cards.
4. Back to **3 x 1**.

Switching grids recomputes page counts and keeps the previously first visible
card on the resulting page. The current grid is shown on the button. Pack
changes and closing/reopening the index retain the grid during the current menu
scene; entering a new menu scene returns to 3x1. Card proportions, sorting,
collection state, upgrade and ascension controls are preserved.

Files changed for this follow-up:

| File | Purpose |
| --- | --- |
| `Assets/Scripts/CardIndexUI.cs` | Own grid state, layout cycling, dynamic capacity/page counts, card positioning and current-grid label. |
| `Assets/Scripts/Editor/UIUpdateSetup.cs` | Author 2x buttons with appropriate label sizes/spacing; add and wire the Layout button through Unity APIs. |
| `Assets/Scenes/MainMenu.unity` | Save the new dimensions/positions and Layout button, label references and CycleLayout callback. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Verify 2x dimensions and each grid's ordered card access, partial pages, navigation, retention, upgrades and ascensions. |
| `Docs/ARCHITECTURE.md` | Describe the implemented 2x sizing and selectable-grid ownership/lifetime. |
| `Docs/UI_UPDATE.md` | Record the final behavior, setup, verification and visual checklist. |

Serialization impact: CardIndexUI adds LayoutButton and LayoutText references,
both assigned and verified in MainMenu. Existing serialized fields and callbacks
were retained. No new script assets, prefab changes, package changes or project
settings changes were required. MainMenu was backed up before authoring; existing
scene changes from previous work remain intact.

Verification actually performed:

- Unity compilation passed with compilationFailed=false.
- **129 menu-flow assertions passed**, covering existing menu, shop, difficulty,
  settings and collection behavior.
- **94 button sizing/default index assertions passed**, including 488x96 pixel
  buttons, preserved image proportions and the new default-sprite Layout control.
- **595 grid assertions passed**, including all 47 cards in every layout,
  ordering, row/column placement, partial pages, page boundaries, layout-cycle
  order and wraparound, first-card retention, reopening the panel and real
  upgrade/ascension callbacks in every grid.
- Rendered MainMenu, Shop, Settings, Keybinds and all three index layouts were
  visually inspected at **1920x1080** and **960x600**. Graphics raycaster checks
  after rendering reached the active controls, including Layout and card actions.
- Tests used disposable progression data. The original Game View size was
  restored and temporary size removed. Exiting Play Mode restored preferences
  and cleared the verification save/session override.
- MainMenu was left saved and clean in Edit Mode, with no missing scripts.
  Source/document whitespace checks passed. Unity-generated empty scene fields
  retain Unity's trailing spaces; serialized YAML was not manually edited.
  Screenshots were written outside Assets; no temporary assets remain.

**Required Inspector, scene or prefab setup: none.** The new button and callback
are already saved and verified. The optional authoring menu is now
**Duck Defender > UI > Apply Button Sizing and Index Layout**; it is not needed
for normal play.

Play Mode checklist:

1. Compare the larger main buttons with the original art, then open each panel
   and use Back to check navigation and spacing.
2. Open Index and click Layout three times: 3x1 -> 4x2 -> 3x2 -> 3x1.
3. Change grids on the last page; check the page count and that the current card
   stays accessible. Change packs and reopen Index to check grid retention.
4. Use upgrade/ascension on an eligible card in each grid and inspect the layout
   at your intended game window size.

No standalone/WebGL player build, physical input test or mobile-hardware test
was performed for this follow-up. The denser grids intentionally show smaller
cards; 3x1 remains the default for readability.
