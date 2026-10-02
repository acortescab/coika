---
last_mapped_commit: cc4a6315450b59b114e2bdbb7e25e918e35eed78
last_mapped_at: 2026-10-03
---
# External Integrations

**Analysis Date:** 2026-10-03

## APIs & External Services

**Runtime:**
- None detected. No `UnityWebRequest`, analytics, ads, or backend SDK usage in `Assets/Scripts/`.
- `com.unity.modules.unityanalytics` and `UnityConnectSettings.asset` (`ProjectSettings/UnityConnectSettings.asset`) exist as Unity defaults only.

**Editor/AI tooling (not shipped):**
- Unity AI Assistant (`com.unity.ai.assistant`) and Unity AI Inference packages in `Packages/manifest.json`
- Claude Code plugins incl. GitHub plugin - `.claude/settings.json`

## Data Storage

**Databases:**
- None

**File Storage:**
- Local filesystem only. Content delivered via Addressables groups in `Assets/AddressableAssetsData/` (Android settings in `Assets/AddressableAssetsData/Android`); remote catalog/CDN not detected.

**Caching:**
- In-process object pooling only: `Assets/Scripts/Core/Pooling/PrefabPool.cs`

## Authentication & Identity

**Auth Provider:**
- None. No `PlayerPrefs`/save system detected in `Assets/Scripts/`.

## Monitoring & Observability

**Error Tracking:**
- None

**Logs:**
- Unity console (`Debug.Log`); Editor logs in `Logs/` (ignored)

## CI/CD & Deployment

**Hosting:**
- Not detected (GitHub repo `acortescab/coika`, PRs via branches like `feature/7-merge-system`)

**CI Pipeline:**
- None (`.github/workflows` absent)

## Environment Configuration

**Required env vars:**
- None

**Secrets location:**
- Not applicable; no secrets detected

## Webhooks & Callbacks

**Incoming:**
- None

**Outgoing:**
- None

---

*Integration audit: 2026-10-03*
