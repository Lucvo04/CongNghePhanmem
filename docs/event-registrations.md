# PB12 — Đăng ký và hủy tham gia hoạt động

Triển khai ngày 10/11/2026. Giữ MVC, giao diện Bootstrap/VinhUni và cơ chế tài khoản hai bảng Users/Roles.

## Cách sử dụng

1. Admin hoặc chủ nhiệm tạo hoạt động tại **Quản lý CLB → Hoạt động → Tạo hoạt động**, kiểm tra thời gian/sức chứa rồi công bố.
2. Sinh viên đã được duyệt vào **Lịch hoạt động → chọn hoạt động → Đăng ký tham gia**.
3. Xem các đăng ký tại **Tài khoản → Hoạt động đã đăng ký**.
4. Muốn hủy, chọn **Hủy đăng ký tham gia** tại chi tiết hoạt động hoặc danh sách cá nhân. Chỗ được trả lại; đăng ký cũ vẫn hiện cùng thời gian và lý do hủy.
5. Admin/chủ nhiệm mở **Chi tiết hoạt động → Danh sách đăng ký** để xem họ tên, mã sinh viên và trạng thái. Không công khai danh sách này cho người tham gia khác.

Nếu chưa thấy hoạt động, cần tạo và công bố trước. Chức năng không tự thêm hoạt động mẫu vào database đồ án.

## Quy tắc nghiệp vụ

- Chỉ Student Active được đăng ký; chủ nhiệm cũng là Student và được đăng ký nếu đủ điều kiện. Admin không đăng ký dưới tài khoản quản trị.
- Hoạt động Published, CLB Active, thời gian hiện tại phải trước hạn đăng ký và giờ bắt đầu.
- School: sinh viên hoạt động có thể tham gia. Club: cần Membership Active chưa kết thúc tại chính CLB đó.
- Không nhận vượt Capacity. Hai người cùng đăng ký chỗ cuối chỉ một người thành công.
- Một người/một hoạt động có một dòng đăng ký. Đăng ký trùng bị chặn. Sau hủy có thể đăng ký lại nếu còn hạn/chỗ và còn đủ điều kiện.
- Sinh viên được tự hủy trước giờ bắt đầu, kể cả đã hết hạn đăng ký; từ giờ bắt đầu trở đi không được tự hủy.
- Form đăng ký mang phiên bản hoạt động. Nếu người quản lý vừa đổi lịch/nội dung, người gửi phải xem lại thông tin rồi đăng ký.
- Form hủy mang phiên bản đăng ký. Form cũ không được hủy một lần đăng ký lại mới hơn.
- Giảm sức chứa không được thấp hơn số đăng ký chưa hủy.
- Hủy hoạt động sẽ hủy toàn bộ đăng ký còn hiệu lực cùng một giao dịch, giữ lý do và lịch sử.
- Tự rời CLB bị chặn nếu còn đăng ký nội bộ chưa diễn ra; sinh viên phải hủy trước.
- Chủ nhiệm/Admin đình chỉ hoặc kết thúc tư cách thành viên sẽ hủy đăng ký nội bộ Published chưa bắt đầu. Không hủy đăng ký toàn trường hoặc lịch sử đã bắt đầu.
- Khôi phục thành viên không tự khôi phục đăng ký đã hủy.
- Trang cá nhân còn hiển thị thông tin tóm tắt các hoạt động đã đăng ký sau khi rời CLB. Điều này không mở quyền xem mô tả nội bộ; chỉ hiện liên kết chi tiết nếu còn quyền xem hiện tại.
- Trạng thái “Đã đăng ký” không phải xác nhận đã tham dự hoặc điểm danh.

## SQL chỉ thêm một bảng 6 cột

| Cột EventRegistrations | Ý nghĩa |
|---|---|
| Id | Khóa chính |
| EventId | Khóa ngoại tới Events |
| UserId | Khóa ngoại tới Users |
| RegisteredAt | Thời gian đăng ký gần nhất |
| CancelledAt | Null: chưa hủy; có giá trị: thời điểm hủy |
| RowVersion | SQL tự sinh để phát hiện form cũ |

Không thêm cột Status vì trạng thái được xác định từ CancelledAt. Không lưu lặp tên sinh viên, tên hoạt động, lý do hoặc người hủy. Thông tin hồ sơ lấy từ Users/Events, lý do và lịch sử đầy đủ lấy từ AuditLogs.

Unique index (EventId, UserId) chống tạo trùng. Index (UserId, RegisteredAt) phục vụ danh sách cá nhân. CHECK bảo đảm thời điểm hủy không trước thời điểm đăng ký. Khóa ngoại Restrict không cho xóa mất liên kết lịch sử.

Đăng ký lại cập nhật RegisteredAt/CancelledAt trên cùng dòng; các lần đăng ký/hủy trước đó vẫn có snapshot trong AuditLogs. PB13 đã bổ sung tìm kiếm/lọc và trang **Lịch sử đăng ký**; xem [hướng dẫn lịch sử cá nhân](registration-history.md).

Ví dụ truy vấn số người đang giữ chỗ:

```sql
SELECT e.Id, e.Title, e.Capacity,
       COUNT(r.Id) AS RegisteredCount
FROM Events e
LEFT JOIN EventRegistrations r
    ON r.EventId = e.Id AND r.CancelledAt IS NULL
GROUP BY e.Id, e.Title, e.Capacity;
```

Không đếm toàn bộ dòng đăng ký làm số chỗ đã dùng, vì có dòng đã hủy.

## Luồng MVC và vị trí code

| Thành phần | File trong src/StudentClubManagement.Web |
|---|---|
| Model | Models/EventRegistration.cs |
| SQL mapping | Data/Configurations/EventRegistrationConfiguration.cs |
| ViewModel | ViewModels/Events/RegistrationViewModels.cs |
| Controller | Controllers/EventRegistrationsController.cs |
| Nghiệp vụ | Services/EventRegistrationService.cs |
| Form đăng ký/hủy | Views/Events/Details.cshtml |
| Danh sách cá nhân/quản lý | Views/EventRegistrations/Index.cshtml |
| Kiểm tra sức chứa, hủy hoạt động | Services/EventService.cs |
| Rời/đình chỉ thành viên | Services/MembershipService.cs |

POST lấy UserId từ phiên đăng nhập, không từ form. Controller kiểm tra đầu vào và trả View/Redirect. Service giữ transaction, lấy khóa tài khoản rồi khóa CLB, kiểm tra lại điều kiện, đếm số chỗ, lưu đăng ký và audit rồi commit.

Dùng chung khóa CLB với sửa hoạt động/hủy/đình chỉ để chống tranh chấp giữa các nghiệp vụ. Đây là lựa chọn đơn giản phù hợp quy mô đồ án; các thao tác cùng CLB được xử lý lần lượt. Không dùng biến đếm trong bộ nhớ hoặc chỉ dựa vào nút disabled trên giao diện.

Các trường hợp lỗi ghi audit rollback cả thao tác gốc và các đăng ký liên quan. POST có antiforgery; trang cá nhân và quản lý no-store; người ngoài đổi ID không đọc/hủy đăng ký người khác.

## Kiểm thử và triển khai

- 34 ca PB12 mới; tổng 313/313 kiểm thử tích hợp đạt trên SQL Server riêng.
- Bao phủ trùng đăng ký, tranh chỗ, đăng ký đồng thời hủy/đình chỉ/giảm sức chứa, hết hạn/bắt đầu, quyền nội bộ, giả mạo UserId, form cũ, rollback audit lỗi, lý do 500 ký tự và quyền xem danh sách.
- Browser QA: sinh viên đăng ký → danh sách cá nhân → hủy → số chỗ trở lại 0; chủ nhiệm xem danh sách trạng thái và lý do. Dữ liệu giả nằm trong database riêng.
- Migration 20261110043425_AddEventRegistrations chỉ thêm bảng; Down chặn nếu còn bản ghi để giữ lịch sử.
- Trước migration thật có backup COPY_ONLY/CHECKSUM, RESTORE VERIFYONLY và đối chiếu fingerprint tám bảng cũ.
- PB13 đã bổ sung lịch sử cá nhân; PB14–PB15 đã có [điểm danh, chốt và kết quả cá nhân](attendance-finalization.md). Chưa có danh sách chờ, chứng nhận hoặc email/thông báo tự động; PB16–PB18 thông báo là các bước tiếp theo.

Sau cập nhật, dừng terminal đang chạy bằng Ctrl+C rồi chạy lại dotnet run trong thư mục src/StudentClubManagement.Web. Không mở thêm bản chạy nền ở cùng cổng.
