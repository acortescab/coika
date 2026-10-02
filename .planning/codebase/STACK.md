# Technology Stack

**Analysis Date:** 2026-10-03

## Languages

**Primary:**
- C# (Unity API compatibility level 6, .NET Standard 2.1 profile) - all game code in `Assets/Scripts/`

**Secondary:**
- Unity serialized YAML (`.asset`, `.prefab`, `.unity`, `.inputactions` JSON) - data and config in `Assets/Data/`, `Assets/Input/`, `Assets/Settings/`

## Runtime

**Environment:**
- Unity Editor 6000.6.3f1 (Unity 6) - `ProjectSettings/ProjectVersion.txt`
- 2D game, Universal Render Pipeline with 2D Renderer
- Targets: desktop/default plus Android (min SDK 26, `ProjectSettings/ProjectSettings.asset`)

**Package Manager:**
- Unity Package Manager - `Packages/manifest.json`
- Lockfile: `Packages/packages-lock.json` (managed by Unity)

## Frameworks

**Core:**
- Unity 2D + Physics2D (`com.unity.modules.physics2d`) - piece physics in jar
- Universal RP 17.6.0 (`com.unity.render-pipelines.universal`) - `Assets/Settings/UniversalRP.asset`, `Assets/Settings/Renderer2D.asset`
- Input System 1.20.0 - `Assets/Input/Coika.inputactions`; `activeInputHandler: 1` (new Input System only)
- Addressables 2.11.2 (+ `com.unity.addressables.android` 1.1.0) - `Assets/AddressableAssetsData/`
- uGUI 2.6.0 (`com.unity.ugui`)

**Testing:**
- Unity Test Framework 1.8.0 with NUnit - `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/`

**Build/Dev:**
- Assembly definitions: `Coika.Core`, `Coika.Data`, `Coika.Gameplay`, `Coika.UI`, `Coika.Tools.Editor`, `Coika.Tests.EditMode`, `Coika.Tests.PlayMode` (`Assets/Scripts/*/*.asmdef`, `Assets/Tests/*/*.asmdef`)
- Generated solution/csproj files at repo root (`coika.slnx`, `Coika.*.csproj`)
- IDE: Rider 3.0.38 and Visual Studio 2.0.26 packages

## Key Dependencies

**Critical:**
- `com.unity.addressables` 2.11.2 - all asset and scene loading via `Assets/Scripts/Core/Assets/AssetService.cs`
- `com.unity.inputsystem` 1.20.0 - pointer/keyboard in `Assets/Scripts/Gameplay/Input/PointerInputReader.cs`
- `com.unity.render-pipelines.universal` 17.6.0 - 2D lighting/renderer

**Infrastructure:**
- 2D packages: animation 16.0.1, aseprite 6.0.0, psdimporter 15.0.1, spriteshape 16.0.0, tilemap.extras 9.0.1
- `com.unity.timeline` 6.6.0, `com.unity.visualscripting` 1.9.12 (unused in code)
- `com.unity.ai.assistant` 2.20.0-pre.1, `com.unity.ai.inference` 2.6.1, `com.unity.pipeline` 0.8.0-exp.1 (editor/preview, not referenced by game code)
- No third-party DI or async libraries; uses `System.Threading.Tasks` and a hand-written installer (`Assets/Scripts/Core/Boot/GameInstaller.cs`)

## Configuration

**Environment:**
- No env vars used by game code; `.env` files not detected
- Gameplay tuning in ScriptableObjects: `Assets/Data/GameConfig/GameConfig.asset`, `Assets/Data/Tiers/Tier_*.asset`, `Assets/Data/Themes/Theme_Cosmic.asset`
- Claude Code plugins enabled in `.claude/settings.json`; planning docs in `.planning/` (`gdd.md`, `constraints.md`, `code-standards.md`)

**Build:**
- `ProjectSettings/ProjectSettings.asset` (company `acortescab`, product `coika`, version 1.0, default 1920x1080)
- Build profiles: `Assets/Settings/Build Profiles/`
- Editor menu for Addressables build: `Assets/Scripts/Tools/Editor/AddressablesBuildMenu.cs`

## Platform Requirements

**Development:**
- Unity 6000.6.3f1, Windows (Rider or Visual Studio)

**Production:**
- Android (min SDK 26) mobile target; standalone/desktop and WebGL screen sizes also configured. Store/release pipeline not detected.

---

*Stack analysis: 2026-10-03*
