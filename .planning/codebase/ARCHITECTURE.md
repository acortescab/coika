---
last_mapped_commit: cc4a6315450b59b114e2bdbb7e25e918e35eed78
last_mapped_at: 2026-10-03
---
<!-- refreshed: 2026-10-03 -->

# Architecture

**Analysis Date:** 2026-10-03

## System Overview

```text
┌─────────────────────────────────────────────────────────────┐
│ Boot scene  `Assets/Scenes/Boot.unity`                       │
│ GameInstaller (composition root) `Core/Boot/GameInstaller.cs`│
└───────────────────────────┬─────────────────────────────────┘
                            ▼ loads Addressable scene
┌─────────────────────────────────────────────────────────────┐
│ GameScene  `Assets/Scenes/GameScene.unity`                   │
│ DropControllerBootstrap (scene wiring, async load)           │
│  → PieceFactory, SpawnQueue, MergeSystem, DropController     │
│  `Assets/Scripts/Gameplay/`                                  │
├──────────────┬──────────────┬───────────────────────────────┤
│ Jar/Danger   │ Drop + Input │ Piece / Merge                 │
│ Jar,JarBuilder│DropController│Piece,PieceFactory,MergeSystem│
└──────┬───────┴──────┬───────┴───────────┬───────────────────┘
       ▼              ▼                   ▼
┌─────────────────────────────────────────────────────────────┐
│ Core services: AssetService, SceneLoaderService, PrefabPool  │
│ `Assets/Scripts/Core/`                                       │
└───────────────────────────┬─────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ Data (ScriptableObjects): GameConfig, ThemeDefinition,       │
│ TierDefinition, SpawnSettings  `Assets/Scripts/Data/`        │
│ Assets in `Assets/Data/`, loaded via Addressables            │
└─────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| GameInstaller | Creates core services, inits Addressables, loads game scene | `Assets/Scripts/Core/Boot/GameInstaller.cs` |
| AssetService / IAssetService | Addressable load/release with handle tracking | `Assets/Scripts/Core/Assets/AssetService.cs` |
| SceneLoaderService / ISceneLoader | Addressable scene load/unload | `Assets/Scripts/Core/Assets/SceneLoaderService.cs` |
| PrefabPool<T> | Generic pooled prefab instances | `Assets/Scripts/Core/Pooling/PrefabPool.cs` |
| GameConfig, ThemeDefinition, TierDefinition, SpawnSettings | Tunable data (ScriptableObjects) | `Assets/Scripts/Data/` |
| DropControllerBootstrap | Loads assets, builds factory/queue, initializes MergeSystem and DropController | `Assets/Scripts/Gameplay/DropControllerBootstrap.cs` |
| DropController / DropFlow / DropState | Held piece, aiming, drop cooldown state machine | `Assets/Scripts/Gameplay/DropController.cs` |
| IDropInput, PointerInputReader, DropInputState | Input abstraction over the Input System | `Assets/Scripts/Gameplay/Input/` |
| SpawnQueue, SpawnSelector, SpawnQueueState | Seeded RNG, weighted tiers, anti-streak | `Assets/Scripts/Gameplay/SpawnQueue.cs` |
| Piece / PieceFactory | Physics body per tier; factory over pool, fires PieceCreated | `Assets/Scripts/Gameplay/Piece.cs`, `PieceFactory.cs` |
| MergeSystem / MergePairQueue / MergePair | Deterministic pair queue processed in FixedUpdate; chains; Supernova | `Assets/Scripts/Gameplay/MergeSystem.cs` |
| Jar, JarBuilder, DangerLine, JarCameraFramer, GameBackground | Playfield geometry, overflow line, camera, backdrop | `Assets/Scripts/Gameplay/` |
| Editor tools | Setup/validate data, prefabs, Addressables build | `Assets/Scripts/Tools/Editor/` |

## Pattern Overview

**Overall:** Component-based Unity layered by assembly definitions, with a composition root and constructor/`Initialize(...)` dependency injection (no service locator, no singletons).

**Key Characteristics:**
- Dependencies flow in through `Initialize(...)` methods or constructors, never by lookup; null args throw `ArgumentNullException`.
- Pure logic split from MonoBehaviours (`DropFlow`, `SpawnSelector`, `MergePairQueue`, `DropInputState`) so it is EditMode testable.
- All content is Addressable and loaded through `IAssetService`.
- Collision callbacks only enqueue; mutation happens once per physics step in `FixedUpdate` (`MergeSystem`).
- Events (`Merged`, `SupernovaTriggered`, `PieceCreated`, `Piece.Collided`) are the extension points; score/FX are to subscribe, not be called.

## Layers

**Core:** `Assets/Scripts/Core/` (`Coika.Core`) - asset/scene services, pooling, boot. Depends on Addressables only. Used by all.
**Data:** `Assets/Scripts/Data/` (`Coika.Data`) - ScriptableObject definitions. Depends on Core.
**Gameplay:** `Assets/Scripts/Gameplay/` (`Coika.Gameplay`) - game rules and MonoBehaviours. Depends on Core, Data, InputSystem, ugui, URP 2D.
**UI:** `Assets/Scripts/UI/` (`Coika.UI`) - empty, references Core.
**Tools:** `Assets/Scripts/Tools/Editor/` (`Coika.Tools.Editor`) - editor only.
Empty placeholders: `Assets/Scripts/Audio`, `Fx`, `Input`.

## Data Flow

### Boot and run start

1. `GameInstaller.Start` creates services, `Boot()` calls `Addressables.InitializeAsync` then `Scenes.LoadScene(_gameScene)` (`GameInstaller.cs`).
2. `DropControllerBootstrap` loads `GameConfig`, `ThemeDefinition`, tiers via `IAssetService` (`DropControllerBootstrap.cs`).
3. Builds `PieceFactory` (pre-warmed per tier), `SpawnQueue(config, seed)`, then `MergeSystem.Initialize` and `DropController.Initialize`.

### Drop and merge

1. Input -> `IDropInput` -> `DropController` positions held piece; release asks `SpawnQueue` for tiers and `PieceFactory.Create`.
2. Physics contact -> `Piece.Collided` -> `MergeSystem.OnCollided` -> `MergePairQueue.TryEnqueue`.
3. `MergeSystem.FixedUpdate` -> `ProcessQueue`: sort, `CanStillMerge`, `Resolve` creates next tier at midpoint with average velocity, releases pair, raises `Merged`. Two top-tier pieces raise `SupernovaTriggered`.

**State Management:** per-object fields; `DropState` enum with `DropFlow`; `SpawnQueueState` for queue snapshot; no global state.

## Key Abstractions

**IAssetService / ISceneLoader:** interfaces enabling fakes (`Assets/Tests/EditMode/Fakes/`).
**IDropInput:** input seam; `PointerInputReader` implements it.
**TierDefinition:** index-ordered tier data; next tier = `Index + 1`.

## Entry Points

**Boot scene:** `Assets/Scenes/Boot.unity` (only scene in build) with `GameInstaller`.
**GameScene:** `Assets/Scenes/GameScene.unity`, Addressable group `Scenes`, runs `DropControllerBootstrap`.

## Architectural Constraints

- **Threading:** Unity main thread; async/await via `Task` on Addressables; physics logic in `FixedUpdate`.
- **Global state:** none beyond `DontDestroyOnLoad(GameInstaller)`.
- **Circular imports:** none; assembly graph is acyclic (Gameplay -> Data -> Core).
- **Allocation:** hot paths avoid allocations (tests in `Assets/Tests/EditMode/Fakes/AllocationMeter.cs`); reuse delegates like `_onCollided`.
- **Assembly:** `Coika.Data` has `autoReferenced: false`; add explicit references.

## Anti-Patterns

### Mutating inside collision callbacks

**What happens:** creating or releasing pieces in `OnCollisionEnter2D`.
**Why it's wrong:** corrupts physics iteration and order is nondeterministic.
**Do this instead:** enqueue and resolve in `MergeSystem.ProcessQueue`.

### Service lookup

**What happens:** `FindObjectOfType` or static singletons for services.
**Do this instead:** pass via `Initialize(...)` like `MergeSystem.Initialize`.

## Error Handling

**Strategy:** Log and degrade; never crash on boot. `GameInstaller.Boot` catches and logs (retry UI is a TODO). `MergeSystem` logs creation errors once per run and leaves pieces untouched. `AssetLoadException` wraps load failures. Arguments validated with `ArgumentNullException`.

## Cross-Cutting Concerns

**Logging:** `Debug.LogError` with context.
**Validation:** Editor `ThemeValidator`; runtime null guards.
**Authentication:** Not applicable.

---

*Architecture analysis: 2026-10-03*
