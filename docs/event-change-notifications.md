# PB18 — Thông báo tự động khi đổi lịch hoặc hủy hoạt động

Triển khai 11–13/11/2026, tiếp nối PB16–PB17, đáp ứng FR15/BR17. Dùng lại thông báo và hộp nhận hiện có. Nhóm cần thực hiện Sprint Review theo kịch bản cuối tài liệu; không tự coi toàn bộ Sprint 3 đã nghiệm thu.

## Cách dùng

1. Admin/chủ nhiệm mở một hoạt động **đã công bố, chưa bắt đầu**, chọn **Sửa hoạt động**.
2. Đổi giờ bắt đầu, kết thúc, hạn đăng ký hoặc địa điểm; nhập lý do rồi **Lưu thay đổi**. Hệ thống tạo một thông báo gộp các thay đổi, hiển thị trước → sau theo giờ Việt Nam. Đăng ký vẫn được giữ.
3. Nếu cần hủy, mở **Hủy hoạt động**, nhập lý do rồi xác nhận. Hệ thống lưu thông báo và danh sách người nhận trước khi hủy các đăng ký còn hiệu lực.
4. Sinh viên chọn **Thông báo → Xem thông báo**. Có thể **Đánh dấu đã đọc** theo PB17. Nếu còn quyền xem hoạt động, có liên kết mở lịch/trạng thái hiện tại.
5. Quản lý xem **Thông báo tự động gần nhất** ở chi tiết hoạt động (tối đa 5 lần), hoặc danh sách quản lý thông báo. Mở chi tiết → **Xem người nhận** để kiểm tra danh sách và số đã đọc.

Không phải soạn nháp hoặc bấm đăng lần nữa. Không gửi email/push; số chưa đọc cập nhật khi thao tác hoặc tải lại trang. Thông báo đã phát không sửa nội dung, có thể lưu trữ theo quyền PB16. Cần đính chính thì tạo thông báo mới.

## Khi nào phát thông báo?

| Thao tác | Kết quả |
|---|---|
| Sửa một hoặc nhiều trường StartsAt, EndsAt, RegistrationDeadline, Location của hoạt động Published | Một thông báo chứa các thay đổi; loại khoảng trắng thừa ở hai đầu địa điểm trước khi so sánh |
| Hủy hoạt động Published | Một thông báo có lịch đã công bố và lý do; hủy các đăng ký trong cùng giao dịch |
| Chỉ sửa tên, mô tả hoặc sức chứa; lưu form không thay đổi | Không tạo thông báo tự động |
| Tạo/sửa/hủy nháp; công bố; đánh dấu hoàn thành | Không tạo thông báo PB18 |
| Form sai, hết quyền, sức chứa nhỏ hơn số đăng ký hoặc RowVersion cũ | Không cập nhật hoạt động và không tạo thông báo |
| Không còn ai đăng ký | Vẫn cập nhật và lưu một thông báo với 0 người nhận để quản lý tra cứu |

Các quy tắc PB10/PB12 vẫn áp dụng: sửa Published cần lý do, không đổi đối tượng, không sửa sau giờ bắt đầu; hủy cần lý do; không giảm sức chứa dưới số người còn đăng ký. Nhiều lần đổi lịch hợp lệ tạo các thông báo riêng cho từng lần, không sửa thông báo cũ.

## Người nhận được xác định thế nào?

Lấy **mọi UserId có EventRegistrations.EventId tương ứng và CancelledAt IS NULL ngay trước thay đổi**. Bản đăng ký đã hủy không được tính. Đây là trạng thái CONFIRMED trong phân tích; SQL hiện tại biểu diễn bằng CancelledAt NULL.

PB18 không lọc trạng thái tài khoản hiện tại khi chụp danh sách. Tài khoản bị khóa vẫn có dòng nhận nếu đăng ký còn hiệu lực; chỉ đọc được sau khi trở lại Student Active. Đây là điểm khác với thông báo soạn tay PB16, vốn chọn người nhận Student Active. Snapshot chỉ lưu ID, không sao chép họ tên/email.

Người đăng ký sau lần đổi lịch không tự nhận thông báo cũ. Rút đăng ký, rời CLB hoặc hủy hoạt động không xóa dòng nhận đã lưu. Người nhận chỉ thấy thông báo của mình; không thấy danh sách người khác hoặc audit quản lý. Thông báo không sao chép phần mô tả hoạt động. Rời CLB nội bộ vẫn giữ thông báo đã nhận, nhưng không được nhờ đó mở lại chi tiết hoạt động riêng tư: liên kết chỉ xuất hiện khi EventService.VisibleTo cho phép.

## SQL: chỉ thêm một cột, vẫn 12 bảng

Announcements hiện có **10 cột**, bổ sung `SourceKey varchar(64) NULL`. AnnouncementRecipients vẫn **3 cột**: AnnouncementId, UserId, ReadAt. Không thêm bảng, không đổi Users/Roles.

| Giá trị | Ý nghĩa |
|---|---|
| SourceKey NULL | Thông báo soạn tay PB16 |
| SourceKey có giá trị | Dấu nhận diện lần đổi lịch/hủy tự động |
| ReadAt NULL | Người nhận chưa bấm đánh dấu đã đọc |
| ReadAt có giá trị | Thời điểm đọc đầu tiên; các thông báo sau không thay giá trị này |

Server tính SHA256 của `event:{EventId}:{RowVersion trước thay đổi dạng hex}:{Schedule hoặc Cancel}`. Kết quả 64 ký tự được bảo vệ bằng unique filtered index chỉ áp dụng cho SourceKey khác NULL. CHECK yêu cầu có EventId và trạng thái Published/Archived khi có SourceKey. Khóa ghép người nhận ngăn lặp cùng người trong cùng thông báo. SourceKey là dấu chống trùng, không phải mật khẩu hay mã cấp quyền và không nhận từ form.

Nếu gửi lại form cũ sau khi lưu thành công, RowVersion không còn khớp: trả thông báo yêu cầu tải lại, không tạo thông báo thứ hai và không tính lại snapshot. Sau hủy, dù đăng ký còn hiệu lực đã về 0, người nhận của thông báo đầu vẫn được giữ. Unique index là lớp bảo vệ bổ sung. Gửi lại không được báo như một lần thay đổi mới thành công.

Truy vấn chỉ đọc để giải thích với giáo viên, chạy bằng quyền quản trị SQL:

```sql
SELECT a.Id, a.EventId, a.Title, a.PublishedAt,
       COUNT(r.UserId) AS Recipients,
       COUNT(r.ReadAt) AS ReadCount
FROM Announcements a
LEFT JOIN AnnouncementRecipients r ON r.AnnouncementId = a.Id
WHERE a.SourceKey IS NOT NULL
GROUP BY a.Id, a.EventId, a.Title, a.PublishedAt
ORDER BY a.PublishedAt DESC, a.Id DESC;
```

Số người nhận lấy từ AnnouncementRecipients, không đếm lại EventRegistrations sau khi hoạt động bị hủy. Không sửa SQL bằng tay để thử chức năng; dùng giao diện hoặc database kiểm thử riêng.

## Code MVC và giao dịch

Các đường dẫn dưới đây tính từ thư mục gốc dự án:

| File | Trách nhiệm |
|---|---|
| src/StudentClubManagement.Web/Services/EventService.cs | Kiểm tra quyền/trạng thái/form/RowVersion; gọi tạo thông báo trước khi sửa hoặc hủy; giữ giao dịch ngoài |
| src/StudentClubManagement.Web/Services/EventNoticeService.cs | So sánh lịch, chụp người nhận, tạo nội dung/SourceKey và audit EventNoticePublished |
| src/StudentClubManagement.Web/Models/Announcement.cs | Thêm SourceKey nullable |
| src/StudentClubManagement.Web/Data/Configurations/AnnouncementConfiguration.cs | Ánh xạ SQL, unique filtered index, CHECK |
| src/StudentClubManagement.Web/Controllers/EventsController.cs | Trả thông báo kết quả; nạp 5 thông báo tự động gần nhất cho quản lý |
| src/StudentClubManagement.Web/Controllers/AnnouncementsController.cs | Kiểm tra quyền mở liên kết hoạt động từ thông báo |
| src/StudentClubManagement.Web/Views/Events/Edit.cshtml và Details.cshtml | Hướng dẫn khi sửa/hủy và danh sách thông báo gần nhất |
| src/StudentClubManagement.Web/Views/Announcements/Details.cshtml | Nội dung tự động, trạng thái đọc và liên kết hoạt động được phép xem |
| tests/StudentClubManagement.IntegrationTests/EventNoticeTests.cs | 26 ca kiểm thử tích hợp riêng PB18 |

Trình tự: mở transaction → khóa tài khoản thao tác rồi CLB → kiểm tra quyền và form → lấy đăng ký còn hiệu lực → ghi thông báo/người nhận → sửa lịch hoặc hủy hoạt động và đăng ký → ghi audit → commit. Khóa CLB dùng chung với đăng ký/rút đăng ký nên danh sách có thời điểm xác định ngay trước thay đổi. Không giữ transaction chờ gửi email hoặc dịch vụ ngoài.

EventNoticeService gọi SaveChanges để lấy ID thông báo cho audit, nhưng **SaveChanges không commit giao dịch ngoài**. Nếu ghi sự kiện, người nhận hoặc audit lỗi thì toàn bộ rollback; không có tình trạng đổi lịch thành công nhưng mất thông báo. HTML Razor mã hóa văn bản đầu vào; không dùng Html.Raw cho nội dung.

Migration **20261111013356_AddEventNoticeSource** chỉ thêm SourceKey/index/CHECK. Down bị chặn nếu đã có thông báo tự động để không làm mất dấu chống trùng. Máy khác cần sao lưu, build và chạy `dotnet ef database update` như hướng dẫn README.

## Review và kiểm thử

Kịch bản trình bày: A và B đăng ký hoạt động → chủ nhiệm đổi lịch và nhập lý do → A/B thấy một thông báo trước/sau → A đánh dấu đã đọc → chủ nhiệm hủy → số đăng ký còn hiệu lực về 0 nhưng thông báo hủy vẫn có A/B → A thấy thông báo mới chưa đọc và thông báo cũ đã đọc. Gửi lại form đổi lịch/hủy cũ không tạo bản thứ ba. Kiểm tra thêm người ngoài không có quyền và hoạt động không có người đăng ký.

26 ca mới kiểm tra đúng snapshot (gồm tài khoản bị khóa), sửa nhiều trường, hủy, lặp/đồng thời, tranh chấp đăng ký/rút đơn, 0 người nhận, thao tác không cần thông báo, quyền và validation, XSS/nội dung dài, liên kết nội bộ, unique/CHECK/Down và lỗi ghi từng bước gây rollback. Toàn bộ **448/448 kiểm thử tích hợp đạt**, không bỏ qua. Log: `.local/event-notices-targeted-tests.log`, `.local/event-notices-regression-tests.log`; TRX: `tests/StudentClubManagement.IntegrationTests/TestResults/event-notices-regression.trx`.

Edge headless với database QA riêng đã kiểm tra quản lý đổi giờ/địa điểm → sinh viên nhận và đánh dấu → hủy → số đăng ký về 0, thông báo hủy vẫn có 2 người nhận và thông báo trước vẫn giữ trạng thái đọc. Hộp/chi tiết thông báo và chi tiết hoạt động vừa màn hình 360/768/1440/1920 px, không tràn ngang hoặc lỗi trình duyệt. Đã xem ảnh quản lý và hộp mobile. Bằng chứng: `.local/event-notices-browser-results.json`, ảnh `.local/qa/event-notices-manager.png`, `event-notices-student.png`, `event-notices-mobile.png`.

Ngày 13/11 đã sao lưu database thật bằng COPY_ONLY/CHECKSUM, xác nhận RESTORE VERIFYONLY hợp lệ rồi áp dụng migration. Hash tất cả cột cũ của cả 12 bảng khớp trước/sau; model khớp migration. Khi bàn giao database thật có 1 hoạt động và chưa có thông báo, cần tạo thay đổi hợp lệ trên hoạt động có người đăng ký để tự nghiệm thu. Log cục bộ: `.local/event-notices-backup.json`, `event-notices-before.json`, `event-notices-after.json`, `event-notices-live-migration.log`, `event-notices-model-check.log`.

Đã đóng browser, dừng server QA 5082, xóa đúng database EventNoticeQa của lần chạy và dừng server kiểm tra database thật 5083. Server người dùng 5026 không bị can thiệp. Kiểm tra bản mới: readiness/trang chủ/lịch trả 200, các trang cá nhân/thông báo yêu cầu đăng nhập trả 302. Bằng chứng: `.local/event-notices-live-smoke-checks.json`. Khởi động lại website đang chạy để dùng mã mới; không phải tạo lại tài khoản hoặc đổi mật khẩu.

Bước sau PB18 là **PB19 — Thống kê CLB và thành viên hiện tại**: phân biệt số sinh viên duy nhất với số tư cách thành viên, áp dụng đúng quyền và đối chiếu với truy vấn chi tiết.
