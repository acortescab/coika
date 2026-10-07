# Proposal

## Why

M1 and M2 shipped with flat-colour placeholder circles for the 11 tiers. Every later M3 issue (faces #62, backgrounds #63, Evolution chart #64, Discovery log #70) builds on the real sprites, so the final bodies, chart icons, shared palette and atlas must land first (issue #61). The runtime wiring (`TierDefinition` `AssetReferenceSprite`, `PieceFactory`, `AssetService`) already exists; the gap is the art, its import rules, the atlas and the checks that keep them correct.

## What Changes

- Add one shared palette of at most 32 colours (`Assets/Art/Palette/coika.gpl`) that includes the 11 GDD §9 tier colours.
- Replace the 11 placeholder body PNGs with final bodies named `piece_XX_<name>` (exact `round(diameter*16)` px width, 1 px dark outline, top-left cell-shaded highlight, Pivot Center) and add 11 chart icons named `icon_XX_<name>` (12x12 px).
- Produce the art with a deterministic editor generator that draws only palette colours, replacing the placeholder generator; remove its call from `TierDataSetup` so a rerun cannot overwrite art.
- Enforce GDD §10 import settings (Point, no compression, no mip maps, Single, PPU 16) with an `AssetPostprocessor` and a validator covered by EditMode tests, including a negative test.
- Add a `Theme-Cosmic` Sprite Atlas for the piece bodies (padding >= 4, Tight Packing off, Android and iOS RGBA32 with Point filter) and extend the sprite platform-override tooling to iOS.
- Validate palette size, sprite naming, Addressables group (`Theme-Cosmic`) and collider-versus-sprite size (within 0.5 px).
- Record art licence in `Assets/Art/LICENSES.md`; update GDD §10, README and LEARNINGS.
- No gameplay code changes; `simulation-seed-1234.txt` stays byte-identical; `GhostOutlineSprites` is untouched.

## Capabilities

### New Capabilities
- `piece-art`: final tier body sprites, chart icons, shared palette, import rules and Sprite Atlas, with the validation that keeps them correct.

### Modified Capabilities

## Impact

- Code: `Assets/Scripts/Tools/Editor` (new generator, postprocessor, validator; `TierDataSetup`, `SpriteAndroidOverrides` edited; `PlaceholderTierSpriteGenerator` removed or reduced to a test fake), `Assets/Tests/EditMode` (new tests).
- Assets: `Assets/Art/Sprites/Tiers`, new `Assets/Art/Palette`, `Assets/Art/Atlases`, `Assets/Art/LICENSES.md`, Addressables `Theme-Cosmic` group entries.
- Docs: `.planning/gdd.md` §10, `README.md`, `.planning/LEARNINGS.md`.
- Out of scope: faces (#62), background and jar art (#63), alternative skins (M4), animation frames, other UI sprites. Real-device pixel checks at 1080x1920, 1080x2400 and 720x1280 and the Frame Debugger draw-call count are a manual PR checklist, not done here.
