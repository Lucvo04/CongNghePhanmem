# Tự đăng ký tài khoản và đề nghị thành lập CLB

Phiên bản 09/11/2026. Mỗi tài khoản có một quyền hệ thống; người gửi đơn thành lập được duyệt là chủ nhiệm đầu tiên.

## Cách sử dụng

1. Khách vào **Đăng nhập → Đăng ký sinh viên**, điền họ tên, mã sinh viên, email, lớp/khoa và tự đặt mật khẩu. Gửi thành công tạo tài khoản Pending, chưa đăng nhập được.
2. Admin vào **Tài khoản sinh viên → Đăng ký chờ duyệt → Duyệt đăng ký**. Chấp nhận cho phép đăng nhập; từ chối phải ghi lý do. Admin không xem hoặc đặt hộ mật khẩu.
3. Sinh viên đã duyệt đăng nhập, vào **Thành lập CLB → Đề nghị thành lập CLB**, điền tên, lĩnh vực, mục đích và kế hoạch.
4. Admin vào **Duyệt thành lập CLB → Xem đơn**, chọn chấp nhận/từ chối. Khi chấp nhận, nhập mã CLB duy nhất; khi từ chối, ghi lý do.
5. Chấp nhận tạo CLB Active và Membership Manager Active cho người gửi trong cùng giao dịch. “CLB tôi quản lý” xuất hiện ở lần tải trang tiếp theo, không cần gán thêm quyền hệ thống.
6. Sinh viên xem lịch sử/lý do xử lý. Đơn đang chờ có thể rút; sau rút/từ chối có thể nộp đơn mới. Mỗi sinh viên tối đa một đơn thành lập đang chờ.

Đơn gia nhập CLB đã có vẫn ở “Đơn của tôi”; đây là nghiệp vụ khác với thành lập CLB mới.

## Quy tắc

- Email/mã sinh viên duy nhất, không phân biệt hoa/thường. Mã giữ số 0 đầu.
- Form đăng ký không nhận RoleId, Status, Id từ trình duyệt; luôn tạo Student/Pending.
- Pending/Rejected/Disabled không đăng nhập được. Màn hình khóa/mở khóa không thể bỏ qua bước duyệt.
- Hồ sơ tài khoản bị từ chối vẫn giữ mã/email và lịch sử. Phiên bản này chưa có tự sửa/nộp lại hồ sơ đăng ký tài khoản; sinh viên liên hệ Admin khi cần đối chiếu.
- Mỗi đơn thành lập chỉ có một quyết định cuối. Duyệt trùng hoặc biểu mẫu cũ yêu cầu tải lại.
- Người gửi phải còn là Student Active tại lúc chấp nhận. Người gửi bị khóa/gỡ quyền không được trở thành chủ nhiệm qua đơn cũ.
- Sinh viên chỉ đọc và rút đơn của mình. Chỉ Admin được xét duyệt, không tự duyệt đơn của chính mình.
- Tạo CLB, thêm chủ nhiệm, cập nhật đơn và audit cùng thành công hoặc cùng rollback.
- Giữ chức năng Admin tạo CLB trực tiếp cho CLB đã tồn tại; bỏ chức năng Admin cấp tài khoản sinh viên.
- Tài khoản/CLB cũ giữ nguyên, không tự đưa Active về Pending.
- Chưa gửi email tự động ở bước này; theo dõi kết quả trên website hoặc liên hệ Admin.

## Vị trí code trong src/StudentClubManagement.Web

| Nhiệm vụ | File |
|---|---|
| Form đăng ký | Views/Account/Register.cshtml; ViewModels/Account/StudentRegistrationForm.cs |
| Nhận GET/POST đăng ký | Controllers/AccountController.cs — Register |
| Tạo tài khoản chờ duyệt và xét duyệt | Services/StudentRegistrationService.cs |
| Admin nhận GET/POST xét hồ sơ | Areas/Admin/Controllers/StudentsController.cs — Review |
| Giao diện duyệt tài khoản | Areas/Admin/Views/Students/Review.cshtml |
| Dữ liệu đơn thành lập | Models/ClubProposal.cs; Data/Configurations/ClubProposalConfiguration.cs |
| Giao diện và nhận biểu mẫu đơn | Controllers/ClubProposalsController.cs; Views/ClubProposals |
| Gửi/rút/chấp nhận/từ chối đơn | Services/ClubProposalService.cs |
| Giao dịch và xử lý lỗi dùng chung | Services/WorkflowTransaction.cs |
| Khóa thao tác đồng thời ở SQL | Services/ClubOperationLocks.cs |
| Quyền chủ nhiệm/menu | Security/ClubAccessService.cs — IsManagerAsync; Views/Shared/_Layout.cshtml |

Controller đọc ViewModel và trả View/Redirect; Service kiểm tra nghiệp vụ và transaction; EF Core chuyển thao tác model thành SQL. Không bind trực tiếp entity Users/ClubProposals cho lệnh ghi.

## Đơn giản hóa code

Đã bỏ CreateStudentRequest và phương thức cấp tài khoản cũ. StudentAccountService còn truy vấn sinh viên và khóa/mở khóa. StudentRegistrationForm gom chuẩn hóa/validation; WorkflowTransaction gom commit/rollback và lỗi xung đột cho hai luồng mới. CoreUserStore dùng RoleId thay cho bảng nối, vẫn dùng UserManager/EF store cho mật khẩu.

Không bỏ kiểm tra quyền, CSRF, concurrency và transaction chỉ để giảm số dòng: các phần này giữ chức năng ổn định.

## Kịch bản trình diễn

- Đăng ký → đăng nhập khi chưa duyệt bị chặn → Admin duyệt → đăng nhập được.
- Sinh viên thường không có menu chủ nhiệm.
- Gửi đơn thành lập → Admin duyệt → có CLB mới và menu quản lý.
- Từ chối một đơn khác → có lý do, không tạo CLB.
- Duyệt từ hai cửa sổ cùng một đơn → chỉ một quyết định thành công.
- Xem SQL: Users.RoleId, Roles chỉ Id/Name, không có UserRoles; ClubProposals.ClubId chỉ có sau duyệt.

Xem [giải thích SQL](simple-database.md). Kiểm thử bằng tài khoản giả và database riêng; không đổi mật khẩu Admin thật.
