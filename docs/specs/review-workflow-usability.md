# Spec: Cải thiện thao tác review và báo cáo Excel

**Trạng thái:** Người dùng đã duyệt spec và plan, yêu cầu triển khai bằng subagents — 19/11/2026. Nghiệm thu UI/Excel/Revit vẫn phải ghi bằng chứng riêng.

## 1. Objective — Mục tiêu

Giúp BIM coordinator và modeler xử lý clash nhanh hơn: sửa văn bản như ứng dụng Windows thông thường, thao tác bằng menu chuột phải/phím tắt, double-click để định vị trong Revit và gửi báo cáo Excel có checkbox theo dõi công việc.

Thành công được xác định bằng các tình huống nghiệm thu ở mục 10, không chỉ bằng việc có thêm nút hoặc build thành công.

### Các quyết định người dùng đã xác nhận

- Excel chỉ phục vụ theo dõi bên ngoài; không nhập thay đổi từ Excel về BimGo.
- Cần checkbox tương tác thật, không thay bằng dropdown hoặc ký tự đánh dấu.
- Chuẩn hóa context menu và phím tắt trên toàn bộ giao diện đang được cung cấp, không chỉ Textbox/Test/Set/Group.
- Theo yêu cầu chọn một phiên bản đích, dùng **Excel for Microsoft 365 desktop, 64-bit, Current Channel trên Windows 11**, đã cập nhật tại thời điểm nghiệm thu và có tính năng checkbox trong ô (in-cell checkbox). Ghi lại Version/Build cụ thể trong biên bản kiểm thử.
- Windows 11 x64 là môi trường đích cho người dùng trong phạm vi spec này. Không yêu cầu nghiệm thu Windows 10 hoặc các phiên bản Excel khác.

### Giả định đã được duyệt cùng spec

- Cải tiến viewer hiện tại; không gộp dự án chuyển giao diện sang WPF/MahApps.
- Single-click giữ hành vi chọn/review; double-click trên dòng clash result gọi chức năng Show in Revit hiện có.
- Không thay đổi schema .bimgo, JSON clash, BCF hay giao thức live.
- Toàn bộ giao diện bao gồm các màn hình viewer và dialog add-in hiện còn truy cập được trong bản clash-review; không khôi phục tính năng đã loại khỏi sản phẩm.
- Không coi việc sửa/chọn trong textbox là lệnh tác động vào model hoặc danh sách phía sau.

## 2. Hiện trạng và phạm vi

| Feedback | Hiện trạng đối chiếu mã nguồn | Phạm vi đợt này |
|---|---|---|
| Excel có checkbox | Chưa có .xlsx; báo cáo hiện là HTML/XML | Thêm đầu ra Excel |
| Gửi bộ clash rồi import | Có REPORT… → EXPORT/IMPORT BIMGO REPORT (JSON) | Giữ nguyên, kiểm tra hồi quy và làm rõ hướng dẫn |
| Double-click sang Revit | Có nút REVIT và action mặc định R; chưa có double-click dòng clash | Thêm cách gọi, dùng chung luồng hiện có |
| Quét chọn/chỉnh văn bản | Editor chung chủ yếu thêm/xóa cuối chuỗi; một số search có Ctrl+Backspace xóa hết | Chuẩn hóa editing và focus |
| Context menu/phím tắt | Có menu clash/group và một số shortcut; chưa thống nhất toàn giao diện | Kiểm kê mọi bề mặt và bổ sung theo ngữ cảnh |

Nguồn đối chiếu chính:

- `src/BimGo.App/Game/GameSession.Menu.cs`: editor chung và BeginTextEdit.
- `src/BimGo.App/Game/Clashes/GameSession.ClashDock.cs`: search, REPORT và nút Revit.
- `src/BimGo.App/Game/Clashes/GameSession.ClashMode.cs`: ShowClashInRevit.
- `src/BimGo.App/Game/Clashes/GameSession.ClashGroups.cs`: context menu hiện có.
- `src/BimGo.App/Game/Clashes/GameSession.ClashPanel.cs`: đổi tên, duplicate Test/Set.
- `src/BimGo.Core/Clashes/ClashFiles.cs`: trao đổi JSON.
- `src/BimGo.Core/Clashes/ClashReports.cs`: xuất HTML/XML.

## 3. Yêu cầu chức năng

### 3.1. Chỉnh văn bản chuẩn Windows

Áp dụng cho mọi ô nhập văn bản hiện có: tên Test/Set/Group, rule values, notes, assignee, comment/reply, tên bookmark, search clash/room/controls và các ô nhập khác còn truy cập được. Các trường chỉ đọc cho phép chọn/copy khi có nội dung hữu ích, không cho sửa.

- Click đặt caret; kéo chuột chọn vùng; double-click từ trong textbox để chọn từ, không jump Revit.
- Ctrl+A chọn toàn bộ nội dung của ô đang focus.
- Ctrl+C/X/V copy/cut/paste qua Windows clipboard; cut/paste chỉ cho ô được sửa.
- Gõ hoặc paste thay thế vùng đang chọn. Backspace/Delete xóa vùng chọn; khi không chọn, xóa ký tự trước/sau caret.
- Left/Right, Home/End di chuyển caret; Shift kết hợp mở rộng vùng chọn. Với ô nhiều dòng: Up/Down và Home/End theo dòng.
- Ctrl+Z/Ctrl+Y undo/redo chỉnh chữ trong phiên nhập, không gọi undo/redo model. Giữ giới hạn độ dài và validation của từng trường; không cắt đôi ký tự Unicode.
- Enter/Esc giữ hợp đồng lưu/hủy của editor hiện có; search giữ cách xác nhận/thoát hiện có. Không tự chuyển ô một dòng thành nhiều dòng.
- Context menu văn bản có Undo, Redo, Cut, Copy, Paste, Delete, Select All; trạng thái enabled phụ thuộc selection, khả năng sửa và clipboard.
- Paste vào ô một dòng chuẩn hóa xuống dòng thành khoảng trắng trước khi áp dụng giới hạn. Không thực thi nội dung clipboard; lỗi clipboard không làm ứng dụng crash.
- Tên và ghi chú tiếng Việt có dấu được chọn/copy/paste/lưu/mở lại nguyên vẹn. Nhập trực tiếp bằng bộ gõ tiếng Việt thông dụng phải được kiểm thử; nếu cần thay cách nhận input, trình bày quyết định kỹ thuật trước khi thay.
- Không rò phím/chuột xuống viewport, shortcut clash hay danh sách trong lúc ô/menu đang giữ focus. Tab/Shift+Tab chuyển focus theo thứ tự hợp lý trong dialog/panel, có chỉ báo focus.

### 3.2. Context menu và phím tắt trên toàn giao diện

Trước khi triển khai, lập bảng kiểm kê các màn hình còn truy cập được: Home, Clash Review/Tests/Sets/Groups/Results, Inspect/Find Room/thông số, Issues/comments/replies/markup, Bookmarks nếu còn hiển thị, Settings/Controls và dialog add-in hiện hành. Mỗi bề mặt phải có kết luận: hỗ trợ lệnh nào, không áp dụng lệnh nào và lý do. Không coi việc chỉ sửa editor chung là hoàn tất toàn bộ yêu cầu.

Hợp đồng tối thiểu:

| Ngữ cảnh | Lệnh bắt buộc khi hợp lệ | Phím tắt |
|---|---|---|
| Văn bản được sửa | Các lệnh ở 3.1 | Ctrl+A/C/X/V/Z/Y, Delete |
| Văn bản/thông số chỉ đọc | Copy giá trị hoặc nội dung đang chọn | Ctrl+C |
| Test/Set | Copy cấu hình, Paste thành bản sao, Rename; giữ Run/Review/Delete và lệnh hiện có khi phù hợp | Ctrl+C/V, F2; Delete nếu đối tượng hỗ trợ xóa |
| Group | Rename và các lệnh nhóm hiện có; Copy thông tin group | F2, Ctrl+C; giữ Ctrl+G/Ctrl+Shift+G |
| Clash result | Copy thông tin clash, Show in Revit, các lệnh nhóm/trạng thái hiện có | Ctrl+C, giữ R trong chế độ clash |
| Issue/comment/reply | Copy nội dung; Edit/Delete theo khả năng hiện có | Ctrl+C; Delete chỉ khi hợp lệ |
| Bookmark còn hiển thị | Copy thông tin, Rename, các lệnh điều hướng/xóa hiện có | Ctrl+C, F2; Delete nếu hợp lệ |
| Markup, settings và danh sách khác | Bổ sung menu cho các thao tác có đối tượng/ngữ cảnh rõ ràng; giữ shortcut công cụ hiện có | Quy định trong bảng kiểm kê để duyệt |

- Không đặt tên Rename cho việc sửa nội dung comment; F2 chỉ áp dụng đối tượng có tên sửa được.
- Copy thông tin xuất plain text hữu ích để gửi qua Excel/chat. Test/Set có thể kèm payload có định dạng để Paste cấu hình trong cùng model/session; không nhận dữ liệu clipboard tùy ý như cấu hình tin cậy.
- Paste Test/Set tạo ID mới và tên phân biệt; không sao chép kết quả clash, lịch sử hay liên kết issue. Test giữ tham chiếu Set hợp lệ trong model đích; payload từ model/session khác chưa thuộc phạm vi.
- Không nhân bản clash result, group hay issue qua Ctrl+V. Khi không có ngữ nghĩa Paste phù hợp, lệnh bị vô hiệu hóa hoặc không xuất hiện.
- Click phải vào đối tượng ngoài selection lấy đối tượng đó làm mục tiêu menu; nếu thuộc selection, chỉ cho phép thao tác nhiều đối tượng mà lệnh hỗ trợ. Không âm thầm tác động selection cũ không liên quan.
- Right-click không kéo mở menu; right-drag trong viewport vẫn xoay camera. Esc đóng menu, click ngoài đóng menu và không xuyên xuống model.
- Menu hiển thị shortcut tương ứng; hỗ trợ điều hướng bàn phím Up/Down/Enter/Esc và mở bằng Shift+F10 hoặc phím Menu khi có mục tiêu focus.
- Cùng lệnh từ nút, menu và shortcut phải dùng chung validation/xác nhận/hành vi. Không bỏ qua xác nhận xóa hiện có.
- Phạm vi focus ưu tiên: modal/editor → menu → panel/list → viewport. Ctrl+A trong textbox chọn chữ, trong clash list giữ chọn clash; Delete trong textbox không xóa đối tượng.

### 3.3. Double-click clash → Revit

- Áp dụng trên dòng clash result, kể cả dòng con trong group; không áp dụng vào header group, checkbox, nút, textbox, menu hay vùng trống.
- Hai click trái trên cùng clash trong ngưỡng thời gian/khoảng cách double-click Windows tạo đúng một yêu cầu Show in Revit. Click sang dòng khác, kéo chuột hoặc click có Ctrl/Shift không được coi là double-click điều hướng.
- Target là clash vừa double-click, không phải toàn bộ multi-selection trước đó. Single-click/Ctrl-click/Shift-click giữ hành vi review/chọn hiện có.
- Dùng cùng xử lý cặp phần tử, link transform, camera và section box với nút Revit. Giữ hành vi tạo/tái sử dụng view của luồng hiện tại, không thêm một kiểu view mới.
- Offline hoặc mất kết nối: báo rõ cần phiên Go/live, không gửi yêu cầu sai và không làm mất dữ liệu/selection ngoài hành vi chọn dòng.
- Nút REVIT và shortcut R vẫn hoạt động. Không tự thay shortcut người dùng đã cấu hình.

### 3.4. Báo cáo Excel với checkbox thật

- Thêm lựa chọn Export Excel (.xlsx) trong luồng Reports hiện có. Không thay thế HTML/XML/JSON.
- Người dùng chọn phạm vi: toàn bộ kết quả của test hiện tại, kết quả đang hiển thị theo filter, hoặc selection hiện tại. Hiện rõ phạm vi và số clash trước xuất; không âm thầm xuất cả model.
- Một dòng dữ liệu cho mỗi clash, không gộp group thành một dòng. Giữ tên group thành cột để lọc/sắp xếp.
- Các cột tối thiểu: Done (checkbox), Clash ID, Test, Group, Clash Status, Assigned To, Notes, phần tử A/B (ID và nguồn model/link, category, family/type nếu có), level/grid nếu có, tọa độ clash X/Y/Z và giá trị depth/clearance có đơn vị rõ ràng.
- Metadata báo cáo ghi model, thời điểm xuất, phạm vi/filter và đơn vị. Ô thiếu dữ liệu để trống, không bịa giá trị.
- Done ban đầu bỏ tích cho mọi dòng. Đây là checklist công việc bên ngoài, độc lập với Clash Status; không suy luận một clash resolved chỉ vì người nhận đánh dấu Done.
- Checkbox phải bấm tích/bỏ tích được trong Excel đích và lưu lại khi đóng/mở file; không dùng ký tự ☑/☐, ảnh tĩnh, macro hoặc ActiveX để giả đáp ứng.
- Có header/filter, cố định hàng tiêu đề và định dạng dễ đọc. Khi sort/filter, checkbox và giá trị của nó phải theo đúng Clash ID, không trôi sang dòng khác.
- Xuất .xlsx không cần cài Excel trên máy chạy BimGo. Không nhúng ảnh clash trong đợt này.
- Xuất không sửa trạng thái clash trong BimGo. Hủy dialog không tạo file; lỗi ghi file/đường dẫn/file đang bị khóa phải báo rõ, không để file hỏng thay thế bản đã có.
- Chuỗi do người dùng nhập được ghi dưới dạng dữ liệu văn bản, không thành công thức/link thực thi ngoài ý muốn; kiểm tra tên bắt đầu bằng =, +, -, @ và ký tự XML đặc biệt.

**Môi trường tương thích đã chọn:** Excel for Microsoft 365 desktop, 64-bit, Current Channel trên Windows 11; dùng checkbox trong ô (in-cell checkbox). Trước khi chốt thiết kế bộ xuất, kiểm chứng file mẫu trên môi trường này và ghi Version/Build cụ thể. Không yêu cầu hỗ trợ Excel 2016/2019/2021/2024 bản mua một lần, Excel web/Mac hoặc LibreOffice trong đợt này. Mở được file hoặc chỉ hiển thị TRUE/FALSE chưa đủ đạt yêu cầu checkbox tương tác. Nếu kỹ thuật xuất không tạo được checkbox đúng yêu cầu trên môi trường đích, báo blocker và xin quyết định; không tự hạ yêu cầu xuống dropdown, ký tự đánh dấu hoặc TRUE/FALSE.

### 3.5. Giữ quy trình chia sẻ hiện có

- Giữ export/import JSON trong Reports, không tạo định dạng trao đổi clash mới.
- Hướng dẫn phân biệt: Excel để theo dõi bên ngoài; JSON để nhập/merge clash vào model tương ứng; .bimgo để gửi snapshot hình học và dữ liệu đi kèm.
- Kiểm tra export/import không mất sets/tests/clashes/groups/trạng thái theo hợp đồng hiện tại; import lặp lại không tạo bản trùng cho cùng dữ liệu.

## 4. Tech Stack

- C#, viewer `net8.0-windows`, môi trường nghiệm thu Windows 11 x64; UI hiện tại tự vẽ trên OpenGL, dùng Windows Forms cho các phần hệ thống. Không đổi target framework chỉ vì chọn Windows 11.
- Excel đích: Microsoft 365 desktop 64-bit, Current Channel, đã cập nhật và hỗ trợ in-cell checkbox; không yêu cầu cài Excel trên máy xuất báo cáo.
- `Silk.NET.OpenGL` 2.23.0; không thay renderer/windowing/input stack trong phạm vi này.
- Core hiện multi-target `net8.0;net48`; không đưa phụ thuộc UI/Excel cần host vào Core.
- MSTest.Sdk 4.4.1; test hiện tại target net8.0, chỉ tham chiếu Core.
- Thư viện/cách ghi .xlsx và checkbox chưa được lựa chọn. Thêm dependency cần phê duyệt riêng về khả năng tương thích, giấy phép, kích thước và đóng gói; việc duyệt spec không tự đồng nghĩa duyệt package.

### Trạng thái kỹ thuật khi tiếp tục triển khai (20/11/2026)

Writer BCL (`ZipArchive`/`XmlWriter`, không thêm package) đã qua structural tests nhưng chưa được nghiệm thu trong Excel. Safety preflight phát hiện startup add-in chưa kiểm tra, nên không khởi động Office không giám sát. **Người dùng đã xác nhận bỏ cờ môi trường và cho Excel hiện trực tiếp trong FORMAT.** Không còn yêu cầu `BIMGO_EXCEL_PREVIEW`; hiển thị `FORMAT: EXCEL` cùng các định dạng hiện có và cảnh báo ngắn khi chọn Excel rằng checkbox chưa được nghiệm thu trực tiếp. Quyết định về khả năng truy cập này không đánh dấu pass các SC về checkbox/save/reopen/sort/filter; yêu cầu checkbox thật và môi trường Excel đích giữ nguyên.

## 5. Commands

Chạy PowerShell từ thư mục gốc repo; không tự deploy vào ứng dụng đang cài.

```powershell
# Build viewer và phụ thuộc
dotnet build src/BimGo.App/BimGo.App.csproj -c Release -p:DeployBimGoLocally=false -m:1

# Test logic không cần Excel/Revit
dotnet test --project tests/BimGo.Core.Tests/BimGo.Core.Tests.csproj -c Release

# Chạy viewer để kiểm tra UI
dotnet run --project src/BimGo.App/BimGo.App.csproj -c Debug -p:DeployBimGoLocally=false

# Kiểm tra whitespace của diff
git diff --check

# Chỉ khi có SDK/API tương ứng và phạm vi thay đổi cần kiểm tra add-in
dotnet build src/BimGo.Revit/BimGo.Revit.csproj -c "Release R26" -p:DeployBimGoLocally=false -m:1
```

Lệnh test dùng cú pháp Microsoft.Testing.Platform được chọn trong `global.json`; xác nhận với .NET SDK thực tế khi thực hiện. Repo chưa có lệnh lint riêng được xác nhận; không tự thêm công cụ hoặc formatter quét toàn repo. Các lệnh trên là yêu cầu xác minh, chưa phải kết quả chạy của spec này.

## 6. Project Structure

| Đường dẫn | Trách nhiệm liên quan |
|---|---|
| `src/BimGo.App/Game/Clashes/` | Lists, menu, điều hướng Revit, Reports và tích hợp thao tác clash |
| `src/BimGo.App/Game/GameSession.Menu.cs` | Editor chung |
| `src/BimGo.App/Game/GameSession.Overlays.cs` | Dropdown/overlay và vòng đời menu |
| `src/BimGo.App/Game/GameSession.*.cs` | Comments, Rooms, Controls, Bookmarks và các bề mặt hiện hành |
| `src/BimGo.App/Platform/`, `Native/`, `Rendering/` | Input, focus, clipboard/hệ điều hành, vẽ caret/selection |
| `src/BimGo.App/Shell/` | Home và hành vi ngoài phiên review |
| `src/BimGo.Core/Clashes/`, `Scene/` | Dữ liệu report, logic thuần, khai báo action/phím tắt |
| `src/BimGo.Revit/Forms/` | Dialog add-in hiện có; ưu tiên hành vi control native sẵn có |
| `tests/BimGo.Core.Tests/` | Unit/regression cho logic độc lập UI/host |
| `docs/` | Hướng dẫn controls, clash, báo cáo và bằng chứng nghiệm thu |

Vị trí module input/edit và exporter mới sẽ chốt ở PLAN sau khi spec được duyệt. Không ép đưa logic UI vào Core chỉ để dùng test project hiện có; nếu cần project test riêng cho viewer phải trình bày trong plan.

## 7. Code Style

Theo namespace, định dạng và tên của module được sửa: public PascalCase; private field `_camelCase`; hằng theo phong cách hiện có; C# block braces và early return. Ví dụ thực tế từ `GameSession.Menu.cs`:

```csharp
private void BeginBookmarkRename(BookmarkRecord record, bool isNew)
{
    if (record == null) { return; }
    _editing = true;
    _editRecord = null;
    _editBookmark = record;
```

Đoạn trên là trích đoạn, không phải hàm mới cần thêm. Giữ một đường thực thi cho cùng command từ nút/menu/phím tắt. Không đổi chuỗi trạng thái/ID lưu trữ hoặc format lại file không liên quan.

## 8. Testing Strategy

- **Unit:** selection/caret, thay vùng chọn, undo/redo, giới hạn Unicode, focus routing/command availability, nhận dạng double-click và projection dữ liệu Excel nếu tách được thành logic thuần.
- **File/integration:** cấu trúc .xlsx, số dòng/ID/scope, text không thành formula, metadata/đơn vị, output rỗng và lỗi ghi; regression JSON, HTML/XML và BCF hiện có.
- **UI thủ công:** chuột, clipboard, Tab, menu bàn phím, bộ gõ tiếng Việt, DPI 100%/150%/200%, dialog modal và click không xuyên xuống model. Ghi phiên bản Windows/bộ gõ khi có lỗi input.
- **Excel thực tế:** dùng Excel for Microsoft 365 desktop 64-bit, Current Channel trên Windows 11; ghi Version/Build và phiên bản Windows. Bấm checkbox, save/reopen, sort/filter; kiểm tra mẫu 1, 100 và 10.000 clash. Đo thời gian/kích thước và phản hồi UI; không cam kết ngưỡng hiệu năng chưa đo. Không sửa file thủ công để làm cho mẫu đạt.
- **Revit thực tế:** live double-click với host pair và linked pair, lặp nhiều lần, mất kết nối/offline; xác minh đúng phần tử/vị trí và không tạo thêm view ngoài hành vi nút hiện tại. Mục tiêu hỗ trợ R23–R27; ghi từng host đạt/chưa chạy, không suy từ build sang nghiệm thu host.
- Không đặt tỷ lệ coverage toàn repo mới; mọi hành vi logic mới phải có test dương/âm/biên phù hợp. Không có host/Excel thì đánh dấu chưa nghiệm thu, không đánh dấu pass.

## 9. Boundaries

### Always do

- Bảo toàn dữ liệu/file cũ, định danh, undo model và cấu hình shortcut người dùng.
- Kiểm tra enabled/validation trước mọi command; tiêu thụ input ở đúng focus owner.
- Bổ sung test cho logic mới, chạy test/build liên quan trước commit và ghi các kiểm thử thủ công chưa chạy.
- Cập nhật bảng kiểm kê giao diện, hướng dẫn và spec khi quyết định thay đổi.

### Ask first

- Thêm package, project test, thay cơ chế window/input hoặc dùng native text control thay UI hiện tại.
- Đổi môi trường Excel/Windows đã chọn hoặc hạ yêu cầu checkbox; kỹ thuật xuất và dependency vẫn phải được chốt sau bằng chứng tương thích.
- Thay file schema/live protocol/shortcut mặc định xung đột hoặc mở rộng copy/paste xuyên model.
- Thêm lệnh nhân bản group/issue/result, đổi validation/xác nhận xóa, thay CI/deploy/installer.

### Never do

- Import trạng thái Excel về BimGo, thêm macro/ActiveX hoặc tự biến Done thành trạng thái resolved.
- Thay checkbox thật bằng ký tự/ảnh/TRUE-FALSE rồi báo đã hoàn thành yêu cầu.
- Tự viết lại export/import JSON, thay renderer hay gộp chuyển WPF/MahApps.
- Khôi phục tính năng đã loại khỏi clash-review chỉ để bổ sung menu cho code còn lưu trong repo.
- Xóa test hỏng để lấy pass, ghi secrets, ghi đè công việc người dùng, tự commit/deploy hoặc chạy sửa model Revit ngoài thao tác nghiệm thu được đồng ý.

## 10. Success Criteria — Checklist nghiệm thu

- [ ] **SC-01:** Mọi bề mặt UI hiện hành có dòng trong bảng kiểm kê, command/shortcut hoặc lý do không áp dụng; không còn màn hình bị bỏ sót mà vẫn tuyên bố hỗ trợ toàn giao diện.
- [ ] **SC-02:** Tên Test/Set/Group, notes, comment và search cho phép kéo chọn, Ctrl+A/C/X/V, caret/Delete và thay selection; clipboard/tiếng Việt không hỏng dữ liệu.
- [ ] **SC-03:** Undo/redo trong textbox không tác động model; Delete/Ctrl+A và chữ R đang gõ không kích hoạt thao tác danh sách/Revit. Menu thao tác được bằng chuột và bàn phím.
- [ ] **SC-04:** Copy/Paste Test/Set trong cùng model tạo bản cấu hình ID mới, không nhân bản kết quả/lịch sử/issue; mục tiêu menu và shortcut nhất quán.
- [ ] **SC-05:** Double-click dòng clash hợp lệ gửi đúng một request cho đúng clash; click khác dòng/kéo/Ctrl-Shift/textbox không gửi. Nút Revit và R không hồi quy.
- [ ] **SC-06:** Host và linked clash định vị đúng trong live; offline/mất kết nối báo rõ, không crash hoặc gửi nhầm yêu cầu.
- [ ] **SC-07:** Excel xuất đúng số clash/ID theo ba phạm vi, đủ cột và đơn vị, không thay dữ liệu BimGo; HTML/XML/JSON vẫn dùng được.
- [ ] **SC-08:** Checkbox trong ô bấm được, lưu/mở lại giữ dấu, sort/filter không lệch Clash ID trên Excel for Microsoft 365 desktop 64-bit, Current Channel trên Windows 11; ghi Version/Build nghiệm thu. Không cần Excel trên máy xuất.
- [ ] **SC-09:** Ghi file lỗi/hủy không phá file cũ; notes có ký tự đặc biệt/đầu công thức không thành nội dung thực thi.
- [ ] **SC-10:** JSON import/export và lưu/mở .bimgo giữ dữ liệu review hiện có; hướng dẫn phân biệt JSON/.bimgo/Excel rõ ràng.
- [ ] **SC-11:** Test/build liên quan đạt; bằng chứng UI/Excel/Revit ghi môi trường và các mục chưa kiểm tra một cách tách biệt.

## 11. Open Questions và cổng phê duyệt

1. **Excel — đã chốt môi trường:** Microsoft 365 desktop 64-bit, Current Channel trên Windows 11. Không còn chờ chọn phiên bản; vẫn phải kiểm chứng file mẫu và ghi Version/Build trước khi chốt kỹ thuật xuất checkbox.
2. **Toàn giao diện:** Duyệt bảng kiểm kê và ngữ nghĩa lệnh cho các bề mặt ngoài bảng tối thiểu ở 3.2; đặc biệt markup, settings và các danh sách chỉ đọc.
3. **Mặc định sản phẩm đã duyệt:** Done xuất ra luôn bỏ tích; Excel không có ảnh; Paste cấu hình chỉ cùng model/session.
4. **Kỹ thuật:** Cách cung cấp editing/focus dùng chung và thư viện .xlsx (nếu cần) chỉ được lựa chọn sau kiểm chứng và phê duyệt ở PLAN.

Spec và plan đã được người dùng duyệt, kèm yêu cầu triển khai trực tiếp bằng subagents. Theo dõi tiến độ trong [task tracker](../plans/review-workflow-usability-tasks.md); không tự mở rộng phạm vi, thêm package hoặc bỏ các yêu cầu kiểm chứng UI/Excel/Revit.
