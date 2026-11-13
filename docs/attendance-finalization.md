# PB15 — Chốt danh sách và sửa điểm danh có truy vết

Triển khai ngày 10/11/2026. Tiếp nối PB14; không thêm bảng mới. Database vẫn có mười bảng nghiệp vụ.

## Cách sử dụng cho chủ nhiệm/Admin

1. Mở **Chi tiết hoạt động → Điểm danh** sau giờ kết thúc.
2. Kiểm tra khung **Kiểm tra trước khi chốt**. Số người và các kết quả ở đây tính toàn bộ danh sách hợp lệ, không phụ thuộc bộ lọc hoặc trang bên dưới.
3. Nếu còn **Chưa điểm danh**, ghi kết quả cho từng người, hoặc đánh dấu ô xác nhận chuyển toàn bộ số người chưa điểm danh sang **Vắng** khi chốt. Không có xác nhận thì hệ thống từ chối chốt.
4. Bấm **Chốt danh sách điểm danh**. Hệ thống ghi thời điểm chốt và cố định danh sách người tham gia.
5. Nếu phát hiện sai sót, sửa Có mặt/Vắng/Có phép và nhập **Lý do sửa sau chốt**. Mở **Lịch sử điểm danh** để xem trạng thái trước/sau, người, thời gian và lý do.

Ghi kết quả trên từng thẻ vẫn chỉ lưu người đó. Chốt luôn xử lý toàn bộ danh sách; không chỉ chốt trang hoặc kết quả đang lọc. Nếu dữ liệu đã thay đổi sau khi mở trang, cần xem lại rồi chốt lại. Danh sách không có ai vẫn có thể chốt; mẫu số là 0 và không được tính phần trăm bằng phép chia cho 0.

## Sinh viên xem ở đâu?

Vào **Tài khoản → Hoạt động đã đăng ký → Kết quả điểm danh**. Liên kết cũng có trong trang lịch sử đăng ký.

- Trước khi chốt: chỉ báo chưa chốt, chưa công bố kết quả tạm thời hoặc ghi chú nội bộ.
- Sau khi chốt: hiện kết quả hiện tại và các lần sửa sau chốt của chính sinh viên đó, gồm trạng thái trước/sau, thời gian, lý do; người thao tác hiển thị là “Người quản lý”.
- Sau khi rời CLB: còn xem kết quả cá nhân, không vì thế được xem toàn bộ nội dung hoạt động nội bộ.
- Hoạt động bị hủy: có nhãn rõ ràng; kết quả còn để tra cứu, không dùng xác nhận tham dự hợp lệ.
- Đăng ký không thuộc danh sách đã chốt: báo không thuộc danh sách, không suy ra Vắng.

Admin/chủ nhiệm đúng CLB được xem cả các lần ghi trước và sau chốt. Sinh viên không được đổi ID để đọc kết quả hoặc lý do của người khác. Trang có phân trang 10 lần thay đổi, NoStore và mã hóa nội dung HTML.

## Những quy tắc đã triển khai

| Nội dung | Quy tắc |
|---|---|
| Thời điểm chốt | Sau hoặc đúng EndsAt; hoạt động Published/Completed, CLB chưa Archived |
| Người chốt | Admin Active hoặc chủ nhiệm Active đúng CLB |
| Chưa điểm danh | Phải xử lý hết hoặc có xác nhận rõ ràng chuyển sang Vắng |
| Danh sách cố định | Chỉ người đăng ký hợp lệ trước khi bắt đầu, chưa hủy tại lúc chốt; lưu dấu trên từng kết quả |
| Sau chốt | Chỉ sửa Có mặt/Vắng/Có phép với lý do 1–500 ký tự; không đưa về Chưa điểm danh |
| Thêm người sau chốt | Bị chặn, kể cả tạo đăng ký mới bên ngoài luồng bình thường |
| Chốt lần nữa | Bị chặn; chỉ có một thời điểm chốt và một nhật ký chốt |
| Kết thúc hoạt động | Trạng thái Completed và trạng thái chốt độc lập; chốt không tự đổi trạng thái hoạt động |
| Hủy toàn bộ hoạt động | Giữ dấu danh sách/kết quả/nhật ký, dừng sửa; loại khỏi báo cáo tham dự hợp lệ |

CLB Suspended vẫn được xử lý kết quả hoạt động đã diễn ra theo PB14. Quyền được đọc lại từ database ở từng thao tác; chỉ ẩn nút trên giao diện không đủ để phân quyền.

## SQL chỉ thêm hai cột

| Bảng/cột | Ý nghĩa |
|---|---|
| Events.AttendanceFinalizedAt (nullable) | Null: chưa chốt; có giá trị: thời điểm chốt do máy chủ ghi |
| Attendances.IsInFinalRoster (bit) | True: đăng ký này thuộc danh sách cố định đã chốt |

Attendances từ 5 lên 6 cột; các cột cũ giữ nguyên. Không thêm bảng roster, không lưu lặp danh sách bằng JSON hay chuỗi phân cách. Người chốt lấy từ AuditLogs với Action = AttendanceFinalized; không cần thêm cột trùng trong Events.

Khi chốt, mọi người hợp lệ đều có một dòng Attendances và IsInFinalRoster = true. Người chưa ghi được tạo kết quả Vắng chỉ khi có xác nhận. Những dòng có kết quả sẵn giữ người/thời gian ghi gần nhất; chỉ cập nhật dấu danh sách. Thao tác sửa sau chốt không được thay dấu này.

SQL CHECK bảo đảm thời điểm chốt không trước EndsAt, hoạt động đã từng công bố, và người trong danh sách đã chốt không có Status = 0. Không có ràng buộc SQL xuyên nhiều bảng thay cho transaction; quy trình và quyền được kiểm tra thêm tại service.

Ví dụ kiểm tra mẫu số cố định và số Có mặt:

```sql
SELECT e.Id, e.Title, e.AttendanceFinalizedAt,
       COUNT(a.RegistrationId) AS FinalRosterCount,
       SUM(CASE WHEN a.Status = 1 THEN 1 ELSE 0 END) AS PresentCount
FROM Events e
LEFT JOIN EventRegistrations r ON r.EventId = e.Id
LEFT JOIN Attendances a ON a.RegistrationId = r.Id AND a.IsInFinalRoster = 1
WHERE e.AttendanceFinalizedAt IS NOT NULL
  AND e.Status <> 3 -- Không tính hoạt động đã hủy.
GROUP BY e.Id, e.Title, e.AttendanceFinalizedAt;
```

Không dùng số thành viên CLB hiện tại hoặc số người còn tư cách Active làm mẫu số. Sinh viên rời CLB sau hoạt động không làm thay đổi danh sách đã chốt. Đây là dữ liệu đầu vào cho PB20; dashboard tỷ lệ chưa được làm trong PB15.

## Vị trí code và luồng MVC

| File trong src/StudentClubManagement.Web | Phần chính |
|---|---|
| Models/ClubEvent.cs, Models/Attendance.cs | Hai thuộc tính bổ sung |
| Data/Configurations/ClubEventConfiguration.cs, AttendanceConfiguration.cs | Ràng buộc SQL |
| Services/AttendanceService.cs | SummaryAsync, FinalizeAsync, kiểm tra sửa sau chốt và audit |
| Controllers/AttendancesController.cs | POST Finalize; GET History theo quyền; nhận form sửa |
| ViewModels/Events/AttendanceViewModels.cs | Form chốt, tổng kết, lịch sử trước/sau |
| Views/Attendances/Index.cshtml | Khung tổng kết/chốt, xác nhận chuyển Vắng, lý do bắt buộc |
| Views/Attendances/History.cshtml | Kết quả và lịch sử dành cho quản lý/sinh viên |

Luồng chốt: POST có antiforgery → khóa tài khoản người thao tác rồi khóa CLB trong transaction → đọc lại quyền, giờ và trạng thái → so phiên bản hoạt động và toàn bộ danh sách → xử lý Unmarked theo xác nhận → đánh dấu roster → ghi thời điểm và audit → commit.

RosterVersion là SHA-256 của mã đăng ký và RowVersion của từng đăng ký/kết quả trong toàn bộ danh sách. Nó chỉ dùng phát hiện thay đổi sau khi mở trang, không phải mật khẩu hoặc cơ chế phân quyền. Một người ở trang khác được điểm danh cũng làm phiên bản đổi; form chốt cũ phải được tải lại.

Lỗi ghi audit làm rollback cả các kết quả chuyển Vắng, dấu roster và thời điểm chốt. Sửa sau chốt dùng action AttendanceCorrected với snapshot trước/sau, lý do, người và thời gian. Lưu lại đúng kết quả hiện tại không tạo lịch sử sửa giả.

## Kiểm thử và nghiệm thu

24 ca PB15 mới; toàn bộ **377/377 kiểm thử tích hợp đạt**. Bao phủ chốt thiếu xác nhận, toàn bộ danh sách khi đang lọc, hai người chốt cùng lúc, chốt đua với ghi/hủy, sửa thiếu lý do, cấm thêm người/Unmarked, rollback audit lỗi, quyền lịch sử, rời CLB và bảo vệ migration. Ba ca rollback được chạy lại sau khi đổi câu lệnh tạo trigger kiểm thử sang SQL cố định; bản cuối không còn cảnh báo build.

Edge headless thử từ quản lý chốt đến sinh viên xem kết quả; cả hai màn hình không tràn ngang ở 360/768/1366 px, không có lỗi JavaScript. Đã xem ảnh màn hình dữ liệu thử.

Nhóm có thể nghiệm thu nhanh bằng một hoạt động đã kết thúc có hai đăng ký: chốt khi còn Unmarked phải bị chặn; xác nhận chuyển Vắng rồi chốt; sửa một người thành Có mặt có lý do; sinh viên chỉ xem được kết quả/lịch sử của mình; mẫu số vẫn là hai.

Migration: **20261110120624_FinalizeAttendance**. Dữ liệu cũ mặc định chưa chốt; không tự tạo lịch sử hoặc chốt các hoạt động cũ. Down từ chối nếu đã có danh sách chốt để tránh mất dữ liệu truy vết.

Database trên máy hiện tại đã được sao lưu COPY_ONLY/CHECKSUM, kiểm tra RESTORE VERIFYONLY và áp dụng migration. SHA-256 toàn bộ cột cũ của mười bảng khớp trước/sau; metadata backup tại `.local/finalization-backup.json`. Database và máy chủ QA đã được dọn, không giữ cổng 5026 của người dùng.

Sau cập nhật, dừng ứng dụng bằng Ctrl+C rồi chạy lại `dotnet run`. Máy khác của nhóm cần áp dụng migration mới. PB16 đã có [đăng thông báo theo đối tượng](announcements.md), PB17 có [hộp thông báo và đã đọc](notification-inbox.md); bước tiếp theo PB18 nối thông báo thay đổi/hủy hoạt động.
