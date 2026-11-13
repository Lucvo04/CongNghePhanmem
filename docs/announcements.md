# PB16 — Đăng thông báo theo đối tượng

Triển khai 10–11/11/2026, tiếp nối PB15. Mục tiêu: Admin/chủ nhiệm soạn và đăng đúng đối tượng, giữ danh sách người nhận tại thời điểm đăng. Đây là phần FR14/PB16 của Sprint 3; nhóm vẫn cần review/nghiệm thu. Không đánh dấu toàn bộ Sprint hoàn thành.

## Cách dùng

1. Khởi động lại Web sau khi cập nhật code và migration. Admin chọn **Quản lý thông báo** ở menu trái; chủ nhiệm chọn **CLB tôi quản lý → Thông báo**.
2. Chọn **Soạn thông báo**, nhập tiêu đề (1–180 ký tự), nội dung (1–4.000 ký tự) và đối tượng.
3. Chọn **Thành viên CLB đang hoạt động**, hoặc một hoạt động đã công bố/hoàn thành của chính CLB để gửi người đăng ký hoạt động đó.
4. **Lưu bản nháp**. Trang chi tiết hiển thị nội dung và số người nhận dự kiến; có thể **Sửa bản nháp**.
5. Kiểm tra rồi chọn **Đăng thông báo**. Nếu danh sách thay đổi từ khi mở trang, hệ thống yêu cầu kiểm tra lại. Nếu không có người nhận hợp lệ, giữ nháp.
6. Sau đăng, chọn **Xem người nhận** để đối chiếu danh sách. Muốn đính chính, **Soạn thông báo mới**. Muốn kết thúc theo dõi, nhập lý do và **Lưu trữ thông báo**; vẫn giữ nội dung/người nhận/lịch sử.

Danh sách thông báo có tìm tiêu đề, lọc trạng thái và phân trang 10 mục. Danh sách người nhận phân trang 10 người. Lịch sử chi tiết hiển thị 20 thao tác gần nhất; nhật ký SQL vẫn giữ đầy đủ.

Trang chi tiết hỗ trợ quyền đọc của người nhận qua `/Announcements/Details/{id}`. **PB17 đã bổ sung [hộp thông báo, số chưa đọc và đánh dấu đã đọc](notification-inbox.md)** tại menu Thông báo của sinh viên/chủ nhiệm. Không có bảng tin thông báo công khai và không gửi email. Thông báo tự động khi đổi lịch/hủy hoạt động thuộc PB18.

## Quy tắc người nhận và trạng thái

| Tình huống | Xử lý |
|---|---|
| Đối tượng là CLB | Tài khoản Student Active, membership Active và chưa kết thúc của đúng CLB; gồm chủ nhiệm nếu đủ điều kiện |
| Đối tượng là hoạt động | Tài khoản Student Active, đăng ký đúng hoạt động và CancelledAt IS NULL; có thể là sinh viên ngoài CLB đăng ký hoạt động toàn trường |
| Hoạt động nháp/đã hủy/CLB khác | Không được chọn; kiểm tra lại khi đăng, kể cả gửi giả EventId |
| CLB Active | Cho soạn, sửa nháp và đăng |
| CLB Suspended | Không soạn/sửa/đăng mới; vẫn xem lịch sử và lưu trữ thông báo đã đăng |
| CLB Archived | Quản lý chỉ đọc; người nhận vẫn xem nội dung đã nhận |
| Rời CLB, đình chỉ membership hoặc hủy đăng ký sau khi nhận | Không xóa dòng người nhận cũ; vẫn đọc khi tài khoản Student còn Active |
| Gia nhập/đăng ký sau khi đăng thông báo | Không tự thêm vào thông báo cũ |
| Khóa tài khoản | Không truy cập khi bị khóa; không xóa danh sách người nhận cũ |

Luồng trạng thái: `Draft (1) → Published (2) → Archived (3)`. Nháp không xuất hiện cho người nhận. Không sửa nội dung/đối tượng sau khi đăng, không mở lại bản lưu trữ, không xóa cứng. Lưu trữ cần lý do 1–500 ký tự và không thu hồi nội dung người nhận đã có.

Tài khoản Admin Active quản lý mọi CLB. Chủ nhiệm Active chỉ quản lý đúng CLB được phân công. Thành viên thường không có quyền soạn/đăng hoặc xem danh sách người nhận, kể cả đoán ID. Chuyển giao chủ nhiệm làm quyền của người cũ mất hiệu lực ở request tiếp theo.

## SQL: vì sao cần hai bảng?

Một thông báo có nhiều người nhận, một sinh viên nhận nhiều thông báo. Vì vậy cần bảng liên kết để giữ danh sách tại thời điểm gửi, thay vì suy ra từ thành viên hiện tại.

| Announcements — 10 cột sau PB18 | Công dụng |
|---|---|
| Id | Khóa chính của thông báo |
| ClubId | CLB sở hữu và phạm vi quản lý |
| EventId, nullable | NULL: thành viên CLB; có giá trị: người đăng ký hoạt động. Không lưu thêm Audience trùng ý nghĩa |
| Title, Content | Tiêu đề và nội dung văn bản |
| Status | Nháp, đã đăng hoặc đã lưu trữ |
| CreatedAt, PublishedAt | Thời điểm tạo, thời điểm đăng; lưu UTC, giao diện hiển thị UTC+7 |
| RowVersion | Phát hiện form cũ và ngăn hai thao tác ghi đè nhau |
| SourceKey, nullable | PB18 thêm dấu chống trùng cho thông báo tự động; nháp/thông báo soạn tay giữ NULL |

| AnnouncementRecipients — 3 cột sau PB17 | Công dụng |
|---|---|
| AnnouncementId | Khóa ngoại tới thông báo |
| UserId | Khóa ngoại tới tài khoản |
| ReadAt | NULL = chưa đọc; thời điểm đọc đầu tiên, bổ sung ở PB17 |

Khóa chính ghép `(AnnouncementId, UserId)` bảo đảm một người chỉ nhận một lần đối với mỗi thông báo. Không thêm Id riêng, không chép họ tên/email/mã SV vào bảng này. Các thông tin hiển thị lấy từ hồ sơ hiện tại; snapshot giữ **tập ID**, không giữ bản sao hồ sơ.

Không lưu RecipientCount vì đếm từ bảng liên kết; không thêm bảng nhật ký vì dùng `AuditLogs`. Người soạn, người đăng, người lưu trữ và thời gian thao tác nằm trong audit. Nội dung nháp được ghi hash để đối chiếu thay đổi, không phải bản sao toàn bộ văn bản có thể khôi phục.

SQL có FK Restrict, chỉ mục theo CLB/thời điểm, theo EventId/UserId, CHECK nội dung không trống và trạng thái/thời điểm đăng hợp lệ. Kiểm tra hoạt động thuộc đúng CLB và quyền quản lý nằm trong service; FK đơn lẻ không thay thế quy tắc đó.

Truy vấn chỉ đọc để trình bày với giáo viên (chạy bằng quyền quản trị SQL; truy vấn ứng dụng còn phải lọc quyền):

```sql
SELECT a.Id, a.Title, c.Name AS ClubName,
       CASE WHEN a.EventId IS NULL THEN N'Thành viên CLB'
            ELSE N'Người đăng ký hoạt động' END AS Audience,
       a.Status, a.PublishedAt, COUNT(r.UserId) AS RecipientCount
FROM Announcements a
JOIN Clubs c ON c.Id = a.ClubId
LEFT JOIN AnnouncementRecipients r ON r.AnnouncementId = a.Id
GROUP BY a.Id, a.Title, c.Name, a.EventId, a.Status, a.PublishedAt;
```

Ví dụ giải thích: “Khi đăng cho CLB có 20 người đủ điều kiện, nhóm em lưu 20 dòng liên kết. Sau đó một người rời CLB và hai người gia nhập, thông báo cũ vẫn có 20 người nhận; thông báo mới dùng danh sách mới.”

## Code MVC nằm ở đâu?

Các đường dẫn bên dưới tính từ thư mục gốc repository:

| File | Trách nhiệm |
|---|---|
| src/StudentClubManagement.Web/Models/Announcement.cs | Hai thực thể và enum trạng thái |
| src/StudentClubManagement.Web/Data/Configurations/AnnouncementConfiguration.cs | Ánh xạ SQL, khóa, CHECK, chỉ mục |
| src/StudentClubManagement.Web/Data/ApplicationDbContext.cs | DbSet Announcements và AnnouncementRecipients |
| src/StudentClubManagement.Web/Services/AnnouncementService.cs | Lọc quyền, chọn người nhận, xem trước, lưu nháp, đăng/lưu trữ và audit |
| src/StudentClubManagement.Web/Controllers/AnnouncementsController.cs | Các GET/POST, validation, phân trang, thông báo lỗi và trả View |
| src/StudentClubManagement.Web/ViewModels/Announcements/AnnouncementModels.cs | Dữ liệu form và dữ liệu hiển thị; không bind model SQL trực tiếp |
| src/StudentClubManagement.Web/Views/Announcements/ | Chọn CLB, danh sách, soạn nháp, chi tiết, người nhận |
| tests/StudentClubManagement.IntegrationTests/AnnouncementTests.cs | Kiểm thử nghiệp vụ, HTTP, SQL và tranh chấp đồng thời |

Luồng đăng trong `TransitionAsync`:

1. Mở transaction, giữ khóa tài khoản người thao tác rồi khóa CLB, theo quy ước các module hiện có.
2. Kiểm tra lại quyền, trạng thái CLB/hoạt động, RowVersion và trạng thái nháp.
3. Truy vấn một tập UserId hợp lệ. So SHA-256 của tập ID đã sắp xếp với lúc xem trước; thay người nhưng giữ nguyên số lượng vẫn bị phát hiện.
4. Tạo các dòng người nhận, chuyển Published, đặt PublishedAt bằng giờ server và ghi audit.
5. Commit cùng nhau. Lỗi audit hoặc lưu SQL thì rollback; không có trạng thái đã đăng với danh sách gửi dở dang.

Khóa CLB dùng chung với thay đổi membership, đăng ký và hủy hoạt động. Nếu thay đổi đó xảy ra trước, đăng thông báo phải kiểm tra lại; nếu đăng trước, snapshot đã có được giữ nguyên. Không khóa toàn bộ tài khoản người nhận; trạng thái đủ điều kiện được lấy tại câu truy vấn snapshot. Việc khóa tài khoản sau đó không xóa lịch sử nhận.

Hai lần bấm đăng cùng phiên bản chỉ một lần thành công. RowVersion chặn lần thứ hai; khóa chính ghép tiếp tục bảo vệ khỏi dòng trùng. Không tự thử lại bằng danh sách người nhận mới. `RecipientVersion` là kiểm tra thay đổi, không phải token cấp quyền.

POST có antiforgery; tất cả trang thông báo yêu cầu đăng nhập và NoStore. Razor mã hóa văn bản, không dùng Html.Raw. Người nhận chỉ thấy nội dung/trạng thái/CLB, không thấy danh sách người khác hoặc nhật ký quản trị. Lưu trữ có lý do nhưng lý do quản trị không xuất cho người nhận.

## Migration và nghiệm thu

Migration `20261110151453_AddAnnouncements` chỉ thêm hai bảng, không sửa Users/Roles hoặc dữ liệu mười bảng trước. `Down` từ chối nếu còn bất kỳ thông báo/người nhận để tránh mất lịch sử.

Máy khác trong nhóm, sau khi đã cấu hình SQL và sao lưu:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet build StudentClubManagement.sln --configuration Release
dotnet ef database update --project src/StudentClubManagement.Web --configuration Release --no-build
```

Nghiệm thu bằng hai tài khoản chủ nhiệm/sinh viên và một Admin: lưu nháp → kiểm tra không lộ → đăng đúng đối tượng → đối chiếu người nhận → thử sửa bản đã đăng → lưu trữ có lý do. Kiểm tra thêm người gia nhập sau không nhận bản cũ, người rời vẫn đọc bản đã nhận, người khác không xem được danh sách.

27 ca kiểm thử mới bao phủ: nháp/đăng/lưu trữ qua HTTP, trường giả mạo, quyền chéo và thu hồi quyền, hai loại đối tượng, thay tập người nhận cùng số lượng, rời/hủy sau nhận, bấm đăng đồng thời, tranh chấp với rời CLB/hủy hoạt động, rỗng, dữ liệu sai, CSRF, XSS, phân trang, ba tình huống audit rollback và bảo vệ SQL/migration.

Toàn bộ **404/404 kiểm thử tích hợp đạt**, không bỏ qua; build không warning/error, EF xác nhận model khớp migration. Đã cập nhật kiểm thử schema cũ để nhận diện đủ hai bảng mới. Log: `.local/announcements-full-final-tests.log`; kết quả TRX: `tests/StudentClubManagement.IntegrationTests/TestResults/announcements-full-final.trx`.

Database thật đã sao lưu COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước migration. So sánh SHA-256 mọi cột cũ của mười bảng đều khớp trước/sau; hai bảng mới ban đầu rỗng. Không tạo dữ liệu QA hoặc đổi mật khẩu thật. Bằng chứng cục bộ: `.local/announcements-backup.json`, `announcements-before.json`, `announcements-after.json`, `announcements-live-migration.log`.

Edge headless trên database QA riêng đã chạy lưu/sửa nháp, đăng cho CLB và hoạt động, xem người nhận, đọc bằng sinh viên, Admin lưu trữ và lọc danh sách. Form/chi tiết/người nhận không tràn ngang ở 360/768/1366 px; menu quản trị mobile hoạt động, không có lỗi console/pageerror. Ảnh desktop/mobile đã được xem để kiểm tra bố cục.

Đã đóng trình duyệt QA, dừng đúng server 5078/5079 và xóa đúng database AnnouncementQa của lần kiểm thử. Không can thiệp tiến trình ở 5026. Kiểm tra đọc database thật sau migration: readiness/trang chủ/lịch trả 200, các trang thông báo yêu cầu đăng nhập trả 302. Bằng chứng: `.local/announcements-live-smoke-checks.json`, `announcements-browser-results.json`, ảnh `.local/qa/announcements-admin.png` và `announcements-mobile.png`.

PB17 đã có [hộp thông báo cá nhân và đánh dấu đã đọc](notification-inbox.md). PB18 đã nối [thông báo tự động](event-change-notifications.md) vào giao dịch đổi lịch/hủy hoạt động. Các quy tắc chọn người nhận Student Active ở tài liệu này áp dụng cho thông báo soạn tay PB16; PB18 chụp mọi đăng ký còn hiệu lực, kể cả tài khoản đang bị khóa. Tài khoản đó chỉ đọc được sau khi trở lại Student Active. Thông báo tự động có thể có 0 người nhận, vẫn lưu để đối chiếu thay đổi.
