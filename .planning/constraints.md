# Technical Constraints

Binding technical rules for Coika. Every issue, PR and design decision must respect them. If a constraint blocks a task, raise it before working around it. Changes to this file need explicit approval from the project owner.

Each constraint has an ID (`C-NN`) so issues and PRs can reference it (e.g. "Complies with C-01").

---

## C-01 — Addressables are mandatory (scenes and assets)

**Status:** Mandatory · **Added:** 2026-10-01 · **Applies to:** all milestones

### Rule
1. **All scenes are loaded through Addressables** (Addressable Scenes). `UnityEngine.SceneManagement.SceneManager.LoadScene*` must not be used to load gameplay or UI scenes.
2. **All content that is not needed to boot the app is Addressable** and loaded on demand, to keep the **initial install size as small as possible**.
3. Direct references to heavy assets from code or from a scene that is part of the initial install are not allowed (see "Allowed in the initial build" below).

### Goal
Reduce the size of the initial app download. Content is split into small, purposeful groups so that only what the player needs for the current screen is present or fetched.

### Allowed in the initial build (not Addressable)
Only the minimum required to start the app and reach the first Addressable load:
- A single tiny **bootstrap scene** (`Boot`) containing the bootstrapper that initializes Addressables and loads the first Addressable scene.
- The Addressables runtime, Unity splash/logo settings, and the app icon.
- Anything under `Resources/` is **forbidden** (it always ships in the initial build and defeats this constraint).

Everything else, including the `Menu` and `Game` scenes, tiers, sprites, audio, fonts, VFX, UI prefabs and themes, is Addressable.

### Required setup
- Package: `com.unity.addressables` (installed via the Package Manager client API, per the project's package workflow; never edit `manifest.json` by hand).
- **Addressable Groups** (suggested; adjust with approval):

  | Group | Content | Notes |
  |---|---|---|
  | `Scenes` | `Menu`, `Game` | One entry per scene; marked as scenes. |
  | `Core-Data` | `GameConfig`, `TierDefinition`s, `ThemeDefinition` | Small, always loaded with the `Game` scene. |
  | `Theme-<Name>` | Tier sprites, background, jar art, theme music | One group per theme/skin so skins are downloaded only when selected (GDD §12, M4). |
  | `Audio-Music` / `Audio-Sfx` | Music tracks, SFX clips | Music is large: keep it out of the initial build. |
  | `UI` | UI prefabs, fonts, TMP assets | |
  | `Fx` | Particle prefabs, VFX sprites | |

- **Local vs Remote:** content shipped inside the store build uses the **Local** build/load path. The architecture must allow switching a group to **Remote** (CDN) later without code changes. Remote hosting itself is out of scope until approved (it also requires the `INTERNET` permission, which conflicts with GDD §17 "no network permission" — decide explicitly before enabling).
- **Android:** use **Play Asset Delivery** (install-time / fast-follow / on-demand asset packs) for groups that should not count toward the base APK/AAB limit, via the Addressables Android asset-pack support. **iOS:** use **On-Demand Resources** or the Addressables local path as appropriate.
- Addressables settings and group assets are committed to git (`Assets/AddressableAssetsData/`). Built bundles (`ServerData/`, `Library/com.unity.addressables/`) are git-ignored.

### Code rules
- Introduce a single `AssetService` (in `Coika.Core`) that wraps Addressables: load, release, preload, progress, error handling. Gameplay and UI code request assets through it and never call `Addressables.*` directly.
  - **Only exception:** `GameInstaller` (the composition root in `Boot`) may call `Addressables.InitializeAsync()`, because initialization is a boot step and not an asset load. No retry is applied to it.
- Reference assets with `AssetReference` / `AssetReferenceT<T>` / `AssetReferenceSprite` or Addressable labels, **never** with direct serialized object references to assets that live in a different group or in the initial build.
  - Exception: assets inside the **same** group/scene bundle may reference each other directly.
- **Every load has a matching release.** Hold `AsyncOperationHandle`s, release them when the owner is destroyed or the scene unloads. Leaks are bugs.
- Scenes: load with `Addressables.LoadSceneAsync` (additive where useful), unload with `Addressables.UnloadSceneAsync`. Provide a `SceneLoader` wrapper with a loading-progress callback for a loading indicator.
- Pooled prefabs (`PieceFactory`, VFX, UI items) are instantiated from Addressable references during a load/preload phase, **not** inside gameplay frames (no hitches, no GC during play).
- All asynchronous loads are awaited or callback-driven with **error handling and a retry path**; a failed load shows a recoverable error state, never a crash or an infinite spinner.
- Use **labels** to preload sets (e.g. `core`, `theme-cosmic`, `tier-sprites`) instead of long lists of individual keys.

### Impact on the existing M1 plan
These issues must be updated or read with this constraint in mind:

| Issue | Required change |
|---|---|
| #1 Project foundation | Add the Addressables package and initial groups; `Game` scene is Addressable; **`Boot` scene is the only scene in Build Settings** (replaces "`Game.unity` is the only scene in Build Settings"). Add `AssetService`/`SceneLoader` skeletons. |
| #2 Tier data | `TierDefinition`/`ThemeDefinition` assets and tier sprites go in Addressable groups (`Core-Data`, `Theme-Cosmic`). Sprite fields use `AssetReferenceSprite`. |
| #4 Piece / PieceFactory | The `Piece` prefab is Addressable; the pool pre-warms from an `AssetReference` during load, not on first use. |
| #10 HUD | The `GameCanvas` prefab and font assets are Addressable (`UI` group). |
| #11 GameManager / installer | Starts from `Boot`, initializes Addressables, loads `Game` through `SceneLoader`, and releases handles on exit/restart. |
| #12 Tests | Provide test helpers/doubles for `AssetService` so unit tests don't require built bundles; add a PlayMode test that loads the `Game` scene through Addressables in **Use Asset Database** play mode. |

### Verification (acceptance checks)
- [ ] Build Settings contain **only** the `Boot` scene. No other scene is listed.
- [ ] No usage of `SceneManager.LoadScene`/`LoadSceneAsync` outside `Boot` bootstrap/`SceneLoader` (grep check in review).
- [ ] No `Resources/` folder or `Resources.Load` usage (grep check).
- [ ] No direct `Addressables.*` calls outside `AssetService`/`SceneLoader` (grep check). Only exception: `Addressables.InitializeAsync()` in `GameInstaller`.
- [ ] **Initial size budget:** the base Android AAB/APK and iOS install contain only the Boot scene + runtime; record the build size in the PR. Target budget: **base build ≤ 30 MB** for M1–M2 [TUNE: confirm after the first Android build].
- [ ] Addressables **Analyze** rules (Check Duplicate Bundle Dependencies, Check Resources to Addressable Duplicate Dependencies) report no fixable issues.
- [ ] Group sizes are reviewed in the **Addressables Report**; no single group exceeds the agreed size without justification.
- [ ] Handle leak check: after returning to `Menu` and starting 10 consecutive runs, the Addressables Event Viewer shows no growing ref-counts.
- [ ] Both **Play Mode Scripts** are exercised: *Use Asset Database* (fast iteration) and *Use Existing Build* (before every milestone sign-off).

### Risks and notes
- Async loading changes boot flow: add a minimal loading indicator and never assume an asset is synchronously available.
- Content builds must be rebuilt (`Addressables → Build → New Build → Default Build Script`) before player builds; automate this in the build script.
- Mixing Addressable groups with direct references can silently **duplicate assets across bundles** and increase size; rely on the Analyze rules above.
- `Time.timeScale`/physics freezing logic (issue #11) must not depend on a scene reload.

---

## C-02 — Classes and methods must be commented

**Status:** Mandatory · **Added:** 2026-10-01 · **Applies to:** all milestones, all code under `Assets/Scripts/` and `Assets/Tests/`

### Rule
1. Every type (`class`, `struct`, `interface`, `enum`, `record`) has an XML documentation comment (`/// <summary>`) that says **what it is for** and, when it is not obvious, how it is meant to be used.
2. Every method has an XML documentation comment (`/// <summary>`), including private ones.
   - Public and internal methods also document non-obvious `<param>` values, the `<returns>` value and every exception they throw on purpose (`<exception>`).
   - A one-line `<summary>` is enough for trivial private methods and Unity messages (`Awake`, `Start`, `OnValidate`, ...).
3. Comments explain **intent, contract and constraints** (the why), not a restatement of the name. `/// Loads the asset` on `LoadAsset` does not comply.
4. Comments are kept in sync with the code: a change to behavior, parameters or exceptions updates the comment in the same commit. A comment that contradicts the code is a bug.
5. Language: English (as the rest of the code).

### Goal
Make the code understandable without opening every caller, keep the contracts of the services (`AssetService`, `SceneLoader`, ...) explicit, and make reviews faster.

### Impact on existing issues
Applies to every issue from now on. Existing code that lacks comments is brought into line when the file is next modified; code added by a PR must already comply.

### Verification (acceptance checks)
- [ ] Every type and method added or modified by the PR has an XML summary (checked in review).
- [ ] Public and internal methods document their parameters, return value and thrown exceptions where not obvious.
- [ ] No comment contradicts the code it documents.

---

## Adding a new constraint

Append a new section using the same structure: `C-NN — Title`, status, date, applies-to, rule, goal, impact on existing issues, verification checks. Never reuse or renumber an ID.
