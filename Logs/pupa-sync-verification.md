# PUPA timing and outcome transition — 2026-10-28

PUPA's bundled chart and the existing local saved PUPA project now use an audio offset of -0.02 seconds. Note times, lanes, hold durations, IDs and interfaces are preserved. The saved project was backed up to `Logs/bc00bd09-6551-4a23-88ba-2eb029dbc3ad-before-pupa-sync.rdchart.json`; the update verified that the audio hash and all notes match and changed only the audio offset.

The tested WAV has SHA-256 `108f2b729b9ed635c04856e7d15e4ec8283b8640323b66cda7d335d9d6c13a10`, 48 kHz stereo, and 6,313,971 frames. Its first attack is approximately 0.503 seconds; the first raw chart note is 0.4833333333333334 seconds. Whole-song energy onset comparisons showed a similar fixed displacement in the middle and final sections rather than increasing drift.

Before clock interpolation, the timestamped PUPA regression measured an input error of -0.0162557 seconds despite generating a key event at the chart time. SongClock now interpolates between DSP updates using real time and corrects its mapping toward DSP time with a one-second time constant. Presentation and input use the same corrected clock; user input calibration remains a separate single subtraction.

Both GAME OVER and GAME CLEAR now remain visible for at least one real-time second before Results loads. Restart resets the transition deadline. This delay is independent of the paused/stopped song clock and preserves the existing completion and record behavior.

Final affected coverage: 35 distinct tests passed, zero skipped:

| Scope | Passed |
| --- | ---: |
| PupaResultsFlowTests, including the real WAV full-song run and corrected timestamped input | 6 |
| ResultsFlowTests, including visible delay, restart cancellation and return | 8 |
| GameplayJudgmentTests, including 180 ms hitch, chords, holds and restart | 5 |
| CoordinatorTests, including positive/negative user input calibration | 12 |
| Smoke navigation/input/cleanup category | 4 |

An intermediate mixed-input test reported one missing Perfect. Its event generator sampled clocks separately for simultaneous keys, allowing tiny backward timestamp rounding and discarded keyboard events. Sampling once per event batch fixed the generator; all five input tests passed together afterward. The failure assertion now includes per-note timing diagnostics.

Unity compilation and the affected diff checks passed. Test suites ran sequentially, and Editor cleanup completed. Existing unrelated staged and unstaged work was preserved. No Windows player build was requested or performed.
