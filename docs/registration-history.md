# PB13 — Danh sách đăng ký và lịch sử cá nhân

Triển khai ngày 10/11/2026, sau PB12. Giữ nguyên giao diện MVC/Bootstrap và chín bảng nghiệp vụ; không thêm bảng, cột hoặc migration.

## Nhóm có thể dùng như thế nào?

**Sinh viên:** vào **Tài khoản → Hoạt động đã đăng ký**. Tìm theo tên hoạt động/tên CLB, chọn CLB, trạng thái đăng ký và khoảng ngày bắt đầu hoạt động. Bỏ trống ngày để xem toàn bộ thời gian. Chọn **Lịch sử đăng ký** trên một thẻ để xem từng lần đăng ký, hủy và đăng ký lại; có thời gian và lý do hủy. Nút quay lại giữ bộ lọc và trang đang xem.

**Admin/chủ nhiệm:** vào **Chi tiết hoạt động → Danh sách đăng ký**, tìm theo họ tên hoặc mã sinh viên và lọc đã đăng ký/đã hủy. Chủ nhiệm chỉ xem danh sách thuộc CLB mình quản lý. Danh sách không chứa email, số điện thoại hoặc thông tin đăng nhập của sinh viên. Trang lịch sử cá nhân thuộc riêng người đăng ký, không mở cho quản lý xem hồ sơ ngoài phạm vi này.

Mỗi trang tối đa 10 đăng ký hoặc 10 lần thay đổi. Bộ lọc có lỗi trả trang để sửa, không trả lỗi máy chủ. Danh sách trống hiển thị gợi ý thay đổi bộ lọc.

## Quy tắc cần phân biệt khi trình bày đồ án

- **Đã đăng ký** nghĩa là đang giữ chỗ; **đã hủy đăng ký** nghĩa là không giữ chỗ. Hai trạng thái này được tính từ CancelledAt, không thêm cột Status.
- Trạng thái hoạt động (đã công bố/hủy/hoàn thành) được hiển thị riêng. Đăng ký không phải xác nhận đã đến dự. Điểm danh thuộc PB14–PB15.
- Từ ngày/Đến ngày lọc theo **ngày bắt đầu hoạt động hiện tại**, tính cả hai ngày, theo UTC+7. Thời gian truy vấn được đổi thành UTC; đầu ngày kế tiếp là biên loại trừ.
- Danh sách sắp theo lần đăng ký gần nhất rồi Id, giảm dần. Lịch sử sắp theo thời điểm thao tác rồi Id, giảm dần; thứ tự vẫn xác định khi nhiều thao tác có cùng thời điểm.
- Hủy rồi đăng ký lại vẫn dùng cùng một EventRegistrations.Id. Mỗi thao tác đã có một bản ghi AuditLogs từ PB12 nên các mốc cũ không mất.
- Khi rời/đình chỉ CLB hoặc CLB được lưu trữ, sinh viên vẫn xem lịch sử đăng ký của mình. Trang chỉ giữ thông tin tóm tắt và nhật ký của chính đăng ký đó; liên kết đến toàn bộ nội dung hoạt động chỉ hiện nếu người dùng còn quyền xem hiện tại.
- Thông tin tên/lịch hoạt động ở đầu trang là thông tin hiện tại, không phải ảnh chụp của lịch cũ. Nếu một bản ghi nhập từ bên ngoài không có audit, hệ thống báo chưa có nhật ký, không tự suy đoán lịch sử.
- Trang lịch sử chỉ hiển thị hai loại thao tác đăng ký/hủy, thời gian, lý do và nhãn người thực hiện “Bạn”/“Người quản lý”. Không xuất JSON nội bộ, ID tài khoản hay hồ sơ của người thao tác.

## Code nằm ở đâu?

| File trong src/StudentClubManagement.Web | Vai trò |
|---|---|
| Controllers/EventRegistrationsController.cs | Mine/Index lọc danh sách; History kiểm tra chủ đăng ký rồi đọc nhật ký |
| ViewModels/Events/RegistrationViewModels.cs | RegistrationFilter kiểm tra đầu vào; các record chỉ mang dữ liệu cần hiển thị |
| Views/EventRegistrations/Index.cshtml | Form tìm kiếm/lọc, thẻ đăng ký, liên kết lịch sử, phân trang |
| Views/EventRegistrations/History.cshtml | Thông tin tóm tắt và danh sách các lần thay đổi |
| Services/EventRegistrationService.cs | Nghiệp vụ đăng ký/hủy đã có ở PB12; ghi AuditLogs trong cùng transaction |

Luồng đọc: trình duyệt gửi GET → Controller lấy người dùng từ phiên đăng nhập → giới hạn dữ liệu theo quyền → lọc/đếm/phân trang tại SQL → ánh xạ sang ViewModel → Razor tạo HTML. Không nhận UserId từ bộ lọc để quyết định lịch sử của ai.

Trang riêng dùng NoStore để tránh lưu trang tài khoản vào cache dùng chung. Razor mã hóa tên/lý do để nội dung người dùng nhập không biến thành HTML chạy được. ReturnUrl chỉ nhận đường dẫn nội bộ với độ dài giới hạn.

## Giải thích SQL ngắn gọn

EventRegistrations giữ trạng thái mới nhất. AuditLogs giữ các lần thay đổi. Lọc theo cả EntityType và EntityId để không nhầm đăng ký số 5 với thành viên số 5.

Ví dụ bên dưới dùng tham số do server cung cấp; trong ứng dụng EF Core sinh SQL có tham số:

```sql
-- @CurrentUserId lấy từ phiên đăng nhập; @RegistrationId là đăng ký cần xem.
SELECT a.Id, a.Action, a.OccurredAt, a.Reason
FROM AuditLogs AS a
INNER JOIN EventRegistrations AS r
    ON a.EntityType = N'EventRegistration'
    AND a.EntityId = CONVERT(nvarchar(450), r.Id)
WHERE r.Id = @RegistrationId
  AND r.UserId = @CurrentUserId
  AND a.Action IN (N'EventRegistered', N'EventRegistrationCancelled')
ORDER BY a.OccurredAt DESC, a.Id DESC
OFFSET @Skip ROWS FETCH NEXT 10 ROWS ONLY;
```

Không cần bảng RegistrationHistory riêng vì AuditLogs đã có thời gian, hành động, lý do và snapshot trước/sau. Quyền truy cập phải kiểm tra ở máy chủ; việc ẩn liên kết trên giao diện chỉ hỗ trợ trải nghiệm.

## Nghiệm thu của nhóm

1. Đăng ký một hoạt động, tự hủy, rồi đăng ký lại khi còn hạn/chỗ. Trang lịch sử phải có ba mốc; danh sách chỉ có một đăng ký hiện tại.
2. Lọc bằng từ khóa, trạng thái, CLB và ngày; chuyển trang rồi mở lịch sử/quay lại. Bộ lọc được giữ nguyên.
3. Đăng nhập chủ nhiệm, tìm mã sinh viên trong đúng hoạt động; lọc đã hủy không được hiện người đang giữ chỗ.
4. Đổi ID lịch sử sang đăng ký của người khác phải bị từ chối. Tài khoản khác không đọc được qua URL trực tiếp.
5. Sau khi sinh viên rời CLB, lịch sử của họ vẫn có; nội dung nội bộ không được mở lại nếu không còn quyền.

15 kiểm thử mới bao phủ các quy tắc này cùng biên ngày UTC+7, dữ liệu nhập sai, phân trang ổn định, mã hóa HTML, thu hồi quyền và nhật ký thiếu. Tổng **328/328 kiểm thử tích hợp đạt** trên SQL Server riêng.

Đã chạy thử luồng sinh viên/chủ nhiệm bằng Edge headless trên database riêng, kiểm tra danh sách và lịch sử ở 360/768/1366 px không tràn ngang, không có lỗi JavaScript. Build cuối không có lỗi/cảnh báo; EF xác nhận không thiếu migration. Máy chủ và database thử đã được dọn; không giữ cổng chạy ứng dụng của người dùng.

Sau khi nhận code, dừng bản đang chạy bằng Ctrl+C rồi chạy lại `dotnet run` trong thư mục dự án Web. PB13 không cần migration riêng. PB14–PB15 đã bổ sung [điểm danh, chốt và kết quả cá nhân](attendance-finalization.md); danh sách đăng ký có thêm liên kết Kết quả điểm danh. Bước kế tiếp là PB16 thông báo theo đối tượng.
