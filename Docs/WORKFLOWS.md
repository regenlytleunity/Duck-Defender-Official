# Duck Defender — Development Workflows

## Purpose

This document describes how agents should perform common development tasks in Duck Defender.

Read only the relevant sections for the current task.

The repository and `AGENTS.md` remain authoritative.

---

# General Feature Workflow

For a non-trivial feature:

1. Check:

   ```text
   git status
   ```

2. Read `AGENTS.md`.

3. Read the relevant section of:

   ```text
   Docs/ARCHITECTURE.md
   ```

4. Inspect the actual current implementation.

5. Search references to all affected:

   * classes
   * fields
   * IDs
   * prefabs
   * ScriptableObjects

6. Determine which existing system owns the behavior.

7. Before editing, summarize:

   * files likely involved
   * implementation approach
   * Unity setup likely required

8. Implement the smallest reasonable change.

9. Do not edit unrelated files.

10. Allow Unity to compile.

11. Check for compiler errors.

12. Give the user exact Unity setup instructions.

13. Give a Play Mode test checklist.

14. Report every changed file.

---

# Creating a New C# Script

When a task requires a new Unity C# script:

1. Verify that an existing script cannot reasonably own the behavior.

2. Choose a name matching existing conventions.

3. Create the `.cs` source file.

4. Do **not** fabricate its `.meta`.

5. Allow Unity to import the file.

6. Verify Unity generated:

   ```text
   ScriptName.cs.meta
   ```

7. Do not commit the new script without its Unity-generated `.meta`.

If the script requires a component to be attached to a GameObject, tell the user exactly where and how to attach it.

---

# Adding a New Card

## Inspect First

Read:

```text
Assets/Scripts/CardDefinition.cs
Assets/Scripts/CardManager.cs
Assets/Scripts/PlayerStats.cs
```

Then inspect whichever runtime system will consume the effect, such as:

```text
Assets/Scripts/PlayerController.cs
Assets/Scripts/WeaponPlayer.cs
Assets/Scripts/Projectile.cs
Assets/Scripts/PlayerHealth.cs
Assets/Scripts/TurretManager.cs
```

## Implementation Order

1. Determine whether the effect already exists.

2. Determine whether an existing `StatType` represents the effect.

3. Reuse existing mechanics whenever possible.

4. If a new runtime stat is required:

   * add it to the correct existing owner
   * usually `PlayerStats` for card-derived runtime state

5. Extend `CardManager` only where needed.

6. Avoid giant unrelated changes to the `ApplyStat` logic.

7. Do not invent a second upgrade framework.

## Unity Setup

Create the `CardDefinition` asset through Unity under the appropriate folder:

```text
Assets/Cards/Upgrades/Base Set Upgrades/
Assets/Cards/Upgrades/Gadget Upgrades/
Assets/Cards/Upgrades/Mobility Upgrades/
Assets/Cards/Upgrades/Munitions Upgrades/
Assets/Cards/Upgrades/Survival Upgrades/
```

Configure:

* stable unique card ID
* pack
* rarity
* name
* description
* artwork
* stat modifiers
* shop/run values

Do not hand-write the `.asset` YAML.

## Verification

Test:

* card appears in intended pool
* rarity behavior
* effect applies once
* effect stacks correctly
* level-up behavior
* run reset behavior
* permanent progression if applicable
* save/load if applicable

Never rename an existing stable card ID casually.

---

# Adding a New Enemy

## Inspect First

Read:

```text
Assets/Scripts/EnemyBase.cs
```

Then inspect the closest existing implementation:

```text
Assets/Scripts/SwarmerEnemy.cs
Assets/Scripts/TankEnemy.cs
Assets/Scripts/LobberEnemy.cs
Assets/Scripts/BuzzerEnemy.cs
Assets/Scripts/GroundEnemy.cs
Assets/Scripts/FlyingEnemy.cs
```

Also inspect:

```text
Assets/Scripts/WaveManager.cs
```

when spawn integration is needed.

## Implementation Order

1. Identify behavior already supplied by `EnemyBase`.

2. Do not duplicate:

   * health
   * status effects
   * knockback
   * death
   * rewards
   * coin drops

3. Implement only the enemy's unique movement/attack behavior.

4. Reuse existing enemy projectile types where appropriate.

5. Create new projectile logic only if the existing projectile classes cannot represent the mechanic.

## User Visual Work

The user normally creates:

* enemy sprite
* animations
* animation frames
* visual effects

The agent should explain how those assets connect to the code.

## Unity Prefab Setup

Tell the user explicitly:

* GameObject structure
* required components
* Rigidbody2D configuration
* Collider2D configuration
* tags
* layers
* serialized fields
* animation references
* prefab location
* WaveManager/spawn integration

Do not edit prefab YAML directly unless explicitly requested.

## Verification

Test:

* spawning
* scaling
* movement
* attack timing
* collision
* player damage
* knockback
* poison/slow/freeze as relevant
* death
* coin rewards
* wave completion

---

# Adding a Player Stat

Read:

```text
Assets/Scripts/PlayerStats.cs
Assets/Scripts/CardDefinition.cs
Assets/Scripts/CardManager.cs
```

Then inspect the consumer.

## Determine Ownership

Classify the stat:

```text
run-only
card-derived
calculated
persistent
```

Prefer adding card-derived runtime state to existing `PlayerStats` when that matches current architecture.

Do not create a second generic stat container.

## Implementation

1. Search for similar existing values.
2. Add only necessary state.
3. Extend `StatType` only if required.
4. Add CardManager application logic only if required.
5. Update the runtime consumer.
6. Preserve reset behavior.
7. Consider compatibility aliases when renaming/replacing anything.

## Persistent Stats

Before persistence changes, inspect:

```text
Assets/Scripts/PlayerData.cs
Assets/Scripts/SaveSystem.cs
Assets/Scripts/ShopManager.cs
```

Treat save-schema changes as compatibility-sensitive.

## Verification

Test:

* default value
* card application
* repeated stacking
* run restart/reset
* scene reload
* save/load if persistent
* interactions with existing mechanics

---

# Adding or Changing Projectiles

Inspect:

```text
Assets/Scripts/Projectile.cs
Assets/Scripts/WeaponPlayer.cs
Assets/Scripts/PlayerStats.cs
Assets/Scripts/ObjectPooler.cs
```

Consider whether the mechanic belongs in:

* `BallisticData`
* projectile initialization
* projectile collision
* WeaponPlayer firing pattern
* PlayerStats runtime state

Prefer extending the existing ballistic/projectile system over introducing a parallel projectile architecture.

Be especially cautious about:

* per-frame target searches
* repeated physics queries
* allocations
* pooled-object state not being reset

For pooled projectiles, verify all transient state is reset between uses.

---

# Adding Audio

Inspect:

```text
Assets/Scripts/AudioManager.cs
```

and the closest existing audio use.

## Current Slice Modes

Only use current supported modes:

```text
None
EqualSlices
CustomSlices
```

Do not rely on `AutoDetect`.

## Asset Setup

The user should import the audio under the project's existing audio structure, including:

```text
Assets/Sound Effects/
```

Unity should generate the `.meta`.

## Inspector Setup

Tell the user:

* which AudioManager list/reference to modify
* clip assignment
* volume
* pitch
* slicing mode
* custom slice values if needed

## Verification

Test:

* playback
* volume settings
* pitch
* slicing
* repeated triggering
* scene transitions where relevant

---

# Adding UI

Inspect the closest existing UI component first.

Gameplay UI includes:

```text
GameUI
LevelUpUI
CardDisplay
DamagePopup
OverheatPopup
SpriteHealthBar
SpriteXPBar
EnemyHealthBar
```

Menu UI includes:

```text
MainMenuUI
MainMenuController
ShopManager
CardIndexUI
SettingsMenuUI
FullscreenToggle
```

Prefer extending existing shared UI components.

The user should normally handle visual layout and artwork.

The agent should handle:

* C# state
* data flow
* button callbacks
* UI behavior
* explaining Inspector wiring

If fields need assignment, list every field explicitly.

---

# Input Changes

Inspect:

```text
Assets/Scripts/Inputhelper.cs
Assets/Scripts/InputManager.cs
Assets/Scripts/MobileInputController.cs
Assets/Scripts/VirtualJoystick.cs
```

Gameplay currently uses the project's input abstraction.

Do not casually migrate gameplay to Unity's new Input System.

When adding gameplay input:

1. support desktop behavior
2. consider mobile behavior
3. route through existing abstractions where possible

Avoid adding a third independent input path.

---

# Save-System Changes

Inspect:

```text
Assets/Scripts/SaveSystem.cs
Assets/Scripts/PlayerData.cs
Assets/Scripts/ShopManager.cs
```

If WebGL behavior matters, also inspect:

```text
Assets/Plugins/WebGL/SaveSystemBridge.jslib
```

Before modifying persistent data:

1. identify existing schema
2. determine default values
3. determine behavior for old save files
4. avoid renaming stable identifiers
5. consider migration/fallback behavior

Never assume `SaveOnQuit` is active simply because the script exists.

---

# Adding a Turret / Companion

Inspect:

```text
Assets/Scripts/TurretBase.cs
Assets/Scripts/TurretManager.cs
```

Then inspect the closest implementation:

```text
Assets/Scripts/MarksmanTurret.cs
Assets/Scripts/MedicTurret.cs
Assets/Scripts/ProtectorTurret.cs
Assets/Scripts/ElementalTurret.cs
Assets/Scripts/SecondWindAngel.cs
```

Extend the existing turret framework.

Do not create another companion manager.

The user should create the visual prefab/sprite unless explicitly requested otherwise.

Tell the user exactly:

* components required
* Inspector references
* sprite/prefab placement
* TurretManager integration
* card/stat integration
* test procedure

---

# Serialized Field Rename Workflow

If a serialized field needs renaming:

1. search all references
2. determine whether scenes/prefabs serialize it
3. preserve compatibility using:

```csharp
[FormerlySerializedAs("OldFieldName")]
```

when appropriate

4. compile in Unity
5. inspect affected prefab/scene
6. verify Inspector values remain present

Do not rename serialized fields merely to improve style.

---

# Asset Rename Workflow

Renaming Unity assets is higher risk than normal file renaming.

Before renaming:

1. search references
2. identify serialization usage
3. preserve the existing `.meta`
4. perform the rename through Unity when practical
5. verify references afterward

Do not regenerate the asset GUID.

---

# Normal Unity Verification

For gameplay code:

1. save code changes

2. return to Unity

3. wait for compilation

4. check Console for errors

5. open:

   ```text
   Assets/Scenes/SampleScene.unity
   ```

6. enter Play Mode

7. exercise the changed mechanic

8. check Console for runtime errors

9. test relevant interactions

10. exit Play Mode

11. review Git diff

For menu/shop/meta-progression changes:

1. open:

   ```text
   Assets/Scenes/MainMenu.unity
   ```

2. enter Play Mode

3. exercise the affected UI/shop/save flow

4. inspect Console

5. verify persistence where applicable

---

# Command-Line Unity Smoke Check

Current verified Unity executable:

```text
C:\Program Files\Unity\Hub\Editor\6000.0.35f1\Editor\Unity.exe
```

Current project path:

```text
C:\Users\regen\Duck Defender 2025-11-19_13-03-32\Duck Defender
```

A basic command-line import/compile smoke check is:

```cmd
"C:\Program Files\Unity\Hub\Editor\6000.0.35f1\Editor\Unity.exe" ^
-batchmode ^
-quit ^
-projectPath "C:\Users\regen\Duck Defender 2025-11-19_13-03-32\Duck Defender" ^
-logFile -
```

This can catch import or compilation failures.

It does **not** verify:

* gameplay
* scene wiring
* prefab configuration
* Inspector references
* physics behavior
* animation behavior
* visual correctness

Do not present this as equivalent to Play Mode testing.

---

# Code Review Pass

For substantial changes, perform a separate review after implementation.

Review for:

* incorrect assumptions
* null-reference risks
* Unity lifecycle mistakes
* serialized-field breakage
* duplicated existing behavior
* unnecessary allocations
* expensive `Update()` logic
* pooled state not being reset
* mobile/desktop input mismatch
* save compatibility
* card ID compatibility
* platform-specific compilation problems

Prefer reporting findings before making a second wave of unrelated changes.

---

# Recommended Task Prompt Structure

For user requests, interpret or encourage tasks in this structure:

```text
Goal:
What behavior should exist?

Context:
Which existing system appears relevant?

Constraints:
What should not be changed?

Visual work:
What will the user create/setup in Unity?

Done when:
What should compile or behave correctly?
What setup instructions must be provided?
What testing should be performed?
```

Example:

```text
Goal:
Add a frost turret that periodically freezes nearby enemies.

Context:
Use the existing turret and enemy status systems.

Constraints:
Do not create another turret manager.
Do not edit prefab or scene YAML.
Reuse existing freeze behavior.

Visual work:
The user will create the turret sprite and prefab appearance.

Done when:
The scripting compiles.
Required Inspector fields are documented.
Unity prefab setup instructions are provided.
A Play Mode test checklist is provided.
```

---

# Final Response After Implementation

Every substantial task should end with:

## Changed Files

Exact files changed.

## What Changed

Short description of each change.

## Unity Setup Required

Exact Editor/Inspector/prefab/scene steps for the user.

If none:

```text
No additional Unity Editor setup required.
```

## Verification Performed

State only checks actually completed.

Examples:

```text
Static review completed.
Unity compilation completed.
```

Do not claim Play Mode testing unless actually performed.

## Play Mode Test Checklist

Provide short concrete steps.

## Remaining Risks / Unverified Items

Call out anything the agent could not confirm.
