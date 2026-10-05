# Learnings

Non-obvious things learned while working on the project. Add a dated section per issue; keep entries short and say why.

## Issue #31: piece animations (2026-10)

### Design
- **Animations are visual only.** The Piece prefab has a `Sprite` child that holds the `SpriteRenderer` and `PieceAnimator`; only that child's scale changes. The root scale, rigidbody and collider never change, because `MergeSystem` reads `lossyScale` and the golden simulation must not move.
- **Effects, not flags.** One `PieceEffect` class per animation, a list in `PieceAnimator`, a scale factor per effect multiplied together. Effects of the same `PieceEffectGroup` replace each other (Pop: spawn/merge pop; Contact: drop stretch/landing). A new animation is a class, an enum value and one line in `CreateEffects`. A first version with a flag, elapsed time and duration per animation was rejected as unscalable.
- **Merge sources cannot animate.** `MergeSystem.Resolve` releases the two pieces in the same step, so `MergeSystem.PairMerging` is raised before the release and `MergeGhostPool` shows prewarmed visual-only clones that shrink. The ghost ring is fixed size and recycles the oldest, so nothing is instantiated during play.
- **Config reference.** `GameConfig.Feedback` is a direct reference to `FeedbackConfig`. It is allowed by C-01 because both assets are in the `Core-Data` group. Tuning values (including the rebound ratio) live in `FeedbackConfig` (S-30); only structural constants stay in code.
- **`Piece.Landed(piece, impulse)`** is raised from `OnCollisionEnter2D` for any contact of a piece in play (floor, wall, piece), with the summed `normalImpulse`. Filtering by threshold is the listener's job (#34 will reuse it). Do not use `OnCollisionStay2D` for it.
- **Reset on release and reuse.** `PieceFactory.ClearSubscribers` clears `Landed` and resets the animator when a piece returns to the pool, and `Piece.Initialize` calls `Animator.Begin`. Both are needed for "every pooled piece is at scale 1".

### Testing
- **Harness ticks animators itself.** `SimulationWorld.Step()` runs synchronously and never reaches `Update`, so it calls `PieceAnimator.Tick` and `MergeGhostPool.Tick`. `SimulationOptions.Animations` turns them off, which gives the "identical physics with animations on and off" test.
- **Settle time.** A dropped piece needs about 1 s to fall from the Drop Line. Tests that check that animations ended need roughly 4 s after the last drop, or the falling piece is still stretching.
- **Merge in the harness.** Two drops at the same X with `ForcedOpening = {0, 0, 0}` merge.
- **`AllocationMeter` is global noise.** The Profiler counter counts allocations of every Editor thread, so `RegisterMergeAndTick_InSteadyState_AllocateNothing` failed intermittently (24 to 430) even on a clean `main`. Use `MeasureLowest` (discarded warm-up, lowest of 5) and, for loops, bound the result by a fraction of the iterations instead of the fixed tolerance of 20.
- **Allocation tests are unreliable in isolation.** Run alone with a filter on a cold Editor, `MergePairQueueTests` and `ComboTrackerTests` allocation tests report hundreds to thousands of allocations (also on a clean `main`), while the full suite passes. Judge them by full `./Tools/run-tests.ps1` runs, and use `MeasureLowest` with a bound proportional to the loop for steady-state loops.
- **Removing a rule means removing its tests.** Dropping the root grow-in (`DropFlow.ScaleFactor`, `GameConfig.ScaleInDuration`) made `DropFlowTests`, `GameConfigDropTests` and one `DropControllerTests` case obsolete; the last one became "the controller keeps the root at full size".

### Tooling on Windows
- **Unity.exe from PowerShell returns at once.** Use `Start-Process ... -Wait -PassThru` to run `-batchmode -executeMethod` and read the exit code. The Editor must be closed first.
- **Editor menu tools in batch mode.** `-executeMethod Coika.Tools.FeedbackConfigSetup.Run` and `Coika.Tools.PiecePrefabTool.Run` create the asset and rebuild the prefab. The licensing "Access token unavailable" lines in the log are harmless.
- **`git stash -u` and the Editor.** Running Unity while stashed rewrites `UiFont SDF.asset` and `ProjectSettings.asset`, which makes `git stash pop` refuse. `git checkout --` those two files, then pop. Check the stash is identical (`git diff 'stash@{0}'`) before dropping it.
- **PowerShell helper names.** Do not name a function `R` (it is the alias of `Invoke-History`). Preserve CRLF and BOM when rewriting files with `[IO.File]`.
- **Closing the Editor.** `Process.CloseMainWindow()` closes it gracefully; it exits without a prompt when nothing is unsaved.

### Working agreement
- The user dislikes repeated per-case blocks; ask "would the 101st case need an edit here?" before writing the second copy.
- Commit and push only when asked; keep the Editor-noise files unstaged; separate unrelated fixes (test flake, compiler warning) into their own commits.
