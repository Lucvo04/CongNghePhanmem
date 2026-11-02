# Bước 5 — Quản lý tài khoản sinh viên (PB02)

Ngày thực hiện: 30/10/2026. Thuộc Sprint 1; tiếp nối đăng nhập và đổi mật khẩu ở bước 3–4.

## Mục tiêu và cách sử dụng

Admin mở **Quản trị → Quản lý tài khoản sinh viên**. Danh sách tìm theo tên, mã hoặc email, lọc Active/Disabled, phân trang 10 tài khoản. Danh sách không hiển thị Admin, kể cả tài khoản có đồng thời hai role.

**Cấp tài khoản:** nhập họ tên, mã SV, email, mật khẩu ban đầu và xác nhận; lớp/khoa tùy chọn. Server trim dữ liệu (không trim mật khẩu), viết hoa mã và viết thường email. Mã SV là chuỗi, giữ số 0 đầu, tối đa 24 ký tự, không có khoảng trắng bên trong. Tạo luôn role Student và trạng thái Active; dữ liệu POST chèn thêm Id/Role/Status không được áp dụng.

Mật khẩu tuân chính sách Identity đã có: ít nhất 10 ký tự, chữ hoa, chữ thường, số và ký tự đặc biệt. Khi form lỗi, mật khẩu không được trả lại HTML. Khi thành công, Admin bàn giao riêng cho sinh viên; ứng dụng không gửi email, không có chức năng xem lại mật khẩu và chưa buộc đổi ở lần đăng nhập đầu. Sinh viên dùng **Tài khoản → Đổi mật khẩu** để đổi.

**Khóa/mở khóa:** chọn tài khoản, xem danh tính ở trang xác nhận, nhập lý do tối đa 500 ký tự rồi xác nhận. Khóa chặn đăng nhập và request tiếp theo của phiên đang có; không chủ động đóng trang đã tải trên máy khác. Mở khóa yêu cầu đăng nhập mới, giữ mật khẩu và thời hạn lockout do sai mật khẩu nếu đang có. Không tự thay đổi tư cách thành viên hoặc xóa hồ sơ. Form cũ/xử lý lặp bị trả HTTP 409 và yêu cầu tải lại thông tin, không tự đảo trạng thái lần nữa.

## Trách nhiệm trong MVC

| Thành phần | Trách nhiệm |
|---|---|
| `Areas/Admin/Controllers/StudentsController` | Kiểm tra quyền Admin, nhận/chuẩn hóa/validate dữ liệu, truy vấn trang danh sách và trả View/redirect |
| `ViewModels/Admin/StudentViewModels` | Quy định dữ liệu form được nhận, lỗi tiếng Việt và dữ liệu cần hiển thị |
| `Services/StudentAccountService` | Tạo user/role; đổi trạng thái và SecurityStamp; kiểm tra xung đột; ghi audit cùng transaction |
| `Areas/Admin/Views/Students` | Danh sách, form tạo và xác nhận trạng thái; dùng Bootstrap/layout chung |
| `ApplicationUser`, `AuditLog`, `ApplicationDbContext` | Dữ liệu Identity, nhật ký và ràng buộc SQL |

Service dùng DbContext/Identity cùng scope; không thêm repository để bọc lại các chức năng sẵn có. `[Authorize]` áp dụng mọi action của controller; antiforgery áp dụng mọi POST. `Student` trong ViewModel xác nhận chỉ được nạp từ server và không bind từ form.

## Giao dịch, cạnh tranh và nhật ký

- Tạo: kiểm tra mã trùng → transaction → UserManager tạo người dùng → gán role Student → ghi audit → commit. Unique index chống trùng email/mã ngay cả khi hai request chạy song song; lỗi 2601/2627 được chuyển thành thông báo tiếng Việt. Không serialize request chứa mật khẩu vào log.
- Trạng thái: đọc sinh viên hợp lệ, kiểm tra ConcurrencyStamp từ form → lưu Status + SecurityStamp mới bằng Identity → ghi audit → commit. Identity kiểm tra concurrency tại UPDATE; rollback nếu một phần thất bại.
- Cả khóa và mở khóa đều đổi stamp. Nếu người dùng không gửi request nào trong lúc bị khóa, cookie cũ vẫn bị từ chối sau khi mở khóa.
- `AuditLogs` có ActorId, Action (`StudentCreated`, `StudentDisabled`, `StudentEnabled`), EntityType/EntityId, BeforeData/AfterData, Reason và OccurredAt UTC. Chỉ chụp trường trạng thái/role cần thiết. Không có API sửa/xóa hoặc giao diện tra cứu audit ở bước này.
- Xóa user vật lý không thuộc phạm vi. Các khóa ngoại giữ lịch sử. **Bước 6 đã bổ sung:** chặn khóa tài khoản còn đang làm chủ nhiệm; cần chuyển giao tất cả CLB đang phụ trách trước. Khóa theo tài khoản trong transaction SQL được dùng chung với tạo/chuyển chủ nhiệm để bảo vệ trường hợp thao tác đồng thời.

Tham khảo kỹ thuật: [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions). Giao dịch bên ngoài giữ toàn bộ các lần SaveChanges của Identity và audit thành một thao tác nguyên tử.

## Kiểm thử đã chạy

25 ca mới trong `StudentAccountTests`, tổng suite **79 đạt / 0 lỗi / 0 bỏ qua**. Build Release cuối cùng **0 warning / 0 error**. SQL Server thật và WebApplicationFactory; database ngẫu nhiên riêng được tạo/xóa bởi fixture, không dùng database ứng dụng.

| Nhóm kiểm tra | Kết quả mong đợi đã xác minh |
|---|---|
| Khách/Student gọi GET và POST quản trị | Chuyển đăng nhập hoặc từ chối quyền |
| CSRF, cache dữ liệu cá nhân | Thiếu token trả 400; GET có no-store |
| Cấp hợp lệ và chèn Role/Status/Id | Chỉ tạo Student Active; giữ số 0 đầu; mật khẩu được băm; đăng nhập được |
| Thiếu/sai dữ liệu, mật khẩu yếu/xác nhận sai | Lỗi tiếng Việt; không tạo tài khoản, không trả lại mật khẩu |
| Trùng email/mã khác hoa thường; hai yêu cầu đồng thời | Tối đa một người dùng, một role và một audit được ghi |
| Khóa/mở và phiên hiện tại/phiên chưa truy cập | Khóa chặn truy cập; mở không hồi sinh cookie cũ; đăng nhập mới thành công |
| Lockout do sai mật khẩu | Mở khóa quản trị không xóa LockoutEnd hoặc AccessFailedCount |
| Form cũ và gửi lặp | HTTP 409; không thêm audit hoặc đổi trạng thái lần hai |
| Thay ID thành chính Admin, Admin khác, dual-role, ID lạ | GET/POST trả 404; không đổi đối tượng |
| Audit lỗi khóa ngoại | Rollback user/role hoặc Status/SecurityStamp/ConcurrencyStamp |
| Tìm/lọc/phân trang, nội dung có thẻ HTML | Kết quả đúng, không lộ Admin; Razor encode nội dung |

Minh chứng cục bộ: `.local/test-results/step-5.trx`, `.local/step-5-tests.log`, `.local/step-5-build.log`, `.local/step-5-migration.log`. Không đưa thư mục `.local` vào Git.

Browser QA thực hiện với Admin hiện có: điều hướng Dashboard → danh sách → form, lọc trạng thái, validation trống tại client, nút Hủy, menu mobile và đăng xuất. Form/danh sách trống kiểm tra ở viewport 1366×1000 và 390×844, không tràn ngang; không thấy console error/warning. Ảnh `.local/qa/students-create-desktop.png`, `students-create-mobile.png`, `students-list-desktop.png`, `students-list-mobile.png`. Luồng tạo/khóa/mở có dữ liệu được kiểm thử bằng HTTP trên database thử; chưa nhập sinh viên thật qua browser.

## Review và nghiệm thu của nhóm

PB02 sẵn sàng review cục bộ; chưa tự ghi nhận nhóm đã UAT hay hoàn tất Sprint 1.

1. Thành viên phụ trách review service, phân quyền và migration chỉ thêm AuditLogs; kiểm tra không đưa bí mật vào Git.
2. Thành viên kiểm thử chạy suite trên máy thứ hai; dùng dữ liệu giả trong database demo riêng để thử cấp → Student đăng nhập → Admin khóa → Student bị chặn → Admin mở → Student đăng nhập lại. Đối chiếu audit và email/mã trùng.
3. Nhóm trưởng xác nhận các tiêu chí PB02; cập nhật bảng công việc theo thực tế. Nếu chưa review, giữ trạng thái “Chờ review”, không chuyển cả Sprint sang Done.

Bước 6 đã bổ sung tạo/sửa CLB, chuyển chủ nhiệm và kiểm tra quyền theo CLB; xem `club-management.md`. Bước 8 bổ sung PB06 sửa hồ sơ cá nhân (`personal-profile.md`), dùng chung khóa theo tài khoản khi lưu để phối hợp với khóa/mở tài khoản.
