# PB17 — Hộp thông báo và đánh dấu đã đọc

Triển khai 11/11/2026, tiếp nối PB16. Sinh viên và chủ nhiệm xem thông báo đã gửi tới mình, theo dõi số chưa đọc và tự đánh dấu đã đọc. Đây là FR15/PB17; cập nhật 13/11: [PB18 thông báo tự động đổi lịch/hủy](event-change-notifications.md) đã dùng chung hộp nhận này.

## Thao tác trên website

1. Đăng nhập bằng tài khoản sinh viên đã được duyệt, chọn **Thông báo** trên menu hoặc **Tài khoản → Thông báo của tôi**.
2. Xem thông báo mới nhất trước. Nhãn xanh và viền xanh đánh dấu thông báo chưa đọc. Có thể tìm tiêu đề, chọn CLB, lọc **Tất cả / Chưa đọc / Đã đọc**.
3. Chọn **Xem thông báo** để đọc đầy đủ. Mở trang không tự đổi trạng thái; bấm **Đánh dấu đã đọc** khi muốn xác nhận. Nút có cả trên thẻ danh sách và trang chi tiết.
4. Sau đánh dấu, số chưa đọc giảm và hiện thời điểm đọc đầu tiên. Bấm lặp/gửi lại yêu cầu không thay thời điểm này.
5. Từ chi tiết, chọn **Quay lại danh sách** để giữ bộ lọc/trang trước đó. Nếu đánh dấu từ danh sách đang lọc Chưa đọc, thẻ vừa xử lý sẽ rời danh sách đó.

Mỗi trang có tối đa 10 thông báo. Trang vượt giới hạn được đưa về trang hợp lệ; khi trang cuối không còn mục thì tự lùi về trang còn dữ liệu. Số chưa đọc trên menu và nút đầu trang tính **toàn bộ hộp**, không chỉ kết quả lọc. Khi số lớn hơn 99, badge menu hiện 99+; nhãn truy cập và trang hộp vẫn giữ số chính xác.

Admin không có hộp nhận của sinh viên vì PB16 chỉ gửi tới tài khoản Student. Admin/chủ nhiệm có quyền quản lý xem **Thông báo → Chi tiết → Xem người nhận** để thấy tổng số đã đọc và thời gian đánh dấu của từng người. Chủ nhiệm cũng là Student nên có hộp riêng nếu nằm trong tập người nhận. Quyền quản lý không cho đánh dấu thay người khác.

Các số cập nhật sau POST, điều hướng hoặc tải lại trang; chưa dùng SignalR/polling cập nhật tức thời. Không có Đánh dấu tất cả, chuyển lại Chưa đọc, email hoặc thông báo đẩy trong bước này. “Đã đọc” ghi nhận thao tác xác nhận, không chứng minh người dùng đã hiểu toàn bộ nội dung.

## Quyền truy cập và các tình huống cần hiểu

| Tình huống | Kết quả |
|---|---|
| Có trong AnnouncementRecipients, tài khoản Student Active | Được xem và đánh dấu thông báo đã đăng/lưu trữ |
| Là chủ nhiệm nhưng không nằm trong tập người nhận | Được quản lý theo PB16; không được đánh dấu cho mình hoặc người khác |
| Đổi ID thông báo hoặc gửi thêm UserId/ReadAt giả | Server lấy ID tài khoản từ cookie và thời gian từ server; không thay dữ liệu người khác |
| Rời CLB, bị đình chỉ membership, hủy đăng ký sau khi đã nhận | Giữ thông báo, trạng thái đọc và quyền xem khi tài khoản còn Active |
| CLB/thông báo đã lưu trữ | Vẫn thấy trong hộp; thông báo chưa đọc vẫn được tính |
| Gia nhập CLB sau lúc thông báo đăng | Không tự nhận thông báo cũ |
| Thông báo nháp | Không hiện trong hộp, không tính chưa đọc, không được đánh dấu |
| Tài khoản Disabled/Pending/Rejected hoặc không còn role Student | Không được truy cập hộp/đánh dấu; lịch sử nhận không bị xóa |

Bộ lọc CLB chỉ lấy từ các thông báo chính người dùng đã nhận, kể cả CLB đã lưu trữ. Không đưa danh sách CLB riêng hoặc thông tin người nhận khác vào hộp cá nhân. Các trang dùng NoStore. Nút POST có antiforgery; GET chỉ đọc. ReturnUrl phải là URL nội bộ, tối đa 2.048 ký tự.

## SQL: chỉ thêm một cột

PB16 có hai cột trong AnnouncementRecipients. PB17 thêm **ReadAt**, bảng hiện có đúng ba cột:

| Cột | Ý nghĩa |
|---|---|
| AnnouncementId | Thông báo đã nhận |
| UserId | Người nhận; cùng AnnouncementId tạo khóa chính ghép |
| ReadAt, datetimeoffset NULL | NULL = chưa đánh dấu; có thời điểm = đã đọc |

Không thêm IsRead vì có thể suy ra từ ReadAt; không thêm bảng, Id riêng hoặc RowVersion. Thao tác chỉ chuyển NULL sang thời điểm đầu tiên, không có thao tác đặt lại NULL. Khóa ghép và UPDATE có điều kiện đủ cho trường hợp này.

Ví dụ diễn giải truy vấn đánh dấu (bản ứng dụng còn lọc tài khoản/trạng thái thông báo và giữ khóa tài khoản):

```sql
UPDATE AnnouncementRecipients
SET ReadAt = @ServerUtcNow
WHERE AnnouncementId = @AnnouncementId
  AND UserId = @CurrentUserId
  AND ReadAt IS NULL;
```

Gửi cùng yêu cầu lần thứ hai không cập nhật dòng nào vì ReadAt đã có giá trị. Kết quả trả về vẫn thành công khi đó là thông báo hợp lệ của người dùng; không tạo thêm dòng, không tăng số đã đọc, không đổi dấu thời gian.

Truy vấn đối chiếu cho quản trị SQL:

```sql
SELECT a.Id, a.Title, COUNT(r.UserId) AS Recipients,
       COUNT(r.ReadAt) AS ReadCount,
       COUNT(r.UserId) - COUNT(r.ReadAt) AS UnreadCount
FROM Announcements a
LEFT JOIN AnnouncementRecipients r ON r.AnnouncementId = a.Id
WHERE a.PublishedAt IS NOT NULL AND a.Status IN (2, 3)
GROUP BY a.Id, a.Title;
```

Truy vấn ứng dụng lọc quyền trước khi đếm/phân trang. Đánh dấu đã đọc là trạng thái cá nhân thông thường, không thêm audit quản trị cho mỗi lần xem/đánh dấu; cột ReadAt giữ thời gian cần thiết. Nhật ký PB16 vẫn ghi các thao tác soạn/đăng/lưu trữ.

Các dòng nhận tồn tại trước migration mặc định ReadAt = NULL vì phiên bản cũ chưa theo dõi việc đọc. Không tự suy đoán thông báo nào đã đọc.

## Vị trí code MVC

Đường dẫn tính từ thư mục gốc repository:

| File | Nội dung cần giải thích |
|---|---|
| src/StudentClubManagement.Web/Models/Announcement.cs | AnnouncementRecipient.ReadAt |
| src/StudentClubManagement.Web/Services/AnnouncementService.cs | ReceivedBy lọc đúng người nhận; UnreadCountAsync đếm; MarkReadAsync cập nhật lần đầu |
| src/StudentClubManagement.Web/Controllers/AnnouncementsController.cs | Mine lọc/phân trang; MarkRead xử lý POST; Details bổ sung trạng thái cá nhân |
| src/StudentClubManagement.Web/ViewModels/Announcements/AnnouncementModels.cs | InboxStatus, InboxItem, AnnouncementInbox, AnnouncementReceipt |
| src/StudentClubManagement.Web/Views/Announcements/Mine.cshtml | Giao diện hộp, bộ lọc, badge, nút đọc và phân trang |
| src/StudentClubManagement.Web/Views/Announcements/Details.cshtml | Nút xác nhận hoặc thời điểm đọc của chính mình |
| src/StudentClubManagement.Web/Views/Announcements/Recipients.cshtml | Người quản lý xem tổng số đã đọc và thời gian từng người |
| src/StudentClubManagement.Web/Views/Shared/_Layout.cshtml | Menu Thông báo và số chưa đọc của tài khoản Student |
| tests/StudentClubManagement.IntegrationTests/AnnouncementInboxTests.cs | 18 ca kiểm thử riêng cho PB17 |

`MarkReadAsync` mở transaction → giữ khóa Account(actor) → kiểm tra tập thông báo đã nhận → `ExecuteUpdateAsync` với ReadAt IS NULL → commit. Khóa này dùng chung với khóa/mở tài khoản. Không cần khóa CLB: lưu trữ thông báo không loại quyền đọc và nội dung đã phát không sửa. Không tin quyền quản lý để suy ra quyền nhận.

`ReceivedBy` được dùng chung cho danh sách, số chưa đọc, trạng thái cá nhân và cập nhật, tránh mỗi nơi áp dụng một quy tắc khác nhau. Model SQL không được bind trực tiếp từ POST. Tiêu đề/nội dung/tên CLB được Razor mã hóa; không dùng Html.Raw.

## Cập nhật và nghiệm thu

Migration **20261111011414_AddAnnouncementReadAt** chỉ thêm cột nullable. Down từ chối nếu còn bất kỳ ReadAt có giá trị để không làm mất lịch sử đọc.

Máy khác của nhóm, sau cấu hình SQL và sao lưu:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet build StudentClubManagement.sln --configuration Release
dotnet ef database update --project src/StudentClubManagement.Web --configuration Release --no-build
```

Kịch bản review: người quản lý đăng một thông báo → sinh viên thấy badge tăng → mở chi tiết vẫn chưa đọc → bấm Đánh dấu đã đọc → badge giảm → tải lại vẫn giữ thời điểm → quản lý thấy số đã đọc tăng một. Thử lọc theo CLB/trạng thái, rời CLB sau nhận, thông báo lưu trữ và hai tài khoản đoán ID của nhau.

18 ca mới bao phủ HTTP/CSRF, quyền tài khoản và người nhận, GET không ghi, lặp/đồng thời, giữ thời điểm, bộ đếm toàn hộp, lọc/phân trang, ReturnUrl ngoài bị chặn, XSS, nháp không lộ, snapshot sau rời/lưu trữ, badge 99+, tranh chấp đọc/lưu trữ, schema ba cột và chặn downgrade mất lịch sử. Đã sửa lỗi dịch truy vấn lọc CLB phát hiện trong lần kiểm thử đầu.

Toàn bộ **422/422 kiểm thử tích hợp đạt**, không bỏ qua; build/restore cuối không warning/error. EF xác nhận model khớp migration. Các ca downgrade cũ khôi phục migration mới nhất sau kiểm tra để không để schema kiểm thử dở dang. Bằng chứng: `.local/inbox-full-tests.log`, `tests/StudentClubManagement.IntegrationTests/TestResults/inbox-full.trx`, `.local/inbox-model-check.log`.

Đã sao lưu database thật bằng COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước migration; hash mọi cột cũ của cả 12 bảng khớp trước/sau. Chỉ bổ sung ReadAt; không tự đánh dấu đã đọc, không tạo dữ liệu QA hoặc đổi mật khẩu thật. Log cục bộ: `.local/inbox-backup.json`, `inbox-before.json`, `inbox-after.json`, `inbox-live-migration.log`.

Edge headless dùng database QA riêng: hộp 5 thông báo, 4 chưa đọc ban đầu; thử đọc chi tiết, đánh dấu, quay lại giữ bộ lọc, đọc bản lưu trữ, tìm kiếm và trạng thái rỗng. Đã kiểm tra hộp/chi tiết/danh sách người nhận tại 360/768/1440/1920 px, menu mobile hoạt động, không tràn ngang hoặc lỗi trình duyệt; xem ảnh desktop/mobile.

Đã đóng browser, dừng đúng server 5080/5081 và xóa đúng database InboxQa của lần kiểm thử. Không can thiệp tiến trình 5026. Kiểm tra database thật sau cập nhật: readiness/trang chủ/lịch 200; hộp và trang thông báo yêu cầu đăng nhập 302. Bằng chứng: `.local/inbox-live-smoke-checks.json`, `inbox-browser-results.json`, ảnh `.local/qa/inbox-desktop.png` và `inbox-mobile.png`. Lúc bàn giao database thật chưa có thông báo; cần Admin/chủ nhiệm đăng một thông báo theo PB16 để thử hộp nhận.

PB18 đã bổ sung [thông báo tự động đổi lịch/hủy](event-change-notifications.md), lưu thông báo và snapshot người đăng ký cùng giao dịch thay đổi sự kiện. Bước kế tiếp là **PB19 — Thống kê CLB và thành viên hiện tại**.
