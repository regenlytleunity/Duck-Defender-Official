# Duck Defender — Agent Instructions

## Purpose

This repository contains **Duck Defender**, an existing Unity 2D wave-survival/action platformer.

This file contains the permanent rules coding agents must follow.

The repository is always the source of truth. If these instructions or any documentation disagree with the current files, inspect the repository and follow the current implementation.

Do not invent:

* file paths
* class names
* Unity settings
* package versions
* scene names
* build scripts
* tests
* deployment configuration
* serialized references

If something cannot be verified, say so.

---

# Project References

Detailed project information has intentionally been moved out of this file to keep permanent agent context small.

Read these documents when relevant:

```text
Docs/ARCHITECTURE.md
Docs/WORKFLOWS.md
```

## `Docs/ARCHITECTURE.md`

Read this before work involving:

* existing gameplay systems
* player code
* enemies
* projectiles
* cards
* economy
* saves
* audio
* UI
* input
* turrets
* scenes
* build settings
* WebGL
* packages
* known project rough edges

## `Docs/WORKFLOWS.md`

Read this before:

* adding cards
* adding enemies
* adding stats
* adding audio
* creating new scripts
* changing serialized fields
* verifying gameplay
* running Unity from the command line
* performing multi-file gameplay work

Do not reread large reference documents unnecessarily for tiny isolated tasks.

---

# Core Development Principle

Prefer:

```text
extend the existing Duck Defender system
```

over:

```text
create a cleaner parallel system
```

unless the task explicitly calls for redesign or the current architecture cannot reasonably support the requested feature.

Preserve working behavior first.

Improve architecture deliberately, not incidentally.

---

# Inspect Before Editing

For every non-trivial task:

1. Read this `AGENTS.md`.
2. Check `git status`.
3. Read the relevant reference documentation.
4. Inspect the actual implementation.
5. Search references to affected:

   * classes
   * methods
   * serialized fields
   * card IDs
   * prefabs
   * ScriptableObjects
   * scene objects
6. Identify which existing system owns the behavior.
7. Choose the smallest reasonable implementation.
8. Preserve unrelated behavior.

Repository evidence overrides documentation.

---

# Scope Discipline

Do not:

* perform unrelated refactors
* clean up unrelated naming
* reformat unrelated code
* introduce new frameworks for stylistic reasons
* create duplicate managers
* create duplicate stat systems
* create duplicate input systems
* create parallel save systems
* migrate architecture unless specifically requested

A feature request is not permission to modernize the entire codebase.

---

# User / Agent Division of Work

The user normally handles:

* sprite creation
* pixel art
* animation artwork
* visual design
* prefab appearance
* Unity Inspector setup
* scene layout
* visual tuning
* Play Mode evaluation

Agents should primarily handle:

* C# implementation
* code investigation
* debugging
* system integration
* safe refactoring when requested
* explaining Unity setup
* identifying Inspector requirements
* explaining how to test changes

Do not redesign or generate replacement visual assets unless explicitly requested.

When code requires Unity-side setup, provide exact instructions instead of pretending that setup is already complete.

---

# Unity `.meta` Safety

Never hand-author or fabricate Unity `.meta` files.

Never casually:

* edit
* regenerate
* delete

an existing `.meta` file.

Unity GUIDs stored in `.meta` files are used by:

* scenes
* prefabs
* ScriptableObjects
* materials
* animations
* other serialized assets

When adding a new Unity asset or C# script:

1. create the source asset/file
2. allow Unity to import it
3. allow Unity to generate the `.meta`
4. verify the generated `.meta` exists before committing

When moving or renaming an existing asset, preserve its `.meta` and GUID.

---

# Serialized Unity Asset Safety

Do not manually rewrite raw YAML in:

```text
*.unity
*.prefab
*.asset
```

unless explicitly requested.

These files contain GUIDs, fileIDs, serialized component state, and Inspector references.

Prefer:

* C# changes
* Unity Editor operations
* purpose-built Editor tooling when appropriate

over raw serialized edits.

---

# Serialized Fields

Do not casually rename serialized fields.

Existing scenes and prefabs may rely on their serialized names.

If a serialized field must be renamed, consider:

```csharp
[FormerlySerializedAs("OldFieldName")]
```

and inspect the serialization impact.

Report serialization-impacting changes explicitly.

---

# Project Settings

Do not alter important `ProjectSettings` values unless specifically requested.

This includes:

* Product Name
* Company Name
* application identifiers
* scripting backend
* API compatibility
* graphics APIs
* active input backend
* WebGL compression
* build target
* build profiles
* package versions

Build configuration may depend on external deployment systems that are not represented in this repository.

---

# Generated / Local Folders

Never treat the following as project source:

```text
Library/
Temp/
obj/
Logs/
Build/
Builds/
UserSettings/
```

Do not modify or commit them.

---

# Scene and Inspector Wiring

Code inspection alone cannot prove that Unity Inspector wiring is correct.

When a feature requires any of the following:

* attaching a component
* assigning a prefab
* assigning a sprite
* assigning an AudioClip
* assigning a ScriptableObject
* configuring a LayerMask
* setting a tag
* wiring UI fields
* adding an object to a scene
* assigning animation references
* changing Inspector values

state the required Unity Editor steps explicitly.

Never say scene or Inspector setup is complete unless it was actually performed and verified.

---

# Editor-Only Code

Project-authored Editor scripts do not currently form part of the normal runtime architecture.

If new editor-only code is added, it must either:

* live under an `Editor/` directory

or be guarded appropriately with:

```csharp
#if UNITY_EDITOR
#endif
```

Do not introduce `UnityEditor` dependencies into runtime code.

---

# Platform-Specific Code

Guard platform-specific code appropriately.

Current WebGL interop follows patterns such as:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
#endif
```

Use appropriate guards such as:

```csharp
#if UNITY_WEBGL
#endif
```

or:

```csharp
#if UNITY_STANDALONE
#endif
```

when required.

Do not execute browser/native interop on unsupported targets.

---

# Packages and Unity Version

Do not casually:

* upgrade Unity
* upgrade packages
* add packages
* remove packages
* add assembly definitions

unless the requested task requires it.

Project-wide package or assembly changes require explicit justification.

---

# Git Safety

Before substantial work, inspect:

```text
git status
```

Preserve unrelated user changes.

Do not automatically:

* commit
* push
* force-push
* rewrite history
* discard unrelated modifications

Do not use destructive commands such as:

```text
git reset --hard
git clean -fd
git push --force
```

unless explicitly requested and the consequences are understood.

---

# Coding Style

Match the surrounding file.

Current project style generally includes:

* global namespace classes
* one primary class per file
* PascalCase public members
* underscore-prefixed private runtime fields
* `[Header]`
* `[Tooltip]`
* occasional `[Range]`
* coroutines for timing/gameplay effects
* singleton-style `Instance` access
* Inspector references
* tags and layer masks
* ScriptableObjects for card configuration

Do not reformat unrelated sections while implementing a feature.

Do not perform opportunistic filename or terminology cleanup.

Historical naming may be referenced by serialization or compatibility logic.

---

# Logging

There is no single mandatory project-wide log prefix.

Follow the style of the surrounding class.

Do not introduce a new logging framework unless explicitly requested.

---

# Performance

Be especially cautious inside:

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

Avoid unnecessary allocations and repeated scene-wide searches in hot paths.

Use existing cached/singleton references where appropriate.

Reuse existing pooling when appropriate, but do not rewrite unrelated spawning systems merely for consistency.

---

# Verification

Never claim something was tested when it was not.

For code changes, distinguish clearly between:

* static code review
* successful Unity compilation
* Play Mode testing
* Inspector verification
* scene verification
* build verification

If Unity-side work remains, state it.

Detailed verification procedures are documented in:

```text
Docs/WORKFLOWS.md
```

---

# Required Completion Report

After a non-trivial implementation, report:

1. what changed
2. every file changed
3. why each file changed
4. what was actually verified
5. what was not verified
6. required Unity Inspector setup
7. required scene/prefab setup
8. a Play Mode test checklist

Do not hide uncertainty.

---

# Project Documentation Maintenance

`Docs/ARCHITECTURE.md` documents current architecture, not future plans.

When a task intentionally changes an important architectural fact documented there, update the documentation if appropriate.

Do not add:

* roadmap items
* wishlist mechanics
* speculative systems
* unimplemented features

to architecture documentation.

Current-state documentation should describe only what actually exists.
