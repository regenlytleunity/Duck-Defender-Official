# Duck Defender Update (9)

Implemented from `Duck Defender Update (9).pdf`. The PDF supplies the requested
gameplay changes; repository instructions govern implementation and asset safety.

## Collection difficulty and balance

Easy/Medium/Hard controls are hidden in the main menu and removed from the runtime
host menu. Existing serialized callbacks remain safe no-ops. Old saved difficulty
choices no longer affect gameplay.

At `WaveManager.Start`, after scene `Awake` methods initialize the card registry,
`GameDifficulty.BeginRun` captures `SaveSystem.LoadData().CardCollection` against
`CardManager.AllCards`. Only unique registered, unlocked, non-Basic cards in the
four collection packs count. Persistent levels are clamped to each card's legal
range. Duplicate copies, temporary run upgrades, equipped-card filters, items,
essence and permanent shop stat upgrades do not contribute. Local co-op uses the
same host save. Collection changes during a run cannot change its snapshot.

The requested formula is retained exactly:

```text
x = eligible unlocked card count
a = summed persistent levels of those cards
E = max(0, (x * a - 72) / 53)
Mstart = 1 + 0.03 * E
Mwave = 1 + 0.02 * E
HP(w) = B * Mstart + S(w) * Mwave
```

`S(w)` is the unscaled cumulative destination-wave bracket sum. For example,
`S(1)=0`, `S(10)=4.5`, `S(20)=19.5`, and `S(50)=214.5`. Health is not floored or
compounded. Existing ground/flying/lobber bases are 2/1/1; tanks receive three times
same-wave ground HP, including growth, before existing variant effects. Elite
health modifiers and the existing Sabotage cap/minimum remain; Sabotage now retains
fractional HP. Reinitialization recalculates from authored values and does not
compound health, elite size/tint, or health-bar size. Enemy damage, speed, normal
spawn schedules and coin rewards use their base behavior, without the old mode bonuses.

Two document assumptions differ from the current assets/rewards:

| Situation | PDF assumption | Repository evidence and implementation |
| --- | --- | --- |
| Collection size | 51 eligible collection cards | 51 total registered cards: **47 eligible + 4 Basics**. Count actual eligible IDs; do not invent four cards. |
| Tutorial completion | 8 cards / 9 total levels | Starts at 8/8; the guaranteed pack unlocks one new card and the guided upgrade adds a level, producing **9/10** for every pack. Preserve existing grants and prices. |

The mathematical 8/9 baseline still gives `E=0`. Actual 9/10 completion gives
`E=18/53`, ground HP approximately **2.0204 at wave 1** and **21.6528 at wave 20**.
Fractional health can cross a whole-damage hit threshold: a 1-damage attack needs
three hits against 2.0204 HP. The former floor also hid half-point wave increases.
This is a consequence of the requested fractional formula, not a claim that a
roughly 1% HP increase feels like only a 1% difficulty increase. The formula's
constants and tutorial reward contents have not been silently retuned. Manual
combat evaluation should focus on this early threshold and late-wave survivability.

## Tutorial changes

- Informational prompts, completed practice objectives, protected-run enemy tips,
  pack reveals and completion banners wait for Interact. The text reflects the
  controller mapping, Enter on keyboard or tapping on touch. There is no Continue
  button. Input grace, release-before-press and consumption prevent one press from
  dismissing a prompt and activating the menu underneath.
- Stage 2 no longer creates a Restart stage button.
- Completed box-landing guidance fades using the same behavior as walking goals.
- Card/button guidance uses the existing upward green gradient. Yellow controller
  focus remains separate. Shop guidance follows the selected pack, preventing the
  apparent second focus border stuck on pack one.
- Learned movement and jump controls remain available later in practice. Players
  can browse index packs, pages, layouts and previews while lesson/economy guards
  retain the required purchase and upgrade. The item lesson also permits returning
  to card browsing and guides the player back to Items.
- The aim indicator has a subtle dark outline. New text uses the original
  `DuckDefenderTestFontv2` asset through the existing UI helpers with white infill.
- Tutorial index balances fit beside the prompt. Completed tutorials leave normal
  menus free of the empty tutorial panel.

## Changed files and reasons

Paths below are relative to the repository root. Existing user changes in the
documentation and `tmp/tutorial/` were preserved.

| File | Reason |
| --- | --- |
| `Assets/Scripts/GameDifficulty.cs` | Own immutable run collection snapshot and requested formula; neutralize legacy mode modifiers/callbacks. |
| `Assets/Scripts/WaveManager.cs` | Capture the snapshot at run start; expose unscaled cumulative growth and scaled ground HP. |
| `Assets/Scripts/EnemyBase.cs` | Apply scaled fractional HP once, preserve elite/Sabotage behavior, make health/visual reinitialization idempotent and reuse the existing health bar. |
| `Assets/Scripts/TankEnemy.cs` | Derive tank HP from three times scaled same-wave ground HP. |
| `Assets/Scripts/EnemyHealthBar.cs` | Avoid multiplying bar dimensions repeatedly on initialization. |
| `Assets/Scripts/MainMenuUI.cs` | Hide mode controls, retain serialized callbacks and show the automatic-difficulty explanation. |
| `Assets/Scripts/HostMenuUI.cs` | Replace the mode selector with a static host-collection explanation. |
| `Assets/Scripts/TutorialDirector.cs` | Explicit mapped continuation, input consumption, retained movement/jump, no Restart stage control, and hidden inactive prompts. |
| `Assets/Scripts/TutorialDirector.Practice.cs` | Shared gradient, matching completion fades and green card guidance. |
| `Assets/Scripts/TutorialDirector.Menus.cs` | Free safe browsing, action-driven checkpoints, green objective guidance, and existing purchase/equipment restrictions. Includes the small `TutorialGuidance` helper. |
| `Assets/Scripts/ControllerMenuNavigation.cs` | Suppress controller submissions while a tutorial prompt or enemy tip owns the input. |
| `Assets/Scripts/CardIndexUI.cs` | Permit pack browsing during lessons while blocking it during continuation prompts. |
| `Assets/Scripts/IndexLoadoutUI.cs` | Keep tutorial balances beside the prompt instead of underneath it. |
| `Assets/Scripts/LoadoutPanel.cs` | Green item guidance and accurate upgrade/ascension availability while tutorial previews are open. |
| `Assets/Scripts/EnemyTipUI.cs` | Replace protected-tutorial automatic dismissal with mapped Interact. |
| `Assets/Scripts/LocalPlayer.cs` | Render the aim outline behind the existing line with matching visibility. |
| `Assets/Scripts/Editor/EnemyUpdateVerification.cs` | Verify PDF samples, wave boundaries, collection inputs, variants, fractional health and repeated initialization. |
| `Assets/Scripts/Editor/TutorialVerification.cs` | Check actual post-tutorial collection totals and preserve prior results by writing to `tmp/update9/`. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Update legacy difficulty assertions for automatic scaling and check ordinary menus have no tutorial panel. |
| `Assets/Scripts/TutorialPlayProbe.cs` | Exercise explicit Interact, guidance, browsing, fades, item progression and normal runs; reliably assign the virtual controller after scene reloads. |
| `Docs/ARCHITECTURE.md` | Describe implemented scaling and current tutorial behavior. |
| `Docs/TUTORIAL_IMPLEMENTATION.md` | Update current behavior and link this report while retaining historical Update (8) results. |
| `Docs/UPDATE_9_IMPLEMENTATION.md` | Record this implementation, asset/reward discrepancies, verification and manual checklist. |

Generated verification outputs under `tmp/update9/`: `difficulty-verification.txt`,
`verification.txt`, `play-verification.txt`, `menu-verification.txt`,
`gameplay-verification.txt`, `targeted-verification.txt`, `isolated-save.json`, and
screenshots `welcome.png`, `movement.png`, `jump.png`, `aim.png`, `cards.png`,
`health.png`, `shop.png`, `confirmation.png`, `pack.png`, `index.png`, `item.png`,
`host.png`. These are test evidence, not runtime assets.

## Unity setup and serialization

**No additional Unity Editor, Inspector, scene or prefab setup is required.**
Existing menu font, difficulty-button, card registry and enemy prefab references
are reused. No serialized fields were renamed; no scene/prefab/ScriptableObject
YAML, `.meta` files, packages or project settings were edited. No new Unity source
assets were created, so no new `.meta` generation is required.

## Verification

- Static review of affected owners, call sites, serialized references and diffs.
- Successful Unity 6000.0.35f1 compilation in the running Editor.
- **184 collection difficulty assertions** passed, including all eight PDF sample
  pairs, growth boundaries, exclusions/deduplication, captured state, equal
  products, actual registry totals, fractional damage, elite and Sabotage behavior,
  and repeated enemy initialization.
- **69 tutorial save/economy/reference assertions** passed, including all four
  tutorial packs producing 9/10 after the upgrade.
- **161 automated tutorial Play Mode assertions** passed on November 11, 2026:
  movement, jumping, aim/shooting, pauses, explicit prompts, half-sized waves,
  reload recovery, guided purchase/upgrade, item equipping and normal-run recovery.
  The probe uses a virtual controller; accelerated wave combat calls existing
  damage methods. This is not a manual balance playthrough.
- **130 ordinary menu assertions** and **62 gameplay difficulty assertions** passed.
  The ordinary menu fixture requires completed tutorials and `PackOpeningTier=1`
  to test Open 3. Its first attempt correctly failed the outdated assumption that
  three-pack purchases are unlocked on a fresh profile; the explicit fixture passed.
- Targeted Play Mode checks passed for the static host label/font, remapped Interact
  input/hint, and the tutorial essence label fitting beside the prompt. After the
  final inactive-panel fix, ordinary menu checks passed again and resetting the
  isolated profile still displayed the active Welcome lesson. The full 161-check
  tutorial run preceded that final panel-visibility fix.
- Frame advancement was confirmed. Screenshots at 1920x1080 were visually checked
  for gradient guidance, aim outline, prompt/font placement and menu clearance.
- Tests used isolated saves; real player progression was not used. Play Mode was
  exited and preference restoration/isolation cleanup confirmed. MainMenu was clean.

Earlier attempts stopped at controller/menu transitions. The probe now assigns its
virtual device after every scene reload, and the final full controller run completed;
an earlier item-tab failure did not recur in the isolated tab check or full rerun.
CLI main-thread timeouts occurred while the Editor
was unfocused and recovered after enabling its tick loop. The final targeted
session reported no Unity Console errors. Existing missing-script warnings on
`SampleScene/Tilemap_Background` and `SampleScene/CardManager` remain outside this
change; their serialized objects were not modified.

Not verified: physical-controller behavior, complete keyboard/mouse/touch
playthroughs, all resolutions, release/WebGL builds, browser persistence, manual
late-wave balance, or a complete pooled-enemy combat lifecycle. The current wave
spawner instantiates enemies; repeated initialization checks cover scaling and
visual accumulation, not a new pooling system.

## Play Mode checklist

1. Use a test profile. Confirm ordinary main/host menus have no difficulty selector
   or stray tutorial panel; a normal run reflects that profile's collection.
2. Start fresh onboarding. Leave each explanation open, then press Interact. Hold
   it across transitions to confirm a fresh press is needed. Repeat with a remapped
   controller button, keyboard Enter and touch on the target devices.
3. Complete both walking goals and land on the box; compare their fading glows.
   Move and jump during later practice instructions; inspect the aim outline.
4. Focus the other offered cards while the Health card retains green guidance.
   Move between shop packs and confirm there is only one yellow focus border.
5. Browse other index packs/pages/layouts and previews, then follow green guidance
   back to the guided upgrade. Confirm unrelated spending cannot consume its budget.
6. Finish stage 2 without a Restart stage button. Reload during pack opening and
   confirm no duplicate charge/reward. Finish onboarding and equip the first item.
7. Compare first-wave hits-to-kill with fresh 8/8, post-tutorial 9/10 and developed
   collections. Check ground/flying/lobber HP, tank 3x HP, elites and Sabotage.
   Evaluate waves around 10/11, 15/16, 20/21, 30/31, 40/41 and 50/51.
8. Check intended shipping resolutions and run a target-platform build before release.
