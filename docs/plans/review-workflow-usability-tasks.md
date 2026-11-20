# Tasks: Review workflow usability

Người dùng duyệt spec/plan và yêu cầu subagents triển khai ngày 19/11/2026. Tracker này không thay đổi phạm vi [spec](../specs/review-workflow-usability.md) hoặc bỏ các checkpoint của [plan](review-workflow-usability.md).

**Quyết định mới nhất của người dùng:** cho Excel hiện trực tiếp trong FORMAT, không cần biến môi trường. Đã bỏ `BIMGO_EXCEL_PREVIEW` khỏi code, dùng nhãn `FORMAT: EXCEL` và thông báo ngắn về kiểm thử checkbox còn thiếu. Các ghi nhận preview opt-in bên dưới là lịch sử triển khai, đã được quyết định này thay thế; nghiệm thu Excel thực tế vẫn chưa pass.

## Quy tắc thực hiện

- Một chủ sở hữu cho mỗi cụm file; không sửa đè công việc nhánh khác. Không commit/deploy.
- Mỗi task triển khai tối đa khoảng 5 file; nếu cần nhiều hơn, ghi tách increment trước khi tiếp tục.
- Mỗi task phải có test/build hoặc bằng chứng read-only phù hợp. Chưa có UI/Excel/Revit thì ghi chưa nghiệm thu.
- Không thêm dependency sản phẩm hoặc hạ yêu cầu checkbox nếu chưa được duyệt.

## Nhóm 1 — Baseline và thử rủi ro

- [x] **T01 — Baseline build.** Acceptance: viewer build không deploy; ghi warning/error gốc. Verify: `dotnet build src/BimGo.App/BimGo.App.csproj -c Release -p:DeployBimGoLocally=false -m:1`. Files: không sửa. Kết quả: 0 errors, 155 warnings có sẵn.

Baseline Core: 517/517 tests pass. Main đã thêm `ClashExchangeRegressionTests.cs` cho import JSON lặp và Unicode/group/history/identity; suite hiện 518/518 pass. Đây là một phần T20, không phải nghiệm thu toàn bộ dữ liệu/UI.
- [x] **T02 — Kiểm kê UI.** Acceptance: mỗi bề mặt hiện hành có entrypoint, command/textbox còn thiếu hoặc N/A có lý do. Verify: đối chiếu source/entrypoint; không suy code còn lưu là UI còn dùng. Files: `docs/plans/review-ui-inventory.md`. Dependency: T01. Owner: inventory agent. Kết quả: inventory hoàn tất, Bookmarks/Colour Profiles vẫn reachable; nhiều search/markup bypass editor chung. Đây là static inventory, chưa nghiệm thu toàn UI.
- [ ] **T03 — Native editor slice.** Acceptance: editor chung có selection/clipboard/caret/undo/redo, Enter/Esc và giới hạn dữ liệu không đổi; input không đi vào viewport trong phiên edit. Verify: viewer build, kiểm tra tự động khả thi, manual IME/DPI/focus ghi riêng. Files: `Platform/NativeTextEditor.cs`, `GameSession.Menu.cs`, báo cáo validation; tối đa 3 file. Dependency: T01. Owner: editor agent.
- [ ] **T04 — Checkbox spike.** Acceptance: encoding có nguồn tham khảo và sample do harness tạo; ghi rõ Excel build/availability và khả năng save/sort/filter đã/chưa thử. Verify: sample package XML, chạy harness; interactive Excel nếu môi trường cho phép. Files: tối đa 4 file trong `tools/review-excel-spike/` và `docs/plans/review-excel-validation.md`. Dependency: T01. Owner: Excel agent.

**Checkpoint A:** review T02–T04; không coi sample XML hay build thành bằng chứng checkbox/IME đạt trên UI thật. Nếu thiếu môi trường, ghi blocked/unverified và không tuyên bố đạt checkpoint.

Tiến độ: T03 đã có adapter modal RichTextBox và tích hợp shared editor; main đã đưa editor routing trước toolbar/panel và thêm test Unicode/newline. Search chưa migrate; IME/DPI/focus và undo-at-limit cần kiểm thử UI. T04 đã có BCL-only harness/sample, structural tests và source evidence; Excel hiện có x64 build 16.0.20625.20108 nhưng feature/license và tương tác chưa xác minh. **Checkpoint A chưa đạt, chưa đưa writer spike vào Reports sản phẩm.**

## Nhóm 2 — Double-click và test input

- [x] **T05 — Test harness Windows.** Acceptance: project MSTest cùng version repo, chạy logic App không khởi tạo GL/Revit; không đổi Core/CI/solution để ép phụ thuộc UI. Verify: `dotnet test --project tests/BimGo.App.Tests/BimGo.App.Tests.csproj -c Release`. Files: csproj mới và source-linked helpers cần thiết. Dependency: T01. Owner: main. Kết quả: source-linked gesture/editor/clipboard logic với Core data types, không reference viewer executable; suite đã chạy thành công.
- [ ] **T06 — Gesture ordered input.** Acceptance: giữ thứ tự/timing click, ngưỡng Windows, reset focus/scroll/drag/modifier; logic có test dương/âm/biên. Verify: T05 tests và viewer build. Files: `DoubleClickTracker.cs`, `InputState.cs`, `GameWindow.cs`, `Win32.cs` nếu cần, `DoubleClickTrackerTests.cs`; nếu vượt 5, tách khỏi T07. Dependency: T05. Owner: gesture agent.
- [ ] **T07 — Row → Revit.** Acceptance: đúng một request cho đúng clash, loại group/icon/editor/tool; giữ nút/R và selection. Verify: unit gesture, viewer build; host/link/offline live checklist chưa chạy phải ghi riêng. Files: `ClashDock.cs`, `ClashMode.cs` nếu cần, test liên quan. Dependency: T06. Owner: gesture agent.

**Checkpoint B:** kiểm tra đúng target, không click-through và không double-send; reviewer đọc diff input/editor trước mở rộng menu.

Tiến độ T06/T07: code đã tích hợp ordered pointer events và row singleton Revit path, 7 gesture unit tests pass; reviewer độc lập đang kiểm tra. T07 chưa nghiệm thu host/link/offline UI, không đánh dấu hoàn tất SC-05/06 chỉ vì build/test đạt.

Cập nhật sau vòng sửa: App suite 38/38 pass gồm native-control/gesture/clipboard/context-click tests; Core 521/521 pass. Ordered batch đã bỏ steady-state allocations. Test/Set actions đăng ký catalog/context và conflict guard, defaults v10; hook chính đã tích hợp. Đã sửa overflow undo, batched target routing, menu Delete/labels và setup stationary right-click. Checkpoint review/interactive vẫn ghi riêng trong validation.

## Nhóm 3 — Menu và editing toàn UI

- [ ] **T08 — Menu command foundation.** Acceptance: enabled/target/shortcut và điều hướng bàn phím, không phá dropdown hiện tại. Verify: command tests, viewer build, keyboard/manual menu. Files: tối đa 5 gồm helper/menu/overlays/tests. Dependency: T02, T03, checkpoint B.
- [ ] **T09 — Test commands.** Acceptance: F2/Copy/Paste cùng session, ID mới, không copy results/history/issues. Verify: clone/clipboard validation tests và build. Files: tối đa 5 quanh `ClashPanel.cs`, clipboard helper, tests. Dependency: T08.
- [ ] **T10 — Set commands.** Acceptance: menu/nút/phím cùng validation; rules clone đúng, payload sai/khác session bị chặn. Verify: tests/build. Files: tối đa 5 quanh `ClashPanel.cs`, helper, tests. Dependency: T09.
- [ ] **T11 — Group/result commands.** Acceptance: Rename/Copy/Show in Revit hợp lệ; giữ grouping và multi-selection, không nhân bản clash/group. Verify: tests/build, manual context targets. Files: tối đa 5 quanh `ClashGroups.cs`, `ClashDock.cs`, helper/tests. Dependency: T08, T07.
- [ ] **T12 — Clash search editing.** Acceptance: search selection/clipboard/focus đúng, filter không hồi quy. Verify: build và search manual. Files: tối đa 4 quanh `ClashDock.cs`, native adapter, tests. Dependency: T03, checkpoint A.
- [ ] **T13 — Room search editing.** Acceptance: search room/parameter chuẩn input và không rò shortcut. Verify: tests/build/manual. Files: tối đa 5 quanh `Rooms.cs`, `RoomOverlay.cs`, adapter/tests. Dependency: T12.
- [ ] **T14 — Issues menus.** Acceptance: Copy/Edit/Delete theo handler hiện có, reply/assignee không clone issue. Verify: tests/build và save/reopen. Files: tối đa 5 quanh `Comments.cs`, `Menu.cs`, command/tests. Dependency: T08, T03.
- [ ] **T15 — Inspect read-only.** Acceptance: copy thông số đúng target, không cho sửa, không chọn model phía sau. Verify: tests/build/manual. Files: tối đa 5, chốt theo T02. Dependency: T08.
- [ ] **T16 — Các bề mặt còn lại.** Đây là nhóm task chưa thực thi; sau T02 tách mỗi màn hình Home/Controls/Markup/Settings/Bookmark còn hiển thị/add-in thành task riêng tối đa 5 file, acceptance và verify riêng. Không đánh dấu xong toàn UI bằng việc chỉ sửa editor chung. Dependency: T02, T08.

**Checkpoint C:** sau mỗi 2–3 task, build/test và review ngữ cảnh; đối chiếu từng dòng inventory và SC-01…04 trước kết luận hoàn tất UI.

Tiến độ T08–T10: foundation menu native + Test/Set code/clipboard/keyboard/catalog đã có, tests tự động pass; chưa nghiệm thu UI. T11–T16 chưa triển khai. Shared editor đã cải thiện caller dùng BeginTextEdit/comment path, nhưng không thay thế search/markup input riêng. Không đánh dấu hoàn tất toàn UI.

## Nhóm 4 — Excel sau xác minh kỹ thuật

- [ ] **T17 — Writer và report projection.** Sau T04 tách projection/writer thành task riêng tối đa 5 file, không package mới khi chưa duyệt; fields/units/text-safe và checkbox chưa tích. Verify: XML/file tests và Excel interactive. Dependency: T04, checkpoint A.
- [ ] **T18 — Reports integration.** Acceptance: ba scope/count, không capture ảnh, không thay HTML/XML/JSON. Verify: tests/build và manual export. Files: tối đa 5 quanh `ClashReport.cs`, `ClashDock.cs`, service/tests. Dependency: T17.
- [ ] **T19 — File safety/scale.** Acceptance: lỗi/hủy giữ file cũ, sample 1/100/10.000, checkbox theo Clash ID khi sort/filter/save. Verify: file failure tests, benchmark và Excel đích ghi build. Files: writer/service/tests/validation tối đa 5. Dependency: T18.

**Checkpoint D:** SC-07…09 phải có evidence workbook do BimGo xuất, không chỉ sample spike. Chưa có Excel thì chưa nghiệm thu.

## Tiếp tục triển khai theo yêu cầu người dùng — 20/11/2026

Người dùng yêu cầu tiếp tục. Các bằng chứng UI/Excel còn thiếu không bị tự đánh dấu pass; mở rộng theo lát với feature chưa nghiệm thu được cô lập.

- **T11:** Group/Result Copy, Group Rename, singleton Focus/Show in Revit và menu native đã có; action catalog mặc định v11, Core 523/App 46 pass tại checkpoint nhánh. Reviewer đang kiểm tra riêng source đã hoàn tất.
- **T12:** Nhánh editor đang xây search clash native nhúng, giữ filter cập nhật trực tiếp; không chấp nhận thay search bằng dialog chỉ apply khi Enter. Lifecycle/session-dispose hooks phải tích hợp trước bàn giao.
- **T14a:** Issues list/detail menu-only đã có, source review không có finding bắt buộc; Copy/Open/Go/Edit/Delete dùng handler cũ và đúng target. Chưa có keyboard parity tại checkpoint này.
- **T14b:** Đang thêm catalog-backed Issues Copy/Delete/ContextMenu và conflict handling; không thêm F2 Rename hoặc reply editing không tồn tại.
- **T16a:** Đang triển khai Home recent/live menus: Open/Show in Folder/Copy/Remove from Recent; không đổi thành quản lý/xóa file.
- **T17a:** `Reports/ClashExcelSnapshot.cs` chụp dữ liệu report độc lập với document, scope/order rõ và đơn vị m; tests snapshot, Unicode, source link và optional dữ liệu đã có.
- **T17b/T18 preview:** Writer BCL và Reports integration đang triển khai sau encoding spike, **preview OFF mặc định**, chỉ opt-in `BIMGO_EXCEL_PREVIEW=1`. Không thêm package/schema/Excel COM vào sản phẩm.
- **Excel risk:** Safety preflight phát hiện hai add-in tự nạp; không khởi động Excel không giám sát. Cần profile thử sạch hoặc phiên có người giám sát để xác nhận checkbox/save/sort/filter. Không coi package tests là nghiệm thu Excel.

Mỗi nhánh vẫn phải build/test/review sau tích hợp; số test trong checkpoint nhánh không thay kết quả tổng hợp cuối ở validation.

## Nhóm 5 — Hồi quy và tài liệu

- [ ] **T20 — Dữ liệu hồi quy.** Acceptance: JSON merge lặp không nhân bản, .bimgo/HTML/XML/BCF giữ dữ liệu. Verify: Core suite và fixture manual cần thiết. Files: tối đa 5 test/fixture trong một task; tách nếu cần. Dependency: các nhánh đã tích hợp.
- [ ] **T21 — Hướng dẫn/acceptance.** Acceptance: controls/clash và Excel/JSON/.bimgo rõ; SC-01…11 có trạng thái và bằng chứng, không che mục chưa chạy. Verify: doc review + `git diff --check`. Files: tối đa 5 docs. Dependency: T20.

## Lệnh verify chung

```powershell
dotnet build src/BimGo.App/BimGo.App.csproj -c Release -p:DeployBimGoLocally=false -m:1 -v:q
dotnet test --project tests/BimGo.Core.Tests/BimGo.Core.Tests.csproj -c Release
dotnet test --project tests/BimGo.App.Tests/BimGo.App.Tests.csproj -c Release
git diff --check
```

Các task lớn còn ở dạng nhóm chỉ được dùng để theo dõi phạm vi; bắt buộc tách acceptance/files/verify cụ thể trước khi giao code. Tracker được cập nhật theo kết quả thực tế, không phải danh sách tính năng đã hoàn thành.

## Đối chiếu tích hợp sau review — 20/11/2026

- **Done in code, pending manual acceptance:** clash search native integration, Home recent/live context menus, Group/Result commands, Issues keyboard commands, and Excel preview writer/report path. Issues commands are now called from the main update loop after editor/markup input routing. Excel remains opt-in until checkbox/save/reopen/sort/filter passes on the selected Excel build.
- **Review fixes applied:** App test source links now include `NativeSearchEditor.cs` and `ClashReviewContext.cs`; native search is hidden when paused/UI-hidden/outside Clash mode, hidden on exit, and disposed with `GameSession`; result context menus now use ordered stationary right-click tracking.
- **T13 Room/parameter search — IMPLEMENTED; manual acceptance pending:** both Find Room and Room Data parameter search now use the owned native search editor with click-to-focus, selection/clipboard editing, live filtering, and focus ownership. Find Room Enter still navigates to the first match. Keep the task unchecked until runtime search/focus/shortcut checks pass.
- **T15 Inspect/measurement copy — IMPLEMENTED; manual acceptance pending:** Ctrl+C in Scan copies the locked/hovered element, source/link, runtime/source identifiers, group/phase and displayed parameters; right-click on an identifier/value row offers Copy Value for that exact readout. Ctrl+C in Measure copies the current visible live/saved/incomplete readout in configured units, including vertical clearance. Commands are scoped to their tool contexts and report binding conflicts. Keep unchecked until clipboard/UI behavior is manually verified.
- **Reply-row commands — IMPLEMENTED; manual acceptance pending:** clicking a reply focuses its stable parent/reply IDs; the row supports Copy Reply Content and Delete Reply from its stationary right-click menu and Shift+F10/Menu key. Keyboard Ctrl+C/Delete target the focused reply. Delete shares the existing 3-second arm/confirm behavior with the button. Keep unchecked until UI target and confirmation checks pass.
- Validation evidence and still-unrun manual checks are recorded in [validation](review-workflow-usability-validation.md). Integrated results: Viewer Release build passed with 0 errors/155 warnings; App 81/81 and Core 526/526 passed through framework-specific builds and direct DLL runs.
