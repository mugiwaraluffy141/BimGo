# Validation: Review workflow usability

Ngày bắt đầu: 19/11/2026. Liên kết: [spec](../specs/review-workflow-usability.md), [plan](review-workflow-usability.md), [tasks](review-workflow-usability-tasks.md).

## Cập nhật mới nhất — Excel hiện trực tiếp trong FORMAT

- Theo xác nhận của người dùng, đã bỏ cờ `BIMGO_EXCEL_PREVIEW` và guard opt-in trong exporter. `FORMAT: EXCEL` luôn có trong danh sách; menu Reports cũng liệt kê Excel.
- Khi chọn Excel, card hiển thị thông báo ngắn rằng checkbox save/sort chưa được nghiệm thu trên Excel. Không thay scope, writer, checkbox encoding hoặc quy tắc Done độc lập Clash Status.
- Viewer Release build (không deploy): pass, 0 errors/109 warnings ở các vị trí cũ. App tests: **83/83 pass**, không skip.
- Chưa chạy viewer GUI hoặc Excel để nghiệm thu thay đổi này; chưa deploy/cài đè app đang chạy. Các ghi nhận preview OFF/opt-in ở các phần lịch sử dưới đây đã được quyết định người dùng thay thế.

## Baseline đã thực thi

| Kiểm tra | Kết quả |
|---|---|
| Viewer Release build, local deploy=false, single-node | Pass, 0 errors, 155 warnings có sẵn trước thay đổi code sản phẩm |
| Core tests trước thay đổi | 517 pass, 0 failed/skipped |
| Thêm regression JSON exchange | 518 pass, 0 failed/skipped; giữ IDs/groups/history/Unicode sau import lặp và serialize/parse |

Không coi baseline là bằng chứng các tính năng mới hoạt động. Các kết quả tích hợp sau đó phải ghi riêng.

## Kết quả tích hợp — đợt triển khai đầu

- Native shared editor: dialog RichTextBox owned/modal, không phải search nhúng. Main đã sửa thứ tự UpdateFrame để editor sở hữu input trước toolbar/review panel.
- Double-click: ordered pointer events, selective consumption và batch tái sử dụng; regression gồm row→group/pencil, đúng singleton trước click tiếp theo, right events, capacity và allocation sau warm-up. Native editor có pure Unicode/newline tests và 5 real HWND-backed STA regression tests; clipboard có deep copy/session/model/name-limit tests. App suite tích hợp hiện **38 pass, 0 failed/skipped**.
- Viewer build sau hook editor routing: 0 errors, 0 warnings trên incremental build. Số warnings khác baseline không có nghĩa đã sửa các warnings gốc.
- [UI inventory](review-ui-inventory.md) đã hoàn tất source inspection, không phải runtime acceptance.
- [Excel spike](review-excel-validation.md) tạo sample và structural tests thành công; 10.000 dòng 184.225 bytes/85 ms trong lần đo prototype. Không phải benchmark xuất báo cáo BimGo hoặc xác nhận Excel tương tác.
- **Checkpoint A còn mở:** chưa nghiệm thu native editor trên UI thật và checkbox trong Excel, writer chưa tích hợp Reports sản phẩm.

### Lệnh đã chạy sau sửa review

```powershell
dotnet build src/BimGo.App/BimGo.App.csproj -c Release -p:DeployBimGoLocally=false -m:1 -v:q
dotnet test --project tests/BimGo.Core.Tests/BimGo.Core.Tests.csproj -c Release
dotnet test --project tests/BimGo.App.Tests/BimGo.App.Tests.csproj -c Release
git diff --check
```

- Viewer build: pass, 0 errors; full App recompile báo 109 warnings ở các vị trí có sẵn, incremental sau đó có thể báo 0 warnings. Không tuyên bố đã sửa baseline warnings.
- Core: **521/521 pass**, gồm default metadata/migration/conflict tests cho setup actions; defaults version 10.
- App: **38/38 pass**, có native control HWND tests nhưng không hiện cửa sổ.
- Whitespace: pass; Git có thông báo chuyển LF→CRLF, không phải lỗi whitespace.
- Môi trường thực thi: Windows win-x64 `10.0.26200`, SDK `10.0.401`; App tests chạy `net8.0-windows`/x64. Không viewer UI/Excel/Revit host nào được mở để nghiệm thu.

## Review code độc lập — vòng 1

Reviewer yêu cầu sửa trước bàn giao:

1. **P1 — đã sửa:** rollback overflow bằng `SelectedText` tạo undo unit bổ sung, có thể làm Undo mắc kẹt. Nay reject trước native mutation; có native-control regression tests cho undo/redo và paste.
2. **P2 — đã sửa:** tiêu thụ toàn bộ pointer queue sau một row press có thể nuốt group/pencil click đến sau trong cùng frame. Nay route theo thứ tự và consume từng event, dispatch double-click đúng target trước event tiếp theo; có regression row→group/pencil.
3. **P2:** context menu text thiếu Delete/shortcut labels. Main đã bổ sung; enabled theo selection và có labels Ctrl+Z/Y/X/C/V/A/Delete.

Menu Test/Set đã có hook UpdateClashSetupCommands trong UpdateFrame sau các owner editor/key capture/markup; guard loại Controls/Colour Profiles/Geometry. Focus loss xóa target menu/right-click đang chờ. Chưa coi code review hoàn tất chỉ vì tests pass.

Review menu vòng 2 yêu cầu thêm hai sửa đổi, đã được triển khai: object shortcut đăng ký action catalog/context/conflict handling thay raw keys, menu hiển thị Hotkeys labels; right-click setup dùng ordered native down/move/up, clipped row hit-test và `ContextClickTracker` giữ target gốc, hủy vĩnh viễn khi thành drag. Có regression khác target/drag quay về/reset. Một native baseline assertion thất bại trong lúc sửa nhánh đã được xử lý; các kết quả pass cuối ở trên thay cho lần chạy trung gian đó. Review xác minh cuối vẫn tách khỏi nghiệm thu UI.

## Kiểm tra thủ công bắt buộc

### Editing và focus — chưa nghiệm thu

- [ ] Rename Test/Set/Group: kéo bôi đen, Ctrl+A/C/X/V, Backspace/Delete, caret, Undo/Redo, Enter lưu/Esc hủy.
- [ ] Comment/notes/reply/assignee: dữ liệu tiếng Việt, clipboard dài, newline, giới hạn ký tự và lưu/mở lại.
- [ ] Search clash/room/controls: filter đúng, selection/caret đúng, không xóa đối tượng khi nhấn Delete.
- [ ] DPI 100/150/200%, resize, Tab/Shift+Tab, Alt+Tab, đóng model, editor/menu không giữ phím hoặc chuột.
- [ ] Bộ gõ tiếng Việt: ghi bộ gõ và phiên bản, kiểm tra nhập trực tiếp lẫn clipboard; không suy từ test chuỗi Unicode.

### Context menu — chưa nghiệm thu

- [ ] Đúng đối tượng khi click phải ngoài selection; lệnh nhiều đối tượng chỉ tác động khi hợp lệ.
- [ ] Shift+F10/Menu và Up/Down/Enter/Esc; click ngoài đóng, không xuyên xuống viewport.
- [ ] F2/Ctrl+C/V/Delete theo focus; Test/Set Paste mới ID/name, không clone result/history/issue.
- [ ] Group/result/issues/inspect và các màn hình còn hiện hành đối chiếu từng dòng UI inventory.

### Double-click → Revit — chưa nghiệm thu host

- [ ] Hai click cùng dòng clash gửi một request; hai dòng khác nhau, kéo, Ctrl/Shift, icon markup, group header không gửi.
- [ ] Cặp host và linked pair: đúng phần tử/view/section box; lặp lại không tạo view ngoài hành vi nút cũ.
- [ ] Nút REVIT và R vẫn hoạt động; offline/mất kết nối báo rõ.
- [ ] Ghi từng host R23/R24/R25/R26/R27 đạt/chưa chạy; build không thay nghiệm thu live.

### Excel 365 Windows 11 — chưa nghiệm thu tương tác

- [ ] Ghi phiên bản Windows, Excel Microsoft 365 desktop 64-bit Current Channel và Version/Build.
- [ ] File do BimGo xuất có checkbox trong ô thật, ban đầu bỏ tích; tích/lưu/mở lại giữ dấu.
- [ ] Sort/filter không đổi quan hệ checkbox–Clash ID; Done không thay trạng thái trong BimGo.
- [ ] Đúng ba scope, số dòng, group/metadata/units; sample 1/100/10.000 clash và đo thời gian/kích thước.
- [ ] Máy không cài Excel vẫn xuất được. Lỗi quyền/file khóa/hủy không phá bản có sẵn.
- [ ] Text đầu =/+/-/@ và ký tự XML không thành công thức; HTML/XML/JSON cũ không hồi quy.

## Nguyên tắc ghi kết quả

- Chỉ đánh dấu pass khi có command output hoặc kiểm thử UI/host thực tế.
- Chưa có môi trường thì ghi not-run/blocked, không suy đoán đạt.
- Sample spike chỉ chứng minh kỹ thuật ở phạm vi đã chạy; không thay nghiệm thu workbook xuất từ BimGo.
- Không commit/deploy/cài add-in trong phạm vi xác minh tự động của phiên này.

## Tiếp tục triển khai — 20/11/2026

- Group/Result command slice: nhánh báo Core 523/App 46 tests pass và viewer build đạt; kiểm tra tổng hợp sẽ chạy sau tích hợp các nhánh mới.
- Shared pencil geometry đã được dùng chung giữa render/hit-test và có coordinate tests ở scale 1/1.5/2, sửa độ lệch group picture pencil do reviewer phát hiện.
- Reviewer không còn finding bắt buộc trong Issues menu-only, Excel snapshot và shared pencil geometry ở snapshot đã kiểm tra. Điều này không phải nghiệm thu keyboard parity, UI hay Office.
- Excel validation đã chạy safety preflight read-only, phát hiện hai startup add-in chưa được kiểm tra. Không kích hoạt COM/Excel hoặc thay đổi Office settings. Runtime native checkbox, save/reopen và sort/filter vẫn NOT RUN.
- Writer/Reports dự kiến chỉ bật khi opt-in `BIMGO_EXCEL_PREVIEW=1` cho tới khi được nghiệm thu trên Excel đích; ordinary Reports không tự xuất hiện tính năng chưa nghiệm thu.
- Embedded search, Issues keyboard, Home menus và writer đang được tích hợp; không dùng kết quả đợt đầu để tuyên bố các phần này đã pass.

## Xác minh tích hợp — 20/11/2026

- Source-link của App test thiếu `Platform/NativeSearchEditor.cs` và `Platform/ClashReviewContext.cs`; cả hai Compile links đã được thêm. App test build Release thành công với target framework cụ thể và không build project references lần nữa.
- Source inspection cho thấy `UpdateCommentCommands` có handler nhưng không có call site. Đã tích hợp vào `GameSession.UpdateFrame` sau editor/markup routing và trước clash setup/global shortcuts để Issues shortcuts chỉ nhận input khi panel có focus.
- Clash search native đã được xác nhận có callback thay đổi filter/focus, input owner trong `GameWindow`, và dispose khi session/window đóng. Home card menu được gọi trước thao tác click mở thẻ; cả hai xác nhận bằng source inspection, chưa chạy viewer UI.
- Viewer Release rebuild, `DeployBimGoLocally=false`: **pass**, 0 errors, 109 pre-existing warnings.
- App tests: build `dotnet build tests/BimGo.App.Tests/BimGo.App.Tests.csproj -c Release -f net8.0-windows --no-restore -p:BuildProjectReferences=false -m:1`, sau đó `dotnet exec tests/BimGo.App.Tests/bin/Release/net8.0-windows/BimGo.App.Tests.dll`: **80/80 pass**.
- Core tests: build target `net8.0` với `BuildProjectReferences=false`, sau đó chạy DLL Release trực tiếp: **525/525 pass**.
- Lệnh `dotnet test --project tests/BimGo.App.Tests/BimGo.App.Tests.csproj -c Release` thất bại trước compile/test khi MSBuild đánh giá multi-target project reference; log chỉ báo `MSB4276` cho workload locator SDK và kết thúc 0 warnings/0 errors. Vì vậy đã build framework-specific và chạy DLL theo phương án thay thế; đây không phải lỗi named pipe.
- `git diff --check`: pass; Git chỉ cảnh báo các file LF hiện tại sẽ được chuẩn hóa CRLF khi được Git ghi lại.
- UI viewer, IME/DPI/focus, menu keyboard, Revit host/link/offline và Excel 365 checkbox/save/reopen/sort/filter: **NOT RUN**. Preview Excel vẫn opt-in qua `BIMGO_EXCEL_PREVIEW=1`; không có bằng chứng để gỡ gate.

## Sửa theo review độc lập — 20/11/2026

- **P1 native search lifecycle:** `Render` now hides the child search whenever the session is paused, UI-hidden, or outside Clash mode; `ExitClashMode` and the hide-UI transition also hide it immediately. `GameSession.Dispose` closes/disposes the child HWND so callbacks cannot retain the disposed session when the shared `GameWindow` is reused. The App suite has HWND-backed hide/dispose coverage (`HideRetainsQueryButDisposeDestroysOwnedChildAndCannotReopen`); complete viewer transitions were not manually exercised.
- **P2 result-row context click:** removed the sampled `RightPressed`/final-hover opening path. The ordered pointer batch freezes the clipped row identity on `RightDown`, cancels on drag/focus/scroll/mode changes, and opens only on `RightUp` over the same row within the Windows drag threshold. A right press on a dock row is consumed so it cannot turn into viewport navigation; viewport RMB behavior remains on the existing path. Added routed-batch tests for row retarget/drag, scroll gating and consumed-down preservation.
- After these fixes, Viewer Release build passed with 0 errors and 109 warnings; App suite framework-specific build passed and direct DLL run passed **79/79**. `git diff --check` passed.
- Reconciliation against the task tracker: **T13 Room/parameter search remains NOT DONE**; `GameSession.Rooms.cs` and `GameSession.RoomOverlay.cs` still use independent character buffers with no native editor selection/clipboard integration. **T15 Inspect/measurement copy remains NOT DONE**; the source inventory lists read-only Scan/Measure information, but no focused value/element/measurement copy command or handler. **Reply-row Copy/Delete remains NOT DONE**; current Issues commands target comment list/detail, and no reply-row keyboard/context-menu target was added. These need separate bounded implementation slices and have no implementation/verification evidence in this integration. Do not mark T13/T15/reply commands complete.
- Manual right-click behavior, native HWND lifecycle transitions in the running viewer, Room search, Inspect copy, reply-row commands, Revit, and Excel interaction remain **NOT RUN**.

### Review round 2 corrections

- `PointerEventBatch.MarkHandled` accumulates handling so a later left-row pass cannot overwrite a consumed right-down; the ordered routing test exercises this case.
- Context click routing is disabled for the full scrolling batch, so later down/up messages in that same batch cannot rearm a menu.
- Clash look ownership follows the latest still-held native right-button down/up/cancel event and its coordinates. Sampled hover is only a fallback when no native button event was captured; the render pass no longer relatches ownership. A regression covers a dock click followed by a still-held viewport down in one batch.
- A later `RightDown`, left `Down`, or `Cancel` in the same ordered batch clears an earlier accepted dock-menu result; the mixed-batch regression verifies that a following viewport down remains unhandled and cancels the earlier menu outcome. Final App tests: **80/80 pass**.

## Tiếp tục triển khai T13/T15/reply — 20/11/2026

- **T13 Room/parameter search:** Find Room and Room Data parameter search now use the owned native editor. Click focuses the field; native selection, clipboard, paste, undo/redo and focus ownership apply; filtering remains live. Enter in Find Room still chooses the first match. The child HWND remains visible while the paused Rooms panel is active and hides when Rooms opens/closes or another non-room state takes over. Automated editor Enter/Esc callback regression passes; no viewer UI search/focus/shortcut leakage test was run.
- **T15 Inspect/measurement copy:** context-scoped Ctrl+C actions were added for Scan and Measure. Scan copies the target's host/link, runtime element ID, source identity when distinct, category/family/level/group/phase, runtime state and displayed parameters. Right-clicking a visible identifier or value row offers a focused **Copy Value** action for that exact row. Measure copy follows visible live/saved/incomplete states and includes vertical clearance. Binding conflict checks, F1 rows, panel hints and default migration version 13 are included. Clipboard output has not been manually inspected in the viewer.
- **Reply commands:** clicking a reply establishes its parent/reply ID as keyboard focus; stationary right-click and Shift+F10/Menu open Copy Reply Content and Delete Reply. Ctrl+C and Delete use the focused reply target. Delete reuses the existing 3-second arm/confirm operation from the reply button. Commands re-resolve stable IDs before acting. UI focus/menu/clipboard/delete confirmation has not been manually exercised.
- **Verification after this slice:** App/Viewer Release framework-specific build succeeded with 0 errors and 109 warnings; App.Tests Release framework-specific build succeeded and direct DLL run passed **83/83**. Core build was not rerun for this review-fix slice. `git diff --check` passed (only line-ending warnings). The standard App `dotnet test` workload-locator failure and framework-specific workaround are recorded above.
- **Still NOT RUN:** live viewer interactions for Room search, Scan/Measure clipboard, reply context/keyboard/Delete behavior; IME/DPI/focus, Revit host, and Excel checkbox/save/reopen/sort/filter acceptance. Excel preview remains opt-in (`BIMGO_EXCEL_PREVIEW=1`).

### Review follow-up — 20/11/2026

- Native Room search now hides on the Room Data no-data early path, Find/Parameter search can be reached with Tab/Shift+Tab, Ctrl+F requests initial native focus, and native fields reset undo/caret only when the logical field changes. Clash's accessible name is reset explicitly. An HWND regression covers switching to a second field with identical current text and confirms undo history is cleared.
- Measure copy uses the same live/no-hit conditions as the visible distance/angle readout and includes the visible total vertical clearance. Scan copy distinguishes runtime element ID from source identity and includes visible Group and Phase. The per-row Copy Value context action targets the exact visible value row.
- Reply keyboard focus is routed from ordered, clipped pointer Down events; clicking issue body clears reply focus, and scrolling clears click trackers while retaining stable reply IDs.
- A confirmed Scan value context click now cancels the Review pointer gesture state before opening the native menu, so its RMB cannot also dispatch Scan's clear-lock secondary action. The viewport RMB drag path is unchanged until a row gesture is accepted.
- Latest checks: App/Viewer Release build 0 errors/109 warnings; App.Tests direct DLL 83/83 pass; `git diff --check` passed. Core was not rerun, and Sol re-review remains pending. Manual UI/Revit/Excel acceptance remains NOT RUN.
