# Baseline environment inspection

Inspection date: 2026-11-02. Source: `d34b688c9411817e02f84a2f49b8d1cd7f90192a` in `C:/Users/song/RhythmDojo-pr-quality`.

This is static environment evidence, not a passing Unity test result.

- Unity project version: 6000.3.23f1, revision 09d2ecc7fb28; installed executable exists.
- Manifest and lock: Test Framework 1.6.0; Pipeline 0.8.0-exp.1; Input System 1.20.0; uGUI 2.0.0.
- Enabled build-settings scenes: Title, Bootstrap, SongSelection, Gameplay, Settings, Results, ChartEditor.
- Six tracked WAV assets exist under Assets/Game/Audio (nine repository-wide, including three under Resources/UI). TestPulse and TempoPulse each contain 1,058,444 bytes; DemoMelody 2,646,044; DojoBoardCrack 22,094; DojoDoorSlide 48,554; DojoKnock 14,156. Their existing meta files are present.
- WMI Win32_SoundDevice reports OK for two USB audio devices, NVIDIA High Definition Audio, Steam Streaming Microphone, Steam Streaming Speakers, and Realtek High Definition Audio. This does not identify the selected Unity output device or establish audible output.
- LocalAudio/PUPA.wav does not exist. Existing real-PUPA tests explicitly Ignore without it. New external-audio tests must generate temporary WAV files; existing AudioTimelineTests and ChartEditorMenuTests contain WAV-writer examples.
- Main agent launched baseline process 13084 using FoundationVerification.RunTestsOnly. Log: baseline-editor.log in this directory. Result pending at initial inspection.
- FoundationVerification writes Logs/verification-editmode.xml, Logs/verification-playmode.xml and Logs/verification.txt. It stops before PlayMode if EditMode fails, skips, or is inconclusive. A separate PlayMode run is needed to fill that missing baseline scope if this occurs.
- The Foundation BuildPlayer method uses an explicit scene list which omits Results despite its enabled EditorBuildSettings entry. This is an observed source discrepancy, not yet a reproduced player failure.

SHA-256 of verification WAV assets:

| Asset | SHA-256 |
| --- | --- |
| TestPulse.wav | 9D8FBFB80536375648BDEB9589ECD3633DC3AACFC191137BCAF82DC38CFD28DA |
| TempoPulse.wav | 441B2D46D0DAC419F75324C5470BEB89D68AF241B6A583B37DC7A3290E8A20FE |
| DemoMelody.wav | E754311295D4B9E9247AF249DFCD00A76FA12CACD6D34E86DFD5EBE9A5F74B9C |
| DojoBoardCrack.wav | D754D4F373EB65C0E062DEADB63C6FB83327D53FECADF9BBFA12E73352326EF1 |
| DojoDoorSlide.wav | 3A16C3A5A947D4539199D76FBC5ED7CD648CC3EBA71B1929444CDC542DEC143A |
| DojoKnock.wav | 84ECA07A128B2159BC2FB72C21E7656EE6E8C6836F3F49A5B2074F23F8A477E6 |

## Baseline execution result (2026-11-03 KST)

RunTestsOnly finished and process 13084 exited. EditMode: 241 passed, 0 failed/skipped. PlayMode: 114 passed, 7 failed, 3 skipped. This is not a full pass. XML, report, failure details and generated binary diff are preserved here as baseline-editmode.xml, baseline-playmode.xml, baseline-verification.txt, baseline-failures.txt, baseline-generated.patch.

The seven failures include one explicit batchmode WaitForEndOfFrame limitation, four cursor failures (focus=False observed), one Results transition timeout, and one keyboard song-selection mismatch. Three PUPA tests were ignored because the private external file is absent. General Editor retries are pending; causes are not assumed resolved.

CLI run_tests filter accepts a partial substring, not an alternation of names: a combined filter returned zero tests. That invocation is not a pass. Retry fixtures/methods individually.

General Editor retries: cursor 5/5 and render 1/1 passed; Results transition and keyboard song selection each failed again (JSON evidence in this directory). Editor PID1424 ready and PlayMode exited after each run. Generated tracked mutations restored after preserving patch. Existing product-source failures remain open for final verification. Independent baseline reviewer findings about generated scenes and WAV count were corrected.
