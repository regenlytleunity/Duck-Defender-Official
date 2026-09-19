# Duck Defender — Agent Instructions

## Purpose

This file documents the current Duck Defender repository and defines the rules coding agents must follow when working in it.

The repository is the source of truth.

If this document disagrees with the current files, inspect the repository and follow the current implementation.

Do not invent file paths, classes, Unity settings, build scripts, tests, packages, scenes, or deployment configuration.

---

# 1. Project Overview

Duck Defender is an existing Unity 2D wave-survival/action platformer.

The player controls a duck through platforming and combat while fighting scaling enemy waves, collecting coins and XP, leveling up, and selecting card-based upgrades.

Verified project information:

* Engine: Unity
* Unity version: `6000.0.35f1`
* Unity revision: `9a3bc604008a`
* Rendering pipeline: Universal Render Pipeline
* URP version: `17.0.3`
* Rendering configuration: 2D
* Product Name: `Duck Defender`
* Company Name: `DefaultCompany`
* Bundle version: `1.0`
* Standalone application identifier: `com.DefaultCompany.Duck-Defender`
* API compatibility: `.NET Standard 2.1`
* Primary gameplay scene: `Assets/Scenes/SampleScene.unity`
* Main menu scene: `Assets/Scenes/MainMenu.unity`
* Project scripts compile into Unity's default `Assembly-CSharp`
* No `.asmdef` or `.asmref` files exist
* No project-specific automated tests exist
* No repository-specific automated build script exists

The repository contains a saved WebGL build profile.

Release status, Steam configuration, active build target, effective scripting backend, and external deployment configuration are not established by the repository and remain under **TO CONFIRM**.

---

# 2. Repository Layout

```text
Duck Defender/
├── AGENTS.md
├── .gitignore
│
├── Assets/
│   ├── Backgrounds/
│   │   └── Background art and demo content
│   │
│   ├── Cards/
│   │   ├── Packs/
│   │   └── Upgrades/
│   │       ├── Base Set Upgrades/
│   │       ├── Gadget Upgrades/
│   │       ├── Mobility Upgrades/
│   │       ├── Munitions Upgrades/
│   │       └── Survival Upgrades/
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
│   │   └── WebGL/
│   │       ├── Fullscreen.jslib
│   │       └── SaveSystemBridge.jslib
│   │
│   ├── Prefabs/
│   │   └── Gameplay, projectile, turret, effect, and UI prefabs
│   │
│   ├── Resources/
│   │   └── Currently empty
│   │
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   └── SampleScene.unity
│   │
│   ├── Scripts/
│   │   └── All project-authored C# scripts
│   │
│   ├── Settings/
│   │   ├── URP assets
│   │   ├── scene templates
│   │   └── Build Profiles/
│   │
│   ├── Sound Effects/
│   │   └── Project audio assets
│   │
│   ├── Sprites/
│   │   └── Sprite and tile assets
│   │
│   └── TextMesh Pro/
│       └── TMP resources
│
├── Packages/
│   ├── manifest.json
│   └── packages-lock.json
│
└── ProjectSettings/
    ├── ProjectVersion.txt
    ├── ProjectSettings.asset
    └── EditorBuildSettings.asset
```

## Off-Limits Generated Folders

Do not modify or treat the following as authoritative source:

```text
Library/
Temp/
obj/
Logs/
Build/
Builds/
UserSettings/
```

These are generated or machine-local.

Do not add generated IDE files to source control.

---

# 3. Complete C# Inventory

There are currently exactly **65 project-authored `.cs` files**.

All 65 are directly inside:

```text
Assets/Scripts/
```

There are no C# subfolders under `Assets/Scripts/`, and no other project-authored `.cs` files were found elsewhere under `Assets`.

Current inventory:

```text
Assets/Scripts/AudioManager.cs
Assets/Scripts/AuraController.cs
Assets/Scripts/BackgroundFiller.cs
Assets/Scripts/BouncyEnemyProjectile.cs
Assets/Scripts/ButtonClickSound.cs
Assets/Scripts/BuzzerEnemy.cs
Assets/Scripts/CardDefinition.cs
Assets/Scripts/CardDisplay.cs
Assets/Scripts/CardIndexUI.cs
Assets/Scripts/CardManager.cs
Assets/Scripts/Coin.cs
Assets/Scripts/DamagePopup.cs
Assets/Scripts/ElementalTurret.cs
Assets/Scripts/EnemyBase.cs
Assets/Scripts/EnemyHealthBar.cs
Assets/Scripts/EnemyProjectile.cs
Assets/Scripts/FireTrailPatch.cs
Assets/Scripts/FlyingEnemy.cs
Assets/Scripts/Flyingenemyanimator.cs
Assets/Scripts/FullscreenToggle.cs
Assets/Scripts/GameUI.cs
Assets/Scripts/GroundEnemy.cs
Assets/Scripts/Groundenemyanimator.cs
Assets/Scripts/Inputhelper.cs
Assets/Scripts/InputManager.cs
Assets/Scripts/LevelManager.cs
Assets/Scripts/LevelUpUI.cs
Assets/Scripts/LobberEnemy.cs
Assets/Scripts/MainMenuUI.cs
Assets/Scripts/MarksmanTurret.cs
Assets/Scripts/MechanicsController.cs
Assets/Scripts/MedicTurret.cs
Assets/Scripts/MenuController.cs
Assets/Scripts/Meteor.cs
Assets/Scripts/MobileInputController.cs
Assets/Scripts/ObjectPooler.cs
Assets/Scripts/OverheatPopup.cs
Assets/Scripts/PixelPerfectCanvasScaler.cs
Assets/Scripts/Playeranimator.cs
Assets/Scripts/PlayerController.cs
Assets/Scripts/PlayerData.cs
Assets/Scripts/PlayerHealth.cs
Assets/Scripts/PlayerStats.cs
Assets/Scripts/Projectile.cs
Assets/Scripts/ProtectorTurret.cs
Assets/Scripts/SafeAreaPanel.cs
Assets/Scripts/SaveOnQuit.cs
Assets/Scripts/SaveSystem.cs
Assets/Scripts/SecondWindAngel.cs
Assets/Scripts/SelfDestruct.cs
Assets/Scripts/SettingsMenuUI.cs
Assets/Scripts/ShockwaveExpand.cs
Assets/Scripts/ShopData.cs
Assets/Scripts/ShopManager.cs
Assets/Scripts/SlowingAuraController.cs
Assets/Scripts/SpriteHealthBar.cs
Assets/Scripts/SpriteXPBar.cs
Assets/Scripts/SwarmerEnemy.cs
Assets/Scripts/TankEnemy.cs
Assets/Scripts/TurretBase.cs
Assets/Scripts/TurretManager.cs
Assets/Scripts/VirtualJoystick.cs
Assets/Scripts/WaveManager.cs
Assets/Scripts/WeaponPlayer.cs
Assets/Scripts/XP&Gems.cs
```

Important filename/type mismatches:

```text
InputHelper             -> Assets/Scripts/Inputhelper.cs
PlayerAnimator          -> Assets/Scripts/Playeranimator.cs
GroundEnemyAnimator     -> Assets/Scripts/Groundenemyanimator.cs
FlyingEnemyAnimator     -> Assets/Scripts/Flyingenemyanimator.cs
MainMenuController      -> Assets/Scripts/MenuController.cs
ShopPackDefinition      -> Assets/Scripts/ShopData.cs
XPGem                   -> Assets/Scripts/XP&Gems.cs
Projectile.BallisticData -> nested in Assets/Scripts/Projectile.cs
```

Do not rename these merely to improve filename casing or consistency.

Serialized references and compatibility behavior may depend on their current identities.

---

# 4. Scenes

Four Unity scenes currently exist under `Assets`.

## Enabled Build Scenes

```text
Index 0: Assets/Scenes/MainMenu.unity
Index 1: Assets/Scenes/SampleScene.unity
```

These come from:

```text
ProjectSettings/EditorBuildSettings.asset
```

## Additional Scenes

```text
Assets/Backgrounds/Scenes/demoscene.unity
Assets/Settings/Scenes/URP2DSceneTemplate.unity
```

These are not included in the current global build scene list.

The saved WebGL profile inherits the global scene list.

---

# 5. Core Systems Map

## Player Movement

### `Assets/Scripts/PlayerController.cs`

Primary movement and mobility controller.

Responsibilities include:

* Rigidbody2D movement
* acceleration/deceleration
* jumping
* multi-jump
* directional dash
* ground slam
* blink
* low-gravity behavior
* fire-trail behavior
* hypersonic collision damage
* player facing
* movement animation state

The class participates in the project's singleton-style `Instance` architecture.

Extend this controller for player movement abilities rather than creating a parallel movement framework.

---

## Player Combat

### `Assets/Scripts/WeaponPlayer.cs`

Owns player weapon behavior including:

* mouse/touch aiming
* firing cadence
* projectile spawning
* parallel firing patterns
* spread patterns
* special feathers
* buckshot
* airburst
* tripleshot
* minigun heat/overheat behavior

### `Assets/Scripts/Projectile.cs`

Owns player projectile behavior.

`Projectile.BallisticData` is a nested struct in this file.

Verified projectile mechanics include:

* damage
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

Player bullets use:

```text
Assets/Scripts/ObjectPooler.cs
```

Before adding projectile mechanics, inspect:

```text
Assets/Scripts/Projectile.cs
Assets/Scripts/WeaponPlayer.cs
Assets/Scripts/PlayerStats.cs
Assets/Scripts/CardManager.cs
```

---

## Player Health and Stats

### `Assets/Scripts/PlayerHealth.cs`

Responsibilities include:

* taking damage
* healing
* regeneration
* thorns
* death
* game-over transition
* second-wind behavior

### `Assets/Scripts/PlayerStats.cs`

Central mutable runtime state for card-derived gameplay stats and abilities.

It also contains coin-related mechanics including `ReportCoinsGained()` and Money High / Powerful Profit behavior.

Do not introduce a second general runtime-stat container unless explicitly required.

---

## Animation

### `Assets/Scripts/Playeranimator.cs`

Contains the `PlayerAnimator` class.

Coordinates player animation.

`PlayerController` also directly manipulates some animation state.

When debugging animation behavior, inspect both systems.

Enemy animator scripts include:

```text
Assets/Scripts/Groundenemyanimator.cs
Assets/Scripts/Flyingenemyanimator.cs
```

---

## Waves

### `Assets/Scripts/WaveManager.cs`

Owns:

* starting waves
* selecting enemy prefabs
* enemy spawning
* multi-spawning
* difficulty scaling
* wave progression
* enemy-count tracking

Do not create a second wave manager.

---

## Enemy Base

### `Assets/Scripts/EnemyBase.cs`

Abstract common enemy superclass.

Owns shared behavior including:

* base health
* wave-scaled health
* speed
* damage
* player lookup
* facing
* health bars
* knockback
* poison
* slow
* freeze
* hit feedback
* death
* rewards
* coin drops

Movement behavior is delegated through abstract `Move()` behavior.

New enemy types should normally build on this architecture.

---

## Enemy Implementations

Current specialized enemies:

```text
Assets/Scripts/SwarmerEnemy.cs
Assets/Scripts/TankEnemy.cs
Assets/Scripts/LobberEnemy.cs
Assets/Scripts/BuzzerEnemy.cs
```

Current older/general enemy implementations:

```text
Assets/Scripts/GroundEnemy.cs
Assets/Scripts/FlyingEnemy.cs
```

Current specialized behavior includes:

* `SwarmerEnemy`

  * fast ground melee
  * separation
  * persistent strike window

* `TankEnemy`

  * slower melee unit
  * damage reduction
  * shield feedback

* `LobberEnemy`

  * moves into firing range
  * launches ballistic/bouncing projectiles

* `BuzzerEnemy`

  * hovering ranged behavior
  * retreat behavior
  * swarm separation

Do not remove the older/general enemy scripts without verifying all prefab and scene references.

---

## Enemy Projectiles

```text
Assets/Scripts/EnemyProjectile.cs
Assets/Scripts/BouncyEnemyProjectile.cs
```

`EnemyProjectile` provides conventional enemy projectile behavior.

`BouncyEnemyProjectile` provides lobbed/bouncing projectile behavior.

---

## XP, Coins, and Run Progression

### `Assets/Scripts/LevelManager.cs`

Owns:

* run XP
* leveling
* run coins
* passive coin spawning
* wave rewards
* progression UI synchronization
* coin-trigger thresholds

`LevelManager.AddCoins()` directly calls:

```text
PlayerStats.Instance.ReportCoinsGained(amount)
```

Coin reporting is therefore currently wired.

### `Assets/Scripts/Coin.cs`

Physical coin behavior.

The coin magnet reads:

```text
PlayerStats.Instance.MagnetRange
```

### `Assets/Scripts/XP&Gems.cs`

Contains `XPGem`.

Important current rough edge:

`XPGem` uses its own `MagnetRange` field, defaulting to `3.0f`.

It does **not** currently read:

```text
PlayerStats.MagnetRange
```

This means card/stat changes to the central magnet range affect coins but not XP gems.

Do not silently change this behavior as part of unrelated work.

---

## Coin-Triggered Mechanics

### Money High / Powerful Profit

Implemented primarily in:

```text
Assets/Scripts/PlayerStats.cs
Assets/Scripts/Projectile.cs
```

`ReportCoinsGained()` adds independently expiring stacks when the mechanic is enabled.

Each stack expires based on:

```text
Time.time + MoneyHighDuration
```

Damage multiplier is based on active stacks.

### Coin Meteor

Implemented through:

```text
Assets/Scripts/LevelManager.cs
Assets/Scripts/PlayerController.cs
Assets/Scripts/Meteor.cs
```

`LevelManager.AddCoins()` accumulates coin progress toward meteor triggers.

The threshold logic preserves remainder by using a loop.

Default/fallback threshold is `10`.

`Meteor` currently deals:

```text
ceil(enemy.MaxHealth * 0.5)
```

It reads:

```text
PlayerStats.MeteorRadius
```

It does **not** currently consume `PlayerStats.MeteorDamage`.

### Tripleshot

Coin-triggered through:

```text
Assets/Scripts/LevelManager.cs
Assets/Scripts/WeaponPlayer.cs
```

Default/fallback threshold is `15`.

`WeaponPlayer` adds two bonus spread projectiles during `TripleshotDuration`.

Retriggering restarts the timer.

### Passive Income

Implemented in:

```text
Assets/Scripts/PlayerController.cs
Assets/Scripts/LevelManager.cs
```

`CoinsPerSecond` becomes physical coins through `LevelManager.SpawnPassiveCoin()`.

### Wave Rewards

`LevelManager` spawns physical `CoinsPerWave` rewards.

If `PassiveCoinPrefab` is missing, the spawn path falls back to:

```text
AddCoins(1)
```

### Interest

Wave completion calculates:

```text
floor(TotalCoins * InterestRate)
```

Positive results go through `AddCoins()` and therefore participate in coin-trigger mechanics.

---

# 6. Cards and ScriptableObjects

## Definitions

### `Assets/Scripts/CardDefinition.cs`

Defines:

* `CardDefinition`
* `CardPackType`
* `CardRarity`
* `InRunStackMode`
* `StatType`
* `CardStatModifier`

Current `CardPackType` members:

```text
Munitions = 0
Mobility  = 1
Survival  = 2
Gadget    = 3
BaseSet   = 4
```

There is no current:

```text
CardPackType.Tech
CardPackType.Base
```

Current `CardRarity` members:

```text
Common    = 0
Rare      = 1
Legendary = 2
Corrupted = 3
```

Current `InRunStackMode` members:

```text
MatchBaseValue   = 0
MatchShopGrowth  = 1
Custom           = 2
```

### `Assets/Scripts/ShopData.cs`

Contains:

```text
ShopPackDefinition
```

---

## Card Assets

There are currently exactly **50 `CardDefinition` assets**.

Distribution:

```text
Assets/Cards/Upgrades/Base Set Upgrades/   4
Assets/Cards/Upgrades/Gadget Upgrades/     12
Assets/Cards/Upgrades/Mobility Upgrades/   9
Assets/Cards/Upgrades/Munitions Upgrades/  13
Assets/Cards/Upgrades/Survival Upgrades/   12
```

Current rarity distribution:

```text
Common      24
Rare        13
Legendary   13
Corrupted    0
```

No duplicate card IDs were found during the audit.

Treat card IDs as stable persistent identifiers.

Do not rename existing IDs without considering save-data migration.

---

## Pack Assets

Current pack assets:

```text
Assets/Cards/Packs/Pack_BasePack.asset
Assets/Cards/Packs/Pack_Mobility.asset
Assets/Cards/Packs/Pack_Munitions.asset
Assets/Cards/Packs/Pack_Survival.asset
Assets/Cards/Packs/Pack_Tech.asset
```

Current categories:

```text
Pack_BasePack.asset  -> BaseSet
Pack_Mobility.asset  -> Mobility
Pack_Munitions.asset -> Munitions
Pack_Survival.asset  -> Survival
Pack_Tech.asset      -> Gadget
```

`Pack_Tech.asset` retains a historical filename but currently represents the `Gadget` category.

Do not rename it incidentally.

---

## CardManager

### `Assets/Scripts/CardManager.cs`

Owns:

* rarity rolling
* unlocked-card filtering
* run card pool
* card pickups
* stacking
* application of card modifiers to runtime systems

It applies changes to systems including:

```text
PlayerStats
PlayerController
WeaponPlayer
```

and other gameplay components.

---

# 7. Shop and Permanent Progression

### `Assets/Scripts/ShopManager.cs`

Owns permanent economy and collection/upgrading behavior.

`LoadEconomy()` calls an `UnlockBaseSet()` routine.

The routine attempts to add missing BaseSet cards from the serialized `AllCards` list.

## Known BaseSet Wiring Issue

The current `ShopManager` in:

```text
Assets/Scenes/MainMenu.unity
```

contains 46 `AllCards` references.

It currently omits all four BaseSet cards:

```text
Assets/Cards/Upgrades/Base Set Upgrades/Basic Damage.asset
Assets/Cards/Upgrades/Base Set Upgrades/Basic Health.asset
Assets/Cards/Upgrades/Base Set Upgrades/Basic Income.asset
Assets/Cards/Upgrades/Base Set Upgrades/Basic Mobility.asset
```

No code was found that populates `AllCards` at runtime.

Therefore, although `UnlockBaseSet()` exists, the current serialized MainMenu configuration does not provide those BaseSet assets to the routine.

The MainMenu `AvailablePacks` list also currently contains four references and omits:

```text
Assets/Cards/Packs/Pack_BasePack.asset
```

Treat this as a verified wiring issue.

Do not assume BaseSet auto-unlock currently works as intended.

---

# 8. Save System

### `Assets/Scripts/SaveSystem.cs`

Permanent progression is serialized to:

```text
Application.persistentDataPath/duck_save.json
```

Stored information includes:

* total coins
* card ID
* permanent card level
* duplicate count
* unlock state

Missing or corrupt data falls back to fresh player data.

Do not casually change the serialized save-data schema.

Persistent-data changes must consider backward compatibility.

---

## Player Data

### `Assets/Scripts/PlayerData.cs`

Contains save-data structures.

New `CardSaveData` values currently initialize with:

```text
Level = 1
DuplicateCount = 0
IsUnlocked = true
```

Inspect the current file before changing defaults.

---

## WebGL Save Bridge

### `Assets/Plugins/WebGL/SaveSystemBridge.jslib`

Exports:

```text
SyncFiles
```

It calls:

```text
FS.syncfs(false, callback)
```

to flush filesystem writes.

### `Assets/Scripts/SaveSystem.cs`

Imports and calls `SyncFiles` under:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
```

Do not change the `.jslib` extension.

Unity requires this extension for WebGL JavaScript-library integration.

---

## SaveOnQuit

### `Assets/Scripts/SaveOnQuit.cs`

`Awake()` calls:

```text
DontDestroyOnLoad(gameObject)
```

It handles:

```text
OnApplicationQuit()
OnApplicationPause(true)
```

It does not currently implement `OnApplicationFocus()`.

Its save routine reloads data through `SaveSystem.LoadData()` and writes that result with `SaveSystem.SaveData()`.

Important current state:

* no references to the script GUID were found in inspected scenes
* no references were found in inspected prefabs
* no references were found in inspected `.asset` files
* no runtime creation path was found in project code

Therefore the implementation exists, but the repository provides no evidence that a `SaveOnQuit` component is actually instantiated in the current game.

Do not rely on it without verifying runtime setup.

---

# 9. Audio

### `Assets/Scripts/AudioManager.cs`

`AudioManager` is persistent.

`Awake()` calls:

```text
DontDestroyOnLoad(gameObject)
```

Volume PlayerPrefs keys include:

```text
DuckDefender_SFXVolume
DuckDefender_MusicVolume
```

## Audio Slicing

`AudioManager` currently defines:

```text
SliceMode.None         = 0
SliceMode.EqualSlices  = 1
SliceMode.CustomSlices = 2
```

There is no current:

```text
SliceMode.AutoDetect
```

`PlaySlice()` uses:

```csharp
AudioSource.SetScheduledEndTime(...)
```

Slice duration accounts for playback pitch.

Slice selection can avoid immediately repeating the previous slice.

The current `MainMenu.unity` SFX library contains five entries configured with:

```text
SliceMode.CustomSlices
```

There is currently no project-authored:

```text
AudioManagerEditor.cs
```

and no project-authored custom C# inspector was found anywhere under `Assets`.

---

# 10. UI

The project uses Unity uGUI and TextMesh Pro.

It does not use UI Toolkit as its primary UI architecture.

## Gameplay UI

```text
Assets/Scripts/GameUI.cs
Assets/Scripts/LevelUpUI.cs
Assets/Scripts/CardDisplay.cs
Assets/Scripts/SpriteHealthBar.cs
Assets/Scripts/SpriteXPBar.cs
Assets/Scripts/EnemyHealthBar.cs
Assets/Scripts/DamagePopup.cs
Assets/Scripts/OverheatPopup.cs
Assets/Scripts/SafeAreaPanel.cs
Assets/Scripts/PixelPerfectCanvasScaler.cs
```

`GameUI` manages gameplay information such as:

* coins
* health
* XP
* waves
* enemy count
* banners
* damage feedback
* game-over UI

`LevelUpUI` handles the level-up card choice flow.

`CardDisplay` is reused for card presentation.

Prefer existing shared UI components over parallel UI implementations.

## Menu UI

```text
Assets/Scripts/MainMenuUI.cs
Assets/Scripts/MenuController.cs
Assets/Scripts/ShopManager.cs
Assets/Scripts/CardIndexUI.cs
Assets/Scripts/SettingsMenuUI.cs
Assets/Scripts/FullscreenToggle.cs
Assets/Scripts/ButtonClickSound.cs
```

`MenuController.cs` contains the `MainMenuController` type.

---

# 11. Input

Input is currently hybrid.

The project uses:

```text
com.unity.inputsystem 1.11.2
```

but gameplay primarily reads legacy `UnityEngine.Input` through the project's abstraction.

Important files:

```text
Assets/Scripts/Inputhelper.cs
Assets/Scripts/InputManager.cs
Assets/Scripts/MobileInputController.cs
Assets/Scripts/VirtualJoystick.cs
```

`Inputhelper.cs` contains the `InputHelper` type.

Project setting:

```text
activeInputHandler: 2
```

means both input backends are enabled.

Do not migrate gameplay to the new Input System unless explicitly requested.

New player-input behavior should normally integrate with `InputHelper` so desktop and mobile behavior remain consistent.

---

# 12. Fullscreen WebGL Interop

### `Assets/Plugins/WebGL/Fullscreen.jslib`

Exports:

```text
JSEnterFullscreen
JSExitFullscreen
JSIsFullscreen
```

It uses browser fullscreen APIs with WebKit alternatives.

### `Assets/Scripts/FullscreenToggle.cs`

Imports these functions under:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
```

Preserve platform guards.

---

# 13. Turrets and Companion Systems

### `Assets/Scripts/TurretBase.cs`

Base class for player-following companion/turret behavior.

Existing implementations:

```text
Assets/Scripts/MarksmanTurret.cs
Assets/Scripts/MedicTurret.cs
Assets/Scripts/ProtectorTurret.cs
Assets/Scripts/ElementalTurret.cs
Assets/Scripts/SecondWindAngel.cs
```

Manager:

```text
Assets/Scripts/TurretManager.cs
```

`TurretManager` creates, removes, and positions companion objects based on runtime state.

Extend the existing system rather than creating another companion manager.

---

# 14. Manager and Dependency Pattern

The project relies heavily on singleton-style `Instance` access.

Verified examples include:

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

Dependencies are generally obtained through:

* static `Instance`
* Inspector references
* `GetComponent`
* tags

The project does not currently use a dependency-injection framework or service container.

Do not introduce a DI framework as incidental cleanup.

Work with the established architecture unless redesign is explicitly requested.

---

# 15. Packages

Direct non-module dependencies currently include:

```text
com.unity.collab-proxy               2.10.2
com.unity.feature.2d                 2.0.1
com.unity.ide.rider                  3.0.31
com.unity.ide.visualstudio           2.0.22
com.unity.inputsystem                1.11.2
com.unity.multiplayer.center         1.0.0
com.unity.render-pipelines.universal 17.0.3
com.unity.test-framework             1.4.5
com.unity.timeline                   1.8.7
com.unity.ugui                       2.0.0
com.unity.visualscripting            1.9.5
```

Unity built-in modules are also declared at version `1.0.0`.

Do not upgrade, add, or remove packages unless explicitly requested.

---

# 16. Build and Player Settings

Verified values from:

```text
ProjectSettings/ProjectSettings.asset
```

include:

```text
companyName: DefaultCompany
productName: Duck Defender
bundleVersion: 1.0
applicationIdentifier.Standalone: com.DefaultCompany.Duck-Defender
activeInputHandler: 2
API compatibility: .NET Standard 2.1
desktop resolution: 1920 x 1080
web resolution: 960 x 600
resizableWindow: false
runInBackground: false
```

The serialized `scriptingBackend` dictionary is empty.

Do not claim this establishes the effective scripting backend for every target.

The current active/default build target was not established by the audit.

---

# 17. WebGL Build Configuration

There are two relevant sources of WebGL settings.

## Global Project Settings

`ProjectSettings/ProjectSettings.asset` currently contains:

```text
webGLCompressionFormat: Disabled
webGLDecompressionFallback: enabled
webGLTemplate: APPLICATION:Default
webGLNameFilesAsHashes: false
webGLDataCaching: false
webGLThreadsSupport: false
webGLEnableWebGPU: false
webGLInitialMemorySize: 32
webGLMaximumMemorySize: 2048
webGLMemoryGrowthMode: 2
```

## Saved WebGL Build Profile

```text
Assets/Settings/Build Profiles/New Web Profile.asset
```

is a WebGL profile.

It currently contains WebGL player settings in which:

```text
webGLCompressionFormat: Gzip
```

The profile uses the global scene list.

It is non-development and has profiler/debugging disabled.

Important:

The global project setting says **Disabled**, while the saved WebGL profile says **Gzip**.

Do not claim that all WebGL builds use one of these settings without first determining which profile/settings are actually being used for the build.

---

# 18. Coding Conventions

Match the style of the file being modified.

Current conventions include:

* MonoBehaviour classes generally use the global namespace
* no project-wide namespace convention
* one primary class per file
* PascalCase for public members
* underscore-prefixed private runtime fields
* `[Header]` for Inspector organization
* `[Tooltip]` for Inspector documentation
* occasional `[Range]`
* coroutines for timing and gameplay effects
* tags and layer masks for discovery/collision filtering
* singleton-style static `Instance`
* ScriptableObjects for card configuration
* Inspector configuration for much other gameplay data

Newer or expanded code sometimes includes:

* XML summaries
* section-divider comments
* version-oriented comments
* compatibility aliases

Do not reformat unrelated code while implementing a feature.

---

# 19. Logging Conventions

There is no single project-wide logging-prefix standard.

Follow the surrounding class.

Verified examples include:

```text
SaveOnQuit.cs            -> [SaveOnQuit]
SaveSystem.cs            -> [SaveSystem]
AudioManager.cs          -> [AudioManager]
PlayerStats.cs           -> [MoneyHigh]
BouncyEnemyProjectile.cs -> [BouncyProjectile]
PlayerHealth.cs          -> [Second Wind]
WaveManager.cs           -> [WaveManager]
```

Other scripts use plain messages.

Do not introduce a new logging framework unless explicitly requested.

---

# 20. Rules for Agents

## Inspect Before Editing

For every non-trivial task:

1. Read this file.
2. Inspect `git status`.
3. Inspect the relevant implementation.
4. Search for references.
5. Identify which existing system owns the behavior.
6. Prefer the smallest reasonable change.
7. Preserve existing behavior outside the requested scope.

Do not begin with broad architectural refactoring.

---

## `.meta` Files

Never hand-author a Unity `.meta` file.

Never casually modify, regenerate, or delete an existing `.meta` file.

When creating a new Unity asset or script:

1. create the source file
2. let Unity import it
3. let Unity generate the `.meta`
4. ensure the `.meta` is present before committing

When moving or renaming an asset, preserve its `.meta` and GUID.

---

## Serialized Unity Files

Do not manually rewrite raw YAML in:

```text
*.unity
*.prefab
*.asset
```

unless explicitly requested.

These contain serialized object references, GUIDs, and fileIDs.

Prefer Unity Editor operations or C# changes.

---

## Serialized Fields

Do not casually rename serialized fields.

Existing scenes and prefabs may depend on the serialized field name.

If a rename is required, consider:

```csharp
[FormerlySerializedAs("OldFieldName")]
```

and verify serialization compatibility.

---

## Project Settings

Do not alter these unless explicitly asked:

* Product Name
* Company Name
* application identifiers
* scripting backend
* API compatibility
* graphics APIs
* WebGL compression
* build target
* build profiles
* package versions

Build configuration may be tied to external distribution systems not represented in the repository.

---

## Scene and Inspector Wiring

Code review alone cannot prove Inspector setup.

If a change requires:

* component attachment
* prefab assignment
* AudioClip assignment
* ScriptableObject assignment
* LayerMask configuration
* tag configuration
* UI references
* scene hierarchy edits
* manager placement

state explicitly that Unity Editor setup is required.

Never claim those references are configured unless they were actually verified.

---

## Editor-Only Code

No project-authored `Editor/` directories currently exist.

If editor-only code is added, it must either:

* live under an `Editor/` folder

or

* be appropriately guarded with:

```csharp
#if UNITY_EDITOR
#endif
```

Do not allow `UnityEditor` dependencies into runtime code.

---

## Platform-Specific Code

Use compiler guards for platform-specific behavior.

Current WebGL integrations use:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
#endif
```

Use:

```csharp
#if UNITY_WEBGL
#endif
```

or:

```csharp
#if UNITY_STANDALONE
#endif
```

where appropriate.

Do not execute browser/native platform interop on unsupported targets.

---

## Git Safety

Do not automatically:

* commit
* push
* force-push
* rewrite history
* discard unrelated user changes

Do not use destructive commands such as:

```text
git reset --hard
git clean -fd
git push --force
```

unless explicitly instructed.

Before making substantial changes:

```text
git status
```

and preserve unrelated work.

---

# 21. Build and Verification

## Automated Tests

Unity Test Framework `1.4.5` is installed.

However:

* no project-specific NUnit tests were found
* no Unity Test Framework test methods were found
* no project test assembly definitions were found

Do not claim automated tests passed unless tests are later added and actually run.

---

## Build Automation

No project-authored code using:

```text
BuildPipeline
BuildPlayer
```

was found.

No repository CI workflow was found.

Do not invent build automation.

---

## Local Unity Installation

Verified Unity executable on the current development machine:

```text
C:\Program Files\Unity\Hub\Editor\6000.0.35f1\Editor\Unity.exe
```

Verified project path on this machine:

```text
C:\Users\regen\Duck Defender 2025-11-19_13-03-32\Duck Defender
```

A command-line import/compile smoke check can be run from Windows Command Prompt with:

```cmd
"C:\Program Files\Unity\Hub\Editor\6000.0.35f1\Editor\Unity.exe" ^
-batchmode ^
-quit ^
-projectPath "C:\Users\regen\Duck Defender 2025-11-19_13-03-32\Duck Defender" ^
-logFile -
```

This is not a replacement for gameplay testing.

It can detect import/compile failures but does not verify gameplay, scene wiring, physics, animations, or visual behavior.

---

## Normal Verification

For gameplay changes:

1. save modified files
2. let Unity recompile
3. check the Console
4. open `Assets/Scenes/SampleScene.unity`
5. enter Play Mode
6. exercise the changed mechanic
7. check runtime errors
8. verify physics/UI/animation behavior
9. exit Play Mode
10. review changed files

For menu, shop, collection, and meta-progression work:

1. open `Assets/Scenes/MainMenu.unity`
2. enter Play Mode
3. exercise the affected flow
4. verify save behavior when applicable
5. check the Console

Never say gameplay was verified unless it was actually run.

---

# 22. Common Task: Add a New Card

1. Inspect:

   ```text
   Assets/Scripts/CardDefinition.cs
   Assets/Scripts/CardManager.cs
   Assets/Scripts/PlayerStats.cs
   ```

2. Inspect the runtime system that will consume the new effect.

3. Determine whether an existing `StatType` already represents it.

4. Add code only if a new runtime effect is necessary.

5. Create the `CardDefinition` asset through Unity in the appropriate directory:

   ```text
   Assets/Cards/Upgrades/Base Set Upgrades/
   Assets/Cards/Upgrades/Gadget Upgrades/
   Assets/Cards/Upgrades/Mobility Upgrades/
   Assets/Cards/Upgrades/Munitions Upgrades/
   Assets/Cards/Upgrades/Survival Upgrades/
   ```

6. Give the card a unique stable ID.

7. Configure rarity, category, description, art, and stat modifiers in Unity.

8. Ensure the card is included in any serialized list/pool required by `CardManager` or `ShopManager`.

9. Test in-run stacking.

10. If permanently collectible, test save/load and shop behavior.

Do not hand-write the `.asset` YAML.

---

# 23. Common Task: Add a New Enemy

1. Inspect:

   ```text
   Assets/Scripts/EnemyBase.cs
   ```

2. Inspect the closest existing implementation:

   ```text
   Assets/Scripts/SwarmerEnemy.cs
   Assets/Scripts/TankEnemy.cs
   Assets/Scripts/LobberEnemy.cs
   Assets/Scripts/BuzzerEnemy.cs
   Assets/Scripts/GroundEnemy.cs
   Assets/Scripts/FlyingEnemy.cs
   ```

3. Reuse `EnemyBase` for shared health, statuses, death, and rewards.

4. Implement only unique behavior.

5. Create/configure the enemy prefab in the existing `Assets/Enemies/` structure using Unity.

6. Configure physics, collider, animation, and Inspector references.

7. Integrate the prefab with:

   ```text
   Assets/Scripts/WaveManager.cs
   ```

   if required by the current spawn configuration.

8. Verify:

   * spawning
   * wave scaling
   * movement
   * attacks
   * player damage
   * status effects
   * death
   * coin drops

9. Test in `SampleScene`.

---

# 24. Common Task: Add a Player Stat

1. Inspect:

   ```text
   Assets/Scripts/PlayerStats.cs
   Assets/Scripts/CardDefinition.cs
   Assets/Scripts/CardManager.cs
   ```

2. Search for similar existing stats.

3. Decide whether the value is:

   * run-only
   * card-derived
   * persistent
   * calculated

4. Add the runtime value to the existing owning system.

5. Extend `StatType` only if necessary.

6. Add application logic to `CardManager` only if required.

7. Update the actual consumer.

8. If the stat affects persistence, inspect:

   ```text
   Assets/Scripts/PlayerData.cs
   Assets/Scripts/SaveSystem.cs
   Assets/Scripts/ShopManager.cs
   ```

9. Test:

   * base value
   * stacking
   * reset behavior
   * existing card interactions
   * persistence when applicable

Do not create a second general stat container.

---

# 25. Common Task: Add Audio

1. Inspect:

   ```text
   Assets/Scripts/AudioManager.cs
   ```

2. Inspect similar sounds in the current SFX library.

3. Import audio under the project's existing audio assets, including:

   ```text
   Assets/Sound Effects/
   ```

4. Let Unity create the `.meta`.

5. Use existing `AudioManager` playback behavior.

6. If slicing is required, use one of the currently supported modes:

   ```text
   None
   EqualSlices
   CustomSlices
   ```

7. Configure clip references through Unity Inspector.

8. Test:

   * playback
   * pitch
   * volume controls
   * slicing if applicable
   * scene transitions if persistent behavior matters

Do not look for or depend on an `AudioManagerEditor.cs`; none currently exists.

---

# 26. Known Rough Edges

These are verified current-code conditions, not planned work.

## Empty MechanicsController

```text
Assets/Scripts/MechanicsController.cs
```

currently exists as a zero-byte file.

It contains no class or methods.

Do not assume it owns any mechanic.

---

## XP Gem Magnet Range Is Separate

```text
Assets/Scripts/XP&Gems.cs
```

uses its own `MagnetRange`.

It does not consume:

```text
PlayerStats.MagnetRange
```

Coins do consume the central magnet range through:

```text
Assets/Scripts/Coin.cs
```

This is a verified behavioral mismatch.

---

## BaseSet Shop Wiring

`ShopManager.UnlockBaseSet()` exists, but the current `MainMenu.unity` serialized `AllCards` list omits all four BaseSet cards.

`AvailablePacks` also omits `Pack_BasePack.asset`.

Do not assume BaseSet auto-unlock currently functions as intended.

---

## SaveOnQuit Is Not Wired

`Assets/Scripts/SaveOnQuit.cs` implements persistence callbacks and `DontDestroyOnLoad`.

No current serialized reference or runtime creation path was found.

Its presence in the codebase does not establish that it runs.

---

## WebGL Compression Is Inconsistent

Global project settings:

```text
Disabled
```

Saved WebGL profile:

```text
Gzip
```

Do not assume which one is used by the latest or production build.

---

## Audio Documentation Was Previously Stale

Current `SliceMode` is exactly:

```text
None
EqualSlices
CustomSlices
```

There is no `AutoDetect`.

There is no project `AudioManagerEditor.cs`.

---

## Old and New Enemy Implementations Coexist

Specialized and general implementations coexist.

Do not delete older classes without checking serialization and prefab use.

---

## Mixed Object Pooling

Player bullets use `ObjectPooler`.

Many other gameplay objects are instantiated normally.

Do not assume everything should already be pooled.

Optimize deliberately and only when justified.

---

# 27. Performance Guidelines

Be cautious in:

```text
Update()
FixedUpdate()
enemy loops
projectile loops
physics overlap queries
raycasts
target searches
frequent coroutines
```

Avoid avoidable allocations in hot paths.

Prefer cached references to repeated scene-wide searches.

Reuse `ObjectPooler` where appropriate, but do not rewrite all spawning systems merely for stylistic consistency.

---

# 28. Naming and Compatibility

Current historical naming includes:

```text
Inputhelper.cs
Playeranimator.cs
Groundenemyanimator.cs
Flyingenemyanimator.cs
XP&Gems.cs
Pack_Tech.asset
```

Other legacy names may also remain for compatibility.

Do not perform opportunistic naming cleanup.

Before renaming anything:

1. search the repository
2. inspect scene/prefab references
3. inspect ScriptableObjects
4. inspect save identifiers
5. preserve compatibility where necessary

---

# 29. Required Agent Workflow

For any non-trivial task:

1. Read `AGENTS.md`.
2. Check `git status`.
3. Inspect the relevant files.
4. Search all references to affected classes, fields, IDs, and assets.
5. Identify the existing owner of the behavior.
6. Explain the smallest reasonable implementation.
7. Modify only necessary files.
8. Avoid unrelated formatting.
9. Avoid incidental architecture changes.
10. Let Unity compile.
11. Report compiler errors.
12. State any required Inspector work.
13. State any required scene/prefab work.
14. State exactly which files changed.
15. State what was actually verified.
16. State what still requires Play Mode/build verification.

Never claim something was tested if it was not actually tested.

---

# 30. TO CONFIRM

The following remain unresolved from repository/static inspection.

Do not treat them as facts until verified from the relevant external configuration or runtime environment.

* Effective scripting backend for each build target
* Current active/default Unity build target
* Which build profile/settings were used for the latest WebGL build
* Effective compression format of the latest deployed WebGL build
* Complete Windows standalone output configuration
* Windows build architecture
* Actual Windows build output filename
* Actual Windows build output location
* Effective application identifiers for platforms without explicit entries
* Steam release status
* Whether Steam App ID `4797570` is currently used by this project
* Steam depot configuration
* Steam launch executable configuration
* Whether changing Product Name would break the current Steam launch option
* Steam depot exclusions for `*_BurstDebugInformation_DoNotShip`
* Steam depot exclusions for D3D12 files
* Actual GitHub Pages deployment configuration
* Current deployed GitHub Pages URL
* Current filename/URL-encoding behavior in the deployed WebGL build
* External CI/build workflows stored outside this repository
* Runtime activation of `SaveOnQuit`
* Actual Play Mode behavior of any system not explicitly tested

---

# Default Development Principle

Prefer:

```text
extend the existing Duck Defender system
```

over:

```text
replace it with a cleaner parallel architecture
```

unless the task explicitly calls for redesign or the existing implementation cannot reasonably support the requested feature.

Preserve working behavior first.

Improve architecture deliberately, not incidentally.
