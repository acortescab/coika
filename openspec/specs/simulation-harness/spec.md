# simulation-harness Specification

## Purpose
Proves that the deterministic simulation gives the same result whatever the feedback layer, pauses or slow-mo do around it, and that runs, pools and listeners do not leak across Retry and Pause/Resume.

## Requirements

### Requirement: Feedback layer does not change the result
The simulation SHALL give an identical result for the same seed and drops with all M2 systems (animations, particles, feedback director) present and with all of them absent.

#### Scenario: All M2 systems on versus off
- **WHEN** the same seed and drop script run once with every M2 system and once with none, for each of at least 5 seeds
- **THEN** both runs produce an identical simulation result

### Requirement: Pauses do not change the result
The simulation SHALL support pausing and resuming a run at scripted points, and a paused run SHALL give the same result as an uninterrupted run with the same seed and drops.

#### Scenario: Pauses inserted at scripted points
- **WHEN** a run is paused and resumed after chosen drops, with simulated time frozen while paused
- **THEN** its result equals that of the same run without pauses, for each of at least 5 seeds

#### Scenario: Nothing advances while paused
- **WHEN** the run is paused and steps are requested
- **THEN** pieces, score and simulated time stay unchanged until it resumes

### Requirement: Slow-motion does not change the result
The simulation SHALL give the same result when a slow-motion request is active during the run as when none is.

#### Scenario: Slow-mo during a heavy merge
- **WHEN** a slow-motion request starts at a scripted merge and ends after its duration
- **THEN** the result equals that of the run without slow-motion, and the time scale is back to normal at the end

### Requirement: Golden result is stable
The simulation SHALL keep the recorded golden result for seed 1234 byte-identical unless a physics or rule change was intended.

#### Scenario: M2 features leave the golden file alone
- **WHEN** the golden simulation test runs after the M2 features are in place
- **THEN** it matches the stored golden file and the file has no diff

### Requirement: Feedback end to end
A scripted run that produces a combo (two merges inside the combo window), a merge of tier 8 or higher, a Supernova and a game over SHALL trigger the sounds, haptics, particles and screen effects of the feedback table, as seen through the test doubles.

#### Scenario: Whole table in one run
- **WHEN** the scripted run is played with real physics and the feedback director
- **THEN** each of the four events produces its feedback entries, and the game over produces exactly one game-over sound

#### Scenario: Pauses and slow-mo really happen
- **WHEN** a determinism comparison asks for pauses or slow-mo
- **THEN** the run pauses at least once and the slow-mo reaches the time scale, so an identical result is not the result of nothing happening

### Requirement: Run lifecycle does not leak
Starting a run through Retry, and pausing and resuming, SHALL leave object counts, event listener counts, particle pool sizes and audio voices equal to those at the start of the first run.

#### Scenario: Three consecutive runs
- **WHEN** three runs are played in a row through Retry
- **THEN** the counts at the start of each run are identical

#### Scenario: Pause and resume
- **WHEN** a run is paused and resumed
- **THEN** the counts are identical to those before the pause

#### Scenario: Scene services released
- **WHEN** the game is booted through the boot scene and the gameplay scene is released
- **THEN** every M2 service created with the scene is released with it

### Requirement: Full run allocates nothing
A scripted run with every M2 system active SHALL stay within the allocation budget of the allocation meter.

#### Scenario: Allocation over a full scripted run
- **WHEN** the allocation meter measures a full scripted run with all M2 systems active
- **THEN** the median allocations per step stay under the budget
