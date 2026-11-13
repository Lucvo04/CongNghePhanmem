> Cập nhật 10/11/2026: PB12 đã bổ sung EventRegistrations, đăng ký/hủy tham gia, chặn vượt sức chứa và xử lý đăng ký khi rời/đình chỉ CLB. Xem [quy tắc và code mới](event-registrations.md). PB14 đã thêm bảng Attendances, tổng cộng 10 bảng nghiệp vụ; quyền hệ thống dùng Users.RoleId → Roles, không còn UserRoles. Các phần theo ngày bên dưới ghi lại phiên bản trước.

# PB11 — Xem và lọc lịch hoạt động

Triển khai ngày 06/11/2026. Trang `/Events/Calendar` dùng danh sách nhóm theo ngày; không cần thư viện lịch hoặc thêm bảng SQL.

## Cách sử dụng

1. Khởi động lại website sau khi cập nhật code. Chọn **Lịch hoạt động** trên menu chính; nếu menu đang thu gọn, mở nút ba gạch. Có thể mở lịch của một CLB từ trang chi tiết CLB hoặc từ màn hình quản lý hoạt động.
2. Chọn **Từ ngày**, **Đến ngày** và bấm **Xem lịch**. Mặc định từ hôm nay theo giờ Việt Nam đến ngày cách đó 30 ngày, tính cả hai đầu. Có thể chọn một ngày duy nhất hoặc ngày trong quá khứ; mỗi lần xem tối đa 366 ngày.
3. Kết hợp bộ lọc **Câu lạc bộ**, **Trạng thái** và **Tên hoạt động**. Nút **Đặt lại bộ lọc** trở về khoảng ngày mặc định, tất cả CLB/trạng thái và bỏ từ khóa.
4. Mỗi trang có tối đa 10 hoạt động, sắp xếp theo giờ bắt đầu rồi mã hoạt động. Các mục trong trang nhóm theo ngày Việt Nam. Một ngày có nhiều hơn 10 mục có thể tiếp tục ở trang sau.
5. Bấm tên để xem chi tiết; **← Lịch hoạt động đã lọc** quay lại đúng ngày, CLB, trạng thái, từ khóa và trang trước đó. Đăng nhập từ lời nhắc trên lịch cũng giữ bộ lọc.

Nếu chưa có kết quả, kiểm tra khoảng ngày và bảo đảm hoạt động đã được công bố. Bản nháp chỉ nằm trong màn hình quản lý. Dữ liệu minh họa trong ảnh kiểm thử nằm ở database riêng, không được thêm tự động vào dữ liệu đồ án.

## Quy tắc hiển thị

| Người xem | Nội dung trên lịch |
|---|---|
| Khách chưa đăng nhập | School đã công bố, bao gồm đã hủy/hoàn thành |
| Sinh viên hoạt động | School và Club mà mình còn là thành viên Active |
| Chủ nhiệm | Quyền sinh viên và hoạt động đã công bố của CLB mình quản lý |
| Admin hoạt động | Hoạt động đã công bố của các CLB chưa lưu trữ |

Lịch không hiển thị nháp, nháp bị hủy trước khi công bố hoặc hoạt động của CLB đã lưu trữ, kể cả Admin. Người quản lý vẫn có thể xem những mục đó ở màn hình quản lý theo quyền. CLB tạm dừng vẫn có lịch đã công bố để người xem tra cứu thay đổi/hủy.

Hoạt động đã hủy có nhãn đỏ và dòng giải thích riêng; không bị mất khỏi lịch chỉ vì đổi trạng thái. Hoạt động Published còn hiển thị nhãn thời gian tính tại lúc tải trang: sắp diễn ra, đang diễn ra hoặc đã kết thúc. Nhãn thời gian không thay thế thao tác Complete và không phải kết quả điểm danh.

Lọc theo **ngày bắt đầu**, không theo toàn bộ khoảng diễn ra: hoạt động bắt đầu ngày 9 và kết thúc ngày 10 thuộc nhóm ngày 9, không xuất hiện khi chỉ chọn ngày 10. Giờ kết thúc luôn kèm ngày để các hoạt động qua đêm không gây nhầm lẫn. Ô chọn ngày dùng định dạng hiển thị của trình duyệt; dữ liệu gửi đi là ISO `yyyy-MM-dd`, nhóm ngày và giờ kết quả hiển thị dạng Việt Nam.

## Giải thích code và SQL khi bảo vệ

| Tệp | Vai trò |
|---|---|
| `src/StudentClubManagement.Web/Controllers/EventsController.cs` — Calendar | Nhận GET, kiểm tra bộ lọc, đổi ngày Việt Nam sang khoảng UTC, đếm/phân trang và trả View |
| `src/StudentClubManagement.Web/Services/EventService.cs` — CalendarFor/VisibleTo | Dùng chung quyền xem với trang chi tiết; lịch bổ sung điều kiện đã công bố/chưa lưu trữ |
| `src/StudentClubManagement.Web/ViewModels/Events/EventCalendarViewModel.cs` | Bộ lọc và các dòng dữ liệu cần hiển thị, không trả hồ sơ thành viên hay nhật ký |
| `src/StudentClubManagement.Web/Views/Events/Calendar.cshtml` | Biểu mẫu GET, nhóm ngày, trạng thái, trang rỗng và phân trang |
| `src/StudentClubManagement.Web/wwwroot/css/site.css` | Bố cục thời gian/nội dung, nhãn ngày, trạng thái hủy và quy tắc màn hình hẹp |
| `tests/StudentClubManagement.IntegrationTests/EventCalendarTests.cs` | Kiểm thử quyền xem, lọc, giờ, phân trang, đầu vào sai và đường dẫn quay lại |

Không có migration mới: đọc Events và Clubs hiện có; kiểm tra quyền nội bộ qua Memberships, Users và UserRoles. Chuỗi xử lý là **lọc quyền → lọc ngày/CLB/trạng thái/tên → Count → OrderBy/Skip/Take → nhóm ngày ở View**. Nếu đếm trước khi lọc quyền, người ngoài có thể suy ra số hoạt động nội bộ; vì vậy Count cũng dùng đúng truy vấn đã lọc quyền.

Ví dụ chọn ngày **02/01/2030** theo UTC+7: khoảng SQL là `StartsAt >= 2030-01-01 17:00:00Z` và `StartsAt < 2030-01-02 17:00:00Z`. Dùng mốc đầu ngày kế tiếp thay vì `23:59:59` để không bỏ sót phần giây. EF Core chuyển LINQ thành SQL có tham số; không nối từ khóa người dùng vào câu SQL. Chỉ mục `(ClubId, StartsAt)` đã có từ PB10, hữu ích khi chọn CLB và ngày.

GET không tạo/sửa hoạt động. Form lỗi trả HTTP 400 cùng hướng dẫn sửa, thay vì truy vấn lịch bằng bộ lọc sai. Trường hợp page quá lớn/nhỏ được đưa về trang hợp lệ. Link quay lại chỉ nhận URL nội bộ có độ dài giới hạn; không chuyển người dùng đến website ngoài. Trang dùng NoStore để tránh dùng lại lịch nội bộ sau khi thay đổi quyền; mỗi lần tải kiểm tra tư cách hiện hành trên máy chủ.

## Nghiệm thu

- Khách chỉ thấy School; Student Active thấy nội bộ CLB mình, chủ nhiệm CLB khác không thấy nội bộ trái quyền.
- Đình chỉ, rời CLB, khóa tài khoản hoặc bỏ quyền Student khiến nội bộ biến mất ở yêu cầu tiếp theo; URL chi tiết cũng từ chối.
- Ngày 00:00 được tính, mốc 00:00 ngày tiếp theo bị loại. Lịch nhóm đúng ngày Việt Nam, kể cả khi ngày UTC khác.
- Bộ lọc kết hợp và số lượng đúng; phân trang giữ bộ lọc, thứ tự ổn định khi nhiều hoạt động có cùng giờ; không lộ tổng số nội bộ.
- Ngày sai, khoảng đảo ngược/quá dài, enum lạ hoặc CLB không hợp lệ trả lỗi có biểu mẫu; không gây lỗi 500.
- Nháp không có trên lịch, trạng thái hủy rõ; chi tiết quay lại đúng lịch. Nội dung người dùng được Razor mã hóa.

23 ca lịch mới đã đạt; toàn bộ 256 kiểm thử tích hợp đạt. Build không cảnh báo/lỗi và EF xác nhận model khớp migration. Đã kiểm tra giao diện trên trình duyệt desktop với dữ liệu QA riêng; chưa kiểm tra viewport điện thoại trong lượt này.

Nhóm cần review/nghiệm thu trước khi chốt PB11. Ngoại lệ xem lịch sử cá nhân của người từng đăng ký nhưng đã rời CLB cần tích hợp khi có đăng ký/lịch sử PB12–PB13; hiện chưa có EventRegistrations nên không giả lập quyền lịch sử này. Bước tiếp theo là **PB12 — đăng ký/hủy tham gia**, đồng thời nối các kiểm tra sức chứa PB10 và đăng ký nội bộ tương lai PB09.
