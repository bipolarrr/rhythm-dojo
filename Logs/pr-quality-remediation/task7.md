# Task 7 — scene preparation and overlapping input ownership

Base: `854318a`. Source work only; Unity execution belongs to the integration owner.

## Reproduction evidence

`ProjectLibraryFlowTests.NestedModalScopesKeepInputBlockedUntilTheLastOwnerCloses` uses the existing `ModalInputScope` API. Root ran it before production edits: 1/1 failed because disposing the first owner enabled `SongSelectionScreen` while the second owner still held it. See `task7-red.json`. This is the experimentally reproduced defect.

Separate static findings: AppFlow duplicated warm and cold additive entrance handling; preparation clearing did not drain an uncancellable operation; scene-loaded processing could reach request cleanup, and composition `Start` could attach services after preparation had been invalidated. Those timing hazards were traced in source, not claimed as experimentally reproduced on the old implementation.

## Decisions and implementation

- `InputStateScope` counts owners of each Behaviour and CanvasGroup, retains original disabled/interactable/raycast state, and notifies participants only after the last owner restores state. Focus scopes form a release-aware stack, supporting both nested popup order and out-of-order release. All participant notifications run; aggregated notification errors propagate only after restoration and focus cleanup.
- `ModalInputScope` keeps its public API. Door presentation uses the same counted scope instead of independently enabling the selection screen/group.
- `PreparedSelectionScene` owns the actual AsyncOperation, exact scene handle, preparation generation, canvas orders and scopes. Cancellation invalidates the generation; pending loads are drained and unloaded before a queued same-path generation starts. Stale scenes are deactivated in `sceneLoaded` before `Start` can initialize services.
- Warm and cold entrance use one owner activation path. Composition is initialized explicitly while blocked; `ScreenCompositionRoot.Initialize` has an owner gate and idempotence guard. Readiness reaches the door only after initialization and prior-scene unload completion.
- The preparing selection never holds a title focus scope. Its synchronous initialization restores the focus it found, including an open settings popup. Final screen input restoration focuses the selected row, after which a closing modal restores its exact captured focus.
- MainMenu editor, fallback Bootstrap, and recovery-title exits invalidate preparation before starting their direct loads. Existing public navigation APIs and mode-specific settings behavior remain.
- Optional owner constructor delegates allow real scene loading with a deterministic initialization gate, injected load failure, and short timeout. They expose no product debug UI.

## Verification pending

New `ScenePreparationFlowTests` cover real-load invalidation followed by editor navigation; title settings focus while preparation completes; duplicate entrance requests and final scene/input/listener counts; queued generation drain/reentry; timeout and retry; load failure and cold entrance retry; mixed initial input states and throwing participant cleanup. Existing MainMenu and ProjectLibrary fixtures cover direct scene entry, entrance rendering and navigation; root will run those and Smoke sequentially after refresh.

No Unity tests were run by the implementation agent. Results, discovered regressions and any remaining limits must be appended by the integration owner; source changes are not evidence of runtime success.

## First validation and review follow-up

Integration owner reported the initial lifecycle fixture passed 6/6. MainMenuFlow first run passed 17/19: two presentation-only entrance tests exposed a new resume notification reaching an unbound `SongSelectionScreen`. `ResumeAfterModal` now uses the existing lifetime binding state to skip refresh before initialization or after unbinding; counted owner notifications still run normally for bound screens. No exception logs or assertions were suppressed.

Review identified a separate exact-focus gap: both initialization restoration and modal restoration skipped a captured null focus. Both now explicitly restore null (also when the captured object was destroyed or became inactive). Added delayed preparation/null focus and modal close/null focus regressions, changed the title popup scenario to capture the selectable logo button, and added disposal during outstanding real loading. These follow-up changes await root refresh and validation.
Final root validation: preparation 9/9, project library 3/3, main menu 19/19, Smoke 4/4. First menu failures retained; corrected unbound lifecycle and null-focus paths passed independent re-review. Play Mode exited and Editor ready after each suite. No scene regeneration required.
