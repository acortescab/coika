# render-quality-config Specification

## Purpose

Fixes the project-level rendering quality for mobile so the pixel-art game renders sharply and cheaply on the target Android devices.

## Requirements

### Requirement: Android default quality
The Android player SHALL start at the Very Low quality level.

#### Scenario: Fresh Android install
- **WHEN** the game starts on Android with no prior quality override
- **THEN** the active quality level is Very Low

### Requirement: No HDR or anti-aliasing
The render pipeline SHALL have HDR and MSAA disabled for gameplay rendering.

#### Scenario: Build configuration
- **WHEN** the render pipeline asset and the gameplay camera are inspected
- **THEN** HDR is off and anti-aliasing is off
