# Duck Defender — Agent Instructions

## Purpose

This file describes the current Duck Defender repository and the rules coding agents must follow when working in it.

The repository itself is always the source of truth.

If this document disagrees with the current files, inspect the repository and follow the current implementation rather than assuming this document is correct.

Do not invent file paths, classes, build scripts, tests, packages, scenes, or project settings that cannot be verified from the repository.

---

# 1. Project Overview

Duck Defender is an existing Unity 2D wave-survival/action game.

The player controls a duck through platforming and combat while fighting scaling waves of enemies, collecting coins and XP, leveling up, and selecting card-based upgrades.

Verified project information:

* Engine: Unity
* Unity version: `6000.0.35f1`
* Unity major version: Unity 6
* Rendering pipeline: Universal Render Pipeline
* URP version: `17.0.3`
* Rendering configuration: 2D
* Primary gameplay scene: `Assets/Scenes/SampleScene.unity`
* Main menu scene: `Assets/Scenes/MainMenu.unity`
* Main project code is under `Assets/Scripts`
* Project scripts currently compile into Unity's default `Assembly-CSharp`
* No project `.asmdef` or `.asmref` files were found
* No project-specific automated tests were found

Steam release information, exact standalone build configuration, company name, product name, scripting backend, API compatibility settings, and several platform-specific deployment details still require direct verification from project files and are listed under **TO CONFIRM**.

---

# 2. Repository Layout

Meaningful verified project directories include:

```text
Duck Defender/
├── Assets/
│   ├── Backgrounds/
│   │   └── Background art and related assets
│   │
│   ├── Cards/
│   │   └── CardDefinition and shop-pack ScriptableObject assets
│   │
│   ├── Enemies/
│   │   └── Enemy prefabs and animation assets
│   │
│   ├── Fonts/
│   │   └── Font assets
│   │
│   ├── Player/
│   │   └── Player animation assets
│   │
│   ├── Plugins/
│   │   └── Platform-specific plugins
│   │
│   │   └── WebGL/
│   │       └── WebGL JavaScript interop
│   │
│   ├── Prefabs/
│   │   └── Gameplay objects, effects, turrets, projectiles, and UI
│   │
│   ├── Resources/
│   │   └── Present but currently appears unused or empty
│   │
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   └── SampleScene.unity
│   │
│   ├── Scripts/
│   │   └── Main project gameplay and UI C# code
│   │
│   ├── Settings/
│   │   └── URP renderer, pipeline, build-profile, and scene-template data
│   │
│   ├── Sound Effects/
│   │   └── Project audio assets
│   │
│   ├── Sprites/
│   │   └── Sprite assets
│   │
│   └── TextMesh Pro/
│       └── TMP resources
│
├── Packages/
│   └── Unity package manifest and package configuration
│
├── ProjectSettings/
│   └── Unity project settings
│
├── AGENTS.md
└── .gitignore
```

Additional scenes exist outside the two primary build scenes, but their exact filenames must be confirmed before documenting them here.

## Generated and Local Folders

The following directories are generated or machine-specific and are off-limits to agents:

```text
Library/
Temp/
obj/
Logs/
Build/
Builds/
UserSettings/
```

Do not intentionally modify, inspect as authoritative source code, or commit files from these directories.

The repository also ignores generated IDE/project files such as:

```text
*.csproj
*.sln
.vs/
.vscode/
```

where applicable according to `.gitignore`.

---

# 3. Core Systems Map

## Player

Player behavior is composition-based rather than contained in one large controller.

### PlayerController

Located within the project's `Assets/Scripts` codebase.

Responsibilities include:

* Rigidbody2D movement
* acceleration and movement handling
* jumping
* multi-jump behavior
* directional dash
* ground slam
* blink
* low-gravity behavior
* fire-trail behavior
* hypersonic collision damage
* player facing
* movement animation state

`PlayerController` is also used through a static `Instance` singleton-style reference.

Do not create a second player movement controller when adding movement abilities.

Extend the existing controller when appropriate.

### WeaponPlayer

Owns player weapon behavior including:

* mouse/touch aiming
* firing cadence
* projectile firing
* parallel shot patterns
* spread patterns
* special feather scheduling
* buckshot
* airburst
* tripleshot
* minigun heat
* minigun overheat

Weapon upgrades should normally integrate with this existing system.

### Projectile

Player projectile behavior receives a `BallisticData` snapshot.

Verified projectile capabilities include:

* base damage
* critical hits
* piercing
* ricochet
* homing
* knockback
* explosions
* healing
* poison
* freezing
* target reacquisition

Player projectiles are pooled through `ObjectPooler`.

Before adding a new projectile mechanic, inspect `Projectile`, `BallisticData`, `WeaponPlayer`, and `PlayerStats`.

### PlayerHealth

Owns:

* player damage
* healing
* regeneration
* thorns
* death
* game-over transition
* second-wind behavior

### PlayerStats

`PlayerStats` is the central mutable runtime state for many card-derived statistics and abilities.

It is reached through singleton-style access and direct references from other gameplay systems.

Before creating a new gameplay-stat container, determine whether the value belongs in `PlayerStats`.

### PlayerAnimator

Coordinates player animation.

Some animation state is also manipulated directly from `PlayerController`.

Do not assume animation state is exclusively owned by `PlayerAnimator`.

---

## Waves

### WaveManager

`WaveManager` owns:

* starting waves
* enemy selection
* enemy spawning
* multi-spawning
* wave progression
* wave/tier difficulty scaling
* tracking enemies remaining

It is a scene-owned singleton-style manager.

Before changing spawn logic or difficulty scaling, inspect `WaveManager` and the enemy stat-scaling logic in `EnemyBase`.

---

## Enemies

### EnemyBase

`EnemyBase` is the common abstract enemy superclass.

It owns shared behavior including:

* base health
* wave-scaled health
* movement speed
* damage
* player lookup
* facing
* enemy health-bar creation
* knockback
* poison
* slow
* freeze
* hit flashing
* death
* rewards
* coin drops

Movement is delegated through abstract `Move()` behavior.

New enemy types should inherit or otherwise work with the established `EnemyBase` architecture unless a task explicitly requires a different design.

### Current specialized enemy classes

Verified specialized classes include:

```text
SwarmerEnemy
TankEnemy
LobberEnemy
BuzzerEnemy
```

Verified behavior:

* `SwarmerEnemy`

  * fast ground melee behavior
  * separation behavior
  * persistent strike window

* `TankEnemy`

  * slower melee unit
  * damage reduction
  * shield feedback

* `LobberEnemy`

  * approaches a firing range
  * launches ballistic/bouncing projectiles

* `BuzzerEnemy`

  * hovering ranged enemy
  * retreat behavior
  * swarm separation

The repository also contains older or more general:

```text
GroundEnemy
FlyingEnemy
```

Current named prefabs use the specialized classes listed above.

Do not remove the older implementations merely because they appear redundant without first checking serialized references and prefab usage.

### Enemy Projectiles

Verified enemy projectile classes include:

```text
EnemyProjectile
BouncyEnemyProjectile
```

`EnemyProjectile` handles conventional straight projectiles.

`BouncyEnemyProjectile` handles lobbed or bouncing projectiles and player collision behavior.

---

# Cards and Upgrades

## CardDefinition

Cards are defined through `CardDefinition` ScriptableObjects.

Verified card-definition responsibilities include:

* card identity
* pack
* rarity
* visuals
* descriptions
* shop scaling
* in-run stacking
* one or more stat modifiers

The repository currently contains approximately 49 `CardDefinition` assets.

Treat that count as descriptive rather than as a hard-coded invariant.

## ShopPackDefinition

Permanent shop packs use `ShopPackDefinition` ScriptableObjects.

Verified shop-pack categories/assets currently include:

```text
Base
Mobility
Munitions
Survival
Gadget
```

The Gadget category was historically referred to as Tech in parts of the project.

Do not globally rename old Tech/Gadget references without checking compatibility.

## CardManager

`CardManager` owns:

* rarity rolling
* filtering unlocked cards
* run card pools
* run pickups
* card stacking
* applying upgrades to runtime systems

It applies effects to systems including:

```text
PlayerStats
PlayerController
WeaponPlayer
```

and other related components.

Stable card string IDs are also used by persistent save data.

Verified examples include:

```text
mun_buckshot
mob_blink
sur_second_wind
```

These IDs must be treated as persistent identifiers.

Do not casually rename existing card IDs.

A card-ID rename may require save-data migration.

---

# Economy and Progression

## LevelManager

`LevelManager` owns:

* run XP
* leveling
* run coins
* passive coin spawning
* wave-completion rewards
* progression-related UI synchronization

Enemies notify wave, level, and stat systems when they die.

Level-ups interact with `LevelUpUI` and `CardManager`.

Do not create parallel XP, leveling, coin, or wave-reward managers.

Some additional coin-triggered mechanics were described in previous project context but have not all been independently verified during the current repository audit.

See **TO CONFIRM**.

---

# Turrets and Companion Systems

## TurretBase

Turret companions derive from `TurretBase`.

`TurretBase`:

* follows the player
* occupies a floating assigned slot
* invokes subclass-specific behavior at configurable intervals

Verified turret/companion classes include:

```text
MarksmanTurret
MedicTurret
ProtectorTurret
ElementalTurret
SecondWindAngel
```

Responsibilities:

* `MarksmanTurret`

  * targets visible enemies
  * fires projectiles

* `MedicTurret`

  * periodically heals the player

* `ProtectorTurret`

  * creates damaging and knockback shockwaves

* `ElementalTurret`

  * fires random elemental feather variants

* `SecondWindAngel`

  * represents second-wind readiness visually and/or mechanically

## TurretManager

`TurretManager` listens to changes in `PlayerStats`.

It creates, removes, and repositions companion/turret objects.

Extend this system rather than introducing a second companion manager.

---

# Save and Persistence

## SaveSystem

Permanent progression is serialized by `SaveSystem`.

The save file is:

```text
Application.persistentDataPath/duck_save.json
```

Verified saved information includes:

* total coins
* card ID
* permanent card level
* duplicate count
* unlock state

Corrupt or missing save files fall back to a fresh `PlayerData`.

Do not casually modify the persistent data schema.

Any persistent-field change should consider backward compatibility with existing saves.

## WebGL Saving

Verified WebGL save synchronization uses:

```text
Assets/Plugins/WebGL/SaveSystemBridge.jslib
```

The bridge invokes file synchronization so writes can reach IndexedDB.

Do not rename the `.jslib` extension.

Platform-specific WebGL behavior must remain behind appropriate WebGL-specific logic where required.

## ShopManager

`ShopManager` loads and writes persistent economy and collection data.

It is also responsible for permanent currency and collection/upgrading behavior.

## SaveOnQuit

A `SaveOnQuit` implementation exists.

The previous repository inspection did not find it attached in the two main scene component lists.

Do not assume it is currently active without checking the current scenes or runtime creation path.

---

# Audio

## AudioManager

`AudioManager` exists as a persistent manager and uses `DontDestroyOnLoad`.

Verified persistent audio settings use `PlayerPrefs`.

Previous project context described additional audio-slicing behavior and an `AudioManagerEditor` custom inspector, but those details were not independently confirmed in the current repository audit.

See **TO CONFIRM** before relying on those systems.

---

# UI

The project uses Unity uGUI and TextMesh Pro rather than UI Toolkit.

## Gameplay UI

Verified gameplay UI classes include:

```text
GameUI
LevelUpUI
CardDisplay
SpriteHealthBar
SpriteXPBar
EnemyHealthBar
OverheatPopup
DamagePopup
SafeAreaPanel
PixelPerfectCanvasScaler
```

### GameUI

Owns display of:

* coins
* health
* XP
* wave information
* enemy count
* wave banners
* damage popups
* game-over UI

### LevelUpUI

Creates card-choice UI during level-up events and handles card selection.

Level-ups interrupt or pause normal gameplay while the player chooses an upgrade.

### CardDisplay

Shared card-rendering component used by:

* level-up rewards
* shop pack reveals
* card index UI

Reuse it where possible rather than creating separate card rendering systems.

### Mobile/Layout UI

`SafeAreaPanel` and `PixelPerfectCanvasScaler` support mobile-safe and pixel-oriented UI layout.

## Menu UI

Verified menu classes include:

```text
MainMenuUI
ShopManager
CardIndexUI
SettingsMenuUI
FullscreenToggle
ButtonClickSound
```

`MainMenuUI` handles panel navigation, shop-related UI, coin display, and reset-data confirmation.

`CardIndexUI` provides the categorized card collection/index.

`SettingsMenuUI` and `FullscreenToggle` manage settings/display-related controls.

---

# Input

Input is currently hybrid.

Installed Unity packages include the new Input System, and:

```text
InputSystem_Actions.inputactions
```

contains `Player` and `UI` maps.

Project settings currently use both input backends.

However, actual gameplay scripts primarily use the legacy `UnityEngine.Input` API through `InputHelper`.

Verified input classes include:

```text
InputHelper
InputManager
MobileInputController
VirtualJoystick
```

## InputHelper

Acts as the primary gameplay abstraction.

It selects mobile virtual input when enabled and otherwise reads legacy desktop input including axes/buttons and mouse position.

New gameplay input should normally integrate through this abstraction.

## InputManager

Persistent singleton.

Provides:

* legacy `KeyCode` bindings
* PlayerPrefs-based rebinding

## MobileInputController

Provides mobile movement and aiming/shooting behavior.

Works with `VirtualJoystick`.

Verified mobile functionality includes drag-based jumping/dashing and aim snapping.

Do not migrate the project wholesale to the new Input System unless explicitly requested.

---

# Manager and Dependency Pattern

The project heavily uses singleton-style `Instance` access.

Verified singleton-style classes include:

```text
AudioManager
InputManager
MobileInputController
PlayerController
PlayerStats
PlayerAnimator
WaveManager
LevelManager
CardManager
TurretManager
GameUI
LevelUpUI
MainMenuUI
ShopManager
ObjectPooler
```

`AudioManager` and `InputManager` explicitly persist through `DontDestroyOnLoad`.

Most other managers are scene-owned instances.

Dependencies are commonly accessed through:

* static `Instance`
* Inspector references
* tags
* `GetComponent`

The project does not currently use a dependency-injection framework or service container.

Agents should work with this architecture unless explicitly asked to redesign it.

Do not introduce a new global DI framework merely because it would be architecturally cleaner.

---

# 4. Packages and Assembly Structure

Verified direct packages include:

```text
Universal RP            17.0.3
Input System            1.11.2
2D feature set          2.0.1
uGUI                    2.0.0
Visual Scripting        1.9.5
Timeline                1.8.7
Test Framework          1.4.5
Multiplayer Center      1.0.0
```

Rider and Visual Studio integration packages are also present.

Unity Version Control/Collab integration is present in the package configuration.

The presence of Unity Test Framework does **not** mean this repository currently has project tests.

No project-specific automated tests were found during the current inspection.

No `.asmdef` or `.asmref` files were found.

Project scripts therefore currently compile into:

```text
Assembly-CSharp
```

Do not create assembly definitions without a clear reason and explicit task context.

---

# 5. Coding Conventions

Match the style of the file being modified.

Verified conventions include:

* MonoBehaviour classes generally use the global namespace
* no general project namespace convention is currently used
* one primary class per file
* PascalCase public members
* underscore-prefixed private runtime fields
* inspector grouping through `[Header]`
* inspector documentation through `[Tooltip]`
* occasional `[Range]`
* coroutines for timing
* coroutines for buffs
* coroutines for attacks
* coroutines for wave behavior
* coroutines for effects
* tags for entity identification
* layer masks for collision and targeting
* static `Instance` fields for cross-system communication
* ScriptableObjects for card configuration
* Inspector configuration for much other gameplay data

Newer or expanded systems sometimes use:

* XML documentation summaries
* large section-divider comments
* version-oriented comments
* backward-compatibility aliases

Do not reformat unrelated sections of a file when implementing a small feature.

Do not remove compatibility aliases merely because a newer name exists.

## Logging

Use existing logging style in the surrounding class.

Specific project-wide `Debug.Log` prefix conventions were not fully verified during the current audit.

Do not invent a new global logging framework without being asked.

---

# 6. Rules for Agents Working in This Repository

## Inspect Before Editing

Before implementing a non-trivial feature:

1. Inspect all relevant existing scripts.
2. Identify the current owner of the behavior.
3. Inspect connected systems.
4. Check for serialized references and ScriptableObjects.
5. Prefer extending an existing system over creating a parallel one.

Do not begin with a broad refactor unless the user explicitly requests one.

---

## Unity `.meta` Files

Never hand-author or fabricate `.meta` files.

Never casually edit, regenerate, or delete an existing `.meta` file.

Unity asset GUIDs are stored in `.meta` files and are used by scenes, prefabs, ScriptableObjects, materials, animations, and other serialized references.

When creating a new Unity asset or `.cs` script:

1. Create the source asset.
2. Allow Unity to import it.
3. Allow Unity to generate the `.meta` file.
4. Verify the generated `.meta` exists before committing the new asset.

When moving or renaming an asset, preserve its associated `.meta` file and GUID.

---

## Serialized Unity Assets

Do not manually rewrite raw YAML in:

```text
*.unity
*.prefab
*.asset
```

unless the task explicitly requires serialized-file editing.

Scenes, prefabs, and ScriptableObjects contain GUID/fileID references and are high-risk merge surfaces.

Prefer:

* C# changes
* Unity Editor changes
* custom Editor tooling when appropriate

over manually manipulating large serialized YAML documents.

---

## Generated Folders

Never modify project source under:

```text
Library/
Temp/
obj/
Logs/
Build/
Builds/
UserSettings/
```

These directories are generated or machine-specific.

---

## Serialized Fields

Do not casually rename serialized fields.

Existing:

* scene references
* prefab data
* Inspector values

may rely on the serialized field name.

When a serialized field must be renamed, consider Unity's:

```csharp
[FormerlySerializedAs("OldFieldName")]
```

and inspect the affected serialized assets.

Explicitly report serialization-impacting changes.

---

## ProjectSettings

Do not modify important `ProjectSettings` values unless explicitly requested.

This includes, where applicable:

* Product Name
* Company Name
* bundle/application identifiers
* scripting backend
* API compatibility
* build target
* compression
* player settings
* graphics configuration

Build and distribution settings may be tied to external deployment configuration.

A request to change gameplay code is not permission to alter build configuration.

---

## Scenes and Inspector Wiring

Code review alone cannot prove that scene wiring or Inspector references are correct.

When a feature requires:

* adding a component
* assigning a prefab
* assigning an AudioClip
* setting a LayerMask
* setting a tag
* connecting a UI field
* adding a manager to a scene
* wiring a ScriptableObject
* changing Inspector values

say explicitly what must be done in Unity.

Never claim Inspector setup is complete unless it was actually performed and verified.

---

## Editor-Only Code

Editor-only code must:

* live under an `Editor/` directory

or

* be appropriately guarded with:

```csharp
#if UNITY_EDITOR
#endif
```

Do not introduce `UnityEditor` dependencies into runtime assemblies.

---

## Platform-Specific Code

Use appropriate compiler guards for platform-specific behavior.

Examples include:

```csharp
#if UNITY_WEBGL
#endif
```

and, when appropriate:

```csharp
#if UNITY_STANDALONE
#endif
```

Do not allow WebGL-specific native/browser interop to execute on unsupported platforms.

---

## Package Changes

Do not:

* upgrade Unity
* upgrade packages
* add packages
* remove packages

unless the task explicitly requires it.

Package changes can affect the entire project and must not be incidental to gameplay work.

---

## Git Safety

Do not automatically:

* commit
* push
* force-push
* rewrite Git history
* run destructive cleanup commands

unless explicitly asked.

Avoid commands such as:

```text
git reset --hard
git clean -fd
git push --force
```

unless the user explicitly requests that operation and its consequences are understood.

Before a substantial change, inspect the working tree with:

```text
git status
```

so unrelated user changes are not accidentally overwritten.

---

# 7. Build and Verification

## Current Automated Testing State

Unity Test Framework is installed.

However, no project-specific automated tests were found during the current repository audit.

Do not claim a change has passed automated tests unless tests actually exist and were run.

## CLI Build Scripts

No repository-specific automated CLI build script was verified.

Do not invent one.

No CI pipeline should be assumed unless one is actually found in the repository.

## Normal Verification

For most gameplay changes, verification currently means:

1. Save the modified C# files.
2. Allow Unity to recompile scripts.
3. Check the Unity Console for compiler errors.
4. Open the relevant scene.
5. Enter Play Mode.
6. Exercise the affected mechanic.
7. Check the Console for runtime exceptions.
8. Check the affected UI/physics/animation behavior.
9. Exit Play Mode.
10. Review changed files before committing.

For gameplay work, the likely primary verification scene is:

```text
Assets/Scenes/SampleScene.unity
```

For shop/menu/meta-progression work, the likely primary verification scene is:

```text
Assets/Scenes/MainMenu.unity
```

## Unity Command-Line Verification

The project Unity version is known:

```text
6000.0.35f1
```

The actual local Unity Editor executable path has not been verified.

Do not guess it.

Once the executable location is confirmed, verified Unity batchmode arguments can include:

```text
-batchmode
-quit
-projectPath "<repository-root>"
-logFile -
```

Do not present a complete executable command until the installed Unity Editor path has been confirmed on the current machine.

---

# 8. Common Tasks

## Adding a New Card

Before adding a card:

1. Inspect `CardDefinition`.
2. Inspect existing card assets under `Assets/Cards`.
3. Inspect `CardManager`.
4. Inspect the system that will consume the new stat:

   * `PlayerStats`
   * `PlayerController`
   * `WeaponPlayer`
   * another existing gameplay component
5. Determine whether the effect already exists.
6. Reuse the existing effect if possible.
7. Add new runtime logic only where required.
8. Create the new `CardDefinition` asset through Unity rather than hand-writing `.asset` YAML.
9. Assign its pack, rarity, text, art, modifiers, and identifier in the Inspector.
10. Ensure its identifier is unique and stable.
11. Add it to the appropriate pack/pool if the existing system requires explicit registration.
12. Test acquisition and stacking in Play Mode.
13. If it participates in permanent progression, test save/load behavior.

Do not rename an existing persistent card ID just to improve naming.

---

## Adding a New Enemy Type

Before creating a new enemy:

1. Inspect `EnemyBase`.
2. Inspect the existing specialized enemy closest to the desired behavior.
3. Determine which functionality is already implemented by `EnemyBase`.
4. Create only the behavior that is unique to the new enemy.
5. Use the existing health/status/reward/death logic rather than duplicating it.
6. Create or configure the enemy prefab through Unity.
7. Assign colliders, Rigidbody2D configuration, animation, sprites, and Inspector references.
8. Register the prefab with `WaveManager` or the current spawn configuration if required.
9. Verify wave scaling.
10. Verify player collision/damage.
11. Verify status effects.
12. Verify death and coin rewards.
13. Test the enemy in `SampleScene`.

Do not modify existing enemy prefabs as raw YAML unless explicitly requested.

---

## Adding a New Player Stat

Before adding a stat:

1. Inspect `PlayerStats`.
2. Search for related existing fields and compatibility aliases.
3. Determine whether the stat is:

   * run-only
   * card-derived
   * permanent
   * calculated
4. Add the runtime state to the existing owning system.
5. Inspect `CardDefinition` modifier types.
6. Inspect the relevant application logic in `CardManager`.
7. Add the modifier/application path only where needed.
8. Update whichever system consumes the stat.
9. If persistence is required, inspect `SaveSystem` and existing save models before changing the schema.
10. Test stacking and reset behavior.
11. Test interaction with old cards.
12. Test save compatibility if persistent.

Do not create a second general-purpose stat container unless explicitly requested.

---

## Adding an Audio Clip

Before adding audio:

1. Inspect `AudioManager`.
2. Inspect how similar existing sounds are triggered.
3. Import the audio asset into the project's existing audio asset structure.
4. Allow Unity to generate the `.meta`.
5. Reuse existing playback methods where possible.
6. Add a new serialized audio reference only if an existing mechanism cannot represent it.
7. Assign the clip in the Unity Inspector.
8. Test volume/settings interaction.
9. Test scene transitions if the audio uses the persistent `AudioManager`.

If the clip uses specialized slicing functionality, first verify that the previously described slicing implementation still exists in the current `AudioManager`.

Do not assume an `AudioManagerEditor` exists until its exact file has been verified.

---

# 9. Performance Guidelines

Duck Defender can have many enemies, projectiles, effects, pickups, and physics interactions active simultaneously.

Be careful when modifying:

```text
Update()
FixedUpdate()
projectile processing
enemy movement loops
physics overlap queries
raycasts
target searches
frequently running coroutines
```

Avoid unnecessary allocations in hot paths.

Avoid repeated scene-wide searches where an existing cached or singleton reference can be used.

Player projectiles already use `ObjectPooler`.

Inspect whether pooling is appropriate before adding large numbers of repeatedly instantiated gameplay objects.

Not every current object type is pooled; do not rewrite all spawning systems solely for consistency unless profiling or the task justifies it.

---

# 10. Existing Naming and Compatibility Concerns

The repository contains historical naming inconsistencies.

Verified examples include names such as:

```text
Playeranimator.cs
Groundenemyanimator.cs
XP&Gems.cs
Throns.asset
SeondWind Angel.prefab
ShopPannel
```

The project also contains historical terminology differences including:

```text
Tech / Gadget
coin-shot / tripleshot
flinger / marksman
```

Some newer code keeps backward-compatible aliases.

Do not perform opportunistic naming cleanup.

A seemingly misspelled filename, enum value, identifier, or prefab name may be referenced by:

* scene serialization
* prefab serialization
* ScriptableObjects
* save files
* runtime lookup
* compatibility code

Before renaming anything, search the full repository and determine compatibility impact.

---

# 11. Known Rough Edges

The following conditions were observed during the current repository inspection.

## Hybrid Input

The Unity Input System package is installed, but gameplay primarily uses legacy `UnityEngine.Input` through `InputHelper`.

This is intentional current architecture unless a migration task explicitly changes it.

Avoid mixing another third input path into gameplay.

## Old and New Enemy Implementations

Both specialized enemy implementations and older/general implementations exist.

For example:

```text
SwarmerEnemy
TankEnemy
LobberEnemy
BuzzerEnemy
```

coexist with:

```text
GroundEnemy
FlyingEnemy
```

Do not delete either set without checking prefab/scene usage.

## Animation Ownership

`PlayerAnimator` handles animation coordination, but some animation state is also controlled directly by `PlayerController`.

When fixing animation bugs, inspect both.

## SaveOnQuit Attachment

`SaveOnQuit` exists, but the previous audit did not find it attached among the component lists of the two primary scenes.

Its current runtime activation must be confirmed before relying on it.

## Mixed Instantiation and Pooling

Player bullets are pooled through `ObjectPooler`.

Many other effects, coins, enemies, and some projectiles are instantiated normally.

Do not assume all frequently spawned objects currently use pooling.

## Legacy Naming

Backward-compatibility names and inconsistent spelling exist throughout the project.

Treat cleanup as a separate migration task rather than incidental refactoring.

---

# 12. Required Workflow for Agent Changes

For any non-trivial task:

1. Read this `AGENTS.md`.
2. Run or inspect `git status`.
3. Inspect the relevant existing files.
4. Search for all references to the affected class, field, identifier, prefab, or asset.
5. Identify the system that currently owns the behavior.
6. Explain the smallest reasonable implementation approach.
7. Modify only necessary files.
8. Do not reformat unrelated code.
9. Do not make unrelated architecture changes.
10. Allow Unity to compile.
11. Report any compiler errors.
12. Identify any Unity Inspector setup still required.
13. Identify any prefab/scene changes still required.
14. State exactly which files changed.
15. State what was actually verified.
16. State what still requires Play Mode or build verification.

Never claim something was visually, physically, or behaviorally verified unless it was actually run and observed.

---

# 13. TO CONFIRM

The following information was supplied as prior project context or requested for documentation, but was not fully verified during the current repository inspection.

Agents should inspect the actual files before converting any of these into documented facts.

* Exact `Company Name` from `ProjectSettings/ProjectSettings.asset`
* Exact `Product Name` from `ProjectSettings/ProjectSettings.asset`
* Exact bundle/application identifier
* Exact scripting backend per build target
* Exact API compatibility level
* Exact currently configured target platforms
* Whether Windows standalone is the active/default build target
* Steam release status
* Steam App ID `4797570`
* Steam depot configuration
* Whether Steam launch configuration specifically expects `Duck Defender.exe`
* Whether changing Product Name would currently break the configured Steam launch option
* Exact WebGL compression configuration
* Whether WebGL compression is currently set to Disabled
* GitHub Pages WebGL deployment configuration
* WebGL Product Name / URL-encoding behavior in the current deployment
* Steam depot exclusions for `*_BurstDebugInformation_DoNotShip`
* Steam depot exclusions for D3D12 files
* Exact inventory and grouping of every `.cs` file under `Assets/Scripts`
* Exact paths of each core script within `Assets/Scripts`
* Exact contents of any `Editor/` directories
* Whether `AudioManagerEditor.cs` currently exists
* Whether `AudioManager` currently implements `SliceMode`
* Whether `SliceMode` contains `None`, `EqualSlices`, `CustomSlices`, and `AutoDetect`
* Whether audio slicing currently uses `AudioSource.SetScheduledEndTime`
* Exact filenames of non-build/demo scenes under `Assets/Scenes`
* Exact fullscreen WebGL `.jslib` plugin filename
* Whether a separate fullscreen WebGL plugin currently exists in addition to `SaveSystemBridge.jslib`
* Whether `ShopManager` currently auto-unlocks the Base/BaseSet pack on load
* Whether `MechanicsController.cs` exists
* Whether `MechanicsController.cs` is still an empty stub
* Whether XP-gem magnet behavior currently reads `PlayerStats.MagnetRange`
* Whether `PlayerStats.ReportCoinsGained` is wired to `LevelManager.AddCoins`
* Exact current coin-triggered mechanic implementations
* Exact current rarity enum values
* Exact current pack-category enum values
* Exact logging-prefix conventions such as `[SaveOnQuit]`
* Exact installed Unity Editor executable path on this development machine
* Whether any external CI/build workflow exists outside the files inspected
* Whether any project-specific tests have been added since the last repository audit

When any item above becomes relevant to a task, inspect and verify it before acting on it.

Do not guess.

---

# Default Development Principle

When choosing between:

```text
extend the existing Duck Defender system
```

and:

```text
replace it with a cleaner new architecture
```

prefer extending the existing system unless the task explicitly calls for redesign or the existing implementation cannot reasonably support the requested change.

Preserve working behavior first.

Improve architecture deliberately, not incidentally.
