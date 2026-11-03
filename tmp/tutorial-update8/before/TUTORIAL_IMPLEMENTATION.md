# Guided tutorial implementation

Implemented from `Duck Defender Update (7).pdf` (three pages). The document supplies
the gameplay outline; repository rules govern implementation and Unity assets.

## Behavior and recovery

- A fresh save automatically enters practice. Existing version-2 saves keep their
  coins/cards and skip the core tutorial. Reset Game Data creates a fresh tutorial.
- Practice unlocks movement, jumping, aiming and shooting in order. Both movement
  goals, landing on the box, all five aim goals and five real feather hits are
  required. Instructions reflect the current keyboard/controller bindings and
  move/aim stick assignment. Touch practice instructions use the existing sticks.
- Practice has a **Restart step** button; controller Back restarts that objective.
  Falling below or wandering outside the practice area returns the player to its
  origin. Restart rebuilds targets/enemies and clears partial objective progress.
- Two existing ground-enemy prefabs have 2 HP, freeze nearby, give exactly half a
  level each and no coins. Only Basic Health can be selected from the three Basic
  offers. XP and health HUDs appear at their respective lessons.
- Stage 2 holds waves until Continue, runs waves 1–5 with enemy tips, disables
  items/drops and floors incoming damage at 1 HP. **Restart stage** replays the
  five-wave stage. Returning to MainMenu during it returns to the Play checkpoint.
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
  The index selects the purchased pack, directs paging toward its eligible card
  and permits only that upgrade. Upgrade and completion checkpoint save together.
- Continue requires a released input plus a short grace period. Completion banners
  own controller focus so dismissing one cannot also activate Play underneath.
  Tutorial, enemy-tip, disconnect and level-up pauses respect each other.
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
Unity generated the `.meta` files for the eight new scripts.

The simple stick illustration, markers and box are functional runtime geometry.
Artist-authored visuals and device/resolution-specific visual tuning remain the
user's responsibility. The solo health bar uses classic proportions and numeric
white text, placed above the existing solo XP bar.

## Changed files

All paths below are relative to the repository root.

| File | Reason |
| --- | --- |
| `Assets/Scripts/TutorialProgress.cs` | Serializable core/item checkpoints, legacy compatibility, one-time reward. |
| `Assets/Scripts/TutorialDirector.cs` | Scene bootstrap, prompts, input gates, pause ownership, stage restart and reward transition. |
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
`Assets/Scripts/TutorialDirector.Menus.cs.meta`, `Assets/Scripts/TutorialTarget.cs.meta`,
`Assets/Scripts/ShopManager.Tutorial.cs.meta`, `Assets/Scripts/TutorialPlayProbe.cs.meta`,
`Assets/Scripts/Editor/TutorialVerification.cs.meta`. Existing metadata was preserved.

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
- 57 isolated save/economy/serialized-reference assertions, including all four packs,
  cached save writes, repeated reward/purchase/upgrade calls and item restrictions.
- Existing regressions: 1,357 card checks, 79 ordinary shop economy checks and
  13 item/developer-code checks passed with completed tutorial fixtures.
- 106 automated Play Mode checks in the final run exercise controller input events for movement,
  jumping, aiming, shooting and menu submissions. Enemy damage during combat/waves
  is accelerated through existing damage methods; this is not a manual combat run.
- Game-view screenshots inspected for prompt/font and health HUD placement.

Manual hardware controller testing, keyboard/mouse/touch end-to-end testing, broad
resolution/aspect-ratio testing, release builds and browser persistence were not
verified. The existing SaveSystem writes and WebGL IndexedDB synchronization are
retained; abrupt storage failure or closing before browser synchronization completes
is outside the verified checkpoint guarantees. Missing/changed game assets still
require fixing their assignments; the current required references were checked.

## Play Mode checklist

1. Use an isolated fresh profile. Confirm auto-entry, hidden HUD, locked controls,
   both movement markers, box landing, five aim goals and five feather hits. Try
   early Continue, held Confirm, falling away, and Restart step halfway through.
2. Defeat the two practice enemies. Check 2 HP each, half-XP awards, no coins,
   forced Health choice and readable numeric health. Return to the Play-only menu.
3. Play waves 1–5 without acceleration. Check enemy tips, wave x/5, the 1-HP floor,
   no items, Restart stage, closing/reopening during a wave and the paused ending.
   Verify 200 bonus coins only once and no wave 6.
4. Repeat the shop/index flow with each pack. Cancel the purchase confirmation,
   reopen it, buy once, close/reopen before slicing and during reveal, then find
   and upgrade the highlighted card. Try unrelated spending and navigation.
   Verify the pack plus upgrade spend exactly 200 and preserve earned kill coins.
5. Test first item purchase and first pickup separately. Verify the pickup lesson
   waits until MainMenu, Items appears with even spacing, all four equip slots
   work, Sell/Unequip cannot remove the required item, and completion releases
   menus. Reload at Tab, Equip and Controls checkpoints.
6. Start normal runs with and without equipped items. Check wheel visibility,
   real lethal damage, normal waves, item drops after two completed normal runs,
   regular menus and existing co-op. Repeat the tutorial with remapped controls,
   swapped sticks, unplug/reconnect, keyboard/mouse and touch on supported targets.
