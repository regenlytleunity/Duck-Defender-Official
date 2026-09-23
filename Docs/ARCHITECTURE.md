# Duck Defender — Architecture Reference

## Purpose

This document describes the verified current architecture of Duck Defender.

It is a reference for coding agents and developers working in the repository.

This is **current-state documentation**, not a roadmap.

Repository files remain the source of truth.

---

# Project Overview

Duck Defender is a Unity 2D wave-survival/action platformer.

The player controls a duck through platforming and combat while fighting scaling enemy waves, collecting coins and XP, leveling up, and choosing card-based upgrades.

Verified project information:

```text
Unity version:       6000.0.35f1
Unity revision:      9a3bc604008a
Rendering pipeline:  Universal Render Pipeline
URP version:         17.0.3
Rendering mode:      2D
Product Name:        Duck Defender
Company Name:        DefaultCompany
Bundle version:      1.0
Standalone ID:       com.DefaultCompany.Duck-Defender
API compatibility:   .NET Standard 2.1
```

Project scripts currently compile into:

```text
Assembly-CSharp
```

No project `.asmdef` or `.asmref` files currently exist.

No project-specific automated tests currently exist.

No repository-specific automated build script currently exists.

---

# Repository Layout

```text
Duck Defender/
├── AGENTS.md
├── Docs/
│   ├── ARCHITECTURE.md
│   └── WORKFLOWS.md
│
├── Assets/
│   ├── Backgrounds/
│   ├── Cards/
│   │   ├── Packs/
│   │   └── Upgrades/
│   ├── Enemies/
│   ├── Fonts/
│   ├── Player/
│   ├── Plugins/
│   │   └── WebGL/
│   ├── Prefabs/
│   ├── Resources/
│   ├── Scenes/
│   ├── Scripts/
│   ├── Settings/
│   ├── Sound Effects/
│   ├── Sprites/
│   └── TextMesh Pro/
│
├── Packages/
└── ProjectSettings/
```

Generated/local directories include:

```text
Library/
Temp/
obj/
Logs/
Build/
Builds/
UserSettings/
```

These are not authoritative project source.

---

# Script Layout

There are currently **65 project-authored C# files**.

All are directly under:

```text
Assets/Scripts/
```

There are currently no script subfolders.

Inventory:

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

Important filename/type distinctions:

```text
Inputhelper.cs             -> InputHelper
Playeranimator.cs          -> PlayerAnimator
Groundenemyanimator.cs     -> GroundEnemyAnimator
Flyingenemyanimator.cs     -> FlyingEnemyAnimator
MenuController.cs          -> MainMenuController
ShopData.cs                -> ShopPackDefinition
XP&Gems.cs                 -> XPGem
Projectile.cs              -> Projectile.BallisticData nested struct
```

Do not rename these merely for stylistic consistency.

---

# Scenes

## Enabled Build Scenes

```text
0: Assets/Scenes/MainMenu.unity
1: Assets/Scenes/SampleScene.unity
```

Configured through:

```text
ProjectSettings/EditorBuildSettings.asset
```

## Additional Scenes

```text
Assets/Backgrounds/Scenes/demoscene.unity
Assets/Settings/Scenes/URP2DSceneTemplate.unity
```

These are not currently part of the global build scene list.

---

# Player Architecture

The player uses component composition rather than one monolithic script.

## PlayerController

```text
Assets/Scripts/PlayerController.cs
```

Responsibilities include:

* Rigidbody2D movement
* acceleration/deceleration
* jumping
* multi-jump
* dash
* ground slam
* blink
* low gravity
* fire trail
* hypersonic collision damage
* player facing
* movement animation state

Uses the project's singleton-style architecture.

---

## WeaponPlayer

```text
Assets/Scripts/WeaponPlayer.cs
```

Responsibilities include:

* mouse/touch aiming
* fire cadence
* projectile spawning
* parallel shots
* spread patterns
* special feathers
* buckshot
* airburst
* tripleshot
* minigun heat
* minigun overheat

---

## Projectile

```text
Assets/Scripts/Projectile.cs
```

Contains nested:

```text
Projectile.BallisticData
```

Current projectile features include:

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

---

## PlayerHealth

```text
Assets/Scripts/PlayerHealth.cs
```

Responsibilities include:

* damage
* healing
* regeneration
* thorns
* death
* game-over transition
* second wind

---

## PlayerStats

```text
Assets/Scripts/PlayerStats.cs
```

Central mutable runtime state for many card-derived stats and abilities.

Also contains coin-driven mechanics including `ReportCoinsGained()` and Money High / Powerful Profit behavior.

---

## PlayerAnimator

```text
Assets/Scripts/Playeranimator.cs
```

Contains `PlayerAnimator`.

Animation ownership is partially shared with `PlayerController`, which directly manipulates some animation state.

---

# Wave Architecture

## WaveManager

```text
Assets/Scripts/WaveManager.cs
```

Responsibilities include:

* starting waves
* selecting enemies
* enemy spawning
* multi-spawning
* difficulty scaling
* wave progression
* enemy count tracking

---

# Enemy Architecture

## EnemyBase

```text
Assets/Scripts/EnemyBase.cs
```

Abstract common enemy superclass.

Shared behavior includes:

* health
* wave-scaled health
* movement speed
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

Movement is delegated through abstract `Move()` behavior.

---

## Specialized Enemy Classes

```text
Assets/Scripts/SwarmerEnemy.cs
Assets/Scripts/TankEnemy.cs
Assets/Scripts/LobberEnemy.cs
Assets/Scripts/BuzzerEnemy.cs
```

### SwarmerEnemy

* fast ground melee
* separation behavior
* persistent strike window

### TankEnemy

* slower melee
* damage reduction
* shield feedback

### LobberEnemy

* approaches firing range
* launches ballistic/bouncing projectiles

### BuzzerEnemy

* hovering ranged behavior
* retreat behavior
* swarm separation

---

## Older / General Enemy Implementations

```text
Assets/Scripts/GroundEnemy.cs
Assets/Scripts/FlyingEnemy.cs
```

These coexist with the specialized enemy implementations.

Do not assume they are obsolete without checking serialized references.

---

## Enemy Projectiles

```text
Assets/Scripts/EnemyProjectile.cs
Assets/Scripts/BouncyEnemyProjectile.cs
```

---

# Economy and Progression

## LevelManager

```text
Assets/Scripts/LevelManager.cs
```

Responsibilities include:

* XP
* leveling
* run coins
* passive coin spawning
* wave rewards
* progression UI synchronization
* coin-trigger thresholds

`LevelManager.AddCoins()` calls:

```text
PlayerStats.Instance.ReportCoinsGained(amount)
```

---

## Coin

```text
Assets/Scripts/Coin.cs
```

Physical coin behavior.

Coin magnet behavior reads:

```text
PlayerStats.Instance.MagnetRange
```

---

## XP Gem

```text
Assets/Scripts/XP&Gems.cs
```

Contains `XPGem`.

Current behavior uses its own:

```text
MagnetRange
```

with a default of `3.0f`.

It does not currently use:

```text
PlayerStats.MagnetRange
```

This differs from coin magnet behavior.

---

# Coin-Triggered Mechanics

## Money High / Powerful Profit

Primary files:

```text
Assets/Scripts/PlayerStats.cs
Assets/Scripts/Projectile.cs
```

`ReportCoinsGained()` creates independently expiring stacks.

Stacks expire based on:

```text
Time.time + MoneyHighDuration
```

Damage scales from active stack count.

---

## Coin Meteor

Files:

```text
Assets/Scripts/LevelManager.cs
Assets/Scripts/PlayerController.cs
Assets/Scripts/Meteor.cs
```

Coin Meteor targets the nearest living enemy. Its selected card supplies a threshold of
50 down to 35 coins and flat non-feather damage of 10 up to 35. Damage runs through
PlayerStats.CalculateDamage and applies once per enemy, independent of collider count.
MeteorRadius remains an authored runtime stat. Threshold handling preserves remainder.

Absolute Extinction retains the level-6 primary meteor. Each of its three dropped coins
can summon a secondary meteor at a random viewport position. Secondary meteors deal
50% damage and drop no coins, preventing recursive secondary-coin chains.

---

## Tripleshot

Files:

```text
Assets/Scripts/LevelManager.cs
Assets/Scripts/WeaponPlayer.cs
```

Default/fallback threshold:

```text
15
```

Adds two bonus spread projectiles during `TripleshotDuration`.

Retriggering restarts the duration.

---

## Passive Income

Files:

```text
Assets/Scripts/PlayerController.cs
Assets/Scripts/LevelManager.cs
```

`CoinsPerSecond` becomes physical coins through:

```text
LevelManager.SpawnPassiveCoin()
```

---

## Interest

Wave completion calculates:

```text
floor(TotalCoins * InterestRate)
```

Positive results pass through `AddCoins()`.

---

# Cards

## CardDefinition

```text
Assets/Scripts/CardDefinition.cs
```

Defines:

```text
CardDefinition
CardPackType
CardRarity
InRunStackMode
StatType
CardStatModifier
```

### CardPackType

```text
Munitions = 0
Mobility  = 1
Survival  = 2
Gadget    = 3
BaseSet   = 4
```

There is currently no `Tech` enum value.

### CardRarity

```text
Common    = 0
Rare      = 1
Legendary = 2
Corrupted = 3
```

### InRunStackMode

```text
MatchBaseValue  = 0
MatchShopGrowth = 1
Custom          = 2
```

---

## Card Assets

### Current card rework

CardDefinition contains six permanent levels and optional CardAscension metadata on the
same asset/ID. CardManager applies a collection card once per run at its saved level or
ascended form. Basic cards remain repeatable at their original level-1 strength. The
serialized InRunStackMode fields remain for compatibility but no longer drive repeated
collection-card pickups.

New run-offer defaults are Common 60%, Rare 30%, Basic 7%, Legendary 3%. Unavailable
rarities fall back to the eligible pool. When fewer than three collection cards remain,
Basics fill the remaining choices. Lucky Talisman shifts Common probability toward
Rare and Legendary. The serialized gameplay scene requires the configuration step in
[CARD_REWORK_IMPLEMENTATION_GUIDE.md](CARD_REWORK_IMPLEMENTATION_GUIDE.md).

The 25 ascensions are runtime flags in PlayerStats. AscensionEffects owns the player's
beam, wormhole, volcanic eruption, healing areas, orbs and wall spawning. AscensionArea,
HealingOrb and DefenderWall supply reusable effect behavior. Their prefabs are Inspector
references; automatic addition of AscensionEffects does not populate them.

PlayerStats.CalculateDamage combines flat damage with an additive percentage bucket.
Explicit projectile ratios, crits and bounce falloff apply afterward. EnemyBase maintains
an active enemy registry and fractional damage remainder for area effects/poison.
PlayerHealth handles Rebirth once per run. Read-time stat helpers apply its beneficial
scaling to later acquisitions as well. LevelUpUI queues multiple earned level choices.

Electric Feathers uses the existing pool. Successfully emitted normal volleys and minigun
shots each count once for all special-feather intervals; extra feathers do not recurse.
Electric damage snapshots normal attack damage at firing. Chain targets cannot repeat;
unrounded damage halves per jump before integer hit conversion. Supercharged uses a
maximum-health hit through the enemy's normal damage path. Optional chain/bolt visuals
reuse WeaponPlayer-owned LineRenderers.

Assets/Scripts/Editor/CardReworkSetup.cs provides explicit authoring/configuration menu
commands. CardReworkVerification runs isolated checks with a temporary save and transient
objects. CardReworkCombatProbe is guarded by UNITY_EDITOR and excluded from builds.

### Existing assets

Current count:

```text
51
```

Distribution:

```text
Base Set Upgrades:   4
Gadget Upgrades:     12
Mobility Upgrades:   9
Munitions Upgrades:  14
Survival Upgrades:   12
```

Rarity distribution:

```text
Common:     24
Rare:       14
Legendary:  13
Corrupted:  0
```

No duplicate card IDs were found during the verified audit.

Card IDs should be treated as persistent identifiers.

---

## CardManager

```text
Assets/Scripts/CardManager.cs
```

Responsibilities include:

* rarity rolls
* unlocked-card filtering
* run card pool
* pickups
* once-per-run selection and repeatable Basic effects
* application of upgrades

Applies effects into systems including:

```text
PlayerStats
PlayerController
WeaponPlayer
```

---

# Shop

## ShopPackDefinition

Defined in:

```text
Assets/Scripts/ShopData.cs
```

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

`Pack_Tech.asset` retains an old filename while representing `Gadget`.

---

## ShopManager

```text
Assets/Scripts/ShopManager.cs
```

Owns permanent economy and collection/upgrading behavior.

`LoadEconomy()` calls `UnlockBaseSet()`.

### Collection and pack wiring

MainMenu's ShopManager contains 47 collection cards and intentionally omits the four
Basic cards. Its AvailablePacks omits Pack_BasePack. Basics are always eligible through
the gameplay CardManager's 51-card list and are not purchasable or upgradeable.

PlayerData.CreateNew unlocks eight specified collection starters independently of the
shop list. ShopManager owns four essence balances, six-level upgrade prices/copy costs,
1,000-essence ascensions and persistent developer resource flags. At maximum level,
new duplicates and leftover copies convert to essence (Common 1, Rare 2, Legendary 4).

TryBuyPacks accepts one or three packs, grants/saves three or nine cards before the
visual reveal, and publishes OnCollectionChanged for collection/resource UI. Pack odds
are 60% Common, 35% Rare and 5% Legendary. No Corrupted cards are rolled.

MainMenuUI contains the one-/three-pack controls and a three-column reveal grid, while
CardDisplay contains the ascension button/balance bindings. DeveloperConsoleUI accepts
the five four-digit codes. The new scene/prefab UI references still require the editor
setup documented in the implementation guide.

---

# Save Architecture

## SaveSystem

```text
Assets/Scripts/SaveSystem.cs
```

Writes permanent progression to:

```text
Application.persistentDataPath/duck_save.json
```

Saved information includes:

* total coins
* card ID
* permanent level
* duplicate count
* unlock state
* ascension state
* four pack-specific essence balances
* developer resource flags
* progression schema version

Missing/corrupt data falls back to fresh player data.

---

## PlayerData

```text
Assets/Scripts/PlayerData.cs
```

Current new `CardSaveData` defaults include:

```text
Level = 1
DuplicateCount = 0
IsUnlocked = true
```

---

Version 2 is the card-rework schema. Loading an older save intentionally replaces it
with the eight-starter profile once. Current-version saves preserve progression. An
editor-only VerificationSavePath allows the isolated checks to avoid real player data.

## SaveOnQuit

```text
Assets/Scripts/SaveOnQuit.cs
```

Calls:

```text
DontDestroyOnLoad(gameObject)
```

Handles:

```text
OnApplicationQuit()
OnApplicationPause(true)
```

No serialized references to the component were found in inspected:

* scenes
* prefabs
* ScriptableObjects

No runtime creation path was found.

The implementation exists, but its runtime activation is not established.

---

# WebGL Plugins

## Save Bridge

```text
Assets/Plugins/WebGL/SaveSystemBridge.jslib
```

Exports:

```text
SyncFiles
```

and uses:

```text
FS.syncfs(false, callback)
```

`SaveSystem.cs` calls it under:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
```

---

## Fullscreen Bridge

```text
Assets/Plugins/WebGL/Fullscreen.jslib
```

Exports:

```text
JSEnterFullscreen
JSExitFullscreen
JSIsFullscreen
```

Consumed by:

```text
Assets/Scripts/FullscreenToggle.cs
```

under WebGL guards.

---

# Audio Architecture

## AudioManager

```text
Assets/Scripts/AudioManager.cs
```

Persistent through:

```text
DontDestroyOnLoad(gameObject)
```

PlayerPrefs keys include:

```text
DuckDefender_SFXVolume
DuckDefender_MusicVolume
```

### SliceMode

Current values:

```text
None         = 0
EqualSlices  = 1
CustomSlices = 2
```

There is no current `AutoDetect`.

`PlaySlice()` uses:

```text
AudioSource.SetScheduledEndTime(...)
```

Slice duration accounts for playback pitch.

The main-menu SFX library currently contains entries using `CustomSlices`.

No project-authored `AudioManagerEditor.cs` currently exists.

No project-authored custom C# Inspector was found.

---

# UI Architecture

The project primarily uses:

* uGUI
* TextMesh Pro

Gameplay UI scripts include:

```text
GameUI.cs
LevelUpUI.cs
CardDisplay.cs
SpriteHealthBar.cs
SpriteXPBar.cs
EnemyHealthBar.cs
DamagePopup.cs
OverheatPopup.cs
SafeAreaPanel.cs
PixelPerfectCanvasScaler.cs
```

Menu-related scripts include:

```text
MainMenuUI.cs
MenuController.cs
ShopManager.cs
CardIndexUI.cs
SettingsMenuUI.cs
FullscreenToggle.cs
ButtonClickSound.cs
```

`MenuController.cs` contains `MainMenuController`.

---

# Input Architecture

The project has Unity's Input System package installed:

```text
com.unity.inputsystem 1.11.2
```

Project setting:

```text
activeInputHandler = 2
```

Both input backends are therefore enabled.

Gameplay primarily uses legacy `UnityEngine.Input` through project abstractions:

```text
Assets/Scripts/Inputhelper.cs
Assets/Scripts/InputManager.cs
Assets/Scripts/MobileInputController.cs
Assets/Scripts/VirtualJoystick.cs
```

`Inputhelper.cs` contains `InputHelper`.

New gameplay input should generally integrate through the existing abstraction rather than introducing another input path.

---

# Turret Architecture

Base:

```text
Assets/Scripts/TurretBase.cs
```

Implementations:

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

`TurretManager` creates/removes/repositions companions based on runtime state.

---

# Manager Pattern

The project heavily uses singleton-style `Instance` access.

Examples include:

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

Dependencies are commonly obtained through:

* static `Instance`
* Inspector references
* `GetComponent`
* tags

The project does not currently use a dependency-injection framework.

---

# Packages

Current direct non-module package versions:

```text
com.unity.collab-proxy                2.10.2
com.unity.feature.2d                  2.0.1
com.unity.ide.rider                   3.0.31
com.unity.ide.visualstudio            2.0.22
com.unity.inputsystem                 1.11.2
com.unity.multiplayer.center          1.0.0
com.unity.render-pipelines.universal  17.0.3
com.unity.test-framework              1.4.5
com.unity.timeline                    1.8.7
com.unity.ugui                        2.0.0
com.unity.visualscripting             1.9.5
```

---

# Build / Player Settings

Verified values include:

```text
companyName:                         DefaultCompany
productName:                         Duck Defender
bundleVersion:                       1.0
applicationIdentifier.Standalone:   com.DefaultCompany.Duck-Defender
activeInputHandler:                  2
API compatibility:                  .NET Standard 2.1
desktop default resolution:         1920 x 1080
web default resolution:             960 x 600
resizableWindow:                    false
runInBackground:                    false
```

The serialized `scriptingBackend` dictionary is empty.

This does not establish the effective backend of every target.

---

# WebGL Settings

## Global Project Settings

Current global values include:

```text
webGLCompressionFormat:       Disabled
webGLDecompressionFallback:   enabled
webGLTemplate:                APPLICATION:Default
webGLNameFilesAsHashes:       false
webGLDataCaching:             false
webGLThreadsSupport:          false
webGLEnableWebGPU:            false
webGLInitialMemorySize:       32
webGLMaximumMemorySize:       2048
webGLMemoryGrowthMode:        2
```

## Saved Web Build Profile

```text
Assets/Settings/Build Profiles/New Web Profile.asset
```

Target:

```text
WebGL
```

The saved profile currently contains:

```text
webGLCompressionFormat: Gzip
```

This differs from the global setting.

Do not assume which configuration was used for a particular build without verification.

---

# Known Rough Edges

## MechanicsController

```text
Assets/Scripts/MechanicsController.cs
```

currently exists as a zero-byte file.

---

## XP Gem Magnet

XP gems use their own `MagnetRange`.

Coins use:

```text
PlayerStats.MagnetRange
```

This is a current behavior mismatch.

---

## BaseSet Shop Wiring

Basic cards and their pack are intentionally omitted from the MainMenu shop. They remain in the gameplay CardManager list as repeatable fallback offers.

---

## SaveOnQuit

Implementation exists but no active serialized or runtime creation path was found.

---

## WebGL Compression

Global settings say:

```text
Disabled
```

Saved WebGL profile says:

```text
Gzip
```

---

## Audio Historical Documentation

Current modes are only:

```text
None
EqualSlices
CustomSlices
```

There is no `AutoDetect`.

There is no project `AudioManagerEditor.cs`.

---

## Mixed Enemy Generations

Specialized and older/general enemy implementations coexist.

Do not delete either category without checking references.

---

## Mixed Pooling

Player bullets use pooling.

Many other objects still use normal instantiation.

Do not assume every spawned object is pooled.

---

# TO CONFIRM

The following remain unresolved by static repository inspection:

* effective scripting backend per target
* current active/default build target
* which WebGL profile/settings produced the latest build
* effective deployed WebGL compression
* current Windows build architecture
* current Windows build output location
* current Windows executable filename
* Steam release status
* current Steam App ID usage
* Steam depot configuration
* Steam launch executable configuration
* Steam exclusion rules
* actual GitHub Pages deployment configuration
* current deployed Pages URL
* external CI/build workflows
* runtime activation of `SaveOnQuit`
* runtime behavior not explicitly Play Mode tested

Do not convert these into facts without verification.
