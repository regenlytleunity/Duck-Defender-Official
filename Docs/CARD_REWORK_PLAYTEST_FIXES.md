# Card rework — 24 September playtest fixes

This update addresses the reported upgrade bugs and balance changes. It builds on your completed prefab/UI work and keeps the version-2 save format; **no additional progression wipe is introduced**. Start a new run to apply the updated card strengths to a clean set of run stats.

## What changed

| Report | Implemented fix / balance |
| --- | --- |
| Hunter / Marksman too slow | Hunter cooldowns are **6, 5, 4, 3, 2, 1 seconds** at levels 1–6. Ascended Marksman fires every **1 second** before Rebirth. Target search reuses a list and the enemy registry. |
| Savior invisible and blocking movement | Ascension areas disable their authored physics colliders/bodies. Savior draws a green **LineRenderer circle**, using the same approach as the existing damage/slow auras, at sorting order 100. Old sprite/particle visuals are suppressed on that runtime instance. Healing remains 2 HP/second inside radius 3 for 5 seconds, every 7 seconds before bonuses. |
| Defender needs gravity and an immediate cast | Picking Defender immediately creates two walls. Subsequent placement is every **20 seconds**. Each wall has a Dynamic Rigidbody2D with gravity 2, Continuous detection, frozen horizontal position and rotation, and a solid collider. It falls onto Ground and still ignores player collision. |
| Divine Duplicator missing the ordinary pair | Ascension now retains the level-six base modifiers: two parallel feathers, with the duplicate's 25% damage penalty. A third feather falls vertically at the **cursor/touch aim's world X coordinate**, for 50% damage. It does not select an enemy or inherit homing. |
| Blink distance / erratic movement | Blink remains a fixed **1.5 world units** by default, independent of speed, duration and Rebirth. It executes during the physics step, sweeps the player collider against blockers, clamps clearance so touching a wall cannot teleport backward, and sets Rigidbody position directly. Horizontal acceleration now moves toward target velocity without force-feedback overshoot. |
| Wormholes not visible | The existing circle sprite is normalized to the actual radius, rendered unlit at sorting order 100 and rotated at **20 degrees/second**. Without a sprite/prefab, a circle ring is generated. Solid prefab colliders no longer turn it into an enormous obstruction. |
| Faster Firing too strong | Cooldown reduction is **20%, 30%, 40%, 50%, 60%, 70%** at levels 1–6. |
| Vampire orbs need gravity | Orbs use Dynamic bodies, gravity 2 and solid ground contact while falling. They collide only with Ground. Once in attraction range they remove gravity, use a trigger and move toward the player without obstructing them. |
| Tungsten still looks like a feather | The bullet Animator was writing the feather sprite over the assigned square. It is disabled for Tungsten/needle sprite overrides and restored for normal pooled reuse. Initialization caches the original sprite only once. The redundant, invalid legacy Animation component was removed from PlayerBullet through Unity's prefab API; its working Animator and artwork remain. |
| Ricochet selects the wrong enemy | Hit tracking and target lookup now both use **EnemyBase component IDs**. Already-hit/dead enemies are excluded. Target searches use the living-enemy registry instead of allocating scene-wide tag searches. |
| Absolute Extinction fails after enemy impacts | Meteor coins are created and flagged **before** damage callbacks run. Damage loops use reusable snapshots because kills can remove enemies from the registry mid-iteration. The same correction covers beam, aura, trail, explosion, shockwave and wall effects. Secondary meteors spawn at viewport Y 0.92 and X 0.08–0.92, inside the current view, with no extra off-screen height offset; they drop no coins. |
| Rebirth too strong | Beneficial stat bonus is **+150%**, replacing +200%. Additive percentage stats receive +1.5; other beneficial strengths use 2.5× and cooldowns divide by 2.5. Its one-use rule, full heal and safety window remain. |
| Heavy stacked-feather performance / clone buildup | Bullet reuse is queue-based, with lazy growth and a bounded capacity. Hit/explosion visuals are also bounded and reused, explicitly activated even if their source prefab is inactive, and expired after a finite lifetime. Per-projectile homing/ricochet searches no longer allocate full scene tag arrays. |
| Open 3 reveal too slow | Initial reveal delay, each card's pop duration, spacing between cards and final wait are all **halved for nine-card reveals**. Single-pack timing and purchase logic are unchanged. |

Minigun's heat slowdown remains in place.

## Findings confirmed in the project

- `SampleScene` had `ObjectPooler.PoolSize = 10000`. The previous code instantiated all of those bullets at startup and scanned from the start of the list for every shot.
- `Feather Hit Effect.prefab` was inactive. Instantiating it without activation meant its particle-system Destroy action never ran, leaving inactive unowned effect clones.
- `Wormhole.prefab` and `Savior area.prefab` had solid CircleCollider2D components. Their sprites were on sorting order 0, and the area radius scaled the entire root including colliders.
- `PlayerBullet.prefab` had the square sprite assigned, but also had a sprite Animator and an incompatible legacy Animation component. The latter produced repeated non-Legacy-clip warnings.
- The Console contained `InvalidOperationException: Collection was modified` errors in the ascension beam loop. Meteor and other area-damage loops used the same unsafe iteration pattern. The interrupted-meteor explanation is an inference from that shared path; a full before/after meteor gameplay reproduction was not performed.
- Ricochet stored component IDs and compared them with GameObject IDs. This mismatch was confirmed directly in code and is covered by a regression check.

## Editor follow-up

The existing assignments were inspected read-only. Your authored scene and effect-prefab edits were preserved. Runtime repairs mean you do **not** need to rebuild the new UI or replace these prefabs.

1. Let Unity finish compiling. The five affected CardDefinition assets have already been updated. **Duck Defender → Card Rework → Apply September Playtest Balance** reapplies only these five changes if needed; do not run the full catalog reset just for this patch.
2. Open `Assets/Scenes/SampleScene.unity` and inspect Player → AscensionEffects. The inspected assignments were:

   | Field | Existing assigned prefab |
   | --- | --- |
   | Wormhole Prefab | `Assets/Prefabs/Effects/Wormhole.prefab` |
   | Healing Area Prefab | `Assets/Prefabs/Effects/Savior area.prefab` |
   | Wall Prefab | `Assets/Prefabs/Object Prefabs/DefenderWall.prefab` |
   | Healing Orb Prefab | `Assets/Prefabs/Object Prefabs/Healing Orb.prefab` |

3. Keep these references. Savior now supplies its own ring; Wormhole uses your existing circle sprite. Neither area needs a collider or Rigidbody. Existing ones are disabled on the spawned instance. You may remove redundant colliders from the prefab in the editor, but it is not required for the runtime fix.
4. Inspect DefenderWall: BoxCollider2D must match your art. The script creates/configures its Dynamic Rigidbody2D and solid collider. For the Inspector to mirror runtime, save Gravity Scale 2, Continuous detection, Freeze Position X and Freeze Rotation. Ensure its existing layer can collide with Ground and enemy projectiles. The project collision matrix was not changed.
5. Inspect Healing Orb: keep HealingOrb and CircleCollider2D, with a Dynamic Rigidbody2D, gravity 2, Continuous detection and Freeze Rotation. The script sets its solid/trigger state and Ground-only collision override. These components can be saved on the prefab for clarity; runtime also ensures the required body exists.
6. On PlayerController, keep **Blink Distance = 1.5** and **Blink Blocker Layer = Ground**. The inspected scene already had these values. No duration-to-distance conversion or movement-speed multiplier is applied.
7. On `Assets/Prefabs/Object Prefabs/PlayerBullet.prefab`, retain the working Animator and the assigned **Tungsten Sprite**. The invalid extra legacy Animation component has already been removed. Do not re-add it for the square; the runtime handles the override.
8. On GameManager → ObjectPooler, the new defaults are **Player Prewarm 64**, **Maximum Player Projectiles 2048**, **Maximum Effects Per Prefab 32**, **Maximum Effects Total 128**. PoolSize now acts as an upper requested capacity; the smaller of it and Maximum Player Projectiles is used. The old saved value of 10,000 will no longer allocate 10,000 bullets. Keep these defaults for the next playtest before tuning.
9. **PooledVisualEffect requires no manual attachment or prefab.** ObjectPooler adds it to runtime one-shot effects. When the cosmetic cap is reached, additional hit/explosion visuals are skipped while damage still resolves. When the bullet cap is reached, additional projectiles cannot be emitted until a slot returns; increase the cap only if profiling demonstrates headroom.
10. No MainMenu wiring change is needed for the faster Open 3 reveal.

An inactive `PlayerBullet(Clone)` or pooled hit-effect instance in the Hierarchy is expected: it is available for reuse. The useful failure signals are an ever-growing instance count after warm-up or active effects/projectiles continuing beyond their lifetime. Expired pooled objects are not destroyed on every shot.

## Verification recorded on 24 September

- **Unity C# compilation passed** in Unity 6000.0.35f1.
- **1,321 isolated progression/combat checks passed**, using a temporary save and transient objects. New checks cover all six balance steps, Divine inheritance, Rebirth arithmetic, non-blocking circles, physics component configuration, fixed-distance Blink/high-stat movement, ricochet exclusion, registry mutation during damage, Tungsten/normal sprite reuse, bounded lazy allocation, 10,000 projectile rentals without pool growth and hit-effect activation/expiry/reuse/caps.
- **Six isolated preview-physics checks passed.** A separate PhysicsScene2D simulated falling walls/orbs for five seconds, confirmed ground contact/rest, and verified Blink's collider sweep and close-wall behavior. This did not simulate your open scene or enter Play Mode.
- The existing seeded 10,000-offer distribution checks also passed: Common 6,026; Rare 2,989; Legendary 294; Basic 691.
- Read-only scene/prefab inspection and a final source/diff review were completed. New metadata was generated by Unity; serialized asset changes used Unity APIs.
- After checks, MainMenu remained open outside Play Mode, the temporary save override was cleared and no verification GameObjects remained. No gameplay scene was saved by this patch.

Repeat with **Duck Defender → Card Rework → Run Isolated Verification** and **Run Isolated Physics Verification**. Compilation/isolated checks do not establish frame rate, visual readability or every combined card interaction. Full gameplay, mobile input, desktop/WebGL builds and profiling remain unverified. The pre-existing Coin obsolete-API and PlayerController unused-field warnings remain unrelated to this patch.

## Next Play Mode pass

1. Test Hunter at L1 and L6, then Marksman: 6 seconds, 1 second and 1 second respectively. Test Faster Firing L1/L6 and Rebirth in separate fresh runs.
2. Walk through a Savior circle and check healing only while inside. Move through a Wormhole circle, confirm visible rotation and enemy pull, and check it expires.
3. Pick Defender while airborne: two walls should appear immediately, fall and rest on the floor. Wait 20 seconds for replacement; shoot/hit the walls with enemies to check damage and destruction.
4. Kill a flying enemy with Vampire, observe the orb fall, then approach and collect it. Confirm it cannot obstruct the player or remain suspended outside attraction range.
5. Select Divine Duplicator, aim away from enemies and fire. Confirm the parallel pair plus a third feather above the cursor column; move the cursor and repeat with Homing selected.
6. Combine speed upgrades, Untouchable and Rebirth; run both directions, release movement, Blink near walls and compare with a low-speed run. The extra teleport must stay 1.5 units in open space without oscillation between blinks.
7. Fire Tungsten, let that projectile return to the pool, then fire normal feathers. Check square/feather sprites, bounce/linger behavior and normal animation restoration.
8. Place several enemies nearby and combine Pierce/Ricochet/Homing. Confirm ricochets prefer living enemies not already hit by that projectile and do not keep choosing its previous victim.
9. With Absolute Extinction, test a primary impact that kills an enemy and a ground-only impact. Collect the **three meteor payload coins** in each case. Each must create one visible secondary meteor; secondary impacts must not drop further payload coins. Ordinary enemy reward coins remain ordinary coins.
10. Stack the feather upgrades for a long wave. In the Profiler compare CPU/GC and Physics2D cost; watch active versus inactive projectile/effect counts through firing, expiry and a pause. Pool counts should remain within their caps. This is still needed to measure the actual performance gain.
11. Open one pack, then three. The nine-card reveal should use half-duration delays/pops while still revealing and awarding nine cards exactly once.

## Changed-file manifest

All five card assets and the PlayerBullet prefab were saved through Unity APIs. No `.meta` was hand-authored, and no scene/prefab YAML was manually rewritten.

| File | Why it changed |
| --- | --- |
| `Assets/Cards/Upgrades/Gadget Upgrades/Duplicator.asset` | Retain ordinary parallel-feather modifiers on Divine ascension and update the description. |
| `Assets/Cards/Upgrades/Gadget Upgrades/Marksman.asset` | Hunter 6-to-1-second cooldown scaling and ascended 1-second description. |
| `Assets/Cards/Upgrades/Gadget Upgrades/Protector.asset` | Describe immediate falling walls and the new 20-second cooldown. |
| `Assets/Cards/Upgrades/Munitions Upgrades/Faster Firing.asset` | 20-to-70-percent cooldown-reduction scaling in 10-point increments. |
| `Assets/Cards/Upgrades/Survival Upgrades/Second Wind.asset` | Describe Rebirth’s +150% bonus and 2.5× cooldown speed. |
| `Assets/Prefabs/Object Prefabs/PlayerBullet.prefab` | Removed only the incompatible redundant legacy Animation component through Unity PrefabUtility; Animator, square sprite and other settings preserved. |
| `Assets/Scripts/AscensionArea.cs` | Disable area physics; generate Savior rings and normalize/rotate Wormhole circles; use safe damage snapshots. |
| `Assets/Scripts/AscensionEffects.cs` | Circle fallback spawning and reusable snapshots for beam, eruption and Rebirth damage. |
| `Assets/Scripts/AuraController.cs` | Prevent enemy-removal callbacks from invalidating aura damage iteration. |
| `Assets/Scripts/CardReworkCombatProbe.cs` | Editor-only target that can remove itself on damage, reproducing the registry mutation bug. |
| `Assets/Scripts/DefenderWall.cs` | Ensure a dynamic, falling, upright wall; safe knockback iteration and pooled destruction visuals. |
| `Assets/Scripts/Editor/CardReworkSetup.cs` | Update authoring defaults and add a targeted five-card playtest-balance menu. |
| `Assets/Scripts/Editor/CardReworkVerification.cs` | Updated balance expectations, 35 new regression assertions, and a separate six-check preview-physics verification. |
| `Assets/Scripts/EnemyBase.cs` | Reusable active-enemy snapshot helper for callbacks that change the registry. |
| `Assets/Scripts/FireTrailPatch.cs` | Safe trail damage iteration when enemies die or are disabled. |
| `Assets/Scripts/HealingOrb.cs` | Gravity/solid ground contact before attraction; Ground-only collisions and physics-driven attraction. |
| `Assets/Scripts/MainMenuUI.cs` | Half-duration initial/inter-card/final waits and pop animation for three-pack reveals. |
| `Assets/Scripts/MarksmanTurret.cs` | Reuse target buffers and cached weapon instead of repeated scene searches at the faster cadence. |
| `Assets/Scripts/Meteor.cs` | Create flagged payload coins before damage callbacks; safe damage iteration and pooled explosion visuals; secondary payload guard. |
| `Assets/Scripts/ObjectPooler.cs` | Queue-based bullet reuse, lazy prewarm/growth limits, bounded one-shot effect pools and runtime diagnostics. |
| `Assets/Scripts/PlayerController.cs` | Physics-step fixed-distance Blink with a collider sweep, stable horizontal acceleration, visible secondary-meteor spawn positions and safe shockwave iteration. |
| `Assets/Scripts/PlayerHealth.cs` | Apply the shared +150% Rebirth bonus to stats and maximum health. |
| `Assets/Scripts/PlayerStats.cs` | Shared Rebirth constant, Defender 20-second cooldown and immediate wall placement. |
| `Assets/Scripts/PooledVisualEffect.cs` | New runtime component controlling activation, particle reset, bounded lifetime and effect reuse; no manual prefab attachment. |
| `Assets/Scripts/PooledVisualEffect.cs.meta` | Unity-generated metadata for the new runtime component. |
| `Assets/Scripts/Projectile.cs` | Preserve original sprite state, suspend/re-enable animation for variants, correct ricochet IDs, reuse the enemy registry, return bullets to their pool and pool visual effects. |
| `Assets/Scripts/WeaponPlayer.cs` | Aim Divine Duplicator’s falling feather at the cursor world column and disable its homing. |
| `Docs/ARCHITECTURE.md` | Document current pooling, area physics/rendering, balance and verification behavior. |
| `Docs/CARD_REWORK_IMPLEMENTATION_GUIDE.md` | Bring values and setup steps up to date, and distinguish the original handoff from this patch. |
| `Docs/CARD_REWORK_PLAYTEST_FIXES.md` | Record all requested fixes, causes, verification boundaries, exact editor follow-up and the next Play Mode checklist. |

### Existing user edits preserved

These paths were already modified at the start of this task and were not rewritten by the patch:

- `Assets/Prefabs/Effects/Feather Hit Effect.prefab`
- `Assets/Prefabs/Effects/Wormhole.prefab`
- `Assets/Scenes/SampleScene.unity`

Their existing differences were the inactive hit-effect root, Wormhole collider radius, and gameplay-scene color/active-state edits. Runtime code handles the effect/collider behavior without overwriting those authoring choices. No commit or push was made.
