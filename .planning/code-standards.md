# Code Standards

Rules every C# script in Coika must follow. They complement [gdd.md](gdd.md) (§14 architecture) and [constraints.md](constraints.md). If this file conflicts with `constraints.md`, the constraints file wins. Reviewers reject PRs that break a **MUST**; a **SHOULD** needs a justification in the PR.

Each rule has an ID (`S-NN`) so PRs can reference it. Never reuse or renumber an ID.

---

## 1. Project structure

- **S-01 (MUST)** Follow the folder layout in GDD §14.1 (adjusted by C-01: no `Resources/`). Scripts live under `Assets/Scripts/<Module>/`.
- **S-02 (MUST)** One assembly definition per module: `Coika.Core`, `Coika.Gameplay`, `Coika.UI`, `Coika.Tests`. `Coika.Data` and `Coika.Fx` exist already. Add `Coika.Audio`, `Coika.Input` only if compile times or dependencies justify it.
- **S-03 (MUST)** Dependencies flow one way: `UI → Fx → Gameplay → Core`. `Core` never references `Gameplay` or `UI`. No circular references; no `Auto Referenced` on asmdefs. Test asmdefs reference the module under test and are Editor-only (EditMode) or Editor+Player (PlayMode).
- **S-04 (MUST)** One top-level type per file; the file name equals the type name. Namespace equals the asmdef name plus the folder (`Coika.Gameplay`, `Coika.UI`, ...).
- **S-05 (MUST)** Never edit `Packages/manifest.json` by hand; use the Package Manager client API (see C-01).
- **S-06 (MUST)** Never rename or move assets or scripts outside the Unity Editor (or without their `.meta` file), or GUID references break. Commit every `.meta` file.

## 2. Naming and style

- **S-10 (MUST)** Types, methods, properties, events, enums: `PascalCase`. Constants: `UPPER_CASE` (S-12). Parameters and locals: `camelCase`. Interfaces: `IPascalCase`.
- **S-11 (MUST)** Private fields: `_camelCase`. Serialized private fields use `[SerializeField] private`, never `public` fields just for the Inspector.
- **S-12 (MUST)** `const` fields use `UPPER_CASE` with underscores (`const int TIER_COUNT = 11;`), as the whole codebase already does. `static readonly` fields keep `PascalCase`.
- **S-13 (SHOULD)** Booleans read as a question: `IsMerged`, `HasSettled`, `CanDrop`.
- **S-14 (MUST)** Always declare accessibility explicitly. Use braces for every `if/for/while`. Use `var` only when the type is obvious from the right side.
- **S-15 (SHOULD)** Methods stay short (about 30 lines or fewer) and do one thing. Prefer early returns over deep nesting.
- **S-16 (MUST)** Member order: constants, static, serialized fields, private fields, events, properties, Unity messages (`Awake`, `OnEnable`, `Start`, `Update`, ...), public methods, private methods.
- **S-17 (MUST)** Code, identifiers, comments and commit messages are in **English**. User-visible text goes through localization keys (GDD §15), never hard-coded strings.
- **S-18 (SHOULD)** Comments explain **why**, not what. No commented-out code, no stale `TODO` without an issue number (`// TODO(#12): ...`).
- **S-19 (MUST)** Enable nullable-safe habits: no silent `null` returns from public APIs; use `TryGet...` patterns or throw for programmer errors. Unity object null checks use `==`/`!=` or `if (obj)`, never `?.` / `??` / `is null` on `UnityEngine.Object`.

## 3. MonoBehaviour design

- **S-20 (MUST)** Small, single-purpose components. Logic that does not need the Unity API lives in plain C# classes (testable in EditMode); MonoBehaviours are thin adapters.
- **S-21 (MUST)** Cache component references in `Awake` (or via `[SerializeField]`). Never call `GetComponent`, `Find*`, `Camera.main` or `FindObjectOfType` in `Update`/`FixedUpdate`, nor anywhere at runtime in gameplay (GDD §14.4).
- **S-22 (MUST)** Initialize your own state in `Awake`; touch other objects only from `Start` or later, or via explicit `Initialize(...)` calls from the composition root.
- **S-23 (MUST)** Subscribe to events in `OnEnable`, unsubscribe in `OnDisable`. Every `+=` has a matching `-=`.
- **S-24 (SHOULD)** Use `[RequireComponent]`, `[DisallowMultipleComponent]`, `[DefaultExecutionOrder]` and `[Min]`/`[Range]`/`[Tooltip]` to make intent and valid values explicit. Add `OnValidate` checks for required references.
- **S-25 (MUST)** No empty Unity messages (`Update() {}`): they still cost a native call. Remove unused ones.
- **S-26 (MUST)** No singletons except the audio manager and the `Game` root (GDD §14). Pass dependencies through the `GameInstaller` composition root, by constructor or `Initialize(...)`. No static mutable state.
- **S-27 (SHOULD)** Prefer composition over inheritance. Avoid inheritance chains deeper than two levels.

## 4. Data and ScriptableObjects

- **S-30 (MUST)** Tunable values (every `[TUNE]` in the GDD) live in `GameConfig` / `FeedbackConfig` / `TierDefinition` / `ThemeDefinition` ScriptableObjects, never as magic numbers in code.
- **S-31 (MUST)** ScriptableObjects are **immutable data at runtime**. Never write to a ScriptableObject field during play (it persists in the Editor and corrupts data). Runtime state goes in plain C# objects (e.g. the run state).
- **S-32 (SHOULD)** Provide `[CreateAssetMenu]` for every ScriptableObject type and validate in `OnValidate` (indices, non-null references, ranges).
- **S-33 (MUST)** Serializable data classes (`SaveData`) are plain classes with `[Serializable]`, version field included (GDD §13). Never serialize `UnityEngine.Object` references into the save file.
- **S-34 (MUST)** Use `enum`s or constants for states, layers, tags, animator hashes and mixer parameter names. Cache `LayerMask`, `Animator.StringToHash` and `Shader.PropertyToID` in `static readonly` fields. No string literals scattered in code.

## 5. Events and communication

- **S-40 (MUST)** Use plain C# `event Action<...>` on the owning class (GDD §14.4). Only the owner invokes the event; subscribers never raise it.
- **S-41 (MUST)** Raise events with the null-conditional operator on the delegate (`OnMerged?.Invoke(...)`), which is fine because delegates are not `UnityEngine.Object`.
- **S-42 (SHOULD)** Event payloads are small value types or primitives; do not allocate per event.
- **S-43 (SHOULD)** Do not use `SendMessage`, `BroadcastMessage` or `UnityEvent` for gameplay logic. `UnityEvent` is acceptable only for UI button wiring.

## 6. Performance and memory (GDD §14.5)

- **S-50 (MUST)** **Zero GC allocations per frame** during gameplay: no `new` of reference types, LINQ, `foreach` over non-struct enumerators of `IEnumerable`, boxing, closures/lambdas capturing locals, or string concatenation/interpolation in `Update`, `FixedUpdate`, collision callbacks or event handlers on the hot path.
  Tests that check this criterion measure with `AllocationMeter` (Profiler counter "GC Allocation In Frame Count"). `GC.GetAllocatedBytesForCurrentThread` always returns 0 in the Unity Editor and must not be used.
- **S-51 (MUST)** Pool anything created repeatedly (pieces, particles, UI items, audio sources). Instantiate and destroy only during load phases, never in gameplay frames or inside a physics callback.
- **S-52 (MUST)** Reuse collections: allocate `List<T>`/arrays once and `Clear()`. Use the non-allocating physics APIs (`Physics2D.CircleCast` with a `ContactFilter2D` and a preallocated `RaycastHit2D[]`, `GetContacts` into a buffer).
- **S-53 (MUST)** Text updates use `TMP_Text.SetText` with format arguments or cached strings; do not build strings every frame. Update UI only when the value changes.
- **S-54 (SHOULD)** Do expensive work at a lower rate (e.g. overflow detection at 10 Hz) instead of every frame; do not poll what an event can announce.
- **S-55 (MUST)** Cache `Transform`, `Rigidbody2D`, `Collider2D` and material references. Use `MaterialPropertyBlock` or shared materials, never `renderer.material` (it clones the material).
- **S-56 (SHOULD)** Avoid `Camera.main` in loops, `string` comparison of tags (use `CompareTag`), and `gameObject.name` lookups.
- **S-57 (MUST)** Profile before optimizing anything beyond the rules above, and record the result in the PR when a change is made for performance.

## 7. Physics and time (GDD §5)

- **S-60 (MUST)** Physics work happens in `FixedUpdate` and through the `Rigidbody2D` API (`position`, `linearVelocity`, `AddForce`, `MovePosition`). Never move a physics body through `transform` in `FixedUpdate`; keep interpolation on.
- **S-61 (MUST)** Use `Rigidbody2D.position`, not `transform.position`, when computing physics positions (GDD §14.3).
- **S-62 (MUST)** Never destroy, instantiate or change `Rigidbody2D` simulation state inside `OnCollision*`/`OnTrigger*`. Enqueue the work and process it in `MergeSystem.FixedUpdate`.
- **S-63 (MUST)** Gameplay results must be **deterministic** for a given seed and input sequence (GDD §19): use the seeded `System.Random` from the run state, never `UnityEngine.Random`, and resolve ordering with a stable key (lowest `InstanceID` wins), never by iteration order of a hash set or dictionary.
- **S-64 (MUST)** Use `Time.deltaTime` in `Update` and `Time.fixedDeltaTime` in `FixedUpdate`. Anything that must keep running while paused (`timeScale = 0`) uses `Time.unscaledDeltaTime`.
- **S-65 (MUST)** Use layers and the collision matrix (GDD §5) for filtering, not runtime checks. Cache layer indices and masks.
- **S-66 (MUST)** Visual-only animations change only the local scale or offset of a visual child, never the root transform, the `Rigidbody2D` or a collider, and they never feed back into gameplay. Each animation is a `PieceEffect` class; add a new one instead of branching in `PieceAnimator`.

## 8. Input (GDD §6)

- **S-70 (MUST)** Use the **Input System** package only. Do not use the legacy `UnityEngine.Input` API.
- **S-71 (MUST)** All input is read through `PointerInputReader` and exposed as events or state. Other classes never read devices or `InputAction`s directly.
- **S-72 (MUST)** Enable and disable `InputAction`s in `OnEnable`/`OnDisable` and dispose any action you created in code.
- **S-73 (MUST)** Ignore presses that start over UI (`EventSystem.IsPointerOverGameObject`).

## 9. Addressables and assets (C-01)

- **S-80 (MUST)** Follow every rule in `constraints.md` C-01. In particular: all asset and scene loading goes through `AssetService` and `SceneLoader`; no direct `Addressables.*` calls elsewhere; no `Resources/`; no `SceneManager.LoadScene*` outside the bootstrap and `SceneLoader`.
- **S-81 (MUST)** Reference assets across groups with `AssetReference*` or labels, not direct serialized references. A direct reference between two assets of the same group is fine (`GameConfig` to `FeedbackConfig`, both in Core-Data). Every load has a matching release.
- **S-82 (MUST)** Every async load has error handling and a retry path. Never leave an infinite spinner or an unobserved failed handle.
- **S-83 (SHOULD)** Prefer `async`/`await` (Unity `Awaitable`) or callbacks over coroutines for load flows. Do not start untracked fire-and-forget tasks; keep a handle or cancellation token and cancel on destroy.
- **S-84 (MUST)** Sprites follow the import settings in GDD §10 (Point, no compression or no mipmaps, PPU 16, Sprite Atlas). Apply the Android/iOS platform overrides.

## 10. UI (GDD §8)

- **S-90 (MUST)** UI is **uGUI + TextMeshPro**. No `OnGUI`/IMGUI at runtime.
- **S-91 (MUST)** Views are passive: they render state and raise events (`OnPlayClicked`). They contain no game rules and read no other system directly; a presenter or the installer wires them.
- **S-92 (MUST)** Respect `Screen.safeArea`. Touch targets are at least 44 px in reference space.
- **S-93 (SHOULD)** Disable `Raycast Target` on graphics that do not need input. Avoid nested layout groups on hot UI and avoid rebuilding layouts every frame.
- **S-94 (MUST)** All user-visible text uses localization keys from day 1 (GDD §15).

## 11. Robustness and errors

- **S-100 (MUST)** Never crash on bad data or a failed load. Save/load and asset loading return a recoverable result (GDD §13, C-01).
- **S-101 (MUST)** Do not swallow exceptions. Catch only what you can handle, log with context, and rethrow or fall back deliberately. No empty `catch` blocks.
- **S-102 (MUST)** Use `Debug.Log*` only through a thin logger or guard with `[Conditional("UNITY_EDITOR")]`/`DEVELOPMENT_BUILD`. No logging in `Update` paths and none that allocate in release builds. Prefer `Debug.LogError` with a context object for real faults.
- **S-103 (SHOULD)** Validate invariants with `Debug.Assert` (editor/development only) at API boundaries, and check serialized references in `OnValidate`/`Awake`.
- **S-104 (MUST)** Writes to disk are atomic (write `save.tmp`, then replace) and happen at the points in GDD §13.

## 12. Platform and build (GDD §14.6)

- **S-110 (MUST)** Platform-specific code lives behind `#if UNITY_ANDROID` / `#if UNITY_IOS` in a single wrapper (e.g. `Haptics`), with a safe fallback for the Editor and other platforms.
- **S-111 (MUST)** Android is the primary target: verify new features in an Android build, not only in the Editor.
- **S-112 (MUST)** No network access and no `INTERNET` permission unless the owner approves a change to GDD §17/C-01. No analytics or ad SDKs.
- **S-113 (SHOULD)** IL2CPP is the scripting backend; avoid reflection and code that breaks under managed code stripping. When reflection is unavoidable, preserve with `[Preserve]` or `link.xml`.

## 13. Testing (GDD §19)

- **S-120 (MUST)** Pure logic (`SpawnQueue`, `ScoreSystem`, `ComboTracker`, `SaveSystem`) has EditMode tests. Physics-dependent behavior (merge chain, overflow) has PlayMode tests.
- **S-121 (MUST)** Tests do not depend on built bundles: use `AssetService` test doubles (C-01). A single PlayMode test loads the `Game` scene through Addressables in **Use Asset Database** mode.
- **S-122 (SHOULD)** Test names follow `Method_Scenario_ExpectedResult`. Tests are independent, deterministic (fixed seed) and clean up everything they create.
- **S-123 (MUST)** A bug fix includes a regression test whenever the code is testable.

## 14. Git and review

- **S-130 (MUST)** Branch per issue: `feature/<issue>-<short-name>` (or `fix/...`). Commits are small and describe why, in English. PR descriptions reference the issue and any constraint it complies with (e.g. "Complies with C-01").
- **S-131 (MUST)** Do not commit generated folders (`Library/`, `Temp/`, `Logs/`, `obj/`, `ServerData/`, `UserSettings/`) or IDE files. Commit `.meta` files and `ProjectSettings/`.
- **S-132 (MUST)** Use Force Text serialization and Visible Meta Files. Resolve scene/prefab merge conflicts by keeping scenes small and splitting content into prefabs; avoid two people editing the same scene.
- **S-133 (SHOULD)** Before opening a PR: no compiler warnings, no console errors on entering Play mode, all tests green, grep checks from C-01 pass.
- **S-134 (MUST)** Before opening a PR, update every document the change touches (`README.md`, `.planning/*.md`) and add the lessons of the issue to `.planning/LEARNINGS.md`.

---

## Adding or changing a rule

Append a new `S-NN` in the matching section (never reuse or renumber an ID). Changes that relax a **MUST** need explicit approval from the project owner.
