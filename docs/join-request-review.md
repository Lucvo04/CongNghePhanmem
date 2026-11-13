# PB08 — Xét duyệt đơn gia nhập

Triển khai và kiểm thử cục bộ ngày 03/11/2026; chờ nhóm review/nghiệm thu. Phần này tiếp nối PB07; không bao gồm quản lý trạng thái thành viên PB09 hoặc thông báo tự động.

## Cách sử dụng

1. Đăng nhập bằng tài khoản chủ nhiệm đang hoạt động → **CLB tôi quản lý** → chọn **Đơn gia nhập** ở CLB mình phụ trách. Admin đi từ **Quản lý câu lạc bộ** và có quyền xử lý mọi CLB theo cơ chế quản lý hiện tại.
2. Mặc định danh sách lọc **Chờ xử lý**, 10 đơn/trang, đơn gửi trước hiển thị trước. Có thể lọc tất cả hoặc từng trạng thái.
3. Chọn **Xem và xử lý**, đọc lý do, chọn **Chấp nhận** hoặc **Từ chối**. Từ chối bắt buộc nhập lý do, tối đa 500 ký tự.
4. Bấm **Xác nhận quyết định**. Chấp nhận tạo một thành viên Active với vai trò Member và kết thúc đơn trong cùng transaction. Từ chối không tạo thành viên.
5. Sinh viên vào **Đơn của tôi** để thấy kết quả, người/thời điểm xử lý và lý do từ chối. Sau khi bị từ chối có thể nộp đơn mới nếu còn đủ điều kiện; đơn cũ được giữ.

Đơn đã xử lý không thể quyết định lại. Nếu dữ liệu vừa thay đổi, trang báo xung đột, cần tải lại trước khi thao tác. Mở màn hình xác nhận hoặc bấm Quay lại không làm thay đổi dữ liệu.

## Điều kiện và quyền

- Giữ chính sách ClubAccessService: Admin Active quản lý toàn bộ; Student Active có Membership Manager Active chưa kết thúc quản lý đúng CLB. Không dựa vào việc ẩn nút để bảo vệ dữ liệu.
- Người ngoài CLB, thành viên thường, chủ nhiệm CLB khác hoặc người đã mất quyền không xem/xử lý được đơn. Kiểm tra lại quyền sau khi lấy khóa CLB, nên chuyển chủ nhiệm làm mất hiệu lực form đang mở của người cũ.
- Không tự xét duyệt đơn của mình, kể cả khi sau đó được nâng quyền.
- Chấp nhận chỉ khi CLB Active, người gửi còn là Student Active không có Admin, chưa có đợt Membership mở (Active hoặc Suspended). Nếu từng Left, tạo đợt tham gia mới.
- CLB Suspended: không chấp nhận thành viên mới, vẫn cho từ chối kèm lý do. CLB Archived: chỉ đọc lịch sử. Người gửi vẫn có thể rút Pending theo PB07.
- Đơn phải Pending và RowVersion khớp. Duyệt/duyệt hoặc duyệt/rút đồng thời chỉ một kết quả có hiệu lực.

## MVC và SQL để giải thích khi bảo vệ

| File (trong src/StudentClubManagement.Web) | Vai trò |
|---|---|
| Controllers/JoinRequestReviewController.cs | GET danh sách/chi tiết, POST quyết định; kiểm tra dữ liệu form, trả View/404/409 |
| Services/JoinRequestReviewService.cs | Quyền theo CLB, điều kiện chấp nhận, transaction, khóa nghiệp vụ, tạo Membership và audit |
| ViewModels/JoinRequests/ReviewJoinRequestViewModels.cs | Form chỉ nhận quyết định, lý do, RowVersion; người gửi/người xử lý không lấy từ form |
| Views/JoinRequestReview/Index.cshtml, Decide.cshtml | Danh sách, bộ lọc, xác nhận xử lý và lịch sử |
| Models/JoinRequest.cs | Bổ sung Approved = 3, Rejected = 4; giữ nguyên Pending = 1, Withdrawn = 2 |
| Data/Configurations/JoinRequestConfiguration.cs | Ràng buộc và liên kết lưu kết quả xét duyệt |

Không tạo thêm bảng mới. `JoinRequests` bổ sung ba cột:

- `DecidedById`: FK Users, người quyết định. `ClosedAt` sẵn có là thời điểm quyết định (hoặc thời điểm rút với Withdrawn).
- `DecisionReason`: lý do từ chối, nullable, tối đa 500 ký tự. Bắt buộc với Rejected; null với các trạng thái còn lại.
- `MembershipId`: FK Memberships, chỉ có khi Approved; unique khi khác null, liên kết đúng đợt thành viên được tạo bởi quyết định.

Check constraint bắt buộc trạng thái và dữ liệu quyết định phù hợp. FK Restrict bảo vệ lịch sử. RowVersion phát hiện form cũ; unique index của Memberships bảo vệ một đợt thành viên mở trong cùng CLB. Service bảo đảm Membership thuộc đúng người gửi và CLB, không nhận MembershipId từ client.

Trong transaction: đọc đối tượng trong phạm vi quyền → khóa tài khoản người xử lý/người gửi theo thứ tự cố định → khóa CLB → đọc lại quyền/trạng thái → tạo thành viên nếu chấp nhận → lưu kết quả đơn + audit → commit. Lỗi ở bất kỳ bước lưu nào làm rollback cả thành viên, đơn và audit. Không sao chép nội dung lý do riêng tư vào AuditLogs.

Migration `20261102233544_AddJoinRequestDecisions` giữ đơn cũ, thêm cột nullable và mở rộng constraint. Down từ chối nếu có đơn Approved/Rejected, tránh làm mất lịch sử quyết định. Không sửa migration PB07 đã áp dụng.

## Nhóm nghiệm thu

- Chủ nhiệm đúng CLB chấp nhận một đơn: chỉ một Membership Member Active được tạo, sinh viên thấy Đã chấp nhận.
- Từ chối không nhập lý do: báo lỗi, đơn vẫn Pending. Nhập lý do: sinh viên thấy nội dung và không có Membership mới.
- Mở form ở hai tab, xử lý một tab: tab còn lại không ghi đè được quyết định.
- Sinh viên rút trước khi chủ nhiệm xác nhận: không tạo thành viên.
- Thay ID sang đơn CLB khác: không xem hoặc xử lý được.
- Chuyển chủ nhiệm trước khi form cũ gửi: người cũ mất quyền.
- Kiểm tra chế độ điện thoại, bộ lọc, lịch sử và nội dung dài.

PB08 chỉ được nhóm đánh dấu Done sau review/nghiệm thu. Cập nhật 04/11/2026: phần danh sách và trạng thái thành viên PB09 đã được triển khai ở bước kế tiếp, xem [hướng dẫn thành viên](memberships.md). PB09 còn phụ thuộc quy tắc đăng ký sự kiện tương lai.

## Kết quả kỹ thuật

- Lượt toàn bộ: 194/194 tests đạt, gồm 20 ca xét duyệt mới và 174 ca trước đó. Sau bổ sung hai ca bảo vệ form cũ/rollback migration và dọn warning, chạy lại 25/25 ca xét duyệt + CompactIdentity đạt (22 ca PB08, 3 ca Identity). Tổng cộng 196 ca khác nhau đã đạt; không cộng dồn các ca chạy lại thành số ca mới.
- Build Release cuối: 0 warning, 0 error; EF không có thay đổi model thiếu migration.
- Browser với database riêng: chủ nhiệm chấp nhận; lọc Tất cả thấy đơn đã xử lý; từ chối thiếu lý do bị chặn; từ chối hợp lệ; sinh viên nhìn thấy cả hai kết quả, người/thời điểm/lý do. Kiểm tra màn hình hẹp không tràn ngang, console không warning/error. Ảnh lưu trong `.local/qa/join-review-form.png`, `join-review-results.png`, `join-review-mobile.png`.
- Đã dừng server thử và xóa đúng database QA. Database đồ án không chứa hai đơn hoặc các tài khoản kiểm thử.
- Đã sao lưu COPY_ONLY CHECKSUM và RESTORE VERIFYONLY trước khi áp dụng migration; metadata ở `.local/review-backup.json`. Fingerprint dữ liệu cũ của cả bảy bảng khớp trước/sau (với JoinRequests so sánh các cột có từ PB07). Database thật vẫn có 0 đơn tại thời điểm cập nhật.
- Smoke với database thật: readiness 200 Healthy, danh mục 200, đường dẫn xét duyệt yêu cầu đăng nhập. Server smoke đã dừng; người dùng chạy lại ứng dụng bằng lệnh trong README.
- Bằng chứng: `.local/test-results/join-review.trx`, `join-review-final.trx`, `.local/review-final-build.log`, `.local/review-model-check.log`, `.local/review-live-migration.log`, `.local/review-before.txt`, `.local/review-after.txt`, `.local/review-live-smoke-checks.json`.
