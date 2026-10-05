# Learnings

Non-obvious things learned while working on the project. Add a dated section per issue; keep entries short and say why.

## Issue #35: pause (2026-10)

- **The panels follow `GameState`, not the other way round.** `GameSceneInstaller.HandleStateChanged` opens the `PausePresenter` on `Paused` and closes it on leaving it, so Resume, a confirmed Restart (`StartRun`) and the debug keys all close the menu without each knowing about it. `GameManager.Pause()/Resume()` only change the state; time, music and input react to the state change.
- **`PanelStack` + `IPanel` instead of a flag per screen.** `PauseView` and `ConfirmView` are panels; the presenter pushes the dialog over the menu and Back pops the top one (menu alone: asks to resume). The Settings screen (#36) is one more `IPanel`, and Reset progress (M3) is one more `ConfirmRequest` (two string keys), with no new view or branch.
- **Strings:** there is no `Loc.Get`. Static labels are baked into the prefab by `GameCanvasPrefabTool`; the dialog's title and message are set at runtime from `UiTextKeys` (runtime) whose English texts live in `UiStrings` (Editor). After adding keys run Coika > Setup Localization, then Build Game Canvas Prefab.
- **UI sounds are played by the presenter**, not the views (`UiClick`, `UiBack` through `IAudioService`; null audio is silent), so views stay dumb.
- **No drop on resume = `DropController.BlockInput(seconds)` called on pause.** It forgets the press in progress and ignores input for the time, counted by `Tick(deltaTime)`: at timeScale 0 the delta is 0, so the block only runs down after the resume, which is the grace period. A press that starts over the UI never drops anyway (#27); this covers a finger held across the pause and the press right after the tap.
- **Clocks need no change.** `Piece`, `OverflowDetector`, `RunSystems` and the cooldown read scaled time, so they freeze at timeScale 0 and the run's "time played" and the combo windows exclude the pause. Do not switch them to unscaled.
- **Auto-pause:** `GameSceneInstaller.PauseForInterruption()` (public for tests) pauses only while `Playing`, then saves at once. Both `OnApplicationPause(true)` and `OnApplicationFocus(false)` call it.
- **Tooling:** `ExecuteMenuItem` right after a refresh can run the old code, because the domain reload is not finished; probe with `Type.GetType(...)` before running the Coika menu tools. NUnit reuses the fixture instance, so counters that tests increment must be reset in `SetUp`.

## Issue #34: FeedbackDirector (2026-10)

`FxDirector` and `ScreenFxDirector` (sections #32 and #33 below) were merged into `FeedbackDirector`; what those sections say about them now applies to it.

### Design
- **`FeedbackConfig` is four groups, not 50 fields.** `Animations` (`PieceAnimationSettings`), `Particles` (`ParticleSettings`), `ScreenFx` (`ScreenFxSettings`) and `Sound` (`FeedbackSoundSettings`) are `[Serializable]` classes (not structs: a struct would not run the field initializers that hold the `[TUNE]` defaults), each with its own pure helpers. A new value goes in its group; callers read `config.Sound.DropPitch`. Tests that set a private field by reflection target the group (`SetField(config.Animations, "_landImpulseThreshold", ...)`). The shipped asset kept only the #31 values, all equal to the defaults, so the old keys were removed and Unity fills the groups from the initializers; save the asset in the Editor once to write them out.
- **One class maps every event to particles, screen effects, sound and haptics.** The numbers are in `FeedbackConfig` (new `[TUNE]` fields: Heavy haptic tier 7, land volume and pitch, drop pitch, danger tick rates, game-over sweep) and the formulas are pure helpers on it (`MergeHaptic`, `LandVolume`, `LandPitch`) plus `AudioMath.MergePitch`, so they are tested in EditMode and the director stays a thin switch. Audio and haptics may be null (tests, no audio device): it then skips them.
- **The Heavy haptic tier (7) is not the shake tier (8).** The GDD asks for both; they are two config fields.
- **Merge sound and haptic wait for `Tick()`.** `Merged` handlers fire the visuals at once but only keep the highest tier; `Tick()` (installer `Update`, harness end of `Step`) plays one pop and one haptic, so a chain is one bundle. The combo step is `Combo - 1`, read in `Tick`, which is why the director binds after `new RunSystems(...)`: the score must have registered the merge. Tier >= 8 layers `MergeBig` over the pop.
- **Events added on their owners:** `DropController.PieceSpawned(tier)` is raised only after a drop (not at run start, Retry or resume, so no bloop there); `OverflowDetector.DangerChanged(bool)` comes from `ShowDanger` and `ClearState`. The danger tick is a loop in `Tick()` on the injected unscaled clock, 4 Hz or 2 Hz with Reduce Shake.
- **Silence is a state, not a flag per effect.** `_playing` follows `GameManager.StateChanged`; paused or over, every handler returns early (except `RunEnded`, which plays the game over). Pause clears the shake, flash and slow-mo; `RunStarted` forgets held merges and the danger loop. The music duck stays in `GameSceneInstaller.HandleRunEnded`: `GameOver_WithAudio_DucksTheMusicAndRetryRestoresIt` expects exactly one.
- **The game-over flash is a piece effect that tints.** The flash happens on each piece, so `GameOverFlashEffect` is a `PieceEffect` (id `GameOverFlash`, own `Flash` group so it excludes nothing); the parameter of `Play` is its delay, which the director takes from `PieceAnimationSettings.GameOverDelay` by height (top first). To allow it, `PieceEffect` got a virtual `Tint` (white by default) and `PieceAnimator` multiplies the tints of the active effects into the sprite colour and resets it to white at rest, so `Begin`/`ResetState` of a reused pooled piece also clears a flash that was cut short. The animator now changes the scale and the sprite colour, never the body. A first version scheduled the tint from a `GameOverSweep` class in the director; it was dropped because the animator already owns per-piece time and rest state.
- **Land thuds fire on any contact of a piece in play,** including the two overlapping pieces of a fresh merge. Tests that look at merge sounds set `_landImpulseThreshold` to `float.MaxValue` first.

### Tests
- `SimulationOptions.Feedback` (on by default) builds the director with `FakeAudioService`, `FakeHaptics`, `FakeScreenEffects` and `FakeParticleSpawner` (recording, used when the real particles are off); the world calls `Feedback.Tick()` at the end of each `Step`. `FeedbackSimulationPlayModeTests` proves the result with and without it is identical, so the golden file does not change.
- To test the danger tick without stepping physics (`Overflow.Evaluate` resets the danger every 0.1 s), a test binds its own director on a clock it moves by hand and raises `DangerChanged` through `TestReflection.GetField`.
- The Retry lifecycle checks (`RunLifecycleHarnessPlayModeTests.Describe`, `GameLoopPlayModeTests.CountSubscribers`) now also count `PieceSpawned`, `DangerChanged` and the `Landed` listeners of the active pieces.

## Issue #33: screen shake, slow-mo and flash (2026-10)

### Design
- **Plain cores, thin MonoBehaviours.** `ShakeCore` (slots, linear fade, per-axis cap, snap to 1/16, seeded `System.Random`), `FlashCore` (fade and the 3 Hz limit) and `TimeScaleOwner` hold the maths and the state; `ScreenShake` and `ScreenFlash` only apply them in `LateUpdate`. All read an injected unscaled `Func<double>`, so tests drive them with a fake clock and never touch `Time`. `ScreenFxDirector` decides when (merge tier >= `HeavyMergeMinTier`, supernova); like `FxDirector` it uses cached delegates and an idempotent `Bind`.
- **The shake moves a parent rig, not the camera.** `CameraRig` sits at the origin with the main camera as its child; `JarCameraFramer` still writes the camera's own position once in `Start`, so framing is untouched. `ScreenShake` remembers the rig's rest position in `Initialize` and writes `rest + offset`; with no shake the position is exactly the rest value. Do not call `Frame()` in the middle of a shake.
- **`TimeScaleOwner` is the only writer of `Time.timeScale`:** 0 when paused, 0.7 during a slow-mo, 1 otherwise. A slow-mo is refused while paused and pausing cancels it, so after Resume the scale is 1. The installer sets `Paused` from `GameState` changes, so #35 only has to enter `GameState.Paused`. The slow-mo ends on unscaled time; the installer's `Update` ticks it.
- **Pause and run start stop everything.** Shake and flash run on unscaled time, so they would keep moving a paused game; `ScreenFxDirector.StopAll()` clears them (and the slow-mo) on `GameState.Paused` and on every run start. The installer resolves the shake and flash once as interfaces (`NullScreenEffects` when the scene has none), and checks Unity objects with `!= null`, never `?.`.
- **Slow-mo does stretch gameplay clocks a little.** `ScoreSystem` and the run timer read scaled time, so a 0.1 s slow-mo at 0.7 adds about 0.03 s to the combo window. Accepted: the issue asks for it and the effect is tiny. Physics steps are fixed-size; the harness steps them synchronously with its own simulated clock, so `timeScale` cannot change a simulation result and the golden file is untouched.
- **Reduce Shake** turns off shake and slow-mo, soft-pulses the Danger Line at 2 Hz (`SetPulseRate(Soft)` from `ApplyReduceMotion`, re-applied on run start) and still halves particles. The supernova flash stays on: it is a single 0.15 s event, always under the 3 Hz limit.
- **Tier numbers are indices.** `Merged` reports the index of the created tier (0-10), so `tier >= 8` and the amplitude `0.05 x (tier - 7)` are written as `HeavyMergeMinTier` and `MergeShakeAmplitude`. A supernova raises `SupernovaTriggered`, not `Merged`.
- **Flash limit by ignoring.** A request less than 1/3 s after the previous accepted one is dropped, so two overlapping flashes never add up.

### Testing
- **Cores are EditMode tests** (`ShakeCoreTests`, `FlashCoreTests`, `TimeScaleOwnerTests`) with a fake clock and a fake `ITimeScale`; none touch the real time scale, because a leaked `timeScale` would slow every later test. The director is tested in PlayMode with a recording fake and real merges (`MergeFresh(createdTier - 1)`).
- **Pulse frequency** is tested by sampling `DangerLine.EvaluateColor` over a second of fake time and counting peaks (4 fast, 2 soft).

### Tooling
- **Setup tool.** `Coika/Setup Screen Effects` (`ScreenFxPrefabTool.Run`) builds the `ScreenFlash` prefab (overlay canvas, `CanvasGroup`, one white image that ignores raycasts), registers it in `FX`, adds the `CameraRig`, moves the main camera under it and wires `_cameraShake` and `_screenFlashPrefab` in the Game scene.

## Issue #32: pooled particles (2026-10)

### Design
- **Two shared systems, not one prefab per effect.** `ParticleSpawner` owns one `ParticleSystem` for pixel particles (merge burst, dust, confetti) and one for rings (flash, supernova flash, shockwave), emitted with `Emit(EmitParams, count)`. `EmitParams` is a struct, so a burst allocates nothing. Rings are single big particles; the prefab's size-over-lifetime and colour-over-lifetime curves make them grow and fade. A ring needs its own texture, hence the second system.
- **The cap is `maxParticles`.** Unity removes the oldest particles when the cap is reached, which is the "oldest-first recycling" of the issue. `Initialize` sets the caps from `FeedbackConfig` and emits each system full once, then clears it, so buffers exist before play. There is no per-effect pool to grow, so the pool-growth warning cannot fire; the harness base class still fails the tests on any warning.
- **`IParticleSpawner` is the test seam.** `FxDirector` (plain class) decides what to emit from `MergeSystem.Merged`, `SupernovaTriggered`, `Piece.Landed` and `ScoreSystem.NewBestReached`; tests use a recording spawner for tier colours, counts and positions, and the real spawner for caps and allocations.
- **Landing is hooked per piece.** `Piece.Landed` is wiped when the factory releases a piece, so `FxDirector` subscribes in `PieceFactory.PieceCreated` (like `MergeSystem.HookPiece`) and to the pieces already active.
- **`Coika.Fx` is its own assembly** between Gameplay and UI (`UI -> Fx -> Gameplay -> Core`), because it listens to Gameplay events. `GameSceneInstaller` loads the prefab through `IAssetService`, builds the director and releases both in `TearDown`. The prefab reference is optional, like the ghosts.
- **Particle randomness lives in Unity.** Spread and speed come from the particle system's own random ranges, so there is no `UnityEngine.Random` or `System.Random` in our code (S-63) and nothing the golden simulation could see.
- **Reduce Shake and quality.** `ParticleSpawner.ReduceMotion` (set from `SettingKey.ReduceShake`) multiplies counts by `ReduceMotionCountFactor` (0.5); rings stay. `CountMultiplier` is the hook #39 can drive for low-end devices. Issue #33 adds the shake and slow-mo, which the same setting disables.
- **Tuning is data.** Counts, sizes, lifetimes and caps are `FeedbackConfig` fields.

### Testing
- **`SimulationOptions.Particles`** (on by default) adds a real spawner and director to `SimulationWorld`; the golden test therefore runs with particles on and is unchanged. `ParticleSimulationPlayModeTests` proves identical physics and score with particles on and off. `SimulationWorld.TickAnimations` advances the particle systems with `ParticleSystem.Simulate(dt, false, false, false)`, because no frame passes in the harness; without it live counts would only grow. `SimulationOptions.ParticleCap` and `RingCap` lower the caps so a 10-merge chain can reach them (`MergeScenario.MergeTwo` scripts a merge).
- **Event handlers are measured through their backing delegates.** `FxDirectorPlayModeTests.Handlers_CalledManyTimes_AllocateNothing` reads the field-like events (`Merged`, `Landed`, ...) with `TestReflection.GetField` and invokes them 1000 times, because a test cannot raise another class's event. Unbind the recording director first, or its list allocates.
- **Cap tests need a reachable cap.** Unity enforces `maxParticles`, so "never above the cap" always passes; assert that the cap is reached too.
- **`TestParticleSpawner.Create`** builds the two systems in code, so tests need no Addressables.

### Tooling
- **Setup tool.** `Coika/Setup Particles` (`FxPrefabTool.Run`) writes the textures, materials and prefab, registers them in `FX` and wires `_fxPrefab` in the Game scene. Run it in batch mode with `-executeMethod Coika.Tools.FxPrefabTool.Run`; `Start-Process -Wait` can return before the Editor has finished, so check that the Unity process is gone before running tests.
- **New asmdefs need a `.meta`.** Unity creates it on the first import; commit it with the asmdef.

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
