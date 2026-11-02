# Phân tích website quản lý câu lạc bộ sinh viên

Đặc tả yêu cầu và kế hoạch Agile Scrum cho đồ án Công nghệ phần mềm

Nhóm thực hiện: 3 thành viên, trong đó nhóm trưởng phụ trách điều phối và ưu tiên sản phẩm. Thời gian mục tiêu: 6 tuần; phương án mở rộng: tối đa 8 tuần. Phiên bản kế hoạch: 1.0, ngày 29/10/2026.

Website được đề xuất quản lý nhiều câu lạc bộ trong một trường, phục vụ xuyên suốt quy trình tìm hiểu CLB, gia nhập, tổ chức hoạt động, đăng ký tham dự, điểm danh và theo dõi kết quả. Phạm vi cốt lõi gồm bảy nhóm chức năng trong đề bài: đăng nhập và phân quyền, quản lý CLB, đăng ký CLB, sự kiện và hoạt động, điểm danh, thông báo, thống kê. Quản lý thành viên và đăng ký tham dự sự kiện được đặc tả bổ sung để các quy trình này hoạt động đầy đủ.

Bản kế hoạch chọn 4 Sprint, mỗi Sprint 10 ngày lịch, để triển khai trong 40 ngày và dành 2 ngày còn lại cho trình bày, bàn giao. Nếu thống nhất thời hạn 8 tuần trước khi bắt đầu, dùng 4 Sprint, mỗi Sprint 14 ngày. Các con số giờ công, Story Point, quy mô dữ liệu và ngưỡng hiệu năng dưới đây là ước lượng hoặc mục tiêu đề xuất, phải được nhóm xác nhận trong Sprint Planning và kiểm chứng khi thực hiện. Đây là kế hoạch triển khai, chưa phải bằng chứng website đã được xây dựng hoặc các Sprint đã hoàn thành.

## 1 Mô tả đề tài và vấn đề cần giải quyết

Tên đề tài: Xây dựng website quản lý câu lạc bộ sinh viên theo phương pháp Agile và khung Scrum.

Hệ thống cung cấp một nơi tập trung để sinh viên xem thông tin CLB, gửi đơn gia nhập, xem lịch và đăng ký hoạt động. Ban chủ nhiệm quản lý hồ sơ CLB, xét đơn, theo dõi danh sách thành viên, tổ chức sự kiện, điểm danh và công bố thông báo. Quản trị viên nhà trường quản lý danh mục CLB, tài khoản, quyền truy cập và xem số liệu tổng hợp.

Các vấn đề cần khảo sát gồm danh sách thành viên bị phân tán giữa bảng tính và nhóm chat, đơn đăng ký khó theo dõi, thông báo thay đổi lịch dễ bị bỏ sót, đăng ký vượt sức chứa và báo cáo phải tổng hợp thủ công. Đây là các giả thuyết nghiệp vụ cần kiểm chứng với người dùng; không xem chúng là kết quả khảo sát đã có.

Giá trị sản phẩm nằm ở tính nhất quán của dữ liệu. Một lần duyệt đơn phải tạo đúng một tư cách thành viên; một lần đăng ký không được chiếm nhiều suất; số liệu tham dự phải dựa trên danh sách đã chốt điểm danh. Nhờ đó, người quản lý có thể giải thích nguồn gốc của báo cáo và xử lý sai sót.

Luồng giá trị chính: sinh viên tìm CLB → gửi đơn → chủ nhiệm duyệt → sinh viên trở thành thành viên → đăng ký hoạt động → được điểm danh → kết quả xuất hiện trong báo cáo.

## 2 Mục tiêu và tiêu chí thành công

Product Goal: Đến cuối đợt phát triển, nhóm cung cấp một website dùng được trong môi trường trình diễn của trường, giúp sinh viên và ban chủ nhiệm hoàn thành quy trình từ gia nhập CLB đến tổng hợp hoạt động trên dữ liệu thống nhất và quyền truy cập đúng phạm vi.

| Mã | Mục tiêu | Bằng chứng nghiệm thu dự kiến |
| --- | --- | --- |
| G01 | Quản lý tập trung thành viên và CLB | Thực hiện trọn luồng nộp đơn, duyệt, xem thành viên, rời CLB; không sinh bản ghi trùng |
| G02 | Tổ chức và tham gia hoạt động | Công bố sự kiện, hiển thị lịch, đăng ký và hủy đúng hạn; không vượt sức chứa |
| G03 | Ghi nhận và truyền đạt kết quả | Điểm danh, chốt danh sách, đăng thông báo đúng người nhận; có lịch sử sửa |
| G04 | Cung cấp số liệu có thể đối chiếu | Dashboard và CSV khớp với bộ dữ liệu kiểm thử và công thức đã thống nhất |
| G05 | Bảo vệ dữ liệu theo quyền | Toàn bộ ca kiểm thử truy cập trái phạm vi CLB trong bộ test bị từ chối |
| G06 | Bàn giao sản phẩm có thể tái tạo | Người nhận dựng được ứng dụng theo README, chạy migration, nạp dữ liệu mẫu và khôi phục bản sao lưu |

Mục tiêu trải nghiệm đề xuất: ít nhất 4/5 người dùng thử hoàn thành được việc tìm sự kiện và đăng ký trong 2 phút, không cần người hướng dẫn thao tác hộ. Nhóm ghi nhận cả người không hoàn thành và nguyên nhân. Mẫu nhỏ này chỉ hỗ trợ cải tiến giao diện, không đại diện cho toàn trường.

Không đưa các tuyên bố như “giảm 70% thời gian quản lý” vào kết luận khi chưa đo quy trình trước và sau. Có thể đo trung vị thời gian duyệt một đơn hoặc lập báo cáo bằng cùng một kịch bản để đánh giá sau này.

## 3 Phạm vi và các giả định cần xác nhận

### 3.1 Phạm vi bản cốt lõi trong 6 tuần

- Một trường, nhiều CLB; một sinh viên có thể tham gia nhiều CLB.
- Quản trị viên cấp tài khoản sinh viên; người dùng đăng nhập, đăng xuất, đổi mật khẩu và xem hồ sơ cơ bản. Không xây hệ thống tự xác minh danh tính sinh viên trong phiên bản đầu.
- Danh mục CLB công khai; tạo, sửa, tạm dừng, lưu trữ CLB và chỉ định chủ nhiệm.
- Sinh viên nộp hoặc rút đơn gia nhập; chủ nhiệm duyệt hoặc từ chối; xem danh sách thành viên và kết thúc tư cách thành viên.
- Tạo, công bố, đổi lịch, hủy sự kiện; lịch dạng danh sách theo ngày/tháng, có bộ lọc.
- Đăng ký sự kiện tự động xác nhận khi đúng điều kiện và còn chỗ; hủy trước hạn; không dùng quy trình duyệt sự kiện nhiều cấp.
- Điểm danh thủ công bởi người quản lý của đúng CLB, chốt kết quả và sửa có lý do.
- Thông báo trong ứng dụng, danh sách người nhận và trạng thái đã đọc; thông báo thay đổi lịch hoặc hủy hoạt động.
- Thống kê thành viên hiện tại, hoạt động, lượt đăng ký, số người có mặt, tỷ lệ tham dự; xuất CSV theo quyền.
- Giao diện đáp ứng điện thoại và máy tính; kiểm thử, triển khai môi trường demo, dữ liệu mẫu, sao lưu và tài liệu sử dụng.

### 3.2 Phần mở rộng khi có thời gian

Ưu tiên khôi phục mật khẩu tự phục vụ, nhập danh sách thành viên có xem trước lỗi, QR điểm danh và email. Chỉ chọn tối đa một hoặc hai mục sau khi đo năng lực và bảo đảm bản cốt lõi đạt Definition of Done. Không mặc định thêm toàn bộ phần mở rộng nếu có 8 tuần.

SSO của trường, thu hội phí và thanh toán, chứng nhận, điểm rèn luyện chính thức, đặt phòng dùng chung, chat thời gian thực và ứng dụng di động riêng nằm ngoài phạm vi đợt này. Những chức năng này cần đối tác, quy định hoặc tích hợp bổ sung.

### 3.3 Quyết định nghiệp vụ đề xuất

| Vấn đề | Chính sách cho bản cốt lõi | Người xác nhận |
| --- | --- | --- |
| Ai quản lý tài khoản | Quản trị viên cấp tài khoản từ danh sách được phép sử dụng | Đại diện trường và PO |
| Ai tạo CLB | Quản trị viên; chủ nhiệm chỉ sửa nội dung CLB được giao | PO và đại diện CLB |
| Ai được đăng ký hoạt động | Từng sự kiện chọn toàn trường hoặc thành viên CLB đang hoạt động | Đại diện CLB |
| Hủy đăng ký | Cho phép trước hạn đóng đăng ký; sau hạn liên hệ người tổ chức | PO và đại diện CLB |
| Rời CLB khi còn sự kiện nội bộ | Chặn rời nếu còn đăng ký nội bộ chưa diễn ra; người dùng hủy trước, hoặc quản lý xử lý và ghi lý do | Đại diện CLB |
| Đình chỉ thành viên | Quản lý hủy các đăng ký nội bộ chưa bắt đầu trong cùng giao dịch, có lý do; giữ lịch sử đã diễn ra; chuyển chủ nhiệm trước nếu cần | Đại diện CLB |
| Lưu trữ CLB | Chỉ khi đã xử lý đơn chờ và sự kiện tương lai; giữ dữ liệu lịch sử | Quản trị viên |
| Dữ liệu trình diễn | Dữ liệu giả lập, không dùng danh sách sinh viên thật khi chưa được cho phép | Nhóm trưởng |

Các quyết định này được PO ghi vào nhật ký quyết định. Nếu thay đổi, phải cập nhật đồng thời yêu cầu, tiêu chí chấp nhận, dữ liệu và kiểm thử liên quan.

## 4 Đối tượng sử dụng và phân quyền

| Đối tượng | Nhu cầu chính | Phạm vi dữ liệu |
| --- | --- | --- |
| Khách chưa đăng nhập | Tìm hiểu CLB và hoạt động công khai | Thông tin đã công bố, không có danh sách sinh viên |
| Sinh viên | Gia nhập CLB, đăng ký sự kiện, xem thông báo và lịch sử cá nhân | Hồ sơ của mình và nội dung được phép truy cập |
| Thành viên CLB | Nhận thông báo nội bộ, dự hoạt động dành cho thành viên | CLB mà tư cách thành viên đang có hiệu lực |
| Chủ nhiệm hoặc cán bộ quản lý CLB | Duyệt đơn, quản lý thành viên, sự kiện, điểm danh và báo cáo | Các CLB được phân công |
| Quản trị hệ thống | Quản lý tài khoản, CLB, phân công chủ nhiệm và vận hành | Toàn hệ thống, các thao tác nhạy cảm có nhật ký |
| Đại diện nhà trường và giảng viên | Góp ý nghiệp vụ, đánh giá kết quả đồ án | Báo cáo hoặc phiên trình diễn được cung cấp |

Giảng viên là bên liên quan, không tự động trở thành Product Owner hay người dùng có quyền quản trị. Nếu trường cần vai trò chỉ xem báo cáo, bổ sung thành backlog riêng; không cấp tài khoản quản trị chỉ để xem thống kê.

| Thao tác | Khách | Sinh viên | Quản lý CLB | Quản trị |
| --- | --- | --- | --- | --- |
| Xem CLB công khai | Có | Có | Có | Có |
| Nộp đơn và đăng ký sự kiện | Không | Cho mình | Cho mình | Theo tư cách sinh viên nếu có |
| Đọc hoặc sửa hồ sơ cá nhân | Không | Của mình | Của mình | Quản lý tài khoản theo nhiệm vụ |
| Duyệt đơn và xem danh sách riêng | Không | Không | Đúng CLB | Có khi có nhiệm vụ hỗ trợ |
| Tạo sự kiện, thông báo, điểm danh | Không | Không | Đúng CLB | Có, ghi nhận người thực hiện |
| Xem lịch sử tham dự cá nhân | Không | Của mình | Đúng CLB khi cần quản lý | Theo nhiệm vụ |
| Xuất danh sách chi tiết | Không | Không | Đúng CLB | Toàn hệ thống |
| Tạo CLB, giao chủ nhiệm | Không | Không | Không | Có |

Quyền quản lý CLB gắn với cặp người dùng và CLB. Người quản lý CLB A vẫn có thể chỉ là thành viên CLB B. Mọi API kiểm tra quyền với tài nguyên thực tế; không tin role hoặc club_id do trình duyệt gửi lên. Nguyên tắc này phù hợp hướng dẫn kiểm tra quyền trên từng yêu cầu và từ chối mặc định của [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html).

Sự kiện SCHOOL đã công bố được xem công khai; sự kiện CLUB chỉ thành viên đang hoạt động và người quản lý đúng CLB được xem. Sau khi rời hoặc bị đình chỉ, sinh viên vẫn xem được thông báo đã nhận và phần thông tin cần thiết trong lịch sử đăng ký của mình, nhưng không nhận thông báo CLB mới và không có quyền quản trị. Không áp quyền thành viên hiện tại để xóa quyền xem toàn bộ lịch sử cá nhân.

## 5 Yêu cầu chức năng

| Mã | Yêu cầu có thể kiểm tra | Backlog liên quan |
| --- | --- | --- |
| FR01 | Cấp và khóa tài khoản; đăng nhập, đăng xuất; đổi mật khẩu có kiểm tra mật khẩu hiện tại | PB01, PB02 |
| FR02 | Sinh viên xem, sửa các trường hồ sơ được cho phép; không tự sửa mã SV hay vai trò | PB06 |
| FR03 | Kiểm soát quyền theo vai trò hệ thống và quan hệ với CLB; thu hồi quyền có hiệu lực ngay ở yêu cầu tiếp theo | PB03 |
| FR04 | Xem, tìm kiếm, lọc danh sách CLB và trang chi tiết công khai | PB04 |
| FR05 | Tạo, sửa, tạm dừng, lưu trữ CLB và chuyển giao chủ nhiệm hợp lệ | PB05 |
| FR06 | Nộp, rút và theo dõi đơn gia nhập; không có hai đơn chờ cùng CLB | PB07 |
| FR07 | Duyệt hoặc từ chối đơn; lưu người quyết định, thời điểm và lý do từ chối | PB08 |
| FR08 | Tìm thành viên, xem trạng thái, rời hoặc đình chỉ; lưu lịch sử hiệu lực | PB09 |
| FR09 | Tạo bản nháp, công bố, sửa lịch, hủy hoặc hoàn thành hoạt động | PB10 |
| FR10 | Xem lịch và lọc theo thời gian, CLB; xem lịch đăng ký cá nhân | PB11, PB13 |
| FR11 | Đăng ký, hủy đúng hạn; chống trùng, kiểm tra đối tượng và sức chứa khi có yêu cầu đồng thời | PB12 |
| FR12 | Xem danh sách đăng ký theo quyền và lịch sử đăng ký của bản thân | PB13 |
| FR13 | Ghi có mặt, vắng, có phép; chốt và sửa điểm danh có lý do | PB14, PB15 |
| FR14 | Soạn, công bố, lưu trữ thông báo; xác định người nhận theo CLB hoặc sự kiện | PB16 |
| FR15 | Nhận thông báo trong ứng dụng, đánh dấu đã đọc; nhận thông báo đổi lịch hoặc hủy | PB17, PB18 |
| FR16 | Thống kê thành viên hiện tại và tình hình hoạt động theo kỳ, CLB và quyền | PB19, PB20 |
| FR17 | Xuất CSV theo cùng bộ lọc và công thức với báo cáo | PB21 |
| FR18 | Tra cứu nhật ký quản trị cần thiết, sao lưu, khôi phục, dựng môi trường từ tài liệu | PB22 và các PB có thao tác nhạy cảm |

Nhật ký duyệt đơn, chuyển quyền, hủy hoạt động và sửa điểm danh được làm cùng chức năng tương ứng. PB22 bổ sung khả năng vận hành và tra cứu; không trì hoãn việc ghi nhật ký đến Sprint cuối.

## 6 Yêu cầu phi chức năng và phương pháp đo

| Mã | Yêu cầu đề xuất | Cách nghiệm thu |
| --- | --- | --- |
| NFR01 | Không truy cập trái quyền hoặc trái CLB | Kiểm thử API trực tiếp với tài khoản thiếu quyền, đổi ID, tài khoản bị khóa và quyền vừa thu hồi |
| NFR02 | Mật khẩu không lưu rõ; phiên đăng nhập an toàn | Dùng cơ chế băm có salt của thư viện xác thực; kiểm tra cookie HttpOnly, Secure trên HTTPS, chống CSRF khi dùng cookie và giới hạn thử đăng nhập |
| NFR03 | Không vượt sức chứa, không tạo bản ghi trùng | Gửi 20 yêu cầu đồng thời tranh một suất; chỉ một thành công; gửi lặp cùng người không tăng số suất |
| NFR04 | API danh sách đạt p95 dưới 2 giây và lỗi kỹ thuật dưới 1% | Chạy 10 phút với 30 người dùng đồng thời, sau 1 phút làm nóng; 1.000 người dùng, 20 CLB, 200 sự kiện, 10.000 đăng ký; ghi cấu hình máy, truy vấn và phân trang |
| NFR05 | Luồng chính sử dụng được trên điện thoại | Kiểm tra ở chiều rộng 360, 768 và 1366 px; không cuộn ngang ở biểu mẫu; bảng dài có cách cuộn phù hợp |
| NFR06 | Có thể sử dụng bằng bàn phím và đọc lỗi rõ ràng | Nhãn gắn với ô nhập, thứ tự focus hợp lý, focus nhìn thấy, lỗi không chỉ thể hiện bằng màu |
| NFR07 | Dữ liệu nhất quán và có thể truy vết | Khóa ngoại, duy nhất, kiểm tra thời gian; giao dịch cho duyệt đơn, giữ chỗ và chuyển giao quyền |
| NFR08 | Khôi phục được dữ liệu trình diễn | Mục tiêu RPO 24 giờ, RTO 60 phút trên môi trường demo; thực hiện một lần phục hồi và đối chiếu số bản ghi cùng mẫu dữ liệu |
| NFR09 | Có thể cài lại và tiếp tục phát triển | README, cấu hình mẫu, migration, lệnh seed, kiểm thử nghiệp vụ quan trọng; không chứa khóa bí mật trong Git |
| NFR10 | Thời gian và nội dung nhất quán | Lưu thời gian theo UTC, hiển thị giờ Việt Nam UTC+7; kiểm thử mốc hết hạn; tiếng Việt có dấu |
| NFR11 | Dữ liệu cá nhân chỉ hiển thị theo nhu cầu | Danh sách công khai không lộ email, mã SV; CSV giới hạn cột; log không chứa mật khẩu, token |
| NFR12 | Lỗi được xử lý và theo dõi | Có màn hình rỗng, tải, lỗi, hết phiên, không đủ quyền; log mã yêu cầu để tra cứu, không trả stack trace ra trình duyệt |

HTTP 409 do hết chỗ đúng nghiệp vụ không tính vào tỷ lệ lỗi kỹ thuật của NFR04. Tải nêu trên là cấu hình kiểm thử đề xuất, không phải số người dùng thực của trường. Mục tiêu RPO là lượng dữ liệu tối đa có thể mất theo thời gian; RTO là thời gian khôi phục dự kiến. Nhóm chỉ được kết luận đạt khi đã đo.

Đối với khả năng tiếp cận, dùng các tiêu chí liên quan của [WCAG 2.2](https://www.w3.org/WAI/standards-guidelines/wcag/) để thiết kế và kiểm tra. Một vài ca kiểm thử bàn phím chưa đủ để tuyên bố toàn bộ website đạt WCAG AA.

## 7 Quy tắc nghiệp vụ và trạng thái

### 7.1 Quy tắc bắt buộc

| Mã | Quy tắc |
| --- | --- |
| BR01 | Chuẩn hóa email; mã SV lưu dạng chuỗi và duy nhất. Không dùng tên người làm khóa định danh |
| BR02 | Một sinh viên có nhiều tư cách thành viên ở nhiều CLB; chỉ có một giai đoạn chưa kết thúc tại cùng CLB |
| BR03 | Mỗi cặp sinh viên và CLB có tối đa một đơn PENDING; đơn đã kết thúc được giữ làm lịch sử |
| BR04 | Duyệt đơn và tạo thành viên trong cùng giao dịch; gửi yêu cầu duyệt lặp không tạo thành viên mới |
| BR05 | Không cho tự nâng quyền; chuyển chủ nhiệm đồng thời giao quyền mới và thu hồi quyền cũ; không bỏ trống người phụ trách |
| BR06 | CLB ngừng hoạt động không nhận đơn hoặc tạo đăng ký mới; lịch sử vẫn đọc được theo quyền |
| BR07 | Bắt đầu trước kết thúc; hạn đăng ký không sau giờ bắt đầu; sức chứa là số nguyên dương |
| BR08 | Sự kiện chỉ nhận đăng ký khi đã công bố, còn hạn, còn chỗ, người đăng ký đúng đối tượng và tài khoản đang hoạt động |
| BR09 | Kiểm tra chỗ và tạo đăng ký trong một giao dịch có khóa hoặc cơ chế nguyên tử tương đương; UNIQUE chỉ chống trùng người, không tự chống vượt sức chứa |
| BR10 | Giảm sức chứa dưới số đăng ký còn hiệu lực bị từ chối; hủy hợp lệ giải phóng chỗ |
| BR11 | Không cho tự hủy sau hạn đăng ký; quản lý chỉ xử lý ngoại lệ trước starts_at, có lý do và nhật ký. Từ starts_at không đổi danh sách đăng ký; không hủy hồi tố để thay mẫu số |
| BR12 | Sự kiện nội bộ kiểm tra tư cách thành viên khi đăng ký; đình chỉ hoặc rời CLB phải xử lý đăng ký tương lai theo chính sách ở phần 3 |
| BR13 | Chỉ người quản lý đúng CLB được điểm danh; bản cốt lõi không ghi nhận người chưa có đăng ký hợp lệ |
| BR14 | Chưa điểm danh không đồng nghĩa vắng. Khi chốt, yêu cầu xử lý hết người chưa đánh dấu hoặc xác nhận chuyển phần còn lại thành vắng |
| BR15 | Sửa sau chốt cần lý do, người sửa, thời điểm, giá trị trước và sau; chỉ thay đổi kết quả, không âm thầm thay mẫu số |
| BR16 | Sự kiện bị hủy bị chặn đăng ký, giữ lịch sử và loại khỏi tỷ lệ tham dự |
| BR17 | Thông báo lưu snapshot người nhận; khi đổi lịch/hủy, lấy các đăng ký CONFIRMED ngay trước thay đổi trong cùng giao dịch. Trạng thái đọc thuộc từng người; không lộ danh sách người nhận |
| BR18 | Không xóa cứng dữ liệu đã có lịch sử nghiệp vụ; có quy trình lưu trữ và thời hạn giữ dữ liệu trước khi vận hành thật |

### 7.2 Trạng thái và điều kiện chuyển

- CLB: ACTIVE ↔ SUSPENDED; ACTIVE hoặc SUSPENDED → ARCHIVED sau khi xử lý đơn chờ và sự kiện tương lai.
- Đơn gia nhập: PENDING → APPROVED, REJECTED hoặc WITHDRAWN. Đơn đã kết thúc không quay về PENDING; nộp lại tạo đơn mới.
- Thành viên: ACTIVE ↔ SUSPENDED; ACTIVE hoặc SUSPENDED → LEFT. Tái gia nhập tạo giai đoạn mới để giữ lịch sử.
- Sự kiện: DRAFT → PUBLISHED → COMPLETED; DRAFT hoặc PUBLISHED → CANCELLED. COMPLETED chỉ sau thời gian kết thúc; chốt điểm danh được quản lý riêng.
- Đăng ký sự kiện: CONFIRMED → CANCELLED; đăng ký lại trước hạn cho phép CANCELLED → CONFIRMED sau khi kiểm tra lại điều kiện và lưu lịch sử.
- Điểm danh: UNMARKED → PRESENT, ABSENT hoặc EXCUSED. Thay đổi sau chốt tuân theo BR15.
- Thông báo: DRAFT → PUBLISHED → ARCHIVED. Nội dung đã phát không sửa âm thầm; đính chính bằng thông báo mới.

“Đang mở đăng ký” được tính từ trạng thái công bố và thời gian hiện tại; không lưu thêm cờ dễ bị lệch. “Đã kết thúc sự kiện” và “đã chốt điểm danh” là hai thông tin khác nhau. Khi hủy hoạt động, lưu sự kiện cùng trạng thái CANCELLED; đăng ký vẫn được giữ nhưng không còn hiệu lực do trạng thái sự kiện.

Chỉ cho đổi lịch trước thời gian bắt đầu cũ và phải đáp ứng các ràng buộc mới. Từ khi sự kiện bắt đầu, tập đăng ký được cố định theo BR11; đình chỉ hoặc rời CLB về sau không làm mất người khỏi tập này. Hủy toàn bộ sự kiện vẫn giữ lịch sử và loại sự kiện khỏi báo cáo tham dự.

## 8 Các ca sử dụng trọng tâm

### 8.1 UC01 Gia nhập CLB

Tác nhân chính là sinh viên. Điều kiện trước: đã đăng nhập, tài khoản hoạt động, CLB đang nhận đơn, chưa có tư cách thành viên hiệu lực hoặc đơn chờ. Sinh viên mở CLB, nhập lý do, gửi đơn; hệ thống kiểm tra điều kiện trên máy chủ và lưu PENDING. Sinh viên xem trạng thái hoặc rút đơn khi còn chờ.

Chủ nhiệm xem đơn, kiểm tra nội dung rồi duyệt hoặc từ chối. Duyệt thành công tạo tư cách thành viên ACTIVE và kết thúc đơn trong cùng giao dịch. Từ chối cần lý do. Nếu hai người xử lý cùng đơn, quyết định sau nhận thông báo đơn đã được xử lý, không tạo dữ liệu lần hai. Sinh viên xem kết quả ở màn hình trạng thái đơn ngay từ Sprint 2; thông báo tự động kết quả xét đơn chưa thuộc bản cốt lõi.

### 8.2 UC02 Đăng ký hoạt động

Tác nhân là sinh viên. Điều kiện trước: sự kiện đã công bố, còn hạn, tài khoản và tư cách thành viên đáp ứng chính sách. Sinh viên xem lịch, mở sự kiện, chọn đăng ký. Máy chủ kiểm tra trùng và số chỗ trong giao dịch, ghi CONFIRMED, trả mã đăng ký và số chỗ còn lại.

Luồng ngoại lệ: hết hạn, sai đối tượng, sự kiện bị hủy hoặc hết chỗ đều trả lý do rõ ràng. Nếu một suất cuối có nhiều yêu cầu, chỉ một yêu cầu được xác nhận. Nút gửi bị bấm lặp hoặc tải lại trang không tạo nhiều đăng ký. Hủy trước hạn cập nhật trạng thái và giải phóng chỗ trong cùng giao dịch.

### 8.3 UC03 Điểm danh và chốt kết quả

Tác nhân là người quản lý CLB. Hệ thống hiển thị đăng ký hợp lệ của sự kiện. Người quản lý ghi có mặt hoặc có phép; các dòng còn lại vẫn là chưa đánh dấu. Sau khi sự kiện kết thúc, quản lý chốt danh sách, xác nhận xử lý các dòng còn lại và hệ thống lưu thời điểm chốt.

Tập đăng ký CONFIRMED được giữ nguyên từ starts_at nhờ chặn mọi thao tác thêm/hủy từ mốc này. Khi chốt sau sự kiện, lưu tập đó vào is_in_attendance_roster trên đăng ký hoặc bảng snapshot tương đương, đồng thời chốt kết quả. Bản cốt lõi không hỗ trợ thêm hoặc xóa người khỏi danh sách từ lúc hoạt động bắt đầu; chỉnh kết quả có mặt/vắng/có phép sau chốt phải có lý do. Người dùng xem lịch sử cá nhân; dashboard chỉ tính tỷ lệ ở các sự kiện đã chốt.

## 9 Mô hình dữ liệu và thiết kế lưu trữ

### 9.1 Các thực thể chính

| Bảng | Trường trọng tâm | Ràng buộc và ý nghĩa |
| --- | --- | --- |
| users | id, student_code, full_name, email, class_name, faculty, password_hash, system_role, status | Email và mã SV duy nhất; system_role chỉ là ADMIN hoặc STUDENT trong MVP |
| clubs | id, code, name, description, category, status, created_at | code duy nhất; không lưu tổng thành viên như nguồn dữ liệu gốc |
| club_applications | id, user_id, club_id, reason, status, submitted_at, decided_by, decided_at, decision_reason | Tối đa một PENDING mỗi người và CLB; giữ các lần nộp trước |
| memberships | id, user_id, club_id, club_role, status, joined_at, ended_at | Một giai đoạn chưa kết thúc mỗi người và CLB; club_role MEMBER hoặc MANAGER |
| membership_history | id, membership_id, old_status, new_status, old_role, new_role, changed_by, changed_at, reason | Theo dõi đình chỉ, khôi phục, đổi vai trò; hỗ trợ tái tạo lịch sử |
| events | id, club_id, title, description, location, starts_at, ends_at, registration_deadline, capacity, audience, status, attendance_finalized_at | Thời gian hợp lệ, sức chứa dương; audience SCHOOL hoặc CLUB |
| event_registrations | id, event_id, user_id, status, registered_at, cancelled_at, is_in_attendance_roster | UNIQUE event_id và user_id; cờ danh sách chốt chỉ ghi khi chốt điểm danh |
| registration_history | id, registration_id, old_status, new_status, changed_at, changed_by, reason | Lưu cả hủy và đăng ký lại; không mất mốc đăng ký trước |
| attendances | id, registration_id, status, marked_at, marked_by | registration_id duy nhất; thiếu bản ghi được hiển thị UNMARKED |
| announcements | id, club_id, event_id, title, body, status, audience_type, published_by, published_at, source_event_key | event_id có thể rỗng; source_event_key chống tạo thông báo tự động trùng |
| notification_recipients | announcement_id, user_id, delivered_at, read_at | Cặp announcement_id và user_id duy nhất; read_at rỗng là chưa đọc |
| audit_logs | id, actor_id, action, entity_type, entity_id, club_id, before_data, after_data, reason, occurred_at | Chỉ ghi trường cần truy vết; không lưu mật khẩu, token hoặc toàn bộ hồ sơ không cần thiết |

MVP gộp vai trò hệ thống vào users và vai trò CLB vào memberships để giảm số bảng. MANAGER là chủ nhiệm duy nhất; chưa có cán bộ phụ trợ trong bản cốt lõi. Ràng buộc duy nhất bảo đảm mỗi CLB có tối đa một MANAGER chưa kết thúc; giao dịch tạo/chuyển quyền bảo đảm CLB luôn có chủ nhiệm hoạt động. Bảng memberships và thao tác tạo tư cách chủ nhiệm được làm từ PB05 ở Sprint 1, trước quy trình xét đơn ở Sprint 2. Khi cần nhiều vai trò cùng CLB, tách role_assignments và bổ sung yêu cầu riêng.

### 9.2 Quan hệ và nguồn dữ liệu

- users 1–N club_applications; clubs 1–N club_applications.
- users N–N clubs thông qua memberships; memberships 1–N membership_history.
- clubs 1–N events; users N–N events thông qua event_registrations.
- event_registrations 1–0..1 attendances và 1–N registration_history.
- clubs 1–N announcements; events 1–N announcements nếu thông báo liên quan sự kiện.
- announcements N–N users thông qua notification_recipients.
- users 1–N audit_logs theo người thực hiện; entity_type và entity_id là tham chiếu kiểm toán có kiểm soát, không thay khóa ngoại của bảng nghiệp vụ.

Chuẩn hóa dữ liệu theo hướng mỗi bảng biểu diễn một loại thực thể hoặc quan hệ. Không lưu danh sách thành viên bằng chuỗi phân tách dấu phẩy. Không lưu tên CLB hoặc tên sinh viên lặp lại trong mọi đăng ký làm nguồn chính; truy xuất bằng khóa ngoại. Chỉ giữ snapshot ở nơi nghiệp vụ yêu cầu đóng băng thời điểm, như danh sách điểm danh đã chốt hoặc tập người nhận thông báo.

### 9.3 Ràng buộc và chỉ mục dự kiến

Tạo chỉ mục cho memberships(club_id, status), club_applications(club_id, status, submitted_at), events(club_id, starts_at), event_registrations(event_id, status) và notification_recipients(user_id, read_at). Tạo ràng buộc duy nhất có điều kiện cho đơn đang chờ và giai đoạn thành viên chưa kết thúc nếu hệ quản trị hỗ trợ; nếu không, kết hợp giao dịch với cơ chế khóa phù hợp.

Duyệt đơn, chuyển giao chủ nhiệm và giữ suất sự kiện là các ranh giới giao dịch quan trọng. Với giữ suất, khóa bản ghi sự kiện trước khi kiểm tra số đăng ký hiệu lực; mọi đường đăng ký và hủy phải tuân cùng quy ước khóa. Không chỉ đếm trên giao diện rồi gửi yêu cầu tạo mới.

Khóa ngoại ngăn bản ghi mồ côi. Những bảng có lịch sử dùng hạn chế xóa hoặc lưu trữ thay vì cascade xóa toàn bộ. Đổi tên CLB không làm thay đổi số liệu; thay đổi trạng thái phải có thời điểm và người thực hiện.

### 9.4 Dữ liệu tối thiểu và chất lượng dữ liệu

Chỉ cần mã SV, họ tên, email liên hệ, lớp/khoa nếu dùng để lọc; không thu căn cước, địa chỉ nhà hoặc thông tin sức khỏe. Trường bắt buộc, độ dài và định dạng phải thống nhất giữa UI, API và DB. Phân biệt giá trị thiếu với giá trị bằng 0. Loại bỏ khoảng trắng đầu cuối; dữ liệu tìm kiếm hỗ trợ tiếng Việt; không tự thay đổi tên người dùng quá mức.

Kiểm tra chất lượng theo bốn nhóm: đầy đủ các trường bắt buộc; không trùng email, mã SV, đăng ký; hợp lệ về thời gian và trạng thái; nhất quán giữa báo cáo và bản ghi chi tiết. Bộ dữ liệu demo phải có cả dữ liệu hợp lệ và các trường hợp bị từ chối để trình bày khả năng kiểm soát lỗi.

## 10 Phân tích dữ liệu và các chỉ số báo cáo

Phân tích dữ liệu của đề tài gồm mô hình, ràng buộc, công thức chỉ số và phương pháp kiểm chứng. Khi chưa thu thập số liệu thực, chưa thể kết luận CLB nào hoạt động hiệu quả hoặc nhu cầu sinh viên tăng bao nhiêu. Các báo cáo dưới đây là thiết kế cần triển khai.

| Chỉ số | Công thức và điều kiện | Ý nghĩa và giới hạn |
| --- | --- | --- |
| Thành viên hiện tại | COUNT memberships có status ACTIVE và ended_at rỗng | Tổng tư cách thành viên; một SV ở hai CLB được tính hai lần khi cộng theo CLB |
| Sinh viên duy nhất | COUNT DISTINCT user_id trong tập đang xét | Đếm người, không nhầm với tổng tư cách thành viên hoặc lượt tham dự |
| Đơn được duyệt | Đơn APPROVED có decided_at trong kỳ | Đếm quyết định trong kỳ, không trộn với thời điểm nộp |
| Tỷ lệ duyệt đơn | APPROVED / (APPROVED + REJECTED) theo decided_at × 100% | Không đưa đơn chờ hoặc rút vào mẫu số |
| Số hoạt động hoàn thành | Sự kiện COMPLETED có starts_at trong kỳ và không bị hủy | Tách số hoạt động đã chốt điểm danh để đánh giá độ đầy đủ dữ liệu |
| Đăng ký hiệu lực | CONFIRMED của sự kiện chưa bị hủy tại thời điểm truy vấn | Phải ghi rõ thời điểm; không phải số đã có mặt |
| Tỷ lệ lấp đầy | Đăng ký hiệu lực tại hạn đăng ký / capacity × 100% | Nếu muốn báo cáo lịch sử, dùng history để lấy đúng thời điểm; MVP có thể chỉ hiển thị mức hiện tại |
| Tỷ lệ tham dự | PRESENT / số đăng ký thuộc danh sách điểm danh đã chốt × 100% | Chỉ tính sự kiện không hủy và đã chốt; EXCUSED vẫn nằm trong mẫu số đã thống nhất |
| Tỷ lệ vắng | ABSENT / danh sách đã chốt × 100% | Hiển thị thêm EXCUSED; không coi UNMARKED là vắng |
| Tỷ lệ đọc | Số recipients có read_at / tổng recipients của lần phát × 100% | Đã đọc là tín hiệu tương tác, không chứng minh người nhận đã hiểu |
| Thời gian xử lý đơn | Trung vị và p90 của decided_at − submitted_at | Phần mở rộng; phân nhóm theo kỳ quyết định và loại CLB khi đủ dữ liệu |

Nếu mẫu số bằng 0, hiển thị “Không áp dụng” thay vì 0%. Tỷ lệ chung nhiều sự kiện tính bằng tổng tử số chia tổng mẫu số; không lấy trung bình giản đơn các tỷ lệ khi số người khác nhau. Bộ lọc kỳ dùng khoảng thời gian từ đầu kỳ bao gồm đến đầu kỳ kế tiếp không bao gồm để tránh đếm trùng mốc ngày.

Ví dụ kiểm chứng giả lập: sự kiện A có 50 đăng ký trong danh sách đã chốt, gồm 40 có mặt, 8 vắng và 2 có phép; tỷ lệ tham dự là 80%. Sự kiện B có 10 đăng ký đã chốt, 5 có mặt, 5 vắng; tỷ lệ chung A và B là 45/60 = 75%, không phải (80% + 50%)/2 = 65%. Sự kiện C bị hủy phải bị loại khỏi cả tử và mẫu. Nếu còn người chưa được đánh dấu, không công bố tỷ lệ chính thức của sự kiện đó.

Dashboard MVP gồm số CLB hoạt động, thành viên hiện tại theo CLB, hoạt động trong kỳ, số đăng ký, số có mặt và tỷ lệ tham dự của hoạt động đã chốt. Báo cáo tăng trưởng thành viên quá khứ chỉ triển khai khi đã kiểm thử việc tái dựng lịch sử đình chỉ và tái gia nhập; không suy ngược bằng trạng thái hiện tại.

## 11 Thiết kế giải pháp và các phần thường bị bỏ sót

### 11.1 Kiến trúc phù hợp nhóm nhỏ

Đề xuất một ứng dụng web theo kiến trúc nguyên khối có phân lớp: giao diện → xử lý nghiệp vụ và kiểm tra quyền → truy cập dữ liệu → cơ sở dữ liệu quan hệ. Chọn bộ công nghệ nhóm đã quen và thư viện xác thực sẵn có; chốt trong ngày đầu Sprint 1. Không tách microservice trong phạm vi 6 tuần.

Các module nội bộ gồm Accounts, Clubs, Memberships, Events, Attendance, Notifications và Reports. Có thể dùng giao diện render phía máy chủ hoặc frontend riêng; tiêu chí chọn là cả ba người có thể dựng môi trường, sửa lỗi và kiểm thử được trong thời hạn. Phiên bản thư viện và hướng dẫn triển khai phải được kiểm tra trên tài liệu chính thức khi chốt công nghệ, không lấy số phiên bản bất kỳ từ kế hoạch này.

MVP thông báo trong ứng dụng lấy snapshot người có đăng ký CONFIRMED ngay trước thay đổi, rồi ghi trạng thái sự kiện, thông báo và tập người nhận trong cùng giao dịch đổi lịch/hủy, với khóa nguồn để chống trùng. Retry dùng cùng khóa và snapshot, không chọn lại tập người nhận sau khi sự kiện đã bị hủy. Email hoặc hàng đợi xử lý nền chỉ bổ sung khi có nhu cầu. Trang trạng thái sự kiện luôn hiển thị dữ liệu mới nhất, kể cả khi người dùng chưa đọc thông báo.

### 11.2 Các màn hình cần có

- Trang danh sách và chi tiết CLB; đăng nhập; hồ sơ cá nhân và đổi mật khẩu.
- Trang đơn gia nhập của tôi; hàng đợi xét đơn; danh sách thành viên theo CLB.
- Lịch hoạt động; chi tiết sự kiện; hoạt động tôi đã đăng ký; quản lý người đăng ký.
- Biểu mẫu tạo/sửa sự kiện; xác nhận hủy; màn hình điểm danh và chốt.
- Danh sách thông báo, soạn thông báo, chi tiết và trạng thái đã đọc.
- Dashboard có bộ lọc; xuất CSV; quản trị tài khoản và CLB.
- Trạng thái không có dữ liệu, đang tải, lỗi kết nối, hết phiên, không đủ quyền, hết chỗ và hết hạn đăng ký.

### 11.3 Khảo sát và xác nhận yêu cầu

Trong Sprint 1, phỏng vấn dự kiến một đại diện quản lý CLB, một đại diện nhà trường hoặc người đóng vai nghiệp vụ và 3–5 sinh viên. Hỏi về quy trình hiện tại, trường dữ liệu thật sự cần, ai có quyền duyệt, cách xử lý rời CLB, đổi lịch và điểm danh. Sử dụng mẫu biểu được phép chia sẻ, ẩn dữ liệu cá nhân trước khi đưa vào báo cáo.

Lập nhật ký gồm câu hỏi, câu trả lời, người xác nhận, ngày, quyết định và yêu cầu bị ảnh hưởng. Sau mỗi Review, cập nhật nhật ký. Nếu không tiếp cận được đại diện trường, ghi rõ chính sách nào do nhóm giả định và cần xác nhận trước khi vận hành thật.

### 11.4 Vận hành và bàn giao

Mỗi Sprint có bản chạy tích hợp trên môi trường demo, migration có thứ tự và dữ liệu mẫu. Tách cấu hình khỏi mã nguồn, có tài khoản thử cho từng vai trò, hướng dẫn reset dữ liệu và lịch sao lưu. Khi triển khai thật, cần chốt đơn vị vận hành, quyền quản lý máy chủ, thời hạn giữ dữ liệu và quy trình yêu cầu sửa/xóa dữ liệu theo quy định của trường.

## 12 Vận dụng Agile và tổ chức Scrum

Agile định hướng việc nhận phản hồi và thích ứng; Scrum tổ chức công việc thành các vòng lặp ngắn. Với đề tài này, mỗi Sprint tạo ra một luồng nghiệp vụ dùng được, để người dùng thử rồi điều chỉnh ưu tiên. Tài liệu được cập nhật cùng sản phẩm. Cách tiếp cận này dựa trên [Tuyên ngôn Agile](https://agilemanifesto.org/iso/vi/manifesto.html).

Scrum phân định trách nhiệm Product Owner, Scrum Master và Developers. Product Backlog gắn với Product Goal; Sprint Backlog gắn với Sprint Goal; Increment phải đáp ứng Definition of Done. Refinement diễn ra thường xuyên và không phải một sự kiện bắt buộc riêng. [Scrum Guide 2020](https://scrumguides.org/scrum-guide.html).

| Người trong nhóm | Trách nhiệm Scrum đề xuất | Công việc thực tế |
| --- | --- | --- |
| Thành viên A là nhóm trưởng | Product Owner đồng thời tham gia Developers | Làm rõ yêu cầu, sắp thứ tự backlog, trao đổi giảng viên/người dùng; phát triển nghiệp vụ thành viên và báo cáo |
| Thành viên B | Scrum Master đồng thời tham gia Developers | Giữ nhịp sự kiện, giúp giải quyết trở ngại, hỗ trợ cải tiến; phát triển nghiệp vụ sự kiện và dữ liệu |
| Thành viên C | Developers | Phát triển giao diện và luồng tích hợp, kiểm thử tự động, hỗ trợ triển khai |

Cả ba cùng chịu trách nhiệm về chất lượng và kết quả của Sprint. Phân công trên là đầu mối ban đầu, không tạo bộ phận BA, lập trình, kiểm thử tách rời. A không giao số Story Point bắt buộc cho từng người; nhóm tự dự báo công việc dựa trên năng lực. Vai trò nhóm trưởng theo học phần không thay thế việc cả nhóm tự quản lý công việc.

### 12.1 Definition of Ready đề xuất

Một mục đủ rõ để cân nhắc vào Sprint khi đã có người hưởng lợi, giá trị, tiêu chí chấp nhận, quy tắc quyền, dữ liệu mẫu và phụ thuộc đã nhận diện; nhóm có thể hoàn thành trong một Sprint. Đây là thỏa thuận hỗ trợ của nhóm, không phải thành phần bắt buộc của Scrum. Mục chưa rõ có thể được refinement hoặc tách nhỏ trước khi chọn.

### 12.2 Definition of Done áp dụng mọi Sprint

- Luồng giao diện, xử lý nghiệp vụ và dữ liệu đã tích hợp, chạy được trên môi trường demo.
- Tiêu chí chấp nhận của PBI đã có bằng chứng kiểm tra; dữ liệu mẫu đủ trường hợp chính và ngoại lệ quan trọng.
- Kiểm thử tự động phù hợp với thay đổi đã chạy; kiểm tra hồi quy luồng bị ảnh hưởng và kiểm tra quyền trên máy chủ.
- Có ít nhất một thành viên khác đọc thay đổi; lỗi nghiêm trọng về dữ liệu, đăng nhập hoặc phân quyền đã được sửa.
- Migration, cấu hình, tài liệu liên quan và nhật ký thay đổi đã cập nhật; không có bí mật hoặc dữ liệu thật không được phép trong repository.
- Giao diện dùng được trên kích thước đã chọn; các trạng thái lỗi chính hiển thị rõ ràng.

Acceptance Criteria là điều kiện riêng của một PBI; Definition of Done là chuẩn chất lượng chung. Một chức năng chạy trên máy cá nhân nhưng chưa tích hợp hoặc chưa kiểm tra quyền chưa được ghi Done. Các ngưỡng hiệu năng tổng thể được đo đầy đủ ở PB22 nhưng lỗi hiệu năng hoặc an toàn đã biết phải xử lý ngay khi phát hiện.

## 13 Product Backlog phiên bản đầu

Đây là backlog ban đầu bao phủ phạm vi đã biết, không phải danh sách đóng băng. Product Owner cập nhật thứ tự khi có phản hồi; Developers ước lượng lại khi hiểu thêm. M là Must cho bản cốt lõi, S là Should nếu đủ năng lực, C là Could. Story Point dùng dãy 1, 2, 3, 5, 8 để ước lượng tương đối; không quy đổi cố định sang giờ.

Thứ tự dưới đây đi theo giá trị và phụ thuộc của bốn Sprint. Số điểm là gợi ý cho buổi ước lượng đầu tiên, chưa phải velocity đã đo. Bất kỳ mục nào nhóm đánh giá quá lớn phải tách trước khi đưa vào Sprint.

| ID | Hạng mục | Ưu tiên | Điểm | Phụ thuộc chính | Dự kiến |
| --- | --- | --- | --- | --- | --- |
| PB01 | Đăng nhập, đăng xuất, đổi mật khẩu | M | 5 | Không | SP1 |
| PB02 | Cấp và khóa tài khoản sinh viên | M | 3 | PB01 | SP1 |
| PB03 | Quyền hệ thống và quyền theo CLB | M | 5 | PB01 | SP1 |
| PB04 | Tìm và xem thông tin CLB | M | 3 | Không | SP1 |
| PB05 | Quản lý CLB và chuyển chủ nhiệm | M | 5 | PB02, PB03 | SP1 |
| PB06 | Xem và cập nhật hồ sơ cá nhân | M | 2 | PB01 | SP1 |
| PB07 | Gửi, rút và theo dõi đơn gia nhập | M | 3 | PB01, PB04 | SP2 |
| PB08 | Duyệt đơn và tạo thành viên | M | 5 | PB03, PB07 | SP2 |
| PB09 | Quản lý danh sách và trạng thái thành viên | M | 5 | PB08 | SP2 |
| PB10 | Tạo, công bố và cập nhật hoạt động | M | 5 | PB03, PB05 | SP2 |
| PB11 | Xem và lọc lịch hoạt động | M | 3 | PB10 | SP2 |
| PB12 | Đăng ký và hủy tham gia hoạt động | M | 5 | PB08, PB10 | SP2 |
| PB13 | Danh sách đăng ký và lịch sử cá nhân | M | 3 | PB12 | SP2 |
| PB14 | Điểm danh người đăng ký | M | 5 | PB13 | SP3 |
| PB15 | Chốt và sửa điểm danh có truy vết | M | 3 | PB14 | SP3 |
| PB16 | Đăng thông báo theo đối tượng | M | 5 | PB03, PB09, PB10 | SP3 |
| PB17 | Hộp thông báo và đánh dấu đã đọc | M | 3 | PB16 | SP3 |
| PB18 | Thông báo đổi lịch và hủy sự kiện | M | 3 | PB10, PB16 | SP3 |
| PB19 | Thống kê CLB và thành viên hiện tại | M | 3 | PB09 | SP4 |
| PB20 | Thống kê đăng ký và tham dự | M | 3 | PB13, PB15 | SP4 |
| PB21 | Xuất CSV theo bộ lọc và quyền | M | 3 | PB19, PB20 | SP4 |
| PB22 | Vận hành, đối chiếu và bàn giao bản demo | M | 5 | Bản tích hợp SP1–SP3 | SP4 |
| PB23 | Khôi phục mật khẩu tự phục vụ | S | 3 | PB01, kênh gửi được phép | Mở rộng |
| PB24 | Nhập thành viên từ tệp có xem trước | S | 5 | PB09 | Mở rộng |
| PB25 | QR điểm danh có kiểm soát | C | 5 | PB14, PB15 | Mở rộng |
| PB26 | Gửi email thông báo | C | 5 | PB16, dịch vụ gửi | Mở rộng |
| PB27 | Danh sách chờ khi hết chỗ | C | 5 | PB12 | Sau đợt này |
| PB28 | Thu phản hồi sau hoạt động | C | 3 | PB15 | Sau đợt này |

Bản cốt lõi có 22 PBI, tổng điểm gợi ý 85: SP1 là 23; SP2 là 29; SP3 là 19; SP4 là 14. Các số này hỗ trợ thảo luận về độ phức tạp, không chứng minh nhóm làm được 85 điểm trong 6 tuần. PB22 có 5 điểm bao gồm thiết kế kiểm chứng vận hành; kiểm thử của từng chức năng vẫn nằm trong chính PBI đó và Definition of Done.

### 13.1 User Story và tiêu chí chấp nhận SP1

**PB01.** Là người dùng, tôi muốn đăng nhập, đăng xuất và đổi mật khẩu để sử dụng tài khoản an toàn. Đúng thông tin và tài khoản hoạt động thì tạo phiên; sai thông tin trả thông báo chung. Đăng xuất khiến phiên cũ không dùng được. Đổi mật khẩu cần mật khẩu hiện tại, có xác nhận mật khẩu mới; phiên cần được xử lý theo chính sách đã chọn và kiểm tra.

**PB02.** Là quản trị viên, tôi muốn cấp hoặc khóa tài khoản để kiểm soát người sử dụng. Email và mã SV trùng bị từ chối; mật khẩu tạm không ghi vào log. Khóa tài khoản chặn yêu cầu tiếp theo của phiên đang có. Sinh viên không gọi được API quản trị này.

**PB03.** Là người quản lý, tôi muốn quyền được giới hạn theo CLB để chỉ xử lý dữ liệu được giao. Trong SP1, tài khoản quản lý A sửa hồ sơ CLB B bị từ chối cả khi thay ID qua API; từ SP2 bổ sung kiểm tra trên sự kiện B. Thu hồi quyền có hiệu lực ở yêu cầu sau; ẩn nút chỉ hỗ trợ giao diện, không thay kiểm tra máy chủ.

**PB04.** Là sinh viên, tôi muốn tìm và xem CLB để chọn nơi phù hợp. Danh sách hỗ trợ tìm tên, lọc lĩnh vực và phân trang; trang chi tiết hiển thị thông tin được công bố. Danh sách trống có hướng dẫn; không tiết lộ danh sách sinh viên nội bộ.

**PB05.** Là quản trị viên, tôi muốn quản lý CLB và chủ nhiệm để duy trì người phụ trách. Mã CLB duy nhất; trường bắt buộc được kiểm tra. Chuyển chủ nhiệm bảo đảm người mới có tư cách thành viên và quyền phù hợp trong cùng giao dịch; không để CLB không có chủ nhiệm. Lưu trữ bị chặn khi còn đơn chờ hoặc hoạt động tương lai chưa xử lý.

**PB06.** Là sinh viên, tôi muốn xem và sửa thông tin liên hệ được phép để hồ sơ chính xác. Chỉ sửa hồ sơ của mình; mã SV và vai trò không thay đổi qua API cập nhật. Trường sai định dạng có thông báo; dữ liệu lưu lại đúng sau khi mở lại trang.

### 13.2 User Story và tiêu chí chấp nhận SP2

**PB07.** Là sinh viên, tôi muốn gửi và rút đơn gia nhập để chủ động tham gia CLB. Gửi hợp lệ tạo một PENDING; gửi lại không tạo đơn chờ thứ hai. Rút chỉ được khi còn PENDING; đơn đã quyết định vẫn xem được nhưng không bị sửa thành đơn mới.

**PB08.** Là chủ nhiệm, tôi muốn duyệt hoặc từ chối đơn để chọn thành viên. Chỉ người có quyền đúng CLB được quyết định. Duyệt tạo một membership ACTIVE và kết thúc đơn nguyên tử; hai yêu cầu duyệt đồng thời chỉ có một kết quả hiệu lực. Từ chối cần lý do, lưu người và thời điểm.

**PB09.** Là chủ nhiệm, tôi muốn xem và cập nhật trạng thái thành viên để danh sách phản ánh đúng hoạt động. Tìm theo tên/mã SV, lọc trạng thái; đình chỉ/rời lưu lịch sử. Không để chủ nhiệm cuối cùng rời trước chuyển giao. Đăng ký nội bộ tương lai được xử lý theo chính sách trước khi kết thúc tư cách thành viên.

**PB10.** Là chủ nhiệm, tôi muốn tạo và công bố hoạt động để sinh viên biết kế hoạch. Bản nháp chỉ quản lý được thấy; công bố cần đủ thời gian, địa điểm, hạn đăng ký, sức chứa và đối tượng. Thời gian sai hoặc giảm sức chứa dưới số đăng ký bị chặn. Hủy giữ lịch sử và dừng nhận đăng ký.

**PB11.** Là sinh viên, tôi muốn xem lịch theo thời gian và CLB để chọn hoạt động. Kết quả lọc khớp dữ liệu, hiển thị giờ Việt Nam và trạng thái hủy rõ ràng; không hiển thị bản nháp trái quyền. Người ngoài CLB không xem nội dung sự kiện CLUB qua danh sách hoặc đổi ID; lịch sử cá nhân được giữ theo chính sách phần 4. Bản cốt lõi dùng danh sách nhóm theo ngày, chưa cần kéo thả lịch.

**PB12.** Là sinh viên, tôi muốn đăng ký và hủy tham gia để giữ chỗ đúng nhu cầu. Kiểm tra quyền, đối tượng, hạn và chỗ trên máy chủ. Hai người tranh một suất chỉ một người thành công; bấm lặp không chiếm thêm suất. Hủy trước hạn giải phóng chỗ; hủy sau hạn bị từ chối rõ lý do.

**PB13.** Là người tổ chức, tôi muốn xem danh sách đăng ký; là sinh viên, tôi muốn xem đăng ký của mình. Người quản lý chỉ xem đúng CLB; sinh viên chỉ xem lịch sử cá nhân. Danh sách phân biệt xác nhận và đã hủy, có tìm kiếm/phân trang; không lộ hồ sơ người khác.

### 13.3 User Story và tiêu chí chấp nhận SP3

**PB14.** Là người tổ chức, tôi muốn điểm danh để ghi nhận tham dự. Chỉ đăng ký hợp lệ xuất hiện trong danh sách được xử lý; chỉ quản lý đúng CLB được ghi. Cập nhật cùng người không tạo dòng thứ hai; trạng thái chưa đánh dấu được phân biệt với vắng. Lưu người và thời điểm điểm danh.

**PB15.** Là người tổ chức, tôi muốn chốt và sửa điểm danh có lý do để báo cáo kiểm chứng được. Chỉ chốt sau khi sự kiện kết thúc; phải xử lý UNMARKED. Chốt tạo danh sách mẫu số cố định; sửa sau chốt yêu cầu lý do và audit trước/sau. Bản cốt lõi không cho thêm người vào danh sách đã chốt.

**PB16.** Là chủ nhiệm, tôi muốn đăng thông báo đến thành viên hoặc người đăng ký hoạt động để truyền đạt đúng đối tượng. Kiểm tra quyền và phạm vi người nhận; phát thành công lưu snapshot người nhận, không tạo trùng. Nháp không xuất hiện trong hộp người nhận; thông báo đã phát được đính chính bằng bản mới.

**PB17.** Là sinh viên, tôi muốn xem và đánh dấu thông báo đã đọc để theo dõi việc cần làm. Chỉ thấy thông báo mình là người nhận, kể cả thông báo đã nhận trước khi rời CLB. Đánh dấu lặp không làm tăng số người đọc; số chưa đọc cập nhật đúng sau khi tải lại. Không cho truy cập thông báo riêng bằng cách đổi ID; MVP không có bảng tin thông báo công khai riêng.

**PB18.** Là người đã đăng ký, tôi muốn được báo khi hoạt động đổi lịch hoặc hủy để điều chỉnh kế hoạch. Thông báo chứa sự kiện và thay đổi chính, gửi đến snapshot người có đăng ký CONFIRMED ngay trước thay đổi/hủy. Việc lấy snapshot, cập nhật sự kiện và ghi thông báo nằm trong cùng giao dịch. Cùng một lần thay đổi không tạo nhiều thông báo khi gửi lại; dùng nguyên snapshot ban đầu, kể cả sau khi sự kiện bị hủy.

### 13.4 User Story và tiêu chí chấp nhận SP4

**PB19.** Là người quản lý, tôi muốn xem số thành viên và CLB để nắm quy mô hiện tại. Số thành viên ACTIVE khớp truy vấn chi tiết; người quản lý chỉ xem CLB mình. Tổng sinh viên duy nhất khác tổng tư cách thành viên, nhãn báo cáo phải thể hiện rõ.

**PB20.** Là người quản lý, tôi muốn xem đăng ký và tham dự theo kỳ để đánh giá tổ chức. Sự kiện hủy không vào tỷ lệ tham dự; sự kiện chưa chốt báo thiếu dữ liệu. Tổng tỷ lệ dùng tổng tử chia tổng mẫu; mẫu số bằng 0 hiển thị không áp dụng. Số liệu khớp ví dụ kiểm chứng ở phần 10.

**PB21.** Là người quản lý, tôi muốn xuất dữ liệu đang lọc để lập báo cáo. CSV dùng cùng bộ lọc và quyền với màn hình, hiển thị tiếng Việt đúng và không chứa cột riêng tư không cần thiết. Dữ liệu bắt đầu bằng ký tự công thức được xử lý an toàn khi mở bằng bảng tính; không cho đổi tham số để xuất CLB khác.

**PB22.** Là người tiếp nhận hệ thống, tôi muốn dựng lại, đối chiếu và khôi phục bản demo để tiếp tục sử dụng. Tài liệu cài đặt được một thành viên khác thực hiện từ môi trường sạch; sao lưu phục hồi có số liệu đối chiếu. Có báo cáo kiểm thử tải, UAT, danh sách lỗi còn lại, tag phiên bản và hướng dẫn tra cứu audit theo quyền.

### 13.5 Điều kiện tối thiểu cho các mục mở rộng

PB23 dùng token khôi phục một lần, có hạn sử dụng và không tiết lộ tài khoản tồn tại. PB24 có xem trước lỗi, chống trùng và không nhập nửa chừng không kiểm soát. PB25 có token hết hạn, gắn người đăng nhập và kiểm tra đăng ký; một mã QR tĩnh không đủ chứng minh hiện diện. PB26 có trạng thái gửi, thử lại có giới hạn và chống gửi lặp. PB27 thống nhất thứ tự chờ và thời hạn nhận suất. PB28 chỉ cho người thuộc danh sách hợp lệ phản hồi một lần, quy định rõ ẩn danh hay định danh. Các mục này phải refinement thêm trước khi lựa chọn.

## 14 Lịch phát triển và năng lực nhóm

### 14.1 Phương án chính trong 6 tuần

| Sprint | Thời gian tương đối | Sprint Goal | PBI dự kiến | Điểm gợi ý |
| --- | --- | --- | --- | --- |
| SP1 | Ngày 1–10 | Người dùng truy cập đúng quyền và tìm được CLB có người phụ trách | PB01–PB06 | 23 |
| SP2 | Ngày 11–20 | Sinh viên hoàn thành luồng gia nhập CLB và giữ chỗ cho hoạt động hợp lệ | PB07–PB13 | 29 |
| SP3 | Ngày 21–30 | Người tổ chức ghi nhận tham dự và truyền đạt thay đổi đến đúng người | PB14–PB18 | 19 |
| SP4 | Ngày 31–40 | Người quản lý sử dụng báo cáo đối chiếu được trên bản demo có thể bàn giao | PB19–PB22 | 14 |
| Bàn giao | Ngày 41–42 | Trình bày kết quả, bàn giao mã nguồn và hồ sơ | Không trì hoãn phát triển cốt lõi đến đây | Không chấm điểm |

Ngày 1 tính từ ngày nhóm chính thức bắt đầu; không suy ra từ ngày lập tài liệu. Bốn Sprint có độ dài nhất quán 10 ngày lịch, không phải 10 ngày làm việc. Nhóm xác định ngày có thể cộng tác và lịch thi trước khi lập lịch họp. Nếu thời khóa biểu khó phối hợp nhịp 10 ngày, nên thống nhất phương án 8 tuần ngay từ đầu.

### 14.2 Phương án 8 tuần

Dùng 4 Sprint hai tuần: tuần 1–2, 3–4, 5–6 và 7–8, giữ cùng mục tiêu. Thời gian tăng trước hết dành cho hoàn thiện luồng, kiểm thử, phản hồi và tài liệu. Chỉ đưa PB23–PB26 vào khi năng lực thực tế cho phép; không tăng tự động theo tỷ lệ thời gian. Nếu chỉ được gia hạn sau Sprint 4, có thể thêm Sprint 5 từ ngày 41–54 cho tồn đọng hoặc cải tiến có giá trị và chuyển bàn giao sang ngày 55–56. Đây là thay đổi nhịp cho giai đoạn tiếp theo được thống nhất trước, không kéo dài Sprint đã kết thúc để làm đẹp báo cáo.

### 14.3 Tính năng lực trước khi chọn việc

Giả định lập kế hoạch: mỗi người dành 18 giờ/tuần cho đồ án, bao gồm họp và tài liệu. Một Sprint 10 ngày có năng lực thô xấp xỉ 3 × 18 × 10/7 = 77 giờ. Tạm dành khoảng 18 giờ công của cả nhóm cho các sự kiện, refinement và trao đổi; còn 59 giờ. Giữ khoảng 9 giờ cho trở ngại và công việc phát sinh, nên dự báo khoảng 50 giờ task có kế hoạch.

Mỗi thành viên ghi giờ thực sự có thể tham gia; PO và Scrum Master phải tính cả thời gian kiêm nhiệm. Nếu mỗi người chỉ có 12 giờ/tuần, năng lực thô còn khoảng 51 giờ/Sprint, không thể giữ nguyên dự báo 50 giờ task sau khi trừ họp. Khi đó cần chọn phương án 8 tuần hoặc cùng PO điều chỉnh phạm vi, không coi làm thêm giờ là mặc định.

Story Point đánh giá tương đối độ phức tạp và bất định. Giờ ở Sprint Backlog là ước lượng phục vụ phối hợp; hai thước đo không có tỷ lệ quy đổi cố định. Sau Sprint 1, chỉ tính điểm của mục đã Done để hỗ trợ dự báo; một Sprint chưa đủ dữ liệu để xác lập năng suất ổn định.

## 15 Sprint Planning và cách tạo Sprint Backlog

Sprint Planning trả lời vì sao Sprint có giá trị, chọn công việc gì và thực hiện thế nào. Kết quả của buổi này chính là Sprint Backlog, gồm Sprint Goal, các PBI được chọn và kế hoạch thực hiện. Không cần thêm một vòng phê duyệt riêng để biến Product Backlog thành Sprint Backlog. [Scrum.org về Sprint Planning](https://www.scrum.org/resources/what-is-sprint-planning).

Trước buổi họp, A sắp thứ tự backlog, chuẩn bị tiêu chí chấp nhận và câu hỏi; B/C rà phụ thuộc, dữ liệu, rủi ro. Cả ba khai báo lịch rảnh, xem kết quả Sprint trước và chọn số việc phù hợp. Các bảng dưới đây là kế hoạch tham chiếu để bước vào Planning; chỉ Sprint sắp bắt đầu mới chốt dự báo dựa trên thông tin thực tế.

Buổi Planning đề xuất 90 phút: 15 phút xem mục tiêu và phản hồi; 30 phút làm rõ và chọn PBI; 35 phút chia task, kiểm tra phụ thuộc và năng lực; 10 phút kiểm tra Goal, rủi ro và Definition of Done. Đây là lịch họp do nhóm lựa chọn, không phải thời lượng bắt buộc của Scrum.

Sprint Backlog do Developers quản lý và cập nhật trong Sprint. Có thể thương lượng lại phạm vi với PO khi học được điều mới, đồng thời bảo vệ Sprint Goal và chất lượng. [Scrum.org về Sprint Backlog](https://www.scrum.org/resources/what-is-a-sprint-backlog).

## 16 Sprint Backlog tham chiếu cho Sprint 1

Sprint Goal: Người dùng truy cập đúng quyền và tìm được CLB có người phụ trách. Lựa chọn dự kiến PB01–PB06. Điều kiện đầu vào là nhóm chốt bộ công nghệ, mô hình quyền và dữ liệu demo tối thiểu ngay trong các task đầu Sprint.

| Task | PBI | Công việc và kết quả cần có | Đầu mối | Giờ |
| --- | --- | --- | --- | --- |
| S1T01 | PB03–05 | Xác nhận quyền, chính sách CLB và phác thảo màn hình | A | 4 |
| S1T02 | PB01–05 | Khởi tạo ứng dụng, migration, seed và môi trường demo | B | 6 |
| S1T03 | PB01 | Xây luồng xác thực, phiên và đổi mật khẩu | B | 6 |
| S1T04 | PB01 | Giao diện đăng nhập/đổi mật khẩu, kiểm tra luồng tích hợp | C | 4 |
| S1T05 | PB02, PB06 | Cấp/khóa tài khoản và hồ sơ, kiểm thử quyền riêng tư | A | 6 |
| S1T06 | PB03 | Áp dụng quyền theo CLB, kiểm thử truy cập chéo | B | 4 |
| S1T07 | PB04 | Danh sách, tìm kiếm, trang chi tiết CLB | C | 5 |
| S1T08 | PB05 | CRUD CLB, chuyển chủ nhiệm và audit | A | 6 |
| S1T09 | PB01–06 | Hồi quy luồng, sửa lỗi UI, cập nhật README và kịch bản demo | C | 5 |
| Tổng |  | A 16 giờ, B 16 giờ, C 14 giờ | Nhóm | 46 |

Các task 6 giờ được chia nhỏ tiếp thành phần dưới một ngày làm việc thực tế khi đưa lên board. Người đầu mối không làm một mình: mỗi PBI được người khác review, thời gian review và kiểm thử tại task đã tính trong ước lượng. T09 bổ sung kiểm tra liên luồng, không thay thế kiểm thử ở từng task.

Kế hoạch theo mốc: ngày 1 Planning và làm rõ nghiệp vụ; ngày 1–3 dựng ứng dụng và xác thực; ngày 3–7 hoàn thiện các lát cắt tài khoản, quyền và CLB; ngày 8–9 dùng thử, sửa lỗi, refinement SP2; ngày 10 Review và Retrospective. Tích hợp và kiểm thử diễn ra từ khi có thay đổi đầu tiên.

Increment dự kiến: quản trị cấp tài khoản, tạo CLB và giao chủ nhiệm; sinh viên đăng nhập và tìm CLB; tài khoản không có quyền bị chặn. Kịch bản Review phải thử trực tiếp việc quản lý A truy cập tài nguyên B. Mục chưa đáp ứng DoD quay về Product Backlog để sắp lại thứ tự, không được tính hoàn thành vì đã hết thời gian.

## 17 Sprint Backlog tham chiếu cho Sprint 2

Sprint Goal: Sinh viên hoàn thành luồng gia nhập CLB và giữ chỗ cho hoạt động hợp lệ. Lựa chọn dự kiến PB07–PB13. Đây là Sprint rủi ro cao nhất do vừa có xét thành viên vừa có đăng ký theo sức chứa. Nhóm giữ giao diện lịch dạng danh sách và quy trình sự kiện đơn giản để giảm tải.

| Task | PBI | Công việc và kết quả cần có | Đầu mối | Giờ |
| --- | --- | --- | --- | --- |
| S2T01 | PB07–13 | Chốt trạng thái, chính sách rời CLB và ca kiểm thử | A | 3 |
| S2T02 | PB07 | Nộp/rút đơn và xem trạng thái từ UI đến DB | C | 5 |
| S2T03 | PB08 | Duyệt/từ chối bằng giao dịch, chống xử lý lặp | A | 6 |
| S2T04 | PB09 | Danh sách, đình chỉ/rời, history và xử lý đăng ký tương lai | A/C | 7 |
| S2T05 | PB10 | Tạo/công bố/sửa/hủy sự kiện và kiểm tra dữ liệu | B | 7 |
| S2T06 | PB11 | Lịch dạng danh sách, bộ lọc và trang chi tiết | C | 4 |
| S2T07 | PB12 | Giao dịch giữ chỗ, hủy và ca tranh suất cuối | B | 7 |
| S2T08 | PB12–13 | Giao diện đăng ký, danh sách quản lý và lịch cá nhân | C | 5 |
| S2T09 | PB07–13 | Kiểm tra liên luồng, quyền, hồi quy và tài liệu | A/B/C | 5 |
| Tổng |  | A 16 giờ, B 16 giờ, C 17 giờ sau chia task chung | Nhóm | 49 |

T04 chia A 5 giờ và C 2 giờ; T09 chia A 2 giờ, B 2 giờ, C 1 giờ. Với khoảng 6 giờ phối hợp/người, dự phòng SP2 phân bổ A 3,5 giờ, B 3,5 giờ và C 2 giờ; tổng từng người lần lượt 25,5, 25,5 và 25 giờ, nằm trong giả định khoảng 25,7 giờ/người/Sprint. Task 7 giờ là nhóm công việc, phải tách thành thao tác dữ liệu, xử lý nghiệp vụ, giao diện và kiểm thử có đầu ra nhỏ. Dùng các nhánh ngắn và tích hợp mỗi ngày làm việc để tránh dồn ghép mã vào cuối Sprint.

Mốc dự kiến: ngày 11 Planning; ngày 11–14 luồng gia nhập và bản nháp sự kiện; ngày 14–17 đăng ký, lịch, danh sách; ngày 18–19 kiểm chứng đồng thời và dùng thử liên luồng; ngày 20 Review/Retro. Các phần thành viên và sự kiện được phát triển song song nhưng phải thống nhất mô hình quyền từ đầu.

Kịch bản Review: SV01 nộp đơn, chủ nhiệm duyệt; SV01 đăng ký sự kiện nội bộ; SV02 chưa là thành viên bị chặn; hai người đủ điều kiện tranh suất cuối chỉ một thành công; hủy trước hạn giải phóng chỗ. Đây là tiêu chí trình diễn dự kiến, chưa phải kết quả đã xảy ra.

Phương án nếu năng lực không đủ: trước Planning, tách PB13 thành PB13a danh sách cho người tổ chức và PB13b lịch sử cá nhân. Giữ PB13a ở SP2 để PB14 có đầu vào; cân nhắc chuyển PB13b sang SP4. Ước lượng sơ bộ có thể chuyển khoảng 2 giờ của T08 sang SP4, thành SP2 khoảng 47 giờ và SP4 khoảng 42 giờ; nhóm phải ước lượng lại điểm, tiêu chí và phụ thuộc trước khi áp dụng. Lịch cơ sở ở trên chưa áp dụng phương án này. Nếu vẫn thiếu, chọn gia hạn hoặc thương lượng phạm vi; không bỏ giao dịch chống vượt chỗ, kiểm tra quyền hay kiểm thử nghiệp vụ để giữ số lượng PBI.

## 18 Sprint Backlog tham chiếu cho Sprint 3

Sprint Goal: Người tổ chức ghi nhận tham dự và truyền đạt thay đổi đến đúng người. Lựa chọn dự kiến PB14–PB18. Điểm danh và thông báo cùng dựa trên danh sách đăng ký đã có từ Sprint 2.

| Task | PBI | Công việc và kết quả cần có | Đầu mối | Giờ |
| --- | --- | --- | --- | --- |
| S3T01 | PB14–18 | Chốt cửa sổ điểm danh, mẫu số và chính sách người nhận | A | 3 |
| S3T02 | PB14 | API, dữ liệu và giao diện điểm danh tích hợp | B | 6 |
| S3T03 | PB15 | Chốt roster, sửa có lý do và audit | B | 6 |
| S3T04 | PB16 | Soạn/phát thông báo và snapshot người nhận | A | 6 |
| S3T05 | PB17 | Hộp thông báo, đã đọc và kiểm tra quyền | C | 6 |
| S3T06 | PB18 | Gắn thông báo với đổi lịch/hủy, chống trùng | A | 5 |
| S3T07 | PB14–18 | Bổ sung kiểm thử snapshot, trạng thái và quyền chéo | C | 5 |
| S3T08 | PB14–18 | Hồi quy, dùng thử, sửa lỗi và cập nhật hướng dẫn | A/B/C | 5 |
| Tổng |  | A 15 giờ, B 14 giờ, C 13 giờ sau chia T08 | Nhóm | 42 |

T08 chia A 1 giờ, B 2 giờ, C 2 giờ. Ngày 21 Planning; ngày 21–25 hoàn thiện điểm danh và thông báo; ngày 26–28 nối luồng thay đổi sự kiện, đọc thông báo, chốt và sửa; ngày 29 xác nhận công thức thống kê SP4; ngày 30 Review/Retro.

Increment dự kiến: người quản lý điểm danh và chốt một hoạt động, sinh viên xem thông báo thay đổi của hoạt động khác, lịch sử sửa được lưu. Review tập trung vào tình huống còn UNMARKED, thông báo không đúng người và gửi lặp. Không thêm QR trước khi luồng thủ công và kiểm soát quyền đạt DoD.

## 19 Sprint Backlog tham chiếu cho Sprint 4

Sprint Goal: Người quản lý sử dụng báo cáo đối chiếu được trên bản demo có thể bàn giao. Lựa chọn dự kiến PB19–PB22. Thống kê phải tận dụng dữ liệu có lịch sử từ các Sprint trước; không chỉ tạo giao diện biểu đồ không kiểm chứng nguồn số.

| Task | PBI | Công việc và kết quả cần có | Đầu mối | Giờ |
| --- | --- | --- | --- | --- |
| S4T01 | PB19–21 | Chốt công thức, kỳ lọc và bộ dữ liệu đối chiếu | A | 3 |
| S4T02 | PB19 | Truy vấn và màn hình thống kê thành viên | A | 5 |
| S4T03 | PB20 | Thống kê tham dự, dữ liệu thiếu và sự kiện hủy | B | 6 |
| S4T04 | PB21 | Xuất CSV, tiếng Việt, bộ lọc và quyền | C | 4 |
| S4T05 | PB22 | Đo tải, phân tích truy vấn chậm và kiểm tra toàn vẹn | B | 5 |
| S4T06 | PB22 | Sao lưu/khôi phục, cấu hình sạch và tra cứu audit | C | 5 |
| S4T07 | PB19–22 | UAT có ghi nhận, hồi quy và xử lý lỗi ưu tiên | A/B/C | 7 |
| S4T08 | PB22 | README, hướng dẫn, bộ minh chứng và tag phát hành | A/B/C | 5 |
| Tổng |  | A 13 giờ, B 14 giờ, C 13 giờ sau chia task chung | Nhóm | 40 |

T07 chia A 3 giờ, B 2 giờ, C 2 giờ; T08 chia A 2 giờ, B 1 giờ, C 2 giờ. Ngày 31 Planning; ngày 31–35 báo cáo và CSV; ngày 35–38 kiểm chứng vận hành, UAT và xử lý phản hồi; ngày 39 hoàn chỉnh hồ sơ; ngày 40 Review/Retro và bản bàn giao nếu đạt tiêu chí.

Review dùng bộ dữ liệu ở phần 10 để đối chiếu tỷ lệ 75%, thử xuất CSV sai quyền và trình bày bằng chứng khôi phục. Các lỗi còn lại được phân loại và đưa vào Product Backlog. Hai ngày 41–42 dùng cho trình bày và chuyển giao; không coi đó là thời gian bí mật để hoàn thành những mục đã báo Done.

## 20 Các bước tiếp theo trong mỗi Sprint

### 20.1 Thực hiện công việc và Daily Scrum

Board gồm To do → In progress → Review → Verify → Done; gắn cờ Blocked kèm nguyên nhân thay vì giấu trong phần trăm tiến độ. Mỗi task có PBI, người đầu mối, ước lượng còn lại, liên kết thay đổi và bằng chứng kiểm tra. Nhóm có thể giới hạn mỗi người tối đa một task chính đang làm để tập trung hoàn thành.

Daily Scrum diễn ra 15 phút vào mỗi ngày làm việc của Sprint để Developers kiểm tra tiến độ hướng đến Sprint Goal và điều chỉnh kế hoạch. Nhóm tự chọn hình thức; không bắt buộc lần lượt trả lời ba câu hỏi cố định. [Scrum.org về Daily Scrum](https://www.scrum.org/resources/what-is-a-daily-scrum). Khi cần xử lý kỹ thuật dài, hẹn ngay sau buổi họp với những người liên quan.

Mẫu ghi nhận ngắn: ngày; Sprint Goal; luồng đã tích hợp; trở ngại; việc cần làm trong ngày; người hỗ trợ; thời điểm xem lại. Ví dụ minh họa: “S2T07 bị chặn vì hai yêu cầu vẫn nhận cùng suất cuối; B và C kiểm tra ranh giới giao dịch trong hôm nay; A tiếp tục hoàn thiện xét đơn.” Không ghi tình huống này như nhật ký đã diễn ra.

### 20.2 Kiểm thử và tạo Increment

Mỗi thay đổi được review và kiểm thử ngay trong Sprint. Những PBI đạt DoD tạo thành phần tăng trưởng dùng được, kết hợp với các Sprint trước. Không chờ hết Sprint mới tích hợp. Có thể đưa bản đã đạt DoD cho người dùng thử sớm; Sprint Review không phải cửa phê duyệt bắt buộc để phát hành. [Scrum.org về Increment và Sprint Backlog](https://www.scrum.org/resources/what-is-a-sprint-backlog).

Nếu phát hiện lỗi chặn mục tiêu, nhóm ưu tiên xử lý và trao đổi lại phạm vi với PO. Nếu mục tiêu không còn phù hợp, PO cân nhắc hủy Sprint theo Scrum; không kéo dài Sprint chỉ vì chưa làm xong. Trong đồ án, tình huống này cần ghi rõ lý do và ảnh hưởng kế hoạch.

### 20.3 Refinement xuyên suốt

Dành khoảng 45 phút mỗi Sprint cho refinement có chủ đích, ngoài các trao đổi ngắn khi cần. Nhóm làm rõ mục sắp tới, bổ sung dữ liệu và tiêu chí, tách mục quá lớn, xem phụ thuộc và ước lượng lại. Sprint 1 chuẩn bị trạng thái đơn và sự kiện; Sprint 2 chuẩn bị chốt điểm danh và người nhận; Sprint 3 chốt công thức thống kê; Sprint 4 sắp lại tồn đọng hoặc hướng mở rộng.

### 20.4 Sprint Review

Đề xuất 60 phút ở ngày cuối Sprint với cả nhóm, giảng viên hoặc đại diện người dùng có thể tham gia. Mục đích là kiểm tra kết quả và cùng chọn điều chỉnh cho sản phẩm, không chỉ trình chiếu slide. [Scrum.org về Sprint Review](https://www.scrum.org/resources/what-is-a-sprint-review).

Chương trình: nhắc Sprint Goal, nêu mục Done và chưa Done, chạy kịch bản thực tế, mời người dùng thao tác, ghi phản hồi và thống nhất ưu tiên tiếp theo. Biên bản gồm ngày, người tham gia thực tế, PBI được xem, bằng chứng, ý kiến, quyết định và người phụ trách. Chỉ ghi “đạt” khi có bằng chứng; không tự tạo phản hồi hoặc chữ ký thay người dùng.

### 20.5 Sprint Retrospective

Đề xuất 45 phút sau Review với cả ba thành viên. Nhóm xem điều gì giúp hoàn thành công việc, điều gì gây chậm hoặc lỗi, rồi chọn một cải tiến có người chịu trách nhiệm và thời điểm kiểm tra. Retrospective tập trung vào cách làm việc và chất lượng của nhóm. [Scrum.org về Sprint Retrospective](https://www.scrum.org/resources/what-is-a-sprint-retrospective).

Ví dụ cải tiến dự kiến: nếu SP1 phát hiện lỗi phân quyền muộn, SP2 thêm ma trận quyền vào bước refinement, B làm đầu mối và nhóm kiểm tra trước mỗi lần merge. Nếu ghép mã cuối Sprint gây xung đột, yêu cầu nhánh nhỏ và tích hợp mỗi ngày làm việc. Chỉ chọn cải tiến dựa trên quan sát thực tế; không ghi sẵn rằng nhóm đã gặp lỗi này.

### 20.6 Cập nhật và bắt đầu Sprint kế tiếp

PO sắp lại Product Backlog theo phản hồi và rủi ro; Developers cập nhật ước lượng và năng lực. Mục chưa Done trở lại backlog để xem xét, không tự động chuyển nguyên trạng sang Sprint sau. Cải tiến quan trọng từ Retro được đưa vào kế hoạch. Sprint tiếp theo bắt đầu ngay sau Sprint trước theo lịch đã thống nhất.

Chu trình của nhóm: Product Goal và backlog ban đầu → Sprint Planning tạo Sprint Backlog → thực hiện, Daily Scrum, refinement và tạo Increment → Sprint Review → Sprint Retrospective → cập nhật Product Backlog → Sprint Planning kế tiếp.

## 21 Kế hoạch kiểm thử và truy vết

### 21.1 Các cấp kiểm thử

Kiểm thử đơn vị cho kiểm tra thời gian, trạng thái và công thức; kiểm thử tích hợp cho giao dịch, truy vấn, quyền tài nguyên; kiểm thử xuyên luồng cho gia nhập, đăng ký, điểm danh, thông báo và báo cáo. UAT do người dùng hoặc đại diện nghiệp vụ thao tác trên kịch bản; nhóm ghi lại ý kiến thay vì tự chấm thay họ. Kiểm thử tải và khôi phục thực hiện theo NFR, có cấu hình và log.

| Test | Kịch bản | Kết quả mong đợi | Truy vết |
| --- | --- | --- | --- |
| TC01 | Sai mật khẩu, tài khoản bị khóa và phiên cũ sau đăng xuất | Không truy cập được dữ liệu bảo vệ | FR01; PB01–02; NFR02 |
| TC02 | Quản lý A sửa tài nguyên CLB B bằng API | Từ chối, dữ liệu B không đổi | FR03; PB03; NFR01 |
| TC03 | Gửi hai đơn chờ cùng người và CLB | Chỉ một PENDING | FR06; PB07; BR03 |
| TC04 | Duyệt cùng đơn đồng thời | Một membership hiệu lực và một quyết định | FR07; PB08; BR04 |
| TC05 | Chủ nhiệm cuối cùng rời CLB | Bị chặn đến khi chuyển giao hợp lệ | FR05/08; PB05/09; BR05 |
| TC06 | Thời gian sai hoặc sức chứa dưới số người đã đăng ký | Từ chối và báo lỗi rõ | FR09; PB10; BR07/10 |
| TC07 | 20 yêu cầu tranh suất cuối | Một thành công; không vượt capacity | FR11; PB12; NFR03 |
| TC08 | Gửi lặp đăng ký rồi hủy đúng hạn | Một đăng ký; một chỗ được giải phóng | FR11; PB12; BR09/10 |
| TC09 | Người ngoài CLB đăng ký sự kiện nội bộ | Từ chối đúng chính sách | FR11; PB12; BR08 |
| TC10 | Sự kiện còn UNMARKED khi chốt | Yêu cầu xử lý hoặc xác nhận; không âm thầm coi là vắng | FR13; PB14–15; BR14 |
| TC11 | Sửa điểm danh sau chốt không có lý do | Từ chối; có lý do thì có audit trước/sau | FR13; PB15; BR15 |
| TC12 | Đổi lịch/hủy rồi gửi lặp cùng yêu cầu | Một thông báo mỗi người trong snapshot trước thay đổi; hủy không làm tập người nhận rỗng | FR15; PB18; BR17 |
| TC13 | Đọc thông báo riêng của người khác qua ID | Từ chối | FR15; PB17; NFR01 |
| TC14 | Hai sự kiện tỷ lệ 80% và 50% với quy mô khác nhau | Tỷ lệ chung 75% theo bộ mẫu | FR16; PB20; phần 10 |
| TC15 | Sự kiện hủy, chưa chốt hoặc mẫu số 0 | Loại đúng hoặc hiển thị chưa đủ/không áp dụng | FR16; PB20 |
| TC16 | Xuất CSV với CLB trái quyền và ô giống công thức | Chặn trái quyền; xử lý ô nguy hiểm | FR17; PB21; NFR11 |
| TC17 | Khôi phục backup trên môi trường thử | Đạt đối chiếu và ghi thời gian khôi phục | FR18; PB22; NFR08 |
| TC18 | Chạy tải, màn hình nhỏ và điều hướng bàn phím | Báo cáo số đo và lỗi theo NFR04–06 | PB22; DoD |
| TC19 | Hủy đăng ký sau starts_at dù người dùng có quyền quản lý | Từ chối; không làm mất người khỏi mẫu số điểm danh | FR11/13; PB12/15; BR11 |
| TC20 | Đình chỉ người còn đăng ký nội bộ tương lai | Hủy các đăng ký chưa bắt đầu và lưu lý do; giữ lịch sử, thông báo đã nhận | FR08; PB09; phần 3/4 |
| TC21 | Người ngoài CLB đọc sự kiện nội bộ bằng ID | Từ chối, trừ phần lịch sử của chính người đã đăng ký theo chính sách | FR10; PB11; NFR01 |

Các test là kế hoạch, trạng thái ban đầu “Chưa chạy”. Khi thực hiện phải bổ sung phiên bản phần mềm, ngày, dữ liệu, người chạy, kết quả thực tế và liên kết minh chứng. Một lần demo thành công không thay thế kiểm thử quyền và đồng thời.

### 21.2 Ma trận bao phủ theo module

| Module | Yêu cầu | Sprint | Bằng chứng cần lưu |
| --- | --- | --- | --- |
| Tài khoản và quyền | FR01–03, NFR01–02 | SP1 và hồi quy sau đó | Test API, log phiên, ảnh/video luồng |
| CLB | FR04–05 | SP1 | Ca CRUD, chuyển quyền, kiểm tra dữ liệu |
| Thành viên | FR06–08 | SP2 | Ca nộp/duyệt/rời, lịch sử và dữ liệu DB |
| Hoạt động và đăng ký | FR09–12, NFR03 | SP2 | Ca trạng thái, đồng thời, lọc lịch |
| Điểm danh và thông báo | FR13–15 | SP3 | Snapshot, audit, kiểm tra người nhận |
| Báo cáo và vận hành | FR16–18, NFR04–12 | SP4, kết hợp kiểm tra từng Sprint | Đối chiếu báo cáo, CSV, đo tải, backup/restore |

## 22 Theo dõi tiến độ và xử lý rủi ro

Các số liệu theo dõi gồm mức đạt Sprint Goal, số PBI Done, công việc còn lại, thời gian bị Blocked, lỗi phát hiện sau tích hợp và phản hồi chưa xử lý. Burndown nếu dùng dựa trên giờ còn lại được cập nhật mỗi ngày làm việc; không dùng phần trăm cảm tính. Story Point hoàn thành chỉ tính PBI đạt DoD, không ghi 50% điểm cho mục làm dở.

| Rủi ro | Mức ưu tiên | Dấu hiệu sớm | Cách xử lý và đầu mối |
| --- | --- | --- | --- |
| Thiếu giờ do học hoặc thi | Cao | Lịch rảnh thấp hơn giả định 18 giờ/tuần | A cập nhật capacity mỗi Planning; chọn 8 tuần hoặc thương lượng phạm vi |
| SP2 quá tải | Cao | Nhiều PBI chưa tích hợp giữa Sprint | A/B chia nhỏ theo luồng; giữ lịch đơn giản, loại trang trí, bảo vệ giao dịch và quyền |
| Sai phân quyền giữa CLB | Cao | Chỉ kiểm tra role chung, thiếu club_id | B lập ma trận quyền; kiểm thử API trái phạm vi trong mỗi module |
| Vượt số chỗ sự kiện | Cao | Đếm trước rồi tạo ngoài giao dịch | B thiết kế khóa/giao dịch và chạy TC07 trước hoàn tất PB12 |
| Số liệu điểm danh thiếu | Cao | UNMARKED bị tính là ABSENT | A chốt định nghĩa mẫu số; C kiểm tra nhãn và TC10/15 |
| Tích hợp muộn | Vừa | Nhánh kéo dài, chỉ chạy trên máy cá nhân | C hỗ trợ môi trường demo từ SP1, review thay đổi nhỏ |
| Yêu cầu mở rộng liên tục | Vừa | Thêm QR, chat hoặc thanh toán trước luồng chính | PO đưa vào backlog, đánh đổi minh bạch, không tự chen làm mất Goal |
| Không có người dùng góp ý | Vừa | Review chỉ nội bộ nhóm | A hẹn trước người tham gia; nếu không có, ghi rõ người đóng vai và giới hạn phản hồi |
| Mất dữ liệu demo | Vừa | Seed không tái tạo, backup chưa thử | C giữ migration/seed, sao lưu và kiểm chứng khôi phục |

Nội dung mới giữa Sprint được PO và Developers đánh giá tác động đến Goal. Nếu không cần ngay, đưa Product Backlog; nếu thiết yếu, thương lượng thay mục ít quan trọng hơn. Không bổ sung vô hạn công việc rồi giữ nguyên cam kết thời hạn.

## 23 Hồ sơ và sản phẩm cần bàn giao

- Báo cáo đề tài: bối cảnh, mục tiêu, phạm vi, đối tượng, yêu cầu, quy tắc và quyết định nghiệp vụ.
- Sơ đồ use case có tác nhân; activity cho gia nhập và đăng ký; sequence cho duyệt đơn và giữ suất; ERD với khóa, quan hệ và ràng buộc; sơ đồ kiến trúc triển khai.
- Product Backlog có lịch sử thay đổi, Sprint Goal và Sprint Backlog thực tế của từng Sprint.
- Minh chứng Daily, Review, Retro và cải tiến; không cần biên bản dài mỗi ngày, nhưng phải thể hiện quan sát thật.
- Mã nguồn, migration, seed, cấu hình mẫu, hướng dẫn cài đặt và tài khoản thử từng vai trò.
- Bộ kiểm thử, kết quả thực chạy, lỗi còn lại, báo cáo đo tải và khôi phục; ma trận yêu cầu–PBI–test cập nhật.
- Website demo, hướng dẫn cho sinh viên và chủ nhiệm; báo cáo giới hạn và hướng mở rộng.
- Slide hoặc video trình diễn luồng xuyên suốt; phân công và đóng góp thực tế của cả ba thành viên.

Tiêu chí kết thúc đợt phát triển: 22 PBI cốt lõi đạt DoD hoặc mọi thay đổi phạm vi được ghi rõ và đồng thuận; các ca quyền, đồng thời và toàn vẹn quan trọng đạt; báo cáo đối chiếu đúng; có bản demo tái tạo được và hồ sơ trung thực. Nếu có yêu cầu chưa đạt, ghi vào tồn đọng và nêu ảnh hưởng thay vì tuyên bố sản phẩm hoàn chỉnh.

## 24 Các quyết định nhóm cần chốt ở buổi đầu

1. Ngày bắt đầu, thời hạn được giảng viên chấp nhận là 6 hay 8 tuần, lịch rảnh của từng người và thời lượng thực tế mỗi tuần.
2. Bộ công nghệ cả nhóm sử dụng được, cách chia module và quy tắc review; một thành viên khác phải dựng được môi trường.
3. Người đóng vai PO, Scrum Master; đại diện CLB hoặc người dùng tham gia Review; quyền quyết định phạm vi của nhóm trưởng.
4. Trường dữ liệu hồ sơ, ai cấp tài khoản, tiêu chí duyệt CLB, chính sách rời/đình chỉ và đối tượng đăng ký sự kiện.
5. Nguồn dữ liệu demo, môi trường hosting, ngân sách được phép và người giữ quyền quản trị.
6. Xác nhận các công thức báo cáo, Definition of Done, bộ test bắt buộc và những mục mở rộng để sau.

## 25 Tài liệu tham khảo

Các nguồn dưới đây dùng để đối chiếu phương pháp Scrum, giá trị Agile và yêu cầu kỹ thuật. Lịch, backlog, giờ công và chính sách CLB trong báo cáo là phương án thiết kế riêng cho đồ án này.

- [Scrum Guide 2020](https://scrumguides.org/scrum-guide.html) – các trách nhiệm, sự kiện, hiện vật và cam kết của Scrum.
- [Tuyên ngôn Agile tiếng Việt](https://agilemanifesto.org/iso/vi/manifesto.html) – định hướng phát triển và thích ứng.
- [Sprint Planning](https://www.scrum.org/resources/what-is-sprint-planning) – mục tiêu, lựa chọn công việc và kế hoạch thực hiện.
- [Sprint Backlog](https://www.scrum.org/resources/what-is-a-sprint-backlog) – Goal, công việc được chọn, kế hoạch và Increment.
- [Sprint Review](https://www.scrum.org/resources/what-is-a-sprint-review) – xem xét kết quả và điều chỉnh sản phẩm.
- [Daily Scrum](https://www.scrum.org/resources/what-is-a-daily-scrum) – kiểm tra tiến độ và điều chỉnh kế hoạch mỗi ngày làm việc.
- [Sprint Retrospective](https://www.scrum.org/resources/what-is-a-sprint-retrospective) – cải tiến cách làm việc và chất lượng.
- [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) – kiểm tra quyền và hạn chế truy cập tài nguyên.
- [W3C WCAG Overview](https://www.w3.org/WAI/standards-guidelines/wcag/) – tiêu chí khả năng tiếp cận web.
