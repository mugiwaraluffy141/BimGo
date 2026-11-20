# Native text editor — first slice (2026-11-19)

## Implemented

- `Platform/NativeTextEditor.cs`: owned WinForms modal dialog, using the existing STA entry point and in-box Windows Forms dependency. RichTextBox provides native caret/selection and undo; redo has an explicit Ctrl+Y handler. A native ContextMenuStrip supplies Undo/Redo/Cut/Copy/Paste/Select All and disabled states.
- `Game/GameSession.Menu.cs`: existing BeginTextEdit/comment/bookmark/reply/assignee paths open the adapter during the next editor update, not while constructing immediate-mode UI. Their existing CommitComment callbacks, Trim, empty-value validation, cancellation toasts, pending-bookmark cancellation and persistence remain in place.
- Enter saves; Escape, Cancel and window Close cancel. Shift+Enter inserts a newline for comments/replies and the existing Clash/Group Notes titles. Other shared short-text editors remain single-line. Newline handling converts CRLF/CR to LF; single-line pasted newlines become spaces.
- Existing UTF-16 budgets remain (including 280 for notes/comments, 60 for assignees, caller-specific names/rules). Truncation uses Unicode text-element boundaries rather than cutting surrogate pairs or combining sequences. Clipboard paste is plain Unicode text, bounded directly rather than going through InputState's 64-character buffer, and preserves unselected suffix text. Review follow-up rejects overflow before native mutation, rather than restoring text through an extra undo operation.
- Mouse capture is released before showing the dialog. InputState is released before and in a finally block after the nested dialog pump; viewer updates do not execute while the owned dialog is active. The dialog/control/menu are disposed after closing.

## Deliberate first-slice departure

This is a separate owned modal dialog, **not** an embedded overlay textbox. Viewer rendering/live updates pause while the nested WinForms message pump runs. No search field has been migrated. The existing immediate-mode editor drawing is retained for the pending frame before the native dialog is shown. This is not full P4/P5 or whole-interface acceptance.

Clash/Group Notes multiline detection currently recognizes their existing shared-editor titles to avoid editing caller-owned files. A later caller migration should replace that with an explicit multiline argument.

## Integration hook applied by main

In `GameSession.UpdateFrame`, the IsEditingComment/UpdateCommentEditor early-return now runs **before** UpdateViewerToolbarInput and UpdateReviewPanelInput. This prevents pre-dialog input reaching those routers. No GameWindow/InputState changes were needed for the owned-dialog adapter itself. Main also added Delete and shortcut labels to the text context menu, plus source-linked tests for newline/Unicode text-element boundaries; these tests do not instantiate a dialog or prove native undo/IME behavior.

## Verification evidence

- Adapter-only increment: `dotnet build src/BimGo.App/BimGo.App.csproj -c Release -p:DeployBimGoLocally=false -m:1` succeeded, 0 errors / 109 existing warnings (4.53 s).
- Integrated increment: same Release build (with `-v:q`) succeeded, 0 errors / 109 existing warnings (3.35 s).
- Final overflow-protection increment: same Release build succeeded, 0 errors / 109 existing warnings (2.45 s). Overflowing typing/IME edits are rejected as a whole rather than deleting an existing suffix; plain-text paste is bounded before insertion.
- `git diff --check` succeeded; only repository line-ending conversion notices were printed.
- No packages, test projects, commits, deployment, viewer launch or Revit operations.

### Review fix verification

- Original overflow rollback could trap Undo; that implementation was replaced before handoff.
- Added `tests/BimGo.App.Tests/NativeTextEditorControlTests.cs`: five real HWND-backed STA tests on .NET 8, without showing windows. They cover middle overflow, earlier undo/redo history, bounded Unicode selection paste and surrogate safety. These are native-control tests, not just pure string checks.
- The previously failing unmodified-control paste baseline assertion was investigated and corrected alongside the native control tests; the combined App suite subsequently passed. Main's latest integrated run has 38 tests pass, 0 failures/skips; exact final suite evidence is recorded in the aggregate validation document.

## Not yet verified — Windows acceptance required

Build success is not evidence that native focus/IME/visual behavior passes. No GUI harness was available or launched. Before accepting P1/P3, manually check:

1. Rename Test: mouse/keyboard selection; caret/Home/End; Ctrl+A/C/X/V/Z/Y; right-click and Shift+F10 context menu; Tab/Shift+Tab between editor and buttons; Enter/Escape/Close; empty rename unchanged according to its existing callback.
2. Notes/replies/comments: Shift+Enter, pasted CRLF/LF, 280-character boundary, Vietnamese composed/decomposed text, emoji/ZWJ/combining sequences at limits; paste over a selection preserves the surrounding text; save/reopen retains text.
3. Undo/redo with real user keyboard/mouse around typing, selection replacement, paste, newline normalization and overflow. Automated HWND tests cover the reported overflow trap, but full dialog keyboard/IME behavior still needs interactive validation.
4. Vietnamese IME/Telex and other IME composition: Enter should finish active composition rather than prematurely save. The adapter tracks native IME start/end messages, but this has not been demonstrated with a real input method.
5. DPI 100/150/200%, monitor changes, Alt+Tab, owner minimize/close, model close requests, repeated opens, and cancellation of a pending bookmark/thumbnail. Confirm the initially selected value and all controls fit and focus returns safely.
6. Hold/release R/Delete/Ctrl/A and mouse buttons while entering/exiting. Confirm no viewer action, camera movement, selection change or stuck capture. Check the next frame as well as the active modal.

Read-only selectable values and search focus are outside this adapter slice. Whole-window model-close behavior while the modal blocks the main update loop remains a manual-validation risk, not a proven acceptance result.
