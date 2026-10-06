# Spec Delta

## Purpose

Chooses a performance tier for the running device and applies its cheaper rendering and particle settings on weak hardware, so the game keeps its frame budget on low-end phones.

## ADDED Requirements

### Requirement: Device classification
The system SHALL classify the device as Low when its logical processor count is below 4 or its system memory is below 3000 MB, and as Normal otherwise.

#### Scenario: Few cores
- **WHEN** the device reports 2 processors and 4096 MB
- **THEN** the tier is Low

#### Scenario: Little memory
- **WHEN** the device reports 8 processors and 2999 MB
- **THEN** the tier is Low

#### Scenario: Boundary values are Normal
- **WHEN** the device reports exactly 4 processors and exactly 3000 MB
- **THEN** the tier is Normal

### Requirement: Tier effects
The system SHALL disable post-processing and reduce particle counts when the tier is Low, and SHALL leave post-processing on and particles at full count when the tier is Normal.

#### Scenario: Low tier
- **WHEN** a gameplay scene starts on a Low device
- **THEN** the post-processing Volume is disabled and the particle count multiplier is below 1

#### Scenario: Normal tier
- **WHEN** a gameplay scene starts on a Normal device
- **THEN** the post-processing Volume is enabled and the particle count multiplier is 1

### Requirement: Read-only tier
The system SHALL NOT offer a player-facing setting to change the tier.

#### Scenario: Settings screen
- **WHEN** the player opens Settings
- **THEN** no quality tier option is shown
