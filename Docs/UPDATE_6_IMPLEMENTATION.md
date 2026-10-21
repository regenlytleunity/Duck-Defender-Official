# Duck Defender Update (6) — implementation and verification

Implemented from the three-page `Duck Defender Update (6).pdf` supplied by the user. Verification completed October 21, 2026, in Unity 6000.0.35f1.

## Result

| Item | Cost | Behavior |
| --- | ---: | --- |
| Bomb | 100 | One high arc mirrored left/right, independent of vertical aim. Preview and flight share collision tracing. Terrain or enemy contact detonates an instant-kill radius. |
| Health Potion | 100 | Full heal; existing behavior retained. |
| Experience Potion | 100 | One level; existing behavior retained. |
| Sawblade | 250 | Up to four evenly spaced blades orbit the cursor. Each deals one contact damage to an enemy at most once per full orbit. Persists until death. A fifth blade is not consumed. |
| Shock | 250 | All living enemies receive a ten-second stun and damage equal to 50% of maximum health. |
| Battle Banner | 250 | Nearby players receive +15% damage and fire rate for ten completed waves; existing behavior retained. |
| Shield | 250 | Unbreakable for two completed waves. Expels enemies inside and blocks enemy movement/projectiles. |
| Pocket Casino | 500 | Positive outcomes last ten completed waves; negative outcomes last two. |
| Clone | 500 | One AI clone for five completed waves. Copies the current card build, health, full damage/fire rate, permanent bonuses, and effective movement. Later owner upgrades do not change the snapshot. |
| Flamethrower | 500 | Map-wide damage of 10% enemy maximum health per second for three completed waves. Reuses the existing fire particles. |
| Divine Intervention | 1,000 | Suspends card effects for two completed waves, grants flight and 10 HP/second regeneration, and replaces ordinary shooting with lightning. Afterwards restores the saved build and applies cards selected during the effect exactly once. |
| Nuke | 1,000 | Falls from above, instantly defeats all enemies, then creates green fire-particle radiation that slows enemies by 50% for ten completed waves. Includes enemies spawned during radiation. |

The project's `FireRate` is a shot interval. Divine's “90% reduced fire rate” is implemented as **90% less interval**, using the baseline before starting cards: the current scene's 0.5 seconds becomes 0.05 seconds. Lightning hits the aimed target for 50% maximum health, then up to five additional nearby enemies; each hop uses 90% of the preceding damage fraction (50%, 45%, 40.5%, …). Other item buffs and permanent upgrades are not cards. Ordinary item damage uses the existing tank protection/shield rules; instant defeat uses the existing death/reward path.

Wave durations count wave completions after activation. Reusing Divine refreshes its two-wave duration without replacing the original saved build. Temporary enemy statuses already inflicted keep their normal expiry, while ongoing damage from suppressed cards is gated. Existing temporary card buffs also retain their normal clocks.

Additional fixes:

- Co-op equip/unequip refreshes availability counts in every loadout pane without dismissing another player's open menu.
- Tank chain alpha is 40% of its previous value, for both authored and fallback lines.
- Each automatic normal/elite introduction wave contains exactly one new variant, including co-op. Explicit custom WaveDefinition groups/pools retain their authored behavior.
- Keyboard and controller settings include Use Item and all four item slots. Formerly reserved controls can be remapped. Duplicate controls are rejected within the gameplay or menu group; menu and gameplay can share buttons.
- Controller sliders require Interact to enter editing; the left stick adjusts them, and Interact or Back exits. Editing focus is green.
- Ascended card backgrounds slowly cycle a subtle chromatic tint; text stays white.
- Three-pack opening has larger pack images and a five-column/two-row card reveal with a centered second row. Six packs retain a six-column/three-row reveal.

## Files changed by this task

The working tree already contained other edits. This list is based on comparison with the files captured at the start of this task, rather than attributing all existing Git changes to this update.

All script paths below are under `Assets/Scripts/`.

| File | Why it changed |
| --- | --- |
| `ItemCatalog.cs` | Append four stable item IDs, update costs/descriptions/name, and wrap placeholder icon hues for the expanded catalog. |
| `PlayerData.cs` | Expand and normalize old inventories without changing existing IDs or balances. |
| `ShopManager.Upgrades.cs` | Sanitize reservations against the expanded catalog. |
| `PlayerItems.cs` | New item activation, sawblade orbit/hit limits, Shock, Divine snapshot/restore/deferred cards, casino durations, single clone, and shared bomb preview. |
| `ItemArea.cs` | Bomb contact tracing, wave-based shields/flames, percentage damage, falling nuke, radiation slow and reused particles. |
| `ItemClone.cs` | Five-wave clone lifetime. |
| `PlayerStats.cs` | Card suppression and full current clone damage/fire-rate snapshots. |
| `EnemyBase.cs` | Fractional item damage and off-screen item stun; prevent suppressed card damage. |
| `WeaponPlayer.cs` | Divine firing interval/lightning route, firing-clock reset, reusable fallback lightning visuals. |
| `PlayerController.cs` | Divine flight and cancellation of active card movement when suppression begins. |
| `PlayerHealth.cs` | Temporarily rescale maximum/current health while retaining the underlying permanent/card health bookkeeping. |
| `CardManager.cs` | Record cards picked during Divine immediately and defer their effects until restoration. |
| `AscensionEffects.cs` | Suspend defender walls, identify healing-orb ownership, and gate delayed eruptions. |
| `AscensionArea.cs` | Suspend damage/healing/pull while the source's cards are disabled. |
| `FireTrailPatch.cs` | Suppress ongoing card fire damage. |
| `HealingOrb.cs` | Prevent a suppressed source's healing orb from applying its card effect. |
| `Projectile.cs` | Silently remove projectiles from a suppressed card source. |
| `Meteor.cs` | Cancel suppressed card meteors and their explosions. |
| `TurretBase.cs` | Pause card companion actions during suppression. |
| `LevelManager.cs` | Skip suppressed players when distributing queued card meteors. |
| `WaveManager.cs` | Mark introduction spawns and exclude the introduced variant from random filler and co-op duplication. |
| `TankEnemy.cs` | Translucent protection-chain visuals. |
| `CoopLobbyUI.cs` | Subscribe to collection changes and refresh every pane's availability text. |
| `LoadoutPanel.cs` | Update counters in place and tint ascended mini-cards. |
| `AscendedCardTint.cs` | New runtime component for the slow chromatic background tint. |
| `CardDisplay.cs` | Apply the tint to eligible ascended cards. |
| `MainMenuUI.cs` | Separate three-pack sizing/reveal layout and create the required additional pack images. |
| `ControllerBindings.cs` | Five item actions, display names, formerly reserved remapping, and one-time compatibility migration. |
| `InputManager.cs` | Persist/reset keyboard item bindings and migrate the previous reserved-control rule once. |
| `Inputhelper.cs` | Read remapped item selection/use through the existing input abstraction. |
| `SettingsMenuUI.cs` | Include keyboard item controls and permit remapping former item keys. |
| `ControllerBindingsUI.cs` | Fit keyboard/controller item controls into the existing settings panel and allow Start to be captured. |
| `ControllerMenuNavigation.cs` | Interact-to-edit sliders and editing focus feedback. |
| `ItemWheelUI.cs` | Show the actual saved item-slot bindings. |
| `Editor/UIUpdateSetup.cs` | Keep the existing menu setup helper compatible with the larger keybind list. This helper was not run to rewrite scenes. |
| `Editor/UIUpdateVerification.cs` | Snapshot/restore the new binding preferences during isolated verification. |
| `Editor/ShopUpdateVerification.cs` | Seed all twelve items for isolated checks. |
| `ShopUpdatePlayProbe.cs` | Update legacy clone, flame, bomb, and reveal expectations to the new requirements. The entire older suite was not rerun. |
| `ItemUpdatePlayProbe.cs` | New editor-only isolated integration probe for this update. |

Unity generated `Assets/Scripts/AscendedCardTint.cs.meta` and `Assets/Scripts/ItemUpdatePlayProbe.cs.meta`; both were verified present. No existing meta files were edited by this task.

Documentation:

- `Docs/ARCHITECTURE.md`: updated current item, input, introduction-wave, tint, and pack-layout facts.
- `Docs/UPDATE_6_IMPLEMENTATION.md`: this implementation, file list, verification, and handoff.

Local verification artifacts are under `tmp/item-update/`: `verification.txt`, `extra-verification.cs`, `extra-verification.txt`, `implementation.diff`, and screenshots. They are diagnostics rather than Unity assets.

## Verification performed

- Unity compiled the implementation successfully in the open 6000.0.35f1 Editor.
- The new isolated Play Mode suite passed **208 assertions**. It confirmed actual advancing frames, old inventory expansion, item consumption/prices, co-op count refresh, keyboard/controller mappings, white original-font controller text, slider behavior, pack sizes/reveals, chromatic tint, repeated introduction plans, bomb contact, Shock duration/damage, wave expiry boundaries, clone stat snapshots, flame percentage damage, Divine suppression/regeneration/lightning/restoration/deferred cards, nuke/radiation, and sawblade spacing/hit limits/death cleanup.
- **11 focused Play Mode checks** passed using the real enemy prefabs and the real wave-start plan expansion: all eight four-player introduction waves remain unique, and both chain rendering paths have the intended alpha.
- Existing visible-UI verification passed **182 assertions** for the twelve-item inventory, **91** for new shop offers, and **37** for the Divine item menu. These inspect font/color, text fit, and interactive controls.
- Verified MainMenu uses `Assets/Fonts/DuckDefenderTestFontv2.asset`. The probe confirmed the new UI's white infill. No font/atlas/material was regenerated.
- Inspected captured controller settings, three-pack/six-pack reveals, inventory, and item-description views. New item icons use the existing procedural placeholder approach.
- Checked the Console during verification: no new gameplay runtime errors or compiler errors. A historical tooling timeout was already present; one later inspection expression also referenced gameplay objects after the death test had returned to MainMenu. Neither was a game-code failure.
- `git diff --check` passed. Existing unrelated changes were preserved. No commit or push was made.
- A final visual review caught an out-of-range hue in the expanded placeholder catalog. Wrapped the hue and verified all twelve icons contain visible colored pixels; Unity compilation still passed afterwards.
- Tests used a separate verification save. The existing verification harness restores binding/audio/difficulty preferences on leaving Play Mode.
- Finished with Play Mode stopped, the verification-save override cleared, and the active scene clean.

The first two probe attempts exposed hard-coded test assumptions about difficulty-scaled enemy HP and the scene's initial fire interval. Assertions were corrected to use actual maximum health and the actual baseline interval; the complete subsequent run passed.

## Unity Inspector and scene/prefab setup

**No additional Inspector, scene, or prefab setup is required for the current project.** New UI and item behaviors attach through the existing runtime owners. The existing fire-trail prefab successfully supplied the flame/radiation particle visuals in Play Mode. Lightning has a runtime fallback when `WeaponPlayer.ElectricChainEffectPrefab` is unassigned.

No scene, prefab, ScriptableObject, package version, build profile, or ProjectSettings values were intentionally changed by this task. Existing serialized names and the original eight item IDs are preserved. New keyboard binding fields have defaults and are initialized from PlayerPrefs; older inventories expand from nine entries (including empty ID zero) to thirteen. Migration keys are `Key_ItemBindingsVersion` and `DuckDefender_Controller_ItemBindingsVersion`.

The four new item icons and sawblade/nuke representations are placeholders, consistent with the current item system. Final sprite artwork and visual tuning remain the user's visual-design work; no new art assets were generated.

## Play Mode acceptance checklist

1. In MainMenu, buy/equip the new items and inspect their descriptions. In a two- or four-player advanced lobby, leave one player's item menu open while another equips/unequips; all availability counts should update.
2. Remap Use Item and a slot on keyboard and controller, reopen settings, and confirm they persist. Browse past sliders without changing values; press Interact, adjust with the left stick, then press Interact again.
3. Open three packs and six packs. Check readability, centered rows, and Continue access at your normal window size. Inspect an ascended card's subtle color cycle.
4. Throw bombs while aiming high/low on both sides. Compare the arc and verify terrain/enemy contact detonates. Check shield expulsion and projectile blocking.
5. Exercise positive/negative casino effects and the shield/clone/flame expiry boundaries. Compare cloned fire rate/damage before and after upgrading the owner.
6. Use Divine with a varied card build, including movement, health, companions, and ascensions. Confirm flight/lightning/regeneration, pick a card during it, and complete two waves to verify restoration. Check this in ordinary co-op play as well.
7. Use Nuke with off-screen enemies and observe newly spawned enemies slowed during the next ten waves. Add four sawblades, confirm repeated contact is limited per orbit, and verify death clears them.
8. Observe the normal/elite introduction waves and translucent tank links during a regular run.

## Not verified

No standalone or WebGL player build was produced. Real hardware controller feel, touch-device input, every card/item combination, long-session performance, and all screen aspect ratios still need normal acceptance testing. The automated checks cover representative card suppression/restoration and scripted virtual-controller interactions, not exhaustive gameplay balance or visual approval.
