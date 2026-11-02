# Bước 8 — Hồ sơ cá nhân (PB06)

Hoàn tất kiểm tra cục bộ ngày 02/11/2026 (Asia/Bangkok). Phạm vi: sinh viên và Admin xem/sửa hồ sơ của chính mình. Chờ nhóm review và nghiệm thu; không coi kết quả tự động là Sprint Review hoặc UAT của người dùng.

## Cách sử dụng

Khởi động lại ứng dụng theo README, đăng nhập → **Tài khoản → Chỉnh sửa hồ sơ**. Nhập thông tin, chọn **Lưu hồ sơ**, kiểm tra thông báo thành công và dữ liệu trên trang tài khoản. Mở lại form để tiếp tục chỉnh sửa.

| Trường | Quy tắc |
| --- | --- |
| Họ và tên | Bắt buộc, tối đa 150 ký tự; trim hai đầu khi lưu |
| Số điện thoại | Không bắt buộc, 7–15 chữ số ASCII, cho phép một dấu + ở đầu; không khoảng trắng/dấu gạch; giữ số 0 đầu |
| Lớp | Không bắt buộc, tối đa 50 ký tự; trim và đổi chuỗi trắng thành null |
| Khoa | Không bắt buộc, tối đa 100 ký tự; trim và đổi chuỗi trắng thành null |
| Email, mã SV, quyền, trạng thái | Không nằm trong dữ liệu được phép cập nhật, kể cả chèn thêm trường vào HTTP POST |

Điện thoại chỉ dùng liên hệ, chưa có SMS hoặc xác minh. Đổi/xóa số sẽ đặt PhoneNumberConfirmed=false; giữ nguyên số thì không tự xóa cờ này. Không đổi mật khẩu, SecurityStamp, role hoặc các thiết bị đang đăng nhập. Lớp/khoa là thông tin tự khai, không phải dữ liệu đã xác nhận từ nhà trường. Admin có thể để trống lớp/khoa.

**Hủy** bỏ các thay đổi chưa lưu. **Tải lại hồ sơ** mở dữ liệu mới từ server và bỏ các ô đang sửa. Nếu có người/thiết bị cập nhật tài khoản sau khi mở form, POST trả HTTP 409 kèm thông báo để tải lại, không tự ghi đè hoặc âm thầm thử lại bằng phiên bản mới. Gửi lại đúng dữ liệu hiện tại không tạo thêm audit.

## Triển khai và quyền riêng tư

- `AccountController.EditProfile`: GET/POST cần đăng nhập, no-store và antiforgery theo cấu hình MVC toàn cục. UserId chỉ lấy từ principal đã xác thực; không nhận mục tiêu từ query, route hoặc form.
- `EditProfileViewModel`: chỉ chứa bốn trường được sửa và ConcurrencyStamp. DataAnnotations hiển thị lỗi tiếng Việt ở client/server; dịch vụ vẫn kiểm tra giới hạn dữ liệu trước khi ghi.
- `PersonalProfileService`: bắt đầu transaction SQL, lấy khóa tài khoản dùng chung với quản trị trạng thái, nạp lại user để loại dữ liệu EF đã theo dõi trước khi lấy khóa, kiểm tra Active và ConcurrencyStamp. `UserManager.UpdateAsync` đổi ConcurrencyStamp và bảo vệ cập nhật đồng thời ở database.
- Chỉ cập nhật trường cho phép; Identity credentials và tư cách thành viên giữ nguyên. Cùng transaction lưu `PersonalProfileUpdated` vào AuditLogs. Audit chỉ chứa tên trường đã thay đổi, ActorId/EntityId/thời gian, không chép số điện thoại hoặc toàn bộ hồ sơ. Đây không phải chức năng phục hồi lịch sử nội dung.
- Razor encode nội dung, không render HTML người dùng nhập. Danh mục công khai vẫn dùng DTO riêng, không bổ sung hồ sơ cá nhân vào `/Clubs`.
- Dùng PhoneNumber, PhoneNumberConfirmed và ConcurrencyStamp đã có trong Identity, ClassName/Faculty đã có trong model. Không đổi schema, không có migration mới và không thêm thư viện/template.

## Kiểm tra đã thực hiện

- **146/146 kiểm thử đạt**, 0 thất bại, 0 bỏ qua: 126 ca cũ + 20 ca `PersonalProfileTests`. HTTP/cookie/Razor và SQL Server thật trên database riêng do fixture tạo/dọn. TRX `.local/test-results/step-8.trx`, log `.local/step-8-tests.log`.
- Kiểm tra khách bị chặn, chống POST thiếu CSRF, no-store, lưu và đọc lại cho Student/Admin, trim/null, định dạng/độ dài lỗi, chèn ID/email/role/status/credential, bảo toàn tài khoản khác và session khác, HTML encode.
- Kiểm tra form cũ HTTP 409, hai form đồng thời chỉ một lần ghi/audit, không thay đổi không tạo audit, tài khoản Disabled và trường hợp user đã được EF theo dõi trước khi trạng thái đổi.
- Cưỡng bức lỗi audit bằng constraint chỉ trên database kiểm thử: xác nhận rollback họ tên, điện thoại, cờ xác minh và ConcurrencyStamp; constraint được dọn trong finally.
- Build Release cuối **0 warning, 0 error** (`.local/step-8-build.log`). Hai cảnh báo analyzer Assert.Single trong lần test đầu đã sửa cách viết mà không đổi assertion. EF xác nhận không có thay đổi model thiếu migration.
- Browser QA dùng một Admin giả trong database riêng: form lỗi họ tên/điện thoại, lưu bốn trường, xem lại trang tài khoản/form, Hủy không lưu, menu điện thoại. Kích thước 1366×1000 và 390×844; không phát hiện tràn ngang theo DOM trên mobile, không thấy console warning/error.
- Ảnh `.local/qa/profile-edit-desktop.png`, `profile-edit-mobile.png`, `profile-saved-desktop.png` chứa dữ liệu giả. Preview cổng 5048 đã dừng; tab tạm đã đóng, viewport đã reset và database QA đã xóa sau kiểm tra đúng tên sở hữu. Database ứng dụng không có audit PersonalProfileUpdated tại thời điểm kiểm tra; không thay hồ sơ/mật khẩu Admin thật.

## Nhóm review và nghiệm thu

- [ ] Một thành viên khác đọc controller, ViewModel và dịch vụ; xác nhận không cập nhật theo UserId do client gửi.
- [ ] Chạy thử trên máy thứ hai, cập nhật hồ sơ của mình, mở lại và kiểm tra dữ liệu.
- [ ] Thử form mở ở hai tab, lưu tab thứ nhất rồi gửi tab thứ hai để kiểm tra cảnh báo.
- [ ] Nhóm trưởng chấp nhận PB06 hoặc ghi feedback vào backlog. Các ô này chưa được đánh dấu thay nhóm.

Chưa có thay email, avatar, reset mật khẩu, xác minh SMS/email hoặc màn hình tra cứu audit. Bước tiếp theo dự kiến PB07 gửi/rút/theo dõi đơn gia nhập rồi PB08 duyệt đơn. PB05 còn nghiệp vụ trạng thái CLB và phụ thuộc các module tiếp theo; chưa ghi nhận toàn bộ Sprint 1 Done.
