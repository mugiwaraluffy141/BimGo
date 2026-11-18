# Kế hoạch thay Tool Guns bằng panel review

**Trạng thái:** Chờ duyệt kế hoạch. Phạm vi tính năng theo [spec Clash Review](../specs/clash-review.md).

Khi mở model live hoặc file `.bimgo`, viewer hiện sẵn ba panel **Clash Review**, **Inspect** và **Issues**. Người dùng thu gọn từng panel bằng nút trên tiêu đề. Con trỏ luôn hiện, giữ chuột phải và kéo trên vùng model để xoay camera. Chọn lệnh bằng nút, sau đó chọn phần tử hoặc điểm bằng chuột trái. Phím tắt hữu ích có thể gọi cùng lệnh; không còn Gun Bar, Gun Wheel hoặc thao tác chọn công cụ bằng vòng xoay.

| Panel | Nút và hành vi |
|---|---|
| Clash Review | Tests, Results, Previous / Next, Focus và Reports. Dùng lại danh sách clash và hộp thoại hiện có. Focus quay về kết quả đang chọn; khi chưa có kết quả thì tắt các nút cần selection. |
| Inspect | Select / Scan, Measure, Section Box và Find Room. Measure hiện trực tiếp các chế độ khoảng cách, diện tích, góc và khoảng hở. Giữ kết quả đo, thuộc tính, ẩn/cô lập và lệnh hiện phần tử trong Revit khi có live session. |
| Issues | Add Comment, Comments, Markup và Import / Export BCF. Markup dùng issue đang chọn; nếu chưa chọn, người dùng mở danh sách trước. Import/Export dùng hộp thoại và phạm vi xuất đang có. |

Giữ theme MEPTools đã áp dụng. Tạm lưu trạng thái thu gọn trong phiên viewer, mặc định mở cả ba khi bắt đầu phiên mới. Giữ vị trí các panel ở một cột; nếu cửa sổ thấp, cuộn nội dung, các tiêu đề vẫn truy cập được. Dùng lại kiểu nút của `PanelShell` và các màn hình chi tiết hiện có. Không đổi dữ liệu clash, schema `.bimgo`, BCF hoặc giao thức với add-in.

## Thứ tự triển khai

Mỗi dòng là một lát có thể kiểm tra độc lập. S khoảng 1–2 file, M khoảng 3–5 file. Các file được nêu là điểm bắt đầu, tính từ `src/BimGo.App/Game/` trừ khi ghi rõ.

| Việc | Phụ thuộc | Tiêu chí hoàn thành và cách kiểm tra | File dự kiến |
|---|---|---|---|
| 1. Panel hiển thị sẵn | — | Mở model thấy ba tiêu đề và các nút. Collapse từng panel chỉ đóng nội dung của panel đó. Đổi kích thước cửa sổ và DPI, xác nhận cả ba tiêu đề truy cập được và panel không đè lên hộp thoại. | M · file partial mới cho review panels, `GameSession.PanelShell.cs`, `GameSession.Render.cs` |
| 2. Điều khiển camera và ưu tiên input | 1 | Mở model với con trỏ tự do. Kéo phải khởi đầu trên viewport mới xoay camera. Kéo phải trong bảng clash vẫn mở menu dòng như trước. Nút, ô nhập, cuộn và hộp thoại giữ input của chúng. Kiểm tra click ở mép panel, kéo qua mép, mất focus, Esc và chuyển qua lại Clash Mode. | M · `GameSession.cs`, `Clashes/GameSession.ClashMode.cs`, partial panel, input cửa sổ nếu cần |
| 3. Nối Clash Review | 2 | Chạy test từ panel, xem danh sách, tiến/lùi, focus cặp và mở Reports. Kiểm tra trạng thái không có test, kết quả trống, và kết quả đang được chọn. Kiểm tra phím hoặc nút không đổi status khi ô tìm kiếm/hộp thoại đang giữ focus. | M · partial panel, `Clashes/GameSession.ClashDock.cs`, `Clashes/GameSession.ClashMode.cs`, `Clashes/GameSession.ClashReport.cs` |
| 4. Nối Select / Scan và định vị | 2 | Bấm Scan rồi chọn ngay phần tử dưới con trỏ, đọc thuộc tính, ẩn/cô lập và hiện phần tử trong Revit khi live. Find Room và Section Box mở được từ panel. Kiểm tra với một phần tử host và một phần tử link. | M · partial panel, `Guns/ScanGun.cs`, `GameSession.Rooms.cs`, module section |
| 5. Nối Measure | 4 | Chọn chế độ đo bằng nút, click các điểm trên viewport và lưu kết quả như trước. Esc hủy phép đo dở trước khi thoát công cụ. Click panel hoặc bắt đầu xoay camera không thêm điểm. Kiểm tra đủ bốn chế độ trên cùng một file mẫu. | M · partial panel, `Guns/MeasureGun.cs`, `GameSession.Measurements.cs` |
| 6. Nối Issues | 2 | Đặt comment tại vị trí con trỏ, chọn issue, mở markup, nhập/xuất BCF. Kiểm tra bấm panel không tạo comment lên model. Lưu/mở lại `.bimgo` và xác nhận ảnh, viewpoint và markup còn đủ. | M · partial panel, `Guns/CommentGun.cs`, `GameSession.Comments.cs`, `GameSession.Markup.cs`, `GameSession.Bcf.cs` |
| 7. Gỡ giao diện Gun Bar / Gun Wheel | 3, 5, 6 | Gỡ render, cập nhật input và các đường vào bằng chuột, phím, gamepad cho bar/wheel. Không còn chữ gun trong UI review. Giữ logic chọn/đo/comment có ích trong các lớp hiện có nếu việc chuyển lớp không cần thiết. Kiểm tra nút và lệnh còn dùng được khi cả ba panel đang thu gọn. | M · `GameSession.cs`, `GameSession.Render.cs`, `GameSession.GunWheel.cs`, `GameSession.Gamepad.cs` |
| 8. Đồng bộ phím tắt và trợ giúp | 7 | Phím tắt gọi cùng hành động với panel, với scope theo ngữ cảnh. Các phím số của clash status giữ ưu tiên khi danh sách clash có focus. F1 và rebind dùng tên lệnh mới, đọc được settings cũ. Kiểm tra bindings cũ và gamepad sau khi bỏ vòng chọn. | M · `src/BimGo.Core/Scene/InputCatalog.cs`, `GameSession.Controls.cs`, `GameSession.Hints.cs`, `tests/BimGo.Core.Tests/ControlsTests.cs` |
| 9. Cập nhật tài liệu và nghiệm thu | 8 | Bắt đầu review từ panel theo tài liệu, không cần mở menu Esc để tìm lệnh. Xác nhận Go/live và file offline, Find Room, chạy clash, đo, comment/markup, xuất HTML/XML và trao đổi BCF. Ghi riêng kiểm tra live chưa chạy được nếu thiếu host. | M · `docs/controls.md`, trang hướng dẫn tool, `docs/clash-detection.md`, `docs/comments-and-bcf.md` |

## Mốc kiểm tra

1. **Sau 1–2:** trên cửa sổ thường và nhỏ, click panel không xuyên xuống model, kéo phải trên viewport xoay camera, menu chuột phải của clash vẫn hoạt động. Ưu tiên kiểm tra mốc này trước khi chuyển các lệnh.
2. **Sau 3–5:** chạy clash và kiểm tra cặp, dùng Scan, Measure, Section Box và Find Room từ panel. Dùng ray qua vị trí con trỏ cho thao tác chọn, thay cho giả định tâm màn hình của Tool Guns. Đối chiếu vị trí với điểm biết trước trong model mẫu.
3. **Sau 6–8:** lưu/mở lại issue và phép đo, xuất report/BCF; không còn UI hoặc phím gọi bar/wheel; Esc lần lượt đóng hộp thoại, hủy thao tác đang chọn rồi mới quay về menu. Kiểm tra settings, phím tắt và gamepad.
4. **Sau 9:** build viewer và chạy bộ Core tests. Kiểm tra bản build trên file `.bimgo` mẫu và một phiên Revit live. Chạy build bằng `dotnet build src/BimGo.App/BimGo.App.csproj -c Release -m:1 -p:DeployBimGoLocally=false` và test bằng `dotnet test tests/BimGo.Core.Tests/BimGo.Core.Tests.csproj -c Release`.

Các lát Clash Review và Issues có thể tách riêng sau khi chốt cách xử lý input. Select / Scan và Measure dùng chung đường chọn trong viewport nên làm theo thứ tự. Sau khi Minh duyệt và yêu cầu triển khai, GPT‑6 Luna viết code, GPT‑6.2 Sol review từng mốc và gửi lỗi lại cho Luna sửa cho đến khi hết lỗi cần xử lý và các kiểm tra đạt.
