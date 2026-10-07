# test-gate Specification

## Purpose
Gives one command that runs every source rule check and every automated test, fails loudly when any of them fails, and records the M2 sign-off with its coverage table and device checklist.

## Requirements

### Requirement: One-command run
`Tools/run-tests.ps1` SHALL run the source grep checks, then the EditMode and PlayMode suites, and SHALL exit non-zero when any check or test fails.

#### Scenario: Green run
- **WHEN** the script runs on a clean checkout where every rule holds
- **THEN** it exits with code 0

#### Scenario: Broken rule
- **WHEN** a rule is temporarily broken in a source file
- **THEN** the script reports the violation and exits non-zero

### Requirement: Source rule checks
The script SHALL fail on an `INTERNET` permission that is not removed, on `OnGUI` outside the debug overlay, on `UnityEngine.Random` in gameplay code, and on `persistentDataPath` outside the save storage and the installer, besides the existing asset-loading checks.

#### Scenario: Internet permission declared
- **WHEN** a manifest under the Android plugins or the project settings declares `INTERNET` without removing it
- **THEN** the check fails and names the file

#### Scenario: Allowed exceptions
- **WHEN** `OnGUI` appears only in the debug overlay and `persistentDataPath` only in the save storage and the installer
- **THEN** those checks pass

#### Scenario: Comments are ignored
- **WHEN** a forbidden name appears only in a comment line
- **THEN** the check does not fail

### Requirement: Stable and fast suite
The full PlayMode suite SHALL finish in under 3 minutes and SHALL show no flaky test across 10 consecutive runs, with no console error or warning during the runs.

#### Scenario: Repeated run
- **WHEN** the script runs with a repeat of 10
- **THEN** every run passes and the suite time stays under 3 minutes

#### Scenario: Target missed
- **WHEN** a measured run is over the time target, fails a test or was not repeated 10 times
- **THEN** the sign-off document records the measured result, and a deviation is accepted only by the owner, in the document

### Requirement: M2 sign-off document
The project SHALL keep a sign-off document mapping each M2 issue's acceptance criteria to its covering test, marking gaps and manual checks, and listing the on-device checklist with space for the real results.

#### Scenario: Coverage table
- **WHEN** a reviewer opens the sign-off document
- **THEN** every M2 issue has a table of criteria with a covering test, a manual check or a marked gap

#### Scenario: Device results pending
- **WHEN** the device checklist has not been run and the owner has not signed it off
- **THEN** its result fields stay blank and the M2 exit criterion is not marked confirmed

#### Scenario: Owner sign-off without figures
- **WHEN** the owner signs off the checklist without recording figures
- **THEN** the document says the ticks are the owner's declaration and the missing figures read "not recorded"
