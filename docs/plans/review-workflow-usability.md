# Implementation Plan: Cải thiện thao tác review và báo cáo Excel

**Trạng thái:** Người dùng đã duyệt spec/PLAN và yêu cầu điều phối subagents triển khai — 19/11/2026. Thực hiện theo lát nhỏ và checkpoint; không tự commit/deploy.

**Spec nguồn:** [review-workflow-usability.md](../specs/review-workflow-usability.md).

## 1. Overview và cổng phê duyệt

Triển khai bốn luồng: chỉnh văn bản chuẩn Windows, menu/shortcut theo focus trên toàn giao diện hiện hành, double-click clash để Show in Revit và xuất Excel có checkbox thật. Giữ nguyên renderer, giao thức live, schema dữ liệu và export/import hiện có.

Môi trường đích: Windows 11 x64; Excel for Microsoft 365 desktop 64-bit, Current Channel, có in-cell checkbox. Không nhập Excel về BimGo và không yêu cầu cài Excel để xuất file.

Người dùng đã duyệt spec và plan, đồng thời yêu cầu triển khai trực tiếp bằng subagents. [Task tracker](review-workflow-usability-tasks.md) cụ thể hóa các lát đã duyệt; không mở rộng phạm vi. Package mới và các thay đổi ngoài ranh giới đã duyệt vẫn cần xin phép. Bằng chứng kiểm thử tự động không thay nghiệm thu UI/Excel/Revit thực tế.

## 2. Bằng chứng từ mã nguồn

| Vị trí | Phát hiện | Hệ quả thiết kế |
|---|---|---|
| `src/BimGo.App/Platform/GameWindow.cs:464–498` | WndProc chuyển key/char/mouse vào InputState; mất focus giải phóng input | Phải kiểm chứng vòng đời focus/capture khi dùng editor native; không chỉ thêm handler Ctrl+C |
| `src/BimGo.App/Platform/InputState.cs:19–23,138–140` | Mảng key theo frame, buffer 64 char, chưa có mô hình selection/caret | Không đẩy cả clipboard qua buffer char theo frame vì có thể bị mất nội dung |
| `src/BimGo.App/Game/GameSession.Menu.cs` | Nhiều editor dùng BeginTextEdit và editor chung | Bắt đầu bằng một luồng đổi tên Test rồi mở rộng qua adapter chung |
| `src/BimGo.App/Game/GameSession.Overlays.cs:20–30` | Dropdown hiện giữ labels và callback, chưa có command model enabled/shortcut | Tách command availability/target khỏi phần vẽ menu; giữ dropdown chọn giá trị hoạt động |
| `src/BimGo.App/Game/Clashes/GameSession.ClashDock.cs:476–545` | Row click được gom sau khi vẽ; group và member có nhánh riêng | Nhận dạng double-click theo Clash ID và vùng hit-test, không theo index có thể đổi khi scroll/filter |
| `src/BimGo.App/Game/Clashes/GameSession.ClashMode.cs` | ShowClashInRevit đã xử lý session, selection và request | Double-click dùng chung đường thực thi; không viết bridge mới |
| `src/BimGo.App/Game/Clashes/GameSession.ClashReport.cs:25–40,124–155` | Reports có scope shown/selection/test/all và logic chọn danh sách | Tái sử dụng projection/scope hiện có, nhưng Excel chỉ cung cấp ba scope trong spec |
| `src/BimGo.Core/Clashes/ClashFiles.cs` | JSON trao đổi sets/tests/results/groups và merge | Không thêm định dạng chia sẻ; giữ kiểm thử hồi quy |

## 3. Architecture Decisions — Đề xuất để duyệt

### A. Editing: ưu tiên tái sử dụng hành vi Windows, không tự viết một text engine lớn

- Thử editor native Windows/WinForms trong viewer hiện tại, chỉ hiển thị khi một ô được nhập; giữ viewport OpenGL. Không chuyển ứng dụng sang WPF.
- Adapter editor giữ hợp đồng begin/current value/commit/cancel, max length, read-only, single/multiline và vị trí theo DPI. Không tự thay Enter/Esc hay validation của caller.
- Kiểm chứng editor một dòng và nhiều dòng: selection/caret, clipboard dài, undo/redo, Unicode/bộ gõ, Tab, resize/DPI, Alt+Tab, đóng model và dispose handle.
- Không giả định control native tự đáp ứng toàn bộ spec: undo/redo, context menu, chuẩn hóa newline và giới hạn Unicode đều phải thử. Nếu không đạt, trình bày phương án thay thế và chi phí trước khi mở rộng.
- Dùng control native là thay đổi cơ chế input cần duyệt riêng theo spec. Việc lập plan này chưa cấp phép thay GameWindow.

### B. Focus và command routing dùng chung, không lặp shortcut ở mỗi panel

- Một focus owner tại một thời điểm: modal/editor → menu → panel/list → viewport. Đóng modal/model hoặc mất focus phải reset gesture và phím giữ.
- Mỗi command có ID, label, shortcut hiển thị, target/context, CanExecute và Execute. Nút/menu/shortcut gọi cùng command hoặc cùng handler nghiệp vụ; không xây một framework command ngoài nhu cầu này.
- Giữ keymap người dùng. Lệnh chỉnh text theo chuẩn Windows chỉ áp dụng trong editor; action đối tượng bổ sung vào catalog/context phù hợp sau kiểm tra xung đột.
- Tách menu hành động khỏi dropdown chọn giá trị nếu cần enabled state/keyboard khác nhau. Click ngoài đóng menu phải bị tiêu thụ, không xuyên viewport.
- Clipboard văn bản dùng plain text; Test/Set thêm payload có version và dấu phiên/model, validate trước Paste. Không deserialize đối tượng tùy ý hay tin dữ liệu clipboard là dữ liệu nội bộ hợp lệ.

### C. Double-click là một gesture của dòng clash

- Theo dõi Clash ID, timestamp, vị trí, modifier và chuyển động; dùng ngưỡng double-click của Windows, reset khi đổi model/test/filter, scroll, mở modal hoặc mất focus.
- Không suy double-click chỉ từ cờ LeftPressed theo frame: phải giữ thứ tự click/timing đủ chính xác để hai click trong một frame không bị mất hoặc gom sai. Cách bổ sung event nhỏ sẽ chốt sau spike input.
- Sau hit-test, chọn đúng clash và gọi ShowClashInRevit đúng một lần. Không mở Revit từ textbox, group header, icon markup hay thao tác Ctrl/Shift.
- Test logic gesture độc lập với Revit; nghiệm thu live mới xác nhận camera/section box/link.

### D. Excel là đầu ra mới, không sửa mô hình trao đổi dữ liệu

- Tạo snapshot bất biến của scope và report rows tại lúc xác nhận xuất: dữ liệu, count, units, metadata và danh tính clash nhất quán dù filter/selection thay đổi sau đó.
- Ưu tiên module writer riêng trong viewer, ví dụ `src/BimGo.App/Reports/`, không kéo package Excel vào Core đa target hoặc add-in.
- Kiểm chứng hai ứng viên: thư viện .xlsx có hỗ trợ in-cell checkbox rõ ràng; hoặc thư viện Open XML với metadata cần thiết dựa trên file mẫu Excel hợp lệ. Không tự mặc định library xuất Boolean là xuất được checkbox.
- Chưa chọn package/version. Báo cáo spike phải ghi API dùng, giấy phép, phụ thuộc, kích thước đóng gói và kết quả mở/tích/save/sort/filter trong Excel đích; xin duyệt trước khi thêm dependency sản phẩm.
- Workbook dự kiến có sheet Metadata và sheet Clashes dạng table; mỗi clash một dòng, Done là checkbox trong ô, khởi tạo chưa tích. Dữ liệu tự do ghi thành text, không formula/hyperlink.
- Luồng Excel không chụp ảnh, không chạy hàng đợi JPEG và không tự mở bằng luồng trình duyệt HTML. Không thay đổi lựa chọn scope/ảnh của báo cáo cũ.
- Ghi vào file tạm cùng thư mục rồi thay đích sau khi hoàn tất; dọn riêng file tạm do lần xuất tạo ra. Nếu write/replace thất bại giữ file cũ và báo lỗi. Đưa progress/cancel vào luồng nếu phép đo mẫu lớn cho thấy cần; không gọi GL từ worker.

### E. Kiểm thử không buộc UI phụ thuộc Core

- Giữ regression dữ liệu trong `tests/BimGo.Core.Tests`.
- Đề xuất project `tests/BimGo.App.Tests` target `net8.0-windows`, dùng MSTest.Sdk 4.4.1 như repo; test logic input/command/projection/writer không khởi tạo OpenGL hoặc Revit.
- Cần duyệt project test và cơ chế truy cập internal trước khi tạo. Native focus/IME và Excel checkbox vẫn cần kiểm thử Windows/Excel thực tế; unit test không thay nghiệm thu này.

## 4. Dependency Graph

```text
Duyệt spec + PLAN
    |
    +-- Kiểm kê UI + hợp đồng command ----------------------+
    |                                                      |
    +-- Spike editor/focus --> duyệt kỹ thuật --> editor Test --> các editor/search còn lại
    |                                                      |
    |                        focus/command/menu ------------+--> Test/Set --> Group/Result
    |                                                      +--> Issues/Inspect/các màn hình còn lại
    |                                                      +--> gesture double-click --> live Revit
    |
    +-- Spike checkbox Excel --> duyệt writer/dependency --> workbook mẫu --> Reports end-to-end
                                                               |
                                             test lỗi/10.000 clash/Excel thực tế
    |
    +-- Regression dữ liệu + hướng dẫn + nghiệm thu tích hợp <--- tất cả nhánh
```

Input/focus, menu và các file GameSession dùng chung phải làm tuần tự, không sửa cạnh tranh. Nhánh Excel có thể được thực hiện ở phiên độc lập sau khi chốt contract report rows/scope; không cần đợi chuyển hết textbox.

## 5. Lát công việc theo thứ tự và checkpoint

Các lát là đầu ra có thể kiểm tra, không phải một lần sửa tất cả file. Khi sang TASKS, mỗi lát phải tách thành task S/M, tối đa khoảng 5 file và không quá ba acceptance criteria; vượt giới hạn thì tách tiếp. Đường dẫn mới dưới đây chỉ là vị trí dự kiến, không phải file đã tồn tại.

| Lát | Đầu ra/điểm nghiệm thu | Phụ thuộc | Cụm file liên quan khi chia TASKS |
|---|---|---|---|
| P0. Kiểm kê phạm vi | Đi từng màn hình còn truy cập được, ghi entry point, control, command/shortcut, hiện có/thiếu/N/A và lý do; chốt semantics markup/settings/read-only | Duyệt spec/plan | Tài liệu kiểm kê mới trong `docs/plans/`, `GameSession.Menu.cs`, `GameSession.ReviewPanels.cs`, `Shell/`, `Revit/Forms/` chỉ đọc |
| P1. Thử editor nhỏ | Rename Test một dòng và notes nhiều dòng trên harness/nhánh thử; chứng minh focus, tiếng Việt, undo/redo, DPI và không rò input | P0, duyệt phạm vi spike | `Platform/GameWindow.cs`, adapter editor mới, báo cáo thử; không migrate hàng loạt |
| P2. Thử workbook checkbox | Tạo mẫu không cần cài Excel ở máy xuất; bấm/save/reopen/sort/filter trong Excel 365, ghi build và lựa chọn kỹ thuật | Duyệt phạm vi spike | Harness/sample tách biệt, báo cáo kỹ thuật; chưa thêm package sản phẩm |

### Checkpoint A — Fail fast

- [ ] Bảng UI không bỏ sót Home, add-in dialogs và bề mặt còn hiển thị; phân biệt code còn lưu với chức năng còn truy cập.
- [ ] P1/P2 có bằng chứng đạt hoặc blocker; không giả pass vì máy không có Excel hoặc bộ gõ.
- [ ] Người dùng duyệt editor native/phương án thay thế, writer/package và project test nếu cần. Cập nhật spec/plan trước khi dùng lựa chọn khác.

| Lát | Đầu ra/điểm nghiệm thu | Phụ thuộc | Cụm file liên quan khi chia TASKS |
|---|---|---|---|
| P3. Rename Test hoàn chỉnh | Luồng đầu tiên có chọn chữ, clipboard, caret, undo/redo, menu text và Enter/Esc; test focus không kích hoạt R/Delete model | P1, checkpoint A | Adapter đã chọn, `GameSession.Menu.cs`, `Clashes/GameSession.ClashPanel.cs`, test tương ứng |
| P4. Textbox chung | Set/Group names, notes/assignee, comment/reply dùng cùng adapter; giữ max length/validation và lưu/mở lại | P3 | Chia task theo caller: ClashPanel; ClashDock/ClashGroups; Menu/Comments, mỗi task tối đa 5 file |
| P5. Search còn lại | Clash search trước, rồi Room/room parameters và Controls; các ô khác theo kiểm kê; filter vẫn cập nhật đúng và Tab/focus rõ | P3, P0 | Chia task theo `ClashDock`, `Rooms`/`RoomOverlay`, `Controls`; không migrate cả cụm trong một task |

### Checkpoint B — Một luồng editing dùng được

- [ ] Rename, notes/comment và search hoạt động bằng chuột/phím; tiếng Việt và clipboard dài không mất dữ liệu.
- [ ] Tab/Alt+Tab, DPI 100/150/200%, mất focus/đóng model không để input bị giữ; Ctrl+A/Delete/R không lọt xuống model.
- [ ] Build/tests đạt; rà soát danh sách caller còn lại trước khi nhận đã hoàn tất textbox toàn giao diện.

| Lát | Đầu ra/điểm nghiệm thu | Phụ thuộc | Cụm file liên quan khi chia TASKS |
|---|---|---|---|
| P6. Menu và shortcut cho Test | Command target/CanExecute rõ, Rename F2 và Copy/Paste cấu hình Test cùng session; giữ Run/Review/xác nhận Delete | P3, P0 | Menu/command adapter mới, `ClashPanel`, catalog/context cần thiết, tests; tách foundation và tích hợp nếu quá 5 file |
| P7. Menu cho Set và Group | Set Paste ID mới, validate rules/tham chiếu; Group Rename/Copy thông tin và menu nhóm cũ không hồi quy | P6, P4 | Tách Set và Group thành task riêng trong `ClashPanel`, `ClashGroups`, tests |
| P8. Result → Revit | Menu Copy/Show in Revit và double-click đúng clash; giữ R, multi-selection và right-drag; một request cho một gesture | P6, P1 | Chia gesture unit-test và tích hợp row thành task riêng: `InputState`, `GameWindow`, `ClashDock`, `ClashMode`, `ClashSelection`, tests |

### Checkpoint C — Review clash bằng menu/phím

- [ ] Menu dùng được qua chuột, Shift+F10/Menu, Up/Down/Enter/Esc; disabled command không chạy; selection cũ không bị tác động nhầm.
- [ ] Test/Set copy-paste không nhân bản result/history/issue; clipboard từ model/session khác hoặc sai định dạng bị từ chối an toàn.
- [ ] Double-click kiểm tra host/link trong live và offline/mất kết nối; ghi rõ host Revit đã/chưa nghiệm thu, không suy từ unit test.

| Lát | Đầu ra/điểm nghiệm thu | Phụ thuộc | Cụm file liên quan khi chia TASKS |
|---|---|---|---|
| P9. Issues | Copy nội dung, Edit/Delete hợp lệ qua cùng handler; text reply/assignee theo chuẩn; không Paste nhân bản issue | P4, P6 | `GameSession.Comments.cs`, `GameSession.Menu.cs`, command adapter và tests; chia list/detail nếu cần |
| P10. Inspect/read-only | Copy giá trị/thông số/định danh, menu không cho sửa; chọn chữ không chọn model phía sau | P5, P6 | Các module Scan/Rooms/RoomOverlay/ReviewPanels; chia task theo panel có thật trong P0 |
| P11. Phần còn lại | Home/Settings/Controls/Markup/Bookmarks nếu hiện hữu và dialog add-in có kết luận, menu và focus đúng kiểm kê; không phục hồi chức năng bị cắt | P0, P4–P6 | Một task cho mỗi màn hình hoặc tối đa 5 file; dùng control WPF native hiện có ở add-in, không đổi theme |

### Checkpoint D — Phạm vi toàn giao diện

- [ ] Mỗi dòng kiểm kê có bằng chứng test hoặc N/A có lý do; caller cũ không còn editor tự xử lý Backspace-only ngoài ngoại lệ đã duyệt.
- [ ] Giữ các shortcut công cụ markup, keybinding người dùng và shortcut chuẩn trong native dialog.
- [ ] Không dùng blanket Copy/Paste/Delete cho đối tượng không có ngữ nghĩa hợp lệ; không tác động model từ click menu.

| Lát | Đầu ra/điểm nghiệm thu | Phụ thuộc | Cụm file liên quan khi chia TASKS |
|---|---|---|---|
| P12. Workbook từ report rows | Writer được duyệt tạo Metadata/Clashes, checkbox chưa tích và text-safe; test dữ liệu/đơn vị/ID, không phụ thuộc GL/Revit/Excel COM | P2, checkpoint A | Module `Reports/` mới, project dependency đã duyệt, App.Tests; chia projection và writer thành task riêng |
| P13. Reports → Excel | Chọn test/shown/selection, xem count, lưu file; không dùng all-tests của báo cáo cũ cho Excel và không chạy capture ảnh | P12 | `Clashes/GameSession.ClashReport.cs`, `ClashDock`, adapter xuất Excel mới, tests |
| P14. Xuất an toàn | Hủy/lỗi quyền/đích bị khóa không phá file cũ; thử 1/100/10.000 clash; checkbox đúng ID sau save/sort/filter trong Excel đích | P13 | Writer/service, test lỗi, checklist và biên bản Excel; tách sửa hiệu năng nếu đo thấy cần |

### Checkpoint E — Excel end-to-end

- [ ] File do chính BimGo xuất đạt SC-07/08/09, không phải file mẫu đã sửa tay; ghi Version/Build Excel.
- [ ] Máy không cài Excel vẫn xuất được; Done độc lập Clash Status; notes đầu =/+/-/@ không thành formula.
- [ ] HTML/XML/JSON Reports cũ vẫn giữ scope/options/hành vi và kết quả.

| Lát | Đầu ra/điểm nghiệm thu | Phụ thuộc | Cụm file liên quan khi chia TASKS |
|---|---|---|---|
| P15. Hồi quy dữ liệu | JSON export/import lặp, .bimgo lưu/mở, statuses/groups/IDs, HTML/XML và BCF không mất dữ liệu | Các nhánh chức năng đã tích hợp | Các test Core tương ứng và fixture mới tối thiểu; không sửa schema |
| P16. Hướng dẫn và nghiệm thu | Cập nhật controls/clash/report guide, phân biệt Excel/JSON/.bimgo; đối chiếu SC-01…11 và ghi mục chưa chạy | P15, checkpoint B–E | `docs/controls.md`, `docs/clash-detection.md`, hướng dẫn/test evidence liên quan; không thêm deploy |

### Checkpoint F — Hoàn tất để review

- [ ] Toàn bộ SC của spec có evidence; các host/Excel chưa chạy được nêu riêng, không tuyên bố nghiệm thu đầy đủ.
- [ ] Test/build liên quan đạt, diff được review, không có schema/package/shortcut thay đổi chưa duyệt.
- [ ] Không tự commit, cài đè, deploy add-in hoặc chạy chỉnh sửa model Revit.

## 6. Verification Commands

Chạy từ root, sau từng task triển khai có liên quan; chỉ ghi pass khi đã thực thi thành công.

```powershell
dotnet build src/BimGo.App/BimGo.App.csproj -c Release -p:DeployBimGoLocally=false -m:1
dotnet test --project tests/BimGo.Core.Tests/BimGo.Core.Tests.csproj -c Release
git diff --check

# Chỉ sau khi project mới được duyệt và tạo
dotnet test --project tests/BimGo.App.Tests/BimGo.App.Tests.csproj -c Release

# Kiểm thử UI thủ công trên Windows 11
dotnet run --project src/BimGo.App/BimGo.App.csproj -c Debug -p:DeployBimGoLocally=false

# Nếu chạm add-in hoặc cần xác minh package/API theo năm, với API/SDK tương ứng
dotnet build src/BimGo.Revit/BimGo.Revit.csproj -c "Release R26" -p:DeployBimGoLocally=false -m:1
```

Đối với R23–R27, thay cấu hình đúng năm ở task nghiệm thu tương ứng. Không cài add-in/khởi động host chỉ vì lệnh build đạt. Cú pháp test phải khớp Microsoft.Testing.Platform và SDK thực tế; nếu baseline không chạy, ghi nguyên nhân và đề xuất điều chỉnh trước khi sửa cấu hình.

## 7. Risks and Mitigations

| Rủi ro | Mức | Cách giảm |
|---|---|---|
| Thư viện ghi Boolean nhưng không tạo in-cell checkbox | Cao | Spike mở/tích/save/sort/filter trên Excel 365 trước khi chọn package; không dùng ảnh/ký tự thay thế |
| Editor native chiếm focus hoặc tương tác xấu với OpenGL | Cao | Thử một field thật, kiểm tra capture/DPI/Alt+Tab/IME; xin duyệt fallback trước migration |
| Control native thiếu undo/redo/validation đúng hợp đồng | Cao | Test rõ hai loại editor và bộ gõ, không mặc định mọi Windows control đều đáp ứng |
| WM_CHAR và cờ theo frame mất input/gesture | Cao | Clipboard không đi qua buffer 64 char; dùng timing/thứ tự event cho double-click; test nhiều click cùng frame |
| “Toàn UI” mở rộng vô hạn | Cao | Bảng entry point/control thực sự truy cập được, N/A có lý do; chia task theo màn hình, không theo mọi file còn lưu |
| Ctrl+A/Delete/Ctrl+Z xung đột model | Cao | Focus routing trước command, test âm và dùng chung handler; giữ keymap người dùng |
| Clipboard cấu hình không hợp lệ hoặc khác model | Vừa | Version/session/model marker, validate ID/rules, chặn Paste không hợp lệ; không clone results |
| Excel lớn chậm hoặc checkbox lệch khi sort | Cao | Benchmark 10.000 dòng và thử checkbox gắn ID; không mở rộng bằng checkbox floating chưa kiểm chứng |
| Thiếu Excel/Revit cho nghiệm thu | Cao | Ghi blocked/not-run và chuẩn bị artifact/checklist để người có host thử; không suy từ build |
| Sửa nhiều màn hình cùng lúc làm hồi quy | Vừa | Vertical slice đầu tiên rồi từng caller/panel, tối đa khoảng 5 file/task và checkpoint mỗi 2–3 task |

## 8. Song song và ước lượng

- Có thể tách nhánh Excel khỏi nhánh UI sau khi chốt snapshot/rows contract; có thể chạy regression/hướng dẫn ở phiên độc lập sau khi hành vi ổn định.
- Phải tuần tự: thay input/focus/overlays, chốt command contract, migrate các caller cùng shared editor và thay Reports scope.
- Chỉ là cơ hội tổ chức công việc; không tự spawn agent hoặc tạo worktree trong bước lập plan.
- Chưa đưa tổng ngày công cố định: P1/P2 và kiểm kê toàn UI có thể đổi đáng kể chi phí. Tại checkpoint A, cập nhật ước lượng từng task S/M theo số caller, writer thực tế và môi trường host có sẵn; tách thời gian chờ nghiệm thu khỏi thời gian code.

## 9. Các quyết định cần duyệt trước TASKS/IMPLEMENT

1. Xác nhận spec là baseline, gồm Done ban đầu bỏ tích, Excel không ảnh và Paste Test/Set chỉ cùng model/session.
2. Cho phép lên task thử editor native/WinForms mà không chuyển WPF; nếu không đạt sẽ trình lại phương án, không tự viết lại input toàn bộ.
3. Cho phép lên task thử checkbox và đề xuất writer/package; chưa phê duyệt package cụ thể.
4. Chấp thuận phương án project test Windows riêng và cách kiểm thử không khởi tạo GL/Revit; chi tiết project/dependency vẫn trình ở TASKS.
5. Sau khi duyệt PLAN, tạo TASKS riêng: mỗi task có tối đa khoảng 5 file, tối đa ba acceptance criteria, dependency rõ, lệnh verify cụ thể và manual check. Đặt checkpoint sau mỗi 2–3 task; không gộp một lát P4/P5/P11 thành task lớn.

**Bước tiếp theo:** triển khai các task được giao trong [task tracker](review-workflow-usability-tasks.md), ghi kết quả và blocker theo từng checkpoint. Không tự commit/deploy hoặc tuyên bố nghiệm thu host khi chưa chạy.
