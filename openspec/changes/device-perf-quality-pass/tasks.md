# Tasks

## 1. Branch and quality config

- [x] 1.1 `git fetch`, create `feature/39-device-perf-pass` from `origin/main`; verify with `git branch --show-current`
- [x] 1.2 Set Android default quality to 0, HDR off and MSAA off in `UniversalRP.asset`, check the Main Camera HDR/AA flags (grep the scene); verify the values by grep and note any camera override fixed

## 2. Core quality tier

- [x] 2.1 Add `QualityLevel`, `QualityClassifier`, `ISystemInfo`, `SystemInfoProvider` in `Assets/Scripts/Core/Quality/` with C-02 XML docs; verify EditMode `QualityClassifier` tests pass (2 cores, 2999 MB, 4 cores and 3000 MB boundary, Normal)
- [x] 2.2 Add `IQualityTier`, `QualityTierService`, `IQualityConsumer`; verify EditMode tests for `Level`, `ParticleCountMultiplier` (Low 0.5, Normal 1) and `PostProcessingEnabled` using a fake `ISystemInfo`
- [x] 2.3 Build the service in `GameInstaller.Start`, expose a property and inject in `HandleSceneLoaded`; verify with a PlayMode/EditMode test that a consumer receives the tier before `Start`

## 3. UI applier and Volume

- [x] 3.1 Add `Unity.RenderPipelines.Core.Runtime` to `Coika.UI.asmdef`; verify the project compiles
- [x] 3.2 Add `QualityApplier` (UI) setting `ParticleSpawner.CountMultiplier` and `Volume.enabled`; call it from `GameSceneInstaller` beside `ApplyReduceMotion()` and implement `IQualityConsumer`; verify PlayMode tests: Low disables the Volume and multiplier < 1, Normal keeps both at full
- [x] 3.3 Wire the Global Volume reference in `GameScene` via `unity command` (reopen Editor, close it afterwards); verify by grep that the field is assigned and by a PlayMode scene test

## 4. Debug overlay

- [x] 4.1 Add `DebugOverlay` (IMGUI, whole file in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, cached text at about 2 Hz, `ProfilerRecorder` GC counter) and attach it from `GameSceneInstaller` inside the same `#if`; verify a PlayMode test shows the five values and that the overlay causes no allocations via `AllocationMeter`
- [x] 4.2 Verify release exclusion: grep that every overlay reference is inside the `#if`, and compile the player scripts without `DEVELOPMENT_BUILD` (or inspect a release build log)

## 5. Allocation guard

- [x] 5.1 Add the PlayMode full-run allocation test (`Animations`, `Particles`, `Feedback` on, warm-up then measured windows, median under 0.1 allocations per step because of Editor noise); verify it passes twice
- [x] 5.2 Fix any game allocations the test exposes at the source (none found; the rest is Editor noise, see design.md); verify the test and the existing allocation tests still pass and the golden file is unchanged (`simulation-seed-1234.txt`)

## 6. Docs and device checklist

- [x] 6.1 Add a dated #39 section to `.planning/LEARNINGS.md`; update `README.md` and GDD §14.5; verify by grep that the tier, overlay and Very Low default are described
- [x] 6.2 Add a device checklist document with the profiling runs, 3-resolution screenshots, battery and temperature, `adb logcat` and Addressables 10-run leak check, all unchecked; verify the file exists and states that no device was available

## 7. Integration

- [x] 7.1 Run `./Tools/run-tests.ps1` (Editor closed) and verify EditMode and PlayMode pass, C-01 grep checks pass, and the junit XML has no failures
- [x] 7.2 Verify the unstaged noise files (`UiFont SDF.asset`, `ProjectSettings.asset`, iet-framework `Settings.json`) are not staged and that only intended files changed

## Workflow follow-up

- Archive the change with `/opsx:archive` after review and merge.
- Device-only acceptance criteria (60 FPS, zero GC on device, rigidbody and draw-call counts, pixel screenshots, logcat, leak check) remain open until measured on a phone.
