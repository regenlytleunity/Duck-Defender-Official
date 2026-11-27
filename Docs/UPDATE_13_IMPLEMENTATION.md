# Duck Defender Update 13 implementation

Implemented from Duck Defender Update (13).pdf, with the explicit user correction
that a maxed-card daily reward grants the current maximum, **level 6**.

## Implemented behavior

- Four persistent items, levels 1-10, item-specific shards and the exact shard/gold
  upgrade table. Duplicate boxes grant ten shards. Maxed items have half the relative
  draw weight; all-maxed odds are equal again. Two equipped slots per player.
- Reusable items with the specified charges/cooldowns and all forty level effects.
  Cooldown starts after the last charge and refills the full charge count. Bombs
  follow aim plus an upward impulse and gravity. Level 10 produces two primary
  impacts, both with minis and radiation. Shields follow the player. Banners use
  timed effects and the level-10 leave-area retention. XP uses the full next-level
  cost, independently of how much XP was already earned toward it.
- One-key item boxes with zoom, three to five hits, shake, lid flight and reveal.
  Rewards save before the animation. Existing item icons and placeholder box art
  are used. No replacement visual assets were generated.
- Ordinary/elite enemy keys at 0.5%/0.7%, five permanent key-drop upgrades, rare
  Gadget Key Finder and Key Specialist. Bonuses are relative and additive, giving
  combined maximum increases of 50% before ascension and 70% afterward.
- Three unique daily objectives, the full reward table, one additional guaranteed
  bonus key, claim-once state, blue/green/grey bars, timer and card reveal integration.
  Maxed-card rewards exclude maxed cards, clear duplicate copies and never ascend.
  An exhausted eligible rarity pool grants no card; its bonus key still applies.
- All ascensions cost ten keys. Essence acquisition/storage/UI and obsolete codes
  are retired. Maxed card pack weight is halved. Starting-card and bulk-pack upgrades
  remain; basic-stat permanent upgrades are replaced by the key-drop upgrade.
- Old item tutorial disabled. New codes are 1301 (reset daily quests), 1302 (+1000
  keys), 1303 (max all items), 1304 (unlock all items). See CODES.md for all codes.
- Progression version 3 intentionally resets older account data once, including
  cards/currency/items and tutorial progress. PlayerPrefs settings are separate.

## Implementation choices where the brief leaves details open

- EST is fixed UTC-5: daily reset is 05:00 UTC year-round, without daylight-saving
  adjustment. The local device clock supplies time.
- Bomb radius is 2 units initially, rising to 3 and 4. Mini-bomb radius is 1.5.
  Banner radius is 3, rising to 4. Shield radius is 2.5, rising to 3.5. Potion
  knockback checks a 5-unit radius. These are code tuning values in ItemArea.
- Item Info has five vertical rows per page (levels 1-5 and 6-10), initially showing
  the current-level page. This keeps all descriptions readable in the existing UI.
- Banner buffs use the strongest overlapping effect. Co-op XP is shared, so the
  potion's temporary 10% enemy-XP boost benefits shared XP and does not stack.
- Stable old IDs and inactive source APIs remain for existing serialized references
  and historical editor probes. Retired items cannot be bought, drawn, equipped or
  activated through the current gameplay/UI. The obsolete inventory/essence/stat
  fields are excluded from new save JSON. Historical item probes are superseded.

## Verification actually performed

**Static review and compilation**

- Reviewed the PDF, existing owners/callers and final diffs; compared all upgrade
  effects, costs, rewards, weights and drop formulas with the brief.
- Unity 6000.0.35f1 imported the new scripts/assets and generated their .meta files.
  Unity compilation completed successfully with no compile errors.
- Git whitespace validation passed. No package, project-setting, build-target,
  prefab-art or existing .meta changes were required.
- The Unity CLI workflow used the using-unity/unity-cli skills and their
  integration-advanced and playmode-verification-loop guides. PDF extraction and
  rendered brief pages were used to inspect requirements/layout sketches.

**Isolated Play Mode**

All test runs used temporary save profiles through the repository's existing
UIUpdateVerification isolation. Simulated controllers and preference snapshots
were restored when leaving Play Mode. Frames were confirmed advancing.

- Repeated ItemReworkVerification.CheckEconomy passed: old-version reset, first
  unlock/duplicate shards, every upgrade cost through level 10, caps, equip moves,
  co-op shared unlocks, key upgrades, unique objectives, bonus key, claim-once,
  EST boundary, ten-key ascension and save/reload persistence.
- Additional checks passed for retired-code rejection, retired fields absent from
  JSON, max-card reward at level 6 without ascension, and an exhausted reward pool.
- Bomb charges 3 -> 2 -> 1 -> 0, cooldown start only at zero, blocked use, refill,
  unchanged equipment and no key consumption passed. Cooldown expiry was advanced
  in the isolated test to check refill without waiting forty seconds.
- Physical bomb contact defeated a test enemy. Level-10 bounce produced two primary
  explosions and four minis. Radiation delivered three 5%-max-health ticks; tick
  deadlines were advanced in the isolated test to check the expiration boundary.
- The max healing potion healed 75% of maximum health and its shield blocked a
  crossing projectile segment. Banner bonuses/reduction, normal radius exit,
  level-10 retention and expiry passed. XP grant, boost, level-up and ten-second
  movement-buff state passed.
- Key collection granted once, set the specialist damage timer, and survived the
  subsequent coin-save flush. Key Specialist applied through an equipped starting
  card only to its assigned player.
- Simulated D-pad left/right selection, no D-pad movement, held bomb trajectory and
  release spending exactly one charge passed. Two-player co-op selection/use spent
  only the acting player's charge. Simple mode suppressed equipment while preserving
  saved loadouts.
- Editor-simulated touch passed two-slot selection, potion use and bomb hold/release.
- Box opening charged one key across repeated hits and revealed duplicate shards.
  Sixteen-card daily rewards displayed and returned through the existing reveal flow.
- UI checks passed for original font/white infill, overflow, pointer targets and
  controller reachability on the main menu, item shop/index, upgrade/Info dialogs,
  reveal and keyboard/controller bindings. The final binding checks passed 168 and
  142 assertions respectively. Main menu/item/reveal screenshots were inspected.
- Fixed a stale daily-claim navigation link, preserved modal return focus and ensured
  the third radiation tick occurs before cloud expiration.
- Final verification produced no new Unity console errors. Earlier diagnostic entries
  were retained rather than clearing the user's Console.

**Scene and asset verification**

- Key Finder is registered once in MainMenu ShopManager (48 collection cards) and
  once in SampleScene CardManager (52 total including four Basics).
- Removed two pre-existing empty missing-script components from SampleScene:
  CardManager and Grid/Tilemap_Background. Their script GUIDs were absent from the
  repository and the stale entries were confirmed in HEAD before removal.
  Removal and scene saves used Unity Editor APIs, with no raw YAML edits.
- No additional runtime component attachment is required for boxes or daily quests;
  ShopUpdateUI creates the new UI component through the existing startup path.

## Remaining Unity Inspector/art setup

The feature is usable with its placeholders. To supply finished artwork:

1. Open MainMenu. Select the object containing MainMenuUI. Assign **Item Box Body**
   (body without lid) and **Item Box Lid** (separate lid sprite). Both preserve aspect
   ratio. Save the scene. Null fields retain the placeholder box.
2. Open SampleScene. Select the object containing GameUI. Assign **Key Icon**.
   Save the scene. Null retains the gold-circle key placeholder.
3. Select `Assets/Cards/Upgrades/Gadget Upgrades/Key Finder.asset`. Assign **Icon**
   and **Ascended Icon** for Key Finder/Key Specialist. Their current values are null.
4. Retain the original DuckDefenderTestFontv2 UIFont assignments in both UI owners.
   Do not regenerate its atlas/material. No item prefab, collider, tag, layer-mask,
   animation or additional scene-object wiring is needed for these implemented effects.

Existing card assets' serialized AscensionCost fields are retained for compatibility;
they are explicitly marked legacy and all live pricing uses ten keys. No field was
renamed. New enums were appended, and all existing GUIDs were preserved.

## Manual Play Mode checklist and unverified scope

- Finish or skip the core tutorial. Verify dailies appear and the Items tab is
  available without triggering the retired item lesson. Use 1302/1304, open boxes,
  upgrade an item and browse both Info pages. Use 1303 for maximum-level comparisons.
- Equip each item pair, play several real waves and judge bomb aim/bounce, knockback,
  shield positioning, banner readability and cooldown pacing. Compare base and max
  levels. Check keyboard, a physical controller and a real touch device.
- Complete/claim objectives, verify the extra key, close/reopen the game and test the
  next-day reset. Check partial progress across midnight and claimed/empty reward pools.
- Play co-op with physical controllers, including disconnect/reconnect, respawn and
  Simple/Advanced loadouts. Four-player play was not exercised in this update.
- Check the finished artwork at the intended aspect ratios and small screens.
- Produce and run the intended standalone/WebGL build. No player build, deployment,
  browser IndexedDB persistence, real-device touch session, full-length balance test,
  all forty upgrades in live combat, or complete core-tutorial replay was performed.
  Those are separate from the successful compilation and targeted Play Mode checks.

No commit or push was made. The complete changed-file manifest follows.

## Every changed file and purpose

| File | Purpose |
| --- | --- |
| `Assets/Cards/Upgrades/Gadget Upgrades/Key Finder.asset` | New six-level rare Gadget card and Key Specialist ascension, authored through AssetDatabase. |
| `Assets/Cards/Upgrades/Gadget Upgrades/Key Finder.asset.meta` | Generated by Unity import; preserves the new asset/script GUID. Not hand-authored. |
| `Assets/Scenes/MainMenu.unity` | Registered Key Finder in ShopManager through the Unity Editor. |
| `Assets/Scenes/SampleScene.unity` | Registered Key Finder in CardManager, serialized the new default key fields and existing input defaults, and removed two verified empty missing-script components using Editor APIs. |
| `Assets/Scripts/CardDefinition.cs` | Appended KeyDropChance/KeySpecialist enum entries without reordering existing values; marked the serialized ascension-cost field as legacy and made retired essence yields zero. |
| `Assets/Scripts/CardDisplay.cs` | Shows and validates the ten-key ascension cost. |
| `Assets/Scripts/CardIndexUI.cs` | Displays keys in the collection balance area. |
| `Assets/Scripts/CardManager.cs` | Applies Key Finder modifiers through the existing per-player card system. |
| `Assets/Scripts/ControllerBindings.cs` | Two item actions with left/right D-pad defaults and a one-time migration from the old default. |
| `Assets/Scripts/ControllerBindingsUI.cs` | Removes obsolete slot controls and repairs labels/callback indices in the existing keyboard/controller screens. |
| `Assets/Scripts/DailyQuests.cs` | New daily objective/reward state, weighted generation, fixed-EST reset boundaries and batched run progress. |
| `Assets/Scripts/DailyQuests.cs.meta` | Generated by Unity import; preserves the new asset/script GUID. Not hand-authored. |
| `Assets/Scripts/Editor/ItemReworkVerification.cs` | Editor asset registration and repeatable isolated economy regression checks. |
| `Assets/Scripts/Editor/ItemReworkVerification.cs.meta` | Generated by Unity import; preserves the new asset/script GUID. Not hand-authored. |
| `Assets/Scripts/Editor/UIUpdateVerification.cs` | Snapshots/restores the new binding-migration preference and retired bindings during isolated verification. |
| `Assets/Scripts/EnemyBase.cs` | Routes ordinary/elite kills to key drops and daily kill progress. |
| `Assets/Scripts/GameUI.cs` | Adds the optional dropped-key sprite field. |
| `Assets/Scripts/IndexLoadoutUI.cs` | Makes the Items tab available after the core tutorial, even before the first unlock. |
| `Assets/Scripts/Inputhelper.cs` | Reads exactly two item selection actions on keyboard/controller. |
| `Assets/Scripts/ItemArea.cs` | Implements level-dependent bomb physics, bounce/minis/radiation, timed following shields, potion knockback and banner persistence/reduction. |
| `Assets/Scripts/ItemCatalog.cs` | Defines the four available items, two slots, all level costs, charges, cooldowns and upgrade descriptions while retaining stable legacy IDs. |
| `Assets/Scripts/ItemPickup.cs` | Repurposes enemy pickups as keys, adds ordinary/elite drop rates, relative bonuses and the Key Specialist collection buff. |
| `Assets/Scripts/ItemReworkMenuUI.cs` | New box opening/reveal and daily progress/claim UI using the existing font and navigation. |
| `Assets/Scripts/ItemReworkMenuUI.cs.meta` | Generated by Unity import; preserves the new asset/script GUID. Not hand-authored. |
| `Assets/Scripts/ItemWheelUI.cs` | Two-part wheel and touch controls, charge/cooldown display and key collection text. |
| `Assets/Scripts/LevelManager.cs` | Records play time/gold objectives, flushes pending quests, preserves key/quest saves and prevents direct XP integer overflow. |
| `Assets/Scripts/LoadoutPanel.cs` | Two-slot equipment, shard/gold upgrades, ten-level Info pages, current-level highlighting and modal return focus; key-priced ascension. |
| `Assets/Scripts/MainMenuUI.cs` | Replaces difficulty-description visibility, hides essence UI, reuses card reveals for daily rewards, manages box/reveal transitions and exposes optional box artwork. |
| `Assets/Scripts/PlayerController.cs` | Applies the XP-potion jump-height bonus through the square root of the impulse multiplier. |
| `Assets/Scripts/PlayerData.cs` | Version-3 progression, keys/item levels/shards/daily state and two-slot normalization; retired economy fields are no longer serialized. |
| `Assets/Scripts/PlayerHealth.cs` | Applies active banner damage reduction through the existing incoming-damage path. |
| `Assets/Scripts/PlayerItems.cs` | Reusable items, level snapshots, per-player charges/cooldowns, level-specific activations and movement objective progress; stops applying old permanent stat upgrades. |
| `Assets/Scripts/PlayerStats.cs` | Key Finder/Specialist state and damage buff; player-aware banner calculations. |
| `Assets/Scripts/SettingsMenuUI.cs` | Removes keyboard slots 3/4 from the displayed binding actions. |
| `Assets/Scripts/ShopManager.Items.cs` | New item-box/upgrade/key-upgrade transactions, daily claiming and codes 1301-1304 on the existing ShopManager. |
| `Assets/Scripts/ShopManager.Items.cs.meta` | Generated by Unity import; preserves the new asset/script GUID. Not hand-authored. |
| `Assets/Scripts/ShopManager.Upgrades.cs` | Keeps starting cards/bulk packs, disables retired stock/stat/sell services and supports shared reusable unlocks in co-op. |
| `Assets/Scripts/ShopManager.cs` | Half-weight maxed card draws, no essence conversion, ten-key ascension, pack objective progress and retired-code removal. |
| `Assets/Scripts/ShopUpdatePlayProbe.cs` | Updates existing banner-helper callers to the new player-aware signature so historical probe code still compiles. |
| `Assets/Scripts/ShopUpdateUI.cs` | Single item box and three permanent upgrades; removes rotating stock/timer and basic-stat upgrade controls. |
| `Assets/Scripts/TutorialDirector.Menus.cs` | Allows the Items tab after the core tutorial without the old acquisition prerequisite. |
| `Assets/Scripts/TutorialProgress.cs` | Disables the old item tutorial while preserving enum/checkpoint compatibility. |
| `Docs/ARCHITECTURE.md` | Replaces retired economy/item architecture with implemented version-3 behavior. |
| `Docs/CODES.md` | Documents all current codes and retired codes. |
| `Docs/UPDATE_13_IMPLEMENTATION.md` | This implementation, verification, setup and file manifest report. |
