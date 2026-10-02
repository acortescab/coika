# Coding Conventions

**Analysis Date:** 2026-10-03

Authoritative rules live in `.planning/code-standards.md` (IDs `S-NN`; MUST/SHOULD) and `.planning/constraints.md` (wins on conflict). This file records how the code actually applies them.

## Naming Patterns

**Files:**
- One top-level type per file, file name = type name (S-04): `Assets/Scripts/Gameplay/MergeSystem.cs`, `Assets/Scripts/Core/Assets/IAssetService.cs`.
- Interfaces `IPascalCase`; test classes `<Type>Tests.cs` (EditMode) or `<Type>PlayModeTests.cs` / `<Type>IntegrationTests.cs` (PlayMode).

**Functions/Types/Properties/Events:** `PascalCase` (`ProcessQueue`, `SupernovaTriggered`). Events are past-tense or state names (`Merged`, `Collided`, `PieceCreated`).

**Variables:**
- Private fields `_camelCase` (`_queue`, `_creationErrorLogged`); serialized: `[SerializeField] private` (e.g. `_jarSize`, `_wallMaterial` in `Assets/Scripts/Data/GameConfig.cs`).
- Parameters/locals `camelCase`.
- `const` fields `UPPER_CASE` (`CONTACT_TOLERANCE`, `RETRY_COUNT`, `SEED`); `static readonly` stays `PascalCase` (S-12).

**Namespaces:** asmdef name plus folder: `Coika.Core`, `Coika.Data`, `Coika.Gameplay`, tests `Coika.Tests.EditMode` / `Coika.Tests.PlayMode`.

## Code Style

**Formatting:**
- No `.editorconfig` or formatter config; follow existing files: 4-space indent, Allman braces (new line), file-scoped content inside `namespace X { ... }` block (not file-scoped namespace), target-typed `new()` for field initializers (`private readonly MergePairQueue _queue = new();`).
- Braces on every `if/for/while` in gameplay code (S-14). Older Core files (`Assets/Scripts/Core/Assets/AssetService.cs`) still use brace-less single-statement `if`; new code should use braces.
- Use `var` when the type is obvious. Loops use `for (var i = ...)` in gameplay code to avoid enumerator allocation (S-50).
- Member order (S-16): constants, readonly/private fields, events, Unity messages (`OnEnable`, `OnDisable`, `FixedUpdate`), public methods, private methods (see `MergeSystem.cs`).

**Linting:** None configured; the bar is "no compiler warnings" (S-133). Asmdefs set `autoReferenced: false` for tests.

## Import Organization

**Order:** `System.*`, then `Coika.*` project namespaces, then `UnityEngine.*` (see `MergeSystem.cs`); tests add `NUnit.Framework` between `Coika.*` and `UnityEngine.*`. Not strictly enforced (some files put `UnityEditor`/`UnityEngine` first, e.g. `Assets/Tests/EditMode/Fakes/TestGameConfig.cs`).

**Path Aliases:** None. Cross-module access is by asmdef reference only; dependencies flow `UI -> Gameplay -> Core`, `Data` is shared (S-03). Assemblies: `Coika.Core`, `Coika.Data`, `Coika.Gameplay`, `Coika.UI`, `Coika.Tools.Editor`, `Coika.Tests.EditMode`, `Coika.Tests.PlayMode`.

## Error Handling

**Patterns:**
- Programmer errors in public APIs throw: `ArgumentNullException` via `factory ?? throw new ArgumentNullException(nameof(factory))` (`MergeSystem.Initialize`); `ArgumentException` for bad keys (`AssetService.LoadAsset`). Unity objects are null-checked with `!= null`, never `??` (S-19).
- Recoverable failures (asset/scene loads) retry then throw `AssetLoadException` (`Assets/Scripts/Core/Assets/AssetLoadException.cs`); no infinite waits (S-82).
- Setup faults inside hot paths are caught narrowly (`InvalidOperationException`) and logged once per run with a `bool _creationErrorLogged` flag, leaving state untouched (`MergeSystem.TryCreate`).
- No empty `catch` blocks (S-101). Use `TryX`/`out` patterns for expected failure.
- Collision callbacks only enqueue work; creation/release happens in `FixedUpdate` (S-62).

## Logging

**Framework:** `UnityEngine.Debug`. Use `Debug.LogError(message, context)` with a context object for faults, `LogWarning` for retries (`AssetService.cs`, `SceneLoaderService.cs`). Never log in `Update` paths (S-102). Interpolated strings only on error/retry paths.

## Comments

- Every type and every member (including private) carries an XML doc comment (`/// <summary>`), with `<param>`, `<returns>`, `<exception>`, `<see cref=.../>` where relevant. Comments explain why and cite GDD sections / issue / constraint IDs (`GDD §3.4`, `S-62`, `C-01`).
- Short trailing `//` comments on constants (`private const int RETRY_COUNT = 3; // Number of attempts ...`).
- No commented-out code; `TODO` should be `// TODO(#12): ...` (existing: `Assets/Scripts/Core/Boot/GameInstaller.cs:51` uses a bare `TODO` referencing C-01).

## Function Design

**Size:** ~30 lines or fewer, early returns (`ProcessQueue`, `CanStillMerge`).
**Parameters:** Dependencies arrive via `Initialize(...)` or constructors from the composition root `Assets/Scripts/Core/Boot/GameInstaller.cs` (S-22, S-26); no singletons or static mutable state.
**Return Values:** No silent null from public APIs; async work returns `Task<T>`.

## MonoBehaviour / Data Design

- MonoBehaviours are thin adapters; pure logic in plain classes (`SpawnQueue`, `MergePairQueue`, `DropFlow`, `DropInputState`, `SpawnSelector`) (S-20).
- Decorate with `[AddComponentMenu("Coika/...")]`, `[DisallowMultipleComponent]`, `[RequireComponent]`.
- Cache delegates once (`_onCollided ??= OnCollided`) to avoid per-call lambda allocations; subscribe in `OnEnable`, unsubscribe in `OnDisable` with idempotent `Subscribe/Unsubscribe` guarded by `_subscribed` (S-23).
- Events: `public event Action<...> Name;` raised with `Name?.Invoke(...)` by the owner only (S-40/41).
- ScriptableObjects (`GameConfig`, `TierDefinition`, `ThemeDefinition`, `SpawnSettings` in `Assets/Scripts/Data/`) are immutable at runtime; tunables never hard-coded (S-30/31).
- Determinism: seeded `System.Random`, stable ordering by lowest instance id (`MergePairQueue`) (S-63).
- Zero per-frame allocation; reuse buffers/collections (S-50, S-52); pooling via `Assets/Scripts/Core/Pooling/PrefabPool.cs` and `Assets/Scripts/Gameplay/PieceFactory.cs`.
- Assets/scenes load only through `IAssetService` / `ISceneLoader` (`Assets/Scripts/Core/Assets/`) (S-80).

## Module Design

**Exports:** Public types per module, internals kept private. No barrel files.
**Editor tooling:** Under `Assets/Scripts/Tools/Editor/` (`Coika.Tools.Editor` asmdef, Editor-only).

## Git

Branch `feature/<issue>-<short-name>`; commits in English describing why, e.g. "Add the MergeSystem ... (issue #7)"; do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`; commit `.meta` files (S-130..S-132).

---

*Convention analysis: 2026-10-03*
