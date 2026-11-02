# Nhật ký triển khai

## Bước 1 Khởi tạo nền tảng MVC

Ngày thực hiện: 29/10/2026. Thuộc công việc nền tảng của Sprint 1, chưa hoàn thành PB01–PB06.

### Mục tiêu

Mỗi thành viên có thể mở solution, build và chạy trang đầu tiên. Xác nhận môi trường hiện có trước khi xây dữ liệu và xác thực.

### Quan sát môi trường

- .NET SDK: 10.0.101; target framework: net10.0.
- Git repository hiện có chưa có commit; giữ nguyên tài liệu phân tích.
- SQL Server Express 2022 tại `.\SQLEXPRESS`, phiên bản 16.0.1000.6, dịch vụ đang chạy.
- Kết nối SQL Server bằng Windows Authentication đã thành công với truy vấn chỉ đọc.
- Database `StudentClubManagement` chưa tồn tại ở thời điểm kiểm tra; bước này chưa tạo database.

### Thay đổi

- Tạo solution và project ASP.NET Core MVC từ template Microsoft.
- Cấu hình SDK, quy tắc định dạng và loại trừ build output, cache, cấu hình nhạy cảm khỏi Git.
- Tùy chỉnh layout và trang chủ tiếng Việt, dùng Bootstrap đi kèm template.
- Thêm `/health` để kiểm tra tiến trình, giữ xử lý lỗi và HTTPS cho môi trường triển khai.
- Viết hướng dẫn chạy và giới hạn hiện tại trong README.

### Xác minh

- `dotnet restore StudentClubManagement.sln`: thành công.
- `dotnet build StudentClubManagement.sln --configuration Release --no-restore`: thành công, 0 cảnh báo, 0 lỗi sau lần sửa giao diện cuối.
- Chạy ứng dụng tại `http://localhost:5026` trong Development: thành công.
- GET `/`, `/health`, CSS riêng, Bootstrap CSS/JS và `/Home/Error`: HTTP 200. `/health` trả `Healthy`.
- GET đường dẫn không tồn tại: HTTP 404.
- Kiểm tra trình duyệt ở 360, 768 và 1366 px: chiều rộng nội dung bằng chiều rộng hiển thị, không tràn ngang.
- Đã phát hiện và sửa gutter hàng Bootstrap làm tràn ngang trên điện thoại; kiểm tra lại sau build.
- Menu điện thoại mở được; chọn Trang chủ quay về trang gốc. Liên kết giới thiệu đi đến `#gioi-thieu`.
- Kích hoạt liên kết chuyển đến nội dung chính bằng bàn phím đưa focus đến `main-content`.
- Không thấy console error/warning trong phiên kiểm tra bản cuối.
- Ảnh kiểm tra cục bộ nằm trong `.local/qa/home-desktop.png` và `.local/qa/home-mobile.png`; thư mục này được loại khỏi Git.
- Chưa có bộ unit/integration test cho nghiệp vụ vì chưa triển khai nghiệp vụ. Các kiểm tra trên là build, HTTP và giao diện của khung ứng dụng.

Trạng thái bước 1: đạt mục tiêu khởi tạo. Kết nối SQL được kiểm tra độc lập bằng công cụ dòng lệnh; ứng dụng chưa tích hợp DbContext hoặc Identity. Server thử nghiệm được dừng sau kiểm tra; có thể chạy lại theo README.

### Phạm vi bước tiếp theo

Chốt mô hình tài khoản, CLB và thành viên; cấu hình EF Core SQL Server, Identity và migration đầu tiên. Khu vực quản trị được bổ sung cùng cơ chế xác thực và kiểm tra quyền.

## Bước 2 Mô hình dữ liệu, Identity và migration

Ngày thực hiện: 30/10/2026 (Asia/Bangkok). Tiếp nối nền tảng của Sprint 1. Chưa đánh dấu PB01–PB06 hoàn thành.

### Mục tiêu

Lưu được cấu trúc tài khoản, CLB và đợt tham gia trên SQL Server; kiểm chứng các ràng buộc quan trọng trước khi xây form và nghiệp vụ tài khoản.

### Thay đổi

- Thêm `ApplicationUser`, `Club`, `Membership`; phân biệt role hệ thống Admin/Student với vai trò Manager/Member trong từng CLB.
- Thêm `ApplicationDbContext` kế thừa IdentityDbContext, các cấu hình Fluent API, unique filtered indexes, check constraints, quan hệ không xóa dây chuyền lịch sử membership và rowversion.
- Tích hợp Identity vào DI và middleware; bật email duy nhất, chính sách mật khẩu và cấu hình lockout. Chưa có form/luồng đăng nhập nên chưa tuyên bố cơ chế chặn đăng nhập đã hoàn thành.
- Thêm readiness kiểm tra SQL và migration; giữ `/health` chỉ kiểm tra tiến trình.
- Cố định EF Core/Identity EF/dotnet-ef 10.0.14; công cụ dotnet-ef dùng manifest cục bộ.
- Lần restore đầu phát hiện NU1902/NU1903 với `System.Security.Cryptography.Pkcs` 9.0.11. Đã ghim 10.0.14 và kiểm tra lại toàn bộ dependency. Nguồn đối chiếu: [thông báo Microsoft thứ nhất](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), [thông báo thứ hai](https://github.com/advisories/GHSA-846f-f764-76mx). Không thay đổi runtime cài sẵn trên máy.
- Lưu kết nối SQL Express bằng User Secrets; không ghi connection string của máy vào appsettings được chia sẻ.
- Thêm project xUnit tích hợp SQL Server, hướng dẫn thiết lập trong README và thiết kế/giới hạn nghiệp vụ trong `docs/data-model.md`.

### Migration và dữ liệu

- Xác nhận `StudentClubManagement` chưa tồn tại trước thao tác tạo database.
- Sinh và đọc migration `20261029212526_InitialIdentityAndClubs`; xuất SQL vào `.local/initial-schema.sql` để rà soát. Tiền tố migration là thời gian UTC do EF sinh.
- Phần Up tạo bảng, chỉ mục, constraints và hai role cố định; không có thao tác xóa dữ liệu sẵn có. Không chạy phần Down.
- Áp dụng migration thành công vào database mới `StudentClubManagement` trên `.\SQLEXPRESS` bằng Windows Authentication.
- Xác nhận 10 bảng: 7 bảng Identity, Clubs, Memberships và bảng lịch sử migration. Role: Admin, Student. Số tài khoản/CLB/membership trong database ứng dụng đều bằng 0.
- Không tạo tài khoản quản trị hoặc mật khẩu mặc định. Dữ liệu giả chỉ được dùng trong database kiểm thử riêng.

### Kết quả kiểm chứng

- Build Release của solution: **0 cảnh báo, 0 lỗi**.
- `dotnet ef migrations has-pending-model-changes`: không có thay đổi mô hình chưa đưa vào migration.
- **21/21 kiểm thử tích hợp đạt**, 0 bỏ qua. Kiểm tra mật khẩu Identity, email/mã sinh viên/mã CLB trùng, nhiều CLB và tái gia nhập giữ lịch sử, trùng đợt đang mở, trùng chủ nhiệm, đình chỉ chủ nhiệm, thời gian tham gia, khóa ngoại bảo toàn lịch sử, xung đột rowversion ở CLB và membership, readiness với database tồn tại và không tồn tại.
- Kết quả TRX cục bộ: `.local/test-results/step-2.trx`; database kiểm thử được tạo ngẫu nhiên và dọn bằng fixture có kiểm tra quyền sở hữu tên database.
- Chạy website Development tại `http://localhost:5026`: trang chủ và CSS HTTP 200; `/health` và `/health/ready` HTTP 200, nội dung `Healthy`; route không tồn tại HTTP 404.
- Kiểm tra NuGet gồm dependency gián tiếp: cả project Web và IntegrationTests không có gói được nguồn hiện tại báo có lỗ hổng tại thời điểm kiểm tra. Đây không phải tuyên bố mọi thành phần môi trường đều đã được vá.
- Server kiểm tra được dừng sau HTTP smoke test. Không thay đổi giao diện trong bước này nên không lặp lại bộ kiểm tra bố cục của bước 1.

Trạng thái bước 2: hoàn thành nền tảng dữ liệu trên máy hiện tại. Một thành viên khác vẫn cần chạy lại README trên máy riêng trước khi nhóm xác nhận khả năng bàn giao môi trường.

### Việc tiếp theo

Bước 3 triển khai một luồng tài khoản nhỏ có thể dùng được: tạo quản trị viên có kiểm soát, đăng nhập, đăng xuất, xử lý tài khoản bị vô hiệu hóa và kiểm tra quyền truy cập. Tiếp theo mới triển khai tạo CLB/chuyển giao chủ nhiệm bằng transaction. Ràng buộc SQL hiện tại chỉ bảo đảm tối đa một chủ nhiệm, chưa bảo đảm CLB luôn có đúng một chủ nhiệm.

## Bước 3 Đăng nhập, đăng xuất và quyền hệ thống

Ngày thực hiện: 30/10/2026 (Asia/Bangkok). Giữ nguyên mô hình dữ liệu; không có migration mới trong bước này.

### Mục tiêu và kết quả

- Tạo quản trị viên đầu tiên qua lệnh rõ ràng, không seed tài khoản/mật khẩu khi khởi động website.
- Đăng nhập bằng email và mật khẩu Identity; duy trì phiên tùy chọn; đăng xuất qua POST có antiforgery.
- Xem thông tin tài khoản của mình và vào khu vực Admin khi có quyền; các link dùng chung layout Bootstrap.
- Chặn tài khoản Disabled ở luồng đăng nhập và ở cookie đang tồn tại; dựng lại quyền từ database mỗi request để không giữ quyền Admin đã bị gỡ.

### Thay đổi chính

- Thêm `AccountController`, Login/Overview ViewModels và các view đăng nhập, tài khoản, từ chối truy cập.
- Thêm `Areas/Admin` với Dashboard có `[Authorize(Roles = Admin)]`. Không thêm template hoặc menu quản trị chưa hoạt động.
- Thêm `ClubSignInManager`; cấu hình cookie HttpOnly, SameSite=Lax, Secure ngoài Development, thời hạn ticket 8 giờ; bật kiểm tra stamp ở mỗi request.
- Thêm antiforgery toàn cục, kiểm tra URL chuyển hướng nội bộ, không lưu cache trang tài khoản, không đưa mật khẩu trở lại HTML khi có lỗi.
- Thêm `AdminBootstrapper` dùng transaction Serializable; từ chối nếu đã có Admin hoặc email thuộc user hiện có, không tự nâng quyền hoặc đổi mật khẩu.
- Thêm hai script chuẩn bị/xem thông tin quản trị bằng User Secrets, không chứa mật khẩu trong source. Mật khẩu tạo bằng bộ sinh số ngẫu nhiên mật mã và truyền vào CLI qua stdin.
- Đã tạo tài khoản `lucvoAdmin@clb.local`, tên “Quản trị CLB”, theo email người dùng cung cấp. Mật khẩu chỉ được lưu cục bộ; không ghi vào nhật ký này. Đã đăng nhập bằng tài khoản này qua trình duyệt HTTPS rồi đăng xuất sau kiểm tra.

### Kiểm chứng

- Build Release bản cuối: 0 cảnh báo, 0 lỗi.
- Bộ integration test: **36/36 đạt**, 0 bỏ qua, gồm 21 kiểm thử dữ liệu trước đó và 15 kiểm thử HTTP/khởi tạo Admin (có theory và nhiều assertion nghiệp vụ).
- Các test HTTP đi qua MVC/Razor, cookie và SQL Server thật; không giả lập authentication. Mỗi nhóm kiểm thử dùng database ngẫu nhiên riêng và xác nhận đúng server/database trước khi tạo dữ liệu.
- Bao phủ: khách vào trang bảo vệ; Admin/Student đúng quyền; email không phân biệt hoa/thường; mật khẩu sai/email lạ/Disabled; khóa sau 5 lần sai và đăng nhập sau hết khóa; thu hồi cookie theo trạng thái/stamp; gỡ quyền Admin; CSRF; không đăng xuất qua GET; chặn ReturnUrl ngoài; RememberMe; validation mật khẩu trống; bootstrap không ghi đè/cấp thêm quyền.
- Trong quá trình xây test, EF tự thêm Application Name vào connection string. Đã sửa guard để so sánh đúng server và tên database, thay vì so sánh toàn bộ chuỗi. Không bỏ guard tách database.
- Với routing MVC hiện tại, GET `/Account/Logout` trả 404; kiểm thử xác nhận phiên vẫn còn và chỉ POST hợp lệ mới đăng xuất.
- `has-pending-model-changes`: không có thay đổi schema.
- GET trang đăng nhập qua HTTP 5026 và HTTPS 7013: HTTP 200. HTTPS được hệ thống tin cậy trên máy này; không bỏ qua kiểm tra chứng chỉ. `/health/ready` qua HTTPS: HTTP 200.
- Trình duyệt: đăng nhập quản trị thành công, xem đúng email/tên/quyền, điều hướng qua lại Admin và tài khoản, đăng xuất về trang chủ.
- Kiểm tra bố cục ở 360, 768 và 1366 px: không có nội dung tràn ngang. Sửa breakpoint menu sang `navbar-expand-lg` để đủ chỗ cho menu khi đã đăng nhập.
- Phát hiện `_ValidationScriptsPartial` thiếu jQuery do layout trước đó chỉ tải Bootstrap. Đã thêm jQuery trước hai thư viện validation, build lại và xác nhận lỗi để trống/email sai hiển thị tiếng Việt tại client; không phát sinh console error/warning mới sau bản sửa.
- Ảnh cục bộ: `.local/qa/login-desktop.png`, `login-mobile.png`, `admin-desktop.png`. Kết quả test: `.local/test-results/step-3.trx`. Các tệp này được loại khỏi Git.
- Kiểm tra NuGet gồm dependency gián tiếp sau khi thêm Mvc.Testing: cả hai project không có gói được nguồn hiện tại báo có lỗ hổng tại thời điểm kiểm tra. Cú pháp hai script PowerShell hợp lệ.
- Kiểm tra SQL cuối: tài khoản quản trị Active, số lần sai bằng 0, role Admin; không còn database kiểm thử. Đã đăng xuất trình duyệt, khôi phục viewport, đóng tab thử và dừng server. Chạy lại bằng lệnh HTTPS trong README.

### Trạng thái backlog và bước kế tiếp

| Mục | Trạng thái sau bước 3 |
|---|---|
| PB01 — đăng nhập, đăng xuất, đổi mật khẩu | Đăng nhập/đăng xuất đạt; còn đổi mật khẩu |
| PB02 — cấp và khóa tài khoản sinh viên | Chưa có giao diện; bước này mới kiểm thử cơ chế chặn Disabled |
| PB03 — quyền hệ thống và quyền theo CLB | Quyền hệ thống đã bảo vệ trang; quyền theo membership còn lại |
| PB06 — xem và sửa hồ sơ | Xem thông tin tài khoản đã có; chưa chỉnh sửa hồ sơ |

Trạng thái bước 3: đạt mục tiêu luồng đăng nhập và quyền hệ thống. Chưa coi cả Sprint 1 hoặc các PBI còn thiếu chức năng là hoàn thành. Bước nhỏ tiếp theo là đổi mật khẩu có xác minh mật khẩu hiện tại, rồi cấp/khóa tài khoản sinh viên để chuẩn bị quản lý CLB và chủ nhiệm.

## Bước 4 Đổi mật khẩu của tài khoản đang đăng nhập

Ngày thực hiện: 30/10/2026 (Asia/Bangkok). Phần còn lại của PB01 trong Sprint 1; giữ scope ở chức năng đổi mật khẩu, chưa thực hiện cấp/khóa tài khoản sinh viên.

### Mục tiêu và thay đổi

- Thêm đường dẫn **Tài khoản → Đổi mật khẩu**, form MVC với mật khẩu hiện tại, mật khẩu mới và xác nhận, nút Lưu/Hủy, hướng dẫn và lỗi tiếng Việt.
- ViewModel chỉ nhận ba trường mật khẩu. Controller lấy user từ phiên đăng nhập, không cho chọn tài khoản khác qua dữ liệu POST.
- Kiểm tra bắt buộc, tối thiểu 10 ký tự, xác nhận khớp; kiểm tra mật khẩu hiện tại bằng SignInManager có lockout; không cho dùng lại mật khẩu hiện tại.
- Đổi mật khẩu bằng UserManager để giữ cơ chế băm, kiểm tra chính sách Identity và cập nhật SecurityStamp. Bổ sung bản dịch lỗi mật khẩu/xung đột cập nhật.
- RefreshSignIn cấp cookie mới cho thiết bị hiện tại, giữ RememberMe. Cookie cũ ở thiết bị khác bị từ chối tại request tiếp theo.
- Khi sai hoặc dữ liệu không hợp lệ, không gửi lại mật khẩu trong HTML. Sau thành công redirect về tài khoản với thông báo hiển thị một lần.
- Nhập sai mật khẩu hiện tại 5 lần dùng cơ chế khóa 15 phút và đăng xuất phiên đang thao tác. CSRF, phân quyền và no-store kế thừa từ nền tảng bước 3.
- Cập nhật README, thiết kế xác thực và lời nhắc của script xem mật khẩu khởi tạo. Không lưu mật khẩu mới vào User Secrets. Không thêm package hoặc migration.

### Kiểm chứng

- Build Release ngoài sandbox: **0 cảnh báo, 0 lỗi**. Lần build trong sandbox kết thúc không có chẩn đoán; chạy lại ngoài sandbox thành công.
- **54/54 integration tests đạt**, 0 bỏ qua: giữ 36 kiểm thử trước và thêm 18 trường hợp đổi mật khẩu.
- Kiểm thử HTTP/Razor/cookie/SQL thật: khách không truy cập được; thiếu CSRF bị chặn; trang no-store; sai mật khẩu hiện tại; trường bắt buộc; xác nhận sai; từng điều kiện độ mạnh; dùng lại mật khẩu cũ; Admin/Student; RememberMe; tài khoản Disabled trước submit; UserId bị chèn không đổi được tài khoản khác.
- Kiểm thử hai thiết bị: sau đổi, thiết bị hiện tại tiếp tục sử dụng, thiết bị khác bị yêu cầu đăng nhập lại kể cả khi gửi form mở sẵn; mật khẩu cũ không còn đăng nhập được và mật khẩu mới dùng được. Kiểm tra PasswordHash/SecurityStamp thay đổi khi thành công, giữ nguyên trong các tình huống bị từ chối với hash hiện tại.
- Kiểm thử sai mật khẩu hiện tại 5 lần: có LockoutEnd, phiên thao tác bị đăng xuất, đăng nhập đúng trong thời gian khóa vẫn bị từ chối.
- Kết quả TRX: `.local/test-results/step-4.trx`; log cục bộ `.local/step-4-tests.log`.
- `has-pending-model-changes`: không có thay đổi schema.
- HTTPS trang đăng nhập và `/health/ready`: HTTP 200, dùng chứng chỉ được máy tin cậy.
- UI: đăng nhập tài khoản quản trị hiện có; liên kết từ Tài khoản mở đúng form; cả ba trường trống báo lỗi tiếng Việt tại client; nút Hủy quay lại trang tài khoản; đăng xuất về trang chủ.
- Kiểm tra bố cục 360, 768 và 1366 px: scrollWidth không vượt clientWidth; không thấy console error/warning trong tab kiểm tra.
- Ảnh cục bộ: `.local/qa/change-password-desktop.png`, `change-password-mobile.png`. Tài khoản quản trị thật chỉ được dùng để xem form/kiểm tra validation trống, không nhập hoặc thay mật khẩu mới; các ca thay mật khẩu thực sự chạy trên user giả trong database thử.
- Script `Show-LocalAdminLogin.ps1` đã kiểm tra cú pháp; không chạy script để in mật khẩu vào nhật ký.

### Trạng thái và bước tiếp theo

PB01 đã có đủ đăng nhập, đăng xuất, đổi mật khẩu và kiểm thử cục bộ; sẵn sàng để thành viên khác review và nhóm trưởng nghiệm thu theo DoD. Chưa tự đánh dấu toàn bộ Sprint 1 hoàn thành. PB02, quyền theo CLB của PB03 và chỉnh sửa hồ sơ PB06 vẫn còn việc.

Bước 5 sẽ triển khai cấp/khóa tài khoản sinh viên trong khu vực Admin: bắt buộc mã sinh viên, kiểm tra mã/email trùng, xác minh quyền server và bảo vệ các thao tác quản trị.

## Bước 5 Cấp, khóa và mở khóa tài khoản sinh viên

Ngày thực hiện: 30/10/2026 (Asia/Bangkok). Phạm vi PB02, Sprint 1. Tiếp nối phần xác thực; không chuyển sang quản lý CLB trong cùng bước.

### Thay đổi đã triển khai

- Thêm mục **Quản lý tài khoản sinh viên** trên Dashboard Admin; danh sách có tìm theo họ tên/mã/email, lọc trạng thái và phân trang 10 tài khoản.
- Form cấp tài khoản bắt buộc họ tên, mã SV, email, mật khẩu ban đầu và xác nhận. Chuẩn hóa dữ liệu, giữ số 0 đầu mã SV; lớp/khoa tùy chọn. Identity băm mật khẩu, áp dụng chính sách đã có; mật khẩu không trả lại HTML khi lỗi.
- Chỉ tạo Student Active; chèn thêm Id/Role/Status không được áp dụng. Tài khoản Admin, kể cả hai role, không xuất hiện hoặc bị thay trạng thái qua form/POST quản lý sinh viên.
- Khóa/mở có trang xác nhận và lý do. Lưu Status với SecurityStamp mới; ConcurrencyStamp phát hiện form cũ. Mở khóa không khôi phục cookie cũ, không xóa lockout do nhập sai mật khẩu.
- Thêm `StudentAccountService` giữ transaction cho tạo user + role + audit và đổi trạng thái + stamp + audit. Bắt unique violation từ hai yêu cầu cấp trùng; rollback khi không ghi được audit.
- Migration `20261030030529_AddAccountAuditLogs` chỉ thêm bảng nhật ký và chỉ mục. Không sửa migration đã áp dụng, không thay dữ liệu tài khoản/CLB/membership cũ.
- Layout Bootstrap dùng chung; không thêm template Admin. Hướng dẫn sử dụng, quy tắc dữ liệu, xác thực và nghiệm thu được cập nhật trong README và docs.

### Kết quả kiểm tra thực tế

- **79/79 tests đạt**, 0 lỗi, 0 bỏ qua: 54 ca cũ + 25 ca quản lý sinh viên. Bộ test tạo database riêng trên SQL Server và tự dọn. Kết quả `.local/test-results/step-5.trx`; log `.local/step-5-tests.log`.
- Build Release cuối cùng **0 warning, 0 error**, log `.local/step-5-build.log`. Hai cảnh báo cách dùng Assert.Single ở lần test đầu đã sửa; không thay đổi hành vi kiểm thử.
- Kiểm thử mới xác minh quyền GET/POST, CSRF/no-store, chuẩn hóa/giữ số 0, chống nâng quyền, password hash, validation, không echo mật khẩu, trùng khác hoa thường và hai yêu cầu đồng thời, thu hồi phiên đang dùng/phiên chưa truy cập, bảo toàn lockout, form cũ, bảo vệ Admin, rollback khi audit lỗi, tìm/lọc/phân trang và encode HTML.
- Migration đã áp dụng lên `StudentClubManagement`; kiểm tra model không còn thay đổi chưa có migration. Readiness HTTPS trả **200 Healthy**.
- Database ứng dụng sau kiểm tra vẫn có **1 Admin, 0 Student, 0 audit tài khoản**; Admin Active, AccessFailedCount = 0. Không tạo dữ liệu sinh viên giả vào database ứng dụng; không đổi mật khẩu Admin.
- Browser QA đăng nhập Admin hiện có; mở mục quản lý, bộ lọc trạng thái, form cấp, validation trống tại client, nút Hủy, menu mobile và đăng xuất. Viewport 1366×1000 và 390×844; form/danh sách trống không tràn ngang; không thấy console error/warning.
- Lưu ảnh `.local/qa/students-create-desktop.png`, `students-create-mobile.png`, `students-list-desktop.png`, `students-list-mobile.png`. Các luồng tạo/khóa/mở với dữ liệu được xác minh bằng HTTP integration tests; chưa thực hiện trên sinh viên thật qua browser.
- Sau kiểm tra đã đăng xuất, đóng tab kiểm tra và dừng tiến trình preview do trợ lý chạy. Khi xem lại, chạy ứng dụng từ IDE hoặc lệnh trong README.

### Review và việc còn lại

PB02 đã có chức năng và kiểm thử cục bộ, **chờ thành viên khác review/nhóm trưởng nghiệm thu**, chưa tuyên bố Sprint 1 Done. Checklist nằm trong `docs/student-accounts.md`. Chưa có bắt buộc đổi mật khẩu lần đầu, email gửi thông tin, reset mật khẩu, màn hình tra cứu audit hoặc sửa hồ sơ cá nhân.

Bước 6 dự kiến triển khai tạo CLB cùng chủ nhiệm (PB05) và quyền quản lý theo CLB (PB03). Cần giải quyết quan hệ giữa khóa tài khoản và chuyển giao người đang phụ trách CLB trong bước này; không coi membership Active là đủ khi tài khoản người đó Disabled. PB06 chỉnh sửa hồ sơ vẫn còn việc.

## Bước 6 Tạo CLB, sửa hồ sơ và chuyển chủ nhiệm

Ngày thực hiện: 30/10/2026 (Asia/Bangkok). Mục tiêu vòng nhỏ: tạo CLB có chủ nhiệm ngay, người quản lý chỉ sửa đúng CLB, chuyển giao giữ nguyên tư cách thành viên và thu hồi quyền người cũ.

### Quyết định và thay đổi

- Người dùng đã chọn: **chỉ chuyển chủ nhiệm cho thành viên đang hoạt động của chính CLB**, không kết nạp người ngoài trong thao tác chuyển. Đây là ràng buộc áp dụng tại server, không chỉ lọc danh sách UI.
- Admin có mục **Quản lý câu lạc bộ** trên Dashboard; danh sách tìm theo tên/mã, phân trang. Form tạo nhận mã/tên/lĩnh vực/giới thiệu và mã SV chủ nhiệm đầu tiên.
- Tạo CLB cùng membership Manager Active và audit trong một transaction. Chỉ chọn tài khoản Student Active, loại Admin kể cả dual-role. Mã CLB duy nhất, chuẩn hóa viết hoa và giữ cố định sau tạo.
- Chủ nhiệm có **CLB tôi quản lý**, sửa tên/lĩnh vực/giới thiệu. ClubAccessService đọc quyền từ dữ liệu hiện tại; Admin sửa được các CLB. POST sửa kiểm tra quyền lại sau khóa CLB.
- Admin chuyển chủ nhiệm trong các thành viên đủ điều kiện, bắt buộc lý do. Hạ vai trò cũ và lưu trước, nâng vai trò mới và ghi audit sau, cùng transaction. RowVersion CLB đổi để form cũ không ghi đè; người cũ bị từ chối ở request tiếp theo.
- Bổ sung khóa SQL theo tài khoản và CLB, thứ tự tài khoản người nhận trước rồi CLB. Khóa tài khoản sinh viên dùng chung khóa này và từ chối nếu còn bất kỳ membership chủ nhiệm đang mở, tránh race với phân công/chuyển giao.
- Audit ghi tạo/sửa/chuyển với ID liên quan, người thực hiện và lý do khi chuyển. Phần giới thiệu lưu hash trước/sau trong audit, không lưu toàn bộ văn bản; đây không phải lịch sử phục hồi nội dung.
- Dùng layout Bootstrap hiện có; không có template quản trị mới. Không đổi schema hoặc migration.

### Kết quả thực tế

- **108/108 kiểm thử đạt, 0 lỗi, 0 bỏ qua**: thêm 29 ca `ClubManagementTests` bên cạnh 79 ca có sẵn. Build Release cuối **0 warning, 0 error**. Kiểm tra model không có thay đổi thiếu migration.
- Đã thử quyền GET/POST, CSRF/no-store, mã trùng khác hoa thường và hai request cùng mã, tạo nguyên tử, RowVersion, chèn trường không được phép, A truy cập CLB B, quyền cũ sau chuyển, người nhận không đủ điều kiện và không tự tạo membership.
- Đã thử khóa người phụ trách nhiều CLB, hai lần chuyển đồng thời, tạo/chuyển đồng thời với khóa tài khoản; kết quả không để chủ nhiệm có tài khoản Disabled. Cưỡng bức lỗi ghi audit trong DB thử xác minh rollback cả tạo/sửa/chuyển, gồm bước hạ vai trò cũ đã SaveChanges.
- Test SQL Server/HTTP/cookie/Razor thật trên database riêng có kiểm tra quyền sở hữu; không thay dữ liệu ứng dụng. TRX `.local/test-results/step-6.trx`, log `.local/step-6-tests.log` và `.local/step-6-build.log`.
- HTTPS readiness **200 Healthy**. Database ứng dụng vẫn có 1 Admin, 0 CLB, 0 membership; Admin Active, AccessFailedCount 0. Không thay mật khẩu hoặc tạo CLB/sinh viên giả trong database ứng dụng.
- Browser QA: Dashboard → danh sách → form tạo; validation trống tại client, nút Hủy, menu mobile, đăng xuất. Form và danh sách trống ở 1366×1000/390×844 không tràn ngang; không thấy console warning/error. Ảnh `.local/qa/clubs-create-desktop.png`, `clubs-create-mobile.png`, `clubs-list-desktop.png`, `clubs-list-mobile.png`.
- Luồng có dữ liệu và chuyển giao được kiểm thử bằng integration tests; chưa thao tác tạo/chuyển CLB thật trên browser. Đã đăng xuất, đóng tab kiểm tra và dừng server preview do trợ lý chạy.

### Review và bước tiếp theo

Phần triển khai bước 6 sẵn sàng để thành viên khác review và nhóm trưởng nghiệm thu theo `docs/club-management.md`; không ghi nhận Review/UAT của nhóm đã diễn ra. **Chưa hoàn tất toàn bộ PB05 hoặc Sprint 1:** còn tạm dừng/lưu trữ và điều kiện đơn chờ/hoạt động tương lai, chưa có module để xử lý các phụ thuộc đó.

Với lựa chọn chỉ chuyển trong thành viên Active, CLB mới chỉ có một chủ nhiệm sẽ chưa có người nhận hợp lệ. Quy trình gia nhập/duyệt đơn thuộc Sprint 2; không thêm đường tắt kết nạp để vượt quy tắc người dùng đã chọn.

Bước nhỏ tiếp theo dự kiến PB04: danh mục CLB công khai, tìm/lọc và xem chi tiết, không lộ danh sách thành viên. PB06 sửa hồ sơ cá nhân còn trong Sprint 1; PB03 sẽ mở rộng quyền theo sự kiện/thông báo ở Sprint sau.

## Bước 7 Danh mục và chi tiết CLB công khai

Ngày thực hiện: 30/10/2026 (Asia/Bangkok). Phạm vi PB04, Sprint 1.

### Thay đổi

- Thêm `/Clubs`: tìm tên/mã, lọc lĩnh vực, phân trang 6 CLB, giữ bộ lọc khi đổi trang; trang chi tiết giới thiệu đầy đủ và trạng thái. Thêm điều hướng từ menu, trang chủ và CLB tôi quản lý.
- Active/Suspended công khai, Archived ẩn cả truy cập trực tiếp. Schema chưa có cờ xuất bản nên CLB Active mới tạo xuất hiện ngay; nhắc phạm vi nội dung công khai trên form quản lý. Không đưa thành viên/thông tin Identity vào ViewModel hoặc HTML công khai.
- Chuẩn hóa bộ lọc, xác minh độ dài/kiểu số, giới hạn trang hợp lệ; tên/mã tìm không phân biệt hoa thường nhưng có phân biệt dấu. Encode nội dung, phản hồi no-store; chỉ đọc EF Core, không thay schema hoặc dữ liệu ứng dụng.
- Giữ layout Bootstrap chung; có trạng thái trống, dữ liệu giới thiệu/lĩnh vực chưa cập nhật và 404. Chưa triển khai đăng ký gia nhập, không thêm nút thao tác giả.

### Kiểm tra

- **126/126 tests đạt**, 0 lỗi, 0 bỏ qua: 108 ca cũ và 18 ca `PublicClubTests`. Kết quả `.local/test-results/step-7.trx`; log `.local/step-7-tests.log`.
- Build Release cuối **0 warning, 0 error** (`.local/step-7-build.log`), sau sửa một cảnh báo Assert.Single trong test. EF xác nhận không có thay đổi model thiếu migration.
- Các ca mới kiểm tra tìm/lọc/phân trang, tham số sai/cực trị, dữ liệu nullable, Archived, no-store, chuyển trạng thái, HTML encode, chuỗi tìm kiếm đặc biệt, dữ liệu cá nhân không lộ cho khách và Admin.
- Browser QA trên database tạm riêng với 8 CLB giả: danh mục 7 CLB công khai, 6 + 1 trên hai trang; tìm `lập TRÌNH` ra đúng 1, Nghệ thuật ra 2. Xem chi tiết giữ xuống dòng và nhãn CLB tạm dừng; mở menu điện thoại. Viewport 1366×1000, 1024×900, 390×844; không phát hiện tràn ngang theo DOM, không thấy console warning/error. Readiness preview 200 Healthy.
- Ảnh `.local/qa/public-clubs-desktop.png`, `public-clubs-mobile.png`, `public-club-details-desktop.png`, `public-club-details-mobile.png` là dữ liệu minh họa QA. Đã đóng tab, khôi phục viewport, dừng server tại cổng 5047; chỉ xóa database QA do phiên này tạo sau xác nhận đúng tên. DB_ID sau dọn trả NULL. Database ứng dụng vẫn **1 user, 0 CLB, 0 membership**.

### Review và bước tiếp theo

PB04 sẵn sàng để nhóm review và nghiệm thu theo `docs/public-clubs.md`; không coi kiểm thử tự động là Sprint Review/UAT. Bước tiếp theo PB06 sửa hồ sơ cá nhân. PB05 còn thao tác tạm dừng/lưu trữ và kiểm tra phụ thuộc đơn chờ/sự kiện; chưa đánh dấu toàn bộ Sprint 1 Done.

## Hỗ trợ đọc thông tin quản trị cục bộ — 31/10/2026

- Người dùng chạy CheckOnly bằng Windows PowerShell 5.1: User Secrets 168 byte, chỉ có `ConnectionStrings:DefaultConnection`, không có email/mật khẩu bootstrap. Môi trường công cụ đọc cùng đường dẫn nhưng nhận bản 351 byte có đủ các trường. Chưa xác định cơ chế khiến nội dung khác nhau; không kết luận tài khoản trong database bị mất.
- Script xem đăng nhập bổ sung đọc UTF-8, hỗ trợ JSON phẳng/lồng, chế độ CheckOnly báo metadata và tên khóa, không in giá trị bí mật.
- Chuẩn bị bản khôi phục PSCredential mã hóa bằng Windows DPAPI trong `.local`, đã xác nhận Git bỏ qua. Script `Restore-LocalAdminSecrets.ps1` nhập bản này, chỉ thêm hai trường còn thiếu và giữ các trường JSON khác; từ chối ghi đè cấu hình Admin có sẵn, không gọi database hoặc bootstrap.
- Kiểm tra trên Windows PowerShell 5.1 với cấu hình và credential giả: khôi phục thành công, giữ connection string/trường lồng khác, từ chối lần khôi phục thứ hai và không thay tệp. Sửa truyền null cho File.Replace tương thích PowerShell 5.1 sau lần thử đầu thất bại. Bản mã hóa thật giải mã được trong môi trường kiểm tra nhưng không in mật khẩu ra log.
- Chờ người dùng chạy script trong terminal VS Code rồi xác nhận đăng nhập. Chưa khôi phục thay người dùng vào bản 168 byte, chưa thay mật khẩu, chưa đánh dấu sự cố được giải quyết hoàn toàn hoặc bắt đầu PB06. Dọn bản khôi phục khi người dùng xác nhận hoàn tất.

## Bước 8 Chỉnh sửa hồ sơ cá nhân

Hoàn tất kiểm tra cục bộ: 02/11/2026 (Asia/Bangkok), PB06 thuộc Sprint 1. Người dùng đã xác nhận đăng nhập Admin thành công sau khôi phục; bản `.local/admin-login-recovery.clixml` đã được dọn ở lượt trước và kiểm tra lại không còn. Không thay mật khẩu người dùng.

### Triển khai

- Thêm **Tài khoản → Chỉnh sửa hồ sơ**, hiển thị thêm điện thoại/lớp/khoa ở trang tài khoản. Cho Student và Admin sửa họ tên, điện thoại, lớp, khoa của chính mình. Email, mã SV, quyền và trạng thái không được cập nhật qua POST này.
- ViewModel/DataAnnotations kiểm tra độ dài và định dạng; service chuẩn hóa chuỗi, xác minh dữ liệu, lấy user theo principal tại controller, phối hợp khóa tài khoản SQL và nạp lại entity trước kiểm tra phiên bản/trạng thái. Form cũ trả 409 và hướng dẫn tải lại. Thay đổi số điện thoại xóa cờ xác minh, không đổi credentials hoặc SecurityStamp.
- Hồ sơ và audit PersonalProfileUpdated nằm cùng transaction. Audit chỉ ghi danh sách trường thay đổi, không lưu thông tin liên hệ. Không có thay đổi thì không ghi/audit thêm. Dùng Bootstrap chung, không đổi schema, không thêm migration hoặc thư viện.

### Kết quả

- **146/146 tests đạt**, thêm 20 ca PersonalProfileTests. 0 lỗi, 0 bỏ qua. TRX `.local/test-results/step-8.trx`, log `.local/step-8-tests.log`.
- Kiểm tra quyền GET/POST, CSRF/no-store, lưu/đọc lại Student/Admin, validation, overposting, người khác không bị thay đổi, credentials và phiên khác giữ nguyên, stale form, hai lần lưu đồng thời, dữ liệu nullable, HTML encode, không-op, Disabled và EF tracked entity cũ, rollback khi audit lỗi.
- Build Release cuối **0 warning, 0 error** (`.local/step-8-build.log`), sau sửa hai cảnh báo Assert.Single trong test. EF xác nhận không có model change thiếu migration.
- Browser QA tại cổng 5048 dùng database mới và tài khoản giả: validation họ tên/điện thoại, lưu bốn trường, mở lại, Hủy bỏ thay đổi, menu mobile. Xem giao diện 1366×1000 và 390×844; DOM không có tràn ngang mobile, không thấy console warning/error. Ảnh `.local/qa/profile-edit-desktop.png`, `profile-edit-mobile.png`, `profile-saved-desktop.png` là dữ liệu QA.
- Đã dừng preview do trợ lý chạy, đóng tab tạm, reset viewport; chỉ xóa database ProfileQA do phiên này tạo sau kiểm tra đúng tên. DB_ID sau dọn trả NULL. Database ứng dụng có 0 audit PersonalProfileUpdated tại thời điểm kiểm tra, không dùng tài khoản Admin thật để lưu thử.

### Review và vòng tiếp theo

PB06 sẵn sàng cho review và nghiệm thu theo `docs/personal-profile.md`, chưa ghi nhận nhóm đã Review/UAT. Bước tiếp theo dự kiến PB07: gửi, rút và theo dõi đơn gia nhập; sau đó PB08 duyệt đơn. Trước khi chốt Sprint 1, nhóm cần cập nhật các phần còn lại của PB05 và phụ thuộc đơn chờ/sự kiện, không tự đánh dấu toàn Sprint Done.

## Bước 9 Điều chỉnh giao diện VinhUni và khu vực quản trị

Ngày hoàn tất kiểm tra cục bộ: 02/11/2026 (Asia/Bangkok). Theo yêu cầu nhóm trưởng, ưu tiên giao diện trước PB07.

### Thay đổi

- Trang chủ VinhUni Clubs dùng logo và bốn ảnh hoạt động lấy từ các kênh chính thức Đại học Vinh; nội dung giới thiệu được tóm tắt, có link nguồn. Lưu ảnh cục bộ, có alt, kích thước, lazy loading cho ảnh dưới màn hình đầu. Cập nhật header/footer và màu sắc chung cho các trang hiện có.
- Tách `_AdminLayout`: sidebar tối, thu gọn desktop, offcanvas mobile, menu đánh dấu trang hiện tại, Đổi mật khẩu trực tiếp và liên kết quay lại website công khai. Không thêm menu chức năng chưa triển khai.
- Dashboard đọc số liệu thật từ EF Core: toàn bộ CLB, CLB Active, tài khoản Student không gồm Admin, lượt Membership Active chưa kết thúc. Hiển thị 5 CLB mới tạo, 6 thao tác gần đây, lối tắt. Audit chỉ lộ tên hành động và giờ Việt Nam, không hiển thị dữ liệu chi tiết nhạy cảm.
- Giữ MVC/Bootstrap hiện có; không cài package mới, không thay schema/migration và không tạo dữ liệu minh họa trong database ứng dụng. Nguồn ảnh, định nghĩa số liệu và hướng dẫn review: `docs/interface-redesign.md`.

### Kiểm tra

- **147/147 tests đạt**, 0 lỗi, 0 bỏ qua. Thêm 1 ca DashboardTests kiểm tra các trạng thái CLB, sinh viên có/không có role Admin, tài khoản khóa, một sinh viên có nhiều lượt thành viên, lượt đã kết thúc, no-store và không lộ audit payload. Giữ 146 ca kiểm thử nghiệp vụ trước đó. TRX `.local/test-results/ui-redesign.trx`, log `.local/ui-redesign-tests.log`.
- Build Release cuối **0 warning, 0 error** (`.local/ui-redesign-build.log`). Sau bộ tests chỉ bổ sung aria-label cho liên kết tài khoản bị ẩn chữ ở mobile; đã biên dịch lại Razor thành công.
- Browser QA trên database riêng với tài khoản/CLB giả: trang chủ, ảnh, đăng nhập/đăng xuất, dashboard, thu gọn sidebar 240→78px, mở trang đổi mật khẩu, menu điện thoại, trang sinh viên, quản lý CLB và danh mục công khai. Kiểm tra 1366×1000 và 390×844. Không phát hiện tràn ngang toàn trang hoặc ảnh lỗi; console không có warning/error. Escape đóng offcanvas; cần đợi chuyển động đóng hoàn tất trước thao tác mở lại.
- HTTP readiness của preview trả 200 Healthy. Dữ liệu QA nhập lần đầu bị sai code page tiếng Việt của sqlcmd; đã sửa bản ghi QA bằng file UTF-8 và `-f 65001` trước chụp minh chứng, không phải lỗi hiển thị dữ liệu ứng dụng.
- Ảnh: `.local/qa/vinh-home-preview.png`, `vinh-home-desktop.png`, `vinh-home-mobile.png`, `vinh-admin-desktop.png`, `vinh-admin-mobile.png`, `vinh-admin-mobile-menu.png`. Dashboard trong ảnh là **dữ liệu minh họa QA**, không phải dữ liệu thật của trường.
- Đã đăng xuất tài khoản QA, đóng tab riêng, reset viewport, dừng server preview cổng 5049 và xóa đúng database `StudentClubManagement_UiQa_<guid>` do phiên này tạo sau kiểm tra tên. DB_ID sau dọn trả NULL. Không dùng tài khoản quản trị thật để lưu thử.

### Review

Giao diện sẵn sàng cho nhóm trưởng review theo `docs/interface-redesign.md`. Tin giới thiệu hiện là nội dung tĩnh có dẫn nguồn; chức năng quản lý sự kiện/thông báo thuộc các bước nghiệp vụ tiếp theo. PB07/PB08 và việc còn lại của PB05 giữ nguyên phạm vi, chưa đánh dấu Sprint Done hoặc UAT được nhóm chấp thuận.

## Dữ liệu mẫu cho danh mục CLB — 02/11/2026

Theo yêu cầu nhóm trưởng, đã thêm 6 CLB mẫu vào database ứng dụng cục bộ `StudentClubManagement` trên `.\SQLEXPRESS` (xác minh khớp User Secrets). Trước khi thêm: 0 CLB, 0 tài khoản Student, 1 Admin hoạt động.

| Mã | Tên | Lĩnh vực |
|---|---|---|
| DEMO-CODE | Câu lạc bộ Lập trình | Học thuật |
| DEMO-ENG | Câu lạc bộ Tiếng Anh | Ngoại ngữ |
| DEMO-MUSIC | Câu lạc bộ Âm nhạc | Nghệ thuật |
| DEMO-PHOTO | Câu lạc bộ Nhiếp ảnh | Nghệ thuật |
| DEMO-SPORT | Câu lạc bộ Cầu lông | Thể thao |
| DEMO-VOL | Câu lạc bộ Tình nguyện | Cộng đồng |

- Mỗi CLB có mô tả riêng, Status Active, một Membership Manager Active và hồ sơ Student mẫu tương ứng. Hồ sơ dùng email `example.test`, mã sinh viên theo mã DEMO, họ tên ghi rõ chủ nhiệm mẫu; không có PasswordHash, external login hoặc token, không cấp mật khẩu mặc định. Vì vậy số tài khoản sinh viên và lượt thành viên trong dashboard tăng 6.
- Mô tả ghi rõ dữ liệu minh họa phục vụ đồ án, không xác nhận đây là CLB chính thức của trường. Không chép dữ liệu QA tạm ở bước trước vào ứng dụng.
- Script tái sử dụng `scripts/Seed-DemoClubs.sql` chỉ chạy thủ công, giới hạn đúng tên database, kiểm tra Admin dự án và role Student, ghi trong transaction có application lock, không ghi đè dữ liệu đã có. Mã CLB đã tồn tại được bỏ qua; định danh hồ sơ xung đột làm rollback. Không chỉnh tài khoản quản trị.
- Thêm 12 audit StudentCreated/ClubCreated với lý do dữ liệu mẫu và dấu IsDemo. Chạy bằng sqlcmd `-f 65001 -b` để giữ tiếng Việt và báo lỗi đúng.
- Xác nhận lần đầu CreatedClubs=6; lần hai CreatedClubs=0, ExistingCodesSkipped=6. Sau kiểm tra có 6 CLB Active, 6 chủ nhiệm Active, 6 hồ sơ không có mật khẩu và 12 audit, không sinh trùng.
- Browser tại website thực `http://localhost:5026/Clubs`: danh mục hiển thị 6; tìm `lập TRÌNH` có 1 kết quả; lọc Nghệ thuật có 2; trang chi tiết Âm nhạc hiển thị đủ nội dung và xuống dòng. Không thay mã ứng dụng hoặc schema nên không cần build/migration cho bước nạp dữ liệu này.
