# PB14 — Điểm danh người đăng ký

Triển khai ngày 10/11/2026. PB14 ghi nhận kết quả ban đầu; PB15 đã bổ sung [chốt danh sách, sửa có lý do và kết quả cá nhân](attendance-finalization.md). Các mô tả cấu trúc 5 cột và kiểm thử 353 bên dưới là mốc PB14; Attendances hiện có 6 cột sau PB15.

## Cách sử dụng

1. Đăng nhập bằng Admin hoặc chủ nhiệm của CLB tổ chức hoạt động.
2. Vào **Quản lý hoạt động → Chi tiết hoạt động → Điểm danh**. Danh sách đăng ký cũng có liên kết Điểm danh.
3. Tìm bằng tên hoặc mã sinh viên; lọc **Chưa điểm danh / Có mặt / Vắng / Có phép**.
4. Chọn kết quả trên thẻ sinh viên, nhập ghi chú nếu cần rồi bấm **Lưu điểm danh**. Mỗi nút chỉ lưu người trên thẻ đó; các thẻ khác không tự lưu.
5. Sau lưu, trang hiển thị kết quả, người thực hiện và thời gian gần nhất. Bộ lọc/trang hiện tại được giữ; người vừa đổi trạng thái có thể không còn thuộc bộ lọc đang chọn.

Trước giờ bắt đầu có thể kiểm tra danh sách nhưng chưa có nút lưu. Không tự thêm sinh viên chưa đăng ký hoặc đăng ký đã hủy trước giờ bắt đầu vào danh sách.

## Quy tắc nghiệp vụ

| Trạng thái | Ý nghĩa |
|---|---|
| Chưa điểm danh (0) | Chưa xác định kết quả; không tự coi là vắng |
| Có mặt (1) | Người quản lý ghi nhận sinh viên có tham dự |
| Vắng (2) | Người quản lý xác định không có mặt |
| Có phép (3) | Người quản lý ghi nhận vắng có phép |

- Chỉ Admin Active hoặc chủ nhiệm Active đúng CLB được xem/ghi danh sách. Sinh viên thường và chủ nhiệm CLB khác không được truy cập bằng cách đổi URL/ID.
- Chỉ ghi từ StartsAt trở đi cho hoạt động đã công bố hoặc đã đánh dấu hoàn thành. Đánh dấu hoạt động hoàn thành không đồng nghĩa chốt điểm danh.
- CLB Suspended vẫn được xử lý kết quả của hoạt động đã bắt đầu. CLB Archived chỉ đọc. Hoạt động Draft/Cancelled không được ghi kết quả.
- Đăng ký hợp lệ có RegisteredAt không sau StartsAt và CancelledAt null. Đăng ký nhập sai sau giờ bắt đầu không được đưa vào danh sách xử lý.
- Tập người đăng ký được giữ từ giờ bắt đầu theo quy tắc PB12. Sinh viên rời CLB, bị đình chỉ hoặc khóa tài khoản sau đó không tự biến mất khỏi danh sách lịch sử.
- Hủy cả hoạt động vẫn giữ kết quả đã ghi và nhật ký, nhưng trang trở thành chỉ đọc. Các kết quả này không được dùng như hoạt động tham dự hợp lệ khi làm báo cáo sau này.
- Có thể đổi kết quả hoặc đưa về Chưa điểm danh trước khi danh sách được chốt. Sau chốt cần lý do và không đưa về Chưa điểm danh. Không xóa dòng đã ghi; ghi chú của mỗi lần đổi nằm ở AuditLogs. Gửi lại cùng kết quả hiện tại không tạo thêm nhật ký hoặc đổi người/thời gian.
- Form mang phiên bản hoạt động, đăng ký và kết quả điểm danh. Nếu dữ liệu hoặc quyền vừa thay đổi, máy chủ yêu cầu tải lại, không âm thầm ghi đè.
- PB15 đã có chốt và màn hình kết quả cá nhân đã chốt. Chưa có QR hoặc dashboard tỷ lệ tham dự. Màn hình lịch sử PB13 vẫn theo dõi riêng đăng ký.

## SQL: một bảng mới, năm cột

**Attendances** liên kết 1–0..1 với EventRegistrations. Không thêm cột vào Users/Roles và không lưu lặp họ tên/mã SV/tên hoạt động.

| Cột | Công dụng |
|---|---|
| RegistrationId | Vừa là khóa chính, vừa là khóa ngoại tới EventRegistrations.Id; bảo đảm một kết quả/đăng ký |
| Status | 0/1/2/3 tương ứng bốn trạng thái ở trên |
| MarkedAt | Thời gian máy chủ ghi kết quả gần nhất, lưu UTC |
| MarkedById | Khóa ngoại tới Users.Id của người thao tác gần nhất |
| RowVersion | SQL tự sinh để phát hiện form cũ |

Chưa có dòng Attendances được hiểu là Chưa điểm danh. Khi một kết quả đã ghi được đưa về Chưa điểm danh, dòng vẫn được giữ với Status = 0 để còn phiên bản/người/thời gian. Toàn bộ lần sửa lưu ở AuditLogs, không thêm bảng lịch sử riêng.

SQL CHECK chặn Status ngoài 0–3. FK Restrict giữ liên kết đăng ký/người thao tác; PK chặn ghi trùng. Migration **20261110103415_AddAttendances** chỉ thêm bảng/index/ràng buộc; Down từ chối nếu Attendances còn dữ liệu.

Ví dụ truy vấn danh sách hợp lệ của một hoạt động đang diễn ra/chưa hủy:

```sql
-- @EventId do server kiểm tra quyền quản lý trước khi truy vấn.
SELECT r.Id AS RegistrationId, u.FullName, u.StudentCode,
       COALESCE(a.Status, 0) AS AttendanceStatus,
       a.MarkedAt, marker.FullName AS MarkedBy
FROM EventRegistrations r
JOIN Events e ON e.Id = r.EventId
JOIN Users u ON u.Id = r.UserId
LEFT JOIN Attendances a ON a.RegistrationId = r.Id
LEFT JOIN Users marker ON marker.Id = a.MarkedById
WHERE r.EventId = @EventId
  AND r.CancelledAt IS NULL
  AND r.RegisteredAt <= e.StartsAt
ORDER BY u.FullName, r.Id;
```

LEFT JOIN giúp giữ cả người chưa có kết quả. Không dùng INNER JOIN Attendances vì sẽ bỏ mất người chưa được điểm danh. Khi hoạt động bị hủy, màn hình còn đọc những đăng ký có kết quả cũ để phục vụ tra cứu.

## Luồng MVC và vị trí code

| File trong src/StudentClubManagement.Web | Vai trò |
|---|---|
| Models/Attendance.cs | Model và enum bốn trạng thái |
| Data/Configurations/AttendanceConfiguration.cs | Mapping SQL, khóa, ràng buộc |
| ViewModels/Events/AttendanceViewModels.cs | Form và dữ liệu cần hiển thị |
| Controllers/AttendancesController.cs | GET danh sách tìm/lọc/phân trang; POST nhận form rồi gọi service |
| Services/AttendanceService.cs | Quyền, thời điểm, đăng ký hợp lệ, phiên bản và transaction |
| Views/Attendances/Index.cshtml | Thẻ sinh viên và form ghi kết quả |

Luồng ghi: Controller nhận POST có antiforgery → service mở transaction → khóa tài khoản người thao tác rồi khóa CLB → đọc lại quyền/dữ liệu → so phiên bản → lưu Attendances và AuditLogs → commit. Nếu audit lỗi, cả kết quả cũng rollback.

Người thực hiện lấy từ phiên đăng nhập; thời gian lấy từ máy chủ. Các giá trị MarkedById/MarkedAt giả mạo trong request bị bỏ qua vì không có trong form. Dùng cùng khóa CLB với hủy hoạt động/chuyển chủ nhiệm để các thao tác đồng thời không vượt qua điều kiện nghiệp vụ.

## Các ca nhóm cần nghiệm thu

1. Chọn hoạt động có đăng ký và đã bắt đầu; người chưa điểm danh phải hiện Chưa điểm danh.
2. Ghi Có mặt, đổi sang Có phép rồi về Chưa điểm danh: chỉ một dòng Attendances, nhật ký giữ đủ ba lần.
3. Mở cùng người ở hai tab; tab thứ nhất lưu, tab thứ hai gửi form cũ phải bị chặn.
4. Tìm theo mã SV, lọc trạng thái, chuyển trang; mỗi trang tối đa 10 người.
5. Chủ nhiệm CLB khác/sinh viên thường không đọc hoặc ghi được; hoạt động tương lai chưa được ghi.
6. Hủy một hoạt động thử sau khi đã ghi kết quả: kết quả còn để xem, không còn nút lưu.

25 kiểm thử mới; toàn bộ **353/353 kiểm thử tích hợp đạt** trên database SQL Server riêng, bao gồm concurrent writes/cancel, rollback audit lỗi, CSRF, overposting, form cũ, trạng thái, ràng buộc SQL và bảo vệ migration. EF xác nhận model khớp migration; bản build không cảnh báo/lỗi.

Edge headless đã thử lưu/sửa/đặt lại, lọc và giữ từ khóa; màn hình không tràn ngang tại 360, 768, 1366 px, không có lỗi JavaScript. Dữ liệu minh họa nằm riêng với database đồ án.

Database đồ án trên máy hiện tại đã được sao lưu COPY_ONLY/CHECKSUM, kiểm tra RESTORE VERIFYONLY và áp dụng migration. Dấu kiểm SHA-256 dữ liệu chín bảng cũ khớp trước/sau. Metadata bản sao lưu nằm tại `.local/attendance-backup.json`. Đã dọn database/tiến trình QA; không để máy chủ nền chiếm cổng 5026.

Sau cập nhật, dừng bản Web đang chạy bằng Ctrl+C rồi chạy lại `dotnet run`. Máy khác của nhóm cần áp dụng migration mới theo README trước khi chạy. PB15 đã triển khai; bước kế tiếp là **PB16 — Đăng thông báo theo đối tượng**.
