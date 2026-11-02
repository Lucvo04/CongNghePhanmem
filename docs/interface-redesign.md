# Giao diện VinhUni Clubs — 02/11/2026

## Phạm vi

Điều chỉnh giao diện theo yêu cầu nhóm trưởng trước khi tiếp tục PB07. Giữ ASP.NET Core MVC, Razor và Bootstrap 5.3.3 hiện có; không thêm CMS, thư viện hay migration. Bố cục quản trị tham khảo hình WordPress do người dùng gửi, chỉ giữ các mục có chức năng thực.

- Trang chủ: nhận diện xanh dương, logo trường, ảnh sinh viên, nhóm sở thích, ba bài giới thiệu hoạt động và phần giới thiệu Đại học Vinh.
- Menu công khai, danh mục CLB, trang tài khoản dùng cùng màu sắc; footer có địa chỉ và liên kết chính thức.
- Admin: sidebar tối có thu gọn trên desktop; menu trượt trên mobile, thanh trên cùng, bốn ô thống kê, năm CLB mới tạo, sáu thao tác gần đây và liên kết thao tác nhanh.
- Mục Đổi mật khẩu được đặt trực tiếp trong sidebar. Không thay đổi mật khẩu hoặc quyền hiện có.

## Layout và dữ liệu

`Views/Shared/_AdminLayout.cshtml` áp dụng cho toàn bộ `Areas/Admin`. `Views/_ViewStart.cshtml` chọn layout này cho `ClubManagement` và `Account` khi principal có role Admin. Trang chủ và danh mục công khai vẫn dùng layout công khai kể cả khi Admin truy cập. Student giữ giao diện công khai và các chức năng theo quyền cũ.

Dashboard chỉ đọc database, bảo vệ bằng role Admin và no-store:

| Chỉ số | Định nghĩa |
|---|---|
| Câu lạc bộ | Mọi CLB, gồm Active, Suspended, Archived |
| CLB đang hoạt động | Status = Active |
| Tài khoản sinh viên | Có Student, không có Admin; gồm tài khoản bị khóa |
| Lượt thành viên | Membership Active và EndedAt rỗng, đếm theo lượt tại CLB, không phải số người duy nhất |

Danh sách mới tạo sắp CreatedAt rồi Id giảm dần. CLB lưu trữ không có link sửa trong dashboard. Hoạt động gần đây chỉ hiển thị tên thao tác và thời gian UTC+7, không hiển thị BeforeData/AfterData/Reason hoặc thông tin cá nhân trong audit. Dữ liệu trống có hướng dẫn bắt đầu, không thay bằng thống kê giả.

Các bài Nhịp sống VinhUni là nội dung giới thiệu chọn lọc, có ngày bài viết/tháng sự kiện và dẫn nguồn. Đây chưa phải phân hệ quản lý sự kiện, không tự đồng bộ tin mới hoặc tạo bản ghi sự kiện/CLB trong database.

## Nguồn hình ảnh và thông tin

Ngày truy cập: 02/11/2026. Ảnh được lưu tại `wwwroot/images/vinh/` để trang không phụ thuộc tải ảnh từ website bên ngoài mỗi lần mở. Tác quyền hình ảnh/logo thuộc chủ sở hữu nguồn; không gán giấy phép mở cho các tài sản này. Website hiển thị nguồn và ghi rõ đây là đồ án học phần, không phải website chính thức của trường.

| Tệp | Nguồn bài/trang | URL ảnh gốc |
|---|---|---|
| logo.png | [Cổng thông tin Trường Đại học Vinh](https://vinhuni.edu.vn/pages/) | https://vinhuni.edu.vn/pages/assets/images/vu-logo.png |
| community.jpg | [The Beginning 2026](https://vpdangdoan.vinhuni.edu.vn/hoat-dong-doan-the/doan-thanh-nien-hoi-sinh-vien/seo/the-beginning-2026-khoi-dau-hanh-trinh-ket-noi-sang-tao-cua-tuoi-tre-vinhuni-146242) | https://vpdangdoan.vinhuni.edu.vn/data/134/upload/1/images//2026/10/beginning4.jpg |
| beginning.jpg | Cùng bài The Beginning 2026 ở trên | https://vpdangdoan.vinhuni.edu.vn/data/134/upload/1/images//2026/10/beginning7.jpg |
| welcome.jpg | [Đồng hành cùng tân sinh viên K67](https://hoisinhvien.vinhuni.edu.vn/tin-tuc-su-kien/seo/tuoi-tre-truong-dai-hoc-vinh-dong-hanh-ho-tro-tan-sinh-vien-khoa-67-146018) | https://hoisinhvien.vinhuni.edu.vn/data/69/upload/651/images//2026/10/1791086773042_184500223262762917_93161792747856224_06b6244fe46a37887df53ee813c9bda0.jpg |
| club-day.jpg | [Ngày hội CLB và Hàng cây Thanh niên tại cơ sở II](https://banqlcs2.vinhuni.edu.vn/tin-tuc-su-kien/seo/tuoi-tre-truong-dai-hoc-vinh-soi-noi-ngay-hoi-cau-lac-bo-doi-nhom-va-ra-mat-cong-trinh-hang-cay-thanh-nien-tai-co-so-ii-143932) | https://vinhuni.edu.vn/Upload/images/HCTH/2026-03/NgayhoiCLB(2).jpg |

Năm thành lập 1959 tham khảo [khái quát về trường](https://tuyensinh.vinhuni.edu.vn/gioi-thieu/seo/khai-quat-ve-truong-dai-hoc-vinh-61489). Địa chỉ lấy từ footer Cổng TT Đại học Vinh tại ngày truy cập: 182 Lê Duẩn, Phường Trường Vinh, Tỉnh Nghệ An. Bài hội trại đăng 12/10/2026, hoạt động diễn ra 11/10/2026; bài K67 đăng 03/10/2026. Nội dung trên đồ án được tóm tắt, không sao chép toàn bài.

## Cách chạy và review

Từ thư mục gốc dự án, dừng tiến trình chạy cũ bằng Ctrl+C trong terminal của nó rồi chạy:

```powershell
dotnet run --project .\src\StudentClubManagement.Web --launch-profile http
```

Mở URL `Now listening on` trong terminal (profile http hiện dùng `http://localhost:5026`). Tải lại bằng Ctrl+F5 nếu trình duyệt còn giữ trang cũ. Không cần cài thêm package hoặc chạy migration cho lần đổi giao diện này.

Các điểm nhóm trưởng cần review:

1. Trang chủ: logo/ảnh có tải; liên kết tin dẫn đến website trường; nút khám phá mở `/Clubs`.
2. Đăng nhập Admin → `/Admin`: số liệu phản ánh database hiện có. Database chưa có CLB thì số 0 là đúng.
3. Mở Tài khoản sinh viên, Quản lý CLB, Tài khoản của tôi, Đổi mật khẩu từ sidebar; mục đang xem được đánh dấu.
4. Thu gọn/mở rộng sidebar desktop. Mobile mở nút menu, đóng bằng nút X hoặc Escape.
5. Kiểm tra danh mục và các form hiện có trên điện thoại. Bảng rộng được cuộn trong vùng riêng.

Kiểm thử tự động và trình duyệt được ghi tại `docs/implementation-log.md`. Ảnh dashboard trong `.local/qa/vinh-*` dùng dữ liệu QA giả, không phải thống kê hoạt động thực của trường. Chưa đánh dấu Sprint Review/UAT hoàn tất thay cho nhóm.