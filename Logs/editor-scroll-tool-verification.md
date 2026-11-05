# Editor scroll tool

This behavior has been replaced by the playback-position tool at the user's request. See editor-playback-position-verification.md for the current behavior and verification.

- Added Scroll to the top Tools menu and below Grab in the left toolbar.
- The icon is a horizontal line with vertical end caps (I----I). The quantization button moves down one row.
- Left-button dragging scrolls the grid vertically, using its visible beat span and local pointer displacement. It does not zoom, edit notes, or change selection.
- The existing minimum scroll boundary applies. Pointer release and Escape stop the drag; busy state or a different tool cancels it on further movement.
- Updated WorkspaceSceneBuilder for lasting scene generation; applied the same additions to the saved ChartEditor scene while preserving existing object references.

## Verification

Unity 6000.3.23f1, isolated copy of the current project. PlayMode: 4 passed, 0 failed.

- ScrollToolDragsViewportBothWaysWithoutEditingNotes: menu and toolbar selection, icon placement, both scroll directions, minimum boundary, unchanged zoom/notes/selection.
- LeftToolIconsSelectDrawEraseAndGrabWithVisibleSelection: existing tool interactions and selection colors with the added tool.
- OriginalEditorKeepsItsStandardDefault: saved editor scene references and initial state.
- GrabCancelsOutsideGridAndDoesNotDrawOnEmptySpace: existing note drag cancellation.

Visual inspection: Logs/editor-scroll-tool.png confirms icon below Grab and quantization beneath it. Results: Logs/editor-scroll-tool-playmode.xml and .log. Binary evidence is ignored by Git.

No full suite or player build: this change adds a local editor viewport tool and does not alter navigation or build settings.
