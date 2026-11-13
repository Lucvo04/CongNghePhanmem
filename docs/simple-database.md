# Cơ sở dữ liệu và phân quyền — 13/11/2026

## Hai bảng cho tài khoản và quyền hệ thống

**Users.RoleId → Roles.Id** thay thế UserRoles. Mỗi tài khoản có tối đa một quyền hệ thống: Admin hoặc Student. Các luồng tạo tài khoản luôn gán quyền. RoleId cho phép null khi gỡ quyền bằng công cụ nội bộ; tài khoản đó không có quyền Admin/Student.

| Bảng | Vai trò |
|---|---|
| Users | Tài khoản, hồ sơ, trạng thái phê duyệt và bảo vệ đăng nhập |
| Roles | Hai quyền cố định; chỉ còn Id và Name |
| Clubs | Thông tin câu lạc bộ |
| Memberships | Các đợt tham gia, vai trò Member/Manager trong từng CLB |
| JoinRequests | Đơn xin gia nhập CLB đã tồn tại |
| ClubProposals | Đơn đề nghị thành lập CLB mới |
| Events | Hoạt động và lịch của CLB |
| EventRegistrations | Đăng ký tham gia hoạt động; 6 cột, lịch sử dùng AuditLogs |
| Attendances | Kết quả điểm danh; 6 cột, khóa chính là RegistrationId, có dấu thuộc danh sách đã chốt |
| Announcements | Nội dung thông báo, đối tượng, trạng thái và SourceKey chống trùng thông báo tự động; 10 cột |
| AnnouncementRecipients | Snapshot người nhận và ReadAt; 3 cột, khóa chính ghép AnnouncementId/UserId |
| AuditLogs | Người thao tác, thời gian, lý do, thay đổi dữ liệu |

Ngoài mười hai bảng nghiệp vụ, __EFMigrationsHistory ghi các migration đã chạy. Không xóa bảng này. PB14 thêm Attendances; PB15 bổ sung Events.AttendanceFinalizedAt và Attendances.IsInFinalRoster; PB16 thêm hai bảng thông báo. Xem [giải thích SQL chốt điểm danh](attendance-finalization.md) và [thông báo/người nhận](announcements.md). Hai bảng mới không thêm cột vào Users hoặc Roles.

PB17 thêm ReadAt vào AnnouncementRecipients: NULL là chưa đọc, có giá trị là thời điểm đọc đầu tiên. Không thêm IsRead, bảng mới hoặc trường tài khoản. Xem [hộp thông báo và SQL đánh dấu đã đọc](notification-inbox.md).

PB18 thêm SourceKey nullable vào Announcements, chỉ mục unique chỉ áp dụng khi có giá trị. Server tạo khóa theo hoạt động, phiên bản trước thay đổi và loại đổi lịch/hủy; không cho form gửi khóa này. Gửi lại cùng form không tạo thêm thông báo/người nhận. Danh sách được chụp trước hủy nên không mất khi đăng ký về 0. Xem [giao dịch và SQL thông báo tự động](event-change-notifications.md).

PB13 tra cứu lịch sử dùng lại EventRegistrations và AuditLogs, không thêm bảng/cột. Xem [giải thích luồng MVC và SQL lịch sử đăng ký](registration-history.md).

## Users còn 16 cột

| Cột | Công dụng |
|---|---|
| Id | Khóa chính để các bảng khác tham chiếu |
| Email, NormalizedEmail | Email hiển thị và email chuẩn hóa để tìm/kiểm tra trùng không phân biệt hoa thường |
| PasswordHash | Bản băm mật khẩu do Identity tạo, không phải mật khẩu rõ |
| RoleId | Khóa ngoại tới Roles |
| FullName, StudentCode | Họ tên và mã sinh viên |
| PhoneNumber, ClassName, Faculty | Số điện thoại, lớp, khoa |
| Status, CreatedAt | Trạng thái tài khoản và ngày đăng ký |
| AccessFailedCount, LockoutEnd | Đếm nhập sai và thời điểm hết tạm khóa |
| SecurityStamp | Thu hồi phiên khi đổi mật khẩu, khóa/mở khóa hoặc duyệt tài khoản |
| ConcurrencyStamp | Phát hiện hai thao tác sửa cùng tài khoản |

Đã bỏ 6 cột SQL:
- UserName, NormalizedUserName: đăng nhập bằng email; thuộc tính C# của Identity đọc/ghi Email và NormalizedEmail.
- EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled: đồ án chưa có xác minh email/điện thoại hoặc 2FA.
- LockoutEnabled: luôn bật bảo vệ khi nhập sai cho mọi tài khoản, không cần cột bật/tắt riêng.

Roles bỏ NormalizedName và ConcurrencyStamp vì đây là danh mục hai quyền cố định. CoreRoleStore chuẩn hóa tên khi tra cứu. NormalizedEmail và các trường bảo vệ đăng nhập vẫn cần để giữ tính năng hiện có.

## Quyền chủ nhiệm nằm ở Memberships

A có Roles.Name = Student. Trong Memberships, A là Manager của CLB Lập trình và Member của CLB Âm nhạc. A quản lý CLB Lập trình, không có quyền Admin hoặc quản lý CLB Âm nhạc.

Menu “CLB tôi quản lý” chỉ hiện khi có Membership Manager, Active, chưa kết thúc; tài khoản Student Active và CLB chưa lưu trữ. Server vẫn kiểm tra quyền ở từng thao tác.

Không gộp Memberships với Users: một sinh viên tham gia nhiều CLB và một CLB có nhiều sinh viên. Không gộp đơn và CLB: đơn chưa duyệt không được xuất hiện như CLB đang hoạt động. Events chứa nhiều hoạt động của một CLB. AuditLogs dùng chung; ClubProposals không lưu lặp người duyệt, ngày duyệt, lý do mà đọc từ lịch sử này.

RowVersion cần để phát hiện biểu mẫu cũ. Ngày tham gia/kết thúc/trạng thái cần cho lịch sử và các quy tắc hiện có. Các trường còn lại của các bảng nghiệp vụ đang phục vụ chức năng đã triển khai.

## Trạng thái

- Users.Status: 1 Active, 2 Disabled, 3 Pending, 4 Rejected.
- ClubProposals.Status: 1 Pending, 2 Approved, 3 Rejected, 4 Withdrawn.
- Memberships.Role: 1 Member, 2 Manager.
- Không suy diễn ý nghĩa cùng một con số giữa các bảng khác nhau.

## Truy vấn trình bày với giáo viên

```sql
SELECT u.FullName, u.StudentCode, u.Email, u.Status, r.Name AS SystemRole
FROM Users u LEFT JOIN Roles r ON r.Id = u.RoleId;

SELECT c.Name AS ClubName, u.FullName AS ManagerName
FROM Memberships m
JOIN Clubs c ON c.Id = m.ClubId
JOIN Users u ON u.Id = m.UserId
WHERE m.Role = 2 AND m.Status = 1 AND m.EndedAt IS NULL;

SELECT p.Id, p.Name, p.Status, u.FullName AS Applicant, p.ClubId
FROM ClubProposals p JOIN Users u ON u.Id = p.ApplicantId;
```

Không trình chiếu PasswordHash, SecurityStamp hoặc User Secrets. Xem thêm scripts/Inspect-Database.sql.

## Chuyển dữ liệu

Migration 20261109033911_StreamlineAccountsAndClubProposals thêm Users.RoleId, chép quyền từ UserRoles rồi mới xóa bảng nối. Không đổi PasswordHash hoặc SecurityStamp. Migration từ chối nếu có tài khoản nhiều quyền, username khác email, hoặc dữ liệu xác minh/2FA cần giữ; không âm thầm chọn một quyền.

Rollback tái tạo UserRoles và chép RoleId trở lại; bị chặn khi có đơn thành lập hoặc tài khoản Pending/Rejected để không mất dữ liệu mới. Luôn sao lưu trước nâng cấp, không sửa migration lịch sử.

Code cần đọc: Models/ApplicationUser.cs; Data/Configurations/ApplicationUserConfiguration.cs; Data/ApplicationDbContext.cs; Security/CoreUserStore.cs; Security/CoreRoleStore.cs; Security/ClubAccessService.cs. Identity vẫn xử lý băm mật khẩu, cookie và lockout.

Câu giải thích ngắn: “Nhóm em dùng Users và Roles cho quyền hệ thống; mỗi tài khoản có một RoleId. Chủ nhiệm phụ thuộc từng CLB nên nằm ở Memberships. Sinh viên tự đăng ký ở trạng thái chờ duyệt, Admin chấp nhận mới đăng nhập được. Mật khẩu vẫn do ASP.NET Core Identity băm và kiểm tra.”

PB12 thêm EventRegistrations, không sửa Users/Roles. Xem [đăng ký hoạt động và SQL](event-registrations.md).
