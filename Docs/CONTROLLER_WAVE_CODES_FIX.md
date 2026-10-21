# Controller selection, wave one and codes — October 21, 2026

The single-player controller highlight follows the card's visible background.
Its button root was centered 175 units above that background. Upgrade buttons,
sliders and other controls continue to use their own bounds.

The default first wave again contains four normal ground enemies. Later introduction
waves still contain exactly one newly introduced variant. Wave one uses the existing
co-op scaling (four enemies per player); authored WaveDefinition overrides retain
their configured behavior.

Code `1111` adds one of all 12 items. Code `2002` unlocks every collectible card at
level 2 or higher and adds 1,000,000 gold. Both grants are repeatable. Existing higher
levels, ascensions, duplicate copies and equipped loadouts are preserved. The full
eight-code reference is in `Docs/CODES.md`.

| Changed file | Reason |
| --- | --- |
| `Assets/Scripts/ControllerMenuNavigation.cs` | Use the visible CardDisplay background as the highlight target for whole-card buttons. |
| `Assets/Scripts/WaveManager.cs` | Exempt normal ground enemies on wave one from the unique-introduction rule. |
| `Assets/Scripts/ShopManager.cs` | Add both codes, persist their rewards, refresh collection UI and cap additions safely at the integer limit. |
| `Assets/Scripts/Editor/CardReworkVerification.cs` | Add 13 isolated regression checks for grants, persistence, repeat use, preservation and overflow handling. |
| `Assets/Scripts/ItemUpdatePlayProbe.cs` | Update the introduction-wave regression expectation for the four-enemy first wave. |
| `Docs/ARCHITECTURE.md` | Document the first-wave exception and the expanded code list. |
| `Docs/CODES.md` | Provide all eight codes, entry instructions and exact effects. |
| `Docs/CONTROLLER_WAVE_CODES_FIX.md` | Record the changes, verification, setup and remaining manual checks. |

Verification completed:

- Static diff review and `git diff --check` passed.
- Unity 6000.0.35f1 compiled the changes successfully.
- `CardReworkVerification.RunGrantCodes()` passed all 13 checks using a separate temporary save.
- Both new codes were entered through the live DeveloperConsoleUI input/Submit path in isolated Play Mode. The resulting save contained one of every item and 1,000,000 gold, with collectible cards at level 2 or higher.
- The actual solo first wave spawned four normal ground enemies, with no elite enemies or remaining queued spawns.
- All three offered cards had highlight centers matching their visible backgrounds within 0.001 screen pixels at 1920×1080. A screenshot was visually inspected. Virtual controller stick navigation moved selection, and Interact confirmed a card and closed the offer.
- 500 plans using the real scene's enemy prefabs (20 rolls each for waves 1–25) retained their expected counts and unique later introductions.
- Eight checks exercised the real wave-start co-op expansion for waves 1, 3, 6, 9, 12, 16, 20 and 25 with four-player scaling. Wave one had 16 ground enemies; each later introduction remained unique.
- No new Unity console errors were recorded during these checks. The prior console error was an older Unity tooling timeout.
- Play Mode was stopped, the virtual controller removed, the temporary save override cleared, and MainMenu left open without unsaved scene changes.

No additional Unity Inspector, scene or prefab setup is required. No serialized
fields, scene assets, prefab assets, package versions or project settings were changed.
Tests used isolated saves and did not redeem the codes on the real account.

Remaining manual Play Mode checklist:

1. Start a solo run with your physical controller and confirm wave one contains four normal ground enemies.
2. Reach a card offer; move across all three cards and confirm the border remains centered before selecting one.
3. In main-menu Settings, enter `1111` under Codes, choose Apply, and check that each inventory count increases by one.
4. Enter `2002`; confirm gold increases by 1,000,000 and all collection cards are at least level 2, including after restarting.

Physical-controller hardware, other display resolutions, a complete co-op run and
standalone/WebGL builds were not tested. The full item-effect regression suite was
not rerun; its updated first-wave expectation was checked through the focused wave
plan tests above.
