# BimGo cho kiểm tra và phối hợp clash — Revit 2023–2027

**Trạng thái:** Bản đặc tả để duyệt, ngày 15/11/2026.

## Mục tiêu và người dùng

Coordinator và modeler đều có thể tự định nghĩa phép kiểm tra, tìm va chạm, xác minh tại vị trí và giao issue. Một người có Revit xuất mô hình một lần. Đồng nghiệp nhận file `.bimgo` có thể chạy lại test, ghi nhận xét và lưu kết quả ngay cả trên máy không cài Revit. Khi thiết kế thay đổi, người dựng sửa trong Revit và kiểm tra lại trên hình học mới.

## Phạm vi chức năng

| Quy trình | Yêu cầu |
|---|---|
| Chuẩn bị mô hình | Giữ Go (live) và Export .bimgo. Trích xuất host và các instance link cấp đầu theo phạm vi đã chọn, gồm hình học, phép biến đổi, đơn vị, định danh phần tử và nguồn, level, phase, phòng, grid và tham số dùng cho bộ lọc. Hai instance của cùng một link phải phân biệt được. |
| Đặt và chạy test | Tạo search set theo model, category, family/type, level, phase và tham số, có điều kiện loại trừ. Chạy A–B hoặc tự kiểm tra một set. Giữ hard clash với dung sai và clearance với khoảng cách yêu cầu, lưu và trao đổi cấu hình sets/tests. |
| Xác minh | Chọn kết quả để lấy góc nhìn và section box quanh cặp phần tử. Cho phép làm mờ, ẩn hoặc cô lập hình học, bay quanh vị trí, Scan thông số và Measure khoảng cách/khoảng hở. Giữ màu phân biệt cặp clash và phần nền. |
| Định vị | Find Room tìm và đưa người dùng tới phòng. Hiện level, grid, tọa độ để định vị cả các clash ngoài phòng. Có thể quay về góc nhìn của kết quả sau khi di chuyển. |
| Theo dõi | Duyệt, nhóm kết quả theo phần tử, level, grid hoặc thủ công. Nhận diện lại cặp phần tử qua các lần chạy cùng test, bảo toàn trạng thái review theo quy tắc hiện có. Chỉ đánh dấu resolved khi lần chạy hoàn chỉnh cho thấy clash đã biến mất. |
| Quản lý issue | Tạo comment từ clash hoặc tại vị trí tự chọn. Giữ viewpoint, section box, ảnh, trạng thái issue, ưu tiên, người phụ trách và phản hồi. Mở issue để quay về vị trí. Đóng issue không tự đánh dấu clash đã hết. |
| Trao đổi | BCF là tùy chọn. Giữ xuất BCF 2.1 và nhập các phiên bản hiện hỗ trợ (2.0/2.1/3.0), cùng ánh xạ tọa độ và cập nhật issue theo định danh khi nhập lại. Quy trình review nội bộ hoàn chỉnh chỉ cần file .bimgo. |
| Kiểm tra sau sửa | Trong live session, định vị cặp phần tử trong Revit, refresh hình học rồi chạy lại test. Trong file offline, chạy test trên snapshot đã xuất. Sửa file RVT không tự thay đổi snapshot. |

**Đề xuất cho bản đầu:** giữ ảnh issue và các chức năng hiển thị cần để review. Loại khỏi giao diện, ribbon, phím tắt và hướng dẫn các chức năng tour, video, kiosk, entourage, thời tiết, nghiên cứu ánh sáng, đặt/nhân bản/di chuyển/phá phần tử và push nhật ký sửa mô hình. Duy trì renderer, camera, chụp ảnh và đọc dữ liệu dùng chung khi luồng clash còn phụ thuộc. Giữ nguyên ba chức năng của bản gốc ngay từ bản đầu: xuất báo cáo clash (HTML, XML hoặc cả hai), vẽ chú thích trên ảnh issue và đồng bộ family BimGo Comment Marker vào Revit. Giữ lệnh Comment markers trên ribbon và các tùy chọn hiện có.

## Kiến trúc và tương thích phiên bản

Giữ một viewer Windows x64 dùng C#, .NET 8 và OpenGL hiện có. Viewer đọc file và chạy clash độc lập. Add-in từng đời Revit chịu trách nhiệm trích xuất, khởi tạo phiên và định vị phần tử. Giao thức live và định dạng file là ranh giới giữa hai tiến trình.

| Đời Revit | Đích cho add-in | Trạng thái tại thời điểm viết |
|---|---|---|
| 2023, 2024 | .NET Framework 4.8, build riêng với RevitAPI/RevitAPIUI đúng năm | Cần bổ sung. Core hiện chỉ target net8.0, add-in cũ không thể tham chiếu trực tiếp assembly này. |
| 2025, 2026 | net8.0-windows, API đúng năm | Đã có cấu hình R25/R26. |
| 2027 | net10.0-windows, API 2027 | Đã có cấu hình R27. |

Yêu cầu tương thích bao gồm cấu hình solution, đóng gói installer/bundle, manifest đúng năm và tìm cùng một viewer. Phần dữ liệu mà add-in sử dụng phải tương thích với runtime tương ứng. Cách chia hoặc multi-target phần dùng chung là quyết định ở bước thiết kế kỹ thuật. Kiểm tra riêng ID phần tử, transform của link và API khác nhau giữa các năm. Dùng file RVT mẫu đúng đời để nghiệm thu từng host.

## Cấu trúc dự án

- `src/BimGo.Revit` — ribbon, commands, extraction và bridge cho phiên live.
- `src/BimGo.Core/Clashes`, `Format`, `Live` — thuật toán, trạng thái, file và giao thức.
- `src/BimGo.App/Game/Clashes` và các module Rooms, Comments, Bcf, Rendering — trải nghiệm review.
- `tests/BimGo.Core.Tests` — MSTest.Sdk 4.4.1, chạy độc lập với Revit. `installer/` và `build/` chứa phần đóng gói. `docs/` chứa hướng dẫn người dùng.

## Lệnh và quy ước

Chạy từ thư mục gốc bằng PowerShell. Lệnh build từng năm cần SDK và Revit API tương ứng. R23 và R24 dưới đây là **cấu hình phải bổ sung**.

```powershell
dotnet build src/BimGo.App/BimGo.App.csproj -c Release
dotnet test tests/BimGo.Core.Tests/BimGo.Core.Tests.csproj -c Release
foreach ($year in 23..27) {
    dotnet build src/BimGo.sln -c "Release R$year"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: R$year" }
}
```

Theo kiểu C# của module đang sửa, dùng tên public dạng PascalCase, giữ nguyên giá trị chuỗi được ghi vào file. Ví dụ từ `ClashModels.cs`:

```csharp
public const string REVIEWED = "reviewed";
public const string APPROVED = "approved";
public const string RESOLVED = "resolved";
```

## Chiến lược kiểm thử và tiêu chí nghiệm thu

1. **Bộ mẫu có đáp án.** Dùng host HVAC và link kết cấu có transform, một cặp xuyên nhau, một cặp chỉ vi phạm clearance, một cặp đủ khoảng hở. Các test trên cùng snapshot phải trả đúng các cặp dự kiến ở cả live và offline. Kiểm tra đơn vị, lọc loại trừ và phân biệt hai instance link có cùng ID phần tử nguồn.
2. **Máy chỉ có viewer.** Mở file do từng đời add-in xuất, tạo hoặc chạy lại test, dùng Find Room, xem đúng cặp trong section box, đo, tạo issue và lưu. Đóng rồi mở lại file phải khôi phục sets/tests, kết quả, trạng thái, comment, ảnh và viewpoint.
3. **Vòng sửa thực tế trên cả năm đời.** Cài bundle, mở đúng RVT, Go, đối chiếu vị trí clash trong Revit; sửa một phần tử host, refresh và chạy lại. Clash vừa sửa chuyển resolved, cặp còn lại giữ đúng kết quả và trạng thái thủ công. Hủy lần chạy giữa chừng không làm mất kết quả review trước đó.
4. **BCF.** Xuất issue của một clash có section box, nhập vào công cụ đọc BCF với mô hình tương ứng, đối chiếu vị trí và ảnh. Nhập lại cùng issue vào BimGo phải cập nhật đúng issue, không sinh bản sao.
5. **Tương thích dữ liệu.** Kiểm tra mở file .bimgo hiện có chứa dữ liệu review và dữ liệu của tính năng bị cắt. Sau khi lưu bản sao, dữ liệu review phải đọc lại được. Nếu không thể bảo toàn nội dung ngoài phạm vi, không âm thầm ghi đè bản gốc.
6. **Phân tầng kiểm thử.** MSTest bao phủ logic sets/tests, merge trạng thái, định danh và round-trip file/BCF. Kiểm thử trực tiếp trên từng Revit xác nhận load add-in, export và live refresh; build thành công riêng lẻ chưa đủ làm bằng chứng tương thích.

7. **Đối chiếu với bản gốc.** Xuất cùng bộ clash ra HTML, XML và cả hai, kiểm tra nội dung và các tùy chọn nhóm/ảnh tương ứng. Vẽ chú thích, lưu, mở lại issue và xác nhận còn ảnh gốc để vẽ lại. Trên từng đời Revit 2023–2027, kiểm tra nạp family marker tương thích, tạo và cập nhật đúng marker theo ID comment trong phiên live và qua lệnh đồng bộ từ file. Đồng bộ lại không tạo bản trùng.

## Ranh giới

- **Luôn giữ** khả năng review offline, định danh phần tử theo nguồn, dữ liệu review đã lưu và giới hạn đã công bố của thuật toán hiện tại. Hard depth là ước lượng; trường hợp một khối nằm hoàn toàn trong khối khác là giới hạn hiện hữu.
- **Chốt trước khi thay đổi** định dạng .bimgo, giao thức live hoặc làm mất khả năng đọc file cũ.
- **Ngoài phạm vi** chỉnh sửa hình học RVT trong viewer, máy chủ cộng tác nhiều người và hợp nhất chỉnh sửa đồng thời. Hai vai trò dùng cùng chức năng. Việc gộp issue từ nhiều bản snapshot qua các lần xuất khác nhau chưa phải tiêu chí nghiệm thu.

Tài liệu hành vi gốc: [Clash detection](../clash-detection.md), [Comments và BCF](../comments-and-bcf.md), [Live sessions](../live-sessions.md), [Review tools](../review-tools.md).
