# Kế hoạch triển khai BimGo Clash Review

**Bản để duyệt · 15/11/2026.** [Đặc tả và tiêu chí nghiệm thu](../specs/clash-review.md). Mục tiêu là giữ một viewer dùng độc lập với file từ năm đời Revit, cùng add-in đúng năm cho export, live review và marker. HTML/XML, markup và BCF thuộc phạm vi phát hành.

## Quyết định thiết kế và thứ tự

Giữ viewer .NET 8 và thuật toán clash hiện có. Dùng định dạng file và giao thức live hiện tại làm hợp đồng giữa viewer và add-in. Cần chứng minh Revit 2023 có thể xuất, trao đổi dữ liệu và nạp family trước khi nhân cấu hình sang 2024. R25–R27 là đường đối chứng trong suốt quá trình.

Mỗi dòng dưới đây là một phiên làm việc. **S** khoảng 1–2 file, **M** khoảng 3–5 file. Cột cuối chỉ vị trí bắt đầu. Các dòng yêu cầu lặp theo nhóm hoặc theo năm là những phiên riêng. Nếu spike cho thấy cần thay schema, thêm dependency hoặc port quá năm file cùng lúc, ghi rõ lỗi, các caller và chia tiếp trước khi sửa. Chỉ đánh dấu xong sau khi đạt phép kiểm tra ở cùng dòng.

## Các việc theo thứ tự phụ thuộc

| ID | Việc và phụ thuộc | Đầu ra và phép kiểm tra chấp nhận | Phạm vi dự kiến |
|---|---|---|---|
| 01 | Lập mẫu đối chứng. Bắt đầu tại đây. | Chuẩn bị host HVAC và link có hai instance, một phòng/grid, ba cặp đã biết (hard, chỉ clearance, sạch). Ghi ID nguồn/instance, vị trí, dung sai, giá trị đo và đáp án từ bản gốc trên host đang hỗ trợ. Lưu một snapshot cũ có issue và dữ liệu ngoài phạm vi. Dùng bản sao RVT riêng cho mỗi năm, bắt đầu từ mẫu đọc được ở 2023. | S, bộ mẫu QA và bảng đáp án |
| 02 | Kiểm chứng biên .NET Framework 4.8, sau 01. | Lần theo Go/Export từ add-in tới Core; liệt kê API, thư viện và kiểu dữ liệu chặn net48. Thử target bổ sung cho phần dùng chung nhỏ nhất. Chọn multi-target nếu hợp lý, hoặc tách phần hợp đồng/serialization cần cho host. Bằng chứng là payload mẫu do net48 ghi được reader .NET 8 đọc đúng ID và tọa độ, cùng kết quả build target hiện hành. | M, BimGo.Revit.csproj, BimGo.Core.csproj, Format, Live |
| 03 | Kiểm chứng RFA sớm, song song 02 sau 01. | Thử nạp BimGo_Comment_Marker.rfa trong bản sao RVT 2023. Ghi kết quả tạo instance và đọc GUID tham số. Nếu family quá mới, lập family tương đương bằng Revit 2023 và đối chiếu tham số/điểm đặt trước khi đóng gói. | S, Resources/Families, Bridge/MarkerSync.cs |
| 04 | Tạo đường build R23, sau 02. | Thêm cấu hình, reference Revit API 2023 và áp dụng phương án chia sẻ đã thử. Xử lý incompatibility theo từng nhóm caller (ID, extraction, live). Mỗi lần chỉ port một nhóm và build lại cả target cũ lẫn mới. Hoàn tất khi build Release R23 và load được add-in trong host 2023. | M mỗi nhóm, BimGo.sln, project, Extensions và caller đã xác định |
| 05 | Export R23 → review offline, sau 04. | Trích host/link, transform, metadata, phòng và grid. Đóng Revit, mở file trên viewer, tìm phòng, chạy hard/clearance và chọn đúng hai instance theo đáp án 01. So sánh với bản đối chứng trước khi sửa thuật toán. | M, Extraction/SceneExtractor, LinkResolver, Core/Format |
| 06 | Go → sửa → refresh, sau 05. | Trên 2023, mở live, tìm đúng cặp host/link, sửa một phần tử host trong Revit, F5 và chạy lại. Clash đã sửa chuyển resolved, trạng thái review của cặp khác còn nguyên. | M, SessionHost, LiveDispatcher, Core/Live, GameSession.Live |
| 07 | Đồng bộ marker đầu cuối, sau 03 và 06. | Tạo comment trong file offline, sync qua ribbon; tạo/cập nhật comment trong phiên live. Sync hai lần chỉ có một marker cho mỗi ID, đúng tọa độ và tham số. | M, MarkerSync, Resources/Families, Commands, GameSession.Markers |
| 08 | Hỗ trợ R24, sau 07. | Thêm build đúng API 2024; chạy lại 05–07 trên mẫu native R24. Xác nhận ID qua biên add-in/viewer không bị cắt ngắn, kể cả với giá trị lớn trong test serialization. | M, solution/project, tiện ích ID, manifest |
| 09 | Khóa hành vi clash và file, sau 01. | Dùng các test hiện có cho sets, nhóm, trạng thái và format. Bổ sung ca thiếu cho instance trùng ID nguồn, lọc loại trừ, hủy lần chạy, lưu/mở lại. Mở và lưu bản sao file cũ; kiểm tra dữ liệu review và hành vi bảo vệ file gốc theo spec. | M mỗi nhóm, ClashTests, ClashSetupTests, Core/Clashes hoặc Core/Format |
| 10 | Thu gọn các điểm vào viewer, sau 07 và 09. | Theo từng nhóm: menu và panel; phím tắt, gun wheel và F1; màn hình mở file và đối số kiosk. Kiểm tra từng đường vào: tour/media trình diễn, hiệu ứng, công cụ sửa và push hết xuất hiện hoặc kích hoạt. Clash, Find Room, Scan, Measure, bay, lưới, mức tầng, section box và comment vẫn thao tác được trên snapshot. | M mỗi nhóm, GameSession.Menu/Controls/GunWheel, InputCatalog, AppShell |
| 11 | Thu gọn add-in và bảo vệ giao thức, sau 08 và 10. | Rà ribbon, Options, session dispatcher; giữ Go, Export, chọn/định vị, refresh và Comment markers. Thử lệnh sửa/push từ client cũ trên model mẫu: bị từ chối, model không đổi. Kiểm tra lại đúng luồng ghi marker. | M, Application, Commands, Forms, LiveDispatcher |
| 12 | Đối chiếu issue và kết quả xuất sau tinh gọn, sau 10. | Tạo issue, gán người, đổi ưu tiên, trả lời, lưu viewpoint/ảnh. Vẽ và mở lại markup từ ảnh gốc. Chạy đủ HTML/XML/cả hai theo nhóm và tùy chọn ảnh. Xuất BCF 2.1, mở trong công cụ khác, nhập lại cùng GUID không nhân đôi; thử mẫu nhập 2.0/2.1/3.0. So với bản gốc, khoanh lỗi riêng cho mỗi đầu ra. | S–M mỗi đầu ra, GameSession.Markup, GameSession.Bcf, ClashReportTests, BcfTests |
| 13 | Đóng gói, sau 11–12. | Build bundle từng năm và một viewer. Kiểm tra script publish, mapping series/year và bộ cài. Trên máy nhiều phiên bản, mỗi Revit chỉ load add-in của đúng năm. Máy chỉ cài viewer mở được snapshot. | M, build/publish.ps1, installer/PackageContents.xml, installer/BimGo.iss |
| 14 | Nghiệm thu bản đóng gói, sau 13. | Mỗi năm R23–R27 có một dòng kết quả gồm load, Go, export, review offline, refresh sau sửa, marker live/file và sync lặp. Lưu mẫu, phiên bản host, lỗi và bằng chứng. Chỉ xác nhận năm đã chạy đủ trên host thực. | S mỗi năm, ma trận QA và bộ mẫu |
| 15 | Chốt tài liệu người dùng, sau 14. | Cập nhật README, mục lục và các trang cài đặt/review theo UI đã nghiệm thu. Đi theo hướng dẫn từ RVT tới issue offline và báo cáo, kiểm tra không còn hướng người dùng tới lệnh đã bỏ. | M mỗi nhóm trang, README.md, docs/ |

## Các mốc kiểm tra

- **01–03:** Có đáp án đối chứng, bằng chứng net48 trao đổi được dữ liệu, và kết quả thử family. Nếu thiếu host hoặc SDK, ghi chính xác năm và phép thử đang chờ.
- **04–05:** R23 export được file mà viewer độc lập chạy đúng cả hai loại test.
- **06–08:** Hoàn tất vòng sửa và marker trên R23 trước khi xác nhận cùng luồng trên R24.
- **09–11:** File cũ qua kiểm tra; mọi điểm vào đã rà; các lệnh sửa ngoài phạm vi không còn tác động tới RVT.
- **12–13:** Issue, BCF, markup, HTML/XML đạt đối chứng trên viewer tinh gọn; installer tạo đúng bộ file.
- **14–15:** Năm dòng nghiệm thu có bằng chứng, hướng dẫn khớp bản phát hành.

## Cách thực hiện và ghi kết quả

Từ thư mục gốc, dùng `dotnet test tests/BimGo.Core.Tests/BimGo.Core.Tests.csproj -c Release` cho logic độc lập; `dotnet build src/BimGo.sln -c "Release R23"` sau khi thêm cấu hình. Lặp theo ma trận build trong spec. Ghi riêng kết quả build, kiểm tra trên Revit và kiểm tra offline trên máy chỉ có viewer.

Sau khi có bộ mẫu, 02, 03 và 09 có thể làm độc lập. Sau 10, kiểm tra đầu ra viewer (12) có thể chạy cùng đợt rà add-in (11). Những thay đổi đến định dạng file, định danh phần tử hoặc giao thức live cần thống nhất trước để kết quả các nhánh vẫn so sánh được.

Điều kiện cần chuẩn bị cho triển khai: host và API đúng năm, bộ mẫu được phép dùng, môi trường viewer độc lập và công cụ thứ hai đọc BCF. Khả năng nạp RFA 2023, chi phí chia sẻ Core với net48 và độ tương thích của file cũ là các điểm chưa kiểm chứng. Nếu kiểm tra phát hiện mất dữ liệu hoặc sai vị trí, giữ bản đối chứng và xử lý tại bước gây sai trước khi mở rộng sang năm tiếp theo.
