# Task 1: paused input timing

Source baseline: c569072 (product source d34b688). Runtime code currently converts ordinary input with SongClock.InputTimeToSongTime, subtracting InputTimingOffsetSeconds. RhythmSessionCoordinator.Resume instead supplies uncalibrated songTime for deferred releases. Thus offset shifts hold-tail judgments only when release occurs during pause.

Test-only stage adds CoordinatorTests coverage for 0/+0.2/-0.2 calibration, Release/Automatic hold ends, exact Perfect/Good boundaries and just outside Good, calibration changes during pause, arbitrary delayed timestamps, repeated release/resume, repress cancellation, next-pause snapshot, restart/dispose cleanup. Ordinary input at the same logical instant is the comparison oracle. No Unity run performed by the implementation agent; main owns red/green execution and evidence.

Implementation decision: one Application helper will apply the timing offset. SongClock keeps its existing public calibrated InputTimeToSongTime behavior and invokes the helper after mapping the event to logical song time. The coordinator will snapshot the calibrated pause time using that same helper; it will not reverse existing calibration by adding/subtracting the offset again.

## Red evidence

Main refreshed and executed CoordinatorTests with test-only changes: 60 total, 36 passed, 24 failed, no skips. Evidence: task1-red.json. Failures were concentrated in nonzero-offset pause-versus-ordinary comparisons and subsequent-pause exact-tail checks. This reproduces the calibration mismatch without advancing wall/countdown time in the domain.

## Implementation

Added InputTiming.Calibrate in Application and used it in SongClock.InputTimeToSongTime and the coordinator pause snapshot. The existing public clock conversion remains calibrated exactly once. The synthetic release uses pausedInputTime, captured before pausing, so later calibration changes and event timestamps do not alter its judgment time. Input is ignored after disposal, pending releases clear on Start/Restart/Dispose, and a re-press still removes a pending lane.

Resume removes each deferred lane before emitting judgment notifications. That preserves the existing enumeration order while allowing notification subscribers to restart/dispose and clear the remainder without invalidating a live enumerator. Scope excludes audio recovery and scheduled resume, which remain subsequent tasks.

Scoped git diff --check passed. Main owns green run; no Unity execution or commit by the implementation agent.

Green: CoordinatorTests 60/60; GameplayPauseFlowTests 2/2. After both runs PlayMode false and Editor ready. Independent pause_review found no actionable defect. No full suite/build repeated: scoped coordinator and pause integration tests cover this change; final Foundation remains planned.
