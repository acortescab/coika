# Design

## Context

Observed in the code today (see proposal.md for motivation):

- `GameManager(IRunSystems, Func<int> seedSource)` starts every run with `IRunSystems.PrepareRun(seed)`; `GameSceneInstaller` passes `() => Environment.TickCount` (line ~522) and also seeds the first controller queue with it (~490).
- `RunSystems.BeginPlaying` always calls `OverflowDetector.Enable()`; `StopPlaying` builds the `RunSummary` and writes `Score.BestScore`.
- Best score: `GameSceneInstaller` loads `_save.Data.bestScore.classic` into `ScoreSystem.BestScore` (~518) and `HandleRunEnded` calls `SaveData.RecordRun`, which does `bestScore.classic = Math.Max(...)` (~928). `BestScores` already has `classic`, `daily {date, score}` (`DailyBest`) and `zen`.
- The repo has no `IClock`: time reaches systems as `Func<double>` seconds. A UTC calendar date needs a different shape, so it is a new seam.
- `GameInstaller` (Boot) owns the long-lived services (`Scenes`, `Save`, settings) and hands them to the Game scene through `Use*` methods; without them (tests) the scene runs unpersisted.

## Goals / Non-Goals

**Goals:**
- One abstraction that carries every per-mode difference, so Daily/Zen/Menu/autosave add table entries and consumers, not branches.
- Keep `Coika.Gameplay` rules free of Unity types (EditMode-testable) and deterministic: the real clock is read once at run start.

**Non-Goals:**
- No UI, no Daily/Zen behaviour beyond the flags, no autosave (see proposal.md out of scope).
- No change to `SpawnQueue`, the physics or the scoring formula.

## Decisions

**1. `IGameModeRules` + `GameModeRules` registry (table, not switch).** Interface members as in the issue; one small sealed class per mode (`ClassicRules`, `DailyRules`, `ZenRules`) and a static-free `GameModeRules` registry built from a `IReadOnlyDictionary<GameMode, IGameModeRules>` (default table created by the installer). A test builds a registry with a fake fourth rules object. Lookup of a missing mode throws, never falls back to Classic. Alternative: a `switch` on `GameMode` in each consumer, rejected as the exact duplication this issue exists to avoid. Alternative: a ScriptableObject per mode, rejected because the rules are logic, not tunable data, and C-01 would add assets for no gain.

**2. `ResolveSeed(ISeedSource, IUtcClock)`.** The issue lists `ResolveSeed(IClock)`; the rules need both a fresh seed (Classic) and a date (Daily), so each rule takes the two seams and ignores the one it does not use. `ISeedSource.NextSeed()` and `IUtcClock.UtcNow` (`DateTime`, Kind Utc) are tiny interfaces in `Coika.Core`. Real implementations use `Environment.TickCount` and `DateTime.UtcNow` and live next to the installer; tests inject fixed ones. Alternative: reuse `Func<double>`, rejected because a calendar date cannot come from a monotonic seconds clock.

**3. `RunSetup` holds mode and the current seed.** Plain class created by `GameInstaller`, exposed as a property and handed to the scene via `UseRunSetup` like `UseSave`. Members: `Mode` (default Classic), `Seed` (nullable until a run starts). `GameManager.StartRun` resolves a seed through the rules and stores it in `RunSetup`. `Retry` reuses the stored seed when the rules say the seed is not fresh per run (Daily: same board, even across midnight) and re-resolves otherwise (Classic). That bit is one extra data-like member on the rules, `IsFreshPerRun`, a sixth beyond the issue's list. Alternative: a `RetrySeed` method, rejected as more surface for the same one bit.

**4. `GameManager` takes `RunSetup`, `IGameModeRules` lookup, `ISeedSource`, `IUtcClock` instead of `Func<int>`.** `IRunSystems.PrepareRun(int seed, IGameModeRules rules)` so `RunSystems` knows the end condition; `BeginPlaying` enables the `OverflowDetector` only when `rules.EndsOnOverflow` (the detector stays disabled in Zen, so no danger events fire). `StopPlaying`/`Disable` stay symmetrical and harmless when it was never enabled. Alternative: have `GameManager` toggle the detector, rejected because it holds no gameplay rule (its class contract).

**5. Best score through `SaveData.GetBest/SetBest(mode, score, utcDate)`.** `GetBest(mode)` returns `classic`, `zen`, or `daily.score` only when `daily.date` equals today's `yyyyMMdd` (the date is a parameter so `SaveData` stays free of the clock). `SetBest` keeps the max and, for Daily, resets the stored date when it differs. `SaveKey` on the rules is the stable string (`classic`, `daily`, `zen`) matching the JSON field names; `GetBest/SetBest` dispatch on `GameMode`, with the field access in one place in `SaveData` since JsonUtility needs the concrete fields. `RecordRun` gets a `GameMode`/`recordsBest` parameter: totals, highest tier and discovered tiers are still updated for every mode; only the best score honours `RecordsBestScore`. No version bump: the three fields already exist.

**6. Scene wiring.** `GameSceneInstaller.Compose` replaces both `Environment.TickCount` uses: the first queue gets the seed from the manager's resolve-at-start (a placeholder seed is fine, the run replaces it, as the existing comment says), and `_score.BestScore` is loaded from `GetBest(mode)` once in `Compose` (the mode is fixed before the scene loads, and `RunSystems.StopPlaying` carries the best across runs). Without `RunSetup` (tests, Game scene opened directly) the installer creates a local Classic one, so the Editor flow is unchanged.

## Risks / Trade-offs

- [`GameManager` and `IRunSystems` signatures change, touching their tests and fakes] → update them in the same change; the compiler finds every caller.
- [Classic drifts from the golden result] → the Classic path calls the same `PrepareRun(seed)`; the harness still passes an explicit seed, and the golden test is the guard, run twice.
- [Best score loaded once per scene] → the Menu picks the mode before the Game scene loads, so one load is enough; revisit if a mode can change without a scene load.
- [Daily date rollover while the app stays open] → Retry keeps the seed on purpose (spec); a fresh Daily start after midnight re-resolves. The stale-best case is handled by the date check in `GetBest`.
- [`IsFreshPerRun` is a sixth member beyond the issue's list] → it is data-like and removes a mode `if` from `GameManager`; mentioned in the PR.

## Migration Plan

No save migration: the save schema is unchanged. Rollback is reverting the change.
