> Cập nhật 10/11/2026: PB12 đã bổ sung EventRegistrations, đăng ký/hủy tham gia, chặn vượt sức chứa và xử lý đăng ký khi rời/đình chỉ CLB. Xem [quy tắc và code mới](event-registrations.md). PB14 đã thêm bảng Attendances, tổng cộng 10 bảng nghiệp vụ; quyền hệ thống dùng Users.RoleId → Roles, không còn UserRoles. Các phần theo ngày bên dưới ghi lại phiên bản trước.

# PB09 — Danh sách và trạng thái thành viên

Triển khai ngày 04/11/2026. Phần danh sách, trạng thái, tự rời và lịch sử đã triển khai để nhóm nghiệm thu. Phụ thuộc xử lý đăng ký sự kiện tương lai chưa triển khai vì PB10/PB12 chưa có; chưa đánh dấu toàn bộ PB09 Done.

## Cách sử dụng

**Chủ nhiệm:** CLB tôi quản lý → Thành viên → tìm theo tên/mã SV hoặc lọc trạng thái → Chi tiết và lịch sử → Thay đổi trạng thái. Admin vào Quản lý câu lạc bộ → Thành viên, dùng layout quản trị.

**Sinh viên:** Tài khoản → CLB của tôi → Chi tiết và lịch sử. Có thể xem lịch sử của bản thân kể cả sau khi rời CLB; chọn bộ lọc Tất cả các đợt hoặc Đã rời CLB để thấy các đợt cũ. Sinh viên thường không xem được danh sách riêng của cả CLB hoặc hồ sơ thành viên khác.

Mỗi thay đổi cần lý do 1–500 ký tự. Sinh viên đọc được lý do trong lịch sử cá nhân. Mở trang xác nhận hoặc bấm Hủy không thay đổi dữ liệu; chỉ POST xác nhận mới lưu.

| Từ trạng thái | Hành động | Kết quả |
|---|---|---|
| Active (1) | Tạm đình chỉ | Suspended (2), EndedAt vẫn null |
| Suspended (2) | Khôi phục hoạt động | Active (1), giữ JoinedAt ban đầu |
| Active/Suspended | Kết thúc hoặc tự rời | Left (3), EndedAt = thời điểm xử lý |
| Left (3) | Khôi phục bản ghi cũ | Bị chặn; phải nộp và duyệt đơn mới, tạo đợt mới |

Đình chỉ tư cách thành viên chỉ ảnh hưởng CLB tương ứng. Không khóa tài khoản đăng nhập, thay mật khẩu/SecurityStamp hoặc sửa tư cách ở CLB khác. Người còn Suspended không nộp đơn trùng để né đình chỉ.

## Quyền và các điều kiện

- Chủ nhiệm Student Active có Membership Manager Active chưa kết thúc chỉ quản lý đúng CLB. Admin Active quản lý toàn hệ thống theo ClubAccessService.
- Chủ nhiệm hiện tại không được đình chỉ hoặc cho rời, kể cả Admin thực hiện hoặc tự rời. Admin phải chuyển giao cho thành viên Active đủ điều kiện trước.
- Sinh viên tự rời chỉ được tác động Membership của mình. Trạng thái Left được máy chủ quyết định tại action Leave; giả mạo NewStatus/Role/UserId/SelfLeave không nâng quyền hay khôi phục được.
- Khôi phục chỉ khi CLB Active và tài khoản thành viên còn là Student Active không có Admin. CLB Suspended vẫn cho đình chỉ/kết thúc; không khôi phục. CLB Archived chỉ đọc lịch sử.
- Form cũ bị từ chối 409 khi RowVersion thay đổi; quyền đọc lại sau khóa, nên người đã bị chuyển chủ nhiệm/khóa/thu hồi role không dùng được form cũ.
- Các truy vấn lịch sử luôn xác minh quyền trên Membership trước khi đọc AuditLogs. Người ngoài phạm vi nhận 404; trang đặt NoStore, POST có antiforgery, Razor mã hóa nội dung lý do.

## Giải thích MVC và SQL

| File trong src/StudentClubManagement.Web | Vai trò |
|---|---|
| Controllers/MembershipsController.cs | Index: danh sách quản lý; Mine: các đợt của mình; Details: lịch sử; ChangeStatus/Leave: xác nhận và POST |
| Services/MembershipService.cs | Kiểm tra quyền/điều kiện, khóa/giao dịch, thay đổi trạng thái và audit |
| ViewModels/Memberships/MembershipViewModels.cs | Dữ liệu danh sách, chi tiết, lịch sử và form; trường chỉ hiển thị không nhận từ client |
| Views/Memberships/Index.cshtml | Tìm kiếm, lọc, 10 đợt/trang, danh sách riêng của chủ nhiệm hoặc cá nhân |
| Views/Memberships/Details.cshtml | Thông tin tư cách và lịch sử trạng thái, 10 thay đổi/trang |
| Views/Memberships/ChangeStatus.cshtml | Form quản lý hoặc tự rời, có xác nhận/lý do |
| Models/Membership.cs | Active = 1, Suspended = 2, Left = 3 và nhãn tiếng Việt |

Không tạo bảng hoặc migration mới. Memberships đã có Status, JoinedAt, EndedAt, RowVersion và unique index chỉ một đợt mở cho mỗi UserId/ClubId. Dùng AuditLogs để lưu lịch sử trạng thái thay cho bảng membership_history riêng trong mô hình tham chiếu, phù hợp yêu cầu gọn database.

Mỗi log có EntityType = Membership, EntityId = mã đợt tham gia; Action là MembershipSuspended/Restored/Ended; ActorId, OccurredAt, Reason; BeforeData/AfterData chứa ClubId, Status, Role, JoinedAt, EndedAt. Trang hiện tại hiển thị **lịch sử trạng thái** từ thời điểm tính năng được áp dụng. Không suy diễn hoặc tạo lịch sử giả cho quá khứ; chuyển chủ nhiệm vẫn có audit ClubManagerTransferred riêng trong chức năng quản lý CLB, chưa hợp nhất vào timeline này.

Luồng lưu: đọc Membership trong phạm vi quyền → khóa các tài khoản theo thứ tự cố định → khóa CLB → kiểm tra lại quyền, phiên bản, trạng thái, vai trò → UPDATE Memberships + INSERT AuditLogs → commit. Lỗi ghi audit làm rollback trạng thái và EndedAt. Không DELETE Membership khi rời; liên kết JoinRequests.MembershipId vẫn đúng đợt đã được duyệt.

```sql
-- Danh sách trạng thái theo CLB, không cần đọc bảng mật khẩu.
SELECT c.Name AS ClubName, u.FullName, u.StudentCode,
       m.Role, m.Status, m.JoinedAt, m.EndedAt
FROM Memberships m
JOIN Clubs c ON c.Id = m.ClubId
JOIN Users u ON u.Id = m.UserId
ORDER BY c.Name, u.FullName, m.Id DESC;

-- Trạng thái trước/sau từ nhật ký (chỉ chạy để học trên dữ liệu được phép).
SELECT EntityId AS MembershipId, Action,
       JSON_VALUE(BeforeData, '$.Status') AS OldStatus,
       JSON_VALUE(AfterData, '$.Status') AS NewStatus, OccurredAt
FROM AuditLogs
WHERE EntityType = 'Membership'
ORDER BY OccurredAt DESC, Id DESC;
```

## Nghiệm thu và phần phụ thuộc còn lại

1. Tìm tên/mã SV, lọc hiện tại/tất cả/Active/Suspended/Left. Các đợt cũ được giữ, phân trang và bộ lọc không làm lộ CLB khác.
2. Đình chỉ → khôi phục → rời: mỗi lần đúng một log, trạng thái phù hợp, tài khoản vẫn đăng nhập được và tư cách CLB khác giữ nguyên.
3. Hai tab cùng sửa hoặc tự rời đồng thời với đình chỉ: chỉ một thao tác có hiệu lực. Chuyển chủ nhiệm đồng thời với đình chỉ người nhận không tạo chủ nhiệm Suspended.
4. Truy cập ID của CLB khác, người khác, form đã mất quyền hoặc thiếu CSRF: bị chặn.
5. Chủ nhiệm hiện tại không rời/đình chỉ trước chuyển giao; Left không mở lại bằng sửa trạng thái; sinh viên có thể gửi đơn mới sau khi rời.

Phần phụ thuộc cần hoàn tất cùng PB10/PB12, theo chính sách đã ghi trong phân tích ban đầu:

- Tự rời khi còn đăng ký nội bộ chưa diễn ra: chặn và yêu cầu hủy đăng ký trước; quản lý có thể xử lý kèm lý do.
- Đình chỉ: hủy các đăng ký nội bộ chưa bắt đầu trong cùng giao dịch, lưu lý do; không xóa lịch sử sự kiện đã bắt đầu hoặc thông báo đã nhận.
- Phải bổ sung các ca kiểm thử tương ứng trước khi coi PB09/TC20 hoàn tất. Hiện chưa có thực thể sự kiện/đăng ký, nên không tạo hàm giả báo đã xử lý.

## Kiểm tra kỹ thuật

- Full suite 215/215 đạt, gồm 19 ca MembershipTests mới. Kiểm tra HTTP/quyền, CSRF, dữ liệu form giả mạo, tìm kiếm/phân trang, bảo vệ chủ nhiệm, khôi phục có điều kiện, audit rollback, xung đột và chuyển chủ nhiệm đồng thời.
- Browser trên database QA riêng đã thử tìm theo mã SV, đình chỉ, khôi phục và sinh viên tự rời, giữ đủ ba lịch sử. Console không warning/error. Ảnh `.local/qa/members-history.png`, `members-left-history.png`.
- Không thay schema hay chạy migration lên database thật; toàn bộ dữ liệu thử đặt riêng, không sửa thành viên thật.
- Sau sửa warning trong test, chạy lại ca chuyển chủ nhiệm/đình chỉ đồng thời đạt; build Release cuối 0 warning/error, EF không có thay đổi model thiếu migration. Readiness trên database thật trả 200 Healthy; màn hình quản lý/cá nhân yêu cầu đăng nhập. Server QA, database QA và server smoke đã dọn đúng phạm vi do trợ lý tạo.
- Bằng chứng kiểm thử: `.local/test-results/members.trx`, `.local/members-tests.log`. Hướng dẫn này và kết quả kỹ thuật không thay việc nhóm review/nghiệm thu.
