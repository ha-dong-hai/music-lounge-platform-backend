# 24 — Sơ đồ luồng View (text-arrow diagrams)

← [17](17-audience-journey.md)-[22](22-performer-presence.md) (journey nguồn) · [23-view-catalog.md](23-view-catalog.md) (73 view nguồn)
>
> **Bản gộp cuối cùng** (view + sơ đồ luồng + giao thoa + tổng quan + danh sách giả định, cùng 1 file để bàn giao): [View-Design-Spec.md](View-Design-Spec.md).

> **Ký hiệu**: `[Tên View]` = đúng tên view trong [23-view-catalog.md](23-view-catalog.md) (bấm để tìm heading `### Tên View` tương ứng). `→ (hành động)` = actor bấm/submit gì. `↳` = 1 nhánh rẽ (dùng khi nhánh có chuỗi bước riêng dài); `/` = 2 kết quả cùng 1 điểm rẽ, viết trên cùng dòng khi ngắn gọn được. `(thụ động)` = actor không thao tác, chỉ nhận kết quả từ hành động actor khác/hệ thống.
> **Nguồn**: Phần 1 dựng lại 1:1 từ các bảng journey đã có ở docs/17-21 (không thêm bước mới). Phần 2 gộp + khử trùng lặp từ 4 bảng "Tổng hợp điểm giao thoa real-time" cuối mỗi doc journey 17/18/19/20. Phần 3 đối chiếu cột "Actor truy cập" của từng view ở docs/23.
> **Cập nhật**: 2026-08-13.

---

## Phần 1 — Sơ đồ luồng theo từng actor

### 1.1 Audience

**J1. Đăng ký & thiết lập tài khoản**
```
[Đăng ký tài khoản] → (nhập OTP) → [Xác thực Email OTP]
  ↳ (mã đúng) → [Danh sách/Tìm kiếm Show] (đã đăng nhập)
  ↳ (mã sai/hết hạn) → [Xác thực Email OTP] (gửi lại)
[Đăng nhập] → (quên mật khẩu) → [Quên/Đặt lại mật khẩu] → [Đăng nhập]
[Danh sách/Tìm kiếm Show] → (tự chọn) → [Xác thực định danh (KYC CCCD)] / [Cài đặt sở thích gợi ý AI]
```

**J2. Khám phá & theo dõi show/venue**
```
[Danh sách/Tìm kiếm Show] → (chọn 1 show) → [Chi tiết Show]
[Chi tiết Show] → (xem venue tổ chức) → [Chi tiết Venue] → (xem tour) → [Tour ảo 360°]
[Chi tiết Show] → (bấm gợi ý) → [Gợi ý dành cho bạn] → (chọn item) → [Chi tiết Show]
[Chi tiết Venue] → (follow) → ở lại [Chi tiết Venue] (nút đổi trạng thái)
[Chi tiết Show] → (wishlist) → ở lại [Chi tiết Show]
[Danh sách đã theo dõi/yêu thích] ← (menu tài khoản, độc lập)
```

**J3. Mua vé xem show**
```
[Chi tiết Show] → (bấm "Mua vé") → [Chọn vé & Giữ chỗ]
[Chọn vé & Giữ chỗ] → (giữ chỗ 15 phút)
  ↳ (đổi ý, huỷ) → [Chọn vé & Giữ chỗ] (reset, nhả chỗ)
  ↳ (tiếp tục mua) → VNPay (ngoài hệ thống) → [Kết quả thanh toán]
      → (thành công) → [Chi tiết vé]
      → (thất bại) → [Chọn vé & Giữ chỗ] (giữ chỗ mới nếu còn)
```

**J4. Xem livestream & donate**
```
[Chi tiết Show] → (Ongoing + có vé, bấm "Vào xem live") → [Phòng xem Livestream]
[Phòng xem Livestream] → (chọn nghệ sĩ, bấm donate) → [Form Donate]
[Form Donate] → (submit) → VNPay → [Kết quả thanh toán]
  → (thành công) → [Phòng xem Livestream] (quay lại xem, ticker tự cập nhật) / [Lịch sử Donate của tôi]
  → (thất bại) → [Form Donate] (thử lại)
[Lịch sử Donate của tôi] ← (thụ động, thông báo DonationConfirmed)
```

**J5. Đặt F&B tại venue**
```
[Chi tiết Venue] / [Chi tiết Show] → (xem menu) → [Đặt món F&B]
[Đặt món F&B] → (đặt xong) → [Đơn F&B của tôi]
[Đơn F&B của tôi] ← (thụ động, Staff cập nhật trạng thái — không push riêng, phải tự vào xem lại)
```

**J6. Quản lý vé đã mua**
```
[Vé của tôi] → (chọn 1 vé) → [Chi tiết vé]
[Chi tiết vé]
  ↳ (chuyển nhượng) → (thụ động, chờ người nhận `transfer/accept`) → ở lại [Chi tiết vé] (đổi chủ)
  ↳ (huỷ yêu cầu chuyển nhượng đang chờ) → ở lại [Chi tiết vé]
  ↳ (huỷ vé) → [Yêu cầu hoàn tiền của tôi]
[Yêu cầu hoàn tiền của tôi] ← (thụ động, Admin duyệt/từ chối)
```

**J7. Đánh giá show sau khi kết thúc**
```
[Chi tiết Show] (Ended, có vé, trong 7 ngày, chưa đánh giá) → (bấm "Đánh giá") → [Đánh giá Show]
[Đánh giá Show] → (submit) → [Chi tiết Show] (nút ẩn đi, không đánh giá lại được)
```

**J8. Gửi khiếu nại**
```
[Chi tiết vé] / [Chi tiết Show] → (báo cáo vấn đề) → [Gửi khiếu nại]
[Gửi khiếu nại] → (submit, đã đăng nhập) → [Khiếu nại của tôi]
[Khiếu nại của tôi] ← (thụ động, thông báo ComplaintUpdate) — nếu ResolvedAction liên quan hoàn tiền vé → [Yêu cầu hoàn tiền của tôi]
```

**J9. Thông báo & hồ sơ cá nhân** *(chạy song song mọi journey khác, không tuyến tính)*
```
[Hộp thư thông báo] ← (icon chuông, mọi màn hình đã đăng nhập)
[Hồ sơ cá nhân] → [Xác thực định danh (KYC CCCD)] / [Cài đặt sở thích gợi ý AI] / [Quyền riêng tư & Dữ liệu cá nhân]
[Quyền riêng tư & Dữ liệu cá nhân] → (khoá tạm / xoá vĩnh viễn) → [Đăng nhập] (session bị huỷ ngay)
```

---

### 1.2 Owner

**J1. Đăng ký & thiết lập tài khoản Owner**
```
[Đăng ký tài khoản] (Role=Owner) → [Xác thực Email OTP] → (thành công) → [Danh sách Venue của tôi]
[Danh sách Venue của tôi] → (bắt buộc trước J2) → [Quản lý Tài khoản ngân hàng] → (thụ động, chờ Admin xác minh)
```

**J2. Tạo & đưa venue vào hoạt động**
```
[Danh sách Venue của tôi] → (tạo mới) → [Tạo/Sửa Venue] → (submit, venue Pending) → (thụ động, chờ Admin)
  ↳ (Approved) → [Tạo/Sửa Venue] (bổ sung ảnh/giấy phép) → [Sơ đồ chỗ ngồi (Zone Editor)] → [Quản lý Nhân viên (Staff)]
      → (tự chọn) → [Tour ảo 360° (quản lý)] / [Nội dung bổ sung Venue (Gallery & Tiêu chí gợi ý)] / [Quản lý Menu F&B]
  ↳ (Rejected) → [Tạo/Sửa Venue] (tạo venue MỚI — không có "nộp lại đúng venue đó")
```

**J3. Đăng ký gói Subscription**
```
[Bảng giá gói Subscription] → (chọn gói) → [Gói Subscription của tôi] → (khởi tạo thanh toán) → VNPay → [Kết quả thanh toán]
  → (thành công) → [Gói Subscription của tôi] (Active, mở khoá J4)
  → (thất bại) → [Gói Subscription của tôi] (thử lại)
[Gói Subscription của tôi] → (sắp hết hạn) → (gia hạn) → [Kết quả thanh toán] → [Gói Subscription của tôi]
```

**J4. Tạo show mới & mở bán vé**
```
[Danh sách Show của tôi] → (tạo mới, cần Subscription Active) → [Tạo/Sửa Show] → (submit, Draft) → [Bảng điều khiển Show (Owner)]
[Bảng điều khiển Show (Owner)] → [Quản lý Hạng vé (Ticket Tier)] (≥1 bắt buộc)
[Bảng điều khiển Show (Owner)] → [Poster Show] (AI hoặc upload thủ công)
[Bảng điều khiển Show (Owner)] → [Khai báo pháp lý & Tác quyền]
[Bảng điều khiển Show (Owner)] → (nộp duyệt, cần venue Approved + ≥1 hạng vé) → (thụ động, chờ Admin)
  ↳ (Approved) → show công khai ngay → nối [Chi tiết Show] (phía Audience, xem 1.1 J2/J3)
  ↳ (Rejected) → [Tạo/Sửa Show] (sửa lại, về Draft)
```

**J5-A. Vận hành show Offline thuần**
```
[Bảng điều khiển Show (Owner)] → (bắt đầu) → ở lại (Ongoing) → (kết thúc) → ở lại (Ended, mở cửa sổ đánh giá 7 ngày)
```

**J5-B. Vận hành show Online (Livestream)**
```
[Bảng điều khiển Show (Owner)] → (tạo Livestream) → [Vận hành Livestream] → (thụ động, chờ Admin duyệt livestream — độc lập duyệt Show)
[Vận hành Livestream] → (lấy credentials, cắm OBS) → (bắt đầu phát — chặn nếu chưa duyệt HOẶC chưa khai VCPMC)
  → (thành công) → Ongoing → nối [Phòng xem Livestream] (phía Audience, xem 1.1 J4)
[Vận hành Livestream] → (kết thúc phát) → [Bảng điều khiển Show (Owner)] (Ended, mở cửa sổ đánh giá)
[Vận hành Livestream] ← (ngoại lệ, thụ động — Admin terminate) → (Terminated, ngắt kết nối mọi khán giả)
```

**J6. Điều chỉnh show đã publish**
```
[Bảng điều khiển Show (Owner)]
  ↳ (đổi lịch, chỉ khi Published) → ở lại (áp lại quy tắc 7-ngày-làm-việc)
  ↳ (đổi Offline→Online, 1 chiều) → ở lại (hoàn 100% vé vật lý Confirmed tự động)
  ↳ (đổi playback 2D/3D) → ở lại
  ↳ (huỷ show, chặn nếu Livestream đang Live) → ở lại (Cancelled, hoàn 100% mọi vé Confirmed)
```

**J7. Nhận & xử lý donate cho nghệ sĩ**
```
(thụ động, Audience donate thành công) → [Donate chờ xác nhận]
[Donate chờ xác nhận] → (acknowledge trong 24h) → [Donate chờ trả nghệ sĩ]
  ↳ (không thao tác kịp 24h) → (thụ động) → [Donate chờ trả nghệ sĩ] (hệ thống tự AutoConfirmed)
[Donate chờ trả nghệ sĩ] → (chuyển khoản thủ công ngoài hệ thống, rồi confirm-paid) → hoàn tất chu trình
  ↳ (chặn nếu Performer chưa có bank account) → [Quản lý Tài khoản ngân hàng] (đăng ký hộ trước)
```

**J8. Theo dõi doanh thu & tài chính** *(phần lớn thụ động)*
```
(thụ động, vé đầu tiên bán) → (thụ động, tới hạn giải ngân) → [Thu nhập tổng quan]
  ↳ (chưa có bank account) → chặn tạo lịch → [Quản lý Tài khoản ngân hàng]
  ↳ (tranche cuối, show kết thúc sớm bất thường) → (thụ động, chờ Admin xét PendingReview)
[Thu nhập tổng quan] → (xem chi tiết 1 venue) → [Thống kê Venue]
```

**J9. Vận hành F&B**
```
[Quản lý Menu F&B] → (tạo menu, thêm món) → menu xuất hiện ngay ở [Đặt món F&B] (phía Audience)
[Quản lý đơn F&B tại quầy] → (đặt hộ khách tại quầy) / (cập nhật trạng thái Pending→Preparing→Served→Paid)
```

**J10. Đối phó xử phạt & kháng cáo**
```
(thụ động, nhận quyết định xử phạt) → [Xử phạt & Kháng cáo của tôi]
[Xử phạt & Kháng cáo của tôi] → (gửi kháng cáo, chỉ khi Active) → (thụ động, chờ Admin hoặc auto-overturn quá SLA)
  ↳ (Overturned) → ở lại (lưu ý: không tự mở lại nếu còn phạt Active khác chồng lên)
  ↳ (Upheld) → ở lại
```

---

### 1.3 Staff

**J1. Được gán vào venue & đăng nhập lần đầu**
```
(thụ động, Owner gán qua [Quản lý Nhân viên (Staff)]) → [Đăng nhập] → (thành công, nhận claim lounge_id) → Trang chủ Staff
  ↳ (ngoại lệ, thụ động — Owner gỡ khỏi venue) → mất quyền vận hành từ lần gọi API kế tiếp
```

**J2. Bán vé tại quầy (Walk-in)**
```
[Bán vé tại quầy (Walk-in)] → (chọn hạng vé Physical + số lượng, bán ngay) → ở lại (QR sinh tại chỗ, đưa khách trực tiếp)
```

**J3. Check-in khán giả tại cửa**
```
[Check-in vé] → (quét xem trước, tách riêng) → (xác nhận check-in thật) → ở lại (reset, sẵn sàng quét tiếp)
```

**J4. Vận hành livestream** *(dùng chung view với Owner — xem 1.2 J5-B)*
```
[Vận hành Livestream] → (tạo/lấy credentials/bắt đầu/kết thúc — cùng quyền RequireVenueOperator như Owner)
```

**J5. Vận hành show Offline (bắt đầu/kết thúc)**
```
[Bảng điều khiển Show (Owner)] → (Staff chỉ dùng được nút bắt đầu/kết thúc — không sửa/huỷ/đổi lịch được, các nút đó RequireOwner)
  ↳ (show có Livestream đi kèm) → nút start/end ở đây tự ẩn, dùng [Vận hành Livestream] thay thế
```

**J6. Xử lý đơn F&B**
```
[Quản lý đơn F&B tại quầy] → (đặt hộ khách tại quầy) / (cập nhật trạng thái tuần tự, hoặc Cancelled bất kỳ lúc nào trước Paid)
```

---

### 1.4 Admin

> Không có journey "đăng ký" — Admin luôn bắt đầu thẳng từ [Đăng nhập] (tài khoản tạo sẵn qua sửa DB trực tiếp).

**J1. Duyệt venue mới**
```
[Duyệt Venue mới]
  ↳ (duyệt) → Owner mở khoá J4 (Owner) ngay lập tức
  ↳ (từ chối, kèm Reason) → Owner phải tạo venue mới (không "nộp lại")
```

**J2. Kiểm duyệt show & livestream**
```
[Kiểm duyệt Show & Livestream]
  ↳ (duyệt show) → show công khai ngay cho Audience
  ↳ (từ chối show, cần ReviewNote) → show về Draft cho Owner sửa
  ↳ (duyệt livestream) → mở khoá nút "Bắt đầu phát" cho Owner/Staff
  ↳ (từ chối livestream, cần ReviewNote) → Owner/Staff không phát được
```

**J3. Xử phạt venue & xử lý kháng cáo**
```
[Ra quyết định Xử phạt Venue] → (thụ động, Owner kháng cáo) → [Xử lý Kháng cáo]
[Xử lý Kháng cáo] (chỉ xử lý được trạng thái Appealed)
  ↳ (Overturned) → Owner nhận kết quả (không tự mở lại venue nếu còn phạt Active khác)
  ↳ (Upheld) → Owner nhận kết quả
  ↳ (không xử lý kịp SLA, thụ động) → (hệ thống tự Overturn giùm, cùng khoá tránh chồng quyết định)
```

**J4. Xử lý hoàn tiền (vé) & hoàn donate**
```
[Yêu cầu hoàn tiền (Refund Requests)]
  ↳ (từ chối) → không đụng tiền
  ↳ (duyệt, toàn phần/một phần) → đảo bút toán, co settlement chưa Released
[Yêu cầu hoàn tiền (Refund Requests)] → (escape-hatch, tự tạo thủ công) → ở lại (vẫn qua bước duyệt bình thường)
[Hoàn Donate] → (chỉ hợp lệ trước PerformerPaid) → đảo bút toán chặng 1, không gọi VNPay
```

**J5. Xác minh bank account**
```
(thụ động, Owner đăng ký ở [Quản lý Tài khoản ngân hàng] phía họ) → [Xác minh Tài khoản ngân hàng]
[Xác minh Tài khoản ngân hàng] → (xác minh) → settlement từng bị chặn ở Owner tự động retry ngay
```

**J6. Xử lý khiếu nại**
```
[Xử lý Khiếu nại (Admin)] → (quyết định, theo ResolvedAction)
  ↳ (Refund/Compensate, target=ticket) → tự tạo RefundRequest → nối [Yêu cầu hoàn tiền (Refund Requests)]
  ↳ (Refund/Compensate, target=donation) → không tự động, Admin tự vào [Hoàn Donate]
  ↳ (TakeDownContent, target=show) → gỡ show + hoàn 100% mọi vé (logic riêng, không dùng chung path Huỷ show của Owner)
  ↳ (action khác) → chỉ ghi nhận, không tự động hoá gì thêm
```

**J7. Quản lý người dùng**
```
[Quản lý Người dùng] → (tìm kiếm → chọn 1 user → xem KYC nếu cần)
  ↳ (khoá tài khoản) → ở lại (IsActive=false)
  ↳ (mở lại) → ở lại (IsActive=true)
```

**J8. Quản lý taxonomy & gói subscription**
```
[Quản lý Taxonomy nền tảng] → (thêm category/genre/mood/atmosphere — không sửa/xoá được, xem [23 §Tổng hợp mục 3])
[Quản lý Gói Subscription] → (tạo/sửa gói) → Owner thấy ngay ở [Bảng giá gói Subscription] (không ảnh hưởng ngược Owner đã mua)
```

**J9. Buộc dừng livestream vi phạm**
```
[Phòng xem Livestream] (Admin luôn xem được) → (phát hiện vi phạm) → (buộc dừng, kèm Reason)
  → mọi khán giả đang xem bị ngắt kết nối ngay + [Vận hành Livestream] (phía Owner/Staff) đổi trạng thái Terminated
```

**J10. Giám sát vận hành & tài chính** *(phần lớn phản ứng, không chủ động tìm)*
```
(thụ động, nhận cảnh báo từ 4 job nền) → [Hộp thư thông báo]
[Hộp thư thông báo] → [Kiểm tra Toàn vẹn Sổ cái] / [Thống kê toàn nền tảng] / [Bảng điều khiển Job nền]
[Bảng điều khiển Job nền] → (ép chạy 1 job) → kết quả phản ánh gián tiếp qua view liên quan (vd chạy job đối soát sổ cái → xem lại [Kiểm tra Toàn vẹn Sổ cái])
```

---

### 1.5 Khách chưa đăng nhập (Anonymous)

**J1. Duyệt catalog công khai**
```
[Danh sách/Tìm kiếm Show] → [Chi tiết Show] → [Chi tiết Venue] → [Tour ảo 360°]
[Chi tiết Show] → [Trang cá nhân Nghệ sĩ] (bấm vào nghệ sĩ trong lineup)
```

**J2. Xem minh bạch donate công khai** *(2 cơ chế độc lập, không nhầm lẫn)*
```
[Sổ minh bạch Donate công khai] — trang tra cứu, tải theo trang, chỉ donate đã "chốt"
[Ticker Donate công khai] — nhúng trong [Chi tiết Show]/[Phòng xem Livestream], realtime, không cần vé xem live
[Trang cá nhân Nghệ sĩ] → (xem lịch sử donate riêng nghệ sĩ đó) → [Sổ minh bạch Donate công khai] (biến thể theo nghệ sĩ)
```

**J3. Gửi khiếu nại không cần tài khoản**
```
[Gửi khiếu nại] → (submit, chưa đăng nhập, ContactPhone bắt buộc) → [Tra cứu khiếu nại khách vãng lai] (lưu lại id hiện trên màn hình)
[Tra cứu khiếu nại khách vãng lai] → (nhập id + phone, có thể nhiều lần sau) → xem lại kết quả
(thụ động, song song) → Admin resolve → SMS chủ động tới ContactPhone (kênh ngoài UI, không phải 1 view)
```

**J4. Bản đồ điểm chặn 🚧** *(nơi journey Anonymous kết thúc, phải sang Audience)*
```
[Chọn vé & Giữ chỗ] / [Form Donate] / [Đặt món F&B] / [Danh sách đã theo dõi/yêu thích] / [Đánh giá Show]
  → 🚧 (RequireAuthenticated) → [Đăng nhập] / [Đăng ký tài khoản] → quay lại đúng view đang định vào (FE tự giữ context, BE không có "return URL")
[Phòng xem Livestream] → 🚧 (RequireAuthenticated **và** isGenuineTicketHolder — chặt hơn cả đăng nhập suông) → không thấy gì kể cả sau khi đăng nhập nếu chưa có vé
```

---

## Phần 2 — Sơ đồ giao thoa real-time (cross-actor)

> Định dạng mỗi điểm: **Actor A @ [View]** → *(hành động)* → **BE**: endpoint/service xử lý → **Actor B @ [View]**: thay đổi thấy được. Gộp + khử trùng lặp từ 4 bảng "Tổng hợp điểm giao thoa real-time" ở cuối docs/17, 18, 19, 20 — không thêm điểm giao thoa mới ngoài những gì đã ghi nhận ở đó.

**1. Giữ chỗ làm tồn kho tụt ≤10%**
Audience A @ **[Chọn vé & Giữ chỗ]** → *(giữ chỗ thành công)* → BE: `POST /tickets/holds`, kiểm quota nội bộ phát hiện ngưỡng ≤10% → publish `NotificationType.WishlistLowStock` → Audience B (đã wishlist show đó) @ **[Hộp thư thông báo]**: nhận cảnh báo "sắp hết vé" gần như ngay lập tức, dù không thao tác gì.

**2. Thanh toán vé thành công**
Audience @ **[Kết quả thanh toán]** (vé) → *(VNPay IPN xác nhận)* → BE: `GET /payments/vnpay/ipn` → publish `TicketPaymentConfirmed`, tạo lịch `Settlement` → Owner @ **[Thu nhập tổng quan]**: dòng settlement mới xuất hiện (không đẩy realtime tức thì, Owner cần tự vào xem/refresh).

**3. Chat trong lúc xem livestream**
Audience A @ **[Phòng xem Livestream]** → *(gửi tin nhắn)* → BE: SignalR `LivestreamHub` broadcast trong group `loungeShowId` → mọi Audience khác (B, C, D...) @ **[Phòng xem Livestream]** (cùng show): thấy tin nhắn mới ngay lập tức.

**4. Donate thành công (3 bên nhận cùng lúc)**
Audience @ **[Form Donate]** → *(VNPay IPN xác nhận)* → BE: `GET /donations/vnpay-ipn` → `ProcessDonationPaymentCommandHandler` gửi đồng thời 3 hướng:
  - chính Audience đó @ **[Lịch sử Donate của tôi]** / **[Hộp thư thông báo]**: `DonationConfirmed` (riêng tư)
  - Owner @ **[Donate chờ xác nhận]** / **[Hộp thư thông báo]**: `DonationReceived` (chỉ số tiền, ẩn tên nếu donor chọn ẩn danh)
  - mọi người đang xem show đó @ **[Ticker Donate công khai]** (kể cả Anonymous, kể cả không có vé): alert realtime qua `PublicDonationHub`, lọc theo 3 cờ riêng tư donor đã chọn.

**5. Owner acknowledge / confirm-paid 1 donate**
Owner @ **[Donate chờ xác nhận]** hoặc **[Donate chờ trả nghệ sĩ]** → *(acknowledge / confirm-paid)* → BE: `POST /donations/{id}/acknowledge` hoặc `.../confirm-paid` → `PublicDonationBroadcast.PublishAsync` → mọi người xem show đó @ **[Ticker Donate công khai]**: thêm 1 alert cập nhật tiến độ (chặng 2/3, 3/3) — cùng donate, KHÔNG phải donate mới.

**6. Kết nối vào xem livestream**
Audience @ **[Phòng xem Livestream]** → *(kết nối SignalR thành công)* → BE: `LivestreamHub.OnConnectedAsync`, tăng `Livestream.ViewerCount` (đếm nguyên tử tại DB) → Owner/Staff @ **[Vận hành Livestream]**: số viewer thay đổi (đọc lại qua polling/refresh, không tự đẩy).

**7. Owner/Staff bắt đầu phát livestream**
Owner/Staff @ **[Vận hành Livestream]** → *(bắt đầu phát)* → BE: `POST /livestreams/{id}/start` → publish `EventLive` → mọi người đã mua vé + mọi người follow venue đó @ **[Hộp thư thông báo]**: nhận thông báo "Đang phát trực tiếp", dẫn thẳng sang **[Phòng xem Livestream]**.

**8. Owner đổi lịch / đổi format / huỷ show**
Owner @ **[Bảng điều khiển Show (Owner)]** → *(reschedule / format / cancel)* → BE: `POST .../reschedule`, `PUT .../format`, `POST .../cancel` → publish `EventRescheduled`/`EventCancelled`, tự tạo `RefundRequest` nếu áp dụng → mọi Audience đã mua vé show đó @ **[Hộp thư thông báo]** → **[Chi tiết vé]**: trạng thái vé đổi, hoặc → **[Yêu cầu hoàn tiền của tôi]**: có yêu cầu hoàn tiền mới tự sinh.

**9. Owner gán Staff mới**
Owner @ **[Quản lý Nhân viên (Staff)]** → *(gán)* → BE: `POST /lounges/{id}/staff`, đổi `User.Role → Staff` → chính người được gán @ **[Đăng nhập]** (lần kế tiếp): nhận claim `lounge_id`, có quyền vận hành ngay — không cần Owner thông báo thủ công.

**10. Owner/Staff cập nhật trạng thái đơn F&B**
Owner/Staff @ **[Quản lý đơn F&B tại quầy]** → *(đổi trạng thái)* → BE: `PUT /fnb-orders/{id}/status` → Audience đã đặt đơn đó @ **[Đơn F&B của tôi]**: trạng thái đổi — **không có push riêng**, Audience phải tự vào lại/refresh để thấy (khác mọi điểm giao thoa khác đều có kênh chủ động).

**11. Staff bán vé walk-in**
Staff @ **[Bán vé tại quầy (Walk-in)]** → *(bán)* → BE: `POST /tickets/walk-in`, publish `TicketPaymentConfirmed` → Owner @ **[Thu nhập tổng quan]**: thêm dòng settlement (thường không tính hoa hồng, khác vé online).

**12. Staff check-in vé**
Staff @ **[Check-in vé]** → *(quét, xác nhận)* → BE: `POST /tickets/check-in`, `Ticket.Status → Used` → chính khán giả đó @ **[Chi tiết vé]**: QR không dùng lại được, mất khả năng chuyển nhượng/huỷ (điều kiện hiển thị nút đổi ngay).

**13. Admin duyệt/từ chối venue**
Admin @ **[Duyệt Venue mới]** → *(approve/reject)* → BE: `POST /admin/lounges/{id}/approve|reject` → Owner @ **[Danh sách Venue của tôi]**: trạng thái đổi Pending→Approved/Rejected; nếu Approved, nút "Tạo show" ở **[Danh sách Show của tôi]** mở khoá ngay.

**14. Admin duyệt/từ chối show hoặc livestream**
Admin @ **[Kiểm duyệt Show & Livestream]** → *(review)* → BE: `POST /moderations/shows/{id}/review` hoặc `.../livestreams/{id}/review` → Owner @ **[Bảng điều khiển Show (Owner)]** / **[Vận hành Livestream]**: trạng thái đổi ngay; nếu duyệt show → đồng thời mọi Audience/Anonymous @ **[Danh sách/Tìm kiếm Show]**: show xuất hiện công khai ngay lập tức (không có độ trễ cache).

**15. Admin terminate livestream**
Admin @ **[Phòng xem Livestream]** (Admin luôn xem được) → *(terminate, kèm Reason)* → BE: `POST /livestreams/{id}/terminate` → mọi khán giả đang xem @ **[Phòng xem Livestream]**: bị ngắt kết nối SignalR ngay lập tức; đồng thời Owner/Staff @ **[Vận hành Livestream]**: trạng thái đổi `Terminated` (khác `Ended` tự nhiên, có `TerminatedById`+`TerminatedReason`).

**16. Admin duyệt refund request / hoàn donate**
Admin @ **[Yêu cầu hoàn tiền (Refund Requests)]** hoặc **[Hoàn Donate]** → *(process/refund)* → BE: `POST /admin/refund-requests/{id}/process` hoặc `.../donations/{id}/refund` → Audience liên quan @ **[Yêu cầu hoàn tiền của tôi]** hoặc **[Lịch sử Donate của tôi]**: trạng thái đổi + thông báo kết quả.

**17. Admin xử phạt venue**
Admin @ **[Ra quyết định Xử phạt Venue]** → *(tạo phạt)* → BE: `POST /venue-penalties` → Owner @ **[Xử phạt & Kháng cáo của tôi]**: phạt mới xuất hiện, một phần chức năng vận hành có thể bị khoá tuỳ mức (Warning/Suspension/Ban).

**18. Admin xác minh bank account**
Admin @ **[Xác minh Tài khoản ngân hàng]** → *(verify)* → BE: `POST /admin/bank-accounts/{id}/verify` → Owner @ **[Thu nhập tổng quan]**: mọi settlement từng bị chặn trước đó **tự động retry ngay**, không cần Owner thao tác lại.

**19. Admin resolve khiếu nại (TakeDownContent, show)**
Admin @ **[Xử lý Khiếu nại (Admin)]** → *(resolve, TakeDownContent)* → BE: `TakeDownShowAsync` (nội bộ `ResolveComplaintCommandHandler`) → show `Cancelled` + hoàn 100% mọi vé `Confirmed` → mọi Audience đã mua vé show đó @ **[Hộp thư thông báo]** → **[Yêu cầu hoàn tiền của tôi]**: nhận `EventCancelled` + yêu cầu hoàn 100% tự sinh, hàng loạt cùng lúc.

> **Lưu ý về Performer**: Performer không có view/tài khoản riêng (xem [22-performer-presence.md](22-performer-presence.md)) nên không xuất hiện như "Actor B" độc lập trong sơ đồ trên — mọi thay đổi liên quan Performer (nhận donate, được xếp lineup) phản ánh gián tiếp qua view của **Owner** (người quản lý hộ), không phải 1 màn hình riêng của Performer.
> **Lưu ý về khiếu nại khách vãng lai**: Admin resolve khiếu nại của khách vãng lai gửi kết quả qua **SMS** tới `ContactPhone` (xem [21 Journey 3](21-anonymous-journey.md#journey-3--gửi-khiếu-nại-không-cần-tài-khoản)) — đây không phải thay đổi trên 1 "view" trong app (khách vãng lai không có phiên đăng nhập để nhận in-app), nên không liệt kê chung bảng trên dù cũng là 1 dạng giao thoa near-real-time.

---

## Phần 3 — View dùng chung giữa nhiều role (khác biệt hiển thị/quyền)

| View | Role dùng chung | Khác biệt cụ thể |
|---|---|---|
| **Chi tiết Show** | Anonymous, Audience, Owner (chủ show) | Field hiển thị giống nhau; bộ **nút hành động** khác hẳn: Anonymous chỉ xem; Audience có vé + show `Ongoing` → thêm nút "Vào xem live"; Audience có vé + show `Ended` trong hạn 7 ngày, chưa đánh giá → thêm nút "Đánh giá"; Owner sở hữu show → thêm link sang **Bảng điều khiển Show (Owner)**. ⚠️ docs/16 đánh dấu `Optional Auth` có thể có thêm field riêng cho chủ sở hữu nhưng **chưa xác minh field cụ thể** — đừng giả định trước khi hỏi lại BE. |
| **Danh sách/Tìm kiếm Show** | Anonymous, Audience, Owner | Mặc định (không `mine=true`, hoặc chưa đăng nhập): chỉ thấy `Published`. Owner đăng nhập + `mine=true`: thấy thêm `Draft`/`Pending` của chính mình. Tham số `mine=true` **ném lỗi 401** (`UnauthorizedException`) nếu gọi không có token — xác nhận trực tiếp `GetPublishedLoungeShowsQueryHandler` 2026-08-17, sửa lại claim cũ sai ("im lặng bị bỏ qua") — UI không nên hiện toggle "Show của tôi" khi chưa đăng nhập Owner, nếu không sẽ ra lỗi khi bấm. |
| **Danh sách Venue** | Anonymous, Owner | Cùng logic `mine=true` như trên — Owner thấy cả venue `Pending`/`Rejected` của mình, công khai chỉ thấy venue đã duyệt. |
| **Chi tiết Venue** | Anonymous, Owner | ⚠️ Đánh dấu `Optional Auth`, docs/16 ghi "khả năng Owner xem thêm field quản trị" nhưng **chưa xác minh field cụ thể nào khác nhau** — cần xác nhận lại với BE trước khi thiết kế 2 layout khác nhau cho cùng view này. |
| **Hộp thư thông báo** | Mọi actor đã đăng nhập (Audience/Owner/Staff/Admin) | Field/layout giống nhau; khác ở **loại thông báo nhận được** — Admin nhận thêm nhóm cảnh báo vận hành/bảo mật nội bộ (`SettlementSchedulingBlocked`, cảnh báo từ 4 job giám sát) mà Audience/Owner/Staff không bao giờ thấy. |
| **Phòng xem Livestream** | Audience (chủ vé thật), Owner/Staff (vận hành venue đó), Admin (luôn xem) | Khác nhau ở **điều kiện vào được**, không phải field: Audience cần `isGenuineTicketHolder` + trong giới hạn thiết bị đồng thời; Owner/Staff cần đúng venue mình vận hành (`RequireVenueOperator`); Admin luôn qua được (`LivestreamAccessPolicy`). Riêng Admin thấy thêm nút "Buộc dừng phát" (`terminate`) mà 3 actor kia không có. |
| **Bảng điều khiển Show (Owner)** | Owner (đầy đủ), Staff (1 phần) | Owner thấy đầy đủ mọi nút (nộp duyệt/đổi lịch/đổi format/huỷ/start/end). Staff **chỉ** dùng được nút bắt đầu/kết thúc show (`RequireVenueOperator`) — các nút còn lại (`RequireOwner`) nên **ẩn hẳn**, không chỉ disable, với tài khoản Staff. |
| **Vận hành Livestream** | Owner, Staff (cùng quyền `RequireVenueOperator`) | Không khác biệt hiển thị giữa Owner/Staff ở view này — cả 2 làm được mọi hành động (tạo/credentials/start/end). Khác với **Bảng điều khiển Show (Owner)** ở trên, nơi Staff bị giới hạn nhiều hơn. |
| **Quản lý Hồ sơ Nghệ sĩ** | Mọi Owner (CREATE/READ/ASSIGN), người tạo + Admin (EDIT/DELETE) | Mọi Owner thấy toàn bộ catalog và gán được nghệ sĩ bất kỳ vào show mình — nhưng nút "Sửa"/"Xoá" chỉ hiện với đúng `CreatedByUserId` hoặc Admin (kiểm tra ở tầng handler, không phải policy — UI phải tự so `CreatedByUserId` với user hiện tại để ẩn nút, không dựa vào response 403 mới ẩn). |
| **Bảng giá gói Subscription** | Anonymous, Audience, Owner | Field/nội dung giống hệt nhau (công khai hoàn toàn, không cần login để xem giá) — chỉ khác ở nút "Chọn gói": với Anonymous/Audience dẫn sang Đăng ký/Đăng nhập trước, với Owner dẫn thẳng vào khởi tạo thanh toán ở **Gói Subscription của tôi**. |

---

*Xem thêm: [23-view-catalog.md](23-view-catalog.md) (chi tiết đầy đủ từng view), [17](17-audience-journey.md)-[22](22-performer-presence.md) (nguồn journey/business rule gốc).*
