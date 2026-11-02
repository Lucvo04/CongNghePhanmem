# Bước 6 — CLB, chủ nhiệm và quyền theo CLB

Ngày triển khai: 30/10/2026. Mục tiêu vòng triển khai: tạo CLB có người phụ trách ngay, sửa hồ sơ đúng quyền và chuyển giao không bỏ trống chủ nhiệm. Đây là một phần PB05 và phần quyền hồ sơ CLB của PB03, không phải hoàn tất toàn Sprint 1.

## Quyết định nghiệp vụ đã được người dùng chọn

**Chuyển chủ nhiệm chỉ chọn thành viên đang hoạt động của chính CLB.** Không tự kết nạp người ngoài khi chuyển giao. Đồng thời người nhận phải có tài khoản Student Active, không mang role Admin và khác chủ nhiệm hiện tại.

Tạo CLB mới là trường hợp khởi tạo: Admin chọn sinh viên theo mã, hệ thống tạo CLB và membership Manager Active đầu tiên trong một transaction. Nếu bất kỳ bước nào lỗi thì không có CLB hoặc membership dở dang.

## Luồng sử dụng

1. Admin cấp tài khoản Student qua bước 5, hoặc dùng sinh viên đã có.
2. Vào **Quản trị → Quản lý câu lạc bộ → Tạo câu lạc bộ**. Mã CLB duy nhất (viết hoa sau trim), tên bắt buộc; lĩnh vực và giới thiệu tùy chọn. Mã dùng chữ không dấu/số/gạch ngang/gạch dưới, tối đa 30 ký tự và không đổi sau khi tạo. Nhập mã sinh viên của chủ nhiệm đầu tiên.
3. Sinh viên được phân công đăng nhập, vào **CLB tôi quản lý**. Chỉ CLB mình phụ trách xuất hiện; được sửa tên, lĩnh vực, giới thiệu. Admin có thể sửa các CLB trong hệ thống. View không nhận hoặc cập nhật Code/Status/ClubId do client chèn thêm.
4. Admin chọn **Chuyển chủ nhiệm**. Danh sách chỉ gồm thành viên đủ điều kiện; nhập lý do và xác nhận. Người cũ trở thành Member, người mới trở thành Manager; tư cách thành viên và ngày gia nhập của cả hai giữ nguyên. Không đổi mật khẩu hay role hệ thống.
5. Tài khoản cũ mất quyền quản lý CLB từ request tiếp theo, kể cả POST form mở sẵn. Khi dữ liệu CLB đổi, form mang RowVersion cũ nhận HTTP 409 và yêu cầu tải lại.
6. Muốn khóa tài khoản người còn phụ trách CLB: chuyển giao tất cả CLB trước, rồi quay lại bước 5 để khóa.

**Giới hạn hiện tại:** CLB mới chỉ có chủ nhiệm đầu tiên. Nếu chưa có thành viên khác Active, trang chuyển giao hiển thị chưa đủ điều kiện và không có nút xác nhận. Luồng xin gia nhập/duyệt đơn được triển khai ở Sprint 2; không cung cấp đường tắt kết nạp trong bước 6. Bộ kiểm thử dùng fixture tạo các thành viên giả trong database kiểm thử để kiểm chứng chuyển giao.

## Cấu trúc MVC và phân quyền

| Thành phần | Trách nhiệm |
|---|---|
| Admin/ClubsController | Form tạo và chuyển giao, bảo vệ bằng role Admin, CSRF, no-store |
| ClubManagementController | Danh sách quản lý theo quyền, form sửa hồ sơ; không phải danh mục công khai |
| ClubAccessService | Truy vấn quyền theo tài khoản/role/membership/ClubId hiện tại |
| ClubManagementService | Kiểm tra nghiệp vụ, tạo/sửa/chuyển trong transaction, RowVersion và audit |
| ClubOperationLocks | Khóa giao dịch SQL theo tài khoản/CLB, dùng chung với khóa tài khoản |
| ViewModels/Clubs và Views | Trường được phép nhập, validation tiếng Việt, layout Bootstrap chung |

Mọi lần sửa kiểm tra lại quyền sau khi lấy khóa CLB trong transaction. Việc ẩn nút chỉ phục vụ UI; đổi ID trực tiếp trên GET/POST không vượt được kiểm tra server. ClubId lấy từ route; các thuộc tính hiển thị lấy lại từ database, không bind từ form.

## Tính nguyên tử và đồng thời

- Tạo: giữ khóa tài khoản chủ nhiệm → đọc lại trạng thái/role → kiểm tra mã → lưu CLB và membership → lưu audit → commit. Unique index là lớp bảo vệ cuối cho hai yêu cầu cùng mã.
- Chuyển: giữ khóa tài khoản người nhận → khóa CLB → kiểm tra RowVersion và tư cách thành viên hiện tại → hạ vai trò cũ và lưu → nâng vai trò mới, chạm phiên bản CLB, ghi audit → commit. Hai lần SaveChanges cùng transaction để tuân unique index một chủ nhiệm. Lỗi audit cũng rollback bước hạ vai trò đã lưu.
- Khóa tài khoản dùng cùng khóa theo tài khoản, đọc lại các membership chủ nhiệm trước khi cập nhật Status/SecurityStamp. Vì vậy tạo/chuyển giao đồng thời với khóa không để người Disabled trở thành chủ nhiệm.
- Sửa hồ sơ giữ khóa theo CLB, kiểm tra quyền và RowVersion sau khóa. Hai lần chuyển giao cùng phiên bản chỉ một lần thành công. SQL application lock thuộc transaction, tự giải phóng khi commit/rollback; chờ tối đa 5 giây rồi báo xung đột để tải lại.
- Không dùng khóa trong bộ nhớ tiến trình. Khi bổ sung cập nhật membership ở bước sau, phải giữ cùng quy ước tài khoản trước, CLB sau khi ảnh hưởng quyền chủ nhiệm. Việc sửa SQL trực tiếp ngoài service không được bảo vệ bởi giao thức ứng dụng.

Theo [tài liệu Microsoft về sp_getapplock](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql), khóa có chủ sở hữu Transaction cần chạy trong transaction. Cách áp dụng ở đây được kiểm chứng bằng các HTTP request đồng thời trên SQL Server thật.

Schema không thay đổi. Audit dùng bảng hiện có: ClubCreated ghi mã CLB và ID người/membership chủ nhiệm; ClubManagerTransferred ghi người/membership trước-sau, lý do và người thực hiện; ClubProfileUpdated ghi tên, lĩnh vực và hash phần giới thiệu trước-sau. Hash không phải bản sao văn bản và không thể dùng để phục hồi nội dung cũ.

## Kết quả kiểm thử cục bộ

**108/108 đạt, 0 lỗi, 0 bỏ qua**: 79 ca trước + 29 ca ở `ClubManagementTests`. Build Release **0 warning, 0 error**; không có model change thiếu migration. `/health/ready` HTTPS trả 200 Healthy.

| Nhóm | Đã kiểm chứng |
|---|---|
| Quyền và CSRF | Khách/Student bị chặn tạo/chuyển; thiếu token bị chặn; form no-store |
| Tạo CLB | Đúng một chủ nhiệm Active và audit; chống chèn Status/Id/người phụ trách khác |
| Dữ liệu không hợp lệ | Thiếu trường, mã sai/trùng khác hoa thường, mã sinh viên lạ/Disabled/Admin bị từ chối |
| Quyền theo CLB | Chủ nhiệm A không đọc/sửa B qua thay ID; Student thường không sửa; danh sách lọc theo quyền |
| Sửa hồ sơ | Bảo vệ trường cố định, từ chối RowVersion cũ; Unicode tối đa và nội dung HTML được xử lý dưới dạng văn bản |
| Chuyển giao | Giữ hai đợt tham gia; đổi đúng role; ghi trước/sau/lý do; phiên cũ mất quyền cả GET và POST |
| Người nhận không hợp lệ | Người ngoài, thành viên đình chỉ/đã rời, thuộc CLB khác, Disabled hoặc dual-role bị từ chối; không tự thêm membership |
| Khóa chủ nhiệm | Chặn cho đến khi đã chuyển tất cả CLB phụ trách |
| Tranh chấp | Cùng mã CLB, hai lần chuyển cùng phiên bản, phân công/chuyển đồng thời với khóa tài khoản |
| Rollback | Cưỡng bức lỗi audit trong database thử: tạo, sửa và chuyển đều không để dữ liệu dở dang |

Test dùng SQL Server và HTTP/cookie/Razor thật; fixture kiểm tra đúng database ngẫu nhiên trước khi ghi/xóa. Không tạo sinh viên/CLB giả vào database ứng dụng. Dữ liệu cục bộ sau QA: 1 Admin, 0 CLB, 0 membership; Admin Active, AccessFailedCount 0. Mật khẩu Admin không thay đổi.

Browser QA xem Dashboard → danh sách → form tạo, validation trống, nút Hủy và menu mobile; form ở 1366×1000 và 390×844, không tràn ngang, không thấy lỗi/warning JavaScript. Luồng có dữ liệu thực hiện bằng integration tests, chưa tạo/chuyển CLB thật qua browser. Ảnh tại `.local/qa/clubs-create-desktop.png`, `clubs-create-mobile.png`, `clubs-list-desktop.png`.

Log: `.local/step-6-tests.log`, `.local/step-6-build.log`; TRX `.local/test-results/step-6.trx`. Thư mục `.local` không đưa vào Git. Server preview do trợ lý chạy được dừng sau QA; mở lại bằng lệnh chạy ở README.

## Review của nhóm và backlog còn lại

Trạng thái phần triển khai bước 6: **chờ review/nhóm trưởng nghiệm thu**, không ghi nhận cuộc họp Review hay UAT của nhóm đã diễn ra.

- Thành viên review mã kiểm tra quyền tại controller/service, thứ tự khóa, transaction và đường rollback.
- Thành viên kiểm thử chạy suite trên máy khác; dùng database demo riêng để diễn tập tạo → sửa đúng/sai CLB → chuyển → người cũ bị chặn → khóa tài khoản sau chuyển hết CLB.
- Nhóm trưởng đối chiếu quyết định chỉ chuyển trong thành viên Active và ghi kết quả nghiệm thu thực tế.

PB05 còn tạm dừng/lưu trữ, đặc biệt điều kiện đơn chờ và hoạt động tương lai chưa có module để xử lý. PB03 còn kiểm tra theo sự kiện/thông báo ở Sprint sau. PB04 danh mục/chi tiết CLB công khai đã triển khai ở bước 7, xem `public-clubs.md`. Menu CLB tôi quản lý có liên kết xem công khai cho CLB chưa lưu trữ; form hồ sơ nhắc tên/lĩnh vực/giới thiệu sẽ được công khai khi Active hoặc Suspended. PB06 sửa hồ sơ cá nhân đã có ở bước 8 (`personal-profile.md`); tiếp theo dự kiến PB07 đơn gia nhập. Chưa đánh dấu Sprint 1 Done.
