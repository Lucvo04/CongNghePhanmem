# Website quản lý câu lạc bộ sinh viên

Đồ án Công nghệ phần mềm của nhóm 3 thành viên, triển khai theo ASP.NET Core MVC và Agile Scrum.

## Trạng thái hiện tại

Cập nhật 13/11/2026: đã có đăng nhập/phân quyền, hồ sơ, CLB, đơn gia nhập, thành viên, hoạt động/lịch; bổ sung sinh viên tự đăng ký chờ Admin duyệt và đơn thành lập CLB. Mỗi tài khoản có một quyền hệ thống qua Users.RoleId, không còn UserRoles. Chủ nhiệm thuộc Memberships. Giao diện giữ Bootstrap/MVC .NET 10 và SQL Server. PB12 đăng ký/hủy, PB13 lịch sử, PB14 điểm danh, PB15 chốt/sửa có truy vết, PB16 đăng thông báo, PB17 hộp thông báo/đã đọc và PB18 thông báo tự động đổi lịch/hủy đã triển khai. Toàn bộ 448 kiểm thử tích hợp đạt.

Migration khởi tạo hai role `Admin`, `Student`. Quản trị viên đầu tiên được tạo riêng bằng lệnh có kiểm soát, không tự tạo khi khởi động website. Chủ nhiệm là vai trò trong từng membership, không phải role hệ thống mới. `/health` kiểm tra tiến trình; `/health/ready` kiểm tra kết nối SQL và migration còn chờ.

Điều hướng mới: thanh ngang giữ **Trang chủ, Câu lạc bộ, Lịch hoạt động, Giới thiệu**. Khi đăng nhập, chọn **Cá nhân** để mở menu bên chứa Thông báo, Đơn của tôi, Thành lập CLB, Tài khoản, Đổi mật khẩu và Đăng xuất. Chủ nhiệm có thêm **CLB tôi quản lý**. Đóng bằng X, Escape hoặc bấm ra ngoài; menu mặc định đóng. Xem [hướng dẫn giao diện và thiết lập màu file trong VS Code](docs/interface-redesign.md#điều-hướng-tối-giản--13112026).

## Yêu cầu môi trường

- .NET SDK 10.0.101 hoặc bản ổn định mới hơn trong nhánh 10.0, theo `global.json`.
- Git.
- SQL Server. Máy phát triển hiện tại dùng SQL Server Express 2022 tại `.\SQLEXPRESS`, kết nối bằng Windows Authentication. Mỗi thành viên xác nhận instance trên máy mình.
- Tài khoản Windows có quyền tạo database khi chạy migration và bộ kiểm thử cục bộ. Đây không phải cấu hình quyền dành cho máy chủ triển khai.

Không cần cài Node.js hoặc tải CSS từ CDN để chạy phiên bản hiện tại. Bootstrap 5.3.3 và các thư viện đi kèm được lấy từ template MVC của .NET SDK; giữ nguyên giấy phép trong `wwwroot/lib`.

## Thiết lập trên máy mới

Mở `StudentClubManagement.sln` bằng IDE hỗ trợ .NET 10, hoặc chạy từ thư mục gốc:

```powershell
dotnet tool restore
dotnet restore StudentClubManagement.sln
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=StudentClubManagement;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True" --project src/StudentClubManagement.Web
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet build StudentClubManagement.sln --configuration Release --no-restore
dotnet ef migrations list --project src/StudentClubManagement.Web --configuration Release --no-build
dotnet ef database update --project src/StudentClubManagement.Web --configuration Release --no-build
```

Kiểm tra đúng instance và database trước khi chạy `database update`. Chỉ áp dụng migration sau khi xem thay đổi trong `Data/Migrations`. Ứng dụng không tự tạo/cập nhật schema lúc khởi động. Nếu thiếu cấu hình kết nối, ứng dụng dừng với hướng dẫn thay vì âm thầm chọn database khác.

Connection string trên chỉ dùng cho máy phát triển với SQL Express. User Secrets nằm ngoài repository và không được đồng bộ cho thành viên khác. Môi trường triển khai dùng cấu hình riêng, ví dụ biến `ConnectionStrings__DefaultConnection`, và chứng chỉ SQL hợp lệ. Không đưa mật khẩu, database file hoặc bản sao lưu vào Git.

EF Core, Identity EF và công cụ `dotnet-ef` được ghim phiên bản 10.0.14. Gói `System.Security.Cryptography.Pkcs` được ghim bản vá 10.0.14 để thay dependency cũ do SqlClient kéo vào. Việc cập nhật NuGet không tự cập nhật .NET runtime đã cài trên máy; kiểm tra runtime khi chuẩn bị triển khai.

## Chạy website

Trong VS Code, mở thư mục gốc chứa `StudentClubManagement.sln` (không chỉ thư mục `src/StudentClubManagement.Web`), rồi chọn **Terminal → New Terminal**. Với máy hiện tại:

```powershell
Set-Location -LiteralPath "C:\Users\ADMIN\Documents\ChatGPT\Công nghệ phầm mềm 2"
```

Chạy từ terminal PowerShell tại thư mục gốc:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project src/StudentClubManagement.Web --configuration Release --launch-profile https
```

Truy cập `https://localhost:7013/Account/Login`. Dừng ứng dụng bằng `Ctrl+C` tại terminal chạy ứng dụng.

Đợi dòng `Now listening on: https://localhost:7013` rồi mới mở trình duyệt. Giữ terminal này chạy; muốn nhập lệnh khác thì mở terminal thứ hai. Đây là website ASP.NET Core: không mở trực tiếp `.cshtml` hoặc dùng nút Go Live của Live Server.

### Khi báo thiếu ConnectionStrings:DefaultConnection

Thông báo này có nghĩa ứng dụng chưa đọc được cấu hình kết nối trong lần chạy đó, chưa chứng minh thiếu .NET hoặc SQL Server. Từ thư mục gốc, lưu kết nối cục bộ cho đúng project:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=StudentClubManagement;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True" --project src/StudentClubManagement.Web
```

Sau đó chạy lại ba lệnh ở phần Chạy website, gồm cả hai biến môi trường Development. User Secrets nằm ngoài thư mục mã nguồn và được nạp trong môi trường phát triển. Không thay mật khẩu Admin hoặc chạy bootstrap lại chỉ để sửa lỗi connection string.

Kiểm tra trên máy ADMIN ngày 30/10/2026: SDK 10.0.101 có sẵn; dịch vụ `MSSQL$SQLEXPRESS` Running; database `StudentClubManagement` có hai migration và Admin đã tạo. Chạy lại lệnh `dotnet run --no-restore` ngay trong thư mục web thành công; trang đăng nhập và readiness HTTP đều trả 200. Không tái hiện được lỗi thiếu cấu hình trong lần kiểm tra này; chưa có bằng chứng để quy lỗi cho một thiết lập cụ thể của terminal VS Code.

Các lệnh khởi chạy HTTPS ở trên cũng đã chạy thành công từ thư mục gốc: trang đăng nhập HTTP 200, `/health/ready` HTTP 200 Healthy, không bỏ qua kiểm tra chứng chỉ. Tiến trình kiểm tra do trợ lý chạy đã được dừng; người dùng tự chạy lệnh trong terminal VS Code để giữ website hoạt động trong phiên làm việc của mình.

Profile `https` đã được kiểm tra trên máy hiện tại và cần development certificate được tin cậy trên từng máy (`dotnet dev-certs https --trust`). Profile `http` tại cổng 5026 chỉ dành cho phát triển cục bộ. Môi trường ngoài Development dùng HTTPS redirection, HSTS và cookie chỉ gửi qua HTTPS.

## Quản trị viên đầu tiên

Trên máy hiện tại đã tạo quản trị viên theo email nhóm trưởng chọn; không cần khởi tạo lại. Để xem thông tin đăng nhập **tại máy của bạn**:

```powershell
.\scripts\Show-LocalAdminLogin.ps1
```

Lệnh trên chạy từ thư mục gốc chứa `StudentClubManagement.sln`; nếu terminal đang ở `src/StudentClubManagement.Web`, dùng `..\..\scripts\Show-LocalAdminLogin.ps1`. Nếu script báo thiếu thông tin quản trị, chạy thêm `-CheckOnly` để xem đường dẫn script/cấu hình và hai cờ `HasAdminEmail`, `HasInitialPassword` mà không in mật khẩu. Script hỗ trợ cả khóa phẳng `BootstrapAdmin:Password` và JSON lồng `BootstrapAdmin.Password`. Không tạo lại Admin hoặc ghi đè User Secrets chỉ để xử lý lỗi đọc cấu hình.

Trường hợp đã chuẩn bị riêng bản khôi phục mã hóa `.local/admin-login-recovery.clixml` cho tài khoản Windows hiện tại: chạy `powershell.exe -NoProfile -File .\scripts\Restore-LocalAdminSecrets.ps1`. Script chỉ bổ sung email/mật khẩu khởi tạo còn thiếu, giữ nguyên các trường khác và từ chối nếu đã có trường email/mật khẩu Admin. Sau khi lưu sẽ hiển thị thông tin qua script xem đăng nhập. Không gọi database hoặc bootstrap, không đặt mật khẩu mới. Bản khôi phục dùng Windows DPAPI qua PSCredential/Export-Clixml, không dùng cho thành viên/máy khác và nằm ngoài Git. Chỉ dọn bản khôi phục sau khi người dùng xác nhận đọc được thông tin và đăng nhập; việc khôi phục cấu hình không chứng minh mật khẩu khởi tạo vẫn là mật khẩu hiện tại nếu từng đổi trên website.

Trên máy mới, sau khi áp dụng migration, chạy từ thư mục gốc (đổi email cho phù hợp):

```powershell
.\scripts\Prepare-LocalAdmin.ps1 -Email "admin@clb.local" -FullName "Quản trị CLB"
dotnet run --project src/StudentClubManagement.Web --configuration Release --launch-profile https -- --bootstrap-admin
```

Script chuẩn bị sinh mật khẩu ngẫu nhiên, truyền qua stdin vào User Secrets. Lệnh `--bootstrap-admin` tạo user và gán role trong cùng transaction rồi thoát; không mở web server. Nếu đã có bất kỳ Admin hoặc email trùng tài khoản hiện có, lệnh từ chối và không đổi mật khẩu/cấp thêm quyền. Script chuẩn bị cũng không ghi đè cấu hình quản trị đã lưu.

User Secrets là cấu hình phát triển cục bộ, **không phải kho mã hóa mật khẩu**. Không chụp mật khẩu vào báo cáo hoặc đưa vào Git. Mỗi thành viên thiết lập trên máy riêng. Xem [thiết kế xác thực](docs/authentication.md) để biết phạm vi và quy tắc phiên đăng nhập.

## Đổi mật khẩu của mình

Đăng nhập → **Tài khoản** → **Đổi mật khẩu**. Nhập mật khẩu hiện tại, mật khẩu mới và nhập lại để xác nhận. Mật khẩu mới cần ít nhất 10 ký tự, có chữ hoa, chữ thường, số, ký tự đặc biệt và khác mật khẩu hiện tại.

Sau khi lưu thành công, thiết bị đang dùng tiếp tục đăng nhập; các phiên cũ trên thiết bị khác bị từ chối ở request tiếp theo và cần đăng nhập bằng mật khẩu mới. Tùy chọn duy trì đăng nhập trên thiết bị hiện tại được giữ nguyên. Nhập sai mật khẩu hiện tại 5 lần sẽ khóa đăng nhập 15 phút và kết thúc phiên đang thực hiện thao tác.

`Show-LocalAdminLogin.ps1` chỉ hiển thị **mật khẩu khởi tạo** đã lưu. Sau khi bạn đổi mật khẩu trên website, script không hiển thị mật khẩu mới; ứng dụng chỉ lưu bản băm và không ghi mật khẩu mới vào User Secrets. Quên mật khẩu/chức năng đặt lại mật khẩu là phần việc riêng, chưa triển khai.

## Chỉnh sửa hồ sơ cá nhân

Khởi động lại ứng dụng sau khi cập nhật mã nguồn → đăng nhập → **Tài khoản → Chỉnh sửa hồ sơ** (`/Account/EditProfile`). Sinh viên và Admin đều sửa hồ sơ của chính mình. Nhập họ tên, số điện thoại, lớp/khoa rồi chọn **Lưu hồ sơ**. Họ tên bắt buộc, tối đa 150 ký tự; các trường khác không bắt buộc. Số điện thoại gồm 7–15 chữ số, có thể có dấu + đầu chuỗi, không dùng khoảng trắng hoặc dấu gạch; lớp tối đa 50, khoa tối đa 100 ký tự.

Email đăng nhập, mã SV, trạng thái và quyền hệ thống không sửa qua form này. **Hủy** quay về trang tài khoản mà không lưu. Nếu form báo hồ sơ đã thay đổi, chọn **Tải lại hồ sơ**, đối chiếu dữ liệu mới rồi sửa lại. Sửa thông tin không đổi mật khẩu hoặc đăng xuất các thiết bị. Số điện thoại chỉ dùng liên hệ, chưa xác minh SMS; dữ liệu này không xuất hiện trong danh mục CLB công khai.

Bước 8 không thêm migration. Xem [thiết kế, kiểm thử và nghiệm thu PB06](docs/personal-profile.md).

## Đăng ký, duyệt và khóa tài khoản sinh viên

Sinh viên vào **Đăng nhập → Đăng ký sinh viên**, tự nhập hồ sơ và mật khẩu. Admin vào **Tài khoản sinh viên → Đăng ký chờ duyệt** để chấp nhận hoặc từ chối (cần lý do). Chỉ tài khoản đã duyệt mới đăng nhập được. Xem [luồng phê duyệt mới và vị trí code](docs/approval-workflows.md).

Sinh viên tự giữ mật khẩu đã đặt và có thể đổi tại **Tài khoản → Đổi mật khẩu**. Website không gửi email tự động hoặc hiển thị lại mật khẩu sau đăng ký.

Để khóa/mở khóa, chọn sinh viên trong danh sách, nhập lý do rồi xác nhận. Thao tác được ghi vào `AuditLogs`; khóa chặn yêu cầu tiếp theo của phiên đang có. Mở khóa yêu cầu đăng nhập mới, không tự xóa thời hạn tạm khóa do nhập sai mật khẩu. Chức năng này không thao tác tài khoản Admin và không xóa hồ sơ/lịch sử thành viên. Xem [hướng dẫn và nghiệm thu PB02](docs/student-accounts.md).

Máy đã có bản bước 4 cần build lại và chạy `dotnet ef database update --project src/StudentClubManagement.Web --configuration Release --no-build` trong môi trường Development để thêm bảng nhật ký. Migration đã được áp dụng trên máy hiện tại; không cần tạo lại Admin.

## Quản lý câu lạc bộ và chủ nhiệm

Admin vào **Quản trị → Quản lý câu lạc bộ → Tạo câu lạc bộ**. Nhập mã CLB, tên và mã sinh viên chủ nhiệm; lĩnh vực/giới thiệu tùy chọn. Người làm chủ nhiệm phải có tài khoản sinh viên đã được duyệt và đang hoạt động. Tạo CLB, tư cách thành viên của chủ nhiệm đầu tiên và nhật ký trong cùng giao dịch. Mã CLB giữ cố định sau khi tạo.

Chủ nhiệm đăng nhập → **CLB tôi quản lý** → **Sửa thông tin**. Chỉ sửa tên, lĩnh vực, giới thiệu của CLB mình phụ trách. Admin sửa được các CLB trong hệ thống. Form cũ sau chỉnh sửa/chuyển giao yêu cầu tải lại; chủ nhiệm cũ mất quyền ngay ở request tiếp theo.

**Chuyển chủ nhiệm** chỉ dành cho Admin, chỉ chọn thành viên Active của chính CLB có tài khoản Student Active, không có quyền Admin. Người cũ trở thành thành viên thường; không kết nạp người ngoài CLB khi chuyển giao. Khi chưa có thành viên khác đủ điều kiện, trang giải thích và không có nút xác nhận. Quy trình xin gia nhập/duyệt đơn được bổ sung ở Sprint 2. Muốn khóa tài khoản đang làm chủ nhiệm, phải chuyển giao tất cả CLB người đó phụ trách trước.

Bước 6 dùng schema hiện có, không thêm migration. Chưa triển khai thao tác tạm dừng/lưu trữ CLB hoặc quy trình gia nhập. Xem [hướng dẫn và kiểm thử bước 6](docs/club-management.md).

## Khám phá câu lạc bộ

Khởi động lại ứng dụng sau khi cập nhật mã nguồn, chọn **Câu lạc bộ** trên menu hoặc truy cập `https://localhost:7013/Clubs`. Không cần đăng nhập. Nhập tên/mã và chọn lĩnh vực → **Tìm kiếm**; chọn **Bỏ lọc** để xem lại toàn bộ. Mỗi trang tối đa 6 CLB; các liên kết chuyển trang giữ bộ lọc. Tìm kiếm không phân biệt hoa/thường nhưng có phân biệt dấu tiếng Việt.

Chọn **Xem chi tiết** để đọc giới thiệu đầy đủ. CLB Active và Suspended xuất hiện với nhãn trạng thái; Archived không xuất hiện và đường dẫn chi tiết trả 404. Mã, tên, lĩnh vực, giới thiệu và trạng thái là thông tin công khai. Danh sách thành viên và thông tin tài khoản không được đưa vào các trang này; form sửa CLB nhắc người quản lý về phạm vi nội dung công khai.

Nếu database ứng dụng chưa có CLB thì danh mục trống là đúng. Sinh viên gửi đơn thành lập CLB để Admin duyệt; Admin cũng có thể tạo CLB trực tiếp cho sinh viên đã được duyệt. Ảnh kiểm tra dùng dữ liệu giả trong database QA riêng, đã dọn sau kiểm tra. Bước 7 không thêm migration và chưa có đăng ký gia nhập. Xem [quy tắc và nghiệm thu PB04](docs/public-clubs.md).

## Kiểm tra nhanh

Khi server đang chạy, mở một terminal khác:

```powershell
(Invoke-WebRequest http://localhost:5026).StatusCode
(Invoke-WebRequest http://localhost:5026/health).Content
(Invoke-WebRequest http://localhost:5026/health/ready).Content
```

Kết quả mong đợi: trang chủ HTTP 200; hai health endpoint trả `Healthy`. `/health/ready` trả HTTP 503 nếu không kết nối được database hoặc còn migration chưa áp dụng. Kiểm tra thêm giao diện màn hình nhỏ, nút menu, liên kết Giới thiệu và điều hướng bằng bàn phím.

## Kiểm thử dữ liệu

```powershell
# Chỉ cần biến này nếu instance khác mặc định .\SQLEXPRESS.
$env:STUDENTCLUB_TEST_SQLSERVER = ".\SQLEXPRESS"
dotnet test StudentClubManagement.sln --configuration Release
```

Bộ kiểm thử tạo database riêng `StudentClubManagement_Tests_<guid>` và dọn sau khi chạy. Không dùng database ứng dụng. Ngoài ràng buộc dữ liệu, test HTTP kiểm tra đăng nhập, lockout, cookie, CSRF, chống chuyển hướng ra ngoài, thu hồi phiên/quyền và bootstrap quản trị. Cần SQL Server đang chạy; không bỏ qua lỗi kết nối hoặc thay bằng InMemory để coi kiểm thử đã đạt.

## Cấu trúc hiện tại

Database có mười hai bảng chính: Users, Roles, Clubs, Memberships, JoinRequests, ClubProposals, AuditLogs, Events, EventRegistrations, Attendances, Announcements, AnnouncementRecipients; EF giữ thêm __EFMigrationsHistory. Attendances có 6 cột, dùng RegistrationId làm khóa chính kiêm khóa ngoại. Announcements có 10 cột, gồm SourceKey chống tạo trùng thông báo tự động. AnnouncementRecipients có 3 cột: hai khóa liên kết và ReadAt lưu thời điểm đọc đầu tiên. Đăng nhập/phân quyền vẫn chỉ có hai bảng Users và Roles. [Giải thích SQL hiện tại](docs/simple-database.md). Migration StreamlineAccountsAndClubProposals chuyển quyền sang Users.RoleId trước khi xóa UserRoles; giữ tài khoản và mật khẩu cũ. Sao lưu trước khi chạy migration, không tự xóa bảng trong SSMS.

```text
StudentClubManagement.sln
src/StudentClubManagement.Web/
  Controllers/HomeController.cs
  Controllers/AccountController.cs
  Controllers/ClubsController.cs         # Danh mục/chi tiết công khai
  Controllers/JoinRequestsController.cs  # Sinh viên gửi, xem và rút đơn của mình
  Controllers/JoinRequestReviewController.cs # Chủ nhiệm/Admin xét duyệt đơn
  Controllers/MembershipsController.cs   # Danh sách, trạng thái, lịch sử và tự rời CLB
  Controllers/ClubManagementController.cs # CLB tôi quản lý và sửa hồ sơ
  Areas/Admin/            # Dashboard, tài khoản sinh viên, tạo CLB/chuyển chủ nhiệm
  ViewModels/Account/
  ViewModels/Clubs/
  Controllers/EventsController.cs # Nháp, công bố, cập nhật, hủy/hoàn thành hoạt động
  Models/                 # ApplicationUser, Club, Membership, JoinRequest, ClubEvent, AuditLog
  Data/                   # DbContext, Fluent configuration, Migrations
  Security/               # Role hệ thống, cấu hình Identity
  Services/               # Giao dịch tạo user/role/audit và thay đổi trạng thái
  Health/                 # Kiểm tra database
  Views/
  wwwroot/
  Program.cs
tests/StudentClubManagement.IntegrationTests/
docs/data-model.md
docs/authentication.md
docs/student-accounts.md
docs/club-management.md
docs/public-clubs.md
docs/personal-profile.md
docs/join-requests.md
docs/join-request-review.md
docs/memberships.md
docs/events.md
docs/activity-calendar.md
docs/implementation-log.md
scripts/                  # Chuẩn bị/xem thông tin quản trị cục bộ
output/Phan_tich_CLB_Agile_Scrum.md
```

Controller nhận request và chọn View; ViewModel chỉ chứa dữ liệu cần cho form/trang. Khu vực công khai dùng `_Layout`; Admin dùng `_AdminLayout` với menu dọc, bao gồm trang quản lý CLB và tài khoản khi đăng nhập Admin. Xem [thiết kế dữ liệu](docs/data-model.md) và [giao diện VinhUni, nguồn hình ảnh](docs/interface-redesign.md).

## Quy ước cộng tác

- Mỗi phần việc nhỏ có tiêu chí hoàn thành, kết quả build và minh chứng kiểm tra.
- Một người khác review trước khi ghép thay đổi vào nhánh chính.
- Theo dõi thay đổi cơ sở dữ liệu bằng migration. Không sửa/xóa migration đã được chia sẻ và áp dụng; tạo migration mới khi cần đổi schema.
- Giữ thông tin đăng nhập và cấu hình riêng của máy ngoài Git; dùng User Secrets hoặc biến môi trường.
- Không ghi nhận một PBI hoàn thành chỉ vì khung ứng dụng đã chạy. Bước này mới là một phần công việc nền tảng của Sprint 1.

## Bước tiếp theo

PB11–PB18 đã có lịch, đăng ký/hủy, tra cứu lịch sử, điểm danh/chốt, đăng thông báo, hộp thông báo cá nhân và thông báo tự động đổi lịch/hủy. Sinh viên xem kết quả điểm danh tại **Hoạt động đã đăng ký → Kết quả điểm danh**, xem thông báo tại **Thông báo** trên menu. Xem [hướng dẫn PB15](docs/attendance-finalization.md), [PB16](docs/announcements.md), [PB17](docs/notification-inbox.md) và [PB18](docs/event-change-notifications.md). Bước kế tiếp là **PB19: thống kê CLB và thành viên hiện tại**.

PB10 đã có tạo nháp, công bố, sửa trước giờ bắt đầu, hủy có lý do và hoàn thành sau giờ kết thúc; xem [hướng dẫn code, SQL và sử dụng hoạt động](docs/events.md). PB12 đã tích hợp kiểm tra sức chứa theo số đăng ký; PB18 đã tích hợp snapshot/thông báo thay đổi cùng giao dịch. Cần nhóm review/nghiệm thu các phần đã triển khai trước khi đánh dấu Done.

PB09 đã triển khai danh sách, tìm kiếm/lọc, đình chỉ/khôi phục/kết thúc, tự rời và lịch sử trạng thái; xem [hướng dẫn thành viên](docs/memberships.md). PB12 đã tích hợp xử lý đăng ký nội bộ tương lai. Các phần đã triển khai cần nhóm nghiệm thu theo hướng dẫn tương ứng. Trước khi chốt Sprint 1 cần review phần đã làm và cập nhật việc PB05 tạm dừng/lưu trữ còn phụ thuộc đơn chờ/hoạt động. Chưa đánh dấu toàn bộ PB05 hoặc Sprint 1 Done.

## Đăng ký tham gia hoạt động — PB12

Sinh viên: **Lịch hoạt động → chọn hoạt động → Đăng ký tham gia**. Quản lý đăng ký tại **Tài khoản → Hoạt động đã đăng ký**. Admin/chủ nhiệm xem **Chi tiết hoạt động → Danh sách đăng ký**. Kiểm tra hạn, sức chứa và quyền nội bộ ở server; hủy trả lại chỗ và giữ lịch sử. Xem [hướng dẫn, SQL và nghiệm thu PB12](docs/event-registrations.md). Migration mới: 20261110043425_AddEventRegistrations.

PB13 bổ sung bộ lọc cho hai danh sách và **Lịch sử đăng ký** trên từng thẻ cá nhân. Không thêm bảng/cột hoặc migration. Xem [thao tác, code MVC/SQL và nghiệm thu PB13](docs/registration-history.md).

## Điểm danh hoạt động — PB14

Admin/chủ nhiệm mở **Chi tiết hoạt động → Điểm danh**. Từ giờ bắt đầu có thể ghi Có mặt/Vắng/Có phép, trước chốt có thể đưa về Chưa điểm danh. Tìm tên/mã SV và lọc kết quả. Người chưa được ghi không tự coi là vắng. Lưu người thực hiện, thời gian và nhật ký cùng giao dịch. Xem [quy tắc, MVC/SQL và nghiệm thu](docs/attendance.md). PB14 dùng migration **20261110103415_AddAttendances**.

PB15 thêm **Chốt danh sách điểm danh** sau giờ kết thúc: cần xử lý hết Unmarked hoặc xác nhận chuyển sang Vắng; sau chốt cố định danh sách và bắt buộc lý do khi sửa kết quả. Xem [hướng dẫn chốt và kết quả cá nhân](docs/attendance-finalization.md). Migration PB15: **20261110120624_FinalizeAttendance**; không thêm bảng.

## Thông báo theo đối tượng — PB16

Admin chọn **Quản lý thông báo** ở menu trái; chủ nhiệm chọn **CLB tôi quản lý → Thông báo**. Soạn nháp cho thành viên CLB hoặc người đăng ký hoạt động, xem trước số người nhận rồi đăng. Hệ thống giữ tập người nhận tại thời điểm đăng, chống bấm trùng và lưu audit. Nội dung đã đăng không sửa; đính chính bằng thông báo mới. Lưu trữ cần lý do và vẫn giữ quyền đọc của người đã nhận.

Migration PB16: **20261110151453_AddAnnouncements**, thêm Announcements (9 cột) và AnnouncementRecipients (ban đầu 2 cột, PB17 bổ sung ReadAt). Không sửa dữ liệu các bảng cũ. Đã sao lưu/kiểm tra bản sao lưu và áp dụng trên máy hiện tại. Máy khác áp dụng migration theo phần thiết lập.

Xem [hướng dẫn thao tác, code MVC, SQL và nghiệm thu PB16](docs/announcements.md). Không gửi email hoặc tự động thông báo đổi lịch/hủy ở PB16.

## Hộp thông báo và đã đọc — PB17

Sinh viên/chủ nhiệm chọn **Thông báo** trên menu hoặc **Tài khoản → Thông báo của tôi**. Tìm tiêu đề, lọc CLB, lọc Đã đọc/Chưa đọc. Bấm **Xem thông báo** chỉ mở nội dung; chọn **Đánh dấu đã đọc** để xác nhận. Số chưa đọc giảm sau thao tác/tải lại; bấm lặp giữ thời điểm đầu tiên. Thông báo đã nhận vẫn tồn tại sau khi rời CLB, hủy đăng ký hoặc lưu trữ.

Admin/chủ nhiệm quản lý xem **Chi tiết thông báo → Xem người nhận** để đối chiếu số đã đọc. Không được đánh dấu thay người khác. Migration PB17: **20261111011414_AddAnnouncementReadAt**, chỉ thêm cột ReadAt, không thêm bảng. Hướng dẫn sử dụng, quyền và SQL: [PB17](docs/notification-inbox.md). Khởi động lại Web sau cập nhật.

## Thông báo tự động đổi lịch/hủy — PB18

Admin/chủ nhiệm sửa giờ bắt đầu, kết thúc, hạn đăng ký hoặc địa điểm của hoạt động đã công bố → nhập lý do → lưu. Người còn đăng ký nhận một thông báo gồm thay đổi trước/sau và lý do. Khi hủy, hệ thống giữ danh sách người nhận trước khi hủy đăng ký; gửi lại form cũ không tạo trùng thông báo. Sinh viên đọc tại **Thông báo**. Quản lý xem **Thông báo tự động gần nhất** ở chi tiết hoạt động.

Migration mới nhất: **20261111013356_AddEventNoticeSource**, chỉ thêm SourceKey vào Announcements và các ràng buộc. Vẫn 12 bảng; không sửa Users/Roles. Không gửi email/push. Xem [hướng dẫn thao tác, code và SQL PB18](docs/event-change-notifications.md).
