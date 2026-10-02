# Testing Patterns

**Analysis Date:** 2026-10-03

## Test Framework

**Runner:**
- Unity Test Framework `com.unity.test-framework` 1.8.0 (NUnit), `Packages/manifest.json`.
- EditMode asmdef: `Assets/Tests/EditMode/Coika.Tests.EditMode.asmdef` (Editor-only; references Core, Data, Gameplay, Tools.Editor, Addressables).
- PlayMode asmdef: `Assets/Tests/PlayMode/Coika.Tests.PlayMode.asmdef` (all platforms; adds InputSystem + TestFramework, ugui, ResourceManager).
- Both: `overrideReferences` with `nunit.framework.dll`, `autoReferenced: false`, `defineConstraints: UNITY_INCLUDE_TESTS`.

**Assertion Library:** NUnit classic model (`Assert.AreEqual`, `Assert.Throws`, `Assert.IsTrue`), `LogAssert` for console logs.

**Run Commands:**
```bash
# Unity Editor: Window > General > Test Runner (EditMode / PlayMode tabs)
Unity.exe -batchmode -projectPath D:\Work\coika -runTests -testPlatform EditMode -testResults results.xml
Unity.exe -batchmode -projectPath D:\Work\coika -runTests -testPlatform PlayMode -testResults results.xml
```
No CI config or coverage tooling detected. About 236 `[Test]`/`[UnityTest]` markers exist.

## Test File Organization

**Location:** Separate trees: `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`; shared doubles in `Fakes/` subfolders.

**Naming:** `<Type>Tests.cs` (EditMode, e.g. `SpawnQueueTests.cs`, `DropFlowTests.cs`); PlayMode `<Type>PlayModeTests.cs` or `<Feature>Tests.cs` (`MergeSystemPlayModeTests.cs`, `JarPhysicsTests.cs`, `GameSceneLoadTests.cs`). Namespaces `Coika.Tests.EditMode` / `Coika.Tests.PlayMode`.

**Structure:**
```
Assets/Tests/EditMode/{*Tests.cs, Fakes/{AllocationMeter,FakeAssetService,FakeDropInput,PieceFixtures,TestGameConfig,TestSpawnSettings}.cs}
Assets/Tests/PlayMode/{*Tests.cs, Fakes/MergeTestWorld.cs}
```

## Test Structure

**Suite Organization:** Test class has an XML doc naming the issue and criteria it checks; private `const` for seeds/tolerances; helpers are private static with docs; each test has a `<summary>`.
```csharp
public class SpawnQueueTests
{
    private const int SEED = TestSpawnSettings.SEED;

    /// <summary>A new queue already holds the current and the next tier, and the seed it was given.</summary>
    [Test]
    public void Constructor_WithValidSettings_FillsCurrentNextAndSeed()
    {
        var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);
        Assert.AreEqual(0, queue.Current);
    }
}
```

**Patterns:**
- Names `Method_Scenario_ExpectedResult` (S-122): `Merge_TwoTouchingPieces_ProducesExactlyOneNextTierPiece`.
- Arrange/Act/Assert separated by blank lines, no comments needed.
- `[SetUp]`/`[TearDown]` reset counters and dispose worlds (`_world?.Dispose()`); ScriptableObjects/GameObjects created in tests are destroyed with `UnityEngine.Object.DestroyImmediate` in `try/finally`.
- Fixed seeds everywhere for determinism (`SEED = 20260`).
- Expected errors: `LogAssert.Expect(LogType.Error, new Regex("needs a sprite"))`; clean paths assert `LogAssert.NoUnexpectedReceived()`.
- Argument guards tested with `Assert.Throws<ArgumentNullException>`.

## Mocking

**Framework:** None (no Moq/NSubstitute). Hand-written fakes in `Fakes/`.

**Patterns:**
```csharp
public class FakeAssetService : IAssetService
{
    public int FailOnAttempt { get; set; }            // fail a chosen load
    public Func<Type, UnityEngine.Object> Provider { get; set; }
    public Task Gate { get; set; }                    // hold loads pending
    public int LoadCount { get; private set; }
    public int ReleaseCount { get; private set; }     // loads minus releases must be 0
}
```
- `Assets/Tests/EditMode/Fakes/FakeDropInput.cs` implements `IDropInput`.
- `Assets/Tests/EditMode/Fakes/TestGameConfig.cs` and `TestSpawnSettings.cs` build in-memory ScriptableObjects by writing serialized fields via `SerializedObject` (this runs `OnValidate`); caller destroys them.
- `Assets/Tests/EditMode/Fakes/PieceFixtures.cs` builds pieces/factories.

**What to Mock:** `IAssetService`, `ISceneLoader`, `IDropInput` (S-121: tests never depend on built bundles).
**What NOT to Mock:** Physics, `Rigidbody2D`, `PieceFactory`, `MergeSystem`: PlayMode tests use the real thing.

## Fixtures and Factories

`Assets/Tests/PlayMode/Fakes/MergeTestWorld.cs`: disposable world with jar, pooled `Factory`, `MergeSystem`, helper `CountActive(tierIndex)`, `Origin`, `Jar`, `TIER_COUNT`, and `UseScriptedPhysics()` to step physics by hand (fast, exact). Tests must call `Dispose()` in `[TearDown]`.

## Coverage

**Requirements:** None enforced; no coverage package configured. S-120: pure logic (`SpawnQueue`, `ScoreSystem`, `ComboTracker`, `SaveSystem`) gets EditMode tests; physics-dependent behavior (merge chain, overflow) gets PlayMode tests. S-123: bug fixes include a regression test. S-133: all tests green before a PR.

## Test Types

**Unit Tests (EditMode):** Pure logic and data validation: `SpawnQueueTests`, `SpawnSelectorTests`, `MergePairQueueTests`, `DropFlowTests`, `DropInputStateTests`, `TierDataTests`, `GameConfig*Tests`, `PrefabPoolTests`, `ProjectFoundationTests`.

**Integration Tests (PlayMode):** Real physics and components: `MergeSystemPlayModeTests` (1000 randomized pairs, chains, Supernova, determinism), `JarPhysicsTests`, `PieceCollisionTests`, `PointerInputReaderPlayModeTests` (Input System test framework), `DropControllerPlayModeTests`, `PieceFactoryIntegrationTests`, `TierSpriteLoadTests`.

**Scene test:** `Assets/Tests/PlayMode/GameSceneLoadTests.cs` loads the `Game` scene through Addressables in Use Asset Database mode (S-121).

**Allocation tests:** Use `AllocationMeter.Measure(Action)` (`Assets/Tests/EditMode/Fakes/AllocationMeter.cs`), which reads the Profiler counter "GC Allocation In Frame Count"; assert result `<= AllocationMeter.TOLERANCE_COUNT` (20). Never use `GC.GetAllocatedBytesForCurrentThread` (always 0 in the Editor). Build delegates/data before measuring.

## Common Patterns

**Async/frame Testing (PlayMode):**
```csharp
[UnityTest]
public IEnumerator Merge_With1000RandomizedPairs_AlwaysProducesExactlyOneResult()
{
    StartWorld(8);
    for (var trial = 0; trial < RANDOM_TRIALS; trial++) { /* arrange, StepFor(MAX_STEPS), assert */ yield return null; }
}
```
Use plain `[Test]` when physics is stepped manually; `[UnityTest]` when frames must pass.

**Error Testing:**
```csharp
LogAssert.Expect(LogType.Error, new Regex("needs a sprite"));
Assert.Throws<ArgumentNullException>(() => system.Initialize(null, tiers, config));
```

---

*Testing analysis: 2026-10-03*
