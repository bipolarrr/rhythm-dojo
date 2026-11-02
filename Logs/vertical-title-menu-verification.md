# Vertical title menu verification

- MainMenuTests: 5 passed, 0 failed after the vertical board layout change.
- MainMenuFlowTests: 15 passed, 0 failed. Covers closed-door logo reveal/reversal, real raycasts, editor entry, settings restoration, quit confirmation, duplicate input rejection, scene traversal, readiness and timeout recovery.
- Final Verify Smoke Actions: 4 passed, 0 failed, 0 skipped, 0 inconclusive; verification.txt reports PASS.
- Added regression coverage verifies logo and plaque halves remain in the transition and their separation equals the initial 220-unit menu gap plus actual door displacement, including local logo scale compensation, through fully open doors.
- Inspected main-menu-expanded.png: four narrow vertical boards with cream vertical labels sit between the split logo halves while doors remain closed. Inspected main-menu-door-opening.png: opening doors reveal the interior without dummies or a 수련장 plaque; logo geometry leaves through the side clip with the doors.
- Source whitespace diff checks passed. No player build was run for title presentation changes.
- Artwork and exact built-in image_gen prompt: ../Docs/vertical-menu-board.md.

## Logo/menu overlap fix

- Menu sibling order changed to Doors → Menu Buttons → Doorway Logo Clip → Entrance Frame. The logo therefore occludes the plaques during reveal and reversal.
- Added an EditMode assertion for the menu/door/logo draw order. MainMenuTests: 5 passed.
- SlidingDoorsRevealDummiesAndReverseWithoutStartingEarly: 1 passed. Visually inspected main-menu-logo-opening.png and main-menu-logo-closing.png; no menu lettering or board pixels render over the logo halves.
- No full-suite rerun or player build: only visual sibling order changed, with the affected reveal/reversal behavior checked directly.

## Smoother door opening

- Door opening duration increased from 0.55 to 0.95 seconds. Quintic smootherstep gives zero velocity and acceleration at both ends while keeping both logo halves tied to door displacement.
- MainMenuFlowTests: 15 passed, 0 failed (41.13 seconds), including full door/logo alignment, actual scene entry, readiness waiting and timeout recovery. Editor returned to ready state.
- No player build or broader suite: only entrance timing/easing changed; all affected title integration tests passed.

## Door catches the waiting logo

- Logo separation now stays at the menu split distance until the moving door seam reaches it, then equals the door displacement. The two stages meet at the same position with no offset jump.
- PlayOpensDoorsWithLogoHalvesAttachedUntilFullyOutsideTheDoorway passed after adding checks that doors move while logos remain still, both edges coincide after contact, local logo scale is compensated, and both halves leave through the doorway clip.
- Existing 1.4-second easing and loading-before-motion behavior remain intact. Verification focused on the changed movement relationship; no build or unrelated suite was run.
