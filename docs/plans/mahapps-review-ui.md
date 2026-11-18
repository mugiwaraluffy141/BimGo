# Phương án chuyển giao diện review sang MahApps.Metro

Đề xuất dùng WPF và MahApps.Metro theo MEPTools cho giao diện nghiệp vụ. BimGo tiếp tục dùng viewport OpenGL để xem, chọn và đo trong model. Ước lượng dưới đây tính theo ngày công cho một người đã quen repo, gồm kiểm tra local. Thời gian chờ máy có đủ đời Revit và sửa lỗi phát hiện khi nghiệm thu được tính riêng.

| Phương án | Ước lượng sơ bộ | Phần chi phối thời gian |
|---|---|---|
| MahApps cho cửa sổ WPF của add-in | 1–2 ngày | Gắn theme và MetroWindow, xử lý tài nguyên, kiểm tra tải DLL trong host net48 và .NET hiện đại. |
| MahApps cho workspace review của desktop app, giữ OpenGL | 6–10 ngày | Ghép viewport Win32 với cửa sổ WPF, chuyển các panel nghiệp vụ, kiểm tra thao tác chuột/phím và lưu dữ liệu. Ước lượng gồm spike bố cục. |
| WebView2 cho toàn bộ UI nghiệp vụ của desktop app, giữ OpenGL | 10–18 ngày | Ngoài phần tách UI hiện vẽ bằng OpenGL còn có giao diện web, cầu nối lệnh/trạng thái, vòng đời và phân phối WebView2 Runtime. |

Chuyển cả renderer từ OpenGL sang WebGL là một dự án riêng và nằm ngoài ước lượng. Thư mục Game của viewer có khoảng 96 file C#, trong đó khoảng 47 file khớp dấu hiệu sử dụng UI vẽ tay. Các con số mô tả độ phân tán của UI, không phải số file bắt buộc viết lại. Hai cửa sổ XAML của add-in đã dùng WPF. MEPTools hiện tham chiếu MahApps.Metro 2.4.10, là lựa chọn đối chứng để kiểm tra khả năng build và nạp trên từng host BimGo trước khi chốt phiên bản.

## Phạm vi và tiêu chí

- Ba panel Clash Review, Inspect và Issues mở sẵn, thu gọn độc lập. Dùng màu, khoảng cách và trạng thái điều khiển theo theme MEPTools.
- Giữ toolbar/menu cần cho thao tác model. Cảnh 3D, selection outline, section box, marker và overlay gắn với không gian model vẫn do renderer vẽ. Con trỏ tự do, kéo chuột phải trong viewport để xoay. Nút trong panel không tác động lên model phía sau.
- Giữ luồng live Revit, file offline, Find Room, bốn chế độ đo, comment, markup, báo cáo HTML/XML, BCF và phím tắt. Giữ định dạng dữ liệu và giao thức với add-in.

## Thứ tự và mốc kiểm tra

| Lát triển khai | Phụ thuộc | Đầu ra kiểm tra được | Chi phí |
|---|---|---|---|
| 1. Theme WPF add-in | — | Options và các dialog dùng chung palette, control states và title bar. Add-in build và mở dialog trên một host net48 và một host .NET hiện đại. Kiểm tra bộ cài mang đúng DLL của theme. | 1–2 ngày |
| 2. Spike workspace viewer | — | Thử cửa sổ MahApps với vùng OpenGL native và một panel thật, đặt cạnh nhau để tránh chồng lên HWND viewport. Kiểm tra resize, DPI, Tab, mất focus, kéo chuột phải và picking tại mép viewport với snapshot mẫu. | 1–2 ngày |
| 3. Clash Review | 2 đạt | Chuyển danh sách, bộ lọc, chạy test, Previous/Next, Focus và Reports. Đối chiếu kết quả, góc nhìn và xuất HTML/XML với UI hiện có. | 2–3 ngày |
| 4. Inspect | 2 đạt | Panel phát lệnh Scan, Measure, Section Box và Find Room. Kiểm tra click panel không chọn model và tọa độ con trỏ đúng sau khi resize. | 1–2 ngày |
| 5. Issues | 2 đạt | Chuyển danh sách, form issue và lệnh BCF. Bọc hoặc giữ editor markup hiện có trong lần chuyển đầu nếu nó cần canvas riêng, rồi đối chiếu ảnh và dữ liệu khi lưu/mở file. | 1–2 ngày |
| 6. Tích hợp và nghiệm thu viewer | 3–5 đạt | Kiểm tra layout, keyboard focus, lưu file, luồng offline và một phiên Go/live. Gỡ công cụ vẽ UI cũ theo màn hình đã thay thế; build Release với tùy chọn deploy tắt trong khi kiểm thử. | 1 ngày |

**Điểm quyết định:** Nếu spike ở mục 2 cần đổi vòng lặp renderer hoặc lấy frame OpenGL sang bitmap mỗi khung hình, dừng mở rộng và ghi kết quả. Khi đó ưu tiên triển khai MahApps cho dialog WPF ở mục 1, sau đó cân nhắc cửa sổ nghiệp vụ riêng cho viewer. Những lát dùng chung input, viewport và command phải chốt chung trước khi làm song song. Các bước deploy .NET self-contained đang được xử lý riêng, nên spike không phụ thuộc vào việc đổi giao diện của bộ cài.
