# game-modes Specification

## Purpose
Defines the game modes (Classic, Daily, Zen) as data: how a run's seed, end condition and best-score storage depend on the mode, so that adding a mode does not change the run loop.

## Requirements

### Requirement: Modes are described by a rules table
The system SHALL describe each mode with a rules entry that gives its seed, whether a settled piece above the Danger Line ends the run, whether the Danger Line is shown, whether the best score is recorded, and the key under which the best score is stored. Adding a mode SHALL require only a new entry.

#### Scenario: Documented flags per mode
- **WHEN** the rules of each mode are queried
- **THEN** Classic ends on overflow, shows the line and records the best score
- **AND** Daily ends on overflow, shows the line and records the best score
- **AND** Zen does not end on overflow, hides the line and does not record the best score

#### Scenario: A new mode is a new entry
- **WHEN** a fourth mode with its own rules is added to the table
- **THEN** it resolves its rules and seed through the same lookup, with no change to the existing modes or to the run loop

#### Scenario: Unknown mode
- **WHEN** rules are requested for a mode that is not in the table
- **THEN** the lookup fails with an explicit error rather than falling back to Classic

### Requirement: Classic seed is fresh per run
The system SHALL take the seed of a Classic run from an injected seed source, so that the real source may vary between runs and tests can inject a fixed one.

#### Scenario: Fixed seed source
- **WHEN** a Classic run starts with a seed source that returns 1234
- **THEN** the run uses seed 1234

#### Scenario: Classic retry draws a new seed
- **WHEN** the player retries a Classic run and the seed source returns a different value
- **THEN** the new run uses the new seed

### Requirement: Daily seed is the UTC date
The system SHALL use the UTC date of an injected clock, as the integer `yyyyMMdd`, as the seed of a Daily run. The clock SHALL be read only when a run starts.

#### Scenario: Seed from the date
- **WHEN** a Daily run starts and the clock reads 2026-10-07 UTC
- **THEN** the seed is 20261007

#### Scenario: Seed changes at UTC midnight
- **WHEN** the clock reads 2026-10-07 23:59:59 UTC for one run and 2026-10-08 00:00:00 UTC for the next
- **THEN** the two runs have two different seeds

#### Scenario: Retry keeps the board
- **WHEN** the player retries a Daily run
- **THEN** the new run uses the same seed and the same piece sequence as the first, even if the clock has advanced

### Requirement: The chosen mode reaches the Game scene through the run setup
The system SHALL keep the chosen mode in a run setup object owned by the installer, written by the Menu or Modes screen and read by the Game scene when it starts a run. It SHALL NOT use static state. When nothing was chosen the mode SHALL be Classic.

#### Scenario: Default mode
- **WHEN** the Game scene starts a run and no mode was chosen
- **THEN** the run is a Classic run

#### Scenario: Mode survives the scene load
- **WHEN** a mode is chosen before the Game scene loads
- **THEN** the Game scene starts its run in that mode

#### Scenario: Retry keeps the mode
- **WHEN** the player retries after a game over
- **THEN** the new run is in the same mode as the previous one

### Requirement: Overflow ends the run only in modes that end on overflow
The system SHALL consult the overflow detection only when the mode's rules say the run ends on overflow.

#### Scenario: Classic ends on overflow
- **WHEN** a settled piece stays above the Danger Line for the configured time in a Classic run
- **THEN** the run ends

#### Scenario: Zen does not end on overflow
- **WHEN** a settled piece stays above the Danger Line for longer than the configured time in a Zen run
- **THEN** the run keeps playing

### Requirement: Best score is stored per mode
The system SHALL store and read the best score of each mode separately in the save, without a schema version bump. The daily best SHALL carry the date it was set on, and a daily best of another date SHALL read as 0. A mode that does not record the best score SHALL NOT write it.

#### Scenario: Round trip per mode
- **WHEN** a best score is set for each of the three modes and the save is written and read back
- **THEN** each mode reads back its own value

#### Scenario: No leak between modes
- **WHEN** the best score of one mode is set
- **THEN** the best score of the other modes does not change

#### Scenario: Daily best of another day
- **WHEN** the daily best was set on 2026-10-06 and it is read on 2026-10-07
- **THEN** it reads as 0

#### Scenario: Best only goes up
- **WHEN** a lower score than the stored best is set for a mode
- **THEN** the stored best is kept

#### Scenario: Older save without the new fields
- **WHEN** a save file written before this change is loaded
- **THEN** it loads without being reset, and the Classic best score is kept

### Requirement: Classic behaves as before
The system SHALL give Classic the same seed policy, end condition and best-score behaviour it had before modes existed, and the same simulation result for the same seed.

#### Scenario: Golden simulation unchanged
- **WHEN** the simulation harness runs with seed 1234
- **THEN** the result equals the pinned `simulation-seed-1234.txt` without regenerating it
