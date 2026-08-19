# 19 — Journey của Staff (Nhân viên vận hành)

← [12-actors-and-authorization.md](12-actors-and-authorization.md) · [14-usecase-traces.md](14-usecase-traces.md) · [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md) · [18-owner-journey.md](18-owner-journey.md) · → [23-view-catalog.md](23-view-catalog.md)

> **Actor**: `Staff` (`UserRole.Staff`, gắn theo claim `lounge_id` trong JWT — **chỉ vận hành đúng 1 venue tại 1 thời điểm**, khác Owner có thể sở hữu nhiều venue). Actor thứ 3 trong chuỗi chạy lần lượt.
>
> **Phạm vi quyền**: mọi hành động nghiệp vụ của Staff đi qua đúng 1 policy — `RequireVenueOperator` (`VenueOperatorAccess.CanOperate`, so `lounge_id` trong JWT với venue đang thao tác). Staff **không** có quyền tạo/sửa venue, show, hạng vé, subscription, bank account — những việc đó thuộc Owner ([18](18-owner-journey.md)). Ngoài phần vận hành, Staff vẫn giữ mọi quyền tài khoản chung của người dùng đã đăng nhập (Journey 9 ở [17](17-audience-journey.md): thông báo, hồ sơ cá nhân...) — không lặp lại ở đây.
>
> **Ký hiệu**: giống [17](17-audience-journey.md)/[18](18-owner-journey.md).
>
> **Cập nhật**: 2026-08-13.

---

## Mục lục journey

1. Được gán vào venue & đăng nhập lần đầu
2. Bán vé tại quầy (Walk-in)
3. Check-in khán giả tại cửa
4. Vận hành livestream (tạo / bắt đầu / kết thúc)
5. Vận hành show Offline (bắt đầu / kết thúc)
6. Xử lý đơn F&B

---

## Journey 1 — Được gán vào venue & đăng nhập lần đầu

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | ↔ *(thụ động)* Owner tra email và gán làm Staff | — | — | Owner gọi `GET /lounges/staff/lookup` rồi `POST /lounges/{id}/staff` (xem [18 J2 bước 5](18-owner-journey.md)) — Staff không tự đăng ký được vai trò này, phải **đã có tài khoản Audience trước** rồi được Owner nâng cấp |
| 2 | Đăng nhập | `POST /auth/login` (hoặc `/auth/google`) | `[NHẬP]` | Token JWT phát ra kèm claim `lounge_id` — xác định venue mình được vận hành |
| 3 | 🔀 *(ngoại lệ)* Bị gỡ khỏi venue | — | — | Owner `DELETE /lounges/{id}/staff/{staffId}` → `LoungeStaff.IsActive=false` — Staff vẫn giữ `Role=Staff` trong hệ thống nhưng **mất quyền vận hành** venue đó ngay từ lần gọi API kế tiếp (token cũ hết hiệu lực về mặt nghiệp vụ dù chưa hết hạn) |

---

## Journey 2 — Bán vé tại quầy (Walk-in)

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Khách đến quầy hỏi mua vé trực tiếp (không qua app) | — | — | |
| 2 | Chọn hạng vé + số lượng, bán ngay | `POST /tickets/walk-in` | `[NHẬP]` PriceId, Quantity → `[BẤM]` | 🔀 Chỉ bán được vé `AccessType=Physical` (không bán walk-in cho vé online); đợt giá phải cho phép kênh Offline. Cùng 4 lớp kiểm tra quota như Audience tự giữ chỗ (đợt giá/tier/zone/show), nhưng gộp Hold+Purchase làm 1 bước |
| 3 | Vé xác nhận + QR sinh ngay lập tức | (kết quả bước 2) | `[XEM]` | Khác hẳn luồng Audience tự mua: **không qua VNPay**, `Payment.Status=Confirmed` ngay, `Method=Cash`, `BuyerId=null` — vé này không gắn với `User` nào trong hệ thống |
| 4 | ↔ *(hệ quả phụ)* | — | — | Publish `TicketPaymentConfirmed` như vé online — vẫn lên lịch `Settlement` cho Owner, nhưng **bỏ qua ghi sổ cái hoa hồng** nếu venue không bật `WalkInCommissionEnabled` (mặc định tắt — bán tiền mặt tại quầy không thu hoa hồng nền tảng) |

---

## Journey 3 — Check-in khán giả tại cửa

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Khán giả đưa vé lên (QR trên điện thoại hoặc vé giấy) | — | — | |
| 2 | *(tự chọn)* Quét xem trước thông tin vé | `GET /tickets/by-qr/{qrCode}` | `[XEM]` | Tách riêng khỏi bước check-in thật — tránh check-in nhầm ngay khi vừa quét |
| 3 | Xác nhận check-in | `POST /tickets/check-in` | `[NHẬP]` QrCode → `[BẤM]` | 🔀 Chặn nếu: show chưa/đã qua giờ diễn (không `Ongoing`), vé không phải `AccessType=Physical` (vé online không cần check-in cửa), vé chưa `Confirmed`, **đã check-in trước đó rồi** (409 — chống quét trùng), hoặc vé đang trong quá trình chuyển nhượng ("đóng băng") |
| 4 | Không có mạng lúc quét | — | — | 🔀 **Không có cơ chế offline fallback** — rủi ro đã biết và được chấp nhận trong phạm vi capstone, không phải thiếu sót |

---

## Journey 4 — Vận hành livestream (tạo / bắt đầu / kết thúc)

Staff có cùng quyền với Owner ở đúng 3 hành động vận hành livestream (`RequireVenueOperator`, không phải `RequireOwner`) — xem chi tiết đầy đủ ở [18 J5 Nhánh B](18-owner-journey.md#nhánh-b--show-onlinehybrid-có-livestream), chỉ tóm tắt phần Staff trực tiếp thao tác:

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Tạo bản ghi Livestream cho show đã có | `POST /livestreams` | `[NHẬP]` ShowId → `[BẤM]` | Tự kéo theo 1 vòng kiểm duyệt Admin bắt buộc (`EventModeration`) — Staff không tự quyết được, phải chờ |
| 2 | Lấy RTMP URL + Stream Key, cắm vào OBS | `GET /livestreams/{id}/credentials` | `[XEM]` | Không lộ ra khán giả |
| 3 | Bắt đầu phát | `POST /livestreams/{id}/start` | `[BẤM]` | 🔀 Chặn nếu chưa được Admin duyệt hoặc chưa khai báo `VcpmcRoyaltyReference` (Owner khai báo ở [18 J4](18-owner-journey.md), Staff không tự khai được vì đó là field trên `LoungeShow`, chỉ `RequireOwner` mới `PUT` được) — nghĩa là Staff có thể bị chặn ở bước này vì 1 việc Owner quên làm trước đó |
| 4 | ↔ Kết thúc phát | `POST /livestreams/{id}/end` | `[BẤM]` | Đồng thời đóng `LoungeShow→Ended`, mở cửa sổ đánh giá cho khán giả |

---

## Journey 5 — Vận hành show Offline (bắt đầu / kết thúc)

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Tới giờ diễn, bấm bắt đầu | `POST /lounge-shows/{id}/start` | `[BẤM]` | `LoungeShow.Status→Ongoing`, `ActualStart=now` — mốc thời gian thật này sau đó quyết định show có bị đưa vào diện `PendingReview` giải ngân hay không (nếu kết thúc quá sớm so với dự kiến) |
| 2 | Show diễn ra xong, bấm kết thúc | `POST /lounge-shows/{id}/end` | `[BẤM]` | 🔀 Endpoint này **tự chặn** nếu show đó có Livestream đi kèm — bắt buộc dùng cặp lệnh Livestream Start/End ở Journey 4 thay vì lệnh này, đảm bảo `ActualStart`/`ActualEnd` chỉ được ghi qua đúng 1 con đường |

---

## Journey 6 — Xử lý đơn F&B

| # | Bước | Endpoint | I/O/Trigger | Ghi chú |
|---|---|---|---|---|
| 1 | Khách gọi món trực tiếp tại bàn/quầy (không tự đặt qua app) | `POST /fnb-orders` (Role=Staff) | `[NHẬP]` LoungeId, ShowId?, ZoneId?, TableNote, PaymentMethod, Items[] → `[BẤM]` | Cùng endpoint Audience tự đặt dùng — khác ở việc ghi `StaffId` thay vì `AudienceUserId` |
| 2 | Xem danh sách đơn đang chờ xử lý | `GET /fnb-orders?loungeId=` | `[XEM]` | |
| 3 | Cập nhật tiến độ chế biến/phục vụ | `PUT /fnb-orders/{id}/status` | `[BẤM]` | 🔀 Chuỗi bắt buộc `Pending→Preparing→Served→Paid`, không nhảy cóc/lùi. `Cancelled` là lối thoát riêng, dùng được ở bất kỳ bước nào trước `Paid` |
| 4 | Đánh dấu đã thu tiền | `PUT /fnb-orders/{id}/status` (Status=Paid) | `[BẤM]` | Tự tạo 1 `Payment` ghi nhận nội bộ để đối soát — **không** kiểm tra Staff có thực thu tiền hay không, chỉ là 1 field trạng thái (rủi ro gian lận nội bộ đã ghi nhận ở [14 §9.2](14-usecase-traces.md#92-cập-nhật-trạng-thái-đơn), không phải việc Staff cần lo, mà là điểm Admin/Owner cần biết khi đối soát) |

---

## Tổng hợp điểm giao thoa real-time (Staff ↔ actor khác)

| Hành động của Staff | Actor khác bị ảnh hưởng ngay lập tức | Kênh |
|---|---|---|
| Bán vé walk-in | Owner (thêm 1 dòng vào lịch settlement, thường không tính hoa hồng) | Ghi DB |
| Check-in vé tại cửa | Chính khán giả đó (vé chuyển `Used`, không dùng lại được để chuyển nhượng/hoàn) | Cập nhật DB |
| Bắt đầu phát livestream | Mọi người đã mua vé + follow venue (như [18 J5](18-owner-journey.md)) | Push/in-app `EventLive` |
| Cập nhật trạng thái đơn F&B | Khách đã đặt đơn đó | Đọc lại qua `GET /fnb-orders/my`, chưa có push riêng |
