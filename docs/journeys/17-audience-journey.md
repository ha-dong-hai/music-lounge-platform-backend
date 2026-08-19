# 17 — Journey của Audience (Khán giả)

← [12-actors-and-authorization.md](12-actors-and-authorization.md) · [14-usecase-traces.md](14-usecase-traces.md) · [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md) · → [23-view-catalog.md](23-view-catalog.md) (view/màn hình suy ra từ journey này)

> **Actor**: `Audience` (`UserRole.Audience`) — chọn actor này vì đây là actor duy nhất được dùng làm ví dụ minh hoạ trong chính yêu cầu ("khán giả... mua vé xem show... donate... xem lại lịch sử vé"). Nếu cần journey cho Owner/Staff/Admin, làm tiếp theo cùng khuôn mẫu này.
>
> **Ký hiệu**: `[NHẬP]` = actor phải nhập dữ liệu · `[XEM]` = chỉ đọc, không thay đổi gì · `[BẤM]` = trigger 1 hành động nghiệp vụ (không nhập field mới, chỉ xác nhận). 🔀 = điểm rẽ nhánh trong journey. ↔ = hành động của Audience ảnh hưởng trực tiếp tới trải nghiệm real-time của actor khác đang dùng hệ thống cùng lúc.
>
> **Cập nhật**: 2026-08-13, dựng từ [14-usecase-traces.md](14-usecase-traces.md) (đã đọc lại trực tiếp trong lượt này) + [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md).

---

## Mục lục journey

1. Đăng ký & thiết lập tài khoản
2. Khám phá & theo dõi show/venue
3. Mua vé xem show
4. Xem livestream & donate cho nghệ sĩ khi đang xem
5. Đặt đồ ăn/thức uống (F&B) tại venue
6. Quản lý vé đã mua (lịch sử, chuyển nhượng, huỷ & theo dõi hoàn tiền)
7. Đánh giá show sau khi kết thúc
8. Gửi khiếu nại
9. Quản lý thông báo & hồ sơ cá nhân

---

## Journey 1 — Đăng ký & thiết lập tài khoản

Điểm vào bắt buộc trước mọi journey khác cần đăng nhập (2–9 đều yêu cầu, trừ phần duyệt-xem công khai ở Journey 2).

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Điền form đăng ký | `POST /auth/register` | `[NHẬP]` Email, Password, FullName, Phone?, AcceptTerms, Role="Audience" | 🔀 Chỉ tạo tài khoản "chưa xác thực", **chưa cấp token** — không đăng nhập được ngay |
| 2 | Nhập mã OTP gửi về email | `POST /auth/verify-email` | `[NHẬP]` Email, Code | Thành công → nhận token đăng nhập lần đầu. 🔀 Sai/hết hạn mã → `POST /auth/resend-verification-code` gửi lại |
| 3a | **Nhánh thường**: đăng nhập bằng mật khẩu | `POST /auth/login` | `[NHẬP]` Email, Password | |
| 3b | **Nhánh thay thế**: đăng nhập bằng Google | `POST /auth/google` | `[NHẬP]` IdToken (từ Google SDK), AcceptTerms | AcceptTerms chỉ bắt buộc lần đầu (tài khoản Google mới) |
| 4 | Quên mật khẩu (nếu cần) | `POST /auth/forgot-password` → `POST /auth/reset-password` | `[NHẬP]` Email; sau đó Token+NewPassword | Luôn trả 204 ở bước forgot dù email có tồn tại hay không (tránh lộ thông tin) |
| 5 | Xác thực số điện thoại (tự chọn) | `POST /me/phone/verification-code` → `POST /me/phone/verify` | `[BẤM]` gửi OTP; `[NHẬP]` Code | NĐ 147/2024 |
| 6 | Nộp KYC CCCD/CMND (tự chọn) | `POST /uploads/citizen-card-images` (×2, mặt trước/sau) → `POST /me/citizen-card` | `[NHẬP]` ảnh + CitizenCardNumber, FrontImageUrl, BackImageUrl | Ảnh lưu kho riêng tư, không qua URL công khai |
| 7 | Thiết lập sở thích AI gợi ý (tự chọn) | `PUT /me/preferences` | `[NHẬP]` GenreIds[], MoodIds[], AtmosphereIds[], EnableAiConsent | Tắt `EnableAiConsent` → Journey 2 chỉ còn gợi ý Trending, không cá nhân hoá (xem J2 bước 3) |

---

## Journey 2 — Khám phá & theo dõi show/venue

Không bắt buộc đăng nhập cho việc duyệt/tìm kiếm — chỉ bắt buộc từ bước tương tác (follow/wishlist).

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem danh sách show đang mở, hoặc tìm kiếm theo từ khoá/bộ lọc | `GET /lounge-shows`, `GET /lounge-shows/search`, `GET /lounge-shows/trending`, `GET /lounge-shows/filter-options` | `[XEM]` | Không cần đăng nhập |
| 2 | Xem chi tiết 1 show / 1 venue | `GET /lounge-shows/{id}`, `GET /lounges/{id}`, `GET /lounges/{id}/tour` (nếu venue có tour 360°) | `[XEM]` | Endpoint dùng chung — cùng 1 trang chi tiết show sẽ dẫn tiếp sang Journey 3 (mua vé) hoặc Journey 4 (xem live) tuỳ show đang ở trạng thái nào |
| 3 | Xem gợi ý cá nhân hoá | `GET /recommendations` | `[XEM]` | 🔀 `AiConsent=false` (J1 bước 7) → chỉ nhận Trending, không cá nhân hoá; đủ dữ liệu hành vi (≥5 dòng log) → chuyển sang tầng Hybrid tự động, không cần actor làm gì thêm |
| 4 | Theo dõi 1 venue để nhận tin show mới | `POST /follows/lounges/{loungeId}` | `[BẤM]` | Follow được cả venue đang `Pending`/`Suspended` — không gate theo trạng thái venue |
| 5 | Thêm 1 show vào wishlist | `POST /wishlist/{showId}` | `[BẤM]` | 🔀 Show `Draft`/`Cancelled` → bị chặn, không wishlist được |
| 6 | Xem lại danh sách đã follow/wishlist | `GET /follows/lounges`, `GET /wishlist` | `[XEM]` | |
| 7 | (Về sau, thụ động) Venue đã follow ra show mới, hoặc show đã wishlist sắp hết vé | — (nhận qua Journey 9) | — | Trigger `NotificationType.NewEvent` / `WishlistLowStock` — xem Journey 9 |

---

## Journey 3 — Mua vé xem show

Journey lõi nhất, có nhiều điểm khoá tranh chấp (concurrency) vì nhiều Audience có thể giành cùng 1 suất vé cùng lúc.

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem hạng vé + giá của show đã chọn (từ J2) | `GET /ticket-tiers?showId=`, `GET /lounge-shows/{id}/seating-map` | `[XEM]` | |
| 2 | Chọn hạng vé + số lượng, giữ chỗ | `POST /tickets/holds` | `[NHẬP]` PriceId, Quantity → `[BẤM]` | Giữ 15 phút (config). 🔀 Hết vé ở 1 trong 5 lớp quota (đợt giá/tier/zone/show/gói subscription Owner) → 400, dừng ngay tại đây, không tạo hold |
| 3 | ↔ *(hệ quả phụ)* nếu tồn kho tụt xuống ≤10% quota | — | — | ↔ Trigger `NotificationType.WishlistLowStock` gửi cho **mọi Audience khác** đã wishlist show này — Audience A giữ chỗ có thể khiến Audience B nhận cảnh báo "sắp hết vé" gần như ngay lập tức |
| 4a | 🔀 **Đổi ý, không mua** | `DELETE /tickets/holds/{holdId}` | `[BẤM]` | Xoá hẳn hold, nhả chỗ lại cho người khác |
| 4b | 🔀 **Tiếp tục mua** — khởi tạo thanh toán | `POST /tickets/purchase` | `[NHẬP]` HoldId → `[BẤM]` | Tạo `Payment` + N `Ticket` ở trạng thái `Pending`, trả về `paymentUrl` |
| 5 | Chuyển sang trang VNPay, nhập thông tin thẻ/OTP ngân hàng | (ngoài hệ thống — VNPay) | `[NHẬP]` (trên trang VNPay, không phải form của MusicLounge) | |
| 6a | 🔀 **Thanh toán thành công** | `GET /payments/vnpay/callback` (redirect trình duyệt) + `GET /payments/vnpay/ipn` (server-to-server, nguồn sự thật) | tự động | Vé → `Confirmed`, sinh `QrCode`. ↔ Đồng thời publish `TicketPaymentConfirmed`: ghi sổ cái, tạo lịch `Settlement` cho Owner (Owner sẽ thấy tiền "đang chờ giải ngân" trong dashboard riêng), gửi push "vé đã xác nhận" cho chính Audience này |
| 6b | 🔀 **Thanh toán thất bại/huỷ giữa chừng** | (như trên) | tự động | `Payment→Failed`, mọi Ticket liên quan → `Cancelled` — Audience quay lại bước 1, giữ chỗ mới nếu còn |
| 7 | Xem vé vừa mua + ảnh QR | `GET /tickets/my`, `GET /tickets/{id}`, `GET /tickets/{id}/qr` | `[XEM]` | Tiếp nối sang Journey 6 |

---

## Journey 4 — Xem livestream & donate cho nghệ sĩ khi đang xem

Đây là journey có **nhiều điểm giao thoa real-time với actor khác nhất** trong toàn hệ thống.

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Vào trang chi tiết show đã có vé, show đang `Ongoing` | `GET /lounge-shows/{id}` | `[XEM]` | |
| 2 | Kết nối vào phòng xem trực tiếp | SignalR `LivestreamHub.OnConnectedAsync` | `[BẤM]` (kết nối) | 🔀 Không phải chủ vé thật, hoặc vượt giới hạn thiết bị xem đồng thời (mặc định 2/vé) → bị `Context.Abort()`, không vào được. ↔ Kết nối thành công → `Livestream.ViewerCount` tăng ngay (đếm nguyên tử tại DB) — Owner/Staff xem dashoard vận hành thấy số viewer thay đổi tức thì |
| 3 | Xem video (Mux HLS) + đọc chat | (video ngoài .NET backend, qua Mux) + `GET /livestreams/{id}/chat` | `[XEM]` | |
| 4 | Gửi tin nhắn chat | SignalR (gửi `Message`) | `[NHẬP]` | ↔ Broadcast realtime cho **toàn bộ nhóm đang xem cùng show** — mọi khán giả khác thấy tin ngay lập tức |
| 5 | Chọn nghệ sĩ đang biểu diễn, bấm donate | — (mở form donate) | `[XEM]` (chọn Performance) | Chỉ hiện nút donate nếu `Performance.AcceptsDonation=true` cho đúng lượt diễn |
| 6 | Nhập số tiền + tuỳ chọn ẩn danh/ẩn số tiền/ẩn lời nhắn | `POST /donations` | `[NHẬP]` PerformanceId, Amount, IsAnonymous, Message, IsMessagePublic → `[BẤM]` | 🔀 Show không còn `Ongoing` → chặn ngay, không tạo donate được |
| 7 | Chuyển sang VNPay, nhập thẻ/OTP | (ngoài hệ thống) | `[NHẬP]` (trên VNPay) | |
| 8a | 🔀 **Thanh toán donate thành công** | `GET /donations/vnpay-return` + `GET /donations/vnpay-ipn` | tự động | Donate → `PendingOwnerAck`. ↔ **3 hệ quả real-time cùng lúc**: (1) Audience này nhận thông báo riêng tư "Donate của bạn đã thành công" (`DonationConfirmed`); (2) Owner nhận thông báo "Bạn vừa nhận donate!" (`DonationReceived`) — chỉ số tiền, không lộ tên nếu ẩn danh; (3) `PublicDonationBroadcast` bắn alert qua `PublicDonationHub` tới **mọi người đang xem show này** (kể cả người không có vé, không đăng nhập) — hiện ngay trên overlay kiểu Streamlabs, tên/tin nhắn/số tiền đã lọc theo đúng 3 cờ riêng tư Audience vừa chọn ở bước 6 |
| 8b | 🔀 **Thanh toán thất bại** | (như trên) | tự động | Donate → `Cancelled`, không ai khác thấy gì (chưa từng public) |
| 9 | Xem lại donate của mình | `GET /donations/my` | `[XEM]` | Thấy trạng thái tiến triển: `PendingOwnerAck → OwnerReceived → PerformerPaid` theo tốc độ Owner xử lý (ngoài tầm kiểm soát của Audience) |
| 10 | *(thụ động, về sau)* Owner xác nhận đã nhận / đã trả nghệ sĩ | — | — | ↔ Mỗi lần đổi trạng thái, `PublicDonationBroadcast` lại bắn thêm 1 alert công khai (chặng 2/3, chặng 3/3) — Audience không cần làm gì, chỉ là người quan sát ở bước này |

---

## Journey 5 — Đặt đồ ăn/thức uống (F&B) tại venue

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem menu của venue | `GET /fnb-menus?loungeId=`, `GET /fnb-menu-items?menuId=` | `[XEM]` | Không cần đăng nhập để xem |
| 2 | Chọn món, đặt đơn | `POST /fnb-orders` | `[NHẬP]` LoungeId, ShowId?, ZoneId?, TableNote, PaymentMethod, Note, Items[] → `[BẤM]` | 🔀 Món hết hàng (`IsAvailable=false`) hoặc zone/show không thuộc đúng venue → chặn |
| 3 | Theo dõi trạng thái đơn | `GET /fnb-orders/my`, `GET /fnb-orders/{id}` | `[XEM]` | ↔ Trạng thái do **Staff** cập nhật (`Pending→Preparing→Served→Paid`) — Audience chỉ xem, không tự đổi trạng thái được |
| 4 | *(thay thế)* Đặt hộ tại quầy | — | — | Staff/Owner đặt hộ qua cùng endpoint (`Role=Staff`), Audience không thao tác gì — chỉ nhận món |

---

## Journey 6 — Quản lý vé đã mua (lịch sử, chuyển nhượng, huỷ & theo dõi hoàn tiền)

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Xem toàn bộ lịch sử vé đã mua | `GET /tickets/my` | `[XEM]` | Mọi trạng thái: Pending/Confirmed/Used/Cancelled/Refunded |
| 2 | Xem chi tiết 1 vé + ảnh QR | `GET /tickets/{id}`, `GET /tickets/{id}/qr` | `[XEM]` | Dùng khi vào cửa (Staff quét) |
| 3a | 🔀 **Chuyển nhượng vé cho người khác** — khởi tạo | `POST /tickets/{id}/transfer` | `[NHẬP]` RecipientEmail → `[BẤM]` | Chỉ khi: vé `Confirmed`, chưa check-in, **chưa từng xem livestream** (`FirstAccessedAt is null`), show chưa `Ended`/`Cancelled` |
| 3b | ↔ Người nhận xác nhận | `POST /tickets/{id}/transfer/accept` (do người nhận gọi) | — | ↔ Vé đổi chủ, sinh `QrCode` + `AccessToken` mới (mã cũ coi như đã lộ). Chủ cũ (Audience đang xem journey này) nhận thông báo xác nhận |
| 3c | 🔀 **Tự huỷ yêu cầu chuyển nhượng** | `POST /tickets/{id}/transfer/cancel` | `[BẤM]` | |
| 4 | 🔀 **Huỷ vé** — nhánh theo trạng thái vé | `POST /tickets/{id}/cancel` | `[BẤM]` | **Vé `Pending`** (chưa thanh toán thật): huỷ ngay lập tức, không điều kiện. **Vé `Confirmed`**: phải còn trong hạn (`ScheduledStart − CancellationDeadlineHours`), show cho phép huỷ (`CancellationAllowed=true`), chưa check-in, không đang chuyển nhượng — mới tạo được `RefundRequest` |
| 5 | Theo dõi yêu cầu hoàn tiền | `GET /tickets/refund-requests/my` | `[XEM]` | Trạng thái `Pending` cho tới khi Admin xử lý |
| 6 | *(thụ động)* Admin duyệt/từ chối hoàn tiền | — | — | ↔ Nằm ngoài tầm kiểm soát Audience — chỉ nhận thông báo kết quả. Tỉ lệ hoàn theo `show.RefundPercentage` (mặc định 100% nếu Owner không cấu hình) cho vé tự huỷ; **luôn 100%** nếu do Owner huỷ show/Admin gỡ show vi phạm — 2 mức khác nhau tuỳ nguyên nhân huỷ, không do Audience chọn |

---

## Journey 7 — Đánh giá show sau khi kết thúc

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Show chuyển sang `Ended` | — (thụ động) | — | Nhận thông báo hoặc tự vào lại trang show |
| 2 | Chấm điểm + viết nhận xét | `POST /lounge-shows/{id}/rate` | `[NHẬP]` Score, Comment → `[BẤM]` | 🔀 Điều kiện: phải có vé `Confirmed`/`Used` cho đúng show; trong cửa sổ `RatingOpenUntil` (mặc định 7 ngày sau `ActualEnd`); mỗi user chỉ đánh giá 1 lần/show — quá hạn hoặc đã đánh giá rồi → 409/chặn |
| 3 | ↔ *(hệ quả gián tiếp)* | — | — | Rating được `ScheduleSettlementHandler` đọc để tính tốc độ giải ngân tiền vé cho Owner (venue uy tín cao → giải ngân nhanh hơn) — Audience không thấy trực tiếp nhưng ảnh hưởng tới trải nghiệm tài chính của Owner |

---

## Journey 8 — Gửi khiếu nại

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Điền khiếu nại (vé không đúng mô tả, donate chưa được xử lý, thái độ venue...) | `POST /complaints` | `[NHẬP]` TargetType, TargetId/TargetGuid, Category, ... → `[BẤM]` | Gọi được cả khi **chưa đăng nhập** (khách vãng lai) — nhưng chỉ Audience đã đăng nhập mới tra lại được kết quả ở bước 2 |
| 2 | Theo dõi khiếu nại của mình | `GET /complaints/my`, `GET /complaints/{id}` | `[XEM]` | |
| 3 | *(thụ động)* Admin xử lý | — | — | ↔ Nếu Admin chọn "hoàn tiền"/"gỡ nội dung", tự động tạo `RefundRequest` — nối tiếp sang Journey 6 bước 5 |

---

## Journey 9 — Quản lý thông báo & hồ sơ cá nhân

Chạy song song, xuyên suốt mọi journey khác — không phải luồng tuyến tính riêng.

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Đăng ký thiết bị nhận push (ngay sau đăng nhập) | `POST /notifications/device-tokens` | `[NHẬP]` Fid (từ Firebase SDK) → `[BẤM]` | Cùng 1 `Fid` từng đăng ký dưới tài khoản khác sẽ tự "re-point" sang tài khoản hiện tại |
| 2 | Xem hộp thư thông báo | `GET /notifications` | `[XEM]` | Tập hợp mọi sự kiện từ Journey 3/4/6/7/8 (vé xác nhận, donate thành công, sắp hết vé, kết quả refund/khiếu nại...) |
| 3 | Đánh dấu đã đọc | `POST /notifications/{id}/read`, `POST /notifications/read-all` | `[BẤM]` | |
| 4 | Huỷ đăng ký thiết bị (khi đăng xuất) | `DELETE /notifications/device-tokens` | `[BẤM]` | Tránh máy dùng chung tiếp tục nhận push cho tài khoản đã đăng xuất |
| 5 | Xem/sửa hồ sơ cá nhân | `GET /me`, `PUT /me/profile`, `PUT /me/password` | `[XEM]`/`[NHẬP]` | |
| 6 | Xuất toàn bộ dữ liệu cá nhân (DSAR) | `GET /me/data-export` | `[BẤM]` | Đồng bộ ngay lập tức, đáp ứng nghĩa vụ ACK 2 ngày theo Luật 91/2025/QH15 |
| 7 | Xoá tài khoản | `DELETE /me` (khoá tạm) hoặc `POST /me/data-erasure` (xoá vĩnh viễn) | `[BẤM]` (+ `[NHẬP]` CurrentPassword nếu xoá vĩnh viễn & tài khoản mật khẩu) | 🔀 2 mức khác nhau: khoá tạm còn khôi phục được; xoá vĩnh viễn là ẩn danh hoá tại chỗ, không hard-delete (giữ lại lịch sử vé/donate/thanh toán theo Luật Kế toán 10 năm nhưng không còn gắn danh tính thật) |

---

## Tổng hợp điểm giao thoa real-time (đáng chú ý nhất khi thiết kế view)

| Hành động của Audience | Actor khác bị ảnh hưởng ngay lập tức | Kênh |
|---|---|---|
| Giữ chỗ khiến tồn kho ≤10% | Audience khác đã wishlist show đó | Push/in-app `WishlistLowStock` |
| Thanh toán vé thành công | Owner (lịch settlement mới xuất hiện trong dashboard) | Ghi DB, không realtime tức thì |
| Gửi tin nhắn chat trong livestream | Mọi khán giả khác đang xem cùng show | SignalR broadcast trong group |
| Donate thành công | Owner (thông báo riêng); **mọi người đang xem show** kể cả chưa đăng nhập (overlay công khai) | Push/in-app (Owner) + SignalR `PublicDonationHub` (công khai) |
| Kết nối vào xem livestream | Owner/Staff (ViewerCount tăng trên dashboard vận hành) | Cập nhật DB nguyên tử, đọc lại qua polling/refresh |
| Khiếu nại được Admin xử lý "gỡ nội dung" | Mọi Audience khác đã mua vé show đó (nhận `EventCancelled` + hoàn 100%) | Push/in-app hàng loạt |
