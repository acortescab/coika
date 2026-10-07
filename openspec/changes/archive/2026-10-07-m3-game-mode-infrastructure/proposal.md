## Why

M3 adds Daily and Zen next to Classic (GDD §12). They differ only in the seed, the end condition and where the best score is stored, but today the Classic assumptions are hard-coded: `GameSceneInstaller` seeds every run with `Environment.TickCount`, always enables the `OverflowDetector`, and reads and writes `bestScore.classic` directly. Without one mode abstraction, Daily (#68), Zen (#69), the Menu (#67) and autosave (#72) would each add a `switch` on the mode in several places.

## What Changes

- Add a `GameMode` enum (`Classic`, `Daily`, `Zen`) and `IGameModeRules` (Unity-free, `Coika.Gameplay`) with `ResolveSeed`, `EndsOnOverflow`, `ShowsDangerLine`, `RecordsBestScore` and `SaveKey`, one implementation per mode, in a registry table. A new mode is a new table entry.
- Add `ISeedSource` (fresh seed for Classic; the real one may use the clock, tests inject a fixed one) and `IUtcClock` (UTC date for the Daily seed `yyyyMMdd`). The clock is read only at run start (S-63).
- Add `RunSetup`, a plain object created by `GameInstaller` that holds the chosen mode and seed. The Menu/Modes screen writes it and the Game scene reads it; it crosses the scene load through the installer, not through statics.
- Change `GameManager` to start from `RunSetup` instead of a `Func<int>`: Classic Retry draws a new seed, Daily Retry keeps the same one.
- Change `RunSystems` to enable the `OverflowDetector` only when the mode `EndsOnOverflow`.
- Change the best-score flow: `SaveData.GetBest(mode)` / `SetBest(mode, score)` hide the nested daily `{date, score}`, a daily best of another date reads as 0, and `RecordRun` and the scene's best-score load go through the mode's `SaveKey`/`RecordsBestScore`. No schema version bump.
- Classic must behave exactly as before: `simulation-seed-1234.txt` stays unchanged.

Out of scope: the Modes screen (#68), the Menu (#67), Daily/Zen specifics such as hiding the Danger Line or the clear-jar button (#68, #69), run autosave (#72). `ShowsDangerLine` is defined and tested here but consumed by #69.

## Capabilities

### New Capabilities
- `game-modes`: the mode abstraction (rules table, seed resolution, run setup, retry semantics, end condition) and the mode-aware best score in the save.

### Modified Capabilities
<!-- None: no existing spec in openspec/specs covers run start, seeding or the save. -->

## Impact

- Code: `Assets/Scripts/Gameplay` (`GameManager`, `RunSystems`, new `GameMode`, `IGameModeRules`, rules, registry, `RunSetup`, `ISeedSource`), `Assets/Scripts/Core/Save` (`SaveData`, `BestScores`, `DailyBest`), `Assets/Scripts/Core/Boot/GameInstaller.cs` (creates `RunSetup`), `Assets/Scripts/UI/GameSceneInstaller.cs` (reads it, replaces the two `Environment.TickCount` seeds and the `bestScore.classic` use).
- Tests: new EditMode tests (rules table incl. a fake fourth mode, daily seed across midnight, per-mode best round-trip) and a PlayMode harness test (Daily retry replays the same sequence); existing M1/M2 tests and the golden file stay green. `GameManager` constructor changes, so its tests and `HarnessTestBase` callers are updated.
- Docs: `.planning/LEARNINGS.md`, `.planning/gdd.md` §12/§13 if the wording changes, `README.md` structure if needed.
- No assets, no new dependencies, no new scene loads (C-01); XML docs on everything (C-02).
