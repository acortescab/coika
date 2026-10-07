# Spec Delta

## Purpose

Defines the final pixel art for the 11 tier pieces and their chart icons, the shared palette, the import rules and the Sprite Atlas, so pieces render sharply and consistently on mobile.

## ADDED Requirements

### Requirement: Shared palette
The piece sprites and chart icons SHALL use at most 32 distinct opaque colours, and the palette SHALL include the 11 tier colours of GDD §9.

#### Scenario: Palette size
- **WHEN** the distinct opaque colours across all piece sprites and chart icons are counted
- **THEN** the count is 32 or fewer and every tier colour is among them

#### Scenario: Palette file
- **WHEN** the palette file is read
- **THEN** it lists the same colours the sprites use

### Requirement: Tier body sprites
Each of the 11 tiers SHALL have one body sprite named `piece_XX_<name>` whose pixel width equals its tier diameter at 16 pixels per unit, with a 1 px dark outline, a top-left cell-shaded highlight and Pivot Center.

#### Scenario: Sprite size matches tier
- **WHEN** a tier body sprite is loaded
- **THEN** its width in pixels equals round(diameter x 16) and it is not resized at runtime

#### Scenario: Collider matches visible body
- **WHEN** the collider radius of a tier is compared with the visible body radius, outline included
- **THEN** they differ by 0.5 px or less

### Requirement: Chart icons
Each tier SHALL have one chart icon named `icon_XX_<name>` of about 12x12 px with the same silhouette as its body, simplified.

#### Scenario: Icon per tier
- **WHEN** the chart icons are listed
- **THEN** there are 11, one per tier, each a single sprite

### Requirement: Import settings
Every piece sprite and chart icon SHALL be imported with Point filter, no compression, no mip maps, Sprite Mode Single and 16 pixels per unit.

#### Scenario: Correct import
- **WHEN** a piece sprite or chart icon is imported
- **THEN** it has the required settings

#### Scenario: Wrong import detected
- **WHEN** a sprite is switched to bilinear filtering or to a compressed format
- **THEN** validation fails and the EditMode suite is red

### Requirement: Sprite Atlas
The piece body sprites SHALL be packed in one Sprite Atlas with padding of at least 4, Tight Packing off, and Android and iOS overrides that are uncompressed RGBA32 with Point filter.

#### Scenario: Atlas settings
- **WHEN** the atlas settings are inspected
- **THEN** padding is 4 or more, Tight Packing is off and both mobile overrides are RGBA32 with Point filter

### Requirement: Addressable placement
Piece sprites, chart icons and the atlas SHALL live in the `Theme-Cosmic` Addressables group, be loaded through the asset service by `AssetReferenceSprite`, and not be duplicated into other bundles.

#### Scenario: Group membership
- **WHEN** the Addressables entries of the sprites, icons and atlas are inspected
- **THEN** all are in `Theme-Cosmic`

### Requirement: No placeholder in normal runs
A normal run SHALL render every tier with its final sprite, and the placeholder generator SHALL NOT overwrite final art.

#### Scenario: Tier data setup rerun
- **WHEN** the tier data setup is run again
- **THEN** the final sprites are unchanged

### Requirement: Simulation unaffected
The art change SHALL NOT alter gameplay simulation results.

#### Scenario: Golden simulation
- **WHEN** the seed 1234 simulation runs
- **THEN** its result equals the committed golden file
