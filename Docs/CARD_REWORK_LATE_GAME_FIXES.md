# Late-game performance and bug fixes — 25 September 2026

This follow-up implements all seven items from the latest playtest report. The previous balance fixes remain in place. Existing user edits to feather colors, the inactive Feather Hit Effect source prefab and Wormhole collider were preserved.

## Changes and evidence

| Request | Investigation and implementation |
| --- | --- |
| Late-wave spawn lag | The scene allowed `MaxExtraSpawns = 100`; a batch instantiated all its enemies in one frame. Batches now yield after each enemy, with a minimum **0.04 seconds** between individual spawns. At most **40 wave enemies** are alive concurrently by default. Additional enemies remain queued and still count toward the wave's remaining total. All must be defeated to finish the wave. Difficulty scaling and total wave enemy counts remain intact; the new cap changes pacing when the queue is full. |
| Additional ammunition cost | Both enemy-ammunition pools were configured to instantiate **10,000 objects each** at startup. Each now prewarms at most **32** and grows only when requested, retaining its configured maximum capacity. Player projectile pooling retains its existing limits. This reduces initial allocation and retained unused objects; it is not an FPS measurement. |
| Airburst particles | Only secondary Airburst feathers set `BallisticData.SuppressHitEffect`. Enemy and terrain hit paths respect it. Regular feathers reset the flag when rented from the same pool. Airburst damage, lifetime and non-recursive behavior remain intact. |
| Coin consolidation | Every **0.25 seconds**, nearby eligible coins are grouped within **2 world units**: ten singles become one value-10 coin; ten value-10 coins become one value-100 coin. Groups below ten remain separate. A **0.18-second** vacuum animation draws donors into the survivor. Sizes are **1× / 1.18× / 1.36×** the prefab scale. A merge reserves all donors immediately so they cannot be collected twice. Search checks adjacent spatial cells and budgets candidate work at 4,096 comparisons / 32 merges per pass. Offscreen coins remain eligible; no currency is culled. |
| Pickup spikes | Coins share physics materials and use terrain-only collision. Collection credits their full value immediately. Powerful Profit stores counted expiration batches, preserving per-coin bonuses without allocating one timer entry per value unit. Coin saves are coalesced to at most one per second and flushed on disable, app pause, focus loss and quit. A crash between flushes can lose less than one second of newly credited coins. Normal and Absolute Extinction meteors are queued and instantiated at most two per frame, preserving all entitlements during the run. Queued meteors are not persisted across leaving the run. |
| Explosive Feathers / Volcano | Explosion Effect had a `SelfDestruct` component, which could destroy pooled roots while their slots remained counted. The pool now owns effect lifetimes, restarts reused animation/particles, recovers destroyed slots and replays a same-kind visual at capacity. Authored effect rotation is preserved. Explosion and Volcano particle renderers now use transparent **URP/Particles/Unlit** materials at sorting order **100**. Volcano's inactive fire-particle child is activated, and the delayed eruption also plays the original explosion visual. No new artwork was generated. |
| Feet catching on terrain | `Tilemap_Ground` had an unused composite collider: its TilemapCollider2D used **Composite Operation: None**, leaving individual tile edges. The scene now uses one merged polygon collision outline. The original `(0, -0.3)` offset was transferred to the composite, preserving the ground surface at **Y = -2.3**. Coins no longer collide solidly with the player. Blink also ignores artificial blocking normals from a tiny resting overlap with a flat floor. Savior areas disable all authored colliders/bodies before use. These address identified causes; full card combinations still need Play Mode evaluation. |
| One-pack positioning | `PackRow` was offset 825 UI units left. On opening packs, MainMenuUI centers the active images as a group in the existing horizontal layout. A single image occupies the center; three images stay centered together. Existing slicing hit detection follows their actual RectTransforms. |
| Distance-based Dash | Dash now consumes a distance budget in FixedUpdate, sweeps its body against terrain and clears residual velocity when finished. Initial tuning is **3, 4, 5, 6, 7, 8 world units** at levels 1–6. Existing cooldowns remain **7, 6, 5, 4, 3, 2 seconds**. Move speed and Rebirth do not extend travel distance; Dash speed changes travel time. Downward impacts still use the existing Shockwave path. |

Passive and ordinary coins merge separately. Passive immunity retains the newest contributing coin's spawn timestamp. Absolute Extinction triggers are counted separately from currency: a stack containing 34 marked original coins still earns 34 secondary meteors. A 100-value pickup also counts as 100 coins for Powerful Profit, Illegal Operations and threshold progress.

## Editor state and exact setup

The following changes were **already performed and saved through Unity Editor APIs** in this workspace:

1. `Assets/Cards/Upgrades/Mobility Upgrades/Dash.asset`: distance modifier, level values and description updated. Artwork, ID and GUID retained.
2. `Assets/Prefabs/Effects/Explosion Effect.prefab`: particle material assigned to `ExplosionParticles.mat`, sorting order 100.
3. `Assets/Prefabs/Effects/Volcano fire.prefab`: particle child active; material assigned to `VolcanoParticles.mat`, sorting order 100. Its existing Circle sprite is retained.
4. `Assets/Scenes/SampleScene.unity`: Ground tilemap merged into its existing composite. New script fields were serialized by Unity with their defaults. Pre-existing scene color edits were retained.
5. Unity generated metadata for the new materials and editor scripts.

**No new runtime component attachments, coin prefabs, pack images or artwork are required.** MainMenu scene changes happen through the existing MainMenuUI at runtime; its source scene was not saved by this update.

After pulling/importing these changes:

- Let Unity finish compiling before testing.
- Open `Assets/Scenes/SampleScene.unity`. On the object with **WaveManager**, confirm `Max Concurrent Enemies = 40` and `Minimum Time Between Enemies = 0.04`. These are initial performance/pacing settings; adjust after profiling. Existing batch chance, count and interval settings still apply.
- On **ObjectPooler**, confirm `Enemy Prewarm = 32`. Keep the assigned normal/bouncy projectile prefabs. The saved 10,000 capacity values are maximums, not prewarm counts. Existing player settings remain prewarm 64 / maximum 2,048 and effects 32 per prefab / 128 total.
- On **Player → PlayerController**, `Dash Distance = 3` is the fallback before card application. Dash's card supplies its level-specific distance. `Dash Speed` controls travel time. `Ground Layer` and `Blink Blocker Layer` must include Ground; the supplied scene does. The hidden legacy `DashDuration` value is no longer used for movement.
- Select **Tilemap_Ground**. TilemapCollider2D: Composite Operation **Merge**, Offset **(0, 0)**. CompositeCollider2D: Geometry **Polygons**, Generation **Synchronous**, Offset **(0, -0.3)**. In the supplied scene the composite has one outline. Leave the Ground layer and existing Rigidbody2D in place.
- Existing **Coin** prefabs need their current Rigidbody2D, Collider2D, sprite and Coin script. Runtime code handles shared material, Ground-only collisions and size tiers. Do not create separate 10/100 prefabs or add extra pickup scripts. Coin values should remain 1 on ordinary drop prefabs.
- Existing **LevelManager** must remain enabled to schedule merging and flush coin saves; it is already present. Keep its passive coin reference.
- **Player → AscensionEffects** retains its Healing Area and Volcano Fire prefab references. Healing effects disable all physics at initialization. Keep the current `Volcano fire.prefab` reference so the corrected particle material is used.
- **PlayerBullet → Projectile → Explosion Prefab** remains `Explosion Effect.prefab`. Do not assign a sprite-lit material back to its ParticleSystemRenderer. SelfDestruct can remain on that prefab: pooled instances disable it and own their lifetime.
- In `Assets/Scenes/MainMenu.unity`, MainMenuUI's existing `Pack Image` and two `Additional Pack Images` should still reference siblings under **PackRow**, which has a HorizontalLayoutGroup. The code centers that row when opening packs. No new button callbacks are needed.

For a separate scene or older copy, use these explicit editor commands (they do not run automatically):

- **Duck Defender → Late Game Fixes → Update Dash and Explosion Assets** updates only Dash and the two particle prefabs/materials. It reapplies Dash's 3–8-unit tuning, so do not run it after intentionally choosing different values.
- **Duck Defender → Late Game Fixes → Merge Active Scene Ground Colliders** repairs Ground-layer tilemaps that already have a CompositeCollider2D. Review the collision outline and save that scene. It transfers a source offset once, so repeat use does not keep shifting the floor.

No project settings, packages, save schema, card IDs or existing enum indices were changed. `StatType.DashDistance` was appended; old DashDuration definitions retain a compatibility mapping. Existing player saves do not require a wipe for this patch.

## Verification performed

- Unity 6000.0.35f1 compilation completed successfully.
- **1,321 existing progression/combat assertions passed**, using a disposable save and transient objects.
- **141 new late-game assertions passed**: 100 → ten 10s → one 100, partial/distant/passive groups, vacuum motion, material reuse, no duplicate pickup, reward conservation, batched bonus expiration, isolated save flush, spawn pacing/cap/accounting, pool growth/reuse/saturation, Airburst flag reset, explosion emission/materials, Volcano activation/delayed burst, single/triple pack layout, boosted Dash travel and wall stopping, and Savior physics exclusion.
- **6 existing isolated physics checks passed** for falling walls/orbs and Blink collision sweeps.
- **3 checks against a preview copy of the saved gameplay scene passed**: one merged ground outline, original terrain height, and a player body crossing tile boundaries while blinking and remaining grounded.
- Saved card/prefab/scene references and generated metadata inspected. Git whitespace check passed. Existing user scene colors and source-prefab edits were preserved.

These checks use Editor objects and isolated physics simulation. **They are not a full Play Mode run, a rendered visual acceptance test, a build test or a Profiler benchmark.** The investigation establishes concrete allocation/collision/lifetime problems and verifies the corrected code paths; it does not establish that all lag past wave 25 is eliminated. The existing unused `_isCoinShotActive` compiler warning remains unrelated to this patch.

Repeat checks through:

- Duck Defender → Card Rework → Run Isolated Verification
- Duck Defender → Card Rework → Run Isolated Physics Verification
- Duck Defender → Late Game Fixes → Run Isolated Regression Checks
- Duck Defender → Late Game Fixes → Check Saved Ground Physics

## Play Mode checklist

1. Run through waves 25–35 with the same upgrade combination that lagged. In the Profiler, compare CPU frame time, GC allocations, Physics2D and particle/render work. Watch `SpawnedEnemiesAlive` / `QueuedEnemies` in a debugger: at capacity the queue should wait; the HUD must not reach zero until every queued enemy has spawned and died. Pause/resume must not release a burst.
2. Stack Airburst with Buckshot, Faster Firing and piercing/ricochet. Confirm secondary feathers still deal damage but emit no feather-hit particles. Ordinary feathers rented afterward must regain their normal hit particles. Check inactive pooled objects are reused rather than growing without limit.
3. Leave at least 100 enemy-drop coins together outside the viewport. Return: expect ten-value and then hundred-value coins, modest size changes and short vacuum merges. Nine isolated singles must stay separate. Collect a hundred and verify the balance rises by exactly 100; reload after returning to the menu to check persistence.
4. Repeat with Coin Meteor / Absolute Extinction and Powerful Profit. All marked-coin secondary meteors should eventually arrive on screen, spread over frames. A stack must count its full value for damage bonuses and thresholds. New pickup bonuses must not extend an older bonus's expiration. Check passive coins remain visible during immunity.
5. Fire Explosive Feathers and Volcano into enemies and terrain repeatedly, including more than 32 explosions. Confirm the impact particles continue to appear, Volcano emits another burst after its delay, and its fire remains visible for its damage duration. Tune artwork size/color only after confirming those events.
6. Run and blink along the ground with Savior active, dropped coins, movement-speed upgrades and Rebirth. Check feet no longer snag, auras never push the player and wall boundaries still block travel. Test ledges/slopes in any additional scenes separately.
7. At Dash levels 1 and 6, check approximately 3 and 8 units in clear space; repeat at high movement speed and after Rebirth. Test diagonal/up/down directions, wall contact, downward Shockwave, pause/resume, dash count reset on landing and moving platforms if used.
8. Buy one pack: the pack should be centered and sliceable. Then buy three: all three should fit and remain sliceable, with the existing faster nine-card reveal. Repeat single → triple → single to catch layout state leaking between purchases.

## Files changed in this follow-up

Paths below are relative to the project root. Files already modified by the earlier update are listed only when this follow-up also changed them.

| File | Reason |
| --- | --- |
| `Assets/Scripts/WaveManager.cs` | Staggered instantiation, concurrent-enemy budget, queued/live accounting and spawn validation. |
| `Assets/Scripts/ObjectPooler.cs` | Lazy enemy ammunition pools; recover/replay effect slots; preserve authored FX rotation. |
| `Assets/Scripts/PooledVisualEffect.cs` | Disable conflicting destruction timers and restart pooled animators. |
| `Assets/Scripts/SelfDestruct.cs` | Leave lifetime ownership to PooledVisualEffect when present. |
| `Assets/Scripts/Projectile.cs` | Per-projectile hit-FX suppression and pass explosion artwork to Volcano's delayed eruption. |
| `Assets/Scripts/WeaponPlayer.cs` | Mark Airburst children to suppress hit FX. |
| `Assets/Scripts/AscensionEffects.cs` | Play an explosion at delayed Volcano eruption. |
| `Assets/Scripts/AscensionArea.cs` | Activate damaging-area particle children. |
| `Assets/Scripts/Coin.cs` | Spatial denomination merging, vacuum animation, tier sizes, shared materials, terrain-only physics, proximity pickup and reward transfer. |
| `Assets/Scripts/LevelManager.cs` | Schedule merges, coalesce coin saves and distribute earned meteor spawns across frames. |
| `Assets/Scripts/SaveOnQuit.cs` | Flush LevelManager's pending currency before the existing final-save path. |
| `Assets/Scripts/PlayerStats.cs` | Counted Powerful Profit expiration batches and cached active stack total. |
| `Assets/Scripts/PlayerController.cs` | Distance-limited physics-step Dash and resting-floor Blink cast correction. |
| `Assets/Scripts/CardDefinition.cs` | Append DashDistance without shifting existing stat indices. |
| `Assets/Scripts/CardManager.cs` | Apply distance modifiers and map legacy DashDuration definitions. |
| `Assets/Scripts/MainMenuUI.cs` | Center the active pack group and reset additional pack rotations. |
| `Assets/Scripts/Editor/CardReworkSetup.cs` | Keep the full card-setup command consistent with distance-based Dash. |
| `Assets/Scripts/Editor/CardReworkVerification.cs` | Update saturation assertion for bounded visual reuse. |
| `Assets/Scripts/Editor/LateGameFixSetup.cs` and `Assets/Scripts/Editor/LateGameFixSetup.cs.meta` | Targeted editor repairs; Unity-generated script metadata. |
| `Assets/Scripts/Editor/LateGameFixVerification.cs` and `Assets/Scripts/Editor/LateGameFixVerification.cs.meta` | Regression and saved-ground physics checks; Unity-generated metadata. |
| `Assets/Cards/Upgrades/Mobility Upgrades/Dash.asset` | Distance scaling and description, saved through Unity. |
| `Assets/Prefabs/Effects/Explosion Effect.prefab` | Correct particle material and sorting. |
| `Assets/Prefabs/Effects/Volcano fire.prefab` | Activate fire particles and assign correct particle material/sorting. |
| `Assets/Prefabs/Effects/ExplosionParticles.mat` and `Assets/Prefabs/Effects/ExplosionParticles.mat.meta` | Transparent unlit explosion material using existing texture; created through Unity. |
| `Assets/Prefabs/Effects/VolcanoParticles.mat` and `Assets/Prefabs/Effects/VolcanoParticles.mat.meta` | Transparent unlit Volcano particle material; created through Unity. |
| `Assets/Scenes/SampleScene.unity` | Merged ground collider/offset and Unity serialization of new runtime settings. |
| `Docs/ARCHITECTURE.md` | Current spawning, pooling, coin, movement and UI behavior. |
| `Docs/CARD_REWORK_IMPLEMENTATION_GUIDE.md` | Current Dash table and link to this follow-up. |
| `Docs/CARD_REWORK_LATE_GAME_FIXES.md` | This report, editor setup, verification boundaries and Play Mode checklist. |
