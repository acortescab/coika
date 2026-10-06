# Proposal

## Why

M2 exits only when the game holds 60 FPS with zero per-frame GC on a mid-range Android phone (issue #39). Low-end devices need a cheaper render path, the Android build still defaults to Medium quality with HDR on, and nothing guards the full M2 feature set against allocations or lets a developer see performance on-device.

## What Changes

- Add a `QualityTier` service in `Coika.Core` that classifies the device as `Normal` or `Low` (`processorCount < 4` or `systemMemorySize < 3000` MB) and exposes read-only `Level`, `ParticleCountMultiplier` and `PostProcessingEnabled`. No Settings option.
- Inject it into scenes through a new `IQualityConsumer`, the same way `GameInstaller` injects haptics and audio.
- Add a UI-layer `QualityApplier` that drives `ParticleSpawner.CountMultiplier` and enables or disables the Global Volume (post-processing) from the tier.
- Add a development-only IMGUI debug overlay (FPS, frame time, GC allocation count, piece count, quality tier), compiled out of release builds.
- Set the Android default quality to Very Low and turn HDR and MSAA off in the URP asset; confirm the camera flags.
- Add tests: classifier boundaries (EditMode), service and applier (PlayMode), and a full scripted run with all M2 systems active that must allocate nothing.
- Fix any per-frame allocations the new test exposes.
- Update README, GDD §14.5, LEARNINGS, and add a device checklist for the measurements that need a real phone (profiling, screenshots, battery, logcat, Addressables leak check). Those criteria stay unchecked.

## Capabilities

### New Capabilities
- `quality-tier`: device classification into Normal/Low, injection into scenes, and the effect of Low on post-processing and particle counts.
- `debug-overlay`: development-only on-screen performance readout, absent from release builds.
- `render-quality-config`: project-level quality settings for mobile (Very Low default on Android, no HDR, no AA).

### Modified Capabilities

None: `openspec/specs/` has no existing capabilities.

## Impact

- Code: `Assets/Scripts/Core/Quality/` (new), `Core/Boot/GameInstaller.cs`, `UI/GameSceneInstaller.cs`, new `UI` applier and overlay, `Coika.UI.asmdef` (URP Core reference), `Fx/ParticleSpawner.cs` consumers.
- Assets: `GameScene.unity` (Volume reference wiring), `ProjectSettings/QualitySettings.asset`, `Assets/Settings/UniversalRP.asset`.
- Tests: EditMode and PlayMode additions, PlayMode fakes for system info.
- Docs: `README.md`, `.planning/gdd.md`, `.planning/LEARNINGS.md`.
- Out of scope: thermal mitigation, iOS, shader optimisation, new Settings option, claiming device criteria without a device.
