# Design

## Context

See proposal.md. Observed state: `TierDefinition._sprite` is an `AssetReferenceSprite`; `PieceFactory` loads it through `AssetService`; `Piece.Initialize` sets the collider from `TierDefinition.DiameterUnits`, not from the sprite; `ThemeDefinition.Validate` already checks 11 tiers and sprite width. The 11 sprites are generated placeholders (`PlaceholderTierSpriteGenerator`, called from `TierDataSetup`) already in `Theme-Cosmic`. There is no atlas, no `AssetPostprocessor`, no palette. `SpriteAndroidOverrides` covers Android only and has a test. Editor tooling lives in `Coika.Tools.Editor`, tested from `Coika.Tests.EditMode`. PlayMode tests use a fake 16x16 sprite, so they do not depend on the final art.

## Goals / Non-Goals

**Goals:** final art reproducible from code and palette; wrong imports fail the EditMode suite; zero runtime and golden change.
**Non-Goals:** faces, backgrounds, skins, animation, ghost outline changes, device verification.

## Decisions

1. **Procedural generator over hand-drawn PNGs.** A `PieceArtGenerator` draws bodies and icons using only palette colours, so palette and size rules hold by construction and art can be regenerated. Alternative: hand-drawn PNGs (not available now). Sprite names and sizes are fixed so hand art can overwrite later without code changes. Own-art licence is recorded.
2. **Palette as single source of truth.** A `CoikaPalette` class defines outline, per-tier base and shade, and a small shared highlight ramp (total asserted <= 32 in a test); it also writes `coika.gpl`. 11 bases + 11 shades + outline is 23, leaving 9 for shared highlights.
3. **Keep generated files in place.** Bodies keep the existing folder; rename to `piece_XX_<name>` keeping `.meta` GUIDs (move via `AssetDatabase`) so `TierDefinition` references stay valid.
4. **One rule set, two enforcers.** A shared `PieceArtRules` feeds an `AssetPostprocessor` (fixes settings on import) and a `PieceArtValidator.GetErrors()` (follows the `AudioValidator` pattern) used by EditMode tests. Alternative: test only. The postprocessor prevents the bad state; the validator and negative test prove it.
5. **Collider check in the validator, not runtime.** Compare `DiameterUnits*16/2` with the sprite body radius measured from opaque pixels (outline included), tolerance 0.5 px. No gameplay code changes.
6. **Atlas v2 `Theme-Cosmic.spriteatlasv2`** with platform overrides for Android and iOS; extend `SpriteAndroidOverrides` through a platform list to add iOS. Chart icons stay outside the atlas (UI), still in `Theme-Cosmic`.
7. **Placeholder generator.** Remove its call from `TierDataSetup`; keep no editor use. Test doubles already use `TestAssetService` fakes.

## Risks / Trade-offs

- Tier 0 is 12 px, so the highlight is 1-2 px and the 12x12 icon equals the body size → icon is outline plus fill.
- Generated art looks generic → fixed names and sizes allow replacement later.
- Atlas batching and the draw-call limit cannot be verified without a device or Frame Debugger → settings are validated; the rest is a PR checklist.
- Postprocessor can rewrite settings during a batch run → limited to piece and icon paths.
