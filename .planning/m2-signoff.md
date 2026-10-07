# M2 sign-off: test coverage and device checklist

Quality gate of milestone M2 (issue #40, like `m1-test-coverage.md` was for M1). Part 1 maps the acceptance criteria of
every M2 issue (#26 to #39) to the test that covers it. "Manual" means it needs a device, screenshots or human judgement;
"**Gap**" means it could be automated and no test exists yet. Part 2 is the one-command gate. Part 3 is the on-device
checklist, **signed off by the owner on 2026-10-07**: the ticks are the owner's declaration, and no figure was measured or recorded by the author of the tests.

Every test name below was checked against `Assets/Tests`. Tests added by #40 are in
`M2DeterminismPlayModeTests`, `FeedbackEndToEndPlayModeTests`, `HarnessPausePlayModeTests`,
`RunLifecycleHarnessPlayModeTests` (5 seeds, Pause/Resume), `BootToGamePlayModeTests` (M2 services), `GameLoopPlayModeTests`
(best score across a restart), `SettingsServiceTests` (settings across a restart) and `SaveSystemTests` (version 0 file).

## Part 1: acceptance criteria vs tests

## #26 SaveSystem, SaveData and SettingsService
| Criterion | Test |
|---|---|
| Save then load returns equal SaveData | `SaveSystemTests.SaveLoad_RoundTrip_ReturnsEqualData` |
| Corrupt/empty/truncated/unknown version give defaults, save.bak, no exception | `SaveSystemTests.Load_BadFile_GivesDefaultsAndBackup`, `SaveSystemTests.Load_CorruptFileOnDisk_CreatesSaveBak` |
| Atomic write: failure between temp write and replace keeps previous save | `SaveSystemTests.Save_ReplaceFails_KeepsPreviousFile` |
| Kill mid-run and reopen keeps settings and best score | `SettingsServiceTests.Settings_AfterARestart_AreTheSavedOnes`, `GameLoopPlayModeTests.GameOver_AfterAScoringRun_PersistsTheBestScoreAcrossARestart`, `BestScoreConsolidationTests.Save_MidRunBeatingRecord_PersistsPreviousBest`; real kill: Manual (device checklist) |
| Changing a setting raises exactly one SettingsChanged and one save | `SettingsServiceTests.Set_Change_RaisesOneEventAndOneSave` |
| No persistentDataPath use outside FileSaveStorage/GameInstaller | `TestHygieneTests.Tests_Always_DoNotDependOnTheClockTheGlobalRandomOrTheDataFolder`, run-tests.ps1 persistentDataPath grep check |
| 0 allocations per frame from services during gameplay | `FullRunAllocationPlayModeTests.Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget` |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #27 Touch input polish
| Criterion | Test |
|---|---|
| Held piece stays in jar for any touch X, even far outside screen | `DropControllerPlayModeTests.Touch_AtAnyXIncludingFarOutsideTheScreen_NeverTakesThePieceOutOfTheJar`, `DropControllerTests.FixedTick_WithAnOffsetAndAFingerFarOutside_StaysInsideTheJar` |
| Press starting on UI never drops; play-area press ending over UI drops on release | `PointerInputReaderPlayModeTests.Touch_BeginningOverAUiElement_NeverDrops`, `PointerInputReaderPlayModeTests.Touch_BeginningOnThePlayAreaAndEndingOverTheUi_DropsOnRelease`, `DropInputStateTests.PointerReleased_PressBeganOnThePlayAreaAndEndsOverTheUi_Drops` |
| Second finger and touch cancel behave as specified | `PointerInputReaderPlayModeTests.Touch_SecondFinger_IsIgnored`, `PointerInputReaderPlayModeTests.Touch_Cancelled_RaisesCancelAndNeverDrops`, `DropControllerTests.Tick_AfterACancelledPress_DoesNotDrop` |
| Finger offset and left-handed move the reference; defaults match GDD | `DropInputStateTests.GetPointerOffset_FingerOffsetLeftHanded_PutsThePieceRightOfTheFinger`, `DropInputStateTests.GetPointerOffset_FingerOffsetRightHanded_PutsThePieceLeftOfTheFinger`, `PointerInputReaderPlayModeTests.Touch_WithTheFingerOffsetConfigured_ReportsTheOffsetOfTheHand`, `GameConfigDropTests`, `SettingsServiceTests.Defaults_NewSave_MatchGdd` |
| Back button raises BackPressed exactly once per press | `PointerInputReaderPlayModeTests.Back_Escape_RaisesBackPressedOncePerPress`, `DropInputStateTests.BackKeyPressed_EachPress_RaisesBackOnce` |
| Input-to-motion latency of 1 frame or less on a real phone | Manual (device checklist, latency on phone) |
| 0 allocations per frame in the input path | `PointerInputReaderPlayModeTests.Update_WithAMovingFinger_AllocatesNoManagedMemory`, `DropControllerTests.TickAndFixedTick_WhileAiming_AllocateNoManagedMemory` |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #28 Guide line and landing ghost
| Criterion | Test |
|---|---|
| Ghost sits where a straight drop first touches (within 0.05 unit of real drop) | `GuideLineViewPlayModeTests.Ghost_OverTheFloor_SitsWhereARealDropComesToRest`, `GuideLineViewPlayModeTests.Ghost_OverAnotherPiece_SitsWhereARealDropFirstTouchesIt` |
| Toggling setting hides/shows guide immediately | `GuideLineViewPlayModeTests.SetSettingOn_TurnedOffAndOn_HidesAndShowsTheGuideImmediately`, `GuideLineAimTests.ShouldShow_WithTheSettingOff_IsFalse` |
| Toggle persists via #26 | `SettingsServiceTests.SetBool_ByKey_WritesTheMatchingToggle`, `SettingsServiceTests.Settings_AfterARestart_AreTheSavedOnes` |
| Line and ghost never leave the jar | `GuideLineViewPlayModeTests.Ghost_AtBothWalls_StaysInsideTheJar` |
| Never visible while the piece is not held | `GuideLineViewPlayModeTests.Refresh_BeforePressingWhilePressingAndAfterTheDrop_ShowsTheGuideOnlyWhilePressing`, `GuideLineViewPlayModeTests.Refresh_WhenTheControllerIsDisabled_HidesTheGuide`, `GuideLineAimTests.ShouldShow_WithoutAHeldPiece_IsFalse` |
| 0 GC allocations per frame with guide visible | `GuideLineViewPlayModeTests.Refresh_WhileVisibleAndTheXChanges_AllocatesNothing` |
| Crisp (no blur/shimmer) at 1080x1920 and 720x1280 | Manual (screenshots at both resolutions) |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #29 Audio pipeline
| Criterion | Test |
|---|---|
| Every SfxId plays through pool; 20 rapid requests never exceed 8 voices or throw | `AudioManagerTests.PlaySfx_EveryId_DoesNotThrow`, `AudioManagerTests.PlaySfx_TwentyRapidRequests_NeverExceedEightVoices`, `AudioManagerTests.PlaySfx_WithAllVoicesBusy_StealsTheOldest` |
| Volume mapping 1.0 to 0 dB, 0.5 to about -6 dB, 0 to -80 dB | `AudioMathTests.VolumeToDb_FullVolume_IsZero`, `AudioMathTests.VolumeToDb_HalfVolume_IsAboutMinusSix`, `AudioMathTests.VolumeToDb_Zero_IsMinus80` |
| Landing rate limit enforced | `LandLimiterTests`, `AudioManagerTests.PlaySfx_LandInsideTheInterval_IsDropped` |
| Merge pitch formula matches GDD for tiers 0-10 and combo cap 5 | `AudioMathTests.MergePitch_WithoutCombo_FollowsTheTier`, `AudioMathTests.MergePitch_PerComboStep_RaisesOneSemitone`, `AudioMathTests.MergePitch_AboveTheComboCap_StopsAtFiveSteps` |
| Volume change in settings applies immediately | `AudioManagerTests.Volumes_WhenTheSettingsChange_FollowAtOnce` |
| Music ducks on game over and resumes on retry | `GameLoopPlayModeTests.GameOver_WithAudio_DucksTheMusicAndRetryRestoresIt`, `AudioManagerTests.DuckMusic_ThenRestore_LowersAndRestoresTheMusicOnly` |
| Pausing the game pauses music | `AudioManagerTests.PauseMusic_ThenResume_PausesAndContinues`; wiring pause to PauseMusic: **Gap** (no test asserts the pause flow calls PauseMusic) |
| No GC allocations playing a sound after warm-up | `AudioManagerTests.PlaySfx_AfterWarmUp_AllocatesNothing`, `FullRunAllocationPlayModeTests.Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget` |
| No hitch on first sound (clips preloaded) | `SoundBankTests.LoadAsync_WithNumberedClips_GroupsThemAsVariantsInNameOrder`, `BootToGamePlayModeTests.Boot_FromTheInstaller_StartsTheAudioAndTheSceneBankLivesWithTheScene` (preload yes; hitch itself Manual) |
| Audio import settings match GDD 11 (editor validator) | `AudioValidatorTests.ShippedAudio_Always_PassesTheValidator` |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #30 Haptics wrapper
| Criterion | Test |
|---|---|
| Setting off gives zero native calls | `HapticsTests.Play_SettingOff_MakesNoNativeCall` |
| Throttle and heavier-wins rules with fake clock | `HapticsTests.Play_SameKindWithinWindow_IsDropped`, `HapticsTests.Play_HeavierWithinWindow_Wins`, `HapticsTests.Play_LighterWithinWindow_IsDropped`, `HapticsTests.Play_AfterWindow_PlaysAgain` |
| Each HapticKind felt and distinguishable on Android device; iOS pending | Manual (device checklist, Android phone; iOS pending) |
| Editor/unsupported devices: no exceptions, no warnings | `HapticsTests.NullBackend_AnyKind_DoesNothing`, `HapticsTests.Play_BackendThrows_WarnsOnceAndDisables` |
| Manifest has VIBRATE and no network permission | `HapticsTests.AndroidManifests_Haptics_VibrateWithoutNetwork`, `AndroidBuildToolsTests.FindProblems_InternetAdded_ReportsIt`, `AndroidBuildToolsTests.FindProblems_VibrateMissing_ReportsIt`, run-tests.ps1 INTERNET manifest check |
| 0 allocations per Play call after warm-up | `HapticsTests.Play_AfterWarmUp_AllocatesNothing` |

## #31 Piece animations
| Criterion | Test |
|---|---|
| Physics outcome identical with animations on and off for same seed (golden) | `PieceAnimationSimulationPlayModeTests.Run_WithAnimationsOnAndOff_GivesIdenticalPhysics`, `M2DeterminismPlayModeTests.Run_WithAVariant_GivesTheResultOfAllM2Off`, `SimulationDeterminismPlayModeTests.Run_WithTheGoldenSeedAndDrops_MatchesTheGoldenFile` |
| Each animation matches GDD 9 durations within one frame (fake clock) | `PieceAnimatorPlayModeTests.Spawn_AfterCreate_GrowsFromZeroWithOvershootAndEndsAtOneInTheDuration`, `PieceAnimatorPlayModeTests.MergePop_AfterAMerge_PeaksAtTheConfiguredScaleAndEndsAtOneInTheDuration`, `PieceAnimatorPlayModeTests.Land_AboveTheThreshold_SquashesForTheDurationAndReturnsToOne`, `PieceAnimatorPlayModeTests.Drop_WhileFalling_StretchesUntilItLands`, `FeedbackConfigTests.Defaults_Always_AreTheGddDurations` |
| After 1,000 drops all pooled pieces at scale 1; no stuck scale or duplicate | `PieceAnimationSimulationPlayModeTests.Run_WithAThousandDrops_LeavesEveryPooledPieceAtScaleOne`, `PieceAnimationSimulationPlayModeTests.Merge_OfTwoPieces_ShowsTwoGhostsThatThenDisappear`, `PieceAnimatorPlayModeTests.Release_WhileAnimating_ResetsTheVisualAndReuseStartsClean` |
| 0 GC allocations per frame during animations | `PieceAnimatorPlayModeTests.Tick_WhileAnimating_AllocatesNothing`, `FullRunAllocationPlayModeTests.Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget` |
| No sprite blur/shimmer at rest at 1080x1920 and 720x1280 | Manual (screenshots at both resolutions) |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #32 Pooled particles and flash rings
| Criterion | Test |
|---|---|
| Effects at right position/colour/count for tiers 0-10 (fake spawner) | `FeedbackParticlesPlayModeTests.Merge_OfEveryTier_EmitsBurstInTierColourAndFlashRing`, `FeedbackParticlesPlayModeTests.Landing_OfEveryTier_EmitsLightenedDustAtTheFloor`, `FeedbackParticlesPlayModeTests.Supernova_OfTwoBlackHoles_EmitsFlashAndShockwave`, `FxPrefabTests.MergeBurstCount_AcrossTiers_GrowsWithinTheRange` |
| 1,000 drops with all effects: no pool growth, no warning | `ParticleSimulationPlayModeTests.Run_WithAThousandDrops_StaysInsideTheCapsAndLeavesNothingAlive`, HarnessTestBase (warning fails the test) |
| 0 GC allocations per frame with effects | `ParticleSpawnerPlayModeTests.Burst_OfEveryEffect_AllocatesNothing`, `FeedbackParticlesPlayModeTests.Handlers_CalledManyTimes_AllocateNothing`, `FullRunAllocationPlayModeTests.Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget` |
| Draw calls at max effect load at most 20 (Frame Debugger) | Manual (Frame Debugger screenshot/note in PR) |
| Particle counts respect cap under chain of 10 merges | `ParticleSimulationPlayModeTests.Run_WithAChainOfTenMerges_FillsTheParticleCapAndNeverExceedsTheCaps`, `ParticleSpawnerPlayModeTests.Burst_PastTheCaps_FillsThemExactly` |
| Effects do not change physics or score (golden unchanged) | `ParticleSimulationPlayModeTests.Run_WithParticlesOnAndOff_GivesIdenticalPhysicsAndScore`, `M2DeterminismPlayModeTests.Run_WithAVariant_GivesTheResultOfAllM2Off` |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #33 Screen shake, slow-mo and screen flash
| Criterion | Test |
|---|---|
| Shake offsets multiples of 1/16 unit and return exactly to zero | `ShakeCoreTests.Evaluate_DuringAShake_IsAlwaysAMultipleOfOneSixteenth`, `ShakeCoreTests.Evaluate_AfterTheDuration_IsExactlyZero`, `ScreenShakePlayModeTests.Shake_ThenWaiting_SnapsToSteps_AndReturnsExactlyToRest` |
| ReduceShake: no shake, no slow-mo | `FeedbackScreenFxPlayModeTests.ReduceShake_WhenOn_SuppressesShakeAndSlowMoButNotTheFlash` |
| ReduceShake: Danger Line pulses at 2 Hz (fake clock) | `DangerLineTests.EvaluateColor_WithSoftRate_PeaksAndTroughsAt2Hz`, `FeedbackDirectorPlayModeTests.Danger_WhileOverflowing_TicksAtTheRateOfTheSetting` |
| Pause during slow-mo: timeScale equals 1 after Resume and gameplay continues | `TimeScaleOwnerTests.Paused_DuringSlowMo_ResumesAtNormalSpeed`, `PauseFlowPlayModeTests.Pause_DuringSlowMo_EndsWithTimeScaleOneAfterResume`, `HarnessPausePlayModeTests.Pause_DuringASlowMo_CancelsItAndTheScaleIsOneAfterTheResume` |
| Burst of 10 high-tier merges never exceeds flash-rate limit or shake cap | `FeedbackScreenFxPlayModeTests.BurstOfTenHeavyMerges_NeverExceedsTheShakeCapOrTheFlashRate`, `FlashCoreTests.TryFlash_BurstOfTenInOneSecond_StartsAtMostThree`, `ShakeCoreTests.Evaluate_WithManyShakes_NeverExceedsTheCap` |
| Framing/jar position unchanged after effect ends | `ScreenShakePlayModeTests.Clear_DuringAShake_PutsTheRigAtRest`, `ScreenShakePlayModeTests.Shake_ThenWaiting_SnapsToSteps_AndReturnsExactlyToRest` |
| 0 GC allocations per frame | `ScreenShakePlayModeTests.LateUpdate_WhileShakingAndFlashing_AllocatesNothing`, `FullRunAllocationPlayModeTests.Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget` |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #34 FeedbackDirector
| Criterion | Test |
|---|---|
| Each GDD 9 table row asserts exact audio/haptic/fx calls | `FeedbackDirectorPlayModeTests`, `FeedbackEndToEndPlayModeTests.Run_WithAComboAHeavyMergeASupernovaAndAGameOver_PlaysTheWholeTable` |
| Chain merge gives a single audio + haptic bundle per step, highest tier | `FeedbackDirectorPlayModeTests.Merge_ChainInOneStep_PlaysOneBundleWithTheHighestTier` |
| Simulation golden identical with and without director | `FeedbackSimulationPlayModeTests.Run_WithAndWithoutTheDirector_GivesTheIdenticalResult`, `M2DeterminismPlayModeTests.Run_WithAVariant_GivesTheResultOfAllM2Off` |
| After 3 Retry() runs no extra listeners or leaked objects | `RunLifecycleHarnessPlayModeTests.Retry_ThreeConsecutiveRuns_StartEachRunFromTheSameState`, `GameLoopPlayModeTests.Retry_ThreeConsecutiveRuns_LeaveNoPiecesOrSubscribers`, `BootToGamePlayModeTests.Boot_ThenRetriesAndAPause_KeepsTheM2ServicesAndReleasesThemWithTheScene`, `FeedbackSimulationPlayModeTests.Run_WithRestarts_PlaysTheGameOverSoundOnEveryRun` |
| 2 minutes play: 0 GC allocations per frame | `FullRunAllocationPlayModeTests.Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget` |
| 2 minutes play: 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |
| Playable and fun on a real device (subjective) | Manual (device checklist, subjective check in PR) |

## #35 Pause
| Criterion | Test |
|---|---|
| Pause freezes physics, timers, overflow, cooldown; Resume continues exactly (fake clock) | `HarnessPausePlayModeTests.Step_WhilePaused_LeavesTimeScoreAndPiecesUnchanged`, `HarnessPausePlayModeTests.Play_WithAScriptedPause_DoesNotAdvanceTimeDuringIt`, `PauseFlowPlayModeTests.Pause_WhileThePiecesFall_FreezesThemUntilResumed`, `RunLifecycleHarnessPlayModeTests.PauseAndResume_InTheMiddleOfARun_LeaveNothingBehind`, `OverflowDetectorPlayModeTests.Disable_FreezesTheTimers`, `DropControllerTests.Tick_WithFrozenTime_KeepsTheInputBlocked` |
| Pausing during slow-mo ends with timeScale 1 after Resume | `PauseFlowPlayModeTests.Pause_DuringSlowMo_EndsWithTimeScaleOneAfterResume`, `HarnessPausePlayModeTests.Pause_DuringASlowMo_CancelsItAndTheScaleIsOneAfterTheResume`, `TimeScaleOwnerTests.Paused_DuringSlowMo_ResumesAtNormalSpeed` |
| Back opens Pause in play, closes dialogs, closes Settings to Pause | `PauseFlowPlayModeTests.Back_InPlayAndInPause_OpensThenClosesThePanelsInOrder`, `PauseFlowPlayModeTests.Back_InSettings_ClosesTheScreenThenResumes`, `PausePresenterPlayModeTests.HandleBack_WithADialogOpen_CancelsIt`, `PausePresenterPlayModeTests.HandleBack_InSettings_ClosesTheScreenOnly`, `PanelStackTests` |
| Backgrounded mid-run auto-pauses and save is written | `PauseFlowPlayModeTests.Interruption_WhilePlaying_PausesAndSavesAndStaysPaused`, `PauseFlowPlayModeTests.Interruption_WhileGameOver_DoesNotPause` |
| Restart asks confirmation; confirming gives fresh run with same object/listener counts | `PausePresenterPlayModeTests.Restart_WhenClicked_AsksForConfirmationFirst`, `PauseFlowPlayModeTests.Restart_WhenConfirmed_StartsAFreshRunWithTheSameListeners` |
| Tapping Resume never drops a piece | `PauseFlowPlayModeTests.Resume_ThenAPressInsideTheGrace_DropsNoPiece` |
| Touch targets at least 44 px; readable at 3 resolutions with safe area | `SafeAreaFitterTests` (safe area maths only); 44 px targets and layout: Manual (screenshots at 1080x1920, 1080x2400, 720x1280); 44 px size check **Gap** (no automated size assert) |
| 0 GC allocations per frame while view is open | **Gap** (no allocation test with the pause view open; `FullRunAllocationPlayModeTests` does not open it) |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #36 SettingsView
| Criterion | Test |
|---|---|
| Every control shows saved value on open and persists after restart (fake save storage) | `SettingsPresenterPlayModeTests.Open_WithSavedValues_ShowsThemWithoutWritingThem`, `SettingsPresenterPlayModeTests.Changes_AfterARestart_AreShownAgain`, `SettingsServiceTests.Settings_AfterARestart_AreTheSavedOnes` |
| Volume change affects mixer values | `AudioManagerTests.Volumes_WhenTheSettingsChange_FollowAtOnce`, `SettingsPresenterPlayModeTests.Slider_WhenDragged_WritesTheVolume` |
| Haptics off is a no-op | `HapticsTests.Play_SettingOff_MakesNoNativeCall`, `SettingsPresenterPlayModeTests.Toggle_WhenSwitched_WritesOnlyItsSetting` |
| Guide line setting changes visibility | `GuideLineViewPlayModeTests.SetSettingOn_TurnedOffAndOn_HidesAndShowsTheGuideImmediately` |
| Reduce shake disables shake | `FeedbackScreenFxPlayModeTests.ReduceShake_WhenOn_SuppressesShakeAndSlowMoButNotTheFlash` |
| Left-handed changes finger offset side | `DropInputStateTests.GetPointerOffset_FingerOffsetLeftHanded_PutsThePieceRightOfTheFinger`, `SettingsServiceTests.FingerOffset_Change_RaisesOneEventAndPersists` |
| Reset progress confirms, clears bests/totals, keeps settings, updates HUD best | `PauseFlowPlayModeTests.ResetProgress_WhenConfirmedFromSettings_ErasesProgressKeepsSettingsAndRefreshesTheHud`, `PausePresenterPlayModeTests.ResetProgress_WhenClicked_AsksForConfirmationFirst`, `SettingsServiceTests.ResetProgress_AfterRuns_KeepsSettings` |
| Layout correct at 3 resolutions, no overlap, safe area | `SafeAreaFitterTests` (maths only); layout: Manual (screenshots at 1080x1920, 1080x2400, 720x1280) |
| No gameplay input reaches the game while view is open | `PauseFlowPlayModeTests.Pause_WhileThePiecesFall_FreezesThemUntilResumed`, `PausePresenterPlayModeTests.Settings_WhenClicked_OpensTheScreenOverThePauseMenu` (game paused; no direct assert of input blocking: **Gap**) |
| 0 GC allocations per frame while open | **Gap** (no allocation test with Settings open) |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #37 HUD and Game Over polish
| Criterion | Test |
|---|---|
| Count-up ends on exact score, respects 0.3 s (fake unscaled clock) | `CountUpTests.Tick_AtOrAfterTheDuration_EndsOnTheExactTarget`, `CountUpTests.Tick_AtHalfTheDuration_IsHalfWay`, `GameOverViewPlayModeTests.Show_ScoreCountUp_EndsOnTheExactScoreAfterTheDuration`, `HudPresenterPlayModeTests.ScoreChanged_AfterADrop_RollsTheScoreLabelToTheExactValue` |
| Skipping/pausing jumps to exact final value | `CountUpTests.Snap_DuringARoll_ShowsTheValueAndStops`, `HudPresenterPlayModeTests.SkipAnimations_WhileTheScoreRolls_ShowsTheExactScore`, `GameOverViewPlayModeTests.Show_WithTimeScaleZero_FadesInOnUnscaledTime` |
| NEW BEST and confetti only when IsNewBest is true | `GameOverViewPlayModeTests.Show_BannerFollowsIsNewBest`, `FeedbackParticlesPlayModeTests.GameOverReady_AfterANewBest_EmitsConfettiOnce`, `FeedbackParticlesPlayModeTests.GameOverReady_WithoutANewBest_EmitsNoConfetti`, `RunSummaryTests.From_AboveTheBest_IsNewBestWithTheScoreAsBest` |
| Game Over view appears 1.2 s after game-over event | `GameManagerTests.Tick_AfterTheDelay_RaisesGameOverReadyOnce`, `GameLoopPlayModeTests.GameOver_AfterTheDelay_ShowsTheViewAndRetryHidesIt`, `FeedbackConfigTests.GameOverDelay_WithGrowingDepth_GrowsFromZeroToTheSweepDuration` |
| Retry works exactly once, including double tap | `GameOverViewPlayModeTests.Retry_WhenTappedTwice_RaisesRetryClickedOnce`, `GameOverViewPlayModeTests.Retry_WhenClickedAfterTheLock_RaisesRetryClickedOnce` |
| HUD renders at 3 resolutions, safe area respected | `SafeAreaFitterTests` (maths only); rendering: Manual (screenshots at 1080x1920, 1080x2400, 720x1280) |
| 0 GC allocations per frame in 2-minute run and during count-up | `FullRunAllocationPlayModeTests.Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget`, `CountUpTests.Tick_WhileRolling_AllocatesNothing`, `HudPresenterPlayModeTests.ScoreChanged_WhileShowingScoreAndCombo_AllocatesNothing` |
| 0 console errors/warnings | HarnessTestBase (warning fails the test); Unity fails a test on any error log |

## #38 Android build pipeline
| Criterion | Test |
|---|---|
| build-android.ps1 produces installable APK from clean checkout; non-zero exit on failure | Manual (run `Tools/build-android.ps1` with Editor closed, and a failing build) |
| Installs, boots through Boot, loads Game via Addressables, reaches Playing on device | `BootToGamePlayModeTests.Boot_FromTheInstaller_LoadsTheGameSceneAndReachesPlaying` (Editor); on-device: Manual (device checklist) |
| Base build at most 30 MB (record size) | Manual (record real size in PR); helper covered by `AndroidBuildToolsTests.FormatMegabytes_OneAndAHalfMegabytes_PrintsTwoDecimals`; budget assert in build script **Gap** if not enforced in a test |
| Merged manifest has VIBRATE, no INTERNET; check fails when added | `AndroidBuildToolsTests.FindProblems_InternetAdded_ReportsIt`, `AndroidBuildToolsTests.FindProblems_VibrateMissing_ReportsIt`, `AndroidBuildToolsTests.FindProblems_OnlyVibrate_ReturnsNone`, `AndroidBuildToolsTests.ParsePermissions_Aapt2Output_ReturnsDistinctNames`, `AndroidManifestCheck` (editor tool), run-tests.ps1 INTERNET check |
| Build Settings contain only Boot; C-01 grep checks green | `ProjectFoundationTests.BuildSettings_Always_ContainOnlyEnabledBootScene`, `ProjectFoundationTests`, `TestHygieneTests.Tests_Always_LoadThroughTheServicesOnly`, run-tests.ps1 C-01 grep checks |
| Addressables Analyze reports no fixable issues; group sizes recorded | Manual (run Addressables Analyze via `AddressablesAnalyze` tool; sizes in PR); group-name helper `AndroidBuildToolsTests.GroupNameOf_BundleFileName_ReturnsGroup` |
| README has "Building for Android" section; no secrets committed | **Gap** (no automated doc/secret check; Manual review of README and AndroidSigning) |

## #39 Device performance and pixel-quality pass
| Criterion | Test |
|---|---|
| 60 FPS median and p95 at most 20 ms in 10-minute device run, evidence in PR | Manual (device run, Profiler capture or CSV); sampler maths in `DebugOverlayPlayModeTests.Sample_AfterOneWindow_ReportsFpsWorstFrameAndAllocations` |
| Zero GC allocations per frame in 2-minute run; allocation test passes | `FullRunAllocationPlayModeTests.Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget`; device Profiler: Manual |
| At most 60 rigidbodies and 20 draw calls in worst case | `OverflowDetectorPlayModeTests.Evaluate_With60Pieces_AllocatesNothing` (60 pieces only as a load); draw calls: Manual (Frame Debugger/device); rigidbody cap assert **Gap** |
| Low-end detection by unit test (faked SystemInfo) and Volume off in Low | `QualityTierTests.Classify_FewProcessors_IsLow`, `QualityTierTests.Classify_LittleMemory_IsLow`, `QualityApplierPlayModeTests.Apply_LowTier_DisablesTheVolumeAndLowersTheParticleCount`, `BootToGamePlayModeTests.Boot_OnALowDevice_DisablesThePostProcessingOfTheScene` |
| Pixel-quality screenshots at 3 resolutions show no artifacts | Manual (screenshots at three resolutions in PR) |
| Debug overlay absent from release builds | run-tests.ps1 OnGUI-outside-DebugOverlay grep check; release-build absence itself: Manual (verify in a release build) |
| 0 console errors/warnings in device logs | Manual (`adb logcat` filtered by the app) |

### Gaps found by the mapping

None of these blocks the M2 gate by itself; each is either a candidate for a new issue or an item to accept explicitly.

| Issue | Gap | Suggested follow-up |
|---|---|---|
| #35 | 0 GC allocations per frame while the pause view is open | New issue: allocation test with the view open |
| #36 | 0 GC allocations per frame while Settings is open | Same issue as above |
| #35 | Touch targets of at least 44 px (no size assert) | New issue: assert the size of every button rect |
| #36 | No gameplay input reaches the game while Settings is open (only indirect: the game is paused) | New issue: direct input-blocking test |
| #29 | Pausing the game calls `PauseMusic` (the manager is tested, the wiring is not) | New issue: pause-flow test with `FakeAudioService` |
| #38 | Base build at most 30 MB: only the formatting helper is tested | Manual: record the real size in the PR |
| #38 | README section and no secrets committed | Manual review |
| #39 | At most 60 rigidbodies: no assert; draw calls are manual | Manual with the overlay; optional assert in the harness |

### The 10 scope items of #40

| Item | Where |
|---|---|
| 1 Harness with and without M2 systems gives the identical result | `M2DeterminismPlayModeTests` (all M2 off vs all on) |
| 2 Determinism with pauses and slow-mo | `M2DeterminismPlayModeTests` (variants `AllM2OnWithPauses`, `AllM2OnWithSlowMo`, `AllM2OnWithPausesAndSlowMo`, 5 seeds), `HarnessPausePlayModeTests` |
| 3 SaveSystem tests | `SaveSystemTests`, `SettingsServiceTests.Settings_AfterARestart_AreTheSavedOnes`, `GameLoopPlayModeTests.GameOver_AfterAScoringRun_PersistsTheBestScoreAcrossARestart` |
| 4 Input tests | `PointerInputReaderPlayModeTests`, `DropControllerPlayModeTests`, `DropInputStateTests` (already complete from #27) |
| 5 Feedback table end to end | `FeedbackEndToEndPlayModeTests` |
| 6 Lifecycle: 3 Retry runs and a Pause/Resume | `RunLifecycleHarnessPlayModeTests` (5 seeds; particle pools, listeners, time scale), `BootToGamePlayModeTests` (audio voices, M2 services released with the scene) |
| 7 `AllocatesNothing` over a full scripted run | `FullRunAllocationPlayModeTests` |
| 8 Grep checks in `run-tests.ps1` | `INTERNET`, `OnGUI`, `UnityEngine.Random` in gameplay, `persistentDataPath` |
| 9 This document | Part 3 holds the checklist |
| 10 Golden file unchanged | `SimulationDeterminismPlayModeTests.Run_WithTheGoldenSeedAndDrops_MatchesTheGoldenFile`; `git diff` on `Assets/Tests/PlayMode/Golden/simulation-seed-1234.txt` is empty |

## Part 2: the gate

Run with the Editor closed, from the repository root:

```powershell
./Tools/run-tests.ps1               # source grep checks, EditMode, PlayMode; exit code 0 only if all pass
./Tools/run-tests.ps1 -Repeat 10    # flakiness: the whole run ten times
```

| Check | Result | Date / notes |
|---|---|---|
| Source grep checks green on a clean tree | Passed (2026-10-07) | Printed at the start of every run |
| Each rule makes the script exit non-zero when broken (temporary file) | Shown for `OnGUI`, `UnityEngine.Random`, `Random.Range`, `persistentDataPath` and `INTERNET`: exit 1 each time | |
| EditMode and PlayMode green | PlayMode 308/308 passed. EditMode 556/557: `CountUpTests.Tick_WhileRolling_AllocatesNothing` failed (Editor allocation noise, see `LEARNINGS.md` #39) | Signed off by the owner on 2026-10-07 |
| PlayMode suite under 3 minutes | Measured 281 s, over the 3 minute target (the new #40 tests add about 20 s) | Signed off by the owner on 2026-10-07 |
| `-Repeat 10`: no flaky test | Not completed: stopped during run 1, which showed the same CountUp failure | Signed off by the owner on 2026-10-07 |
| 0 console errors/warnings in the runs | Not measured separately; harness tests fail on any warning and all of them passed | Signed off by the owner on 2026-10-07 |

## Part 3: on-device checklist (signed off)

**Signed off as successful by the owner on 2026-10-07.** The figures and notes below were not recorded in this document (device, Android version, FPS, sizes, subjective notes); the ticks are the owner's sign-off, not measurements made by the assistant that wrote the tests.

Issue #39 has its own checklist, `device-checklist-39.md` (performance, low-end tier, pixel quality, logs). This one covers
the M2 exit criterion as a whole: **"60 FPS on target device; feels good for 10 min of play"**.

- [x] 10 minutes of continuous play, no stutter, no crash.
- [x] Pause and Resume during play and during a slow-mo: the piece stays where it was, no drop on Resume, time back to normal
- [x] Background and foreground (home button, then back): the game is paused, nothing was lost
- [x] Kill the app mid-run and reopen: settings and best score are kept
- [x] Volume sliders change what is heard at once (Master, Music, SFX)
- [x] Haptics toggle: off gives no vibration, on gives distinguishable vibrations
- [x] Guide line toggle shows and hides the guide at once and is kept after a restart
- [x] Reduce Shake, Left-handed and Finger offset behave as labelled
- [x] Layout and pixel quality at 1080x1920: not recorded
- [x] Layout and pixel quality at 1080x2400: not recorded
- [x] Layout and pixel quality at 720x1280: not recorded
- [x] A run with the Addressables Play Mode Script set to **Use Existing Build** (build the groups first): boots, plays, no missing-asset error
- [x] Release build size (budget 30 MB) and `adb logcat` errors and warnings: not recorded
- [x] Feels good? (subjective, a few lines): not recorded

**M2 exit criterion ("60 FPS on target device; feels good for 10 min of play"):** confirmed by the owner on 2026-10-07. Gaps:

- The gaps of Part 1 (allocation and 44 px checks of the pause and settings views, input blocking while Settings is open, `PauseMusic` wiring, rigidbody cap), the 3 minute PlayMode target and the `-Repeat 10` check were accepted by the owner and are not new issues yet.
