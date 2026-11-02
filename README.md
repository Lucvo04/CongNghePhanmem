# Website quản lý câu lạc bộ sinh viên

Đồ án Công nghệ phần mềm của nhóm 3 thành viên, triển khai theo ASP.NET Core MVC và Agile Scrum.

## Trạng thái hiện tại

Bước 8 trong Sprint 1: đã có xác thực, quản lý tài khoản sinh viên, tạo/sửa CLB, chuyển chủ nhiệm, danh mục CLB công khai và chỉnh sửa hồ sơ cá nhân. Giao diện tiếng Việt dùng chung Bootstrap trên ASP.NET Core MVC .NET 10, EF Core SQL Server. Toàn bộ 146 kiểm thử tích hợp đạt.

Migration khởi tạo hai role `Admin`, `Student`. Quản trị viên đầu tiên được tạo riêng bằng lệnh có kiểm soát, không tự tạo khi khởi động website. Chủ nhiệm là vai trò trong từng membership, không phải role hệ thống mới. `/health` kiểm tra tiến trình; `/health/ready` kiểm tra kết nối SQL và migration còn chờ.

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

## Cấp và khóa tài khoản sinh viên

Đăng nhập Admin → **Quản trị → Quản lý tài khoản sinh viên** (`/Admin/Students`). Chọn **Cấp tài khoản**, nhập họ tên, mã sinh viên, email, mật khẩu ban đầu và xác nhận. Lớp/khoa tùy chọn. Hệ thống yêu cầu email và mã sinh viên duy nhất; mã giữ số 0 đầu, không chứa khoảng trắng bên trong.

Bàn giao mật khẩu riêng cho sinh viên và hướng dẫn đổi mật khẩu tại **Tài khoản → Đổi mật khẩu**. Website không gửi email, không hiển thị lại mật khẩu sau khi tạo và chưa bắt buộc đổi mật khẩu lần đầu.

Để khóa/mở khóa, chọn sinh viên trong danh sách, nhập lý do rồi xác nhận. Thao tác được ghi vào `AuditLogs`; khóa chặn yêu cầu tiếp theo của phiên đang có. Mở khóa yêu cầu đăng nhập mới, không tự xóa thời hạn tạm khóa do nhập sai mật khẩu. Chức năng này không thao tác tài khoản Admin và không xóa hồ sơ/lịch sử thành viên. Xem [hướng dẫn và nghiệm thu PB02](docs/student-accounts.md).

Máy đã có bản bước 4 cần build lại và chạy `dotnet ef database update --project src/StudentClubManagement.Web --configuration Release --no-build` trong môi trường Development để thêm bảng nhật ký. Migration đã được áp dụng trên máy hiện tại; không cần tạo lại Admin.

## Quản lý câu lạc bộ và chủ nhiệm

Admin vào **Quản trị → Quản lý câu lạc bộ → Tạo câu lạc bộ**. Nhập mã CLB, tên và mã sinh viên chủ nhiệm; lĩnh vực/giới thiệu tùy chọn. Cần cấp tài khoản sinh viên đang hoạt động trước. Tạo CLB, tư cách thành viên của chủ nhiệm đầu tiên và nhật ký trong cùng giao dịch. Mã CLB giữ cố định sau khi tạo.

Chủ nhiệm đăng nhập → **CLB tôi quản lý** → **Sửa thông tin**. Chỉ sửa tên, lĩnh vực, giới thiệu của CLB mình phụ trách. Admin sửa được các CLB trong hệ thống. Form cũ sau chỉnh sửa/chuyển giao yêu cầu tải lại; chủ nhiệm cũ mất quyền ngay ở request tiếp theo.

**Chuyển chủ nhiệm** chỉ dành cho Admin, chỉ chọn thành viên Active của chính CLB có tài khoản Student Active, không có quyền Admin. Người cũ trở thành thành viên thường; không kết nạp người ngoài CLB khi chuyển giao. Khi chưa có thành viên khác đủ điều kiện, trang giải thích và không có nút xác nhận. Quy trình xin gia nhập/duyệt đơn được bổ sung ở Sprint 2. Muốn khóa tài khoản đang làm chủ nhiệm, phải chuyển giao tất cả CLB người đó phụ trách trước.

Bước 6 dùng schema hiện có, không thêm migration. Chưa triển khai thao tác tạm dừng/lưu trữ CLB hoặc quy trình gia nhập. Xem [hướng dẫn và kiểm thử bước 6](docs/club-management.md).

## Khám phá câu lạc bộ

Khởi động lại ứng dụng sau khi cập nhật mã nguồn, chọn **Câu lạc bộ** trên menu hoặc truy cập `https://localhost:7013/Clubs`. Không cần đăng nhập. Nhập tên/mã và chọn lĩnh vực → **Tìm kiếm**; chọn **Bỏ lọc** để xem lại toàn bộ. Mỗi trang tối đa 6 CLB; các liên kết chuyển trang giữ bộ lọc. Tìm kiếm không phân biệt hoa/thường nhưng có phân biệt dấu tiếng Việt.

Chọn **Xem chi tiết** để đọc giới thiệu đầy đủ. CLB Active và Suspended xuất hiện với nhãn trạng thái; Archived không xuất hiện và đường dẫn chi tiết trả 404. Mã, tên, lĩnh vực, giới thiệu và trạng thái là thông tin công khai. Danh sách thành viên và thông tin tài khoản không được đưa vào các trang này; form sửa CLB nhắc người quản lý về phạm vi nội dung công khai.

Nếu database ứng dụng chưa có CLB thì danh mục trống là đúng. Admin cần cấp tài khoản sinh viên rồi tạo CLB và chủ nhiệm bằng chức năng bước 6. Ảnh kiểm tra dùng dữ liệu giả trong database QA riêng, đã dọn sau kiểm tra. Bước 7 không thêm migration và chưa có đăng ký gia nhập. Xem [quy tắc và nghiệm thu PB04](docs/public-clubs.md).

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

```text
StudentClubManagement.sln
src/StudentClubManagement.Web/
  Controllers/HomeController.cs
  Controllers/AccountController.cs
  Controllers/ClubsController.cs         # Danh mục/chi tiết công khai
  Controllers/ClubManagementController.cs # CLB tôi quản lý và sửa hồ sơ
  Areas/Admin/            # Dashboard, tài khoản sinh viên, tạo CLB/chuyển chủ nhiệm
  ViewModels/Account/
  ViewModels/Clubs/
  Models/                 # ApplicationUser, Club, Membership, AuditLog
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
docs/implementation-log.md
scripts/                  # Chuẩn bị/xem thông tin quản trị cục bộ
output/Phan_tich_CLB_Agile_Scrum.md
```

Controller nhận request và chọn View; ViewModel chỉ chứa dữ liệu cần cho form/trang; layout dùng chung cho khu vực công khai, tài khoản và Admin. Xem [thiết kế dữ liệu](docs/data-model.md) để phân biệt ràng buộc đã có với nghiệp vụ chưa triển khai.

## Quy ước cộng tác

- Mỗi phần việc nhỏ có tiêu chí hoàn thành, kết quả build và minh chứng kiểm tra.
- Một người khác review trước khi ghép thay đổi vào nhánh chính.
- Theo dõi thay đổi cơ sở dữ liệu bằng migration. Không sửa/xóa migration đã được chia sẻ và áp dụng; tạo migration mới khi cần đổi schema.
- Giữ thông tin đăng nhập và cấu hình riêng của máy ngoài Git; dùng User Secrets hoặc biến môi trường.
- Không ghi nhận một PBI hoàn thành chỉ vì khung ứng dụng đã chạy. Bước này mới là một phần công việc nền tảng của Sprint 1.

## Bước tiếp theo

PB06 đã triển khai và kiểm thử cục bộ, chờ nhóm review/nhóm trưởng nghiệm thu theo `docs/personal-profile.md`. Bước triển khai tiếp theo dự kiến PB07: gửi, rút và theo dõi đơn gia nhập; sau đó PB08 duyệt đơn/tạo thành viên. Trước khi chốt Sprint 1 cần review phần đã làm và cập nhật việc PB05 tạm dừng/lưu trữ còn phụ thuộc đơn chờ/hoạt động. Chưa đánh dấu toàn bộ PB05 hoặc Sprint 1 Done.
