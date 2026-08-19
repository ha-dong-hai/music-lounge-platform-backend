# Bộ câu hỏi phản biện Backend — MusicLounge

Chuẩn bị cho buổi bảo vệ SEP490. Mỗi câu gồm **Trả lời ngắn** (câu bạn nói ra đầu tiên,
3–4 giây), **Giải thích** (cách nói để cả người không rành kỹ thuật cũng hiểu), và
**Nếu bị hỏi sâu** (chỉ dùng khi hội đồng đào tiếp).

**Nguyên tắc trả lời:** nói *quyết định* và *lý do*, đừng đọc tên công nghệ. Hội đồng
không chấm bạn biết bao nhiêu framework — họ chấm bạn có hiểu vì sao mình chọn không.
Mọi con số dưới đây đều lấy trực tiếp từ code, không phải ước lượng.

**Quan trọng:** các câu gắn 🔴 là **điểm yếu thật** của hệ thống. Đừng giấu — hội đồng sẽ
tìm ra. Trả lời thẳng cộng điểm nhiều hơn là bị bắt bài.

---

## Phần 1 — Cơ bản: hệ thống làm gì

### 1. Dự án của em giải quyết vấn đề gì?

**Trả lời ngắn:** Một phòng trà nhạc sống nhỏ hiện phải dùng 3 công cụ rời rạc — bán vé
một nơi, livestream một nơi, tiền tip cho nghệ sĩ thì ghi tay. MusicLounge gộp cả ba vào
một nền tảng.

**Giải thích:** Hãy tưởng tượng một quán cà phê nhạc sống. Tối nay có ca sĩ A hát, mai có
ban nhạc B. Chủ quán muốn bán vé online thì phải lên sàn vé chung — nhưng sàn đó thiết kế
cho concert có giờ diễn cố định, còn quán thì vé có giá trị cả buổi tối và vẫn bán tiếp cho
khách vãng lai lúc nhạc đã bắt đầu. Muốn livestream thì lên nền tảng khác, nhưng ở đó ai
cũng xem được, kể cả người không mua vé. Còn khách muốn tip cho ca sĩ thì đưa tiền mặt hoặc
chuyển khoản riêng — không ai biết chính xác quán đã thu hộ bao nhiêu và trả lại cho ca sĩ
bao nhiêu.

Ba vấn đề đó chính là ba nhóm chức năng chính của hệ thống.

---

### 2. Hệ thống có mấy loại người dùng?

**Trả lời ngắn:** 6 actor, nhưng chỉ 4 loại có tài khoản đăng nhập.

**Giải thích:**

| Actor | Đăng nhập? | Làm gì |
|---|---|---|
| **Guest** (khách vãng lai) | Không | Xem show, xem venue, tham quan tour 360°, gửi khiếu nại. Không mua/xem/donate được |
| **Audience** (khán giả) | Có | Mua vé, xem livestream, donate, đặt đồ ăn tại bàn, đánh giá show |
| **Owner** (chủ venue) | Có | Đăng ký venue, tạo show, quản lý khu vực ghế, xem doanh thu. Trả phí subscription |
| **Staff** (nhân viên) | Có | Bán vé tại quầy, soát vé cửa, xử lý order đồ ăn, bật/tắt phát sóng |
| **Admin** (quản trị) | Có | Duyệt venue, kiểm duyệt nội dung, xử lý hoàn tiền, xác minh tài khoản ngân hàng |
| **Performer** (nghệ sĩ) | **Không** | Có hồ sơ công khai và tài khoản ngân hàng nhận tiền, nhưng không tự đăng nhập |

---

### 3. Vì sao nghệ sĩ lại không có tài khoản đăng nhập?

**Trả lời ngắn:** Vì trong mô hình này nghệ sĩ là *người được venue mời*, không phải người
tự vận hành. Chủ venue quản lý hồ sơ nghệ sĩ thay họ.

**Giải thích:** Một ca sĩ có thể hát ở 5 quán khác nhau trong tháng. Nếu bắt họ tạo tài
khoản, tự cập nhật hồ sơ, tự khai số tài khoản ngân hàng — thì với một quán nhỏ mời ca sĩ
hát một tối, quy trình đó quá nặng và ca sĩ sẽ không làm. Thực tế là chủ quán nhập giúp.

Đây là quyết định có chủ đích, không phải thiếu sót. **Đừng để hội đồng dẫn bạn sang hướng
"vậy là thiếu tính năng"** — hãy nói ngay: nếu sau này mở rộng, thêm login cho nghệ sĩ là
thêm một vai trò, không phải thiết kế lại hệ thống.

---

### 4. Luồng mua vé diễn ra như thế nào?

**Trả lời ngắn:** Giữ chỗ 15 phút → thanh toán qua VNPay → VNPay báo ngược về server →
hệ thống xác nhận vé và sinh mã QR.

**Giải thích:** Giống đặt vé máy bay. Khi bạn chọn ghế, hệ thống **giữ chỗ tạm** cho bạn
15 phút để kịp thanh toán — trong lúc đó người khác không mua được chỗ đó. Nếu 15 phút trôi
qua mà không trả tiền, chỗ tự động được thả ra.

Điểm quan trọng nhất: cái xác nhận vé **không phải** là trình duyệt của khách quay về trang
"thanh toán thành công", mà là VNPay gọi thẳng vào server báo "giao dịch này đã thu tiền".
Nhờ vậy khách có tắt trình duyệt giữa chừng thì vé vẫn được xác nhận bình thường.

---

### 5. Backend làm gì? Khác gì frontend?

**Trả lời ngắn:** Frontend là cái người dùng nhìn thấy; backend là nơi ra quyết định và giữ
dữ liệu. Mọi luật nghiệp vụ đều nằm ở backend.

**Giải thích:** Frontend giống mặt tiền quán — thực đơn, bàn ghế, người phục vụ. Backend là
bếp và két tiền. Nếu chỉ kiểm tra ở frontend rằng "vé còn hay hết", người dùng có thể mở
công cụ lập trình của trình duyệt và bỏ qua kiểm tra đó. Vì vậy **mọi kiểm tra quan trọng
đều lặp lại ở backend** — số vé còn, quyền hạn, giá tiền — kể cả khi frontend đã kiểm rồi.

---

### 6. Vì sao chọn .NET/C# mà không phải Java, Node.js hay Python?

**Trả lời ngắn:** Vì hệ thống này xoay quanh tiền, và .NET có sẵn kiểu dữ liệu chính xác
cho tiền tệ cùng cơ chế transaction chặt chẽ trong framework.

**Giải thích:** Trong lập trình có hai cách lưu số: kiểu "gần đúng" và kiểu "chính xác".
Kiểu gần đúng nhanh hơn nhưng `0.1 + 0.2` cho ra `0.30000000000000004`. Với đồ hoạ thì không
sao; với tiền thì sau vài nghìn giao dịch là lệch sổ. .NET có kiểu `decimal` chuẩn cho tiền
và cả hệ sinh thái mặc định dùng nó.

Ngoài ra nhóm đã quen C#. Với đồ án 15 tuần, chọn thứ mình làm nhanh và ít lỗi quan trọng
hơn chọn thứ đang thời thượng.

---

### 7. Hệ thống lớn cỡ nào?

**Trả lời ngắn:** 68 bảng dữ liệu, 25 nhóm API với 209 endpoint, 109 use case, 30 job nền.

**Giải thích:** Nói kèm quy mô cho dễ hình dung: 5 giao diện khác nhau (website khán giả,
app đặt đồ ăn, dashboard chủ venue, app nhân viên, console quản trị), tổng 75 màn hình,
ước lượng công sức 488 ngày-người.

---

## Phần 2 — Nghiệp vụ và dòng tiền

### 8. Nền tảng kiếm tiền bằng cách nào?

**Trả lời ngắn:** Chủ yếu bằng **phí thuê bao** chủ venue trả hàng tháng, cộng hoa hồng
trên vé bán online.

**Giải thích:** Có hai mô hình phổ biến: ăn hoa hồng từng vé, hoặc thu phí thuê bao. Hệ
thống này nghiêng về thuê bao, vì một quán nhỏ bán 30 vé một tối thì hoa hồng không đáng
kể, trong khi chi phí vận hành nền tảng (server, livestream, SMS) là cố định. Thuê bao cho
doanh thu ổn định và dễ dự đoán hơn.

---

### 9. Vì sao vé bán tại quầy lại không thu hoa hồng?

**Trả lời ngắn:** Vì vé quầy là khách tự đến, nền tảng không mang khách về nên thu hoa hồng
là không hợp lý. Chi phí đó đã nằm trong gói thuê bao.

**Giải thích:** Câu này hay bị hỏi. Nếu thu hoa hồng cả vé quầy, chủ venue sẽ đơn giản là
**không nhập vé quầy vào hệ thống** — họ vẫn bán tiền mặt như cũ. Kết quả là hệ thống mất
luôn dữ liệu, mà dữ liệu mới là thứ có giá trị. Miễn hoa hồng vé quầy chính là để chủ venue
có động lực nhập đủ.

**Nếu bị hỏi sâu:** Trong code vẫn có sẵn công tắc bật hoa hồng vé quầy nếu mô hình kinh
doanh thay đổi — đây là quyết định cấu hình, không phải giới hạn kỹ thuật.

---

### 10. Tiền donate chia thế nào? Con số ở đâu ra?

**Trả lời ngắn:** Mặc định **88% chuyển cho nghệ sĩ**, phần còn lại bù phí cổng thanh toán
và phí nền tảng. Con số này nằm trong bảng cấu hình, không viết cứng trong code.

**Giải thích:** Tất cả con số nghiệp vụ đều nằm trong bảng `system_config`. Một vài giá trị
thật:

| Cấu hình | Giá trị | Ý nghĩa |
|---|---|---|
| `donation_performer_share_rate` | 0.88 | 88% tiền donate chuyển cho nghệ sĩ |
| `gateway_fee_rate` | 0.02 | Phí cổng thanh toán 2% |
| `platform_commission_rate` | 0.05 | Hoa hồng nền tảng 5% |
| `tax_rate` | 0.05 | Thuế khấu trừ 5% |
| `ticket_hold_minutes` | 15 | Giữ chỗ 15 phút |
| `moderation_sla_hours` | 24 | Kiểm duyệt trong 24 giờ |
| `donation_hold_days` | 7 | Giữ tiền donate 7 ngày trước khi trả |

Ý nghĩa thực tế: muốn đổi tỷ lệ chia, admin sửa một dòng cấu hình — **không cần lập trình
viên sửa code và deploy lại**.

---

### 11. Nếu sau này đổi tỷ lệ chia thì giao dịch cũ có bị tính sai không?

**Trả lời ngắn:** Không. Mỗi giao dịch **chụp lại (snapshot)** tỷ lệ tại đúng thời điểm phát
sinh và lưu vào chính bản ghi đó.

**Giải thích:** Đây là điểm hội đồng hay khen nếu nói được. Giả sử hôm nay tỷ lệ 88%, khách
donate 100.000đ → hệ thống lưu luôn vào bản ghi: "tỷ lệ 88%, nghệ sĩ nhận 88.000đ". Tháng
sau đổi thành 90%. Nếu hệ thống *tính lại* mỗi lần xem báo cáo thì giao dịch cũ đột nhiên
hiện 90.000đ — lệch sổ sách và không đối chiếu được với tiền đã chuyển thật.

Nguyên tắc: **con số đã phát sinh thì đóng băng, không tính lại.**

---

### 12. Làm sao chứng minh tiền không thất thoát?

**Trả lời ngắn:** Mọi chuyển động tiền ghi theo **sổ kép** — mỗi giao dịch sinh ra các bút
toán nợ và có phải cân bằng nhau. Có job chạy hằng ngày kiểm tra lại.

**Giải thích:** Đây là nguyên tắc kế toán hàng trăm năm. Tiền không tự sinh ra hay mất đi —
nó chỉ chuyển từ chỗ này sang chỗ khác. Nên mỗi giao dịch phải ghi *hai vế*: tiền ra khỏi
đâu và vào đâu, tổng hai vế bằng nhau.

Nếu ai đó sửa tay trong database làm lệch, job kiểm tra hằng ngày sẽ phát hiện và báo động.
Nói cách khác: hệ thống không chỉ ghi số dư, nó ghi **toàn bộ lịch sử** đủ để dựng lại số
dư từ đầu.

---

### 13. Khi nào chủ venue nhận được tiền? Vì sao không trả ngay sau show?

**Trả lời ngắn:** Trả làm hai đợt — một phần sau 48 giờ, phần cuối sau 14 ngày.

**Giải thích:** Vì cần khoảng đệm để xử lý khiếu nại và hoàn tiền. Nếu trả hết ngay sau show
mà hôm sau khách khiếu nại đòi hoàn tiền, nền tảng đã chuyển hết tiền đi thì lấy đâu ra để
hoàn. Các sàn thương mại điện tử đều làm vậy.

Tỷ lệ đợt đầu còn tuỳ **mức uy tín của venue**: venue mới nhận trước 50%, venue tiêu chuẩn
70%, venue hạng cao 80%. Venue làm tốt lâu dài thì bị giữ tiền ít hơn.

---

### 14. Khách đòi hoàn tiền thì xử lý thế nào?

**Trả lời ngắn:** Khách tạo yêu cầu, Admin duyệt, hệ thống gọi VNPay hoàn tiền và ghi bút
toán đảo vào sổ.

**Giải thích:** Ba bước: (1) khách nêu lý do, (2) admin xem xét và duyệt, (3) tiền chạy
ngược đúng đường nó đã đi. Không có chuyện admin bấm nút rồi tiền "biến mất" khỏi sổ — mỗi
lần hoàn tiền tạo thêm một bút toán đảo, nên vẫn nhìn thấy đủ lịch sử.

🔴 **Giới hạn thật — nói thẳng nếu bị hỏi:** VNPay sandbox của đồ án không cấp API hoàn tiền.
Code đã viết đúng cấu trúc gọi API thật; khi cổng không phản hồi, hệ thống chuyển sang ghi
nhận "chuyển khoản thủ công" để admin xử lý tay và vẫn ghi sổ đầy đủ. Đây là giới hạn môi
trường sandbox, không phải lỗi thiết kế.

---

### 15. Sơ đồ trạng thái vé có `Refunded` mà sao không đường nào tới?

**Trả lời ngắn:** Đúng — đó là trạng thái được khai báo nhưng không nơi nào trong code gán
tới. Vé hoàn tiền thực tế kết thúc ở `Cancelled` kèm bút toán đảo.

**Giải thích:** Nhóm phát hiện điều này khi đối chiếu sơ đồ với code, và **cố ý vẽ nó kèm
ghi chú "không tới được"** thay vì bỏ đi. Lý do: trạng thái đó vẫn tồn tại trong database,
người đọc schema sẽ thấy nó, nên sơ đồ phải phản ánh đúng sự thật.

Đây là câu **bạn nên chủ động nói ra** ở slide State Machine chứ đừng đợi bị hỏi — nó chứng
minh nhóm đối chiếu sơ đồ với code thật chứ không vẽ cho đẹp.

---

## Phần 3 — Kỹ thuật, giải thích dễ hiểu

### 16. Kiến trúc hệ thống là gì? Vì sao phải chia tầng?

**Trả lời ngắn:** Clean Architecture — chia 4 tầng, và **mọi phụ thuộc đều chỉ vào trong**,
hướng về tầng chứa luật nghiệp vụ.

**Giải thích:** Hình dung như củ hành. Lõi trong cùng là **luật nghiệp vụ** — "vé đã dùng
thì không huỷ được", "không bán quá số ghế". Lớp ngoài là những thứ *có thể thay đổi* —
database nào, cổng thanh toán nào, giao diện web hay mobile.

Quy tắc: lớp ngoài biết lớp trong, **lớp trong không biết gì về lớp ngoài**. Lợi ích cụ thể:
kiểm thử toàn bộ luật nghiệp vụ mà **không cần bật database**, vì lõi không biết database
tồn tại. Và nếu mai đổi từ SQL Server sang PostgreSQL, luật nghiệp vụ không phải sửa một dòng.

---

### 17. CQRS là gì và vì sao dùng?

**Trả lời ngắn:** Tách rõ hai loại thao tác: **Command** (thay đổi dữ liệu) và **Query**
(chỉ đọc). Mỗi thao tác là một class riêng.

**Giải thích:** Không dùng CQRS thì thường có một class khổng lồ tên `TicketService` chứa 30
hàm — mua vé, huỷ vé, xem vé, chuyển vé... Sửa một hàm dễ vỡ hàm khác, và hai người trong
nhóm sửa cùng file thì xung đột liên tục.

Với CQRS, "mua vé" là một file riêng, "xem danh sách vé" là file riêng. Ai làm gì rõ ràng,
và tìm code rất nhanh: muốn biết logic mua vé nằm đâu thì mở đúng thư mục tên đó.

**Lợi ích thật đã dùng:** vì tách rõ đọc/ghi, hệ thống chỉ mở transaction database cho lệnh
**ghi**. Lệnh đọc không mở transaction — nhẹ hơn, nhanh hơn.

---

### 18. Cơ sở dữ liệu thiết kế thế nào?

**Trả lời ngắn:** 68 bảng, 95 quan hệ, chia theo 9 miền nghiệp vụ (tài khoản, venue, show,
vé, tiền, đồ ăn, cá nhân hoá, vận hành...).

**Giải thích:** Không vẽ hết 68 bảng vào một hình vì không ai đọc nổi. Nhóm chia thành 9 sơ
đồ theo miền, mỗi sơ đồ trả lời một câu hỏi. Bảng thuộc miền khác thì vẽ nét đứt để biết nó
ở đâu mà không lặp lại.

**Nếu bị hỏi về chuẩn hoá:** Dữ liệu ở dạng chuẩn 3 (3NF) — không lưu trùng lặp. Ngoại lệ
có chủ đích là các trường **snapshot** của tiền (câu 11): cố ý lưu lại giá trị tại thời điểm
giao dịch, vì đó là dữ liệu lịch sử chứ không phải dữ liệu trùng.

---

### 19. API bảo mật thế nào? Làm sao biết ai được gọi cái gì?

**Trả lời ngắn:** Đăng nhập trả về một **token** có hạn; mỗi lần gọi API phải kèm token đó;
hệ thống đọc token biết bạn là ai, vai trò gì, rồi mới cho phép.

**Giải thích:** Giống thẻ ra vào công ty. Đăng nhập một lần, nhận thẻ. Mỗi cửa quẹt thẻ, máy
đọc thấy "nhân viên phòng kế toán" thì cho vào phòng kế toán, không cho vào phòng server.

Điểm quan trọng: hệ thống đặt **mặc định là CẤM**. Một API mới viết mà lập trình viên quên
khai báo quyền thì nó **tự động bị chặn**, chứ không phải tự động mở. Sai sót do quên sẽ dẫn
tới "không dùng được" chứ không dẫn tới "lộ dữ liệu".

---

### 20. Mật khẩu lưu như thế nào?

**Trả lời ngắn:** Không lưu mật khẩu. Chỉ lưu một chuỗi băm một chiều — từ chuỗi đó không
tính ngược lại được mật khẩu gốc.

**Giải thích:** Nếu database bị lộ, kẻ tấn công cũng không đọc được mật khẩu của ai. Kể cả
admin hệ thống cũng không xem được mật khẩu người dùng — đó là lý do khi quên mật khẩu thì
hệ thống bắt đặt lại chứ không gửi lại mật khẩu cũ.

---

### 21. Vì sao mật khẩu bắt 15 ký tự mà không bắt phải có chữ hoa, số, ký tự đặc biệt?

**Trả lời ngắn:** Vì đó đúng theo khuyến nghị hiện hành của NIST và OWASP — **độ dài quan
trọng hơn độ phức tạp**, và quy tắc bắt buộc ký tự đặc biệt phản tác dụng.

**Giải thích:** Câu này trả lời được sẽ rất ghi điểm. Khi bắt "phải có 1 hoa, 1 số, 1 ký tự
đặc biệt", người dùng không tạo mật khẩu mạnh hơn — họ tạo `Password@123`. Máy tính đoán
chuỗi đó trong vài giây vì nó là **khuôn mẫu đoán được**.

Ngược lại một câu dài dễ nhớ như `phongtranhacsong2026` dài 20 ký tự, không có ký tự đặc
biệt nào, nhưng khó đoán hơn nhiều lần. Vì vậy chuẩn mới bỏ quy tắc thành phần và tăng độ
dài tối thiểu lên 15 (khi hệ thống chưa có xác thực 2 lớp).

**Nguồn:** NIST SP 800-63B §5.1.1.2 và OWASP Authentication Cheat Sheet — ghi rõ trong
comment ngay tại file kiểm tra mật khẩu, mở ra chỉ được nếu bị hỏi.

---

### 22. Job chạy nền là gì? Hệ thống có bao nhiêu?

**Trả lời ngắn:** 30 loại job, trong đó 22 job chạy lặp theo lịch. Chúng làm những việc
không cần người bấm nút.

**Giải thích:** Ví dụ dễ hiểu nhất: khách giữ chỗ 15 phút rồi bỏ đi không thanh toán. Không
ai bấm nút "thả chỗ" cả — có một job cứ vài phút chạy một lần, tìm các chỗ giữ quá hạn và
dọn đi. Tương tự: nhắc show sắp diễn ra, đối soát tiền cho venue, kiểm tra sổ cái, cảnh báo
khi sắp trễ hạn kiểm duyệt 24 giờ.

---

### 23. Livestream và donate hiển thị realtime bằng cách nào?

**Trả lời ngắn:** Dùng kết nối hai chiều luôn mở giữa trình duyệt và server, nên server chủ
động đẩy dữ liệu xuống mà không cần trình duyệt hỏi liên tục.

**Giải thích:** Cách cũ là trình duyệt cứ 5 giây hỏi server "có gì mới không?" — tốn tài
nguyên mà vẫn trễ. Cách này giống giữ một cuộc gọi điện thoại mở sẵn: khi có người donate,
server "nói" ngay xuống tất cả người đang xem, hiệu ứng hiện lên tức thì.

Riêng **video** thì không đi qua server của hệ thống — nó đi qua dịch vụ chuyên dụng
(Mux/Cloudflare Stream), vì truyền video cần hạ tầng phân phối toàn cầu mà một server đơn lẻ
không kham nổi.

---

### 24. Hệ thống deploy ở đâu?

**Trả lời ngắn:** Microsoft Azure — API chạy trên App Service, database là Azure SQL, giao
diện web trên Static Web Apps, dịch vụ ghép ảnh panorama chạy riêng trên Container Apps.

**Giải thích:** Nói thêm được thì nói: mọi mật khẩu và khoá API đều để trong Key Vault chứ
không nằm trong code — nên đẩy code lên GitHub công khai cũng không lộ gì. Việc build, chạy
test và deploy đều tự động khi có thay đổi.

---

## Phần 4 — Nâng cao: các tình huống hội đồng hay vặn

### 25. Hai người cùng bấm mua chiếc vé cuối cùng thì sao?

**Trả lời ngắn:** Chỉ một người mua được. Hệ thống dùng **khoá ở tầng database** theo từng
show, nên hai yêu cầu bị xếp hàng xử lý lần lượt chứ không cùng lúc.

**Giải thích:** Đây là câu kinh điển và là chỗ nhiều đồ án sai. Vấn đề: nếu không khoá, cả
hai yêu cầu cùng đọc thấy "còn 1 vé", cả hai cùng thấy hợp lệ, cả hai cùng ghi vào → bán quá
số ghế.

Giải pháp giống phòng thử đồ trong shop: chỉ một người vào một lúc, người sau phải đợi. Ở
đây "phòng thử đồ" được khoá theo từng show — hai show khác nhau vẫn bán song song bình
thường, không làm chậm hệ thống.

**Nếu bị hỏi sâu:** Khoá này đặt ở **SQL Server** (`sp_getapplock`), không phải trong bộ nhớ
ứng dụng. Khác biệt rất quan trọng: nếu khoá trong bộ nhớ, khi chạy 2 server song song thì
mỗi server có khoá riêng và vẫn bán quá. Khoá ở database thì mọi server đều tuân theo cùng
một khoá. Trong code có cả hai bản — bản bộ nhớ chỉ dùng cho môi trường test.

---

### 26. Khách tắt trình duyệt ngay sau khi trả tiền thì vé có mất không?

**Trả lời ngắn:** Không. Vé vẫn được xác nhận, vì cái xác nhận là VNPay gọi vào server, không
phải trình duyệt quay về.

**Giải thích:** Có hai đường tin về sau khi thanh toán. Đường thứ nhất: trình duyệt khách
quay lại trang web — đường này **không đáng tin**, khách tắt máy, mất mạng, hết pin là mất.
Đường thứ hai: VNPay gọi thẳng vào server hệ thống báo kết quả — đường này chạy giữa hai máy
chủ, không phụ thuộc khách.

Hệ thống lấy đường thứ hai làm căn cứ. Trình duyệt quay về chỉ để **hiển thị** cho khách xem,
không dùng để ghi nhận tiền.

---

### 27. Nếu VNPay gọi lại 2 lần thì khách có bị trừ tiền 2 lần không?

**Trả lời ngắn:** Không. Hàm xử lý được viết **idempotent** — gọi bao nhiêu lần thì kết quả
vẫn như gọi một lần.

**Giải thích:** Cổng thanh toán nào cũng có cơ chế gọi lại khi không nhận được phản hồi (mạng
chập chờn chẳng hạn). Nên hệ thống phải giả định "sẽ bị gọi lại nhiều lần".

Cách xử lý: trước khi làm gì, kiểm tra "giao dịch này đã xác nhận chưa?". Nếu rồi thì trả lời
"OK, đã xong" và **không làm gì thêm** — không ghi sổ lần hai, không tạo vé lần hai.

Ví von: giống nút thang máy. Bấm 10 lần thì thang cũng chỉ đến một lần.

---

### 28. Nếu giữ chỗ mà không thanh toán, ghế có bị khoá vĩnh viễn không?

**Trả lời ngắn:** Không. Job nền dọn các bản ghi giữ chỗ quá 15 phút.

**Giải thích:** Điểm tinh tế đáng nói: khi dọn, hệ thống chỉ **xoá bản ghi giữ chỗ** chứ
không phải "cộng lại số ghế trống". Vì số ghế còn trống được **tính động** mỗi lần hỏi: tổng
ghế − vé đã xác nhận − số chỗ đang giữ còn hiệu lực.

Cách này an toàn hơn hẳn kiểu có một biến đếm rồi cộng/trừ. Với biến đếm, chỉ cần một lần trừ
mà quên cộng lại là số ghế sai vĩnh viễn và không ai biết. Với cách tính động, không có gì để
sai.

---

### 29. Nếu ghi nhận thanh toán thành công nhưng tạo vé bị lỗi thì sao?

**Trả lời ngắn:** Cả hai cùng nằm trong một **transaction** — hoặc thành công cả hai, hoặc
huỷ cả hai. Không có trạng thái nửa vời.

**Giải thích:** Giống chuyển khoản ngân hàng: trừ tài khoản A và cộng tài khoản B phải cùng
thành công. Nếu trừ xong mà cộng lỗi thì tiền biến mất. Transaction đảm bảo nếu bất kỳ bước
nào lỗi, mọi thứ quay về như chưa từng xảy ra.

**Nếu bị hỏi sâu:** Trong hệ thống, transaction được mở tự động cho mọi lệnh *ghi* thông qua
một lớp trung gian dùng chung — lập trình viên không phải nhớ mở/đóng thủ công ở từng chỗ,
nên không thể quên.

Nhóm cũng từng gặp một lỗi thật thuộc nhóm này: khi thanh toán xong mà venue chưa khai tài
khoản ngân hàng, hệ thống rollback luôn cả phần xác nhận vé — dù tiền đã bị VNPay trừ thật.
Đã sửa: thiếu tài khoản ngân hàng chỉ chặn bước đối soát, không được huỷ giao dịch đã thu tiền.

---

### 30. Một vé có thể chia cho nhiều người cùng xem livestream không?

**Trả lời ngắn:** Giới hạn 2 thiết bị cùng lúc trên một vé. Thiết bị thứ 3 bị từ chối kết nối.

**Giải thích:** Nếu không giới hạn, một người mua vé rồi chia tài khoản cho cả nhóm bạn —
doanh thu của venue mất trắng. Nhưng cấm hoàn toàn chỉ 1 thiết bị cũng bất tiện: nhiều người
mở máy tính rồi chuyển sang điện thoại, hoặc mạng rớt rồi vào lại.

Con số 2 là điểm cân bằng, và nằm trong file cấu hình nên đổi được mà không cần sửa code.

**Nói thêm được thì rất ghi điểm:** đây là lỗ hổng nhóm tự phát hiện khi rà soát — mã truy
cập livestream vốn đã được sinh ra nhưng **chưa nơi nào kiểm tra**, nghĩa là trên lý thuyết
một vé stream được cho vô hạn thiết bị. Đã bịt lại.

---

### 31. Nếu VNPay, Mux hay dịch vụ AI bị sập thì hệ thống có sập theo không?

**Trả lời ngắn:** Không đồng loạt. Mỗi dịch vụ ngoài chỉ ảnh hưởng đúng phần của nó, và phần
AI được thiết kế **suy giảm nhẹ nhàng** chứ không chặn người dùng.

**Giải thích:** Tách theo mức độ nghiêm trọng:

- **VNPay sập** → không thanh toán online được, nhưng vẫn xem show, vẫn bán vé tại quầy bằng
  tiền mặt, vẫn soát vé đã mua.
- **Mux sập** → không livestream được, nhưng show trực tiếp tại venue và bán vé vẫn chạy.
- **AI (Gemini) sập** → **không ảnh hưởng gì tới người dùng**. AI ở đây chỉ chấm điểm rủi ro
  để xếp thứ tự ưu tiên kiểm duyệt. Nếu gọi lỗi, hệ thống trả điểm trung tính và admin vẫn
  duyệt bình thường. AI **không bao giờ tự quyết định** thay người.
- **SMS sập** → xác thực vẫn chạy vì luồng chính dùng **email**, không phải SMS.

---

### 32. Làm sao đảm bảo chủ venue A không xem được dữ liệu venue B?

**Trả lời ngắn:** Mọi truy vấn đều lọc theo chủ sở hữu ở tầng server, lấy từ token đăng nhập
chứ không lấy từ tham số người dùng gửi lên.

**Giải thích:** Đây là lỗ hổng phổ biến nhất trong hệ thống nhiều người thuê chung. Kiểu sai
điển hình: API nhận `venueId` từ client rồi trả dữ liệu — người dùng chỉ cần sửa số trên URL
là xem được venue người khác.

Cách làm đúng: server **không tin** `venueId` client gửi lên. Nó lấy danh tính từ token, tra
ra venue mà người này thực sự sở hữu, rồi mới truy vấn. Người dùng có sửa URL cũng vô ích.

---

### 33. Có chống đăng nhập dò mật khẩu (brute force) không?

**Trả lời ngắn:** Có — theo dõi số lần đăng nhập sai và khoá tạm sau nhiều lần thất bại.

**Giải thích:** Ngoài ra còn một thứ tinh tế hơn: **chống dò email**. Khi đăng ký với email
đã tồn tại, hệ thống vẫn trả về kết quả **y hệt** như đăng ký thành công. Vì nếu báo "email
đã tồn tại", kẻ tấn công có thể thử hàng loạt email để biết ai có tài khoản trên hệ thống —
đó đã là rò rỉ thông tin rồi.

---

### 34. Dữ liệu cá nhân được xử lý thế nào?

**Trả lời ngắn:** Theo Luật 91/2025/QH15 — người dùng xuất được dữ liệu của mình và yêu cầu
xoá. Nhưng "xoá" ở đây là **ẩn danh tại chỗ**, không xoá cứng bản ghi.

**Giải thích:** Đây là chỗ hai luật xung đột và nhóm xử lý có chủ đích. Luật bảo vệ dữ liệu
cá nhân nói phải xoá khi người dùng yêu cầu. Luật Kế toán nói chứng từ tài chính phải giữ 10
năm. Nếu xoá cứng tài khoản, các hoá đơn liên quan mất tham chiếu → vi phạm luật kế toán.

Giải pháp: giữ bản ghi nhưng **xoá sạch thông tin nhận dạng** — tên, email, số điện thoại
thành giá trị ẩn danh. Hoá đơn vẫn còn, vẫn đối chiếu được, nhưng không còn biết là của ai.

Ngoài ra: **đồng ý dùng AI mặc định là TẮT** — người dùng phải chủ động bật, không phải bỏ
tick để tắt.

---

## Phần 5 — Điểm yếu và câu hỏi khó

### 35. Test coverage bao nhiêu? Có test nào fail không?

**Trả lời ngắn:** 795 test, 791 pass, tỷ lệ 99,5%. **Có 4 test đang fail** và nhóm biết rõ
đó là những test nào.

**Giải thích:** Chia theo tầng: backend 460 test, frontend 307 test, end-to-end 28 test.

⚠️ **Nói thẳng con số 4 test fail.** Đừng làm tròn thành 100%. Nếu hội đồng phát hiện sau khi
bạn nói 100% thì mất uy tín toàn bộ phần trình bày. Chuẩn bị sẵn: đó là các test fail từ
trước, không liên quan tính năng đang bảo vệ. **Chạy lại suite trước hôm bảo vệ** để cập nhật
con số chính xác.

---

### 36. 🔴 Test chạy trên SQLite nhưng production dùng SQL Server — có rủi ro không?

**Trả lời ngắn:** Có, và nhóm **đã từng bị đúng lỗi đó** — một bug chỉ xuất hiện trên SQL
Server mà toàn bộ test trên SQLite đều pass.

**Giải thích:** Hai loại database không hành xử giống nhau ở vài chỗ: quy tắc xoá dây chuyền
qua nhiều đường, cách lưu kiểu enum, và các lệnh khoá đặc thù. SQLite nhanh và nhẹ nên thích
hợp chạy test tự động liên tục, nhưng không phản ánh 100% môi trường thật.

**Cách nhóm xử lý:** trước mỗi bản release, chạy lại toàn bộ suite trên **SQL Server thật**,
không chỉ dựa vào CI mặc định. Và có kiểm thử chấp nhận (UAT) chạy trên hệ thống thật đang
deploy, với tài khoản thật cho từng vai trò.

Trả lời được câu này rất ghi điểm vì nó cho thấy nhóm hiểu **giới hạn của chính bộ test của
mình** — điều rất ít đồ án nói ra.

---

### 37. 🔴 Ảnh và file upload lưu ở đâu?

**Trả lời ngắn:** Hiện lưu trên **ổ đĩa của máy chủ ứng dụng**, chưa chuyển sang Azure Blob
Storage như trong thiết kế.

**Giải thích:** Đây là điểm yếu thật, nên nói thẳng và nói kèm hệ quả để chứng minh mình hiểu:

- Nếu chạy nhiều server song song, mỗi server có ổ đĩa riêng → ảnh upload lên server 1, người
  dùng vào server 2 sẽ không thấy ảnh.
- Nếu App Service bị tạo lại, file có nguy cơ mất.
- Không có CDN nên ảnh tải chậm hơn với người dùng ở xa.

**Vì sao chưa làm:** đây là việc thay một lớp cài đặt lưu trữ, không ảnh hưởng luật nghiệp vụ
— do kiến trúc đã tách interface sẵn, đổi sang Blob Storage là thay một class chứ không phải
sửa toàn hệ thống. Trong phạm vi đồ án chạy một server thì chưa gây lỗi thực tế, nên nhóm ưu
tiên các phần nghiệp vụ trước.

---

### 38. Nếu scale ra nhiều server thì cái gì sẽ hỏng?

**Trả lời ngắn:** Ba thứ: lưu trữ file (câu 37), bộ đếm giới hạn thiết bị livestream, và bộ
đếm chống brute-force — vì cả ba đang giữ trong bộ nhớ của từng tiến trình.

**Giải thích:** Khoá bán vé thì **không nằm trong danh sách này** — nó đã đặt ở database nên
an toàn khi scale. Nhóm đã cân nhắc đúng chỗ quan trọng nhất trước.

Trả lời được câu này cho thấy bạn hiểu hệ thống ở mức vận hành, không chỉ mức code.

---

### 39. Vì sao Admin phải tạo trực tiếp trong database, không có màn hình đăng ký?

**Trả lời ngắn:** Cố ý. Không có bất kỳ đường nào để tự nâng quyền lên Admin từ bên ngoài.

**Giải thích:** Nếu có API tạo Admin, thì chỉ cần một lỗi phân quyền ở đúng API đó là kẻ tấn
công chiếm toàn bộ hệ thống. Bỏ hẳn đường đó đi là cách phòng thủ đơn giản và chắc chắn nhất
— không thể khai thác một thứ không tồn tại.

API đăng ký chỉ chấp nhận hai vai trò: `Audience` và `Owner`. Gửi lên `Admin` bị từ chối ngay
ở tầng kiểm tra dữ liệu.

---

### 40. 🔴 SMS có gửi được về số điện thoại Việt Nam không?

**Trả lời ngắn:** Không, và đó là **rào cản pháp lý của Việt Nam**, không phải lỗi code.

**Giải thích:** Nhà mạng Việt Nam chặn tin nhắn gửi từ số điện thoại thường của nhà cung cấp
nước ngoài. Muốn gửi được phải đăng ký Brand Name/Sender ID, yêu cầu **giấy phép kinh doanh
Việt Nam thật** và mất khoảng 5 tuần duyệt — đồ án không có pháp nhân nên không đạt được.

**Cách demo:** dùng số nước ngoài gửi tới số nước ngoài để chứng minh phần tích hợp hoạt động
đúng. Luồng xác thực chính của hệ thống dùng **email**, nên hạn chế này không chặn tính năng nào.

Nói kèm điều này: nhóm đã thử cả nhà cung cấp nội địa Việt Nam, họ cũng yêu cầu bước đăng ký
tương đương — **đây là quy định chống spam ở cấp nhà mạng, mọi nhà cung cấp đều vướng như nhau.**

---

### 41. Soát vé lúc mất mạng thì làm sao?

**Trả lời ngắn:** Hiện chưa có chế độ offline — cần mạng để soát vé.

**Giải thích:** Đây là **rủi ro đã được ghi nhận có chủ đích**, không phải bỏ sót. Nói được
hướng giải quyết sẽ cứu câu trả lời: cách làm chuẩn là cho app nhân viên tải trước danh sách
vé của show, soát offline rồi đồng bộ lại khi có mạng. Việc này cần xử lý xung đột (cùng một
vé bị soát ở hai cửa) nên không nhỏ, và nằm ngoài phạm vi 15 tuần.

---

### 42. Hệ thống chịu được bao nhiêu người truy cập cùng lúc?

**Trả lời ngắn:** Chưa đo bằng kiểm thử tải chính thức, nên không đưa ra con số để tránh nói sai.

**Giải thích:** Nói cấu hình thật thì tốt hơn con số bịa: hiện chạy trên App Service gói B1 và
Azure SQL gói S0 — cấu hình cho môi trường đồ án, không phải cấu hình chịu tải thật. Kiến trúc
**có thể** scale ngang vì khoá bán vé đã đặt ở database, nhưng ba thứ ở câu 38 phải xử lý trước.

**Đừng bịa con số.** Nếu hội đồng gặng, nói rõ điều cần đo là gì: điểm gãy khi nhiều người cùng
mua vé một show, vì đó là chỗ có khoá và là điểm nghẽn tự nhiên của hệ thống.

---

### 43. Phần nào khó nhất khi làm?

**Trả lời ngắn:** Xử lý dòng tiền — vì đây là phần **sai một lần là mất tiền thật**, không như
lỗi giao diện có thể sửa sau.

**Giải thích:** Cụ thể ba thứ khó: (1) đảm bảo không bán quá số ghế khi nhiều người mua cùng
lúc; (2) xử lý việc cổng thanh toán gọi lại nhiều lần mà không nhân đôi giao dịch; (3) thiết kế
sổ kép sao cho mọi giao dịch đều đối chiếu lại được.

Bạn cũng có thể kể một bug thật đã gặp và cách sửa (xem câu 29) — kể được một lỗi cụ thể mình
tự tìm ra và tự sửa thuyết phục hơn nhiều so với nói chung chung "phần thanh toán khó".

---

### 44. Nếu làm lại thì em sẽ làm khác chỗ nào?

**Trả lời ngắn:** Ba thứ: dùng Blob Storage cho file ngay từ đầu, chạy test trên SQL Server
thật sớm hơn, và làm kiểm thử tải sớm hơn.

**Giải thích:** Câu này hội đồng hỏi để xem bạn có **tự đánh giá** được không. Đừng trả lời
"em thấy ổn rồi" — nghe như không nhận ra vấn đề. Cũng đừng chê hết mọi thứ. Ba điều trên là
thật, cụ thể, và mỗi điều đều gắn với một bài học đã rút ra ở trên.

---

### 45. Điểm mạnh nhất của hệ thống là gì?

**Trả lời ngắn:** Toàn bộ dòng tiền đều **truy vết được** — từ lúc khán giả trả tiền tới lúc
nghệ sĩ nhận, không có bước nào là hộp đen.

**Giải thích:** Cụ thể hoá bằng ba thứ kiểm chứng được:

1. **Sổ kép** — mọi chuyển động tiền có hai vế cân bằng, có job kiểm tra hằng ngày.
2. **Snapshot tỷ lệ** — con số đã phát sinh không bị tính lại khi đổi cấu hình.
3. **Mọi tham số nghiệp vụ nằm trong cấu hình**, không viết cứng — đổi được mà không cần lập
   trình viên.

Và một điểm nữa đáng nói ở phần cuối: **các sơ đồ trong báo cáo được kiểm tra tự động khớp với
code** — tên use case, tên bảng, tên trạng thái đều phải tồn tại thật trong mã nguồn, nếu không
thì build fail. Nghĩa là tài liệu không thể lệch khỏi hệ thống thật.

---

## Ba câu nên chủ động nói trước khi bị hỏi

Nói trước ba điều này ở đúng slide của nó — chủ động thừa nhận mạnh hơn bị vặn:

1. **4 test đang fail** (slide Test Report) — nói kèm lý do, đừng làm tròn 100%.
2. **Trạng thái `Refunded` không tới được** (slide State Machine) — chứng minh nhóm đối chiếu
   sơ đồ với code thật.
3. **SMS chưa gửi được về số Việt Nam** (slide kiến trúc/tích hợp) — kèm giải thích đây là rào
   cản pháp lý cấp nhà mạng, mọi nhà cung cấp đều vướng.

## Ba thứ tuyệt đối đừng bịa

- **Con số chịu tải** — chưa đo thì nói chưa đo (câu 42).
- **Số liệu thị trường** — chưa tìm được nguồn thì đừng bịa số trên slide.
- **Tỷ lệ test pass** — đọc đúng con số suite vừa chạy, không đọc con số mong muốn.
