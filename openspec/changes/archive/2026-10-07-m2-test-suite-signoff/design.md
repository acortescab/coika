# Design

## Context

Most of issue #40 already has tests from #11, #12, #34, #35 and #39: three separate on/off comparisons (animations, particles, director), `SaveSystemTests`, the input tests, `FullRunAllocationPlayModeTests` and the golden-file test. The gaps are pause and slow-mo in the harness, one end-to-end feedback test, particle pool and audio voice counts in the lifecycle test, the four grep checks, and the sign-off document.

`SimulationWorld.Step()` advances a local physics scene with a fixed `dt` and a simulated clock (`steps * fixedDeltaTime`). It has no pause or time scale. In the game, `Time.timeScale` changes only how many fixed steps run per real second, never the `dt` of a step.

Finding (task 1.1): `GameManager.Pause`/`Resume` only change the state, and `TimeScaleOwner` takes an `ITimeScale` and a clock, so both run inside `SimulationWorld` with no production change.

## Goals / Non-Goals

**Goals:**
- Prove the M2 layer, pauses and slow-mo never change a simulation result.
- Reuse the existing harness, fakes and test patterns; keep the golden file untouched.
- Make `run-tests.ps1` fail on the new source rules.

**Non-Goals:**
- Production code changes. A needed production seam is raised with the user first.
- Device results: those are filled in by the user.

## Decisions

- **Pause as "no step".** `SimulationWorld` gets `Pause()` and `Resume()` that call the real `GameManager.Pause` and `Resume`, and `Step()` returns early while paused, as Unity freezes fixed steps at `timeScale = 0`. The runner gets a scripted "pause for N steps after drop K" option. Alternative: only call `Manager.Pause()` and keep stepping. Rejected because physics would not freeze, so it would test less.
- **Slow-mo through the real `TimeScaleOwner`.** The harness builds it with a fake `ITimeScale` and the simulated clock, so the director's slow-mo request is applied to a recording scale instead of `Time.timeScale`. Since slow-mo cannot change a step's `dt`, the test asserts the result is identical and the scale is back to 1 at the end. Alternative: scale `dt` in the harness. Rejected because it would not match the game and would change results by design.
- **One table-driven determinism test.** `M2DeterminismPlayModeTests` holds a table of variants (all on, pauses, slow-mo, both) and a list of 5 seeds; one test runs every pair against the all-off baseline of its seed (played once and cached), comparing the per-step fingerprint (`SimulationFingerprint`, shared with other tests) and the full `SimulationResult` text. A new variant adds one table entry and no loop. Alternative: one test per variant and seed. Rejected as repeated blocks. Random drops never reach a heavy tier, so every run, baseline included, gets the same heavy pair of pieces at the same simulated step (`MergeScenario.PlacePair`), and each variant asserts that its pauses and its slow-mo really happened.
- **End-to-end feedback test** uses `SimulationWorld` with the real director and the fakes, and `MergeScenario` to force the combo (two merges inside the combo window), the tier >= 8 merge and the Supernova, then a game over. It asserts the #34 table rows from the fakes. A same-step chain on real physics depends on piece placement, and its one-bundle-per-step rule has its own unit test.
- **Lifecycle** extends the existing `Describe` string with the particle pool size and the time scale, and inserts a Pause/Resume between runs. Live particles of the last run play out in the next one, so a separate drain check asserts that none stays alive. Real audio voices are read in the Boot -> Game test, which also adds the M2 services created and released with the scene, because the harness uses a fake audio service.
- **Grep checks** reuse `Find-Violations` in `run-tests.ps1` (comment lines skipped). `OnGUI` allows `DebugOverlay.cs`. `INTERNET` reads every `*.xml` under `Assets/Plugins` for a `uses-permission` of INTERNET that lacks `tools:node="remove"`, and flags `ForceInternetPermission: 1` in `ProjectSettings.asset`. `UnityEngine.Random` scans `Assets/Scripts/Gameplay`. `persistentDataPath` scans `Assets/Scripts` and allows `FileSaveStorage.cs` and `GameInstaller.cs`.
- **Sign-off doc** follows `m1-test-coverage.md` (one section per issue with a `| Criterion | Test |` table, `Gap` and `Manual` marks) plus the device checklist from `device-checklist-39.md`.

## Risks / Trade-offs

- [Pause in the harness needs a production seam] -> Read `GameManager.Pause` first; if it cannot run without the scene wiring, stop and ask before touching production code.
- [The 3 minute limit and flakiness need full Editor runs] -> Run `run-tests.ps1 -Repeat 10` with the Editor closed gracefully, and report the real timings.
- [Allocation test can fail once on a cold Editor] -> Rerun once before investigating.
- [A pool-growth warning fails harness tests] -> Size the pool; never silence the guard.
- [The `INTERNET` grep may flag a manifest that removes it] -> The check accepts `tools:node="remove"` on the same element.
