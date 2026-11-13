> Cập nhật 10/11/2026: PB12 đã bổ sung EventRegistrations, đăng ký/hủy tham gia, chặn vượt sức chứa và xử lý đăng ký khi rời/đình chỉ CLB. Xem [quy tắc và code mới](event-registrations.md). PB14 đã thêm bảng Attendances, tổng cộng 10 bảng nghiệp vụ; quyền hệ thống dùng Users.RoleId → Roles, không còn UserRoles. Các phần theo ngày bên dưới ghi lại phiên bản trước.

# PB10 — Quản lý hoạt động của câu lạc bộ

Triển khai ngày 05/11/2026. Đây là phần quản lý hoạt động trong Sprint 2. Cập nhật 13/11: đã tích hợp lịch PB11, đăng ký/giới hạn sức chứa PB12 và [thông báo tự động đổi lịch/hủy PB18](event-change-notifications.md). Các ghi chú theo ngày bên dưới mô tả phạm vi tại từng thời điểm.

## Cách sử dụng

1. Đăng nhập Admin hoặc chủ nhiệm. Mở **CLB tôi quản lý / Quản lý câu lạc bộ → Hoạt động** tại CLB cần xử lý.
2. Chọn **Tạo hoạt động**, nhập tên, nội dung, địa điểm, giờ bắt đầu/kết thúc, hạn đăng ký, sức chứa và đối tượng. Các thời gian nhập theo giờ Việt Nam UTC+7.
3. **Lưu bản nháp**. Bản nháp yêu cầu các trường kế hoạch hợp lệ để giảm lỗi khi công bố; nội dung mô tả có thể bổ sung sau. Chỉ người quản lý đúng CLB và Admin thấy bản nháp.
4. Xem lại chi tiết rồi **Công bố hoạt động**. Hạn đăng ký và giờ bắt đầu phải ở tương lai. Người dùng có thể mở đường dẫn chi tiết được chia sẻ. Lịch tổng hợp sẽ làm ở PB11.
5. Hoạt động đã công bố chỉ sửa trước giờ bắt đầu cũ, phải ghi lý do và không đổi đối tượng. Thời gian mới vẫn phải hợp lệ. Hạn đăng ký khi sửa có thể ở quá khứ để giữ trạng thái đã đóng đăng ký; khi công bố lần đầu thì phải còn hạn.
6. **Hủy hoạt động** cần lý do. Bản ghi và lịch sử vẫn được giữ; không thể công bố lại bản đã hủy. **Đánh dấu hoàn thành** chỉ dùng cho hoạt động đã công bố và đã kết thúc; không đồng nghĩa chốt điểm danh.

Hoạt động toàn trường đã công bố có thể xem khi chưa đăng nhập. Hoạt động nội bộ chỉ thành viên đang hoạt động, có tài khoản sinh viên hoạt động, hoặc người quản lý đúng CLB/Admin được xem. Hủy một bản nháp không làm lộ bản nháp đó. Người xem thông thường không nhận nhật ký hoặc thông tin người thao tác.

CLB tạm dừng: xem lịch sử, hủy hoặc hoàn thành hoạt động hợp lệ; không tạo/sửa/công bố. CLB đã lưu trữ: chỉ xem theo quyền quản lý. Trang danh sách quản lý tìm theo tên, lọc trạng thái và phân trang 10 mục.

## Code nằm ở đâu và vai trò MVC

| Thành phần | Tệp trong dự án Web | Vai trò |
|---|---|---|
| Model | `Models/ClubEvent.cs` | Các thuộc tính và trạng thái hoạt động |
| SQL mapping | `Data/Configurations/ClubEventConfiguration.cs` | Bảng Events, khóa ngoại, chỉ mục, ràng buộc SQL |
| ViewModel | `ViewModels/Events/EventViewModels.cs` | Chỉ nhận trường cho phép từ biểu mẫu |
| Controller | `Controllers/EventsController.cs` | Nhận GET/POST, lọc quyền, trả View hoặc chuyển trang |
| Service | `Services/EventService.cs` | Quy tắc trạng thái, thời gian, quyền, giao dịch và nhật ký |
| View | `Views/Events/Index.cshtml`, `Edit.cshtml`, `Details.cshtml` | Danh sách, biểu mẫu, chi tiết và thao tác |
| Kiểm thử | `tests/StudentClubManagement.IntegrationTests/EventTests.cs` | Luồng HTTP, SQL thật, phân quyền, cạnh tranh, validation |

Luồng: người dùng gửi form → Controller kiểm tra dữ liệu → Service kiểm tra quyền và quy tắc trong transaction → DbContext ghi Events và AuditLogs → Controller chuyển đến trang chi tiết. Form không nhận ClubId mới khi sửa, Status, PublishedAt hoặc quyền từ trình duyệt; những giá trị đó do máy chủ quyết định.

## Giải thích SQL khi bảo vệ đồ án

Chỉ thêm **một bảng nghiệp vụ `Events`**. Ba bảng đăng nhập/phân quyền `Users`, `Roles`, `UserRoles` không đổi.

- `Id`: khóa chính tự tăng. `ClubId`: khóa ngoại sang Clubs. Một CLB có nhiều hoạt động, mỗi hoạt động thuộc một CLB; không gộp lịch vào một cột trong Clubs.
- `Title`, `Description`, `Location`: tên, mô tả văn bản thuần và địa điểm; Razor mã hóa nội dung khi hiển thị.
- `StartsAt`, `EndsAt`, `RegistrationDeadline`: SQL `datetimeoffset`; ứng dụng chuẩn hóa UTC khi lưu và chuyển UTC+7 khi nhập/xem, không phụ thuộc múi giờ Windows của máy chủ.
- `Capacity`: số người tối đa, SQL bắt buộc lớn hơn 0. Chưa phải số người đã đăng ký.
- `Audience`: School = 1, Club = 2. `Status`: Draft = 1, Published = 2, Cancelled = 3, Completed = 4.
- `PublishedAt`: rỗng khi nháp; có giá trị sau công bố, được giữ khi hủy để phân biệt bản đã từng công bố với nháp bị hủy.
- `CreatedAt`: thời điểm tạo. `RowVersion`: phiên bản SQL tự sinh; gửi biểu mẫu cũ sẽ bị từ chối thay vì ghi đè dữ liệu mới.
- SQL CHECK bảo đảm thời gian bắt đầu < kết thúc, hạn đăng ký ≤ bắt đầu, sức chứa và enum hợp lệ. Khóa ngoại không xóa dây chuyền CLB có sự kiện.
- Chỉ mục `(ClubId, StartsAt)` phục vụ danh sách theo CLB và lịch. Nhật ký dùng lại `AuditLogs`, không thêm bảng lịch sử riêng.

Các lệnh thay đổi lấy khóa tài khoản người thực hiện, sau đó khóa CLB trong transaction; kiểm tra lại quyền sau khóa. Hai yêu cầu công bố với cùng RowVersion chỉ một yêu cầu thành công. Dữ liệu sự kiện và audit commit/rollback cùng nhau. Nhật ký lưu trước/sau của kế hoạch, hash nội dung mô tả, người thực hiện, thời gian và lý do; hash chứng minh nội dung thay đổi, không dùng để phục hồi toàn văn mô tả.

Migration: `20261105024917_AddClubEvents`. Up tạo Events, không sửa tài khoản, thành viên hay đơn gia nhập. Down bị chặn nếu Events có dữ liệu để tránh xóa lịch sử ngoài ý muốn.

## Tiêu chí nghiệm thu và phần còn phụ thuộc

- Tạo nháp, công bố, sửa trước giờ bắt đầu, hủy có lý do, hoàn thành sau giờ kết thúc; lưu audit và giữ dữ liệu đã kết thúc.
- Người quản lý CLB A không xem/sửa bản nháp CLB B kể cả đổi ID; quyền bị thu hồi có hiệu lực ở yêu cầu tiếp theo.
- Khách/người ngoài không thấy nội dung nội bộ; không lộ nhật ký của người quản lý. POST cần anti-forgery; nội dung nhập được mã hóa trên giao diện.
- Giờ hiển thị đúng UTC+7; thời gian/sức chứa/đối tượng sai bị từ chối. Biểu mẫu cũ không ghi đè kết quả mới.

**Cập nhật tích hợp:** PB12 đã kiểm tra Capacity không dưới số đăng ký, xử lý hủy/chặn rời với đăng ký nội bộ tương lai. PB18 đã lưu snapshot người còn đăng ký trước đổi lịch/hủy cùng giao dịch sự kiện, chống gửi trùng và giữ danh sách sau hủy. Kiểm thử tích hợp đã bao phủ các phụ thuộc này; vẫn cần nhóm review/nghiệm thu trước khi đánh dấu Done.

Cập nhật 06/11/2026: **PB11 — lịch theo ngày, lọc thời gian và CLB** đã được triển khai, dùng chung `EventService.VisibleTo` thông qua `CalendarFor`. Xem [hướng dẫn lịch hoạt động](activity-calendar.md). Bước tiếp theo là PB12 đăng ký và hủy tham gia.
