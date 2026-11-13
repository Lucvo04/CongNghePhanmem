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

## Rút gọn bảng đăng nhập/phân quyền — 02/11/2026

Theo yêu cầu nhóm trưởng: giảm số bảng và bỏ tiền tố AspNet để dễ học, trình bày đồ án.

- Schema cuối: Users, Roles, UserRoles, Clubs, Memberships, AuditLogs; thêm bảng kỹ thuật __EFMigrationsHistory. Giữ quan hệ nhiều-nhiều UserRoles và phân quyền theo từng CLB ở Memberships.
- Thêm migration `20261102114946_SimplifyIdentityTables`: đổi tên ba bảng chính, đổi tên PK/FK/index liên quan, bỏ bốn bảng claims/login/token mở rộng. Guard có khóa trong transaction từ chối khi bảng mở rộng có dữ liệu hoặc người dùng đang bật 2FA. Down tạo lại bảng mở rộng rỗng và đổi tên trở lại; không sửa các migration lịch sử.
- ApplicationDbContext ánh xạ rõ sáu thực thể; giữ nguyên ràng buộc Identity và nghiệp vụ. CoreUserStore/CoreRoleStore chỉ công bố các interface được hỗ trợ, ủy quyền lưu trữ cho EF store Microsoft. Không công bố claims tùy chỉnh, external login, token/2FA và không đăng ký token providers chưa dùng. PasswordHasher, cookie, role claims tạo khi đăng nhập, SecurityStamp, lockout và các service quyền giữ cơ chế Identity. ClubSignInManager từ chối cờ 2FA không được hỗ trợ, tránh bỏ qua xác thực bổ sung.
- Cập nhật Seed-DemoClubs.sql sang tên bảng mới; thêm Inspect-Database.sql chỉ đọc. Hướng dẫn hiện tại ở docs/simple-database.md; README, authentication.md và data-model.md trỏ đến cấu trúc mới. Các mục trước trong nhật ký là lịch sử theo từng bước, không phải hướng dẫn schema hiện tại.

Kiểm tra trước áp dụng: bốn bảng mở rộng có 0 bản ghi, không tài khoản bật 2FA. **150/150 tests đạt**, 0 bỏ qua; thêm ba ca CompactIdentityTests kiểm tra khả năng store, claims role, schema sáu bảng; từ chối 2FA; Up/Down giữ tài khoản/hash/stamp/role/CLB/membership/audit, và chặn riêng từng loại dữ liệu mở rộng. Model không có thay đổi thiếu migration. Build Release cuối 0 warning, 0 error.

Database thật đã BACKUP COPY_ONLY CHECKSUM và RESTORE VERIFYONLY thành công, metadata tại `.local/identity-backup.json`; giữ bản sao trong thư mục Backup SQL Server. Website không chạy lúc đổi schema. Áp dụng migration thành công và so sánh SHA-256 của toàn bộ sáu bảng trước/sau khớp hoàn toàn: 8 Users, 2 Roles, 8 UserRoles, 6 Clubs, 6 Memberships, 13 AuditLogs. Không tạo lại Admin, không đổi password hash, không mất sáu CLB mẫu.

Smoke check trên ứng dụng sau migration: readiness 200 Healthy, trang đăng nhập 200. Script CLB mẫu chạy lại báo 0 tạo mới/6 bỏ qua. Đã dừng server kiểm tra do trợ lý mở ở cổng 5026. Không đăng nhập hoặc đổi mật khẩu Admin thật. Chưa thao tác Refresh trong cửa sổ SSMS vì yêu cầu truy cập ứng dụng hết thời gian chờ; người dùng cần Tables → Refresh để nhìn thấy tên mới.

Bằng chứng: `.local/test-results/compact-identity.trx`, `.local/compact-identity-tests.log`, `.local/compact-identity-build.log`, `.local/identity-live-migration.log`, `.local/identity-before.txt`, `.local/identity-after.txt`. Thay đổi schema này không mở rộng phạm vi PB07/PB08, không tự đánh dấu Sprint Review/UAT hoàn tất.

## PB07 — Đăng ký gia nhập CLB, hoàn thiện 03/11/2026

- Thêm JoinRequest, mapping và migration `20261102122134_AddJoinRequests`. Chỉ thêm một bảng nghiệp vụ, giữ ba bảng đăng nhập Users/Roles/UserRoles. Pending = 1, Withdrawn = 2; FK Restrict, RowVersion, constraint thời gian và unique filtered index chỉ một đơn Pending cho mỗi UserId/ClubId.
- JoinRequestService kiểm tra Student Active không có Admin, CLB Active, không có đợt Membership mở và không có đơn chờ. Gửi/rút dùng transaction, khóa tài khoản rồi CLB, ghi audit cùng giao dịch. Không tạo Membership khi gửi đơn; không xóa lịch sử khi rút. Audit không sao chép lý do cá nhân.
- Controller chỉ lấy người gửi từ tài khoản đăng nhập; lọc mọi truy vấn đơn theo chủ sở hữu. POST chống CSRF, chặn truy cập chéo 404, trả 409 khi xung đột, NoStore. Danh sách 10 đơn/trang; lý do được Razor mã hóa.
- Thêm các view Create/Index/Withdraw, menu Đơn của tôi cho Student và nút đăng ký ở chi tiết CLB. Khách đăng nhập xong quay về form đúng CLB. Admin không dùng luồng xin gia nhập. PB08 duyệt đơn chưa triển khai.
- 174/174 integration tests đạt, 0 bỏ qua; gồm 24 ca mới PB07, test đồng thời và rollback lỗi audit. Model không có thay đổi thiếu migration; build Release cuối 0 warning/0 error.
- Browser dùng database riêng: kiểm tra khách → đăng nhập → gửi đơn, validation thiếu lý do, rút đơn, nộp lại và giữ cả hai lịch sử. Kiểm tra màn hình hẹp không tràn ngang, không console warning/error. Đã đóng tab, reset viewport, dừng đúng tiến trình thử và xóa đúng database QA có GUID; không nạp đơn thử vào database đồ án.
- Database StudentClubManagement đã BACKUP COPY_ONLY CHECKSUM, RESTORE VERIFYONLY thành công trước migration; đường dẫn bản sao ở `.local/join-backup.json`. Fingerprint sáu bảng cũ trước/sau khớp hoàn toàn; JoinRequests mới có 0 dòng. Tài khoản, vai trò, sáu CLB mẫu và dữ liệu thành viên giữ nguyên.
- Smoke trên database thật: `/health/ready` 200 Healthy, `/Clubs` 200, `/JoinRequests` khi chưa đăng nhập chuyển tới form đăng nhập. Đã dừng server smoke do trợ lý mở. Người dùng chạy lại web theo README.
- Tài liệu sử dụng và giải thích MVC/SQL: `docs/join-requests.md`; cập nhật README, simple-database.md, data-model.md, Inspect-Database.sql. PB07 chờ review/nhóm trưởng nghiệm thu; chưa tự đánh dấu cả Sprint hoàn thành.

Bằng chứng: `.local/test-results/join-requests.trx`, `.local/join-final-build.log`, `.local/join-model-check.log`, `.local/join-live-migration.log`, `.local/join-before.txt`, `.local/join-after.txt`, `.local/join-live-smoke-checks.json`, ảnh `.local/qa/join-requests-desktop.png` và `join-requests-mobile.png`.

## PB08 — Xét duyệt đơn gia nhập, 03/11/2026

- Thêm JoinRequestReviewController/Service/ViewModels và hai view danh sách/xử lý. Từ CLB tôi quản lý hoặc Quản lý câu lạc bộ của Admin có liên kết Đơn gia nhập. Lọc theo trạng thái, phân trang 10 đơn; mặc định Pending, đơn cũ trước. Admin dùng layout quản trị hiện có.
- Giữ ClubAccessService: chủ nhiệm đúng CLB hoặc Admin hoạt động có quyền; không tự xét duyệt. Kiểm tra lại quyền sau khóa để người bị chuyển giao/thu hồi không dùng form cũ. Người ngoài phạm vi nhận 404, POST chống CSRF và trang đặt NoStore.
- Approved = 3 tạo Membership Member Active; Rejected = 4 bắt buộc lý do 1–500 ký tự. Chỉ Pending và RowVersion hiện hành được xử lý. Tài khoản người gửi/CLB/membership được kiểm tra lại; Suspended không chấp nhận, Archived chỉ xem. Sinh viên thấy kết quả, người/thời điểm xử lý và lý do từ chối tại Đơn của tôi, không còn nút rút đơn đã quyết định.
- Transaction gồm thêm Membership (nếu chấp nhận), kết thúc đơn và audit. Khóa tài khoản theo thứ tự cố định trước khóa CLB, phối hợp gửi/rút/chuyển chủ nhiệm. Chống duyệt/duyệt và duyệt/rút đồng thời, rollback tất cả nếu audit lỗi; không sao chép lý do riêng tư vào audit.
- Migration `20261102233544_AddJoinRequestDecisions` chỉ thêm DecidedById, DecisionReason, MembershipId vào JoinRequests, mở rộng constraint/index/FK; không thêm bảng. Down chặn nếu đã có quyết định, bảo vệ lịch sử. Không sửa migration cũ.
- Kiểm thử: 194/194 full suite đạt; sau hai ca bổ sung và sửa warning, 25/25 ca xét duyệt + CompactIdentity đạt. Có 196 ca khác nhau đã đạt, trong đó 22 ca PB08. Build cuối 0 warning/error, model khớp migration.
- Browser dùng dữ liệu QA: chấp nhận, lọc lịch sử, bắt lỗi thiếu lý do từ chối, từ chối hợp lệ, sinh viên thấy hai kết quả. Kiểm tra kết quả ở desktop/mobile, không tràn ngang hoặc lỗi console. Đã đóng tab/reset viewport, dừng server và dọn đúng database QA.
- Database thật được sao lưu COPY_ONLY CHECKSUM và RESTORE VERIFYONLY trước migration. So sánh SHA-256 dữ liệu cũ của bảy bảng trước/sau khớp; không tạo tài khoản, đơn hoặc thành viên thử vào database đồ án. Metadata backup `.local/review-backup.json`. Smoke readiness 200 Healthy, danh mục 200, xét duyệt yêu cầu đăng nhập; đã dừng server smoke.
- Cập nhật README, tài liệu dữ liệu/PB07 và Inspect-Database.sql; hướng dẫn mới `docs/join-request-review.md`. Chờ nhóm trưởng nghiệm thu PB08; bước kế tiếp PB09, chưa triển khai trong lượt này.

Bằng chứng: `.local/test-results/join-review.trx`, `.local/test-results/join-review-final.trx`, `.local/review-final-build.log`, `.local/review-model-check.log`, `.local/review-live-migration.log`, `.local/review-before.txt`, `.local/review-after.txt`, `.local/review-live-smoke-checks.json`, `.local/qa/join-review-results.png`.

## PB09 — Danh sách và trạng thái thành viên, 04/11/2026

- Thêm MembershipsController, MembershipService, ViewModels và ba view Index/Details/ChangeStatus. Chủ nhiệm/Admin vào Thành viên từ danh sách CLB; sinh viên vào Tài khoản → CLB của tôi. Có tìm tên/mã SV, lọc current/all/Active/Suspended/Left, phân trang 10 đợt hoặc 10 lịch sử; không cho sinh viên thường xem roster riêng của CLB hay hồ sơ người khác.
- Hỗ trợ Active → Suspended, Suspended → Active, Active/Suspended → Left; mỗi thay đổi cần lý do. Tự rời qua endpoint riêng ép Left ở server, bỏ qua trạng thái/role/user giả mạo từ form. Left kết thúc đợt, không mở lại; gửi và duyệt đơn mới mới tạo đợt tiếp theo.
- Chủ nhiệm hiện tại phải chuyển giao trước đình chỉ/rời, kể cả Admin hoặc chủ nhiệm tự thao tác. Restore cần CLB Active và tài khoản Student Active không có Admin. Archived chỉ đọc; Suspended không restore nhưng cho đình chỉ/kết thúc. Không sửa Users.Status/SecurityStamp/password hoặc tư cách thành viên ở CLB khác.
- Quyền được kiểm tra trên tài nguyên ở mọi endpoint và đọc lại sau khóa. Transaction khóa các tài khoản theo thứ tự cố định rồi khóa CLB, kiểm tra RowVersion và lưu Membership + AuditLogs nguyên tử. Audit lỗi rollback cả Status/EndedAt. Log ghi người, thời gian, lý do, trạng thái/vai trò/ngày hiệu lực trước và sau. Lịch sử giao diện là lịch sử trạng thái từ lúc tính năng có hiệu lực, không tạo dữ liệu quá khứ giả.
- Dùng lại Memberships/AuditLogs theo yêu cầu gọn database, không thêm bảng/migration. Giữ index đợt mở và chủ nhiệm, các constraint hiện có. Bảng tham chiếu membership_history trong phân tích được hiện thực bằng AuditLogs cho phần trạng thái; audit chuyển vai trò vẫn ở chức năng chuyển chủ nhiệm.
- Full suite **215/215 tests đạt**, thêm 19 ca MembershipTests: quyền/CSRF/giả mạo form, điều kiện khôi phục, bảo vệ chủ nhiệm, lịch sử/phân trang/tìm kiếm, rollback audit, tự rời, xung đột và chuyển chủ nhiệm đồng thời với đình chỉ. Sau đổi `.Result` sang await để dọn warning analyzer, chạy lại ca đồng thời đạt. Build cuối 0 warning, 0 error; model không thiếu migration.
- Browser với dữ liệu QA riêng đã thử tìm mã sinh viên, đình chỉ, khôi phục, sinh viên đăng nhập xem lịch sử và tự rời; thấy đủ ba thay đổi, ngày kết thúc và lịch sử còn nguyên. Console không warning/error. Ảnh `.local/qa/members-history.png`, `members-left-history.png`. Đã đóng tab, dừng đúng tiến trình và xóa đúng database MemberQa có GUID.
- Smoke đọc trên database đồ án: readiness 200 Healthy, danh mục 200; đường dẫn quản lý thành viên và CLB cá nhân chuyển đăng nhập khi chưa xác thực. Không thay schema/dữ liệu thành viên thật. Đã dừng server smoke do trợ lý tạo; người dùng chạy lại ứng dụng để nhận code mới.
- Phạm vi còn phụ thuộc: đăng ký sự kiện chưa có (PB10/PB12). Chưa hiện thực chặn rời khi còn đăng ký nội bộ tương lai hoặc hủy đăng ký chưa bắt đầu khi đình chỉ. Quy tắc và ca kiểm thử TC20 được ghi trong docs/memberships.md để tích hợp trước khi coi toàn bộ PB09 Done. Phần quản lý thành viên hiện tại chờ nhóm review; bước triển khai kế tiếp PB10 quản lý hoạt động/sự kiện.

Bằng chứng: `.local/test-results/members.trx`, `.local/test-results/members-final.trx`, `.local/members-final-build.log`, `.local/members-model-check.log`, `.local/members-qa-results.txt`, `.local/members-live-smoke-checks.json`. Hướng dẫn MVC/SQL và nghiệm thu ở `docs/memberships.md`.

## PB10 — Quản lý hoạt động, 05/11/2026

- Thêm ClubEvent, bảng Events, EventService, EventsController, ViewModels và ba view Index/Edit/Details. Lối vào từ từng CLB; Admin dùng menu dọc hiện có. Danh sách tìm theo tên, lọc trạng thái và phân trang 10 mục.
- Tạo bản nháp có kế hoạch hợp lệ; công bố khi CLB hoạt động, còn hạn đăng ký và chưa bắt đầu. Sửa hoạt động đã công bố cần lý do, chỉ trước giờ bắt đầu cũ và không đổi đối tượng. Hủy cần lý do, giữ dữ liệu; hoàn thành chỉ sau giờ kết thúc. CLB Suspended chỉ hủy/hoàn thành theo điều kiện, Archived chỉ đọc.
- Khách thấy School đã từng công bố; Club chỉ thành viên Student Active hoặc người quản lý đúng CLB/Admin. Nháp và nháp bị hủy không lộ qua đổi ID. Nhật ký chỉ quản lý được xem. POST chống CSRF, NoStore, ViewModel chống overposting, Razor mã hóa nội dung.
- Nhập/xem UTC+7, lưu datetimeoffset UTC. Transaction ghi Events và AuditLogs; khóa tài khoản rồi CLB, kiểm tra lại quyền và RowVersion, chống công bố lặp/ghi đè biểu mẫu cũ. Audit có kế hoạch trước/sau, hash mô tả, người, thời điểm và lý do.
- Migration 20261105024917_AddClubEvents thêm một bảng nghiệp vụ, không thêm bảng Identity. Down từ chối nếu còn Events. SQL CHECK cho enum, sức chứa, thời gian và dữ liệu công bố; FK Clubs Restrict.
- 233/233 kiểm thử tích hợp đạt, gồm 18 ca Events mới. Đã sửa hai cảnh báo analyzer ở test danh sách và chạy lại ca đó thành công; bản cuối không còn cảnh báo, EF báo không có thay đổi model chưa tạo migration.
- Browser QA trên database riêng: đi từ quản trị → CLB → Hoạt động, tạo nháp với giờ Việt Nam, công bố và xác nhận trạng thái/nhật ký. Giao diện desktop rõ ràng, console không có warning/error. Không kiểm tra viewport mobile trong lượt này. Minh chứng .local/qa/events-published.png chỉ chứa dữ liệu thử.
- Database đồ án được COPY_ONLY/CHECKSUM, RESTORE VERIFYONLY hợp lệ trước migration; backup StudentClubManagement_before_events_20261105_095728.bak trong thư mục backup SQL Server, metadata .local/events-backup.json. Counts và SHA-256 của toàn bộ bảy bảng cũ khớp trước/sau; Events ban đầu trống. Smoke /health/ready 200 Healthy, /Clubs 200, /Events yêu cầu đăng nhập. Tab, hai server tạm và đúng database EventQa đã dọn.
- Cập nhật README, simple-database, data-model, Inspect-Database.sql và hướng dẫn docs/events.md. Chưa đánh dấu toàn bộ PB10 Done: kiểm tra giảm sức chứa theo số đăng ký phụ thuộc PB12; snapshot người nhận/thông báo đổi lịch phụ thuộc PB18. PB09 vẫn còn tích hợp đăng ký nội bộ tương lai ở PB12. Tiếp theo PB11: lịch hoạt động theo ngày, thời gian và CLB.

Bằng chứng: .local/test-results/events-full.trx, .local/test-results/events-final.trx, .local/events-final-build-test.log, .local/events-model-check.log, .local/events-live-migration.log, .local/events-before.txt, .local/events-after.txt, .local/events-live-smoke-checks.json, .local/qa/events-published.png.

## PB11 — Lịch hoạt động theo ngày, 06/11/2026

- Thêm Events/Calendar, EventCalendarViewModel, CalendarFor và View Calendar. Menu chính có Lịch hoạt động; trang chi tiết CLB và quản lý hoạt động có lối vào lịch của CLB. Menu thu gọn dưới breakpoint xxl để dành đủ chỗ cho các mục sinh viên.
- GET lọc từ/đến ngày, CLB, trạng thái và tên; mặc định hôm nay UTC+7 đến 30 ngày sau, tính cả hai đầu, tối đa 366 ngày. Lọc theo ngày bắt đầu, đổi biên ngày Việt Nam thành UTC; kết thúc ngày dùng mốc loại trừ 00:00 hôm sau. Thứ tự StartsAt/Id, 10 mục/trang, nhóm ngày sau phân trang; giữ bộ lọc khi chuyển trang, đăng nhập hoặc quay lại từ chi tiết.
- CalendarFor dùng VisibleTo, bổ sung chỉ đã công bố/chưa lưu trữ kể cả Admin. Guest chỉ School; nội bộ cần thành viên Student Active hoặc quyền quản lý đúng CLB/Admin. Count cũng nằm sau lọc quyền; không lộ tổng nội bộ, bản nháp, nháp bị hủy hoặc hồ sơ/người thao tác. NoStore; quyền/tư cách thay đổi có hiệu lực ở request sau.
- Ngày/enum/CLB/bộ lọc sai trả 400 với form sửa, không lỗi 500; page ngoài khoảng được đưa về trang hợp lệ. Link quay lại chỉ chấp nhận URL nội bộ, tối đa 2048 ký tự. Hoạt động hủy hiển thị nhãn đỏ và giải thích, Published có nhãn thời gian tính khi tải trang.
- 23 ca mới đạt; toàn bộ 256/256 kiểm thử tích hợp đạt. Build 0 warning/error; EF không có pending model changes. Không thêm bảng/migration, không ghi dữ liệu vào database đồ án.
- Browser QA trên database riêng: 4 School hiển thị, nháp/nội bộ bị ẩn; lọc CLB + Cancelled còn đúng một mục, mở chi tiết và quay lại giữ bộ lọc; đặt lại khôi phục danh sách; menu hoạt động. Kiểm tra trực quan ở viewport desktop hiện có (1265px), không tràn ngang, console không warning/error. Không kiểm tra viewport điện thoại. Chụp toàn trang không khả dụng nên lưu hai ảnh viewport: .local/qa/calendar.png và .local/qa/calendar-list.png.
- Smoke trên database thật: /health/ready 200 Healthy, /Events/Calendar 200 có form và NoStore, /Clubs 200 có link lịch. Đã đóng tab QA, dừng đúng hai process tạm và dọn database CalendarQa đã tạo. Không thay đổi dữ liệu đồ án.
- Thêm docs/activity-calendar.md giải thích thao tác, MVC, SQL và nghiệm thu; cập nhật README/docs/events.md. Chờ nhóm review PB11; ngoại lệ lịch sử cá nhân sau khi rời CLB cần PB12–PB13 khi có đăng ký. Tiếp theo PB12: đăng ký/hủy, đồng thời tích hợp sức chứa PB10 và đăng ký nội bộ tương lai PB09.

Bằng chứng: .local/calendar-build.log, .local/test-results/calendar-targeted.trx, .local/test-results/calendar-full.trx, .local/calendar-model-check.log, .local/calendar-live-smoke-checks.json, .local/qa/calendar.png, .local/qa/calendar-list.png.

## 09/11/2026 — Đơn giản hóa tài khoản và hai quy trình phê duyệt

- Quy tắc được người dùng xác nhận: mỗi tài khoản một quyền hệ thống Admin/Student; chủ nhiệm thuộc Memberships; duyệt đơn thành lập tự gán người gửi làm chủ nhiệm.
- Migration 20261109033911_StreamlineAccountsAndClubProposals chuyển UserRoles sang Users.RoleId rồi mới bỏ bảng nối; Users còn 16 cột, Roles còn Id/Name. Bỏ sáu cột user chưa cần/lặp và hai cột role. Giữ mật khẩu băm, stamps, lockout và các trường nghiệp vụ đang dùng.
- Migration có chặn nhiều quyền, username khác email, dữ liệu xác minh/2FA; Down phục hồi liên kết quyền nhưng từ chối khi có dữ liệu phê duyệt mới.
- Sinh viên tự đăng ký Student/Pending. Admin duyệt/từ chối có lý do, bảo vệ xung đột; Pending/Rejected/Disabled không đăng nhập. POST cấp sinh viên cũ trả 404, GET chuyển đến hàng chờ; bỏ service/view tạo tài khoản cũ.
- ClubProposals: gửi, rút, lọc/phân trang, lịch sử; Admin chấp nhận/từ chối. Phê duyệt tạo Club Active, Membership Manager Active và audit trong một transaction. Một đơn chờ/người; duyệt trùng, người gửi mất quyền, mã CLB trùng được chặn.
- Menu chủ nhiệm chỉ hiện khi IsManagerAsync trả true. Bootstrap và giao diện VinhUni giữ nguyên; thêm mục quản trị duyệt CLB và form phù hợp.
- WorkflowTransaction dùng chung cho hai luồng mới. Chuẩn hóa dữ liệu đăng ký dùng StudentRegistrationForm.Normalize; mật khẩu không trim và không echo trong HTML.
- 279/279 kiểm thử tích hợp đạt (SQL Server database riêng), không bỏ qua test. Bao gồm luồng mới, chống giả mạo, CSRF, stale/concurrent decisions, rollback do audit lỗi, bảo vệ migration và hồi quy nghiệp vụ cũ.
- Build Release: 0 lỗi, 0 cảnh báo. EF has-pending-model-changes: không có thay đổi chưa tạo migration.
- Browser QA trên database ApprovalQa riêng: đăng ký → Admin duyệt → sinh viên đăng nhập → gửi đơn CLB → Admin duyệt → xuất hiện CLB tôi quản lý với người gửi là chủ nhiệm. Chụp .local/qa/approval-admin.jpg và approval-manager.jpg; đã dọn database/tiến trình QA.
- Database StudentClubManagement đã backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước cập nhật. Fingerprint trước/sau của mọi cột giữ lại khớp, gồm PasswordHash, SecurityStamp, vai trò và toàn bộ bảng nghiệp vụ cũ. Metadata backup tại .local/streamline-backup.json, fingerprint tại streamline-before.json/streamline-after.json (không chứa mật khẩu rõ).
- Sau migration: Users 8, Roles 2, Clubs 6, Memberships 7, ClubProposals 0. Không thêm dữ liệu thử nghiệm vào database thật.
- Bản mới chạy tại http://localhost:5026; GET /health, /health/ready, /, /Clubs, /Account/Login, /Account/Register đều 200. Tiến trình lưu tại .local/streamline-live-process.json. Mật khẩu Admin thật không thay đổi.
- Hướng dẫn hiện tại: docs/simple-database.md và docs/approval-workflows.md. Những tài liệu theo bước cũ đã có ghi chú phiên bản thay thế.
- Chưa bổ sung email tự động hoặc luồng sinh viên nộp lại tài khoản bị từ chối; đây là phạm vi tiếp theo nếu cần. PB12 đăng ký sự kiện chưa triển khai trong lần thay đổi này.

## PB12 — Đăng ký và hủy tham gia hoạt động, 10/11/2026

- Thêm EventRegistration, EventRegistrationService, EventRegistrationsController và giao diện đăng ký/hủy trong chi tiết hoạt động. Sinh viên xem danh sách của mình tại Tài khoản → Hoạt động đã đăng ký; Admin/chủ nhiệm đúng CLB xem danh sách từ chi tiết hoạt động.
- Chỉ Student Active được đăng ký hoạt động Published của CLB Active, còn hạn, chưa bắt đầu và còn chỗ. Hoạt động nội bộ cần thành viên Active. Hủy được trước giờ bắt đầu, kể cả đã hết hạn đăng ký. Admin không đăng ký với quyền Admin.
- Mỗi cặp hoạt động/sinh viên có một dòng; đăng ký lại sử dụng dòng đã hủy. EventRegistrations chỉ gồm sáu cột: Id, EventId, UserId, RegisteredAt, CancelledAt, RowVersion. CancelledAt null nghĩa là đang đăng ký; lịch sử các lần thay đổi nằm trong AuditLogs.
- Transaction khóa tài khoản rồi CLB và kiểm tra lại quyền/phiên bản/điều kiện trước khi ghi. Chống trùng đăng ký, tranh chỗ cuối, form cũ và tranh chấp với sửa/hủy hoạt động hoặc đình chỉ thành viên. Lỗi audit rollback toàn bộ thao tác.
- Tích hợp PB10: không giảm sức chứa dưới số đang đăng ký; hủy hoạt động đồng thời hủy đăng ký còn hiệu lực, giữ lý do và lịch sử. Tích hợp PB09: tự rời CLB phải hủy đăng ký nội bộ tương lai trước; quản lý đình chỉ/kết thúc tư cách tự hủy các đăng ký nội bộ chưa bắt đầu. Khôi phục thành viên không tự đăng ký lại.
- Danh sách cá nhân giữ thông tin tóm tắt sau khi rời CLB nhưng không cấp quyền xem lại toàn bộ chi tiết nội bộ. Danh sách quản lý phân trang, chỉ hiện thông tin sinh viên cần thiết; POST có antiforgery, UserId lấy từ phiên đăng nhập, các trang riêng dùng NoStore.
- Thêm 34 ca kiểm thử; toàn bộ **313/313 kiểm thử tích hợp đạt**, không bỏ qua ca nào. Build cuối không cảnh báo/lỗi; EF xác nhận không có model changes thiếu migration. Bao gồm phân quyền, giới hạn thời gian/sức chứa, giả mạo, đồng thời, rollback và lý do hủy tối đa 500 ký tự.
- Browser QA trên database riêng: sinh viên đăng ký → danh sách cá nhân → hủy → số chỗ trở lại 0; chủ nhiệm xem danh sách và lý do hủy. Kiểm tra trực quan desktop; chưa kiểm tra viewport điện thoại trong lượt này. Đã đóng tab thử, dừng hai máy chủ tạm 5070/5071 và xóa đúng database QA có GUID; không thay đổi tiến trình của người dùng ở cổng 5026.
- Migration 20261110043425_AddEventRegistrations chỉ thêm bảng và ràng buộc/index; Down từ chối nếu còn dữ liệu đăng ký. Trước migration thật đã backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY. SHA-256 toàn bộ dữ liệu tám bảng cũ khớp trước/sau. Database thật sau cập nhật có Users 9, Clubs 6, Events 1, EventRegistrations 0; không thêm dữ liệu QA vào đồ án.
- Smoke trên database thật: /health/ready, /, /Clubs, /Events/Calendar, /Account/Login và /Account/Register đều trả 200. Người dùng chạy lại dotnet run để nhận bản mới; không để máy chủ nền chiếm cổng.
- Cập nhật README, tài liệu dữ liệu/hoạt động/thành viên/lịch và Inspect-Database.sql. Hướng dẫn thao tác, MVC, SQL và quy tắc ở docs/event-registrations.md. Chờ nhóm nghiệm thu PB12; bước kế tiếp PB13 lịch sử cá nhân chi tiết. Thông báo tự động PB18 và điểm danh chưa triển khai trong bước này; chưa đánh dấu toàn bộ PB10 Done.

Bằng chứng: .local/registrations-final-tests.log, tests/StudentClubManagement.IntegrationTests/TestResults/registrations-final.trx, .local/registrations-live-migration.log, .local/registrations-backup.json, .local/registrations-before.json, .local/registrations-after.json, .local/registrations-live-smoke-checks.json, .local/qa/event-registration.jpg, .local/qa/event-registration-manager.jpg.

## PB13 — Tra cứu đăng ký và lịch sử cá nhân, 10/11/2026

- Bổ sung tìm kiếm và trạng thái all/confirmed/cancelled cho danh sách PB12. Trang cá nhân tìm tên hoạt động/CLB, lọc CLB và khoảng ngày bắt đầu theo UTC+7; quản lý tìm họ tên/mã SV trong đúng hoạt động. Danh mục CLB cá nhân chỉ lấy từ đăng ký của chính người xem, kể cả CLB đã lưu trữ.
- Phân trang 10 mục, thứ tự RegisteredAt/Id giảm dần, giữ bộ lọc khi đổi trang và khi quay lại từ lịch sử. Kiểm tra từ khóa dài, enum sai, ngày sai/biên, khoảng ngược và page không phải số; trả 400 kèm form sửa. Trang ngoài phạm vi được đưa về trang hợp lệ.
- Thêm History dành riêng cho chủ đăng ký Student Active. Đọc hai loại EventRegistered/EventRegistrationCancelled từ AuditLogs theo cả EntityType/EntityId; hiển thị đăng ký, hủy, đăng ký lại, thời gian, lý do và nhãn Bạn/Người quản lý. Phân trang nhật ký theo OccurredAt/Id, không xuất JSON hoặc hồ sơ người thao tác.
- Giữ quyền xem tóm tắt/lịch sử cá nhân sau khi rời/đình chỉ hoặc CLB lưu trữ. Không cấp lại quyền đọc mô tả hoạt động nội bộ. Trạng thái hoạt động được hiển thị riêng với trạng thái đăng ký; không giả định đã tham dự. Thông tin lịch ở đầu trang được ghi rõ là lịch hiện tại; thiếu audit thì báo chưa có nhật ký.
- Dùng lại EventRegistrations và AuditLogs, không đổi schema hoặc migration. EF has-pending-model-changes xác nhận model khớp. Các truy vấn đều giới hạn quyền trước lọc/Count; NoStore và Razor encoding được giữ; ReturnUrl chỉ nhận đường dẫn nội bộ có giới hạn độ dài.
- 15 kiểm thử mới đạt; full suite **328/328 đạt**, không bỏ qua test, trên SQL Server riêng. Bao phủ chủ sở hữu, người ngoài/chủ nhiệm/Admin, thu hồi quyền, lịch sử sau rời CLB, lọc ngày UTC+7, phân trang ổn định, XSS, dữ liệu audit thiếu/khác loại và đầu vào sai. Build cuối 0 warning, 0 error. Sau full suite chỉ rút gọn hai câu mô tả giao diện, đã build lại thành công.
- Edge headless trên database HistoryQa riêng: đăng ký → hủy → đăng ký lại, ba mốc lịch sử, tìm/lọc, quay lại giữ giá trị bộ lọc; chủ nhiệm tìm mã SV và lọc xác nhận/đã hủy. Hai màn hình danh sách và lịch sử không tràn ngang ở 360/768/1366 px. Đã xem ảnh desktop và mobile; không có lỗi console/pageerror.
- Đã đóng trình duyệt QA, dừng đúng hai process 5072/5073 và xóa đúng database QA có GUID. Không thay đổi tiến trình người dùng ở cổng 5026. Smoke đọc trên database thật: health/ready, trang chủ, lịch trả 200; Mine/History chuyển đăng nhập 302 khi chưa xác thực. Không thay schema hoặc dữ liệu nghiệp vụ thật.
- Hướng dẫn code MVC, SQL, sử dụng và nghiệm thu tại docs/registration-history.md; cập nhật README và docs/event-registrations.md. Nhóm review PB13 theo các ca trong hướng dẫn. Bước kế tiếp PB14 điểm danh, sau đó PB15 chốt/sửa kết quả; PB18 thông báo còn chưa triển khai.

Bằng chứng: .local/registration-history-tests.log, .local/registration-history-full-tests.log, tests/StudentClubManagement.IntegrationTests/TestResults/registration-history-full.trx, .local/history-final-build.log, .local/history-model-check.log, .local/history-browser-results.json, .local/history-live-smoke-checks.json, .local/qa/registration-history.png, .local/qa/registration-history-mobile.png, .local/qa/registration-history-list.png.

## PB14 — Điểm danh người đăng ký, 10/11/2026

- Thêm Attendance, AttendanceService, AttendancesController, ViewModel và Index với thẻ sinh viên/form lưu từng người. Lối vào từ chi tiết hoạt động và danh sách đăng ký; Admin tiếp tục dùng menu dọc hiện có. Tìm tên/mã SV, lọc kết quả, phân trang 10 người; sau lưu giữ bộ lọc/trang.
- Phân biệt Unmarked/Present/Absent/Excused. Không có dòng điểm danh được hiểu là Unmarked; đặt lại từ kết quả đã ghi về Unmarked giữ dòng và nhật ký. Lưu cùng kết quả hiện tại không tạo lần ghi mới. Người thực hiện/thời gian lấy từ server, không lấy từ form.
- Chỉ Admin Active hoặc chủ nhiệm Active đúng CLB được xem/ghi. Chỉ ghi hoạt động Published/Completed đã bắt đầu; Club Suspended cho xử lý kết quả đã diễn ra, Archived chỉ đọc. Đăng ký phải chưa hủy và RegisteredAt không sau StartsAt. Không thêm người ngoài danh sách hoặc tự coi người chưa ghi là vắng.
- Người tham gia rời/đình chỉ/khóa tài khoản sau giờ bắt đầu vẫn nằm trong tập đăng ký cũ. Hủy toàn bộ hoạt động giữ kết quả đã ghi và audit, màn hình chuyển chỉ đọc; không dùng kết quả hoạt động hủy làm số liệu tham dự hợp lệ.
- Transaction dùng khóa Account(actor) rồi Club, kiểm tra lại quyền và các phiên bản Event/Registration/Attendance. Lưu kết quả và audit nguyên tử; lỗi audit rollback. Chống hai lần ghi đầu tạo trùng, ghi đè form cũ và tranh chấp với hủy hoạt động. POST có antiforgery; danh sách NoStore và Razor encoding.
- Bảng Attendances chỉ 5 cột: RegistrationId (PK/FK), Status, MarkedAt, MarkedById, RowVersion. Không có Id riêng, không lặp hồ sơ/sự kiện, không thêm bảng lịch sử. SQL CHECK giới hạn enum, FK Restrict giữ liên kết. Migration 20261110103415_AddAttendances chỉ thêm bảng/index/ràng buộc; Down chặn nếu còn dữ liệu.
- 25 ca mới; toàn bộ **353/353 kiểm thử tích hợp đạt**, không bỏ qua, trên SQL Server riêng. Bao phủ quyền chéo, trường giả mạo, CSRF, trạng thái/thời gian, form cũ, ghi đồng thời, audit rollback, hủy, lịch sử thành viên rời và ràng buộc/migration. Build không warning/error; EF xác nhận model khớp migration.
- Edge headless trên database AttendanceQa riêng: ghi Có mặt, lọc Chưa điểm danh, tìm mã SV, sửa Có phép, đặt lại Unmarked rồi ghi lại; hiển thị người/thời gian đúng. Kiểm tra 360/768/1366 px không tràn ngang, không lỗi console/pageerror. Đã xem ảnh desktop/mobile.
- Database thật đã BACKUP COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước migration. SHA-256 toàn bộ dữ liệu chín bảng cũ khớp trước/sau; Users 9, Clubs 6, Events 1, EventRegistrations 0, Attendances 0 sau cập nhật. Không thêm dữ liệu QA hoặc đổi mật khẩu thật.
- Đã đóng browser QA, dừng đúng server 5074/5075 và xóa đúng AttendanceQa GUID. Không đổi tiến trình người dùng ở 5026. Smoke đọc database thật: health/ready, trang chủ, lịch 200; đăng ký/lịch sử/điểm danh yêu cầu đăng nhập 302. Người dùng chạy lại ứng dụng để nhận code mới.
- Thêm docs/attendance.md hướng dẫn sử dụng, code MVC/SQL và nghiệm thu; cập nhật README, tài liệu dữ liệu/đăng ký và Inspect-Database.sql. Nhóm review PB14 theo kịch bản trong hướng dẫn. PB15 chốt roster/sửa sau chốt có lý do và màn hình kết quả cá nhân còn ở bước tiếp theo; PB18 thông báo chưa triển khai. Không đánh dấu toàn bộ Sprint 3 Done.

Bằng chứng: .local/attendance-full-tests.log, tests/StudentClubManagement.IntegrationTests/TestResults/attendance-full.trx, .local/attendance-model-check.log, .local/attendance-browser-results.json, .local/attendance-backup.json, .local/attendance-before.json, .local/attendance-after.json, .local/attendance-live-migration.log, .local/attendance-live-smoke-checks.json, .local/qa/attendance-desktop.png, .local/qa/attendance-mobile.png.

## PB15 — Chốt danh sách và sửa có truy vết, 10/11/2026

- Thêm SummaryAsync/FinalizeAsync vào AttendanceService, POST Finalize và khung kiểm tra/chốt trên màn hình điểm danh. Tổng kết tính toàn bộ đăng ký hợp lệ, không phụ thuộc tìm kiếm/trang hiện tại. Chỉ chốt sau EndsAt, Published/Completed, CLB chưa Archived và đúng quyền quản lý.
- Còn Unmarked thì từ chối trừ khi người dùng đánh dấu xác nhận chuyển toàn bộ số này sang Absent. Danh sách rỗng được chốt với mẫu số 0. Chốt không tự chuyển trạng thái hoạt động sang Completed; hai trạng thái độc lập.
- Thêm Events.AttendanceFinalizedAt và Attendances.IsInFinalRoster. Không thêm bảng; Attendances từ 5 lên 6 cột. Chốt ghi dấu trên mọi kết quả hợp lệ trong cùng transaction, tạo dòng Absent cho người thiếu khi đã xác nhận. Kết quả đã ghi giữ người/thời gian ghi gần nhất. Audit chốt lưu người, thời gian và tổng kết.
- Sau chốt chỉ sửa Present/Absent/Excused cho người có dấu thuộc roster; bắt buộc lý do 1–500 ký tự, không thêm người/đưa về Unmarked. Audit AttendanceCorrected lưu trước/sau và lý do. Sửa không đổi roster hoặc thời điểm chốt. Hủy cả hoạt động giữ dấu/kết quả, chuyển chỉ đọc và loại khỏi tham dự hợp lệ.
- Khóa tài khoản rồi CLB, kiểm tra lại quyền/phiên bản hoạt động và SHA-256 toàn bộ Id/RowVersion của đăng ký/kết quả. Form chốt cũ, chốt lặp, ghi đua chốt/hủy đều được xử lý; lỗi audit rollback cả kết quả chuyển Vắng, dấu roster và thời điểm chốt.
- Thêm Attendances/History có kiểm tra quyền tài nguyên. Quản lý đúng CLB xem nhật ký trước/sau chốt. Sinh viên chỉ xem kết quả đã chốt và sửa sau chốt của chính mình; không xuất ghi chú tạm, JSON hoặc hồ sơ người thao tác. Còn xem kết quả sau rời CLB, không mở lại toàn bộ nội dung nội bộ. Thêm liên kết từ danh sách/lịch sử đăng ký; lịch sử phân trang 10 mục, NoStore, Razor encoding, ReturnUrl nội bộ.
- Migration 20261110120624_FinalizeAttendance chỉ thêm hai cột và CHECK. CHECK giữ thời điểm chốt không trước EndsAt và cấm Unmarked trên roster. Down chặn khi đã có chốt/roster. Các kiểm thử downgrade cũ khôi phục latest migration sau khi kiểm tra chặn để không để schema kiểm thử dở dang.
- 24 ca mới, toàn bộ **377/377 kiểm thử tích hợp đạt**, không skip, SQL Server riêng. Sau khi đổi SQL trigger thử sang ba câu lệnh cố định để dọn cảnh báo, chạy lại 3 ca rollback đạt; bản cuối không warning/error. EF xác nhận không thiếu migration. Bao phủ xác nhận Unmarked, roster toàn danh sách, quyền, form cũ, đồng thời, audit rollback, lịch sử sau rời, XSS, phân trang, dữ liệu audit lỗi và ràng buộc SQL.
- Edge headless: chốt thiếu xác nhận bị chặn, xác nhận hai người thành Vắng, sửa một người Có mặt có lý do, quản lý xem nhật ký, sinh viên thấy đúng kết quả và một lần sửa. Hai màn hình quản lý/cá nhân không tràn ngang tại 360/768/1366 px, không lỗi console/pageerror. Đã xem ảnh desktop; kiểm tra kích thước mobile bằng trình duyệt.
- Database thật BACKUP COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước migration. SHA-256 mọi cột cũ của cả 10 bảng khớp trước/sau; không tự chốt dữ liệu quá khứ, không thêm QA hoặc đổi mật khẩu. Sau migration Events 1, Attendances 0, FinalizedEvents 0.
- Đã đóng trình duyệt QA, dừng đúng server 5076/5077 và xóa đúng FinalizationQa GUID. Không đổi tiến trình ở 5026. Smoke đọc database thật: health/ready, trang chủ, lịch 200; các trang cá nhân/điểm danh/lịch sử chuyển đăng nhập 302. Người dùng chạy lại Web để dùng code mới.
- Thêm docs/attendance-finalization.md; cập nhật README, simple-database, PB12–PB14 và Inspect-Database.sql. Nhóm review PB15 theo hướng dẫn; bước tiếp theo PB16 thông báo theo đối tượng, sau đó PB17 hộp thông báo/PB18 đổi lịch-hủy. Dashboard thống kê PB20 và toàn bộ Sprint 3 chưa được đánh dấu Done.

Bằng chứng: .local/finalization-full-tests.log, tests/StudentClubManagement.IntegrationTests/TestResults/finalization-full.trx, .local/finalization-audit-final.log, .local/finalization-model-check.log, .local/finalization-browser-results.json, .local/finalization-backup.json, .local/finalization-columns.json, .local/finalization-before.json, .local/finalization-after.json, .local/finalization-live-migration.log, .local/finalization-live-smoke-checks.json, .local/qa/finalized-attendance.png, .local/qa/finalized-personal-result.png, .local/qa/finalized-personal-mobile.png.

## PB16 — Đăng thông báo theo đối tượng, 10–11/11/2026

- Đối chiếu backlog gốc FR14/PB16/BR17: thông báo riêng cho thành viên CLB hoặc người đăng ký hoạt động, không thêm bảng tin công khai. Admin quản lý mọi CLB, chủ nhiệm đúng CLB; sinh viên thường không được soạn/phát hoặc xem danh sách người nhận.
- Thêm AnnouncementService, AnnouncementsController, ViewModel và năm View: chọn CLB, danh sách, soạn/sửa nháp, chi tiết và người nhận. Admin có mục Quản lý thông báo trong sidebar; thẻ CLB quản lý có nút Thông báo. Tìm tiêu đề, lọc trạng thái, phân trang 10; dùng giao diện MVC/Bootstrap hiện có.
- Draft → Published → Archived. Chỉ sửa nháp; nội dung đã đăng đính chính bằng bản mới. Lưu trữ cần lý do và không xóa lịch sử/quyền đọc người nhận. CLB không Active chặn soạn/sửa/đăng; Archived chỉ đọc. Hoạt động nguồn phải thuộc CLB và đã công bố/hoàn thành.
- Preview lấy tập Student Active theo membership Active chưa kết thúc hoặc đăng ký chưa hủy. Trước đăng so lại SHA-256 tập UserId; thay người dù cùng số lượng vẫn yêu cầu kiểm tra lại. Không có người nhận thì giữ nháp. Server tự lấy danh sách, không tin UserIds/Status/PublishedAt từ form.
- Transaction khóa actor rồi CLB, kiểm tra lại quyền/RowVersion, lưu trạng thái/thời điểm/người nhận/audit cùng nhau. Rời CLB/hủy hoạt động cùng dùng khóa CLB; snapshot đã phát không đổi. Bấm trùng chỉ một lần commit, PK ghép chống dòng trùng, audit lỗi rollback toàn bộ.
- Announcements chỉ 9 cột; EventId null có nghĩa gửi thành viên, không thêm Audience/RecipientCount. AnnouncementRecipients chỉ 2 cột, PK (AnnouncementId,UserId), không Id riêng và không sao chép hồ sơ. FK Restrict/CHECK, migration 20261110151453_AddAnnouncements; Down chặn khi có dữ liệu. Không sửa Users/Roles.
- Người nhận đọc chi tiết theo snapshot, vẫn đọc sau rời CLB/hủy đăng ký/lưu trữ; tài khoản phải Student Active. Không thấy danh sách người khác, số lượng hoặc audit quản trị. Controller Authorize/NoStore, POST antiforgery, Razor encoding. PB17 hộp thông báo/đã đọc còn ở bước tiếp theo.
- 27 ca mới, toàn bộ **404/404 kiểm thử tích hợp đạt**, không skip, build không warning/error. Lần hồi quy đầu phát hiện assertion schema cũ chưa liệt kê hai bảng mới; đã cập nhật và chạy lại toàn bộ đạt. Model khớp migration. Có kiểm tra HTTP/quyền/CSRF/XSS, trạng thái/snapshot, form cũ, đồng thời, ba ca audit rollback và ràng buộc SQL/Down.
- Edge headless với database AnnouncementQa riêng: tạo/sửa/đăng hai loại đối tượng, danh sách nhận, sinh viên chỉ đọc, Admin lưu trữ/lọc; form/chi tiết/người nhận vừa 360/768/1366 px, menu mobile hoạt động, không lỗi console/pageerror. Đã xem ảnh desktop và mobile.
- Database thật được BACKUP COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY trước migration. Hash mọi cột cũ của mười bảng khớp trước/sau. Announcements/AnnouncementRecipients ban đầu rỗng; không thêm QA hoặc đổi mật khẩu thật.
- Đã dừng đúng server 5078/5079, đóng browser và xóa đúng QA GUID sau kiểm thử; không can thiệp server người dùng 5026. Smoke đọc database thật: health/ready, trang chủ, lịch 200; các trang thông báo/cá nhân chuyển đăng nhập 302.
- Thêm docs/announcements.md giải thích nghiệp vụ/MVC/SQL và cách dùng; cập nhật README, schema, Inspect-Database.sql và liên kết PB15. Cần nhóm review theo kịch bản; chưa đánh dấu Sprint 3 Done. Bước kế tiếp PB17 hộp thông báo/đã đọc, sau đó PB18 tích hợp đổi lịch/hủy.

Bằng chứng: .local/announcements-full-final-tests.log, tests/StudentClubManagement.IntegrationTests/TestResults/announcements-full-final.trx, .local/announcements-model-check.log, .local/announcements-browser-results.json, .local/announcements-backup.json, .local/announcements-before.json, .local/announcements-after.json, .local/announcements-live-migration.log, .local/announcements-live-smoke-checks.json, .local/qa/announcements-admin.png, .local/qa/announcements-mobile.png.

## PB17 — Hộp thông báo và đánh dấu đã đọc, 11/11/2026

- Bổ sung AnnouncementRecipient.ReadAt nullable, migration 20261111011414_AddAnnouncementReadAt; chỉ thêm một cột, không thêm bảng/IsRead/RowVersion. Down chặn nếu có lịch sử đọc. Dòng cũ mặc định chưa đọc vì bản trước chưa theo dõi.
- AnnouncementService dùng ReceivedBy chung cho hộp, bộ đếm, quyền đọc chi tiết và cập nhật. Chỉ Student Active trong snapshot nhận thông báo Published/Archived; không phụ thuộc membership hoặc trạng thái CLB hiện tại. Admin/quản lý không có quyền đánh dấu thay người nhận.
- MarkReadAsync mở transaction, khóa tài khoản để phối hợp khóa/mở user, kiểm tra quyền rồi ExecuteUpdateAsync chỉ khi ReadAt IS NULL. Dùng giờ server, không nhận UserId/ReadAt từ form; POST lặp/đồng thời thành công mà giữ thời điểm đầu tiên. Không thêm audit quản trị cho thao tác đọc thông thường.
- Thêm GET Mine và POST MarkRead. Hộp tìm tiêu đề, lọc CLB/trạng thái đọc, phân trang 10 theo PublishedAt/Id giảm dần. Lọc quyền trước đếm/phân trang; bộ lọc CLB chỉ gồm CLB đã gửi cho người xem. Số chưa đọc tính toàn hộp, gồm bản lưu trữ. Giữ bộ lọc/trang qua ReturnUrl nội bộ có giới hạn độ dài; trang biên được clamp.
- Menu Student có Thông báo kèm số chưa đọc, badge 99+ khi vượt 99, nhãn truy cập giữ số thật; tài khoản có liên kết tới hộp. Không đưa hộp Student vào Admin. Hộp/chi tiết có nút xác nhận; GET không đổi trạng thái. Trang người nhận của quản lý hiển thị tổng đã đọc và thời điểm của từng người. Chủ nhiệm vẫn có hộp cá nhân nếu thuộc snapshot.
- Dùng Authorize/NoStore, antiforgery và Razor encoding; không lộ người nhận khác hoặc audit trên hộp cá nhân. Không tự mở quyền nội dung sự kiện nội bộ khi người nhận đã rời CLB. Không email, push, đánh dấu tất cả, chuyển lại chưa đọc hoặc polling trong PB17.
- 18 ca mới; toàn bộ **422/422 kiểm thử tích hợp đạt**, không skip. Kiểm thử đầu phát hiện truy vấn record/Distinct/OrderBy chưa dịch được sang SQL; đã chuyển projection phù hợp và chạy lại đạt. Restore cuối/build sạch warning/error; EF model khớp migration. Downgrade tests cũ được bổ sung khôi phục latest schema sau kiểm tra.
- Edge headless trên InboxQa riêng: 5 thông báo/4 chưa đọc, GET không ghi, POST giảm badge, quay lại giữ bộ lọc, đọc bản lưu trữ, tìm kiếm/đã đọc/rỗng, chủ nhiệm xem số đọc chính xác. Kiểm tra 360/768/1440/1920 px không tràn, menu mobile hoạt động, không lỗi console/pageerror; đã xem ảnh desktop/mobile.
- Database thật COPY_ONLY/CHECKSUM + RESTORE VERIFYONLY trước migration. Hash mọi cột cũ của cả 12 bảng khớp trước/sau; không tự tạo thông báo hoặc đánh dấu dữ liệu thật. Không đổi tài khoản/mật khẩu. Lúc bàn giao Announcements/AnnouncementRecipients vẫn rỗng.
- Đã đóng browser, dừng server 5080/5081 và xóa đúng InboxQa GUID; giữ nguyên server người dùng 5026. Script smoke từng có lỗi tham số StartsWith sau thay tên; đã sửa và chạy hoàn tất. Readiness/trang chủ/lịch 200, hộp/thông báo yêu cầu đăng nhập 302; server tạm luôn dừng trong finally.
- Thêm docs/notification-inbox.md giải thích sử dụng/MVC/SQL và nghiệm thu; cập nhật README, docs schema/thông báo và Inspect-Database.sql. Cần nhóm review; chưa đánh dấu toàn bộ Sprint 3 Done. Bước kế tiếp PB18: thông báo tự động đổi lịch/hủy cùng transaction và snapshot người đăng ký trước thay đổi.

Bằng chứng: .local/inbox-targeted-final-tests.log, .local/inbox-full-tests.log, tests/StudentClubManagement.IntegrationTests/TestResults/inbox-full.trx, .local/inbox-restore.log, .local/inbox-model-check.log, .local/inbox-browser-results.json, .local/inbox-backup.json, .local/inbox-before.json, .local/inbox-after.json, .local/inbox-live-migration.log, .local/inbox-live-smoke-checks.json, .local/qa/inbox-desktop.png, .local/qa/inbox-mobile.png.

## PB18 — Thông báo tự động đổi lịch/hủy, 11–13/11/2026

- EventService gọi EventNoticeService trước khi thay đổi hoạt động Published: đổi StartsAt/EndsAt/RegistrationDeadline/Location hoặc hủy. Một thông báo gộp trước/sau và lý do; hủy giữ lịch đã công bố. Không thông báo khi chỉ sửa tên/mô tả/sức chứa, lưu không đổi, thao tác nháp, công bố hoặc hoàn thành. Giữ validation, quyền, giới hạn sức chứa và RowVersion hiện có.
- Khóa tài khoản rồi CLB trong giao dịch dùng chung với đăng ký. Snapshot mọi đăng ký CancelledAt NULL, không lọc tài khoản đang khóa; lưu thông báo/người nhận trước khi hủy đăng ký. Người bị khóa chỉ đọc được khi trở lại Student Active. 0 người nhận vẫn lưu dấu thay đổi. Thêm audit EventNoticePublished, lỗi ở bất kỳ lần ghi nào rollback toàn bộ.
- Thêm duy nhất Announcements.SourceKey varchar(64) NULL: SHA256(EventId + RowVersion trước thay đổi + loại thao tác), unique filtered index và CHECK. Vẫn 12 bảng; Announcements 10 cột, AnnouncementRecipients 3. Migration 20261111013356_AddEventNoticeSource; Down chặn nếu có lịch sử tự động. Form cũ trả Conflict, không tạo trùng hoặc tính lại snapshot sau hủy. SourceKey do server tính, không bind từ form.
- Người nhận đọc qua PB17, giữ ReadAt và snapshot cũ. Thông báo không chép mô tả riêng; liên kết sự kiện chỉ hiện khi còn quyền VisibleTo. Quản lý xem 5 thông báo gần nhất trong chi tiết hoạt động; form sửa/hủy có hướng dẫn. Giao diện thông báo phân biệt bản tự động. Không email/push hoặc thêm bảng hàng đợi.
- 26 ca mới đạt; toàn bộ **448/448 kiểm thử tích hợp đạt**, không skip. Kiểm tra lặp/đồng thời, snapshot tài khoản khóa/0 người nhận, tranh chấp đăng ký/rút, giới hạn nội dung/XSS, quyền, SQL unique/CHECK/Down và 7 trường hợp lỗi ghi để xác nhận rollback. Test downgrade PB17 khôi phục latest migration sau khi guard từ chối. EF xác nhận model khớp migration; diff không lỗi khoảng trắng.
- Edge headless trên database QA riêng xác nhận đổi lịch → sinh viên đọc → hủy; đăng ký về 0 nhưng thông báo hủy vẫn giữ 2 người, thông báo trước giữ trạng thái đọc. Script QA ban đầu tìm email nhưng giao diện người nhận chỉ hiển thị họ tên/mã SV; sửa assertion theo mã SV, tạo lại database QA và chạy lại toàn bộ kịch bản thành công. Không đổi giao diện để đáp ứng assertion sai.
- Giao diện hộp/chi tiết/thông báo tự động vừa 360/768/1440/1920 px, không lỗi console/pageerror hoặc tràn ngang; đã xem ảnh quản lý/mobile. Đóng browser và dọn đúng server/database QA sau kiểm tra.
- Database thật COPY_ONLY/CHECKSUM + RESTORE VERIFYONLY trước migration; hash tất cả cột cũ của cả 12 bảng khớp trước/sau. Khi bàn giao 1 hoạt động, 0 thông báo/0 người nhận; không tạo dữ liệu QA trên database thật. Không đổi tài khoản/mật khẩu. Readiness/trang chủ/lịch 200; đường dẫn cá nhân/thông báo chuyển đăng nhập 302.
- Dừng đúng server tạm 5082/5083, xóa đúng EventNoticeQa GUID; không chiếm/dừng server 5026. Thêm docs/event-change-notifications.md giải thích sử dụng, MVC, SQL và kịch bản review; cập nhật README, schema, hướng dẫn PB10/PB16/PB17 và truy vấn Inspect-Database.sql.
- PB18 sẵn sàng cho nhóm nghiệm thu; chưa tự đánh dấu Sprint 3 Done. Bước tiếp theo **PB19 — Thống kê CLB và thành viên hiện tại**, phân biệt số sinh viên duy nhất với số membership, áp dụng đúng quyền và đối chiếu dữ liệu chi tiết.

Bằng chứng: .local/event-notices-targeted-tests.log, .local/event-notices-regression-tests.log, tests/StudentClubManagement.IntegrationTests/TestResults/event-notices-regression.trx, .local/event-notices-model-check.log, .local/event-notices-browser-results.json, .local/event-notices-backup.json, .local/event-notices-before.json, .local/event-notices-after.json, .local/event-notices-live-migration.log, .local/event-notices-live-smoke-checks.json, .local/qa/event-notices-manager.png, .local/qa/event-notices-student.png, .local/qa/event-notices-mobile.png.

## Tối giản thanh điều hướng và màu file trong VS Code — 13/11/2026

- Theo yêu cầu nhóm trưởng trước PB19: đặt git.decorations.enabled=false trong .vscode/settings.json ở root và src/StudentClubManagement.Web để hỗ trợ cả hai thư mục người dùng thường mở. Chỉ thay hiển thị Git, không commit/revert hoặc xóa thay đổi. Đã đối chiếu khóa cấu hình với extension Git đi kèm VS Code đang cài.
- _Layout giữ bốn liên kết Trang chủ/CLB/Lịch/Giới thiệu. Khách có nút Đăng nhập. Người đã đăng nhập mở menu bên trái bằng nút Cá nhân; mặc định đóng trên mọi kích thước. Desktop bốn liên kết nằm cùng hàng logo, mobile nằm trên hàng riêng để không bị ngắt chữ.
- _PersonalMenu dùng Bootstrap offcanvas đã có, màu và nhóm chức năng theo giao diện Admin. X/Escape/backdrop đóng menu, focus quay lại nút mở; site.js đồng bộ aria-expanded. Thông báo/Đơn/Thành lập CLB/Tài khoản/Đổi mật khẩu/Đăng xuất được chuyển vào menu; quyền chủ nhiệm vẫn kiểm tra server và chỉ hiện mục quản lý cho đúng tài khoản. Admin từ trang công khai có lối vào khu vực quản trị cũ. Đăng xuất vẫn là POST có antiforgery.
- Số chưa đọc giữ trong menu, chấm xanh trên nút khi số >0, nhãn truy cập nêu số lượng. Đánh dấu đã đọc cập nhật sau request như trước. Các đường dẫn, nghiệp vụ và SQL không đổi; không thêm package/migration. CLB đã tham gia và hoạt động đã đăng ký vẫn nằm trong trang Tài khoản.
- Build Release thành công, 0 warning/error. Toàn bộ **448/448 kiểm thử tích hợp đạt**, không skip; dùng database test riêng. Edge headless xác nhận quyền sinh viên/chủ nhiệm/Admin, các liên kết, nhãn trang hiện tại, bộ đếm thông báo, mở/đóng/focus và đăng xuất ở màn hình thấp.
- Kiểm tra 320/360/768/992/1280/1440/1920 px phát hiện gutter hàng hero trang chủ gây tràn ở điện thoại; giảm riêng gutter ngang tại màn hình hẹp. Chạy lại browser QA đạt, bốn liên kết cùng hàng không chồng nhau, không tràn ngang hoặc lỗi console/pageerror. Đã xem ảnh desktop và mobile khi mở/đóng.
- Đóng browser, dừng đúng server QA 5084 và xóa đúng PersonalMenuQa GUID đã tạo. Không thay database thật, tài khoản thật hoặc tiến trình người dùng 5026. Khởi động lại web và tải mới trang để dùng giao diện. VS Code có thể Reload Window nếu chưa cập nhật thiết lập. PB19 chưa bắt đầu.

Bằng chứng: .local/personal-menu-build.log, .local/personal-menu-regression.log, tests/StudentClubManagement.IntegrationTests/TestResults/personal-menu-regression.trx, .local/personal-menu-browser-results.json, .local/qa/personal-menu-desktop-open.png, .local/qa/personal-menu-desktop-closed.png, .local/qa/personal-menu-mobile-open.png, .local/qa/personal-menu-mobile-closed.png.
