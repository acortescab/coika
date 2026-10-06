# Design

## Context

See proposal.md for motivation. Constraints observed in the code:

- `GameInstaller` (Boot) is the composition root with no DI container. It builds services in `Start()` and `HandleSceneLoaded` injects them into scene objects that implement `I*Consumer` interfaces (haptics and audio are the template).
- `Coika.Core` cannot reference URP types (layer rule `UI -> Gameplay -> Core`), so the Volume toggle must live in `Coika.UI`.
- `ParticleSpawner.CountMultiplier` already exists as the hook for this issue; `GameSceneInstaller.ApplyReduceMotion()` is the current applier pattern.
- Global Volume is a scene object in `GameScene.unity`. `Coika.UI.asmdef` does not reference `Unity.RenderPipelines.Core.Runtime`.
- `QualitySettings.asset`: Android default is index 2 (Medium); `UniversalRP.asset` has HDR on and one URP asset serves all levels.
- C-01 forbids `Resources.Load` and `Addressables.*` outside the services; C-02 requires XML docs; gameplay is deterministic (S-63).
- `AllocationMeter` (Profiler counter) is the only valid allocation measure; the harness (`SimulationWorld`, `SimulationRunner`) steps synchronously and fails on any warning.

## Goals / Non-Goals

**Goals:**
- Tier logic that is pure and unit-testable with plain numbers.
- Same injection and testing patterns as haptics and audio.
- Zero allocation cost from the overlay and the tier code.

**Non-Goals:**
- A runtime tier switch, a Settings option, or per-device tuning tables.
- Replacing the URP asset per quality level.
- Producing device measurements; those are a checklist for the owner.

## Decisions

- **Pure classifier plus provider.** `QualityClassifier.Classify(processorCount, memoryMb)` holds the thresholds; `ISystemInfo`/`SystemInfoProvider` wraps `SystemInfo`. Alternative: fake `SystemInfo` statics, impossible. This keeps boundary tests in EditMode.
- **Service exposes derived values.** `IQualityTier` offers `Level`, `ParticleCountMultiplier` (Low = 0.5, Normal = 1) and `PostProcessingEnabled`, so Core never touches URP or `Fx`. The multiplier composes with the existing Reduce Shake factor in `ParticleSpawner.ScaleCount`.
- **Consumer injection.** `IQualityConsumer.UseQuality(IQualityTier)` is called from `HandleSceneLoaded`, like `UseHaptics`. Alternative: static access, rejected by the no-singletons rule.
- **UI applier.** `QualityApplier` in `Coika.UI` takes the tier, the `ParticleSpawner` and a serialized `Volume`, and sets `CountMultiplier` and `volume.enabled`. `GameSceneInstaller` calls it beside `ApplyReduceMotion()`. Alternative: put the logic inside `GameSceneInstaller`; rejected to keep it testable. Adds the URP Core assembly reference to `Coika.UI.asmdef`.
- **Debug overlay as IMGUI.** One `DebugOverlay` MonoBehaviour, whole file inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, attached with `AddComponent` by `GameSceneInstaller` (also inside the `#if`). No asset, so C-01 is untouched. FPS and frame time come from smoothed `Time.unscaledDeltaTime`, GC count from a `ProfilerRecorder` on "GC Allocation In Frame Count", piece count from the active pieces, tier from the service. Text is rebuilt about twice per second into a reused `StringBuilder`/cached strings; `OnGUI` draws the cached string. Alternative uGUI prefab: needs an Addressables entry and release stripping.
- **Quality config.** Set `m_PerPlatformDefaultQuality` Android to 0, HDR off and MSAA 1 (off) in the shared `UniversalRP.asset`, and check the camera's HDR/AA overrides. Alternative: a dedicated Very Low URP asset; rejected as more config for no benefit in a 2D game.
- **Allocation guard.** A PlayMode test builds a `SimulationWorld` with `Animations`, `Particles` and `Feedback` on, runs a warm-up pass, then measures only stepping (`SimulationRunner.Advance`, no snapshot) in several windows with `AllocationMeter`. Finding during apply: the Editor makes sporadic allocations of its own (25 to 400 per 3600 steps with every game system off), so an exact zero over a long run is not measurable there. The test therefore asserts that the median window allocates under 0.1 per physics step, which any per-frame leak breaks; the strict zero is verified on a device with the Profiler (device checklist).

## Risks / Trade-offs

- [Allocation test exposes existing allocations in input, restart or feedback paths] → fix at the source; keep the diff scoped and note it in LEARNINGS.
- [Scene wiring of the Volume needs a live Editor] → use `unity command` eval, then close the Editor again per CLAUDE.md.
- [HDR/MSAA change alters visuals] → 2D pixel-art rendering; verify in the Editor and on-device checklist screenshots.
- [Device criteria cannot be verified here] → leave them unchecked and listed in the device checklist; never claim 60 FPS from the Editor.
- [Cold-Editor allocation flake] → rerun once before investigating.
