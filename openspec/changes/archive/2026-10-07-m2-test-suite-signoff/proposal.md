# Proposal

## Why

M2 (issues #26–#39) shipped its own unit tests, but nothing yet proves the M2 features work together: determinism with the feedback layer, pauses and slow-mo, the run lifecycle with every pool and listener, and the one-command gate. Issue #40 is the M2 quality gate, as #12 was for M1, and ends with the manual sign-off on device.

## What Changes

- Extend the PlayMode simulation harness with pause/resume and slow-mo hooks, so a run can be paused at scripted points and slowed down like in the game.
- Add M2 determinism tests: all M2 systems on vs all off, with pauses, with slow-mo, over at least 5 seeds, each pair giving an identical `SimulationResult`. The golden file `simulation-seed-1234.txt` stays byte-identical.
- Add one end-to-end feedback test: a scripted run with real merges that produces a combo (two merges inside the combo window), a tier >= 8 merge, a Supernova and a game over, asserting the #34 feedback table through the fakes.
- Extend the lifecycle test: object, listener, particle pool and audio voice counts identical at the start of 3 Retry runs and across a Pause/Resume cycle. Extend the Boot -> Game test to assert the M2 services are created and released with the scene.
- Fill the save and input test gaps: best score on game over persisted through a reload, settings round trip through disk, a lower version number, and piece bounds inside the jar.
- Add grep checks to `Tools/run-tests.ps1` for the `INTERNET` permission, `OnGUI` (allow-listing `DebugOverlay.cs`), `UnityEngine.Random` in gameplay, and `persistentDataPath` outside `FileSaveStorage` and `GameInstaller`.
- Add `.planning/m2-signoff.md`: the M2 coverage table (each M2 issue's acceptance criteria -> covering test, gaps marked) and the on-device checklist, with the device results left blank for the user.
- No production code change is planned. If the harness needs a production seam for pause, it is raised before it is added.

## Capabilities

### New Capabilities
- `simulation-harness`: the deterministic PlayMode simulation, including scripted pause/resume and slow-mo, and the determinism, lifecycle and allocation guarantees it must prove with all M2 systems.
- `test-gate`: the one-command test run, its source grep checks and its exit code, plus the M2 sign-off document.

### Modified Capabilities

None.

## Impact

- Test code: `Assets/Tests/PlayMode/Harness/` (`SimulationWorld`, `SimulationRunner`, `SimulationOptions`), new and extended tests in `Assets/Tests/PlayMode` and `Assets/Tests/EditMode`.
- Tooling: `Tools/run-tests.ps1`.
- Docs: `.planning/m2-signoff.md` (new), `.planning/LEARNINGS.md`, and `README.md` if the test commands change.
- Out of scope: cloud CI, device farms, UI visual regression tests, M3 content.
