# Enemy update implementation and authoring

The October enemy outline is implemented in the existing combat and wave systems. Existing enemy art, animations, tags, layers, and asset GUIDs are reused.

## Confirmed interpretation

The explicit health table overrides the outline's general doubling language. Wave 1 starts at base health. Transitions into waves 2–10 add 0.5; 11–15 add 1; 16–20 add 2; 21–25 add 3; 26–30 add 4; 31–40 add 6; 41–50 add 10; 51 onward add 20. Health is rounded down. Neither movement speed nor firing cadence scales with waves.

Elite flyer slow refreshes a 50% movement reduction without stacking; its default duration is two seconds. Elite tanks turn intercepted damage into shield without losing their own health. These interpretations were confirmed by the user.

## Mechanics

| Enemy | Normal | Elite |
|---|---|---|
| Ground (`SwarmerEnemy`) | Collider-edge melee reach, 0.25s windup, one damage, 0.15s recovery, one-second cooldown | 50% more health before rounding, 15% slower, 50% more reach, two damage and a 0.5s stun on successful hits |
| Flying (`BuzzerEnemy`) | Approaches a hover position at fixed speed, 0.35s windup targeting the player's position when windup begins, one damage, one-second cooldown | 25% larger shots apply the non-stacking slow. On death, becomes untargetable, drifts to terrain over three seconds, explodes for one damage within 1.5 units, then awards rewards |
| Tank | Four times the basic ground enemy's rounded health, 85% of its speed, ground melee. Chains up to four nearest eligible enemies within eight units, each owned by only one tank; tanks cannot be protected | Redirected 50% damage builds a separate blue shield. Each linked ally's death grants +10% movement speed and +0.25 melee damage, up to four stacks |
| Lobber | High arc toward the position sampled at windup. One damage, 50% poison chance. Poison refreshes for five seconds, up to five stacks at 0.2 damage/second each. Ground impacts become one-second clouds dealing one contact hit | 25% more health before rounding. After poison, fires a second non-poison ball with a 0.3s delay. It reflects twice, multiplying speed and size by 1.25 each time; the next terrain collision removes it |

All normal enemy prefabs now drop 4–6 coins. Elite coins are doubled and XP is multiplied by 1.25; fractional XP carries forward. Elites reuse the normal prefab with a 1.15 size multiplier and darker tint, retained when facing changes and status colors expire.

Tank links are lifetime slots, not refillable slots. Linked allies stay linked after moving out of acquisition range. Killing/disabling the tank immediately releases them. Intercepted damage uses floating point so one-point hits split into 0.5/0.5 rather than gaining or losing damage.

Player fractional damage accumulates against the existing integer health system. Poison ticks do not consume or respect ordinary post-hit invulnerability, so a fresh projectile hit cannot erase a poison tick. Blink invulnerability still blocks damage. Stun blocks movement, dash, jump, blink, and player feather firing; turrets, auras, thorns, and existing projectiles continue. Healing to full clears accumulated sub-point damage.

Off-screen spawns reject damage, knockback, poison, and freeze until their sprite first intersects the gameplay camera. `SpawnProtectionSeconds` supplies a no-camera timeout; with a gameplay camera, entering view determines the end of protection. An enemy spawned already on screen is immediately vulnerable.

## Wave authoring

Select the object with the WaveManager component in `Assets/Scenes/SampleScene.unity`. `WaveDefinitions` overrides specific wave numbers; unspecified waves remain random with the existing spawn-cap and multi-spawn controls.

The default introduction schedule is configurable using `NormalUnlockWaves` and `EliteUnlockWaves`, ordered ground/flying/tank/lobber:

| Variant | Ground | Flying | Tank | Lobber |
|---|---:|---:|---:|---:|
| Normal | 1 | 3 | 6 | 9 |
| Elite | 12 | 16 | 20 | 25 |

Each introduction's first spawn guarantees that variant. Default waves contain `2 + 2 × wave` enemies. After introduction, eligible elites have a 15% chance per selected enemy. The schedule and chance are initial tuning choices; the outline did not give exact introductions or probabilities. `UseIntroductoryWaves = false` unlocks all variants immediately.

Create assets with **Assets → Create → Duck Defender → Wave Definition**, then drag them into `WaveDefinitions`. Each wave number should appear once.

- **Random:** `EnemyCount` is the exact count (zero uses the normal count); `RandomEnemies` supplies the allowed pool. Empty uses the default unlocked pool.
- **Scripted:** only `OrderedSpawns` is used. Group counts determine the exact total, and list order determines spawn order.
- **Mixed:** ordered groups spawn first, then weighted random entries fill the total. Counts above the total produce an explicit configuration error.
- Each ordered group specifies prefab, count, elite flag, spawn-point index (`-1` random), delay before the group, and interval (`-1` inherits).
- Each random entry specifies prefab, relative weight, and elite probability. Weights 70 and 30 yield 70% and 30%; a zero weight never spawns.
- `ExcludedEnemies` applies to random choices and validates scripted groups. Excluding a scripted group's prefab is an error, not a silent change to the requested count.
- `SpawnInterval = -1` uses the WaveManager cadence; nonnegative values override it. The manager's minimum time between enemies remains a safety floor. `AllowMultiSpawn` enables the existing burst scheduling while preserving list order.
- Explicit random entries and ordered groups can introduce any variant early. Empty custom pools use the default unlock schedule.
- Missing prefabs, bad spawn-point indices, empty scripted waves, and empty eligible random pools stop the wave with a specific Console error and `HasWaveError = true`.

For example, a mixed wave with total 12, an ordered group of two elite tanks, and ground/flying weights 70/30 always opens with those two tanks and fills the remaining ten slots using that distribution.

## Tutorials and UI

WaveManager adds `EnemyTipUI` automatically. A tip appears only when the full enemy sprite is in view, pauses gameplay, and waits one second plus a release followed by a new click/tap. Holding the fire button cannot dismiss it. Other tips wait until gameplay resumes. Level-up UI retains its pause if it overlaps a tip.

Seen IDs and the **Show tips** setting use the existing save file without changing its version or resetting card progress. Cached economy saves preserve the latest tutorial fields. With Show tips off, each tip appears once across runs. With it on, already-seen tips can appear once per new run when the relevant enemy appears; unseen tips still appear normally. Turning it off preserves seen history.

`SettingsMenuUI.ShowTipsToggle` is wired in MainMenu. The tip uses the existing enemy sprite and a generated top-screen panel; optional authored `Panel`, text, and illustration references can replace it. `EnemyTipUI.Tips` supplies custom content by stable ID, and `ShowTip` supports future non-enemy tips. Attach `EnemyTipUI` to the WaveManager object explicitly to expose these optional references in the Inspector; automatic creation is sufficient for the default presentation.

Enemy health bars enlarge by `BarSizeMultiplier` and display current/max health; shield bars are generated above them when required. Fractions from tank sharing display to one decimal place.

## Unity setup and tuning

The four enemy prefabs, the lobber projectile, and the MainMenu Show Tips toggle were configured through Unity Editor APIs. No raw scene/prefab YAML or `.meta` files were authored.

No additional setup is required to run the default introduction schedule. Optional authoring:

1. Create and assign Wave Definition assets for custom waves.
2. Tune `MeleeReach`, `WindupSeconds`, and `RecoverySeconds` on Fast Enemy/Tank Enemy. Old range/strike fields are retained for serialization, but the new collider-edge fields own timing and reach.
3. Tune `ChainRange`, `ChainSearchInterval`, and optional `ChainPrefab` on Tank Enemy. Without an authored chain, a simple line uses the enemy's existing material.
4. Tune `SlowDuration`, `CrashDuration`, `CrashDrift`, and `CrashExplosionRadius` on Flying Enemy. `GroundLayer` is Ground and `CrashExplosionPrefab` reuses `Assets/Prefabs/Effects/Explosion Effect.prefab`.
5. Tune `ShotWindup`, `EliteShotDelay`, and `LobHeight` on Lobber Enemy; tune poison/cloud values on its projectile prefab. The normal poison cloud reuses the projectile sprite with a green translucent treatment; final cloud/chain artwork remains optional visual work.
6. Adjust Enemy Health Bar's `BarSizeMultiplier` or assign authored health/shield labels after evaluating readability at target resolutions.

No Unity/package version, build target, graphics setting, or save-schema version was changed.

## Verification and Play Mode checklist

Repeat isolated checks with **Duck Defender → Enemies → Run Isolated Verification**. They use transient objects and a unique temporary save. **Run Play Mode Verification** opens SampleScene and runs a controlled combat probe against a separate temporary save; exit Play Mode afterward. `EnemyUpdatePlayProbe` is Editor-only and opt-in through a session flag, cleared when exiting Play Mode; it never runs in player builds or ordinary Play Mode.

Manual balance/visual pass:

1. Start a new run. Check the first-seen tip and full-view requirement; hold/spam click during its grace period, then release and click. Test touch input too.
2. Stand still for ground/tank attacks, then dodge during windup. Check elite stun and passive attacks continuing.
3. Watch all four normal and elite introductions. Verify elite size/tint, coins, XP, and wave completion after a flyer crash.
4. Damage chained enemies with one-point attacks, poison, explosions, and auras. Check tank shield depletion, four-link lifetime limit, ally-death bonuses, and broken chains after tank death.
5. Check poison refresh, five-stack cap, cloud contact/expiry, two growing elite bounces, and pooled reuse after both variants.
6. Compare waves 10/11, 15/16, 20/21, 25/26, 30/31, 40/41, and 50/51. Movement speed should stay fixed.
7. Configure scripted, random, and mixed waves with exclusions, zero weights, alternate spawn points, and custom delays. Verify counts/order and useful errors for invalid configurations.
8. Restart and confirm tips stay seen; enable Show tips and restart to replay them. Confirm cards/currency survive.
9. Compare rapid clicking with held fire at low and high fire rates, including minigun, pause/resume, and stun.

Automated checks are not a substitute for complete balance evaluation, final artwork review, mobile-device testing, or platform builds.

Completed verification for this implementation:

- Unity 6000.0.35f1 compilation succeeded.
- 1,496 isolated enemy-update checks passed (health boundaries, tank protection/shields, default wave plans, authored wave validation, XP fractions and save compatibility).
- The existing card-rework regression verification passed 1,357 checks.
- 23 controlled Play Mode checks passed, including stationary melee, windup dodge, stun/slow/poison, spawn protection, pool resets (including a first-use elite flyer shot), cloud expiry, delayed crash death, live chains/shields, tutorial pause, and fire-cooldown preservation under repeated release/press inputs.
- Settings Show Tips saved both on and off through the wired runtime toggle, against an isolated temporary save.
- Screenshots were inspected for the top-screen tutorial, numeric health/shield bars, and the settings toggle. The shield's vertical offset was corrected after that review.
- All new script `.meta` files were generated by Unity. Final source changes were reviewed; prefab values and settings wiring were inspected in Unity.

Not verified: full 25-wave balance playthrough, device touch interaction, complete card/enemy combination coverage, and standalone/WebGL builds. SampleScene still reports existing missing scripts on `Tilemap_Background` and `CardManager`; SampleScene was not modified. The existing unused `_isCoinShotActive` compiler warning also remains.

## Changed files and reasons

| File | Reason |
|---|---|
| `Assets/Scripts/EnemyBase.cs` | Shared elite stats, additive health, spawn protection, collider-aware melee support, fractional tank redirection, delayed-death hooks, rewards and preserved facing scale/tint |
| `Assets/Scripts/SwarmerEnemy.cs` | Readable windup/impact/cooldown melee, stationary-player hit correction, elite reach/stun, reusable tank melee |
| `Assets/Scripts/TankEnemy.cs` | Replaces proximity armor with four exclusive lifetime chains, shields and ally-death bonuses; now derives from SwarmerEnemy while retaining serialized field names |
| `Assets/Scripts/BuzzerEnemy.cs` | Fixed-speed hover/windup firing, elite slow projectiles, crash and delayed rewards |
| `Assets/Scripts/LobberEnemy.cs` | High-arc aimed poison and elite delayed second shot; removes firing-rate scaling |
| `Assets/Scripts/BouncyEnemyProjectile.cs` | Poison/cloud and elite two-bounce modes with complete pooled reset |
| `Assets/Scripts/EnemyProjectile.cs` | Elite slow payload, larger shots and safe first-use/pool scale reset |
| `Assets/Scripts/Groundenemyanimator.cs` | Checks optional animation parameters once, eliminating repeated missing-parameter warnings and updating supported idle state |
| `Assets/Scripts/Flyingenemyanimator.cs` | Supports the current controller without an optional isFlying parameter |
| `Assets/Scripts/EnemyHealthBar.cs` | Enlarged numeric health bars and a correctly positioned numeric shield bar |
| `Assets/Scripts/WaveManager.cs` | Health table, introduction gates, random elite variants, authored plans, validation and tutorial initialization |
| `Assets/Scripts/WaveDefinition.cs` | Inspector-authorable scripted/random/mixed wave data |
| `Assets/Scripts/EnemyTipUI.cs` | Full-view enemy information, input grace/release guard, pause ownership and content overrides |
| `Assets/Scripts/PlayerController.cs` | Movement/dash slow and stun while preserving passive systems |
| `Assets/Scripts/PlayerHealth.cs` | Successful-hit result, fractional damage, timed stun/slow and capped refreshing poison |
| `Assets/Scripts/WeaponPlayer.cs` | Release/press cannot reset fire cooldown; stun gates feather firing and pending staggered shots |
| `Assets/Scripts/LevelManager.cs` | Carries fractional XP so the elite 25% bonus is not lost |
| `Assets/Scripts/PlayerData.cs` | Additive seen-tip IDs and replay setting, without a schema-version change |
| `Assets/Scripts/SaveSystem.cs` | Preserves tutorial state across cached economy writes and existing saves |
| `Assets/Scripts/SettingsMenuUI.cs` | Show Tips toggle loading/saving and event wiring |
| `Assets/Scripts/LevelUpUI.cs` | Does not resume gameplay while a tutorial still owns a pause |
| `Assets/Scripts/Editor/EnemyUpdateVerification.cs` | Repeatable isolated tests and an opt-in Play Mode test entry point |
| `Assets/Scripts/EnemyUpdatePlayProbe.cs` | Editor-only controlled live combat tests using a temporary save |
| `Assets/Scripts/Editor/EnemyUpdateVerification.cs.meta` | Unity-generated identity for the new Editor verification script |
| `Assets/Scripts/EnemyTipUI.cs.meta` | Unity-generated identity for the new tutorial component |
| `Assets/Scripts/EnemyUpdatePlayProbe.cs.meta` | Unity-generated identity for the Editor-only Play Mode probe |
| `Assets/Scripts/WaveDefinition.cs.meta` | Unity-generated identity for the wave ScriptableObject type |
| `Assets/Enemies/Fast Enemy.prefab` | One-second melee cooldown, 4–6 coins and disabled old scaling |
| `Assets/Enemies/Flying Enemy.prefab` | One-second firing cooldown, 4–6 coins, crash terrain mask and existing explosion effect |
| `Assets/Enemies/Tank Enemy.prefab` | Shared melee/animator wiring, fixed cooldown, 4–6 coins, chain defaults |
| `Assets/Enemies/Lobber Enemy.prefab` | 4–6 coins, fixed firing rate and new volley defaults |
| `Assets/Prefabs/Object Prefabs/Lobber Enemy Projectile.prefab` | Two-bounce limit, lifetime and poison/cloud defaults |
| `Assets/Scenes/MainMenu.unity` | Adds and assigns Show Tips control under the existing settings panel |
| `Docs/ARCHITECTURE.md` | Records the implemented enemy, wave, status and tutorial architecture |
| `Docs/ENEMY_UPDATE.md` | Implementation report, authoring/setup instructions, verification scope and Play Mode checklist |

Unity generated the new `.meta` files for `WaveDefinition.cs`, `EnemyTipUI.cs`, `EnemyUpdatePlayProbe.cs`, and `Editor/EnemyUpdateVerification.cs`. Existing asset and script GUIDs were preserved.
