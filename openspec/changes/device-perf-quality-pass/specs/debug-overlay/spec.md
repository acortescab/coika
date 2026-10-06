# Spec Delta

## Purpose

Gives developers an on-screen performance readout during Editor and development-build play, without shipping it or costing allocations in release builds.

## ADDED Requirements

### Requirement: Overlay contents
In Editor and development builds, the system SHALL display the frame rate, frame time, GC allocation count per frame, active piece count and the quality tier during gameplay.

#### Scenario: Development build
- **WHEN** a development build runs the game scene
- **THEN** the overlay shows all five values and keeps them updated

### Requirement: Release exclusion
The system SHALL NOT include the overlay in release builds.

#### Scenario: Release build
- **WHEN** a release build runs the game scene
- **THEN** no overlay is drawn and the overlay code is not compiled in

### Requirement: Overlay allocates nothing
The overlay SHALL NOT itself cause managed allocations on frames in which it updates or draws.

#### Scenario: Steady play
- **WHEN** the overlay is visible for several seconds of gameplay
- **THEN** the GC allocation count it reports does not rise because of the overlay
