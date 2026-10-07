# Tasks

## 1. Palette

- [x] 1.1 Add `CoikaPalette` (outline, 11 tier bases from GDD §9, shades, shared highlights) and write `Assets/Art/Palette/coika.gpl`; EditMode test asserts <= 32 colours and that all 11 tier colours are present
- [x] 1.2 Document the palette in GDD §10 and verify it lists the same colours as the `.gpl`

## 2. Import rules and validation

- [x] 2.1 Add `PieceArtRules` plus `SpriteImportPostprocessor` for `piece_*` and `icon_*` paths; verify a reimport applies Point, no compression, no mip maps, Single, PPU 16
- [x] 2.2 Add `PieceArtValidator.GetErrors()` (settings, names, palette count, `Theme-Cosmic` group, atlas settings, collider vs sprite within 0.5 px) with EditMode tests, including a negative test that flips a sprite to bilinear/compressed and expects errors; run `./Tools/run-tests.ps1 -Mode EditMode`
- [x] 2.3 Extend `SpriteAndroidOverrides` to a platform list (Android and iOS) and update its test; verify the test fails without the iOS override

## 3. Art and atlas

- [x] 3.1 Add `PieceArtGenerator` (bodies and 12x12 icons, palette only) and a menu entry; run it and verify the 22 PNGs exist with the exact widths
- [x] 3.2 Rename the bodies to `piece_XX_<name>` keeping GUIDs and verify the 11 `TierDefinition` sprite references still resolve and `ThemeDefinition.Validate` passes
- [x] 3.3 Create the `Theme-Cosmic` Sprite Atlas (padding 4, Tight Packing off, Android and iOS RGBA32 Point) and put icons and atlas in the `Theme-Cosmic` group; verify the validator is green
- [x] 3.4 Remove the placeholder generator call from `TierDataSetup` and delete the generator; verify rerunning `Coika/Setup Tier Data` leaves the sprites unchanged
- [x] 3.5 Add `Assets/Art/LICENSES.md` (own generated art) and verify it is referenced from README

## 4. Integration

- [x] 4.1 Run `./Tools/run-tests.ps1` (EditMode and PlayMode) and verify green with `simulation-seed-1234.txt` unchanged and 0 console warnings
- [x] 4.2 Review the Addressables Analyze report for duplicate dependencies and record the collider-vs-sprite numbers
- [x] 4.3 Update README, GDD §10 and `.planning/LEARNINGS.md`; prepare the PR body with `Closes #61` and the manual device checklist (3 resolutions, Frame Debugger <= 20 draw calls)

## Workflow follow-up

- Archive the change with `/opsx:archive` after the PR is merged.

