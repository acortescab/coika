# M1 test coverage: acceptance criteria vs tests

Review of issue #12 (item 7). Each criterion of #2, #5, #7, #8, #9 and #11 is mapped to the test that covers it. "Manual"
means it cannot be automated; "Gap" means no automated test exists yet.

## #2 Tier data
| Criterion | Test |
|---|---|
| 11 assets match the GDD table; sprite width equals the diameter at PPU 16 | `TierDataTests.ShippedTheme_Always_HasElevenTiersWithSpritesMatchingDiameter` |
| `GameConfig.theme` references `Theme_Cosmic` | `TierDataTests.GameConfig_Always_ReferencesTheShippedTheme` |
| Assets are in Addressable groups (C-01) | `TierDataTests.ShippedTheme_Always_HasEveryAssetInItsAddressableGroup` |
| Load and release leave 0 handles | `LoadTiersAsync_ThenRelease_LeavesNoOutstandingHandles`, `LoadTiersAsync_WhenOneLoadFails_ReleasesTheOnesAlreadyLoaded`, `TierSpriteLoadTests` |
| Generator is idempotent (identical PNGs on two runs) | **Gap** |
| Validate Theme reports OK / error | Manual |

## #5 SpawnQueue
| Criterion | Test |
|---|---|
| Same seed gives the same 10,000-tier sequence | `SpawnQueueTests.Advance_WithTheSameSeedAndSettings_ProducesTheSameSequenceOver10000` |
| First three pieces are 0, 1, 0 | `Advance_FirstThreePieces_AreTheForcedOpeningForAnySeed` |
| No tier more than 3 times in a row | `Advance_Over100000Pieces_NeverRepeatsATierMoreThanThreeTimes` |
| Frequencies within 1.5 points | `Advance_Over100000Pieces_MatchesTheWeightsWithinOnePointFivePoints` |
| Out-of-range tiers never produced | `Advance_WithFewerSpawnableTiers_NeverProducesTiersOutsideTheRange` |
| Save and restore RNG state | `SpawnQueueStateTests` |
| Invalid config gives a clear error | `Constructor_WithAConfigWithInvalidValues_ThrowsAClearError` |
| 0 allocations per `Advance()` | `Advance_AfterConstruction_AllocatesNoManagedMemory` |
| Same seed gives the same game through the whole stack | **New:** `SimulationDeterminismPlayModeTests` |

## #7 MergeSystem
| Criterion | Test |
|---|---|
| Two same-tier pieces give exactly one result (1,000 random) | `MergeSystemPlayModeTests.Merge_With1000RandomizedPairs_AlwaysProducesExactlyOneResult`; **New:** `MergeIntegrityPlayModeTests` (5 seeds x 1,000 drops, per-step invariants) |
| Three touching: one merge, leftover kept | `Merge_ThreeTouchingPieces_MergesOnePairAndKeepsTheThird` |
| Chain of two steps | `Merge_ChainOfTwoSteps_ResolvesOverSuccessiveSteps` |
| Determinism | `Merge_SameSeedAndDrops_LeavesTheSameBoardTwice`; **New:** `SimulationDeterminismPlayModeTests` |
| Two Black Holes give one Supernova | `Merge_TwoBlackHoles_VanishAndRaiseSupernovaOnce` |
| Midpoint and average velocity | `Merge_TwoMovingPieces_CreatesPieceAtMidpointWithAverageVelocity` |
| 0 GC allocations | `Merge_InSteadyState_AllocatesNothing` |
| Pool exhausted / pieces released externally | `Merge_WithPoolExhausted_StillProducesTheResult`, `Merge_WhenPiecesAreReleasedAfterTheContact_IgnoresThePair`, `Merge_WhenTheFactoryCannotCreate_LogsOneErrorAndLeavesBothPiecesUntouched` |
| Restart mid-merge | Partial (`GameLoopPlayModeTests`); **New:** `RunLifecycleHarnessPlayModeTests` restarts after real merges |
| No create/destroy inside `OnCollision` | Code review only |

## #8 Score
| Criterion | Test |
|---|---|
| Merge score per tier, drop score, combo multipliers, x3 cap, floor, Supernova, NEW BEST, overflow safety, reset, 0 GC | `ScoreSystemTests`, `ComboTrackerTests` (all covered) |
| Score of a whole run equals the hand calculation | **New:** `ScoreIntegrationPlayModeTests` |

## #9 OverflowDetector
| Criterion | Test |
|---|---|
| Settled piece above the line for 2 s triggers game over once | `OverflowDetectorPlayModeTests.Evaluate_SettledPieceAboveTheLine_TriggersGameOverOnceAfterTwoSeconds`; **New:** `OverflowIntegrationPlayModeTests` (real drops) |
| Falling piece does not trigger | `Evaluate_PieceFallingFromTheDropLine_DoesNotTriggerGameOver`; **New:** `OverflowIntegrationPlayModeTests` |
| Timer resets, merge grace, progress, `Disable`, `ResetForNewRun`, 0 GC | `OverflowDetectorPlayModeTests` (all covered) |
| Danger Line display | Manual (`DangerLineTests` cover the logic) |

## #11 GameManager
| Criterion | Test |
|---|---|
| State transitions | `GameStateTransitionsTests`, `GameManagerTests` |
| `GameOverTriggered` ends the run once | `Evaluate_TwoPiecesOverflowing_RaisesGameOverOnce`, `EndRun_CalledTwice_StopsAndAnnouncesOnce` |
| Nothing drops or scores during game over | `GameLoopPlayModeTests.GameOver_WhileOver_NothingCanDropOrScore` |
| Retry gives a clean run (3 runs) | `GameLoopPlayModeTests.Retry_ThreeConsecutiveRuns_LeaveNoPiecesOrSubscribers`; **New:** `RunLifecycleHarnessPlayModeTests` (real drops, full state snapshot) |
| No `FindObjectOfType`, no singletons | `ProjectFoundationTests` |
| Boot reaches Playing (C-01) | **New:** `BootToGamePlayModeTests` (Use Asset Database mode) |
| Failed load shows the retry UI | `GameLoopPlayModeTests.Installer_WithMissingReferences_ShowsTheRetryUi` |
| Handle / ref-count flatness over 3 runs and a reload | Partial: state is checked, handles are not. **Gap** |
| 5-minute soak, 0 GC per frame | Manual |

## Also added for C-01 and hygiene
| Rule | Test or check |
|---|---|
| `AssetService` / `SceneLoaderService` argument validation | **New:** `AssetServicesArgumentTests` (retry and failure paths need an Addressables seam: **Gap**) |
| No `Resources.Load`, `Resources/` folder, `SceneManager.LoadScene*` or direct `Addressables.*` outside the services | `ProjectFoundationTests` (scripts), **New:** `TestHygieneTests` (tests), **New:** pre-flight in `Tools/run-tests.ps1` (runs for every mode) |
| Tests do not use the real clock, `UnityEngine.Random` or `persistentDataPath` | **New:** `TestHygieneTests` |
