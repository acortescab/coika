---
last_mapped_commit: cc4a6315450b59b114e2bdbb7e25e918e35eed78
last_mapped_at: 2026-10-03
---
# Codebase Concerns

**Analysis Date:** 2026-10-03

## Tech Debt

**Temporary composition root in the Game scene:**
- Issue: `Assets/Scripts/Gameplay/DropControllerBootstrap.cs` is marked TEMPORARY ("TODO #11: the game manager replaces it"). It wires DropController, MergeSystem and related pieces by hand.
- Files: `Assets/Scripts/Gameplay/DropControllerBootstrap.cs`
- Impact: Run lifecycle (restart, game over, score) is not owned by any manager. `MergeSystem.ResetForNewRun` and `PieceFactory.ReleaseAll` rely on a not-yet-existing owner.
- Fix approach: Implement the game manager (issue #11) and delete the bootstrap.

**Boot failure has no recovery:**
- Issue: `Boot()` logs the exception and leaves the player on an empty Boot scene.
- Files: `Assets/Scripts/Core/Boot/GameInstaller.cs` (TODO at line 51, constraint C-01)
- Impact: A failed Addressables init or scene load is a dead end.
- Fix approach: Add an error state with a retry button calling `Boot()`.

**`async void Start()`:**
- Files: `Assets/Scripts/Core/Boot/GameInstaller.cs:27`
- Impact: Exceptions thrown before `Boot()`'s try block (service construction) are unobserved. `Boot` is public, so retry can be re-entered concurrently.
- Fix approach: Guard against re-entry and keep all awaited work inside try/catch.

**Placeholder/debug code in runtime assembly:**
- Files: `Assets/Scripts/Gameplay/PieceDebugSpawner.cs`, `Assets/Scripts/Tools/Editor/PlaceholderTierSpriteGenerator.cs`
- Impact: Debug spawner ships in the Gameplay assembly. Tier art is placeholder.
- Fix approach: Guard with `#if UNITY_EDITOR || DEVELOPMENT_BUILD` or move out of runtime before release.

**Empty feature folders:**
- Files: `Assets/Scripts/Audio`, `Assets/Scripts/Fx`, `Assets/Scripts/UI`, `Assets/Scripts/Input`
- Impact: `MergeSystem.Merged` and `SupernovaTriggered` events have no subscribers yet (no score, effects, audio).

## Known Bugs

None confirmed by static review. Potential edge cases are listed under Fragile Areas.

## Security Considerations

**Local single-player game:** No network, auth or secrets detected. `Library/`, `Temp/`, `Logs/`, `UserSettings/` are untracked build artifacts (0 tracked `Library` files); keep them in `.gitignore`.

## Performance Bottlenecks

**Per-step queue sort in MergeSystem:**
- Files: `Assets/Scripts/Gameplay/MergeSystem.cs` (`ProcessQueue`)
- Problem: `_queue.Sort()` each physics step with pending pairs. Cheap at expected piece counts.
- Improvement path: Only revisit if profiling shows cost; verify `MergePairQueue` sort does not allocate (tests for allocations exist).

**Per-piece `GetComponent` in pool instantiation:**
- Files: `Assets/Scripts/Core/Pooling/PrefabPool.cs:253`
- Impact: Only on instantiation (pre-warm), negligible.

## Fragile Areas

**Merge timing and chain ordering:**
- Files: `Assets/Scripts/Gameplay/MergeSystem.cs`, `Assets/Scripts/Gameplay/MergePairQueue.cs`, `Assets/Scripts/Gameplay/Piece.cs`
- Why fragile: Depends on collision callbacks re-reporting contacts each step, a `CONTACT_TOLERANCE` of 0.1 world units, and `lossyScale.x` assumption (uniform scale). A failed creation is retried each step and only logged once per run.
- Safe modification: Keep creation/release out of collision callbacks (S-62). Change tolerance together with `Assets/Tests/PlayMode/MergeSystemPlayModeTests.cs`.
- Test coverage: EditMode `MergePairQueueTests.cs` and PlayMode tests exist.

**Event subscription lifecycle:**
- Files: `Assets/Scripts/Gameplay/MergeSystem.cs` (`Subscribe`/`Unsubscribe`)
- Why fragile: `Unsubscribe` dereferences `_factory` and its `ActivePieces`; if the factory is destroyed first, it can throw in `OnDisable`. Re-calling `Initialize` with a new factory relies on prior unsubscribe being exact.
- Safe modification: Null-check `_factory` and keep init order stable.

**Pooled piece reuse:**
- Files: `Assets/Scripts/Gameplay/Piece.cs`, `Assets/Scripts/Gameplay/PieceFactory.cs`, `Assets/Scripts/Core/Pooling/PrefabPool.cs`
- Why fragile: Pieces are reused as other tiers; stale `Merged`/`IsHeld` flags or event handlers cause ghost merges. `CanStillMerge` defends against this.

**Large input/controller classes:**
- Files: `Assets/Scripts/Gameplay/DropController.cs` (426 lines), `Assets/Scripts/Gameplay/Input/PointerInputReader.cs` (258 lines)
- Why fragile: Pointer plus keyboard handling and drop flow in one controller; changes risk input regressions.
- Safe modification: Extend `DropInputState` / `DropFlow` (already extracted) rather than growing the controller.

**Addressables data:**
- Files: `Assets/AddressableAssetsData`
- Why fragile: Scenes and themes load by AssetReference; broken references only fail at runtime in `Boot()`.

## Scaling Limits

Not applicable beyond pool pre-warm sizes (see `Assets/Scripts/Core/Pooling/PrefabPool.cs`); exhausted tiers throw `InvalidOperationException`, handled in `MergeSystem.TryCreate`.

## Dependencies at Risk

**Unity 6 Rigidbody2D `linearVelocity` API and Addressables:** Code is tied to Unity 6 APIs (`Rigidbody2D.linearVelocity`); engine downgrades break compilation. Check `Packages/manifest.json` for pinned versions.

## Missing Critical Features

**Game manager, score, game over, UI, audio, FX:** Not implemented (issue #11 and later). Blocks a playable loop beyond the debug bootstrap.

## Test Coverage Gaps

**Boot and Addressables flow:**
- What's not tested: `GameInstaller.Boot` failure path and real Addressables scene loading (tests use `Assets/Tests/EditMode/Fakes/FakeAssetService.cs`).
- Files: `Assets/Scripts/Core/Boot/GameInstaller.cs`, `Assets/Scripts/Core/Assets/SceneLoaderService.cs`
- Priority: Medium

**CI:** No `.github` workflows detected; tests run only locally.
- Priority: Medium

**Editor tools:** `Assets/Scripts/Tools/Editor/*` (JarPrefabTool, PiecePrefabTool, ThemeValidator) have limited test coverage.
- Priority: Low

---

*Concerns audit: 2026-10-03*
