# Bước 7 — Khám phá câu lạc bộ công khai (PB04)

Ngày thực hiện: 30/10/2026 (Asia/Bangkok). Thuộc Sprint 1; đã triển khai và kiểm thử cục bộ, chờ thành viên khác review/nhóm trưởng nghiệm thu. Chưa ghi nhận Sprint Review hoặc UAT của nhóm đã diễn ra.

## Mục tiêu và cách dùng

Khách và sinh viên tìm CLB phù hợp qua tên/mã, lĩnh vực và nội dung giới thiệu mà không cần đăng nhập. Khởi động lại ứng dụng bằng hướng dẫn README, chọn **Câu lạc bộ** trên menu hoặc **Khám phá câu lạc bộ** ở trang chủ.

1. Nhập tên hoặc mã CLB, chọn lĩnh vực nếu cần rồi bấm **Tìm kiếm**.
2. Danh sách hiển thị tối đa 6 CLB/trang, tổng số kết quả và vị trí trang. **Trang trước/Trang sau** giữ nguyên bộ lọc; tìm kiếm mới bắt đầu lại từ trang đầu.
3. Chọn **Xem chi tiết** để đọc đầy đủ giới thiệu. Liên kết **Khám phá CLB khác** quay về danh mục không lọc; nút Back của trình duyệt quay về kết quả trước đó.
4. **Bỏ lọc** mở lại toàn bộ danh mục. Khi chưa có dữ liệu hoặc không khớp, trang giải thích và gợi ý tìm lại.

Database ứng dụng hiện chưa có CLB. Admin dùng chức năng bước 5 cấp tài khoản sinh viên, rồi bước 6 tạo CLB kèm chủ nhiệm. CLB Active mới tạo xuất hiện ngay trong danh mục. Không cần migration mới và không cần tạo lại Admin.

## Quy tắc nghiệp vụ

| Nội dung | Hành vi đã triển khai |
| --- | --- |
| Active | Hiển thị danh sách và chi tiết, nhãn Đang hoạt động |
| Suspended | Vẫn hiển thị để người xem biết tình trạng, nhãn Tạm dừng; phần tham gia giải thích chưa tiếp nhận đăng ký |
| Archived | Ẩn khỏi danh sách/lĩnh vực; chi tiết trả 404 với thông báo chung, kể cả người xem là Admin |
| Nội dung công khai | Id dùng cho đường dẫn, mã, tên, lĩnh vực, giới thiệu, trạng thái |
| Dữ liệu nội bộ | Không đưa thành viên, tên chủ nhiệm, email, mã SV, lớp/khoa, điện thoại, RowVersion hoặc trường Identity vào trang công khai |
| Tìm tên/mã | Khớp chuỗi con, bỏ khoảng trắng hai đầu, không phân biệt hoa/thường, có phân biệt dấu |
| Lọc lĩnh vực | Khớp toàn bộ lĩnh vực sau trim, không phân biệt hoa/thường; danh sách lựa chọn chỉ lấy từ CLB công khai |
| Giá trị trống | Lĩnh vực hiện Chưa phân loại; giới thiệu hiện Thông tin giới thiệu đang được cập nhật |
| Tham số không hợp lệ | Search dài hơn 150, category dài hơn 100, page không phải số nguyên: HTTP 400 và thông báo dễ hiểu |
| Trang vượt giới hạn | Số trang âm về 1; số trang lớn hơn kết quả về trang cuối; không gây tràn phép tính Skip |
| Nội dung HTML | Razor encode; giới thiệu là văn bản thuần, giữ xuống dòng; không render HTML người dùng nhập |

Quyết định triển khai: schema hiện chưa có bản nháp/cờ xuất bản, nên Active/Suspended được xem là công khai. Form tạo/sửa CLB đã nhắc người quản lý về phạm vi này. Mã/tên/lĩnh vực/giới thiệu không nên chứa thông tin cá nhân nội bộ. Nếu cần duyệt xuất bản sau này, phải bổ sung trạng thái và quy trình riêng.

## Phân chia MVC

| Thành phần | Trách nhiệm |
| --- | --- |
| `Controllers/ClubsController.cs` | GET công khai, kiểm tra tham số, điều kiện xuất hiện, tìm/lọc, phân trang, trả 400/404 |
| `ViewModels/Clubs/PublicClubViewModels.cs` | Chỉ chứa dữ liệu công khai cần hiển thị; không truyền entity Identity/Membership ra View |
| `Views/Clubs/Index.cshtml` | Form tìm/lọc, kết quả, trạng thái rỗng, thẻ CLB và phân trang |
| `Views/Clubs/Details.cshtml` | Giới thiệu đầy đủ và trạng thái tham gia hiện tại |
| `Views/Clubs/NotFound.cshtml` | Thông báo chung, không phân biệt CLB lưu trữ với ID không tồn tại |
| `_Layout`, `Home/Index`, `ClubManagement/Index` | Điều hướng công khai và liên kết xem CLB từ khu vực quản lý |

Truy vấn dùng EF Core `AsNoTracking`, điều kiện Active/Suspended dùng chung, chiếu DTO trước khi lấy kết quả. Sắp xếp tên rồi Id để phân trang ổn định. Không join bảng thành viên/tài khoản. Cache response đặt no-store vì layout thay đổi theo phiên và trạng thái CLB cần được đọc ở request tiếp theo.

Mã CLB có collation `Latin1_General_100_CI_AS` ở cột; truy vấn tên/lĩnh vực dùng collation này một cách tường minh. Tìm chuỗi con và lấy danh mục lĩnh vực phù hợp quy mô đồ án hiện tại; chưa kiểm thử tải và chưa có tìm không dấu/full-text. Không suy từ số test đạt ra cam kết thời gian đáp ứng với dữ liệu lớn. Tham khảo [EF Core về collation và chỉ mục](https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity).

## Kết quả kiểm tra thực tế

- Thêm 18 trường hợp trong `PublicClubTests`, tổng **126/126 đạt**, 0 lỗi, 0 bỏ qua. SQL Server thật và HTTP/Razor thật trên database kiểm thử riêng. TRX `.local/test-results/step-7.trx`; log `.local/step-7-tests.log`.
- Kiểm tra tìm tên tiếng Việt/mã khác hoa thường, trim, kết hợp bộ lọc, phân trang giữ tham số và không trùng kết quả, trang quá lớn/âm, tham số lỗi, trạng thái rỗng, lĩnh vực trùng và dữ liệu nullable.
- Kiểm tra Archived không lộ qua danh sách/lĩnh vực/URL trực tiếp; thay trạng thái sẽ ẩn ở request kế tiếp; thêm tham số includeArchived không vượt điều kiện server. Dữ liệu cá nhân giả không xuất hiện trong HTML kể cả khi người xem đăng nhập Admin.
- Kiểm tra encode HTML, ký tự `%`/`_` là từ khóa thuần, chuỗi giống SQL injection không làm thay đổi truy vấn. Toàn bộ kiểm thử quyền của khu vực quản lý trước đó vẫn đạt.
- Build Release cuối **0 warning, 0 error**, log `.local/step-7-build.log`. Một cảnh báo analyzer trong test đã sửa sang `Assert.Single`; hành vi assertion không đổi. EF xác nhận không có model change thiếu migration.
- Browser QA dùng database tạm riêng có 8 CLB giả: 6 Active, 1 Suspended, 1 Archived. Danh mục hiển thị 7, chia 6 + 1; tìm `lập TRÌNH` ra đúng 1, lọc Nghệ thuật ra 2. Đã mở chi tiết đầy đủ và chi tiết CLB tạm dừng, thử menu điện thoại và xem dữ liệu thiếu giới thiệu/lĩnh vực.
- Viewport kiểm tra 1366×1000, 1024×900, 390×844; không phát hiện tràn ngang theo DOM; menu thu gọn hiện ở tablet/điện thoại. Không thấy console error/warning. Readiness preview HTTP 200 Healthy.
- Ảnh `.local/qa/public-clubs-desktop.png`, `public-clubs-mobile.png`, `public-club-details-desktop.png`, `public-club-details-mobile.png` dùng dữ liệu QA, không phải dữ liệu trường. Tab/viewport đã dọn, server preview đã dừng và database tạm đã xóa sau xác minh đúng tên sở hữu. Database ứng dụng vẫn 1 user, 0 CLB, 0 membership.

## Checklist nhóm nghiệm thu

- [ ] Thành viên khác đọc controller/ViewModel, đối chiếu điều kiện công khai và quyền nội bộ không bị thay.
- [ ] Chạy trên máy thứ hai bằng README; tạo dữ liệu CLB của nhóm qua UI quản trị trên môi trường phát triển.
- [ ] Tìm tên/mã có dấu, lọc lĩnh vực, bỏ lọc, chuyển trang, mở chi tiết và thử menu điện thoại.
- [ ] Xác nhận không thấy thông tin tài khoản hoặc thành viên trong HTML công khai.
- [ ] Nhóm trưởng ghi nhận chấp nhận PB04 hoặc bổ sung feedback vào Product Backlog.

Các checkbox là việc nhóm cần làm, không phải kết quả đã thực hiện thay nhóm. Gia nhập/duyệt đơn thuộc Sprint 2; chưa có nút đăng ký giả. Màn hình đổi trạng thái tạm dừng/lưu trữ và điều kiện đơn chờ/sự kiện còn ở PB05. Bước nhỏ tiếp theo: PB06 chỉnh sửa hồ sơ cá nhân.
