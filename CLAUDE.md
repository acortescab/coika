# Coika

Unity 6000.6.3f1 mobile merge-physics game (2D URP). Overview, structure and setup are in `README.md`: do not re-read it for facts listed here.

## Save tokens

- **Docs are large: read only the section you need.** `.planning/gdd.md` (469 lines; scoring §4, physics §5, acceptance criteria §19), `.planning/code-standards.md` (134), `.planning/constraints.md` (116; C-01 Addressables, C-02 comments). Use Grep for a rule number (`S-63`) instead of reading the file.
- **Locate with Grep/Glob, then read the exact range.** Never read a whole `.cs` file to find one member. Skip `Library/`, `Temp/`, `Logs/`, `*.meta`, `*.asset`, `*.unity`, `*.prefab` (huge YAML); grep them for a field name only.
- **Test results are small: parse the junit XML, do not read logs.** `TestResults/*.junit.xml` (git-ignored) holds pass/fail and messages. For Editor runs, the failure text is in `%USERPROFILE%\AppData\LocalLow\acortescab\coika\TestResults.xml` and `%LOCALAPPDATA%\Unity\Editor\Editor.log`: grep it, never read it whole.
- **Delegate wide exploration to a subagent** (it returns a summary); do the focused edits yourself.

## Commands (PowerShell, repo root)

- `./Tools/run-tests.ps1` runs C-01 grep checks, then EditMode and PlayMode, and exits non-zero on failure. Options: `-Mode`, `-Filter <name>`, `-Repeat N`, `-TimeoutSeconds`.
- **The Editor must be closed** (`Get-Process Unity`; a `Temp/UnityLockfile` that is held means it is open). Do not close the user's Editor: ask.
- After a run, summarize with `[xml]$x = Get-Content TestResults\PlayMode.junit.xml` and list failed `testcase` nodes.
- The `unity` CLI is in `%LOCALAPPDATA%\Unity\bin`: refresh `$env:Path` from the Machine and User values if a shell cannot find it.

## Code rules that bite

- Layers: `UI -> Gameplay -> Core`; `Data` depends on `Core`. Assemblies: `Coika.Core/Data/Gameplay/UI`, tests in `Coika.Tests.EditMode/PlayMode` (the PlayMode asmdef has no Editor reference).
- C-01: everything but `Boot` loads through `IAssetService` / `ISceneLoader`. No `Resources.Load`, no `Addressables.*` outside the services, no `SceneManager.LoadScene*` outside `GameInstaller` / `SceneLoaderService`. No singletons.
- C-02: an English XML `<summary>` on every type and method, including private and test methods; `<param>`, `<returns>`, `<exception>` on public and internal ones. Braces always, `_camelCase` fields, `UPPER_CASE` constants, one type per file, tests named `Subject_Condition_ExpectedBehavior`.
- Gameplay is deterministic (S-63): seeded `System.Random`, never `UnityEngine.Random` or the real clock in logic. Time reaches `Piece`, `MergeSystem`, `OverflowDetector` and `RunSystems` through the injected clock.
- Tests: no real clock, `UnityEngine.Random` or `persistentDataPath` (`TestHygieneTests` enforces it). PlayMode tests use the doubles in `Assets/Tests/PlayMode/Fakes` (`TestAssetService`, `TestTiers`, `TestReflection`) and the harness in `Assets/Tests/PlayMode/Harness`.

## Simulation harness gotchas

- Run it synchronously: yielding a frame lets the game's own `FixedUpdate` step the systems.
- Each `SimulationWorld` owns a local physics scene. Sharing the global one made same-seed runs diverge.
- `Assets/Tests/PlayMode/Golden/simulation-seed-1234.txt` pins one result. After an intended rule, Unity or physics change, regenerate it with `COIKA_UPDATE_GOLDEN=1`, run twice and commit it.
- A pool-growth or any other warning fails harness tests (`HarnessTestBase`): size the pool, do not silence the guard.
- Allocation tests (`AllocatesNothing`) can fail once on a cold Editor start: rerun before investigating.

## Git

- Branch `feature/<issue>-<name>` from `origin/main` (local `main` can be stale: fetch first). Commit only files you changed.
- Leave the editor-noise files alone and unstaged: `Assets/Art/Fonts/UiFont SDF.asset`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json`.
- Commit and push only when asked. End commits with the `Co-Authored-By` line the session gives.
- Unity creates `.meta` files on the first open: commit them with their sources.
- Files mix LF and CRLF; edit with a tool that preserves each file's endings.

## Learnings and docs

- **Learnings go in `.planning/LEARNINGS.md`**, not in chat or only in memory. Add or update a dated section per issue with the non-obvious things learned (design decisions, test and tooling gotchas, working agreements), short and with the why.
- **Docs must be up to date before opening a PR.** Update every document the change touches: `README.md`, `.planning/gdd.md`, `.planning/code-standards.md`, `.planning/constraints.md` and `.planning/LEARNINGS.md`. Commit the doc changes in the same PR as the code.