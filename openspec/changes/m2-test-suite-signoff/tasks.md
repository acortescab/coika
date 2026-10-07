# Tasks

## 1. Harness pause and slow-mo

- [x] 1.1 Read `GameManager.Pause`/`Resume` and `TimeScaleOwner`; confirm they run inside `SimulationWorld` without a production change (if not, stop and ask). Verify by writing down the finding in the design notes.
- [x] 1.2 Add `Pause()`/`Resume()` to `SimulationWorld` (`Step()` does nothing while paused) and a scripted pause option to `SimulationOptions`/`SimulationRunner`. Verify with a test that steps while paused leave simulated time, score and pieces unchanged.
- [x] 1.3 Wire a real `TimeScaleOwner` with a fake `ITimeScale` and the simulated clock into the world when the feedback is on. Verify with a test that a heavy merge sets the fake scale and that it is back to 1 at the end.
- [x] 1.4 Add `.planning/LEARNINGS.md` notes for the harness (pause as no-step; slow-mo cannot change `dt`). Verify the dated section exists.

## 2. Determinism tests

- [x] 2.1 Add a shared per-step fingerprint and the table-driven `M2DeterminismPlayModeTests`: all M2 systems on vs off, with pauses, with slow-mo, over 5 seeds. Verify the tests pass and `Assets/Tests/PlayMode/Golden/simulation-seed-1234.txt` has no git diff.

## 3. Feedback, lifecycle and boot tests

- [x] 3.1 Add the end-to-end feedback test (chain, tier >= 8, Supernova, game over) through the fakes. Verify it passes and fails if a table row is removed from the director.
- [x] 3.2 Extend the lifecycle `Describe` with particle pool and audio voice counts and add a Pause/Resume between 3 Retry runs. Verify the test passes for 5 seeds.
- [x] 3.3 Extend the Boot -> Game test to assert the M2 services are created and released with the scene. Verify the test passes in Use Asset Database mode.

## 4. Save and input gaps

- [x] 4.1 Add tests for best score on game over persisted through a reload, a settings disk round trip, and a lower version number. Verify they pass in EditMode.
- [x] 4.2 Add a piece-bounds test (the held piece never leaves the jar) if none exists after checking `DropController` and the jar tests. Verify it passes.

## 5. Test gate script

- [x] 5.1 Add the `INTERNET`, `OnGUI` (allow `DebugOverlay.cs`), gameplay `UnityEngine.Random` and `persistentDataPath` checks to `Tools/run-tests.ps1`. Verify each passes on the current tree.
- [x] 5.2 Demonstrate the exit code: break each rule temporarily and verify the script exits non-zero, then revert. Verify the tree has no leftover change.

## 6. Sign-off and docs

- [x] 6.1 Write `.planning/m2-signoff.md`: the coverage table for #26-#39 (criteria -> test, gaps marked) and the device checklist with blank results. Verify every M2 issue has a section.
- [x] 6.2 Update `README.md` and `.planning/code-standards.md` or `.planning/constraints.md` if the checks or commands changed. Verify the documented command runs as written.

## 7. Integration checks

- [x] 7.1 Close the Editor gracefully and run `./Tools/run-tests.ps1` green, then with `-Repeat 10`; verify no flaky test, PlayMode under 3 minutes and no console error or warning (parse the junit XML). Accepted by the owner on 2026-10-07 without meeting every target: one full run gave PlayMode 308/308 in 281 s and EditMode 556/557 (`CountUpTests` allocation flake); `-Repeat 10` was stopped during run 1. See `.planning/m2-signoff.md`.

## Workflow follow-up

- Fill in the device results in `.planning/m2-signoff.md` and attach it to the PR.
- Archive the change with `/opsx:archive` after the PR is reviewed.
