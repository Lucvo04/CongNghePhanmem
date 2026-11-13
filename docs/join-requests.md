# PB07 — Sinh viên đăng ký gia nhập câu lạc bộ

Hoàn thiện ngày 03/11/2026. Đã triển khai và kiểm thử; chờ nhóm review và nhóm trưởng nghiệm thu. Không coi đây là kết quả Sprint Review của cả nhóm.

## Mục tiêu và phạm vi

Sinh viên gửi lý do muốn gia nhập CLB, xem các đơn của mình và rút đơn còn chờ xử lý. Gửi đơn chưa tạo thành viên. Cập nhật sau PB07: [PB08 xét duyệt đơn/tạo thành viên](join-request-review.md) đã triển khai, kết quả được hiển thị ngay trong Đơn của tôi.

## Chạy và nghiệm thu thủ công

Từ thư mục gốc dự án, chạy:

```powershell
dotnet run --project .\src\StudentClubManagement.Web --launch-profile http
```

Mở `http://localhost:5026/Clubs`. Nếu ứng dụng đang chạy bản cũ, Ctrl+C trong terminal đó trước rồi chạy lại. Máy này đã áp dụng migration; máy thành viên khác cần cấu hình database và chạy `dotnet ef database update --project src/StudentClubManagement.Web` trước khi chạy web.

1. Dùng tài khoản **Student** đang hoạt động. Nếu chưa có, đăng nhập Admin → cấp tài khoản sinh viên, sau đó đăng xuất Admin. Các hồ sơ chủ nhiệm DEMO không có mật khẩu nên không dùng để đăng nhập.
2. Chọn CLB đang hoạt động → Xem chi tiết → Đăng ký gia nhập. Khách chưa đăng nhập được chuyển đến đăng nhập rồi quay về form đúng CLB.
3. Bỏ trống lý do: phải hiện lỗi. Nhập lý do hợp lệ (tối đa 1.000 ký tự), gửi: chuyển đến Đơn của tôi, trạng thái Chờ xử lý.
4. Trở lại cùng CLB: không được gửi thêm khi có đơn chờ. Không có bản ghi Membership mới chỉ vì đã gửi đơn.
5. Chọn Rút đơn: màn hình xác nhận chưa đổi dữ liệu; Giữ đơn quay về danh sách. Xác nhận rút: trạng thái Đã rút, giữ nội dung/ngày gửi/ngày rút.
6. Có thể nộp lại nếu còn đủ điều kiện: tạo đơn mới, vẫn thấy lịch sử đơn cũ. Tài khoản khác không xem/rút được đơn này.

Admin quản trị hệ thống không phải tài khoản xin gia nhập. Tài khoản đồng thời có Admin và Student cũng không được dùng luồng này.

## Quy tắc nghiệp vụ

- Người gửi phải là Student đang hoạt động, không có role Admin; danh tính lấy từ cookie đã xác thực, không lấy UserId do form gửi.
- CLB phải Active. Suspended không nhận đơn; Archived không hiển thị trang đăng ký.
- Thành viên có đợt tham gia chưa kết thúc (kể cả Suspended) không được nộp thêm. Thành viên đã Left có thể nộp lại.
- Mỗi sinh viên chỉ có một đơn Pending ở cùng CLB; có thể gửi đến các CLB khác.
- Lý do được trim, bắt buộc và tối đa 1.000 ký tự; Razor mã hóa nội dung khi hiển thị.
- Chỉ chủ đơn được xem và rút, chỉ rút khi Pending. CLB tạm dừng/lưu trữ sau khi gửi không ngăn chủ đơn rút đơn của mình.
- Rút đơn không xóa lịch sử. RowVersion phát hiện thao tác trên phiên bản cũ; trả xung đột 409 thay vì ghi đè.
- Mỗi thao tác thay đổi và audit nằm trong cùng transaction. Khóa nghiệp vụ theo thứ tự tài khoản → CLB; unique index vẫn bảo vệ gửi trùng ở SQL Server.

## Code nằm ở đâu?

Các đường dẫn dưới đây tính từ `src/StudentClubManagement.Web/`:

| Lớp/file | Trách nhiệm |
|---|---|
| Models/JoinRequest.cs | Dữ liệu đơn và enum Pending = 1, Withdrawn = 2; PB08 thêm Approved = 3, Rejected = 4 |
| Data/Configurations/JoinRequestConfiguration.cs | Tên bảng, độ dài, FK, unique index, check constraint, RowVersion |
| Services/JoinRequestService.cs | Kiểm tra điều kiện, gửi/rút đơn, transaction và audit |
| Controllers/JoinRequestsController.cs | Nhận HTTP, xác thực quyền, lọc đơn theo người đăng nhập, trả View |
| ViewModels/JoinRequests/JoinRequestViewModels.cs | Các trường cho form/danh sách, giới hạn dữ liệu nhận từ client |
| Views/JoinRequests/Create.cshtml | Form gửi lý do |
| Views/JoinRequests/Index.cshtml | Đơn của tôi, phân trang 10 đơn, mới nhất trước |
| Views/JoinRequests/Withdraw.cshtml | Xác nhận rút đơn |
| Views/Clubs/Details.cshtml và Views/Shared/_Layout.cshtml | Nút đăng ký và mục Đơn của tôi |

Luồng MVC: trình duyệt → Controller → Service → DbContext/SQL Server → Controller → Razor View. POST dùng antiforgery; trang đơn đặt NoStore; một sinh viên đoán ID đơn người khác vẫn nhận 404.

## SQL ngắn gọn

`JoinRequests` có Id (PK), UserId (FK Users), ClubId (FK Clubs), Reason, Status, SubmittedAt, ClosedAt, RowVersion. Một User/Club có nhiều đơn trong lịch sử; index `(UserId, ClubId) WHERE Status = 1` chỉ cho một đơn đang chờ. FK Restrict tránh xóa tài khoản/CLB làm mất liên kết lịch sử.

```sql
-- Chỉ xem thông tin tổng hợp, không hiển thị lý do cá nhân.
SELECT c.Name AS ClubName,
       SUM(CASE WHEN j.Status = 1 THEN 1 ELSE 0 END) AS Pending,
       SUM(CASE WHEN j.Status = 2 THEN 1 ELSE 0 END) AS Withdrawn
FROM Clubs AS c
LEFT JOIN JoinRequests AS j ON j.ClubId = c.Id
GROUP BY c.Id, c.Name;
```

Pending có ClosedAt null; các trạng thái kết thúc có ClosedAt không trước SubmittedAt. Thời gian lưu UTC, giao diện đổi sang UTC+7. PB08 thêm Approved = 3 / Rejected = 4 và migration cập nhật check constraint, giữ nguyên ý nghĩa 1/2. Sinh viên thấy người xử lý, thời điểm và lý do từ chối; chỉ đơn Pending có nút rút.

## Bằng chứng kiểm thử và bước tiếp theo

- 174/174 integration tests đạt, gồm 24 ca PB07: phân quyền, CSRF, giả mạo người gửi, trạng thái CLB/tài khoản/thành viên, gửi/rút đồng thời, RowVersion, phân trang, nội dung HTML được mã hóa, constraint và rollback khi ghi audit lỗi.
- Bộ test và browser dùng database riêng. Đã thử trên browser: khách → đăng nhập → gửi đơn; báo lỗi thiếu lý do; rút; nộp lại và thấy cả hai đơn. Kiểm tra màn hình hẹp không tràn ngang, không có console warning/error.
- Migration `20261102122134_AddJoinRequests` đã áp dụng vào database đồ án sau BACKUP COPY_ONLY CHECKSUM và RESTORE VERIFYONLY. Fingerprint toàn bộ sáu bảng cũ giữ nguyên; không thêm đơn thử vào database đồ án.
- Kết quả: `.local/test-results/join-requests.trx`, `.local/join-model-check.log`, `.local/join-live-migration.log`, `.local/join-backup.json`, `.local/join-before.txt`, `.local/join-after.txt`, ảnh `.local/qa/join-requests-desktop.png` và `join-requests-mobile.png`.
- PB08 đã bổ sung chủ nhiệm xem đơn thuộc CLB mình quản lý, chấp nhận/từ chối, tạo Membership khi chấp nhận trong cùng transaction và xử lý xung đột với thao tác rút đơn. Xem hướng dẫn riêng về PB08 để nghiệm thu phần này.
