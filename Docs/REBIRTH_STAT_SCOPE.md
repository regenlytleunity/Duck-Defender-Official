# Rebirth stat scope — September 26, 2026

Rebirth's +150% bonus now multiplies the **current upgraded values of eight core stats by 2.5**. This follows your clarification: base damage 10, upgraded to 20, becomes **50**. It replaces the earlier blanket boost to nearly every beneficial numeric stat.

## Runtime behavior

| Core stat | Rebirth calculation |
| --- | --- |
| Fire rate | Current firing interval / 2.5; existing 0.005-second minimum remains. |
| Feather size | Current feather scale × 2.5. |
| Health | Current upgraded integer maximum × 2.5, rounded to an integer. Rebirth fully heals it. |
| Feather count | Normal parallel and permanent spread counts × 2.5, rounded separately. Temporary and fixed special-projectile counts stay unchanged. |
| Movement speed | Current speed, including existing speed bonuses, × 2.5. |
| Acceleration | Current movement acceleration × 2.5. Braking remains unchanged. |
| Jump height | Jump impulse × sqrt(2.5), with gravity unchanged. This produces the intended 2.5 height factor under the same jump conditions. |
| Damage | Current feather damage × 2.5 after ordinary damage bonuses and ratios. Standalone non-feather upgrade damage stays unchanged. |

Integer values retain `Mathf.RoundToInt` rounding, including ties to the nearest even integer: 1 normal feather becomes 2, 2 become 5. Fractional damage uses the existing hit/tick rounding behavior. Later upgrades to the eight core stats also receive the multiplier. Health recalculation does not repeatedly multiply an already boosted maximum.

The once-per-run lethal-damage trigger, visible-enemy defeat, full heal, five-second protection and angel availability indicator remain in place. A new run starts without the active bonus.

## Upgrade interactions

Rebirth no longer directly scales:

- Turret action intervals, target counts, range, healing, shockwave radius or knockback; Defender wall health and Savior area settings.
- Special-feather shot thresholds; Airburst, Buckshot, Double Down or electric-chain counts; temporary Triple or Nothing bonus feathers and duration.
- Feather speed, piercing, ricochets, homing, critical chance, knockback, freeze duration, healing amount or explosion radius.
- Aura radius/strength, poison/toxin duration, toxin spread chance, missing enemy health, healing regeneration, beam dimensions or effect lifetime.
- XP, coin drops, passive income, magnet range or coin-trigger thresholds.
- Blink timing/distance, dash speed/cooldown/distance/count, jump count, flight timing, gravity or fire-trail settings.

Core-stat dependencies still behave normally. The minigun uses the faster core firing interval but retains the same heat capacity, recovery and slowdown curve. Feather-derived attacks still use increased feather damage at their existing ratios; health-derived effects still use the larger maximum health. Faster normal attacks can reach an unchanged special-feather shot threshold sooner in real time. These effects do not receive an additional multiplier on their upgrade settings.

## Unity Editor setup

**No additional Inspector, scene or prefab setup is required.** No serialized fields were renamed, no new scripts/components or artwork were added, and no save reset is required.

The existing `Assets/Cards/Upgrades/Survival Upgrades/Second Wind.asset` description was updated through Unity's AssetDatabase. Its ID, ascension settings and artwork references were preserved. The authoring defaults now use the same description.

After scripts import, select that asset and inspect **Ascended Description** to see the eight-stat scope. The targeted menu **Duck Defender → Card Rework → Update Rebirth Description** can reapply this description if needed; it has already been run for this project. Do not rerun the complete card-rework setup just for this change.

## Verification performed

| Check | Result |
| --- | --- |
| Unity compilation in the connected 6000.0.35f1 Editor | Passed, no compiler errors. |
| `CardReworkVerification.Run()` | Passed **1,357 checks**, including current damage 20 → 50, all eight core calculations, health rounding/recalculation, later health/size upgrades, unchanged projectile settings, aura values, special-feather trigger count, minigun heat threshold, profit duration and XP. |
| `LateGameFixVerification.Run()` | Passed **141 regression assertions**, including isolated physics. |
| `CardReworkVerification.RunPhysics()` | Passed **6 isolated preview-physics checks** for falling effects and Blink collision clearance. |
| `LateGameFixVerification.RunGround()` | Passed **3 saved-ground preview-physics checks**. |
| Asset/diff review | Rebirth asset changes only its ascended description. No scene, prefab, package or project-setting changes. All generic Rebirth boost helpers/callers were removed. |

Checks used transient objects, a temporary verification save and isolated preview physics. They did **not** enter Play Mode or exercise the complete live revival sequence. Jump-height verification checks the impulse-squared relationship; actual apex height and variable jump release still require Play Mode evaluation. No build or frame-time profiling was performed. Existing artwork, card-description layout and scene wiring were not visually revalidated.

## Play Mode checklist

1. Start a run in `Assets/Scenes/SampleScene.unity`, select Rebirth and some ordinary stat upgrades. Before revival, confirm Rebirth has not applied its bonus.
2. Take lethal damage. Confirm the visible-enemy defeat, full heal to the increased maximum, protection window and consumed angel indicator. After protection expires, a second lethal hit must end the run.
3. Compare normal damage, firing cadence, feather size/count, speed, acceleration and jump apex before/after revival. For damage, a current value of 20 should become 50. Compare jumps with the same button-hold behavior. Account for integer health/count rounding.
4. Acquire another core-stat upgrade after revival. Confirm it receives the multiplier once, particularly a health upgrade and Recovery+ health growth.
5. Compare turret timing/target counts, Savior healing, aura range, Airburst count, explosion radius, ricochets, freeze duration, coin drops, XP and magnet range. Their upgrade settings should stay unchanged. Check minigun slowdown/overheat/recovery at its increased firing rate.
6. Check Blink and Dash retain their distances and cooldowns, jump count stays unchanged, and a fresh run has no leftover Rebirth boost. Review the Console for runtime errors.

## Files changed

| File | Reason |
| --- | --- |
| `Assets/Scripts/PlayerStats.cs` | Replace generic boost helpers with explicit core multiplier; multiply final feather damage and current movement speed; stop scaling aura, profit duration and Second Wind timing. |
| `Assets/Scripts/PlayerHealth.cs` | Keep Rebirth maximum-health multiplier separate from ordinary upgrades, preserve current integer health rounding and leave regeneration unchanged. |
| `Assets/Scripts/PlayerController.cs` | Apply multiplier to movement acceleration and jump height; remove it from other movement/ability settings. |
| `Assets/Scripts/WeaponPlayer.cs` | Multiply current fire rate, size and normal count; preserve minigun heat, special thresholds, special counts and temporary bonuses. |
| `Assets/Scripts/Projectile.cs` | Preserve projectile upgrade values and electric-chain settings instead of applying a blanket bonus. |
| `Assets/Scripts/AscensionEffects.cs` | Preserve ascended ability dimensions, timing, healing, pull strength and wall health. |
| `Assets/Scripts/Coin.cs` | Preserve magnet range. |
| `Assets/Scripts/EnemyBase.cs` | Preserve sabotage, slows, poison/toxin settings and coin-drop multiplier. |
| `Assets/Scripts/LevelManager.cs` | Preserve passive income, XP and coin-trigger thresholds. |
| `Assets/Scripts/TurretBase.cs` | Preserve all turret action intervals. |
| `Assets/Scripts/MarksmanTurret.cs` | Preserve target count, target cap and targeting range. |
| `Assets/Scripts/MedicTurret.cs` | Preserve healing amount. |
| `Assets/Scripts/ElementalTurret.cs` | Preserve targeting range and target count. |
| `Assets/Scripts/ProtectorTurret.cs` | Preserve shockwave radius and knockback. |
| `Assets/Scripts/Meteor.cs` | Preserve meteor blast radius. |
| `Assets/Scripts/Editor/CardReworkSetup.cs` | Update authoring description and provide a targeted description-only menu action. |
| `Assets/Scripts/Editor/CardReworkVerification.cs` | Replace old blanket-boost expectations and add regression checks for the eight-stat policy and exclusions. |
| `Assets/Cards/Upgrades/Survival Upgrades/Second Wind.asset` | Update Rebirth's displayed description through the Editor API. |
| `Docs/ARCHITECTURE.md` | Document current Rebirth ownership/scope and unchanged dash behavior. |
| `Docs/CARD_REWORK_IMPLEMENTATION_GUIDE.md` | Replace obsolete numeric policy and description; link this follow-up. |
| `Docs/REBIRTH_STAT_SCOPE.md` | Record this change, exact setup requirements, verification boundaries and Play Mode checklist. |
