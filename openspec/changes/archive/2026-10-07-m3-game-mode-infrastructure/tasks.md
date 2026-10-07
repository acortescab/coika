# Tasks

## 1. Mode rules and seeds (Coika.Gameplay / Coika.Core)

- [x] 1.1 Add `GameMode`, `IGameModeRules` (incl. `IsFreshPerRun`), `ClassicRules`, `DailyRules`, `ZenRules`, the `GameModeRules` registry, `ISeedSource` and `IUtcClock` with their real implementations, XML docs on everything (C-02). Verify the solution compiles.
- [x] 1.2 EditMode `GameModeRulesTests`: each mode's documented flags and `SaveKey`; a fourth fake mode registered in a test works with no production change; a missing mode throws. Verify with `./Tools/run-tests.ps1 -Mode EditMode -Filter GameModeRules`.
- [x] 1.3 EditMode `DailySeedTests`: fake clock at 2026-10-07 gives `20261007`; 23:59:59 vs 00:00:00 across midnight give different seeds; Classic returns the fixed seed source value. Verify with `-Filter DailySeed`.

## 2. Mode-aware save

- [x] 2.1 Add `SaveData.GetBest(mode, utcDate)` / `SetBest(mode, score, utcDate)` and give `RecordRun` the mode so only modes that record the best write it; totals, highest tier and discovered tiers still update for every mode. No version bump. Verify the solution compiles.
- [x] 2.2 EditMode `SaveDataBestTests`: per-mode round trip through JSON, no leak between modes, daily best of another date reads 0, best only goes up, a pre-change save JSON loads with the Classic best kept. Update existing `RecordRun` tests. Verify with `-Filter SaveData`.

## 3. Run setup and manager integration

- [x] 3.1 Add `RunSetup` (mode default Classic, stored seed); create it in `GameInstaller` and hand it to the Game scene through a `UseRunSetup` like `UseSave`, with a local Classic fallback when absent. No statics, no `SceneManager.LoadScene*` added (C-01). Verify `./Tools/run-tests.ps1` C-01 grep checks pass.
- [x] 3.2 Change `GameManager` to start from `RunSetup`, the rules registry, `ISeedSource` and `IUtcClock`; `Retry` keeps the stored seed when `IsFreshPerRun` is false. Change `IRunSystems.PrepareRun(int seed, IGameModeRules rules)` and `RunSystems.BeginPlaying` to enable the `OverflowDetector` only when `EndsOnOverflow`. Verify the solution compiles.
- [x] 3.3 Update `GameManagerTests` and the test fakes of `IRunSystems`; add cases: Classic retry draws a new seed, Daily retry keeps the seed across a clock change, mode kept on retry, default is Classic, a non-overflow mode never enables the detector. Verify with `-Filter GameManager`.
- [x] 3.4 Wire `GameSceneInstaller.Compose`: remove both `Environment.TickCount` seeds, load `ScoreSystem.BestScore` from `GetBest(mode)` at each run start, and call `SetBest`/`RecordRun` with the mode in `HandleRunEnded`. Verify no `Environment.TickCount` or `bestScore.classic` remains in `Assets/Scripts/UI` (Grep) and the solution compiles.

## 4. Determinism and performance checks

- [x] 4.1 PlayMode harness test: Daily retry replays the identical piece sequence (same seed, two runs, synchronous harness). Verify with `-Mode PlayMode -Filter Daily`.
- [x] 4.2 Confirm Classic is unchanged: `simulation-seed-1234.txt` is not modified (`git diff --stat` shows no change) and the golden test passes twice. Verify the allocation tests (`AllocatesNothing`) still pass and there are no console errors or warnings.

## 5. Docs and integration

- [x] 5.1 Update `.planning/LEARNINGS.md` with a dated section (decisions: rules table, `IsFreshPerRun`, `IUtcClock` seam, best loaded per run), and `.planning/gdd.md` §12/§13 and `README.md` structure only where they now differ. Verify by Grep that each touched doc mentions `IGameModeRules` or the mode flow as needed.
- [x] 5.2 Run the full `./Tools/run-tests.ps1` (C-01 grep checks, EditMode, PlayMode) with the Editor closed. Verify it exits 0 and the junit XML has no failed `testcase`.

## Workflow follow-up

- Open the PR from `feature/66-game-mode-infrastructure` with the doc changes included, then archive the change with `/opsx:archive`.
