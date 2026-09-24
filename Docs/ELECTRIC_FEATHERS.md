# Electric Feathers — Setup and Verification

> Historical pre-rework notes. For the current card values, combat behavior and Unity setup, follow [the card rework implementation guide](CARD_REWORK_IMPLEMENTATION_GUIDE.md). The Electric Feathers card and electric-chain effect prefab now exist; do not create duplicate assets from the older instructions below. The rework also changes criticals, Airburst interactions and fractional damage, so the original mechanic and test expectations below are no longer the current specification.

The scripting is implemented. The card asset, lightning prefab, artwork, and scene
assignments must still be created/configured in Unity. No raw scene, prefab, card
asset, or `.meta` files were authored for this feature.

## Mechanic

- Each successful normal firing volley counts as one attack, irrespective of
  parallel feathers, spread, or Tripleshot.
- Each independently fired Mini Gun projectile counts as one attack. Mini Gun's
  normal volley and extra shot can both count in the same frame.
- Buckshot pellets, airbursts, other special feathers, and turret shots do not count.
- After the configured number of attacks, one additional electric feather fires
  along the player's aim using the existing PlayerBullet pool.
- A firing attempt that emits no normal/Mini Gun projectile does not count.
  If the electric projectile cannot obtain a pooled bullet, its counter stays
  ready and retries on a later successful attack; it does not build an unbounded queue.
- Initial damage is normal non-critical attack damage at firing time, including
  `CurrentStats.DamageMultiplier` and the then-current Money High multiplier.
  Later stat/buff changes do not change this projectile's damage. No critical roll,
  pierce, ricochet, homing, knockback, status effects, or airburst are added to it.
- The initial feather travels normally. After hitting an enemy, all jumps occur
  immediately in the same call. Each jump selects the nearest living, enabled
  EnemyBase with the Enemy tag within the configured radius, measured between
  transforms in 2D. Child colliders are supported. Walls do not obstruct jumps.
- The initial victim is excluded from future jumps; every other victim is hit at
  most once. The chain ends when no valid next enemy exists or the jump cap is met.
- Damage is multiplied by 0.5 before each additional hit. The unrounded falloff
  is retained for subsequent jumps. EnemyBase accepts integer damage, so each hit
  rounds using `Mathf.RoundToInt` with a minimum of 1. For initial damage 100, the
  requested amounts are 100, 50, 25, 12, 6, 3, 2. Enemy defenses may reduce these.
- An unassigned lightning prefab disables only the line visual, not damage.

## Card Asset Setup

1. Wait for Unity compilation to finish.
2. In `Assets/Cards/Upgrades/Munitions Upgrades`, choose
   **Create > DuckDefender > Card Definition** and name it **Electric Feathers**.
3. Configure:

| Field | Value |
|---|---|
| ID | `mun_electric_feathers` (keep stable after release) |
| Card Name | `Electric Feathers` |
| Description | `Every {0} attacks, fire an electric feather dealing normal attack damage and chaining to up to {1} additional enemies. Each jump deals 50% of the previous hit's damage.` |
| Icon | Your card artwork; a sprite is optional for code functionality |
| Rarity | `Rare` |
| Pack Category | `Munitions` |
| Base Upgrade Cost | `100` (matches the existing Metal Feathers configuration) |
| Max Level | `5` |
| Base Cost | `100` |

4. Set **Modifiers** to exactly two entries, in this order:

| Index | Stat Type | Base Amount | Amount Per Shop Level | Stack Mode | In Run Stack Amount |
|---|---|---:|---:|---|---:|
| 0 | `ElectricFeatherThreshold` | 10 | -1 | `MatchShopGrowth` | 0 (unused) |
| 1 | `ElectricFeatherChainCount` | 1 | 1 | `MatchShopGrowth` | 0 (unused) |

5. In **MainMenu**, select **ShopManager** and append this asset to
   `ShopManager.AllCards`. Do not replace the existing entries.
6. In **SampleScene**, select **CardManager** and append the same asset to
   `CardManager.AllCards`.
7. The existing Munitions pack selects cards from `ShopManager.AllCards` by
   category and rarity; no new pack or separate pack card list is needed.
   Obtain/unlock the card through the shop before expecting gameplay offers:
   `CardManager` filters Munitions cards against the saved collection.
8. Save the asset and your intentional scene changes in Unity. Let Unity create
   its `.meta`; do not write a GUID yourself.

The normal shop/UI supports permanent levels 1–5. In-run pickups are uncapped in
the existing CardManager; this effect caps its strength at effective level 6.
Effective level is `min(6, permanent level + run pickup count - 1)` after acquiring
the card. Additional re-picks beyond that do not strengthen it further.

| Effective level | Attack interval | Additional enemies chained | Maximum total victims |
|---|---:|---:|---:|
| 1 | 10 | 1 | 2 |
| 2 | 9 | 2 | 3 |
| 3 | 8 | 3 | 4 |
| 4 | 7 | 4 | 5 |
| 5 | 6 | 5 | 6 |
| 6 (in-run improvement) | 5 | 6 | 7 |

For example, a permanent level-5 card reaches effective level 6 on its second
pickup that run. A permanent level-1 card reaches it on its sixth pickup. No
sixth permanent level or save-schema change was introduced.

## Weapon and Visual Effect Setup

1. Open **SampleScene**, select **Player**, and find **WeaponPlayer**.
2. Leave its existing **Fire Point** and projectile configuration assigned.
   No new component needs attaching to the Player or PlayerBullet prefab.
3. Under **Electric Feathers**, use these initial tuning values:
   - **Electric Chain Radius:** `4` world units per jump.
   - **Electric Chain Effect Duration:** `0.08` seconds.
   - **Electric Feather Color:** your preferred tint; default is light cyan.
4. Create a GameObject named **ElectricChainEffect** with a **Line Renderer** on
   its root. Use an active GameObject and disable just the Line Renderer initially.
   Do not add colliders, Rigidbody2D, self-destruction scripts, or an Animator.
5. Configure the Line Renderer:
   - **Use World Space:** enabled.
   - **Loop:** disabled.
   - **Alignment:** View.
   - **Width:** start with a constant `0.06` world units and tune visually.
   - **Color:** your desired lightning color, with visible alpha.
   - **Material:** assign a material using a compatible unlit shader, for example
     `Universal Render Pipeline/2D/Sprite-Unlit-Default`, and your texture if desired.
   - **Sorting Layer:** choose the layer used for your foreground effects.
   - **Order in Layer:** above the enemies (for example `100` on the same layer).
6. Drag this GameObject into `Assets/Prefabs` to create the prefab through Unity.
   Remove the temporary scene instance yourself after creating the prefab.
7. Assign the prefab's Line Renderer to
   `Player > WeaponPlayer > Electric Chain Effect Prefab` and save your scene.

The script supplies five world-space positions for a short zigzag between each
pair of victims. It preserves your material, color, width, and sorting settings.
It creates line instances only as needed, reuses them, and hides them after the
duration. They are children of WeaponPlayer and disappear when it is destroyed.
No generated texture, replacement sprite, or extra effect script is required.

## Verification Performed During Implementation

- Unity 6000.0.35f1 compilation completed successfully through the live Pipeline
  connection, with no compilation errors.
- Actual `CardDefinition.GetAmountForPickup` checks passed in the Editor for all
  five permanent levels and eight re-pick indices at each level. These were
  temporary in-memory card objects, not saved assets or Play Mode tests.
- Existing serialized enum values remain in place; new StatTypes are appended
  as 80 and 81, and Electric is appended to FeatherType.
- A temporary Editor preview of SampleScene confirmed Player's WeaponPlayer and
  PlayerStats, assigned FirePoint and OverheatPopup, and GameManager's assigned
  PlayerBullet prefab with Projectile and Rigidbody2D. CardManager has 50 entries.
- `ElectricChainEffectPrefab` is currently null, as expected before manual setup.
  The preview was closed without saving. MainMenu stayed active and clean.
- Existing warnings were observed for `Coin.cs` using obsolete `isKinematic`, an
  unused PlayerController field, and PlayerBullet's non-Legacy AnimationClip.
  These unrelated issues were not changed.

No Play Mode, build, visual, collision, or end-to-end card acquisition testing has
been performed. Card registration and the new visual reference remain manual setup.

## Play Mode Test Checklist

Perform these checks after creating and registering the card. Use disposable test
settings or a test scene and restore any tuning after testing.

1. **Acquisition:** acquire the card from Munitions, verify its Rare entry and
   description, and verify it can appear in gameplay offers once unlocked.
2. **Counter:** at permanent level 1, pick it once. Watch the Electric instance in
   `PlayerStats.SpecialFeathers`: no electric shot for attacks 1–9, one on attack
   10, then counter reset. Holding fire still counts emitted volleys, not frames.
3. **Every level:** verify intervals 10/9/8/7/6 for first pickups at shop levels
   1–5. Verify an effective sixth level gives interval 5 and further re-picks cap.
4. **Multishot:** add parallel fire, spread, Tripleshot, Buckshot, and Airburst.
   A normal volley advances the electric counter only once. Bonus projectiles do
   not advance it. With Mini Gun enabled, its independent emitted shots also count.
5. **Damage:** temporarily use normal attack damage 64, multiplier 1, no Money
   High, and durable unarmored enemies. At effective level 6, isolate an electric
   impact and confirm requested damage 64/32/16/8/4/2/1. Ordinary feathers also
   fire and must be accounted for when reading health changes.
6. **Snapshot:** fire while Money High is active, let it expire before impact,
   and confirm the electric projectile retains firing-time damage. Crit chance
   should not change electric damage. Enemy armor still applies normally.
7. **Target count:** levels 1–6 hit at most 2/3/4/5/6/7 total enemies, including
   the initial victim. Arrange targets in a line within 4 units per jump.
8. **No duplicates:** group enemies closely and test an enemy with several child
   colliders. Each EnemyBase may be damaged once per chain, including the first.
9. **Too few targets:** test one enemy, gaps larger than the radius, dead enemies,
   and an initial hit that kills. Chains stop safely or continue from the killed
   victim's recorded position. Missing an initial enemy produces no chain.
10. **Visual:** all chain links appear together and disappear after about 0.08 s.
    Check visibility/sorting and simultaneous activations. Temporarily unassign
    the visual prefab and verify damage still functions without errors.
11. **Pooling:** confirm a normal feather reused after an electric feather has no
    electric behavior. Under pool pressure, unsuccessful firing attempts do not
    advance the counter and one pending electric activation is retained.
12. **Run reset:** restart the run and reacquire the card. Confirm its counter and
    in-run strength reset while the saved permanent card level remains intact.
