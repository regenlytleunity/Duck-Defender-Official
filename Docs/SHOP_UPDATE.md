# Shop 2.0 implementation and verification

Implemented from `Duck Defender Update (4).pdf` and the confirmed loadout rules.
The shop now has pack, item and permanent-upgrade pages. The index and co-op
lobby support four consumables and up to five starting cards. Eight consumables,
persistent upgrades, enemy item drops, bulk opening and Simple/Advanced hosting
are integrated with the existing gameplay and save owners.

## Confirmed behavior

- One consumable per equipped slot; inventory is deducted only on use. Unused
  copies survive quitting or death. Participating co-op seats reserve copies
  from the host's shared inventory.
- Starting cards must be unlocked, non-Basic, and unique within one player's
  loadout. They use the saved collection level/ascension and are excluded from
  that player's subsequent run offers. Different players may select the same card.
- Simple co-op hides and disables consumable/starting-card loadouts for that
  run while retaining selections. Permanent stat upgrades still apply.
- PC: 1–4 select, right-click uses. Controller: D-pad up/right/down/left selects
  slots 1/2/3/4; RB uses. Mobile: tap a slot, then Use. Those controls are reserved
  against conflicting gameplay rebindings. D-pad remains available in menus.
  Throwable items now preview their path while Use is held and launch on release.
  Changing slots or pausing cancels a pending throw without consuming it.
- Shields and banners stay where placed. Overlapping banners do not stack.
  Bomb radius is 4 world units; shield radius is 2.5 and banner radius is 5.
  Shield/flames last 30 game seconds. Casino/clones expire after two completed
  waves; banners expire after ten. Pausing also pauses timed effects.
- Casino coin/XP penalties affect the shared team economy. Other casino effects
  belong to their user. A later casino result replaces that user's earlier result.
- Shop rotation uses UTC ten-minute boundaries, persists stock/sold status and
  rejects purchases against an expired displayed rotation.
- Health retains the project's integer HP model, so percentage upgrades are
  accumulated and rounded by the existing health calculation.

## Changed files and purpose

Paths below are relative to the project root. Every new C# file also has a
Unity-generated `.meta` file of the same name; no GUIDs were authored manually.

| Existing file | Reason |
| --- | --- |
| `Assets/Scripts/PlayerData.cs` | Add normalized item inventory, loadouts, permanent upgrades, shop rotation, completed-run and discovery fields; keep save version 2. |
| `Assets/Scripts/SaveSystem.cs` | Normalize new fields on existing save loads without resetting current progression. |
| `Assets/Scripts/ShopManager.cs` | Extend existing manager through a partial class and gate one/three/six-pack purchases. |
| `Assets/Scripts/MainMenuUI.cs` | Initialize the new shop UI; bulk unlock controls, centered pack/reveal layouts, faster animations and conditional essence labels. |
| `Assets/Scripts/CardDisplay.cs` | Expose the displayed card and connect collection-card equipment interactions while preserving upgrade controls. |
| `Assets/Scripts/CardIndexUI.cs` | Bind loadout interactions after page generation. |
| `Assets/Scripts/HostMenuUI.cs` | Add Simple/Advanced loadout option. |
| `Assets/Scripts/CoopLobbyUI.cs` | Add per-seat item/starting-card equipment panes and controller navigation. |
| `Assets/Scripts/ControllerBindings.cs` | Reserve D-pad/RB for items; use analog movement and preserve D-pad menu navigation. |
| `Assets/Scripts/InputManager.cs` | Reserve 1–4/right-click and resolve conflicting old keyboard mappings. |
| `Assets/Scripts/SettingsMenuUI.cs` | Explain and reject reserved item-key bindings. |
| `Assets/Scripts/Inputhelper.cs` | Route item controls and clone AI through the existing input abstraction. |
| `Assets/Scripts/LocalCoopSession.cs` | Initialize per-player loadouts after Start, count completed waves/runs once, and distinguish clones from real players. |
| `Assets/Scripts/CardManager.cs` | Include permanent luck in existing rarity calculations. |
| `Assets/Scripts/PlayerStats.cs` | Integrate permanent/casino/banner modifiers and clone snapshot data. |
| `Assets/Scripts/WeaponPlayer.cs` | Apply item/permanent/clone fire-rate multipliers. |
| `Assets/Scripts/PlayerController.cs` | Preserve copied card stats when initializing a clone. |
| `Assets/Scripts/PlayerHealth.cs` | Separate clone death/HUD behavior and record solo run completion. |
| `Assets/Scripts/LevelManager.cs` | Advance wave-based item durations, apply casino XP penalty and grant exactly one level for the XP potion. |
| `Assets/Scripts/EnemyBase.cs` | Item drops, casino coin suppression, clone targeting and explicit item damage through spawn protection. |
| `Assets/Scripts/EnemyProjectile.cs` | Block swept enemy projectiles at shields. |
| `Assets/Scripts/BouncyEnemyProjectile.cs` | Block lobbed/bouncing enemy projectiles at shields. |
| `Assets/Scripts/TurretManager.cs` | Clean up owned companions when a clone/player manager is destroyed. |
| `Docs/ARCHITECTURE.md` | Document the implemented shop/loadout/runtime/save changes. |
| `Docs/SHOP_UPDATE.md` | This file-by-file report and test/setup guide. |

| New source file (plus matching `.meta`) | Reason |
| --- | --- |
| `Assets/Scripts/ItemCatalog.cs` | Stable item IDs, prices/descriptions and procedural placeholder icons. |
| `Assets/Scripts/ShopManager.Upgrades.cs` | Purchases, rotation, reservations, sales, loadouts and permanent upgrades within the existing manager. |
| `Assets/Scripts/ShopUpdateUI.cs` | Runtime shop tabs, offer grid, permanent-upgrade controls and six-pack option. |
| `Assets/Scripts/IndexLoadoutUI.cs` | Fifth index tab and vertical starting-card column. |
| `Assets/Scripts/LoadoutPanel.cs` | Shared item/card equipment UI for solo and co-op. |
| `Assets/Scripts/LoadoutInteraction.cs` | Right-click, drag/drop, slot swaps and drag-out unequip. |
| `Assets/Scripts/PlayerItems.cs` | Consume/save items, apply loadouts/permanent bonuses and manage casino effects. |
| `Assets/Scripts/ItemWheelUI.cs` | Four-way selection HUD, casino wheel/result and mobile controls. |
| `Assets/Scripts/ItemArea.cs` | Bomb, shield, banner and map-wide flame effects. |
| `Assets/Scripts/ItemPickup.cs` | Gated enemy drops and proximity collection into persistent inventory. |
| `Assets/Scripts/ItemClone.cs` | Two temporary AI clones with copied card build, health, half fire rate and proximity control. |
| `Assets/Scripts/ShopUpdatePlayProbe.cs` | Editor-only isolated gameplay/UI interaction verification using synthetic controllers. |
| `Assets/Scripts/Editor/ShopUpdateVerification.cs` | Isolated economy/save checks, UI font/bounds/pointer checks and verification entry points. |

## Unity setup and serialization

No additional Inspector, scene or prefab setup is required for this implementation.
The runtime UI extends the existing MainMenu and gameplay scene references. New
text uses the existing `DuckDefenderTestFontv2.asset` with white infill; mini cards
reuse the authored card border. Item icons, banner and shield visuals are
placeholders. Flamethrower now reuses the existing FireTrail prefab on the ground. Final artwork and visual tuning remain the user's work.

No existing serialized fields were renamed, and no scene, prefab, ScriptableObject,
ProjectSettings or package files were edited. New progression fields extend the
existing JSON save. Current-version saves retain their coins/cards; the existing
pre-version-2 migration behavior is unchanged. New runtime components are created
by the existing owners rather than requiring manual attachment.

## Verification

Completed on October 11, 2026, using the connected Unity 6000.0.35f1 Editor:

- Successful Unity compilation; no script compilation errors.
- **79 isolated economy checks passed:** current-version save preservation,
  upgrade prices/caps, bulk unlocks, sold-stock persistence, UTC rollover, stale
  purchases, insufficient funds, item reservations/sales and card restrictions.
- **88 isolated Play Mode gameplay checks passed:** starting cards and offer
  exclusion, permanent modifiers, one-use consumption, healing, casino outcomes
  and regeneration, wave expiry, banner bonuses, shield enemy/projectile blocking,
  clone health/fire rate/death/expiry, off-screen flame damage, bomb kills,
  proximity pickups, mobile slot/Use callbacks, exact level gain, per-player
  synthetic controller input, Simple mode and unused inventory after team defeat.
- **81 UI interaction checks passed**, with nested white-font, text-fit and
  pointer-raycast checks: all shop tabs, inventory/context menus, equipped-slot
  drag/swap/unequip, starting cards, one/three/six/one pack purchases and complete
  reveals, card bounds within grid cells, and four-player equipment navigation.
- Captured and visually reviewed pack layouts, eighteen-card reveal, four-player
  lobby and mobile/item HUD. Visual review found and corrected overlapping reveal
  rows that text overflow checks alone did not detect.
- **Non-development WebGL build succeeded: 0 errors, 4 warnings.** Output:
  `C:/Users/regen/AppData/Local/Temp/duck-coop-web-20261011-200221`.
- `git diff --check` passed. All 13 new scripts have Unity-generated `.meta`
  files. Generated test screenshots were removed from Assets through AssetDatabase.
- Editor was returned to MainMenu in Edit Mode; temporary-save isolation was
  cleared and the existing verification harness restored saved preferences.

The four build warnings are: no Pipeline runtime configuration (Editor tooling
is disabled in the player), the existing unused `_isCoinShotActive` field, and
one missing-script component entry each on `CardManager` and `Tilemap_Background`
in SampleScene. Read-only scene inspection confirmed that the working CardManager
component remains attached alongside the missing entry. These scene files were
unchanged by this update; the missing entries were not removed or guessed at.

Detailed gameplay/UI logs and final screenshots are retained under
`C:/Users/regen/.codex/visualizations/2026/10/11/01a12d40-382b-7190-ba8a-95579ab189c4/shop-update`.

The opt-in verification entry point is `ShopUpdateVerification.Start()` (also
available as **Duck Defender → Shop 2 → Start isolated verification**). Once the
isolated MainMenu is running, use `CheckEconomy()`, `StartGameplay()` or `StartUI()`
through Unity CLI evaluation. The tests explicitly refuse to run without the
existing temporary-save override. Leaving Play Mode restores saved preferences.

## Hands-on Play Mode checklist

1. Open all three shop pages. Buy one item, confirm Sold Out, reopen the game,
   and confirm the offer remains sold until the UTC countdown expires.
2. Unlock starting slots and bulk opening. Open one, three and six packs, then
   one again. Check centering, complete reveals and Continue with mouse/controller.
3. Equip, swap, drag out and sell items in the index. Equip/unequip starting cards
   and confirm original collection entries and upgrade controls remain usable.
4. Start solo. Verify the equipped starting-card level/ascension, permanent stats,
   selection wheel and consumption. Quit with unused items and check inventory.
5. Exercise every item in an ordinary wave: aim a bomb, heal, block enemies and
   both projectile types, stand inside/outside a banner, watch casino results,
   watch clones navigate terrain and die/expire, level up, and burn off-screen enemies.
6. Host Advanced with physical controllers. Equip different loadouts per player;
   try reserving the same last copy from two seats. Confirm each D-pad/RB controls
   only its player. Switch to Simple and verify saved selections remain afterward.
7. On a real mobile/browser device, verify slot taps/Use, movement and shooting
   together, portrait/landscape behavior where supported, and persistence after reload.
8. Play two completed games on a fresh test profile. Confirm drops are then
   possible, the first successful drop is a Bomb and magnet upgrades do not attract it.

## Remaining verification limits

Synthetic input and Editor Play Mode do not establish physical-controller feel,
mobile-device usability, long-session balance or clone navigation quality across
every terrain/card combination. The 0.5% drop roll is code-reviewed; a statistical
drop-distribution test and a real ten-minute wall-clock soak are not substitutes
for the deterministic rollover/persistence checks. Browser-hosted WebGL behavior
and IndexedDB persistence require a deployed/browser run. No deployment is part
of this update.
