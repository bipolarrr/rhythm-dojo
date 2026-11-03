# Task 4 — DSP clock-rate verification

Base: 6444e62. Scope: Application interval policy, AudioSettingsService, affected audio tests and verification documentation.

## Reproduction

Main ran the initial AudioSettingsServiceTests on the old implementation: 11 tests, 9 passed, 2 failed. Persistent 0.98 and 1.02 rates were prematurely accepted by the old one-second ±15% condition. Evidence: task4-red.json.

## Decision

Use conservative two-endpoint DSP block uncertainty E=2B/F; full interval containment in [0.99,1.01], two nonoverlapping windows. Unknown intervals extend; disjoint intervals trigger the next larger existing candidate and inspect applied configuration. A single 20-second wall-time budget includes reset, warmup, pauses and recoveries. Timeout and restored configurations never imply verified health. External configuration events invalidate displayed verification. Exposed read-only measurement history includes interval duration, DSP duration, buffer, sample rate, bounds and state.

## Validation pending main

Smallest scope: AudioSettingsServiceTests (fake backend and pure policy). Then affected AudioFlowTests, GameplayPauseFlowTests, SettingsFlowTests, SongClockResumeTests. Real AudioFlow writes full numerical evidence to Logs/audio-buffers.txt. Success requires two contained intervals; inconclusive/failed settings are explicitly reported, and a restored device configuration is not treated as verified. Main owns refresh, sequential Unity execution, evidence and commits.

## Full grouped verification
EditMode 348 passed. PlayMode 137 passed, 3 failed, 3 skipped. Two failures and three missing-PUPA skips match baseline. The additional KeySettings fixed 3.1-second wait predates the 3-second countdown plus 0.15-second DSP lead; replaced with bounded actual-resume condition and Clock.Running assertion. Targeted rerun pending. Full run is not a pass. task4-all*.xml/txt and task4-device-buffers.txt retain evidence. Independent review found no outstanding issue. Generated scene changes archived separately and restored.

Targeted KeySettingsFlowTests rerun: 2/2 passed (task4-key-flow.json). Editor exited Play Mode and compilation completed. Existing baseline failures remain for final remediation; task4 introduces no remaining regression.
