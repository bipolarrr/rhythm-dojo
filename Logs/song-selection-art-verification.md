# Song selection art verification

- Unity 6000.3.23f1 compilation: no errors.
- SelectionBrowserTests: 6 passed, covering rows, scrolling, favorites, sorting, record saving/display, settings restoration, back navigation and playback.
- After switching to the enhanced original pavilion background: DemoCoverAndRowsRenderAtBothResolutions passed again; captures regenerated at 1920 × 1080, 1280 × 720, 1024 × 768 and 2560 × 1080.
- Visually reviewed 1280 × 720 and 1024 × 768 final captures. The original mountain/pavilion composition is preserved and the larger information text fits beneath the title and difficulty controls. Prior widescreen capture inspection also confirmed the proportional layout.
- Final Verify Smoke Actions: 4 passed, 0 failed, 0 skipped, 0 inconclusive; verification.txt reports PASS.
- Source diff whitespace checks passed. No player build: presentation and text layout changes only.
- Artwork source and exact built-in image_gen prompt: ../Docs/selection-background-prompt.md.

## Larger metadata alongside the album

- Artist/BPM/play time increased from a 26–30 range to 34–42 reference pixels, with 1.45 line spacing and vertical centering beneath the title.
- SelectionBrowserTests.DemoCoverAndRowsRenderAtBothResolutions passed. Inspected updated captures at 1280 × 720, 1024 × 768 and 2560 × 1080: the three metadata lines fill the area beside the album without overlapping the title or difficulty controls.
- Source whitespace checks passed. Verification was limited to the affected selection rendering/playback test; no unrelated suite or player build was run.
