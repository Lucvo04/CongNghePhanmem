# Bước 3–4 — Xác thực và quyền hệ thống

Ngày triển khai: 30/10/2026. Phần việc trong Sprint 1: đăng nhập/đăng xuất, quyền hệ thống và đổi mật khẩu chạy từ form đến database. Không tạo thêm template quản trị; dùng chung layout Bootstrap, chỉ có trang chức năng đã triển khai.

## Luồng người dùng

1. Khách mở trang cần đăng nhập sẽ được chuyển đến `/Account/Login`, kèm ReturnUrl nội bộ.
2. Nhập email, mật khẩu và tùy chọn duy trì đăng nhập.
3. Identity kiểm tra trạng thái Active, mật khẩu và lockout. Sau 5 lần sai, khóa đăng nhập 15 phút; đăng nhập đúng sau khi hết khóa được phép.
4. Thành công: quay lại URL nội bộ, hoặc Admin vào `/Admin`, người dùng khác vào `/Account/Overview`.
5. Người dùng đăng xuất bằng nút POST có antiforgery token. Không có hành động đăng xuất qua GET.

Form dùng DataAnnotations tiếng Việt và jQuery validation có sẵn trong template MVC. Server vẫn kiểm tra độc lập; không phụ thuộc JavaScript. Mật khẩu không được đưa trở lại HTML khi có lỗi. Sai mật khẩu, email không tồn tại, Disabled hoặc đang khóa đều dùng một thông báo chung; chưa cam kết thời gian phản hồi đồng nhất giữa các tình huống.

## Quyền theo trang

| Trang/thao tác | Khách | Người đã đăng nhập | Admin |
|---|---|---|---|
| Trang chủ, form đăng nhập | Có | Có; form chuyển về trang phù hợp | Có; form chuyển về trang phù hợp |
| Tài khoản của tôi | Chuyển sang đăng nhập | Xem chính mình | Xem chính mình |
| Chỉnh sửa hồ sơ | Chuyển sang đăng nhập | Chỉ họ tên, điện thoại, lớp/khoa của mình | Cùng giới hạn, không sửa người khác |
| Đổi mật khẩu | Chuyển sang đăng nhập | Đổi của chính mình, cần mật khẩu hiện tại | Đổi của chính mình, cần mật khẩu hiện tại |
| `/Admin` | Chuyển sang đăng nhập | Từ chối nếu không có Admin | Có |
| `/Admin/Students`, cấp/khóa/mở khóa sinh viên | Chuyển sang đăng nhập | Từ chối nếu không có Admin | Có; không áp dụng cho tài khoản Admin |
| `/ClubManagement` và sửa hồ sơ CLB | Chuyển sang đăng nhập | Chỉ CLB đang là chủ nhiệm hợp lệ | Toàn hệ thống |
| Tạo CLB và chuyển chủ nhiệm | Chuyển sang đăng nhập | Từ chối nếu không có Admin | Có; kiểm tra điều kiện người nhận |
| Đăng xuất bằng POST hợp lệ | Yêu cầu đăng nhập | Có | Có |

`[Authorize(Roles = SystemRoles.Admin)]` bảo vệ controller Admin tại server. Ẩn link trên menu chỉ để giao diện phù hợp, không thay thế kiểm tra quyền. Trang từ chối trả HTTP 403 sau điều hướng của cookie handler. Quyền chủ nhiệm theo CLB chưa triển khai ở bước này.

## Phiên và bảo vệ form

- Cookie `StudentClub.Auth`, HttpOnly, SameSite=Lax; bắt buộc Secure ngoài Development. Đã thử đường dẫn HTTPS cục bộ.
- Thời hạn ticket 8 giờ, có gia hạn khi sử dụng. Bỏ chọn “Duy trì đăng nhập” dùng session cookie; chọn thì cookie có ngày hết hạn. Việc duy trì không có nghĩa là đăng nhập vô thời hạn.
- `SecurityStampValidator` chạy mỗi request có cookie. `ClubSignInManager` trả về null khi người dùng Disabled hoặc stamp không hợp lệ, khiến phiên bị loại bỏ. Role được dựng lại từ dữ liệu hiện tại nên gỡ Admin không giữ quyền cũ trong cookie.
- Chọn kiểm tra mỗi request vì quy mô đồ án nhỏ và cần dễ chứng minh thu hồi phiên. Đổi lại có thêm truy vấn database; cần đánh giá lại nếu mở rộng tải.
- `AutoValidateAntiforgeryToken` áp dụng toàn cục cho thao tác thay đổi dữ liệu; form POST dùng token Razor. Thiếu token hợp lệ trả 400.
- ReturnUrl được kiểm tra bằng `Url.IsLocalUrl` trước `LocalRedirect`; không cho chuyển đến website ngoài sau đăng nhập.
- Trang tài khoản, quản trị và đăng nhập dùng `Cache-Control: no-store`. Không dùng ViewModel trả về PasswordHash, SecurityStamp hoặc thông tin bí mật khác.

## Quản trị viên đầu tiên

Không mở đường dẫn web để tự tạo Admin. Chỉ người chạy ứng dụng từ terminal cùng cấu hình cục bộ có thể gọi `--bootstrap-admin`.

1. Áp dụng migration để có role Admin.
2. `scripts/Prepare-LocalAdmin.ps1` nhận email/tên, tạo mật khẩu ngẫu nhiên bằng bộ sinh mật mã và lưu qua stdin vào User Secrets. Không ghi mật khẩu vào source hoặc tham số lệnh.
3. Chạy ứng dụng với `--bootstrap-admin`. `AdminBootstrapper` kiểm tra dữ liệu, xác nhận chưa có Admin và chưa có người dùng cùng email, tạo user bằng UserManager và gán role trong transaction Serializable.
4. Nếu có lỗi tạo user/gán role, transaction không commit. Chạy lại không đổi mật khẩu hoặc nâng quyền người dùng hiện có. Chạy thành công xong thoát, không mở server.
5. Người dùng chạy `scripts/Show-LocalAdminLogin.ps1` để xem thông tin tại terminal của mình. Script không thay đổi tài khoản. Nếu sau này đổi mật khẩu, giá trị này chỉ còn là mật khẩu khởi tạo.

User Secrets không được mã hóa và chỉ phù hợp phát triển cục bộ. Cấu hình triển khai thật cần cơ chế quản lý secret riêng. Có thể cấp ba biến `BootstrapAdmin__Email`, `BootstrapAdmin__FullName`, `BootstrapAdmin__Password` cho lệnh khởi tạo tại môi trường triển khai, rồi gỡ các giá trị sau khi dùng; không đặt mật khẩu trực tiếp trong lịch sử lệnh.

## Đổi mật khẩu đã đăng nhập — bước 4

`GET /Account/ChangePassword` hiển thị form. `POST` cần phiên hợp lệ và antiforgery token. Controller lấy người dùng từ claims của phiên; ViewModel chỉ nhận CurrentPassword, NewPassword, ConfirmPassword, không nhận UserId/Email để chọn tài khoản khác.

1. DataAnnotations kiểm tra bắt buộc, độ dài tối thiểu 10 ký tự và mật khẩu xác nhận khớp. Server vẫn kiểm tra khi JavaScript bị tắt.
2. `CheckPasswordSignInAsync(..., lockoutOnFailure: true)` xác minh mật khẩu hiện tại và dùng bộ đếm sai chung với đăng nhập. Sai 5 lần khóa 15 phút, đăng xuất phiên đang thao tác và hiển thị thông báo. `ChangePasswordAsync` tự nó không đếm số lần sai nên cần bước kiểm tra này.
3. Không cho dùng lại mật khẩu hiện tại. Không trim mật khẩu hoặc chuyển đổi chữ hoa/thường.
4. `UserManager.ChangePasswordAsync` kiểm tra lại mật khẩu hiện tại, áp dụng chính sách Identity và lưu bản băm cùng SecurityStamp mới. Lỗi chính sách mật khẩu và xung đột cập nhật được dịch sang tiếng Việt bằng IdentityErrorDescriber.
5. `RefreshSignInAsync` cấp cookie mới cho phiên hiện tại và giữ RememberMe. Validator kiểm tra stamp mỗi request khiến các cookie cũ trên thiết bị khác bị từ chối ngay ở request tiếp theo; không đóng trang đang hiển thị một cách chủ động.
6. Redirect về trang tài khoản và hiển thị thông báo thành công một lần. Khi có lỗi, xóa giá trị cả ba ô mật khẩu khỏi model/model state nhưng giữ thông báo lỗi; không gửi lại mật khẩu trong HTML.

Khi xác thực mật khẩu hiện tại sai hoặc dữ liệu mới không hợp lệ, bản băm và stamp đã lưu không thay đổi (ngoại trừ việc Identity có thể nâng cấp định dạng hash cũ trong quá trình xác minh). Kiểm thử dùng hash hiện tại của Identity. Bộ đếm sai được quản lý theo cơ chế lockout; khóa đăng nhập không đồng nghĩa tự thu hồi tất cả phiên hợp lệ khác. Thu hồi toàn bộ cookie cũ xảy ra khi đổi mật khẩu thành công.

Không ghi mật khẩu mới vào log, URL hoặc User Secrets. Script xem thông tin quản trị chỉ còn phản ánh mật khẩu khởi tạo nếu người dùng đã đổi mật khẩu. Bước này không cung cấp reset mật khẩu cho người quên mật khẩu hoặc đổi mật khẩu hộ người khác.

## Phạm vi kiểm thử

HTTP integration tests dùng WebApplicationFactory, Razor thật, cookie thật và SQL Server thật. Host được thay DbContext sang database thử ngẫu nhiên; trước mọi thao tác tạo user, kiểm tra đúng server và tên database. Khóa Data Protection của test chỉ nằm trong bộ nhớ. Không giả lập authentication để bỏ qua đăng nhập.

Kiểm tra: khách vào trang bảo vệ; Admin vào khu vực Admin; Student bị từ chối; email không phân biệt hoa/thường; mật khẩu sai, email lạ, Disabled; lockout; thu hồi phiên sau Disabled/stamp đổi; gỡ role Admin; CSRF đăng nhập/đăng xuất; GET không đăng xuất; ReturnUrl ngoài; RememberMe; lỗi mật khẩu trống; bootstrap từ chối cấu hình thiếu/mật khẩu yếu/email trùng và không tạo thêm Admin khi chạy lại.

Các test nền ở bước 3 thay trạng thái/gỡ role qua fixture. Bước 5 bổ sung kiểm thử khóa/mở khóa qua endpoint quản trị thật, gồm cả cookie đã dùng khi bị khóa và cookie chưa gửi request cho đến sau lúc mở khóa.

Bước 4 bổ sung 18 kiểm thử HTTP: xác thực/CSRF/no-store, sai mật khẩu hiện tại, từng điều kiện mật khẩu mới, trường bắt buộc/nhập lại, không dùng mật khẩu cũ, Admin/Student và RememberMe, hai thiết bị đăng nhập đồng thời, form cũ hết quyền sau đổi, mật khẩu cũ không dùng được, khóa sau nhiều lần sai, Disabled trước lúc submit và UserId bị chèn vào form không đổi được tài khoản khác. Toàn bộ suite hiện có 54 trường hợp. Mọi thay đổi mật khẩu trong test chỉ nằm ở database thử; không dùng tài khoản quản trị thật để thay mật khẩu.

## Cấp và khóa tài khoản ở bước 5

Xem `student-accounts.md`. Thao tác chỉ dành cho Admin; form chỉ nhận trường được phép, service luôn cấp Student. Khóa/mở khóa thay SecurityStamp trong cùng transaction với Status và audit, nên cookie cũ không thể được khôi phục bằng cách mở lại tài khoản. Không thay PasswordHash, không tự bỏ LockoutEnd. ConcurrencyStamp chống ghi đè từ form cũ. Các form không tác động tài khoản Admin, kể cả tài khoản có đồng thời Admin và Student.

Bước 5 thêm 25 trường hợp, nâng toàn bộ suite lên **79/79 đạt**. Bộ kiểm thử gồm HTTP/cookie/Razor thật và SQL Server, không thay mật khẩu hay tạo sinh viên mẫu trong database ứng dụng. Bản build sau dọn cảnh báo analyzer đạt 0 warning, 0 error.

## Danh mục CLB công khai ở bước 7

`/Clubs` và `/Clubs/Details/{id}` cho phép khách chưa đăng nhập. Chúng chỉ trả thông tin giới thiệu CLB Active/Suspended, không trả danh sách thành viên, hồ sơ tài khoản hoặc dữ liệu Identity. Admin cũng không xem được CLB Archived qua đường dẫn công khai; quyền quản lý dùng các controller nội bộ riêng. Phản hồi đặt no-store vì layout có điều hướng theo phiên người dùng. 18 ca kiểm thử mới đưa toàn suite lên **126/126 đạt**, có kiểm tra HTML công khai bằng dữ liệu cá nhân giả dễ nhận diện; xem `public-clubs.md`.

## Chưa triển khai

- Bắt buộc đổi mật khẩu lần đầu và giao diện tra cứu nhật ký quản trị. Cấp/khóa/mở khóa và ghi audit tài khoản đã có ở bước 5; sửa hồ sơ cá nhân đã có ở bước 8, xem `personal-profile.md`.
- Quên/đặt lại mật khẩu, xác minh email và email gửi ra ngoài, 2FA.
- Phân quyền sự kiện/thông báo/thành viên theo CLB ở Sprint 2–3; quyền sửa hồ sơ CLB và chuyển chủ nhiệm đã có ở bước 6.
- Rate limiting theo IP, triển khai production và kiểm thử tải. Account lockout đã có nhưng không thay thế mọi cơ chế chống lạm dụng.

Các giới hạn này được giữ ngoài menu để tránh hiển thị chức năng chưa hoạt động. Chưa coi toàn bộ backlog xác thực/quản lý tài khoản hoàn thành chỉ vì luồng đăng nhập đã đạt.

Bước 6 bổ sung 29 ca kiểm thử, toàn suite **108/108 đạt**. Quyền CLB được đọc từ membership hiện tại trên server, không lưu role Manager toàn hệ thống hoặc dựa vào ClubId từ UI. Sau chuyển giao, cookie cũ của người tiền nhiệm vẫn là phiên sinh viên hợp lệ nhưng không còn quyền sửa CLB. Việc đổi SecurityStamp không cần thiết khi chỉ thay vai trò CLB vì mỗi yêu cầu đều truy vấn quyền hiện tại. Khóa tài khoản chủ nhiệm bị chặn trước khi đổi stamp/trạng thái cho đến khi chuyển giao hết các CLB.

Tham khảo: [Cấu hình ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0), [kiểm thử tích hợp ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/testing/integration-testing), [ChangePasswordAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.changepasswordasync?view=aspnetcore-10.0), [mã nguồn SignInManager](https://source.dot.net/Microsoft.AspNetCore.Identity/SignInManager.cs.html).
