# 14 — Use-Case Trace: Controller → Handler → Repository → Entity

← [11-ba-domain-analysis.md](11-ba-domain-analysis.md) · [13-data-model.md](13-data-model.md)

> **Phạm vi**: truy vết chi tiết mức use-case (không phải mức domain tổng quan như doc 11) cho **toàn bộ 10 domain nghiệp vụ** đã liệt kê ở [11-ba-domain-analysis.md](11-ba-domain-analysis.md) — hoàn tất. Đi từ endpoint → Command/Query Handler (MediatR, không có Service layer riêng — xem [05-architecture.md](05-architecture.md)) → Repository → Entity. §1-5 làm trước theo yêu cầu ưu tiên nhóm tiền/thời gian thực (Ticket, Livestream, Donate, Hủy/Hoàn tiền, Settlement); §6-12 bổ sung sau để phủ hết domain còn lại (Auth, Venue, Show/Event phần tạo/sửa, F&B, Recommendation/Analytics, Notification, Follow/Wishlist). Toàn bộ 12 câu hỏi "Chưa rõ" ban đầu ở doc 11 đã được trả lời trong quá trình truy vết này.
> **Phương pháp**: đọc trực tiếp handler code, không suy đoán. Business rule trích dòng/comment cụ thể.
> **Cập nhật lần cuối**: 2026-08-13, dựa trên working tree hiện tại.

## Mục lục

1. [Bán vé](#1-bán-vé)
2. [Livestream](#2-livestream)
3. [Donate / Tip](#3-donate--tip)
4. [Hủy show & Hoàn tiền](#4-hủy-show--hoàn-tiền)
5. [Settlement / Báo cáo tài chính](#5-settlement--báo-cáo-tài-chính)
6. [Auth & Tài khoản](#6-auth--tài-khoản)
7. [Venue/Lounge](#7-venuelounge)
8. [Show/Event (tạo, sửa, nộp duyệt, kiểm duyệt)](#8-showevent-tạo-sửa-nộp-duyệt-kiểm-duyệt)
9. [F&B](#9-fb)
10. [Recommendation/Analytics](#10-recommendationanalytics)
11. [Notification](#11-notification)
12. [Follow/Wishlist](#12-followwishlist)

---

## 1. Bán vé

### Danh sách use case con

1. Xem danh sách/chi tiết show đang mở bán (browse — công khai)
2. Giữ chỗ (Hold)
3. Hủy giữ chỗ
4. Mua vé — khởi tạo thanh toán qua VNPay
5. Xác nhận thanh toán (VNPay callback/IPN)
6. Bán vé tại quầy (Walk-in / box-office)
7. Xem vé của tôi / chi tiết vé / ảnh QR
8. Check-in vé (quét QR tại cửa)
9. Chuyển nhượng vé (initiate / accept / cancel)
10. Hủy vé đã mua (self-service, sinh yêu cầu hoàn tiền)

---

### 1.1 Giữ chỗ (Hold)

**Actor**: Audience đã đăng nhập.
**Input**: `PriceId`, `Quantity`.
**Endpoint**: `POST /tickets/holds` ([TicketsController.cs:37-47](../src/MusicLounge.Api/Controllers/TicketsController.cs)) → `HoldTicketCommand` → `HoldTicketCommandHandler.cs`.

**Luồng chính**:
1. Đọc `TicketPrice` → `TicketTier` → `LoungeShow` liên quan.
2. Kiểm tra show đang `Published` hoặc `Ongoing` — nếu không, chặn ngay.
3. Kiểm tra khung giờ bán (`price.SaleStart ≤ now ≤ price.SaleEnd`).
4. **Khoá per-show** (`IShowBookingLock`) trước khi kiểm tra/ghi quota — tránh 2 người cùng đọc "còn chỗ" trước khi ai ghi hold.
5. Kiểm tra 5 lớp quota tuần tự (§ business rule bên dưới).
6. Tạo `TicketHold` mới, `ExpiresAt = now + TicketHoldMinutes` (config, mặc định 15 phút).
7. Ngoài khoá: ghi `UserBehaviourLog(Action=ClickTicket)` (nếu đã đăng nhập) — tín hiệu ý định mua cao nhất cho gợi ý cá nhân hoá.
8. Nếu tồn kho còn ≤10% quota → gửi `NotificationType.WishlistLowStock` cho mọi user đã wishlist show này (thực hiện **ngoài** khoá per-show, để không nghẽn cổ chai lúc cháy vé).

**Luồng ngoại lệ**:
- Show không `Published`/`Ongoing` → 400 "chỉ mở bán khi show đã Published hoặc đang diễn ra".
- Ngoài khung `SaleStart`/`SaleEnd` → 400 "Đợt bán vé này chưa mở hoặc đã kết thúc".
- Vượt 1 trong 5 lớp quota → 400 với thông báo tương ứng lớp bị chặn.

**Business rule ẩn (trích code)**:
- 5 lớp kiểm tra quota, đúng thứ tự — [HoldTicketCommandHandler.cs:147-211](../src/MusicLounge.Application/Tickets/Commands/HoldTicket/HoldTicketCommandHandler.cs#L147-L211):
  1. `reserved[price.Id] + quantity > price.Quota.Value` — quota đợt bán.
  2. `tierReserved + quantity > tier.TotalCapacity.Value` — sức chứa tier.
  3. `zoneReserved + quantity > zone.Capacity` — **sức chứa vật lý thật của zone** (§6.11), cộng dồn nhiều tier cùng zone.
  4. `showReserved + quantity > relevantQuota.Value` (`OfflineQuota`/`OnlineQuota` theo `AccessType`).
  5. `showReservedTotal + quantity > activeSub.MaxTicketsPerEventSnapshot` — hạn mức gói subscription của Owner, check lại tại đây dù đã check lúc tạo tier (chặn lách qua field tuỳ chọn để trống).
- Ngưỡng cảnh báo sắp hết vé: `remaining ≤ Math.Max(1, quota × 0.10m)` — hardcode 10%, không qua system_config ([HoldTicketCommandHandler.cs:14](../src/MusicLounge.Application/Tickets/Commands/HoldTicket/HoldTicketCommandHandler.cs#L14)).

**Kết quả đầu ra**: `HoldTicketResultDto(holdId, holdExpiresAt)`.
**Tác động dữ liệu**: tạo `TicketHold`; có thể tạo nhiều `Notification` (low-stock); có thể tạo `UserBehaviourLog`.

---

### 1.2 Hủy giữ chỗ

**Actor**: Audience (chủ hold).
**Endpoint**: `DELETE /tickets/holds/{holdId}` → `CancelHoldCommandHandler.cs`.

**Luồng chính**: khoá cùng key với Purchase (`purchase-hold:{holdId}`) → kiểm tra chủ sở hữu → nếu `IsReleased=false` thì xoá hẳn row `TicketHold` (không phải soft-delete).

**Luồng ngoại lệ**:
- Không phải chủ hold → 403.
- `hold.IsReleased=true` (đã bị `PurchaseTicketCommandHandler` dùng để tạo Payment) → 409 "đã được dùng để mua vé, không thể huỷ" — [CancelHoldCommandHandler.cs:38-39](../src/MusicLounge.Application/Tickets/Commands/CancelHold/CancelHoldCommandHandler.cs#L38-L39) (bug cũ: trước đây không check, cho phép xoá hold đứng sau 1 payment thật).

**Tác động dữ liệu**: xoá `TicketHold`.

---

### 1.3 Mua vé — khởi tạo thanh toán

**Actor**: Audience (chủ hold).
**Input**: `HoldId`, `ClientIpAddress`.
**Endpoint**: `POST /tickets/purchase` → `PurchaseTicketCommandHandler.cs`.

**Luồng chính**:
1. Khoá theo `purchase-hold:{holdId}` (cùng key với Cancel Hold — loại trừ lẫn nhau).
2. Kiểm tra chủ hold, `ExpiresAt` chưa qua, `IsReleased=false`.
3. Kiểm tra show vẫn `Published`/`Ongoing`.
4. Tạo `Payment` (`Status=Pending`, `IdempotencyKey="hold:{holdId}"` — chặn 1 hold sinh 2 payment ở tầng DB).
5. Tạo N `Ticket` (`Status=Pending`, `BuyerId` = user hiện tại, chưa có `QrCode`).
6. Đánh dấu `hold.IsReleased=true` (tránh đếm trùng vào quota — pending ticket + active hold không được cộng dồn).
7. Gọi `IVnPayService.CreatePaymentUrl(...)` → trả URL để redirect người dùng sang VNPay.

**Luồng ngoại lệ**:
- Hold hết hạn → 400.
- Hold đã dùng rồi (`IsReleased=true`) → 409 "đã được dùng để mua vé rồi" — [PurchaseTicketCommandHandler.cs:52-53](../src/MusicLounge.Application/Tickets/Commands/PurchaseTicket/PurchaseTicketCommandHandler.cs#L52-L53) (cùng dạng bug-fix như CancelHold: trước đây không check).
- Show đổi trạng thái giữa lúc Hold và Purchase (vd bị Admin cancel) → 400.

**Kết quả đầu ra**: `PaymentInitiationDto(paymentId, orderId, totalAmount, paymentUrl, ticketIds[])` — FE redirect user sang `paymentUrl`.
**Tác động dữ liệu**: tạo `Payment`, tạo N `Ticket` (Pending), cập nhật `TicketHold.IsReleased`.

---

### 1.4 Xác nhận thanh toán (VNPay callback/IPN)

**Actor**: Cổng thanh toán VNPay (server-to-server, không phải người dùng).
**Input**: query string VNPay gửi về (`vnp_TxnRef`, `vnp_Amount`, `vnp_SecureHash`...).
**Endpoint**: `GET /payments/vnpay/callback` (redirect trình duyệt) và `GET /payments/vnpay/ipn` (server-to-server — nguồn sự thật thật) → cả 2 gọi chung `ProcessVnPayCallbackCommand` → `ProcessVnPayCallbackCommandHandler.cs`.

**Luồng chính**:
1. `_vnPay.VerifyCallback(queryParams)` — verify chữ ký `vnp_SecureHash`. Sai chữ ký → từ chối ngay, **không đụng vào dữ liệu**, log riêng biệt.
2. Khoá theo `vnpay-ticket:{txnRef}` — chống 2 callback gần như đồng thời (VNPay có thể gọi lại nhiều lần) cùng confirm trùng.
3. Tìm `Payment` theo `OrderId = txnRef`.
4. **Idempotency**: nếu `payment.Status != Pending` → đã xử lý rồi, trả kết quả cũ, không làm gì thêm (tránh ghi đè `QrCode`, tránh tạo trùng `PhysicalTicketDetail`/`LivestreamTicketDetail`).
5. **Đối chiếu số tiền**: `result.Amount != payment.GrossAmount` → từ chối dù chữ ký hợp lệ (fail-closed, không tin tưởng callback cho số tiền không khớp).
6. Nếu VNPay báo thành công: `Payment→Confirmed`, mọi `Ticket` liên quan → `Confirmed` + sinh `QrCode` mới; tuỳ `TicketTier.AccessType` mà tạo `LivestreamTicketDetail` (kèm `AccessToken`) hoặc `PhysicalTicketDetail`.
7. Publish domain event `TicketPaymentConfirmed` → kích hoạt 3 handler độc lập (xem §1.4.1).
8. Nếu VNPay báo thất bại: `Payment→Failed`, mọi Ticket liên quan → `Cancelled`.

**Luồng ngoại lệ**:
- Chữ ký sai → false, không đổi dữ liệu, log "VNPay ticket callback rejected — invalid signature".
- Không tìm thấy Payment theo TxnRef → false, log cảnh báo.
- Callback trùng lặp (đã xử lý) → trả lại kết quả cũ, không xử lý lại (idempotent).
- Số tiền không khớp → từ chối, log "amount mismatch".

**Business rule ẩn**: đây chính là câu trả lời cho việc "chữ ký VNPay được verify ra sao" — 2 lớp độc lập: (a) verify HMAC chữ ký, (b) đối chiếu `Amount` với `GrossAmount` đã lưu **trước khi tin bất kỳ điều gì callback nói** — [ProcessVnPayCallbackCommandHandler.cs:90-98](../src/MusicLounge.Application/Tickets/Commands/ProcessVnPayCallback/ProcessVnPayCallbackCommandHandler.cs#L90-L98).

**Tác động dữ liệu**: cập nhật `Payment`, cập nhật N `Ticket`, tạo `PhysicalTicketDetail`/`LivestreamTicketDetail`.

#### 1.4.1 Domain event `TicketPaymentConfirmed` — 3 handler chạy song song

| Handler | Việc làm |
|---|---|
| `WriteTicketLedgerHandler` | Ghi bút toán kép: Gateway (debit gross) → Platform (credit hoa hồng) + Platform (credit `OwnerNet`, **giữ hộ, chưa trả Owner**) + Tax (credit thuế). Cập nhật `Payment.PlatformFee`/`TaxWithheld`/`NetAmount`. **Bỏ qua hoàn toàn nếu `PaymentMethod=Cash` và `WalkInCommissionEnabled=false`** (mặc định) — tiền mặt venue tự thu, ghi sổ sẽ khiến hệ thống tưởng nhầm đã nhận tiền thật rồi lên lịch trả lại 2 lần ([WriteTicketLedgerHandler.cs:28-37](../src/MusicLounge.Application/Tickets/DomainEventHandlers/WriteTicketLedgerHandler.cs#L28-L37)). |
| `ScheduleSettlementHandler` | Tạo 2 `Settlement` (Partial70/Final30) — xem [§5](#5-settlement--báo-cáo-tài-chính). |
| `SendFcmConfirmHandler` | Gửi push "vé đã xác nhận" cho Buyer. |

---

### 1.5 Bán vé tại quầy (Walk-in)

**Actor**: Staff/Owner/Admin của đúng venue (`VenueOperatorAccess.CanOperate`).
**Input**: `PriceId`, `Quantity`.
**Endpoint**: `POST /tickets/walk-in` ([TicketsController.cs:105-119](../src/MusicLounge.Api/Controllers/TicketsController.cs)) → `SellWalkInTicketCommandHandler.cs`.

**Luồng chính**: gần giống Hold+Purchase gộp làm 1 bước, khác biệt chính:
1. Chỉ bán được vé `AccessType=Physical` (không bán walk-in cho vé online).
2. `price.PurchaseChannel` phải khác `Online` (chỉ bán được nếu đợt giá cho phép Offline/Both).
3. Cùng 4 lớp quota như Hold (bỏ lớp subscription riêng lẻ — thực ra vẫn check `MaxTicketsPerEventSnapshot` ở cuối).
4. Tạo `Payment` ngay `Status=Confirmed`, `Method=Cash`, `PayerId=null` (không gắn user nào).
5. Tạo Ticket ngay `Status=Confirmed`, `BuyerId=null`, `QrCode` sinh ngay (không qua bước chờ thanh toán).
6. Tạo `PhysicalTicketDetail.SoldByStaffId` = người bán.
7. Publish `TicketPaymentConfirmed` với `UserId=0` — vẫn kích hoạt `ScheduleSettlementHandler` (nhưng `WriteTicketLedgerHandler` sẽ bỏ qua theo rule ở §1.4.1 nếu `WalkInCommissionEnabled=false`).

**Business rule ẩn**: khách walk-in **không tồn tại dưới dạng `User` nào trong hệ thống** — đây là biến thể dữ liệu đã ghi trong [12 §4](12-actors-and-authorization.md#4-actor-có-nhiều-biến-thể-dữ-liệu).

**Tác động dữ liệu**: tạo `Payment` (Confirmed ngay), tạo N `Ticket` (Confirmed ngay), tạo `PhysicalTicketDetail`.

---

### 1.6 Xem vé của tôi / chi tiết vé / ảnh QR

**Actor**: Audience (chủ vé).
**Endpoint**: `GET /tickets/my`, `GET /tickets/{id}`, `GET /tickets/{id}/qr` — các query handler đơn giản, đọc `Ticket` theo `BuyerId=currentUser`, sinh ảnh QR từ `Ticket.QrCode` bằng `Net.Codecrete.QrCodeGenerator` (đã ghi nhận trước đây — chọn thư viện này để tránh phụ thuộc `System.Drawing`).
**Tác động dữ liệu**: chỉ đọc, không ghi.

---

### 1.7 Check-in vé (quét QR tại cửa)

**Actor**: Staff/Owner/Admin của đúng venue.
**Input**: `QrCode` (quét từ vé).
**Endpoint**: `POST /tickets/check-in` → `CheckInTicketCommandHandler.cs`.

**Luồng chính**:
1. Khoá theo `checkin:{qrCode}` — chống 2 cửa quét trùng 1 vé cùng lúc.
2. Tìm vé theo QR, kiểm tra `VenueOperatorAccess.CanOperate`.
3. Show phải đang `Ongoing`.
4. Tier phải `AccessType=Physical` (vé online không cần check-in).
5. Vé phải `Status=Confirmed`, chưa từng check-in (`PhysicalDetail.CheckedInAt is null`), không đang trong quá trình chuyển nhượng.
6. Set `Ticket.Status=Used`, `PhysicalDetail.CheckedInAt`+`CheckedInByStaffId`.

**Luồng ngoại lệ**: show chưa/đã qua giờ diễn, vé sai loại, vé đã check-in trước đó (409), vé đang chờ chuyển nhượng.
**Không có offline fallback** nếu mất mạng lúc quét (rủi ro đã biết, chấp nhận).
**Tác động dữ liệu**: cập nhật `Ticket.Status`, `PhysicalTicketDetail`.

---

### 1.8 Chuyển nhượng vé

**Actor chính**: Audience (chủ vé hiện tại) khởi tạo; Audience (người nhận, xác định qua email) chấp nhận/từ chối.

**Bước 1 — Initiate** (`POST /tickets/{id}/transfer`, `InitiateTicketTransferCommandHandler.cs`):
1. Chủ vé, vé `Confirmed`, chưa có transfer nào đang chờ, show chưa `Ended`/`Cancelled`, vé chưa check-in, chưa từng xem livestream (`LivestreamDetail.FirstAccessedAt is null`).
2. Tìm người nhận theo email — phải tồn tại tài khoản, không được là chính mình.
3. **Ghi có điều kiện nguyên tử** (`TryInitiateTransferAsync`) re-check `PendingTransferToUserId IS NULL` ngay tại thời điểm ghi — chặn race 2 request initiate gần như đồng thời.
4. Gửi `Notification` cho người nhận.

**Bước 2a — Accept** (`AcceptTicketTransferCommandHandler.cs`): người được mời xác nhận → `Ticket.BuyerId` đổi chủ, **sinh `QrCode` mới** + **sinh `AccessToken` mới cho `LivestreamDetail`** (chủ cũ từng thấy mã cũ — coi như đã lộ, phải đổi vì lý do bảo mật), clear `PendingTransferToUserId`. Báo lại chủ cũ.

**Bước 2b — Cancel** (`CancelTicketTransferCommandHandler.cs`): chủ vé tự huỷ yêu cầu đang chờ, clear `PendingTransferToUserId`.

**Business rule ẩn**: vé đang `PendingTransferToUserId != null` bị "đóng băng" khỏi check-in (§1.7) và huỷ vé (§1.9/§4) — chỉ giải phóng khi accept/cancel transfer, tránh vừa chuyển vừa dùng.

**Tác động dữ liệu**: cập nhật `Ticket` (`PendingTransferToUserId`/`BuyerId`/`QrCode`), cập nhật `LivestreamTicketDetail.AccessToken` (nếu có).

---

### 1.9 Hủy vé đã mua (self-service)

Xem chi tiết đầy đủ ở [§4.1](#41-hủy-vé-đơn-lẻ-do-audience-tự-yêu-cầu) — thuộc domain "Hủy show & Hoàn tiền" nhưng entry point nằm trong `TicketsController`/`Tickets` module (`POST /tickets/{id}/cancel`, `CancelTicketCommandHandler.cs`).

---

## 2. Livestream

### Danh sách use case con

1. Owner tạo Livestream cho 1 show Online
2. Bắt đầu / Kết thúc phát
3. Khán giả kết nối xem (SignalR Hub) — kiểm tra quyền + giới hạn thiết bị
4. Chat trong lúc xem
5. Admin/Owner buộc dừng (Terminate)
6. Gỡ tour ảo 360° dùng chung màn hình livestream (liên domain, ghi chú riêng)

---

### 2.1 Tạo Livestream

**Actor**: Staff/Owner/Admin của venue đó (`VenueOperatorAccess.CanOperate`, không phải chỉ riêng Owner).
**Endpoint**: `POST /livestreams` → `CreateLivestreamCommandHandler.cs`.

**Luồng chính**:
1. Khoá theo `create-livestream:{showId}` — chặn tạo trùng khi 2 request gần như đồng thời.
2. `LoungeShow.Format` không được là `Offline`.
3. Show **được phép ở `Draft`/`Pending`** — cố ý cho tạo livestream **trước khi** show được nộp duyệt (D15: hạ tầng phát phải sẵn sàng trước, không phải sau khi duyệt) — chỉ chặn nếu show đã `Cancelled`/`Ended`.
4. Chưa có `Livestream` nào khác cho show này.
5. **Gọi API tạo stream thật qua `ILivestreamServiceFactory` (Mux)** — `provider.CreateStreamAsync(show.Name, ct)` trả về `ProviderRef`/`RtmpUrl`/`StreamKey`/`HlsUrl` thật từ Mux, không phải giá trị tự sinh.
6. Tạo `Livestream(Status=Scheduled)`.
7. **Tự động tạo `EventModeration(TargetType=Livestream)`** kèm `SlaDeadline` (24h, NĐ 147/2024) và enqueue job chấm điểm AI — nghĩa là **livestream cũng phải qua kiểm duyệt AI+Admin giống Show** (doc 11 §3), không phải chỉ show mới cần duyệt.

**Business rule ẩn**: tạo Livestream **luôn** kéo theo 1 vòng kiểm duyệt bắt buộc — không có đường nào tạo Livestream mà bỏ qua bước duyệt Admin trước khi phát được (xem §2.2).

**Tác động dữ liệu**: tạo `Livestream`, tạo `EventModeration`.

### 2.2 Bắt đầu phát

**Actor**: Staff/Owner/Admin của venue đó.
**Endpoint**: `POST /livestreams/{id}/start` → `StartLivestreamCommandHandler.cs`.

**Luồng chính**:
1. `Livestream.Status` phải đang `Scheduled`.
2. **Bắt buộc đã được Admin duyệt**: `EventModeration.AdminDecision == Approved` cho đúng livestream này — chưa duyệt thì chặn cứng "Livestream chưa được Admin duyệt. Không thể phát sóng." (W08).
3. **Bắt buộc đã khai báo tác quyền VCPMC**: `show.VcpmcRoyaltyReference` không được rỗng (D19) — kiểm tra lại **đúng lúc phát**, khác thời điểm với giấy phép biểu diễn (kiểm tra lúc duyệt show, xem doc 11 §3).
4. `Livestream.Status→Live`, `StartedAt=now`; **đồng thời** `LoungeShow.Status→Ongoing`, `ActualStart=now` (1 hành động đổi trạng thái cả 2 entity).
5. Backfill `LivestreamTicketDetail`+`AccessToken` cho những vé đã `Confirmed` từ trước khi stream bắt đầu (mua vé sớm, lúc đó chưa có gì để gắn access token).
6. Gửi `NotificationType.EventLive` cho **cả 2 nhóm**: người đã mua vé của show này **và** người follow venue này (hợp nhất, khử trùng).

**Luồng ngoại lệ**: sai trạng thái nguồn, chưa qua duyệt, chưa khai báo VCPMC, sai venue operator → đều chặn với thông báo tương ứng.

**Tác động dữ liệu**: cập nhật `Livestream`, cập nhật `LoungeShow` (`Status`/`ActualStart`), tạo N `LivestreamTicketDetail`, tạo nhiều `Notification`.

### 2.2.1 Kết thúc phát

**Actor**: Staff/Owner/Admin của venue đó.
**Endpoint**: `POST /livestreams/{id}/end` → `EndLivestreamCommandHandler.cs`.

**Luồng chính**:
1. `Livestream.Status` phải đang `Live`.
2. `Livestream.Status→Ended`, `EndedAt=now`, `ViewerCount` reset về 0.
3. **Đồng thời** `LoungeShow.Status→Ended`, `ActualEnd=now`, **và set `RatingOpenUntil = now + RatingWindowDays`** (config, mặc định 7 ngày, §6.13) — đây chính là nơi trả lời câu hỏi mở #4 ở doc 11 (điều kiện mở đánh giá).
4. Best-effort gọi Mux xoá stream (`provider.DeleteStreamAsync`) — lỗi ở bước này chỉ log cảnh báo, **không** chặn việc kết thúc livestream (dọn dẹp hạ tầng không được phép chặn nghiệp vụ).

**Đã kiểm tra và đính chính**: show `Offline` thuần (không có Livestream) **vẫn có** cặp lệnh riêng để set `ActualStart`/`ActualEnd` — `StartLoungeShowCommandHandler.cs`/`EndLoungeShowCommandHandler.cs` (Staff tại quầy bấm "bắt đầu/kết thúc show"), logic giống hệt (cùng check `VcpmcRoyaltyReference`, cùng set `RatingOpenUntil = now + RatingWindowDays`) nhưng **tự chặn nếu show đó có Livestream** ("Show này có livestream — dùng chức năng bắt đầu/kết thúc livestream thay vì lệnh này") — 2 cặp lệnh loại trừ lẫn nhau theo đúng 1 show, đảm bảo `ActualStart`/`ActualEnd` luôn được ghi bởi đúng 1 con đường bất kể show có livestream hay không. Vì vậy cơ chế chống gian lận theo thời lượng thực tế ở §5.2 **bao phủ mọi định dạng show**, không chỉ riêng show có livestream như suy đoán ban đầu.

### 2.3 Khán giả kết nối xem

**Actor**: Audience (chủ vé) / Staff/Owner venue đó / Admin.
**Endpoint**: SignalR Hub `LivestreamHub.OnConnectedAsync` (không phải REST endpoint).

**Luồng chính**:
1. Xác định quyền qua `LivestreamAccessPolicy`/inline logic tương đương trong Hub: Admin luôn qua; Staff/Owner phải đúng venue (`VenueOperatorAccess.CanOperate`); còn lại phải là **chủ vé thật** (`HasViewerAccessAsync`) — không phải operator.
2. Nếu là khán giả có vé thật (`isGenuineTicketHolder`): kiểm tra giới hạn số thiết bị xem đồng thời qua `_sessionTracker.TryAddSession(livestreamId, userId, connectionId, MaxConcurrentLivestreamSessionsPerTicket)` — vượt giới hạn → `Context.Abort()`.
3. Join SignalR group của livestream đó.
4. Tăng `ViewerCount` **nguyên tử tại DB** (`ExecuteUpdateAsync`, không đọc-rồi-ghi) — tránh mất đếm khi nhiều người vào cùng lúc.

**Luồng ngoại lệ**: không đủ quyền → `Context.Abort()` ngay không join group. Vượt giới hạn thiết bị/vé → `Context.Abort()`.

**Business rule ẩn**: `LivestreamTicketDetail.AccessToken` **từng tồn tại nhưng chưa bao giờ được đọc lại để enforce** — lỗ hổng cho phép 1 tài khoản share xem trên vô hạn thiết bị, đã vá bằng cơ chế session-tracker riêng (không phải bằng AccessToken) — [LivestreamHub.cs:76-88](../src/MusicLounge.Infrastructure/Hubs/LivestreamHub.cs#L76-L88).

**Tác động dữ liệu**: `Livestream.ViewerCount`/`PeakViewerCount`/`TotalViews` tăng; disconnect → giảm `ViewerCount` (chỉ nếu connection đó từng được cộng — có marker riêng tránh trừ nhầm connection bị Abort ngay từ đầu).

### 2.4 Chat trong lúc xem

**Actor**: bất kỳ ai đã qua được bước 2.3 (đã join group) và `Livestream.ChatEnabled=true`.
**Luồng chính**: gửi `Message` → lưu `LivestreamChatMessage` → broadcast realtime cho cả group qua SignalR.

### 2.5 Buộc dừng (Terminate)

**Actor**: Admin (qua kiểm duyệt) hoặc Owner.
**Luồng chính**: `Status=Terminated` (khác `Ended` tự nhiên), ghi `TerminatedById`+`TerminatedReason`. Đây là 1 trong 4 `ModerationTargetType` (`EventModeration.TargetType=Livestream`) — có thể phát sinh từ luồng kiểm duyệt AI+Admin giống Show (xem doc 11 §3).

**Tích hợp bên ngoài**: video thật sự phát qua **Mux** (RTMP ingest từ Owner → HLS phát cho khán giả) — `Livestream.RtmpUrl`/`StreamKey`/`HlsUrl` do Mux cấp; phần "phát/nhận video" nằm ngoài .NET backend, backend chỉ quản lý vòng đời bản ghi + quyền truy cập + đếm viewer + chat, không xử lý luồng video.

---

## 3. Donate / Tip

### Danh sách use case con

1. Audience khởi tạo donate cho 1 lượt diễn (`Performance`) — chỉ khi show đang `Ongoing`
2. Xác nhận thanh toán (VNPay return/IPN)
3. Owner xác nhận đã nhận tiền (Acknowledge) — có auto-confirm sau 24h nếu Owner không thao tác
4. Owner xác nhận đã chuyển tiền cho nghệ sĩ (Confirm Paid)
5. Xem lịch sử donate (của tôi / công khai theo performer / sổ minh bạch công khai toàn hệ thống)
6. Thông báo donate realtime công khai (SignalR, kiểu overlay livestream) — phát ở cả 3 chặng thay đổi trạng thái tiền thật
7. Thông báo riêng tư cho donor khi thanh toán thành công (thêm 2026-08-13 — trước đó chỉ Owner được báo)

---

### 3.1 Khởi tạo donate

**Actor**: Audience (đăng nhập — endpoint yêu cầu `RequireAuthenticated`).
**Input**: `PerformanceId`, `Amount`, `IsAnonymous`, `Message`, `IsMessagePublic`.
**Endpoint**: `POST /donations` → `CreateDonationCommandHandler.cs`.

**Luồng chính**:
1. Đọc `Performance` → `LoungeShow` liên quan.
2. **Show phải đang `Ongoing`** — không donate được khi show chưa bắt đầu hoặc đã kết thúc ([CreateDonationCommandHandler.cs:44-45](../src/MusicLounge.Application/Donations/Commands/CreateDonation/CreateDonationCommandHandler.cs#L44-L45)) — khác với mua vé (mở từ lúc `Published`).
3. `Performance.AcceptsDonation` phải `true` cho đúng lượt diễn này.
4. Tính trước `Net` (ước lượng, dùng chung công thức `PaymentFeeCalculator.Split` với vé/settlement) — chỉ là **ước lượng hiển thị**, con số chính thức được ghi đè ở bước xác nhận thanh toán (§3.2) vì tỷ lệ hoa hồng/thuế có thể đổi giữa lúc khởi tạo và lúc VNPay xác nhận.
5. Tạo `Donation(Status=PendingPayment)`, gọi `IVnPayService.CreatePaymentUrl` — trả URL redirect, cùng khuôn mẫu với mua vé.

**Business rule ẩn**: `AcceptsDonation` nằm trên `Performance` (từng lượt diễn), nghĩa là cùng 1 nghệ sĩ có thể nhận donate ở show này nhưng bị tắt ở show khác — quyết định theo booking, không phải thuộc tính cố định của nghệ sĩ.

### 3.2 Xác nhận thanh toán donate

**Actor**: VNPay (server-to-server).
**Endpoint**: `GET /donations/vnpay-return` (redirect) + `GET /donations/vnpay-ipn` (nguồn sự thật) → `ProcessDonationPaymentCommandHandler.cs`.

**Luồng chính** (cùng khuôn mẫu xác thực với §1.4 — verify chữ ký [dòng 47], đối chiếu `callbackResult.Amount != donation.Gross` fail-closed [dòng 78], idempotent theo status):
1. Verify chữ ký VNPay, đối chiếu số tiền.
2. `Donation.Status: PendingPayment → PendingOwnerAck`, ghi `PaymentConfirmedAt=now` (mốc bắt đầu tính cửa sổ 24h auto-confirm), ghi đè `Net` bằng con số chính thức (không dùng ước lượng ở bước tạo nữa).
3. **Chốt `PerformerShareRateSnapshot`** tại đúng thời điểm này từ `system_config.donation_performer_share_rate` (mặc định 88%/2%, đã ghi nhận trước đây) — vì đây là lúc `Net` trở thành số chính thức, chốt 1 lần để Admin đổi rate sau này không ảnh hưởng ngược donation đã xảy ra.
4. **Phát sự kiện realtime công khai** — `PublicDonationBroadcast.PublishAsync(...)` ([dòng 175](../src/MusicLounge.Application/Donations/Commands/ProcessDonationPayment/ProcessDonationPaymentCommandHandler.cs#L175)) — xem §3.6.
5. **Báo riêng cho donor** (thêm 2026-08-13) — `NotifyAsync(donorId, DonationConfirmed, ...)` ngay sau bước 4, guard bằng `donation.DonorUserId is int donorId`. Trước bản vá này donor chỉ biết donate thành công qua redirect trình duyệt 1 lần (mất nếu đóng tab trước khi VNPay trả về) hoặc tự vào `GET /donations/my` — không hề có push/in-app notification nào, khác hẳn Owner luôn được báo (`DonationReceived`, bước riêng cùng hàm). Thông báo này **không bị lọc** bởi `IsAnonymous`/`IsAmountPublic` vì là kênh 1-1 riêng tư (`NotificationsController` yêu cầu đúng token của donor).

**Business rule ẩn**: `PerformerShareRateSnapshot` chỉ đọc `system_config` **đúng 1 lần** tại bước này — [Donation.cs:32-38](../src/MusicLounge.Domain/Entities/Donation.cs#L32-L38) tự giải thích lý do (không dùng lại ở `ConfirmDonationPaidCommandHandler` bước 2 chặng — đọc `snapshot`, không đọc config sống).

### 3.3 Owner xác nhận đã nhận tiền (Acknowledge)

**Actor**: Owner (venue tổ chức lượt diễn đó).
**Endpoint**: `POST /donations/{id}/acknowledge` → `AcknowledgeDonationCommandHandler.cs`.
**Luồng chính**: `Status=PendingOwnerAck → OwnerReceived`, ghi `OwnerAckAt=now`.

**Cơ chế tự động nếu Owner không thao tác** — Hangfire job (đã ghi nhận cấu trúc ở doc 11 §5) quét donation `PendingOwnerAck` quá 24h kể từ `PaymentConfirmedAt` → tự chuyển `OwnerReceived` + đánh dấu `AutoConfirmed=true`. Đồng thời sinh `NotificationType.DonationPending` cảnh báo nếu quá hạn mà chưa tự động xử lý xong.

Owner Acknowledge (dù thủ công hay tự động) cũng phát `PublicDonationBroadcast` — chặng 2/3 của luồng công khai (§3.6).

### 3.4 Owner xác nhận đã chuyển tiền cho nghệ sĩ (Confirm Paid)

**Actor**: Owner.
**Input**: `PaymentEvidenceUrl` (bắt buộc — ảnh chụp màn hình chuyển khoản, lấy từ `POST /uploads/images` trước đó).
**Endpoint**: `POST /donations/{id}/confirm-paid` → `ConfirmDonationPaidCommandHandler.cs`.
**Luồng chính**: `Status=OwnerReceived → PerformerPaid`, ghi `OwnerPaidAt`, `PaymentEvidenceUrl`, `PaymentRef`. Phát `PublicDonationBroadcast` — chặng 3/3 (cuối).

**Luồng ngoại lệ**: chỉ Owner của venue liên quan mới gọi được; chỉ hợp lệ khi đang đúng `OwnerReceived` (không nhảy cóc từ `PendingOwnerAck`).

### 3.5 Xem lịch sử donate

**Actor**: Audience (của tôi, `GET /donations/my`) hoặc **công khai không cần đăng nhập** — 2 route riêng biệt:
- `GET /performers/{performerId}/donations` — lịch sử theo 1 nghệ sĩ (`PerformerDonationsController.GetPublicHistory`), chỉ `DisplayName`/`Gross` (không có breakdown phí).
- `GET /donations/public` — **sổ minh bạch toàn hệ thống** (thêm 2026-08-13, `DonationsController.GetPublicFeed`), mọi nghệ sĩ gộp chung, kèm đầy đủ breakdown phí (`Gross/Net/PlatformFee/Tax/PerformerShareRate/PerformerAmount/OwnerRetained`) — cùng bộ số với `DonationDto` mà donor/Owner xem được, không lệch số.

**Luồng chính**: cả 2 route chỉ trả về donation `Status ∈ {OwnerReceived, PerformerPaid}` — loại `PendingOwnerAck` dù đã có tiền vào, vì đây là "sổ đã chốt" (posted), khác luồng realtime ở §3.6 (đã bắn alert cho `PendingOwnerAck` rồi). Field ẩn theo 2 cờ **độc lập nhau**: `IsAnonymous` chỉ ẩn `DonorDisplayName`; `IsAmountPublic` ẩn **cả cụm 7 field tiền cùng lúc** (ẩn lẻ từng field sẽ lộ ngược `Gross` qua phép cộng). Performer/venue/show/status/thời gian không bao giờ bị ẩn — đây là thông tin về buổi diễn, không phải của donor. Đã đối chiếu với thực tế ngoài đời (GoFundMe: 2 cờ tách biệt y hệt; Ko-fi: không cho ẩn số tiền, chặt hơn) — không phải tự nghĩ ra.

### 3.6 Tích hợp thời gian thực — thông báo donate công khai (kiểu overlay livestream)

**Cơ chế**: `PublicDonationBroadcast.PublishAsync` ([PublicDonationBroadcast.cs](../src/MusicLounge.Application/Donations/Common/PublicDonationBroadcast.cs)) — dùng chung 1 điểm phát cho **cả 3 chặng** thay đổi trạng thái có ý nghĩa "tiền vừa di chuyển thật": VNPay xác nhận (§3.2), Owner ghi nhận (§3.3), Owner trả nghệ sĩ (§3.4). Mỗi lần gọi:
1. Lọc quyền riêng tư ngay tại nguồn phát (không phải ở client): `IsAnonymous` → ẩn `DisplayName`; `IsAmountPublic` → ẩn `Gross`; `IsMessagePublic` → ẩn `Message`.
2. Đẩy `PublicDonationAlertDto` qua `IPublicDonationHubService` (SignalR Hub riêng, `PublicDonationHub` — công khai theo `LoungeShowId`, không yêu cầu là chủ vé) tới mọi client đang lắng nghe show đó.

**Ý nghĩa nghiệp vụ**: đây là cơ chế đứng sau tính năng kiểu "thông báo donate hiện trên màn hình livestream" (giống overlay Streamlabs) — khán giả xem trực tiếp thấy ngay khi có người donate, không cần tải lại trang; đồng thời tiến trình 3 chặng (đã trả tiền → Owner đã nhận → nghệ sĩ đã được trả) cũng phát realtime, cho phép hiển thị trạng thái minh bạch cho khán giả nếu FE muốn dùng.

**Tích hợp bên ngoài (thanh toán)**: VNPay — giống hệt cơ chế xác thực callback ở §1.4 (verify chữ ký + đối chiếu số tiền fail-closed), nhưng route/txnRef độc lập với luồng vé (`DON-` prefix cho `OrderId` so với `ML-`/`WI-` của vé) — donation và ticket có 2 `ProcessXxxCallback` handler riêng biệt dù cùng gọi `IVnPayService.VerifyCallback`.

---

## 4. Hủy show & Hoàn tiền

### Danh sách use case con

1. Hủy vé đơn lẻ (Audience tự yêu cầu) → sinh `RefundRequest`
2. Owner hủy toàn bộ show (chưa diễn ra)
3. Admin gỡ show vi phạm (Take-down, qua xử lý complaint) — hoàn 100% mọi vé
4. Admin xử lý `RefundRequest` (duyệt/từ chối, có thể duyệt một phần)

---

### 4.1 Hủy vé đơn lẻ (do Audience tự yêu cầu)

**Actor**: Audience (chủ vé).
**Endpoint**: `POST /tickets/{id}/cancel` → `CancelTicketCommandHandler.cs` (đã trace chi tiết ở [§1.9](#19-hủy-vé-đã-mua-self-service), nhắc lại phần business rule quan trọng nhất tại đây vì đây mới là domain sở hữu logic hoàn tiền):

**Luồng chính (2 nhánh hoàn toàn khác nhau theo trạng thái vé)**:

- **Nhánh A — vé `Pending`** (chưa từng thanh toán thật, đang chờ VNPay hoặc job tự huỷ do hết hạn): Audience huỷ được **ngay lập tức**, không cần chờ, không áp dụng chính sách hoàn vé/deadline của show (vì chưa có tiền thật để hoàn) — `Ticket→Cancelled`, `Payment→Failed` nếu đang Pending.
- **Nhánh B — vé `Confirmed`** (đã thanh toán thật): phải đi qua toàn bộ điều kiện:
  1. `show.CancellationAllowed` phải `true`.
  2. Chưa quá hạn: `now ≤ show.ScheduledStart − CancellationDeadlineHours`.
  3. Vé không đang trong quá trình chuyển nhượng.
  4. Có `PaymentId` hợp lệ.
  5. Set `Ticket→Cancelled`, tạo `RefundRequest(Status=Pending, AmountRequested = price.Price × show.RefundPercentage / 100, RefundPercentage = show.RefundPercentage ?? 100)` — **chưa hoàn tiền ngay**, chỉ tạo yêu cầu chờ Admin xử lý (§4.4).

**Business rule ẩn quan trọng nhất domain này**: `RefundPercentage` không phải hằng số toàn hệ thống — nằm **trên từng show** (`LoungeShow.RefundPercentage`), Owner tự khai báo lúc tạo show. Nếu Owner không set (`null`), mặc định hoàn **100%** ([CancelTicketCommandHandler.cs:86](../src/MusicLounge.Application/Tickets/Commands/CancelTicket/CancelTicketCommandHandler.cs#L86)) — an toàn cho khán giả theo hướng có lợi hơn khi Owner quên cấu hình.

**Tác động dữ liệu**: `Ticket.Status`, có thể `Payment.Status`, có thể tạo `RefundRequest`.

---

### 4.2 Owner hủy toàn bộ show

**Actor**: Owner hoặc Admin.
**Endpoint**: `LoungeShows/Commands/CancelLoungeShow` → `CancelLoungeShowCommandHandler.cs` (đã đọc trực tiếp, không còn là suy luận).

**Luồng chính**:
1. Khoá theo `show-status-change:{showId}` — cùng namespace khoá với `ChangeLoungeShowFormatCommandHandler`, chặn luôn trường hợp đổi định dạng show đang chạy song song với huỷ show (cả 2 đều sinh `RefundRequest` từ cùng tập vé).
2. Chỉ Owner của venue hoặc Admin được gọi; show phải chưa `Cancelled`/`Ended`.
3. **Chặn nếu livestream đang `Live`** — phải terminate livestream trước khi huỷ show ([CancelLoungeShowCommandHandler.cs:53-60](../src/MusicLounge.Application/LoungeShows/Commands/CancelLoungeShow/CancelLoungeShowCommandHandler.cs#L53-L60)) — tránh huỷ+hoàn tiền trong khi vẫn đang phát cho người xem trả phí mà không có gì báo dừng stream.
4. `LoungeShow.Status → Cancelled`.
5. Với **mọi** vé đang `Confirmed`: `Ticket → Cancelled`, tạo `RefundRequest(RefundPercentage=100, Status=Pending)` — **ép cứng 100% bất kể `show.RefundPercentage` khai báo là bao nhiêu**, khác với huỷ vé do khán giả tự yêu cầu (§4.1, theo % cấu hình của show). Đây **vẫn là 1 `RefundRequest` chờ Admin xử lý** ở §4.4, không phải hoàn tiền tức thì tự động.
6. Gửi `NotificationType.EventCancelled` cho từng người mua.

**Business rule ẩn**: điểm bất đối xứng cố ý — Audience tự huỷ có thể chỉ được hoàn 1 phần (theo `RefundPercentage` của show), nhưng huỷ show (lỗi không thuộc về khán giả) luôn tạo yêu cầu hoàn đủ 100%, dù việc thực-thi-hoàn-tiền vẫn cần Admin `Process` request đó.

---

### 4.3 Admin gỡ show vi phạm (qua Complaint)

**Actor**: Admin.
**Endpoint**: `POST /complaints/{id}/resolve` → `ResolveComplaintCommandHandler.cs`.
**Luồng chính**: khi `ResolvedAction=TakeDownContent` và `Complaint.TargetType="show"` → gọi private method nội bộ `TakeDownShowAsync` — **logic giống hệt `CancelLoungeShowCommandHandler` (cùng khoá, cùng điều kiện chặn Live, cùng tạo `RefundRequest` 100%) nhưng là bản sao độc lập, KHÔNG gọi lại `CancelLoungeShowCommand` qua `ISender.Send`**.

**Business rule ẩn đáng chú ý (rủi ro bảo trì tự nhận biết trong code)**: lý do không compose 2 command là vì `ResolveComplaintCommandHandler` đã chạy trong transaction của `TransactionBehavior`, và MediatR `Send` lồng thêm 1 command khác sẽ cố `BeginTransactionAsync` lần 2 trên cùng connection → lỗi "connection is already in a transaction". Code tự để lại cảnh báo: *"Keep this in sync with CancelLoungeShowCommandHandler if that logic changes"* ([ResolveComplaintCommandHandler.cs:109-114](../src/MusicLounge.Application/Complaints/Commands/ResolveComplaint/ResolveComplaintCommandHandler.cs#L109-L114)) — nghĩa là 2 nơi giữ 2 bản logic hủy-show+hoàn-tiền song song, sửa 1 chỗ mà quên chỗ kia sẽ khiến "huỷ thường" và "gỡ do vi phạm" lệch nhau âm thầm.

**Tích hợp pháp lý**: đây là cách nền tảng đáp ứng NĐ 147/2024/NĐ-CP (phải có khả năng gỡ nội dung vi phạm theo khiếu nại có căn cứ).

---

### 4.4 Admin xử lý RefundRequest

**Actor**: Admin.
**Endpoint**: `GET /admin/refund-requests` (danh sách chờ), `POST /admin/refund-requests/{id}/process` → `ProcessRefundRequestCommandHandler.cs`.

**Luồng chính**:
1. Khoá theo `refund:{refundRequestId}` — chống duyệt trùng.
2. **Reject**: `Status→Rejected`, dừng, không đụng tiền.
3. **Approve**: `AmountApproved` = giá trị Admin nhập, mặc định = `AmountRequested` nếu không nhập — **không được vượt `Payment.GrossAmount`**, và **tổng mọi refund đã Approved trên cùng 1 Payment cũng không được vượt GrossAmount** (chặn duyệt refund cho nhiều vé cộng dồn vượt số tiền gốc).
4. Ghi bút toán đảo ngược tỉ lệ (`ratio = amountApproved / GrossAmount`) trên đúng 4 dòng đối xứng với bút toán gốc lúc xác nhận mua vé (§1.4.1): hoàn phí nền tảng, hoàn thuế, trừ lại phần giữ hộ Owner, hoàn qua Gateway.
5. **Co lại tương ứng mọi `Settlement` của payment đó chưa `Released`** (`NetAmount -= NetAmount × ratio`) — nếu không làm bước này, `SettlementReleaseJob` (§5.2) vẫn sẽ trả đủ cho Owner như thể vé chưa từng bị hoàn.
6. `Payment.Status → Refunded` **chỉ khi** tổng đã approve ≥ `GrossAmount` (1 payment có thể gánh nhiều vé, mỗi vé hoàn riêng lẻ, hoàn 1 phần chưa được coi là hoàn hết cả payment).

**Business rule ẩn quan trọng nhất domain này — giới hạn phạm vi của "Approve"**: bước Approve **chỉ đảo bút toán sổ cái nội bộ** (`ILedgerService.WriteJournalAsync`) — **không gọi API hoàn tiền thật của VNPay** ở đâu trong handler này. Tiền chỉ thật sự rời khỏi hệ thống nếu có 1 tích hợp/thao tác riêng biệt (ngoài phạm vi handler đã đọc) thực hiện lệnh hoàn qua VNPay dựa trên `RefundRequestStatus.Approved`. Comment trong code còn tự cảnh báo 1 giả định ẩn khác: bút toán trừ "phần giữ hộ Owner" giả định hoàn tiền luôn xảy ra **trước** khi settlement tranche đầu tiên release (đúng vì `CancellationDeadlineHours` luôn tính từ trước `ScheduledStart`, còn settlement sớm nhất là `showEnd+48h` — sau khi show đã kết thúc) — nếu chính sách huỷ vé sau này đổi để cho phép huỷ sau khi show kết thúc, bút toán này sẽ sai và cần trừ từ `AccountType.User` thay vì `AccountType.Platform` ([ProcessRefundRequestCommandHandler.cs](../src/MusicLounge.Application/Refunds/Commands/ProcessRefundRequest/ProcessRefundRequestCommandHandler.cs), comment ngay trên dòng ghi `refundOwnerNet`).

---

**Đã xác nhận — chấp nhận là giới hạn môi trường, không phải việc cần code**: `IVnPayService` (`src/MusicLounge.Application/Common/Interfaces/IVnPayService.cs`) không có method hoàn tiền nào — chỉ có `CreatePaymentUrl`/`VerifyCallback`/`QueryTransactionAsync` (querydr, cũng chưa từng chạy được thật vì IP server chưa được VNPay whitelist). **VNPay sandbox không cung cấp chức năng refund** — đây không phải lỗ hổng code cần vá, mà là giới hạn của môi trường sandbox dự án đang dùng (capstone, không có quan hệ merchant thật với VNPay). "Approve" refund request vì vậy **dừng đúng ở việc đảo bút toán sổ cái nội bộ** — đây là hành vi đúng/đủ trong phạm vi môi trường hiện tại, không phải việc còn dang dở. Hoàn tiền thật cho khán giả (nếu cần trong thực tế) là thao tác thủ công ngoài hệ thống.

---

## 5. Settlement / Báo cáo tài chính

### Danh sách use case con

1. Lên lịch settlement khi 1 payment vé được xác nhận (system-triggered)
2. Giải ngân tự động theo lịch (job định kỳ)
3. Owner xem báo cáo thu nhập (`GET /me/earnings`)
4. Owner/Admin xem thống kê venue/nền tảng (`Analytics`)
5. Admin kiểm tra toàn vẹn sổ cái (`ledger/integrity-check`)

---

### 5.1 Lên lịch settlement

**Actor**: hệ thống (domain event handler, kích hoạt tự động ngay sau §1.4.1).
**Trigger**: event `TicketPaymentConfirmed` → `ScheduleSettlementHandler.cs`.

**Luồng chính**:
1. Tính `ownerNet` bằng đúng công thức `PaymentFeeCalculator.Split` mà `WriteTicketLedgerHandler` đã dùng (bắt buộc khớp nhau, nếu không 2 tranche sẽ không cộng đúng bằng số tiền ledger ghi nhận).
2. Xác định tier tốc độ giải ngân qua `ResolveTierPreRateAsync` — điểm rating trung bình (loại review đã gỡ) + số show `Ended` → chọn `PreRateApplied` (50%/70%/80% theo doc 11 §2.4.1) — đồng thời **ghi ngược** giá trị điểm vào `MusicLounge.ReputationScore` (cache hiển thị).
3. Tạo 2 `Settlement`:
   - `Partial70`: `ScheduledAt = showEnd + SettlementPartialHoursAfterShow` (mặc định 48h), `NetAmount = ownerNet × PreRateApplied`.
   - `Final30`: `ScheduledAt = showEnd + SettlementFinalDaysAfterShow` (mặc định 14 ngày), `NetAmount = ownerNet − stage1Amount`.
4. Snapshot `PreRateApplied`/`PostRateApplied` tại đúng thời điểm tạo — đổi config sau này không ảnh hưởng ngược.

**Điều kiện tiên quyết chặn cả bước tạo**: venue phải đã đăng ký `BankAccount` mặc định — nếu chưa, ném lỗi ngay lúc này (không cho vé bán ra mà không có nơi nhận tiền) — [ScheduleSettlementHandler.cs:60-62](../src/MusicLounge.Application/Tickets/DomainEventHandlers/ScheduleSettlementHandler.cs#L60-L62).

**Tác động dữ liệu**: tạo 2 `Settlement`, cập nhật `MusicLounge.ReputationScore`.

### 5.2 Giải ngân tự động theo lịch

**Actor**: `SettlementReleaseJob` (Hangfire, chạy định kỳ — không phải người dùng).

**Luồng chính**:
1. Lấy mọi `Settlement.Status=Scheduled` có `ScheduledAt ≤ now`.
2. Với tranche `Final30`: kiểm tra `IsShowCompletionAcceptableAsync` — `thời lượng thực tế (ActualEnd−ActualStart) / thời lượng dự kiến (ScheduledEnd−ScheduledStart) ≥ SettlementCompletionThresholdPct` (mặc định 70%). Không đạt → `Status=PendingReview`, dừng lại, không giải ngân, log cảnh báo.
3. Đạt điều kiện (hoặc là tranche `Partial70`, không bị check này): ghi bút toán `Platform(debit) → User/Owner(credit)`, `Status=Released`, `ReleasedAt=now`, gửi `NotificationType.SettlementReleased`.
4. Lỗi ở 1 settlement không chặn các settlement khác trong cùng batch — giữ nguyên `Scheduled` để job lần sau tự retry.

**Business rule ẩn**: nếu show chưa từng được đánh dấu `ActualStart`/`ActualEnd` (chưa bấm Start/End — xem §2.2), job **coi như hợp lệ mặc định** (`return true`) thay vì chặn giải ngân vĩnh viễn — [SettlementReleaseJob.cs](../src/MusicLounge.Infrastructure/Jobs/SettlementReleaseJob.cs) (`IsShowCompletionAcceptableAsync`, nhánh `show.ActualStart is null`).

### 5.3 Owner xem báo cáo thu nhập

**Actor**: Owner (`RequireOwner`).
**Endpoint**: `GET /me/earnings` → `GetMyEarningsQueryHandler`.
**Luồng chính**: tổng hợp `Settlement`/`Payment`/`Donation` liên quan tới các venue của Owner hiện tại — đọc thuần, không ghi.

### 5.4 Thống kê venue / nền tảng

**Actor**: Owner (`GET /analytics/my-lounge?loungeId=`) hoặc khả năng Admin-only (`GET /analytics/platform` — chưa xác nhận phân quyền chính xác, xem doc 11 §11 câu #11 còn mở).

### 5.5 Kiểm tra toàn vẹn sổ cái

**Actor**: Admin.
**Endpoint**: `GET /admin/ledger/integrity-check`.
**Luồng chính**: quét `LedgerEntry`, kiểm tra `SUM(debit) = SUM(credit)` theo từng `JournalId` — công cụ vận hành xác minh nguyên tắc kế toán kép (D8) không bị vi phạm, không phải use case nghiệp vụ cho người dùng cuối.

**Tích hợp bên ngoài**: không có — settlement/ledger là nội bộ hoàn toàn, tiền chỉ thực sự rời hệ thống khi Owner tự rút từ tài khoản ngân hàng đã đăng ký (nằm ngoài phạm vi backend — backend chỉ ghi sổ "đã đến lượt Owner nhận", không tự động chuyển khoản qua API ngân hàng nào).

---

## 6. Auth & Tài khoản

### Danh sách use case con

1. Đăng ký tài khoản (local — email/password)
2. Xác thực email (kích hoạt tài khoản, nhận token đăng nhập lần đầu)
3. Gửi lại mã xác thực
4. Đăng nhập (local)
5. Đăng nhập bằng Google (tự đăng ký nếu chưa có tài khoản)
6. Quên mật khẩu / Đặt lại mật khẩu
7. Đổi mật khẩu (đã đăng nhập, biết mật khẩu cũ)
8. Đổi email (2 bước, xác minh OTP ở địa chỉ mới)
9. Xác thực số điện thoại (OTP)
10. Nộp hồ sơ KYC (CCCD/CMND)
11. Xuất dữ liệu cá nhân (DSAR — quyền truy cập/tính di động)
12. Xoá dữ liệu cá nhân (DSAR — quyền xoá, không thể hoàn tác)
13. Vô hiệu hoá tài khoản (tự nguyện, khôi phục được) / Admin khoá-mở tài khoản người khác

---

### 6.1 Đăng ký tài khoản (local)

**Actor**: Khách chưa đăng nhập (`AllowAnonymous`).
**Input**: `Email`, `Password`, `FullName`, `Phone?`, `Role` (chỉ `"Audience"`/`"Owner"`), `AcceptTerms`.
**Endpoint**: `POST /auth/register` → `RegisterCommandHandler.cs`.

**Luồng chính**:
1. Validator chặn `Role` ngoài `Audience`/`Owner` ngay từ tầng FluentValidation — không tới được handler nếu sai.
2. Kiểm tra email chưa tồn tại.
3. Sinh mã OTP 6 số, hash lưu (`EmailVerificationCodeHash`), hết hạn sau 10 phút.
4. Tạo `User` — **chưa cấp token**, `EmailVerifiedAt` vẫn `null`.
5. Ghi `TermsAcceptedAt`+`TermsVersion` (đọc từ `system_config.current_terms_version`) — validator đã chặn `AcceptTerms=false` từ trước, bước này chỉ ghi lại đã đồng ý phiên bản nào, lúc nào.
6. Enqueue gửi email chứa mã OTP qua background job.

**Luồng ngoại lệ**: email đã tồn tại → 409. `Role` ngoài whitelist → 400 (chặn ở validator).

**Business rule ẩn**: đây là nơi duy nhất áp policy "tự đăng ký chỉ được Audience/Owner" — [RegisterCommandValidator.cs:26-28](../src/MusicLounge.Application/Auth/Commands/Register/RegisterCommandValidator.cs#L26-L28). Không có API nào tạo Admin (xem doc 12 §6).

**Tác động dữ liệu**: tạo `User` (chưa active hoàn toàn — cần bước 6.2 mới đăng nhập được).

### 6.2 Xác thực email

**Actor**: chủ tài khoản mới đăng ký (chưa cần JWT — xác thực bằng Email+Code).
**Endpoint**: `POST /auth/verify-email` → `VerifyEmailCommandHandler.cs`.

**Luồng chính**:
1. Tài khoản đã xác thực rồi → chặn (409).
2. Kiểm tra khoá tạm (dùng chung cơ chế lockout với Login — sai mã OTP nhiều lần cũng bị khoá).
3. So khớp hash mã OTP, kiểm tra chưa hết hạn.
4. `EmailVerifiedAt=now`, xoá mã đã dùng.
5. **Cấp JWT ngay** (token đăng nhập lần đầu) — nếu là `Staff`, gắn kèm `lounge_id`.

**Luồng ngoại lệ**: mã sai (ghi nhận thất bại vào bộ đếm lockout), mã hết hạn, tài khoản đang bị khoá tạm do nhập sai nhiều lần.

**Business rule ẩn**: dùng **chung 1 bộ đếm lockout** (`IAuthAttemptTracker`) với Login — đoán mật khẩu sai và đoán mã OTP sai bị tính chung, không phải 2 giới hạn độc lập.

### 6.3 Gửi lại mã xác thực

**Endpoint**: `POST /auth/resend-verification-code`. **Luôn trả 204** bất kể email tồn tại/đã xác thực hay chưa — tránh lộ thông tin tài khoản (account enumeration).

### 6.4 Đăng nhập (local)

Đã truy vết chi tiết ở doc 11 §1 (3 lớp phòng vệ: lockout, chống timing side-channel, `LoginFailureLog` cho phát hiện tấn công hàng loạt) — không lặp lại ở đây.

### 6.5 Đăng nhập bằng Google

**Actor**: Khách chưa đăng nhập.
**Input**: `IdToken` (Google ID token, xác minh phía server qua `IGoogleTokenVerifier`), `AcceptTerms`.
**Endpoint**: `POST /auth/google` → `GoogleLoginCommandHandler.cs`.

**Luồng chính**:
1. Verify `IdToken` với Google → lấy `GoogleId`/`Email`/`FullName`/`AvatarUrl` đáng tin cậy.
2. Tìm `User` theo `GoogleId` — có thì đăng nhập thẳng.
3. Không có → tìm theo `Email`:
   - **Có tài khoản local trùng email**: liên kết Google vào tài khoản đó (`GoogleId` gán vào), `EmailVerifiedAt` được set nếu chưa có.
   - **Không có tài khoản nào**: tạo mới, `Role` mặc định `Audience` (entity default) — **Google sign-up không có tuỳ chọn Role, không thể trở thành Owner qua đường này**, khác với đăng ký local (được chọn Audience/Owner).
4. Cấp JWT.

**Business rule ẩn quan trọng — chống "Classic-Federated Merge Attack" (OWASP account pre-hijacking)**: khi liên kết vào tài khoản local **chưa từng xác thực email** (`EmailVerifiedAt` trước đó là `null`), handler **xoá luôn `PasswordHash` cũ** ([GoogleLoginCommandHandler.cs:44-60](../src/MusicLounge.Application/Auth/Commands/GoogleLogin/GoogleLoginCommandHandler.cs#L44-L60)). Lý do: nếu kẻ tấn công từng đăng ký sẵn email của nạn nhân bằng mật khẩu tự chọn (chờ nạn nhân đăng nhập Google sau), việc Google "bảo lãnh" email đó không được phép vô tình làm sống lại mật khẩu của kẻ tấn công — chủ thật đăng nhập qua Google ngay, muốn có mật khẩu thì tự đặt lại sau qua Forgot Password.

### 6.6 Quên mật khẩu / Đặt lại mật khẩu

**Bước 1 — `POST /auth/forgot-password`** (`ForgotPasswordCommandHandler.cs`): **luôn trả 204** bất kể email tồn tại hay không. Nếu tồn tại: sinh token ngẫu nhiên 32 byte (không phải OTP 6 số như các luồng khác), hash lưu, hết hạn 30 phút, gửi link qua **background job** (không await inline — tránh lộ thời gian phản hồi khác nhau giữa "có gửi mail" và "không gửi mail", cùng logic chống timing side-channel).

**Bước 2 — `POST /auth/reset-password`** (`ResetPasswordCommandHandler.cs`): so khớp hash token, còn hạn → đổi mật khẩu, **xoá token ngay** (dùng 1 lần), **xoay `SecurityStamp`** (thu hồi mọi JWT đang hiệu lực phát trước đó — chặn kịch bản kẻ tấn công đã chiếm được 1 JWT trước khi chủ tài khoản kịp đổi mật khẩu).

### 6.7 Đổi mật khẩu (đã đăng nhập)

**Endpoint**: `PUT /me/password` → `ChangePasswordCommandHandler.cs`. Yêu cầu đúng mật khẩu hiện tại; tài khoản Google-only (`PasswordHash=null`) bị chặn với thông báo rõ ràng "không có mật khẩu để đổi". Cũng xoay `SecurityStamp`.

### 6.8 Đổi email (2 bước)

**Bước 1 — `POST /me/email/change-request`** (`RequestChangeEmailCommandHandler.cs`): kiểm tra email mới chưa trùng ai, sinh OTP gửi **về địa chỉ MỚI** (không phải địa chỉ cũ) — đây chính là bằng chứng chủ tài khoản thật sự kiểm soát được hộp thư mới; lưu tạm ở `PendingEmail`, chưa đổi `Email` thật.

**Bước 2 — `POST /me/email/change-confirm`** (`ConfirmChangeEmailCommandHandler.cs`): so khớp OTP → `Email = PendingEmail`, xoá `PendingEmail`, xoay `SecurityStamp` (vì Email là 1 phần thông tin đăng nhập).

### 6.9 Xác thực số điện thoại

**Bước 1 — `POST /me/phone/verification-code`**: bắt buộc đã có `Phone` trong hồ sơ, chưa `PhoneVerified` trước đó — gửi OTP qua SMS.
**Bước 2 — `POST /me/phone/verify`**: so khớp OTP (dùng chung cơ chế lockout) → `PhoneVerified=true`.

### 6.10 Nộp hồ sơ KYC (CCCD/CMND)

**Endpoint**: `POST /me/citizen-card` → `SubmitCitizenCardCommandHandler.cs`.

**Luồng chính**:
1. Số CCCD/CMND được **mã hoá không xác định** (`IPiiEncryptionService`, non-deterministic) trước khi lưu `CitizenCardNumber` — nên **không thể** so sánh trực tiếp cột đã mã hoá để kiểm tra trùng; phải dùng `CitizenCardNumberHash` (hash xác định) riêng cho việc đối chiếu.
2. Chặn nếu số CCCD/CMND đã được tài khoản khác đăng ký trước (409).
3. 2 ảnh mặt trước/sau: chấp nhận 2 dạng input để tương thích ngược — nếu FE lỡ upload qua endpoint công khai `/uploads/images` (từng public trong khoảng thời gian ngắn giữa lúc upload và lúc gọi API này), handler **tự di chuyển file ra khỏi vùng public** (`RelocateToPrivateAsync`); nếu FE đã dùng endpoint riêng `/uploads/citizen-card-images` thì ảnh vốn đã private, không cần làm gì thêm.

**Business rule ẩn**: đây là ví dụ về khoảng hở bảo mật đã được vá bằng thiết kế tương thích ngược thay vì bắt buộc đổi API ngay — ảnh CCCD từng có thể bị truy cập công khai trong 1 khoảng ngắn nếu dùng đường upload cũ.

### 6.11 DSAR — Xuất dữ liệu cá nhân

**Endpoint**: `GET /me/data-export` → xử lý **đồng bộ, trả ngay** (không phải job nền) — đáp ứng nghĩa vụ xác nhận trong 2 ngày làm việc theo Luật 91/2025/QH15 ngay lập tức thay vì phải xếp hàng.

### 6.12 DSAR — Xoá dữ liệu cá nhân (không thể hoàn tác)

**Actor**: chủ tài khoản.
**Input**: `CurrentPassword?` (bắt buộc với tài khoản local, bỏ qua với tài khoản chỉ dùng Google).
**Endpoint**: `POST /me/data-erasure` → `RequestDataErasureCommandHandler.cs`.

**Luồng chính**:
1. Đã xoá trước đó (`DataErasedAt` đã set) → chặn (409), không xoá 2 lần.
2. Tài khoản local: bắt buộc xác nhận đúng mật khẩu hiện tại trước khi cho xoá (hành động không thể hoàn tác). Tài khoản Google-only: bỏ qua bước này — phiên đăng nhập hiện tại đã là bằng chứng kiểm soát.
3. **Xoá hẳn** dữ liệu hành vi/sở thích thuần tuý, không có nghĩa vụ lưu trữ pháp lý riêng: `Follow`, `ShowWishlist`, `UserFavouriteGenre/Mood/Atmosphere`, `UserCustomPreference`, `AiRecommendation`, `UserBehaviourLog`. (Cố ý **không đụng** `UserEventScore` — chỉ là cache điểm ML vô danh, không định danh ai, không đáng công sức xử lý riêng.)
4. **Ẩn danh hoá tại chỗ** (không hard-delete) `User` row: `Email→"deleted-user-{id}@musiclounge.local"`, `FullName→"Người dùng đã xóa"`, xoá `Phone`/`AvatarUrl`/`PasswordHash`/`GoogleId`/CCCD/... , `IsActive=false`, xoay `SecurityStamp` (thu hồi JWT đang hiệu lực ngay lập tức), `DataErasedAt=now`.
5. Các bảng có nghĩa vụ lưu trữ pháp lý (`Ticket`, `Payment`, `Settlement`, `LedgerEntry`, `Donation`...) **giữ nguyên FK trỏ về row đã ẩn danh** — không xoá, không đụng.

**Business rule ẩn quan trọng nhất**: đây chính là cách nền tảng dung hoà 2 nghĩa vụ pháp lý xung đột — Luật 91/2025/QH15 Điều 19 cho phép **từ chối xoá** khi luật chuyên ngành khác (ở đây là Luật Kế toán, yêu cầu lưu chứng từ 10 năm) không cho phép — nhưng thay vì chặn *toàn bộ* yêu cầu xoá (bất công với đa số Audience không hề có hồ sơ tài chính nào), handler tách nhỏ: ẩn danh phần định danh, giữ nguyên phần có nghĩa vụ lưu trữ, tham chiếu vẫn toàn vẹn.

### 6.13 Vô hiệu hoá tài khoản

**Tự nguyện** (`DELETE /me` → `DeactivateMyAccountCommandHandler.cs`): chỉ đơn giản `IsActive=false` — **khôi phục được** (khác hẳn §6.12, không xoá/ẩn danh gì), không yêu cầu xác nhận mật khẩu.
**Admin thực hiện với tài khoản khác** (`POST /admin/users/{id}/deactivate` / `.../reactivate`): cùng field `IsActive`, đảo chiều được bởi Admin.

---

## 7. Venue/Lounge

### Danh sách use case con

1. Tạo venue mới (Owner)
2. Admin duyệt/từ chối venue mới
3. Cập nhật thông tin venue (ảnh đại diện, giấy phép kinh doanh, model 3D)
4. Gán / gỡ Staff cho venue
5. Quản lý khu vực chỗ ngồi (Seating Zone) + layout 2D/3D
6. Xử phạt venue + kháng cáo (+ tự động approve nếu Admin không xử lý kịp)
7. Tour ảo 360° — thêm scene thủ công / ghép ảnh tự động qua microservice / gắn hotspot
8. Gallery ảnh venue

---

### 7.1 Tạo venue mới

**Actor**: Owner (`RequireOwner`).
**Endpoint**: `POST /lounges` → `CreateLoungeCommandHandler.cs`.
**Luồng chính**: tạo `MusicLounge` với `OwnerId=currentUser`, `Status` mặc định `Pending` (không set tường minh, dùng default của entity). Không có bước duyệt nào chặn tại đây — venue **tồn tại ngay** nhưng chưa vận hành được show mới cho tới khi qua §7.2.
**Tác động dữ liệu**: tạo `MusicLounge`.

### 7.2 Admin duyệt/từ chối venue mới

Đã truy vết đầy đủ ở doc 11 §2.2 (BR-01) — `POST /admin/lounges/{id}/approve`/`.../reject`, chặn nộp duyệt show khi venue còn `Pending`/`Rejected`. Không lặp lại ở đây.

### 7.3 Cập nhật thông tin venue

**Actor**: Owner của venue (hoặc Admin).
**Endpoint**: `PUT /lounges/{id}/image`, `.../business-license`, `.../model-3d`, `.../area-layout-image` — mỗi endpoint set 1 field chuỗi URL riêng lẻ trên `MusicLounge` (`PrimaryImageUrl`/`BusinessLicenseUrl`/`Model3DUrl`/`AreaLayoutImageUrl`), ảnh phải upload trước qua `POST /uploads/images` rồi truyền URL vào đây.
**Business rule ẩn**: `SetLoungeImage` chỉ set `PrimaryImageUrl` — **không** ghi vào bảng `LoungeImage` (đã xác nhận là bảng chết, xem [13-data-model.md §4.1](13-data-model.md#41-có-trong-schema-nhưng-không-dùng-ở-code-nghiệp-vụ)).

### 7.4 Gán / gỡ Staff cho venue

**Actor**: Owner của venue.
**Endpoint**: `POST /lounges/{id}/staff` → `AssignStaffCommandHandler.cs`.

**Luồng chính**:
1. Chỉ Owner của venue đó được gán.
2. **Chặn nếu User đang giữ Role `Owner`/`Admin`** (`ConflictException`, [AssignStaffCommandHandler.cs:38-47](../src/MusicLounge.Application/Staffing/Commands/AssignStaff/AssignStaffCommandHandler.cs#L38-L47)) — vá sau khi rà soát: nếu cho phép, `LoungeStaff` row tạo ra sẽ **vô hại về quyền truy cập** (JWT của họ không bao giờ đổi thành `Staff` nên `VenueOperatorAccess` không cấp thêm quyền gì) nhưng **sai lệch dữ liệu thật** — họ vẫn hiện trong danh sách staff của venue dù thực chất không vận hành được gì ở đó. Đây là gap toàn vẹn dữ liệu tự mâu thuẫn với comment gốc của chính handler ("mỗi venue cần một tài khoản staff riêng"), **không phải lỗ hổng bảo mật/leo thang quyền**.
3. Chặn nếu User đã là staff **đang active** của chính venue này.
4. **Chặn nếu User đang active làm staff ở venue khác** — thông báo rõ "Mỗi venue cần một tài khoản staff riêng, hãy tạo tài khoản mới" (kiểm tra tường minh ở tầng Application, không chỉ dựa vào unique filtered index ở DB như lớp phòng vệ cuối).
5. **Chỉ tự động đổi `Role` nếu User hiện đang `Audience`** — `Role: Audience → Staff`. (Sau bước 2, chỉ còn `Audience` hoặc `Staff` có thể tới được đây — `Owner`/`Admin` đã bị chặn từ đầu.)
6. Tạo `LoungeStaff(IsActive=true)`.

**Endpoint gỡ**: `DELETE /lounges/{id}/staff/{staffId}` → set `LoungeStaff.IsActive=false`, `DeactivatedAt` — không xoá row, chỉ khoá quyền vận hành (User giữ nguyên `Role=Staff` trong hệ thống, chỉ mất quyền venue cụ thể).

### 7.5 Quản lý khu vực chỗ ngồi (Seating Zone)

**Actor**: Owner của venue (hoặc Admin).
**Endpoint**: `POST /lounges/{id}/zones` → `CreateSeatingZoneCommandHandler.cs` — đơn giản, `DisplayOrder` tự tăng theo số zone đã có. Sửa layout 2D/3D qua `PUT .../layout-2d`/`.../layout-3d` riêng biệt (toạ độ %, độc lập với việc tạo zone).
**Tác động dữ liệu**: tạo/cập nhật `SeatingZone` — đây chính là nguồn giới hạn sức chứa vật lý dùng ở luồng giữ chỗ vé (doc 14 §1.1).

### 7.6 Xử phạt venue + kháng cáo

Đã truy vết đầy đủ ở doc 11 §2.3 (`IssuePenaltyCommandHandler`, 3 mức Warning/Suspension/Ban). Bổ sung chi tiết `ReviewAppealCommandHandler.cs` (Admin xử lý kháng cáo) chưa có ở doc 11:

1. Khoá theo `appeal-review:{penaltyId}` — **cùng key với `AutoApproveOverdueAppealsJob`**, tránh Admin quyết định đúng lúc job tự động cũng đang chạy ở ngưỡng SLA, gây quyết định mâu thuẫn.
2. Chỉ xử lý được kháng cáo đang `Appealed`.
3. **`Overturned` không tự động mở lại venue nếu venue còn ≥1 hình phạt Suspension/Ban khác đang `Active`** — 1 venue có thể bị phạt nhiều lần chồng lên nhau (vi phạm thứ 2 trong lúc đang chịu phạt vi phạm thứ 1); chỉ mở lại `Approved` khi không còn hình phạt nào khác đang giữ trạng thái khoá.
4. **Nếu hình phạt đã áp dụng hậu quả tài chính rồi** (gia hạn/co ngắn subscription qua `ApplyDuePenaltiesJob`) mới được overturn: hệ thống **không tự động hoàn tác** khoản tài chính đó — chỉ gửi thông báo cho **mọi Admin** yêu cầu tự kiểm tra và điều chỉnh `owner_subscriptions`/ledger thủ công. Đây là 1 quyết định thiết kế có chủ đích (tránh đoán sai chiều hoàn tiền) chứ không phải thiếu sót.

### 7.7 Tour ảo 360°

**3 cách thêm 1 scene**:
- **Thủ công** (`POST /lounges/{id}/tour/scenes` → `AddVenueTourSceneCommandHandler.cs`): Owner đã có sẵn 1 ảnh panorama hoàn chỉnh (chụp bằng app 360 chuyên dụng) — chỉ cần đăng ký URL.
- **Ghép ảnh tự động** (`POST /lounges/{id}/tour/scenes/stitch` → `StitchVenueTourSceneCommandHandler.cs`): Owner có nhiều ảnh chụp xoay vòng, không có app 360 — gửi lên để microservice Python ghép lại. **Chạy nền, không đồng bộ**: tạo `VenueTourStitchAttempt(Status=Pending)` ngay lập tức để khoá quota/anti-abuse trước, trả về `attemptId`, Owner tự poll `GET /lounges/{id}/tour/scenes/stitch/{attemptId}` để biết kết quả — vì 1 lần ghép có thể mất 15-30+ giây, đôi khi chạm ngưỡng timeout 120s của HttpClient gọi microservice, không thể giữ request HTTP gốc chờ suốt thời gian đó.
- **Gắn hotspot** (`POST /lounges/{id}/tour/scenes/{sceneId}/hotspots`): thêm điểm điều hướng/thông tin/màn hình livestream vào 1 scene đã có.

**2 lớp giới hạn cho cách ghép ảnh tự động (khác nhau về bản chất)**:
1. `MaxTourScenesSnapshot` (theo gói subscription đang active) — giới hạn **kết quả cuối** (bao nhiêu scene thật sự có trong tour).
2. `tour_stitch_max_attempts_per_lounge` (system_config, mặc định 20) — giới hạn **số lần thử**, tính cả `Pending` (không chỉ thành công/thất bại) để chặn 1 đợt request đồng thời đều lách qua giới hạn trước khi cái nào xử lý xong — vì ghép ảnh chạy trên CPU máy chủ của chính nền tảng (không phải vendor AI trả phí ngoài), 1 vòng lặp thử lại vô hạn là chi phí/DoS trực tiếp lên hạ tầng của chính mình, khác hẳn lý do giới hạn AI poster (doc 11 §3, chi phí vendor ngoài).

### 7.8 Gallery ảnh venue

**Actor**: Owner của venue.
**Endpoint**: `POST /lounges/{id}/gallery` / `DELETE .../gallery/{imageId}` — tạo/xoá `LoungeGalleryImage`, miễn phí cho mọi Owner (không gate theo subscription, khác tour 360°).

---

## 8. Show/Event (tạo, sửa, nộp duyệt, kiểm duyệt)

> Vòng đời trạng thái, kiểm duyệt AI+Admin, và AI Poster đã truy vết chi tiết ở [11-ba-domain-analysis.md §3](11-ba-domain-analysis.md#3-domain-chương-trình-biểu-diễn-showevent) — không lặp lại, chỉ bổ sung phần **tạo mới** và **đánh giá** chưa có ở đó, cùng 1 business rule D14 quan trọng chưa từng nhắc tới.

### Danh sách use case con

1. Tạo show mới (Draft) — **yêu cầu Owner đang có subscription Active**
2. Tạo hạng vé (TicketTier) cho show — chỉ khi show còn Draft
3. Nộp duyệt / Admin kiểm duyệt — *xem doc 11 §3*
4. AI Poster generation — *xem doc 11 §3*
5. Đổi lịch show (Reschedule)
6. Đổi định dạng show (Offline → Online, 1 chiều)
7. Đổi chế độ phát 2D/3D
8. Đánh giá show (Audience, sau khi kết thúc, có cửa sổ 7 ngày)

---

### 8.1 Tạo show mới

**Actor**: Owner (venue của mình).
**Endpoint**: `POST /lounge-shows` → `CreateLoungeShowCommandHandler.cs`.

**Luồng chính**:
1. **Bắt buộc Owner đang có `OwnerSubscription.Status=Active` và chưa hết hạn tại đúng thời điểm TẠO show** (D14) — không phải lúc publish. Show đã `Published` trước đó **không bị ảnh hưởng ngược** nếu subscription hết hạn sau này (chỉ chặn việc tạo mới).
2. Tạo `LoungeShow(Status=Draft)`.
3. Với mỗi lượt diễn trong request: nếu truyền `PerformerId` → dùng Performer có sẵn; nếu không (chỉ truyền tên) → **tạo mới `Performer` ngay tại đây** (`CreatedByUserId=Owner hiện tại`) — đây chính là con đường phổ biến nhất khiến catalog Performer có dữ liệu (không chỉ qua `PerformersController` riêng).
4. Tạo `Performance` cho từng lượt diễn (Role/OrderIndex/SetTime/AcceptsDonation theo từng lượt, không phải theo Performer — nhắc lại từ doc 11 §3).
5. Gắn `LoungeShowGenre` theo danh sách genre chọn, gắn `EventCustomValue` theo tiêu chí riêng của venue (nếu có).

**Luồng ngoại lệ**: không phải Owner của venue → 403. Không có subscription Active → chặn tạo, thông báo rõ cần đăng ký gói.

**Tác động dữ liệu**: tạo `LoungeShow`, có thể tạo `Performer` mới, tạo N `Performance`, N `LoungeShowGenre`, N `EventCustomValue`.

### 8.2 Tạo hạng vé (TicketTier) cho show

**Actor**: Owner (venue của show đó).
**Endpoint**: `POST /lounge-shows/{id}/ticket-tiers` (qua module `TicketTiers`) → `CreateTicketTierCommandHandler.cs`.

**Luồng chính**:
1. **Chỉ tạo được khi show còn `Draft`** — chặn sửa cấu trúc giá vé sau khi đã nộp duyệt/publish.
2. **D14 — kiểm tra tổng `TotalCapacity` của MỌI tier đã có cộng thêm tier mới không vượt `MaxTicketsPerEventSnapshot`** của subscription đang active (nếu có) — đây là bước kiểm tra sớm, cùng bản chất với bước kiểm tra lại ở lúc giữ chỗ vé (doc 14 §1.1) nhưng ở **thời điểm khác** (lúc tạo tier vs. lúc bán) — 2 lớp phòng vệ độc lập cho cùng 1 giới hạn.
3. `AccessType=Physical` mới được gắn `ZoneId`; `Livestream` thì `ZoneId` luôn bị ép `null` bất kể request truyền gì.
4. Tạo kèm N `TicketPrice` (đợt bán giá) ngay trong cùng 1 lệnh.

**Tác động dữ liệu**: tạo `TicketTier`, tạo N `TicketPrice`.

### 8.3 Đổi lịch show (Reschedule)

**Actor**: Owner của venue.
**Endpoint**: `PUT /lounge-shows/{id}/reschedule` → `RescheduleLoungeShowCommandHandler.cs`.

**Luồng chính**:
1. Chỉ đổi lịch được khi show đang `Published` (chưa `Ongoing`/`Ended`/`Cancelled` — cố ý loại `Ongoing`, vì show đã `ActualStart` thật mà đổi `ScheduledStart` sang tương lai tạo trạng thái mâu thuẫn "đang diễn ra nhưng lịch bắt đầu nằm ở tương lai").
2. **Áp lại đúng quy tắc 7 ngày làm việc tối thiểu (NĐ 144/2020 Điều 10) cho ngày diễn MỚI** — chặn lỗ hổng "publish đúng hạn rồi đổi lịch gấp sang ngày mai".
3. Dịch cả `ScheduledStart` và `ScheduledEnd` theo cùng 1 khoảng chênh lệch.
4. **Ép `CancellationAllowed=true`** — đảm bảo khán giả không đồng ý được lịch mới vẫn huỷ vé lấy lại tiền được (deadline huỷ tính lại theo `ScheduledStart` mới).
5. Thông báo mọi chủ vé `Confirmed` — dùng loại thông báo `EventRescheduled` (không dùng `EventReminder` — code tự ghi chú đây là fix cho lỗi cũ: dùng trùng loại thông báo khiến `EventReminderJob` tưởng nhầm buyer "đã được nhắc" rồi âm thầm không gửi nhắc lịch thật trước giờ diễn mới).

### 8.4 Đổi định dạng show (Offline → Online)

**Actor**: Owner của venue.
**Endpoint**: `PUT /lounge-shows/{id}/format` → `ChangeLoungeShowFormatCommandHandler.cs`.

**Luồng chính**:
1. Khoá `show-status-change:{showId}` — **cùng key với Hủy show** (doc 14 §4.2) và Reschedule — chống double-click lẫn race giữa các thao tác đổi trạng thái show khác nhau trên cùng show.
2. Show phải `Published`/`Ongoing`. **Chỉ hỗ trợ đúng 1 chiều: `Offline → Online`** — mọi chiều khác (kể cả Online→Offline) bị chặn thẳng.
3. Với **mọi vé vật lý đã `Confirmed`**: huỷ vé, tạo `RefundRequest(RefundPercentage=100)` — hoàn 100% bất kể `show.RefundPercentage`, giống mẫu hình đã thấy ở Hủy show/Take-down (doc 14 §4.2/§4.3) — đây là **lần xuất hiện thứ 3** của đúng 1 pattern "venue thay đổi kế hoạch → khán giả không chịu thiệt".

**Business rule ẩn**: chỉ đổi được Offline→Online, không có lệnh nào cho chiều ngược lại — thay đổi định dạng show là hành động 1 chiều, có giới hạn cố ý (`LoungeShowFormat` chỉ có 2 giá trị, không có Hybrid — 1 show không bao giờ bán đồng thời cả 2 loại vé).

### 8.5 Đổi chế độ phát 2D/3D

**Actor**: Owner của venue (hoặc Admin).
**Endpoint**: `PUT /lounge-shows/{id}/playback-mode` → `SetPlaybackModeCommandHandler.cs`.

**Trả lời dứt điểm câu hỏi mở #5 ở doc 11**: `PlaybackMode=ThreeD` **hoàn toàn không gate theo subscription** — chỉ có đúng 1 điều kiện: `show.Format != Offline` ("Chỉ show Online mới có thể phát dạng 3D"). Không có kiểm tra gói/quota nào khác. Không đổi được khi show đã `Ended`/`Cancelled`.

### 8.6 Đánh giá show (Rating)

**Actor**: Audience đã có vé.
**Endpoint**: `POST /lounge-shows/{id}/rate` → `RateShowCommandHandler.cs`.

**Luồng chính**:
1. Show phải đã `Ended`.
2. **Trong cửa sổ `RatingOpenUntil`** (§6.13, `ActualEnd + 7 ngày`, xem doc 14 §2.2.1) — quá hạn thì chặn. Show cũ (được tạo trước khi field `RatingOpenUntil` tồn tại) có giá trị `null` → **không** bị chặn hồi tố (không tự suy ra giới hạn cho dữ liệu cũ không có mốc thời gian thật).
3. Phải có vé `Confirmed` hoặc `Used` cho chính show này — không phải chỉ cần đăng nhập là đánh giá được.
4. Mỗi user chỉ đánh giá 1 lần / show (409 nếu đã đánh giá).

**Tác động dữ liệu**: tạo `LoungeShowRating`, sau này được `ScheduleSettlementHandler` đọc để tính tier tốc độ giải ngân của venue (doc 11 §2.4.1).

---

## 9. F&B

### Danh sách use case con

1. Quản lý menu/món ăn (Owner) — *đơn giản, không truy vết riêng*
2. Đặt đơn F&B (Audience tự đặt / Staff đặt hộ khách tại quầy)
3. Cập nhật trạng thái đơn (Staff/Owner/Admin, vận hành bếp/quầy)
4. Hủy đơn

---

### 9.1 Đặt đơn F&B

**Actor**: Audience (tự đặt) hoặc Staff/Owner/Admin của venue (đặt hộ khách tại quầy).
**Endpoint**: `POST /fnb-orders` → `CreateFnbOrderCommandHandler.cs`.

**Luồng chính**:
1. Xác định vai trò đặt đơn: nếu `Role ∈ {Staff, Owner, Admin}` → bắt buộc `VenueOperatorAccess.CanOperate` đúng venue, ghi `StaffId`; ngược lại → ghi `AudienceUserId` (khách tự đặt qua app).
2. Nếu có `ZoneId`/`ShowId` truyền kèm: **kiểm tra thuộc đúng venue này** — không cho trỏ sang zone/show của venue khác (validate chặt hơn `MenuItemId`, vốn đã luôn được kiểm tra).
3. Với từng món: phải thuộc đúng menu của venue này, phải `IsAvailable=true`.
4. Tạo `FnbOrder(Status=Pending)`, sau đó tạo N `OrderItem` (`UnitPrice` chốt ngay từ giá menu hiện tại), cộng dồn `TotalAmount`.

**Luồng ngoại lệ**: món/zone/show không thuộc venue → chặn; món hết hàng (`IsAvailable=false`) → chặn.

**Tác động dữ liệu**: tạo `FnbOrder`, tạo N `OrderItem`.

### 9.2 Cập nhật trạng thái đơn

**Actor**: Staff/Owner/Admin của venue.
**Endpoint**: `PUT /fnb-orders/{id}/status` → `UpdateFnbOrderStatusCommandHandler.cs`.

**Luồng chính**:
1. **Chuỗi tuần tự bắt buộc, không được nhảy cóc**: `Pending → Preparing → Served → Paid` — chỉ chấp nhận đúng bước kế tiếp trong mảng cố định, chuyển sai thứ tự (kể cả lùi) đều bị chặn.
2. **`Cancelled` là lối thoát riêng**, không nằm trong chuỗi tuần tự — được phép từ **bất kỳ trạng thái nào trước `Paid`** (không phải chỉ từ `Pending`). Khi huỷ: đơn `→Cancelled`, mọi `OrderItem` đánh dấu `Cancelled=true`.
3. **Khi chuyển sang `Paid`**: tự động tạo 1 `Payment` ghi nhận (`Method` lấy từ `order.PaymentMethod`, `Status=Confirmed` ngay) — **thuần mục đích kiểm toán nội bộ** ("ai đánh dấu đơn nào đã thanh toán, bao nhiêu tiền, lúc nào"), **không** đưa vào pipeline ledger/settlement (F&B không phải sản phẩm có hoa hồng nền tảng, giống lý do walk-in ticket mặc định không tính hoa hồng — doc 11 §4).

**Business rule ẩn**: trước khi có `Payment` ghi nhận ở bước 3, đánh dấu "Paid" chỉ là 1 field trạng thái đơn thuần — Staff có thể đánh dấu đã thu tiền mà không thực thu (làm sai lệch báo cáo doanh thu F&B), hoặc thu tiền thật mà không đánh dấu (rút ruột không dấu vết). Payment ghi nhận này đóng vai trò bằng chứng đối soát, dù không chảy vào sổ cái kép.

**Tác động dữ liệu**: cập nhật `FnbOrder.Status`, có thể cập nhật N `OrderItem.Cancelled`, có thể tạo `Payment` (không liên kết ledger).

---

## 10. Recommendation/Analytics

### Danh sách use case con

1. Xem show được gợi ý (Audience) — 3 tầng chiến lược tuỳ dữ liệu sẵn có, gate theo `AiConsent`
2. Ghi nhận hành vi (behaviour log) — *side-effect rải rác trong nhiều domain khác, không lặp lại*
3. Owner xem thống kê venue mình
4. Admin xem thống kê toàn nền tảng

---

### 10.1 Xem show được gợi ý — trả lời dứt điểm công thức `FinalScore` (câu hỏi mở #10 doc 11)

**Actor**: Audience.
**Endpoint**: `GET /recommendations` → `GetRecommendedLoungeShowsQueryHandler.cs`, tính toán thật nằm ở `MLNetRecommendationService.cs`.

**Luồng chính (đọc cache trước, không tính live trong request)**:
1. **Cổng đồng ý AI**: `User.AiConsent = false` → bỏ qua toàn bộ cá nhân hoá, trả thẳng danh sách **Trending** (show đang thịnh hành, không định danh hành vi cá nhân).
2. Đọc `AiRecommendation` đã cache cho user này, lọc còn hạn (`ExpiresAt > now`, TTL 6 giờ).
3. **Cache rỗng/hết hạn** → enqueue job `RefreshUserRecommendationJob` chạy nền cho lần sau, **nhưng request hiện tại vẫn trả Trending ngay** (không bắt user chờ tính toán).
4. Có cache hợp lệ → sắp theo `FinalScore` giảm dần, trả kèm `Reason`.

**3 tầng chiến lược tính toán** (chạy trong job nền, không phải lúc user gọi API) — `MLNetRecommendationService`:

| Tầng | Điều kiện kích hoạt | Công thức |
|---|---|---|
| Trending | Không đủ điều kiện 2 tầng dưới (chưa follow venue nào, chưa có sở thích genre/mood/atmosphere nào) | Không tính `AiRecommendation`, dùng thẳng danh sách show thịnh hành tại thời điểm query |
| Content-based | Có sở thích nội dung (genre/mood/atmosphere) hoặc đang follow venue, nhưng **`UserBehaviourLog` < 5 dòng** | `FinalScore = ContentScore + (0.15 nếu venue của show đang được follow)` |
| Hybrid | `UserBehaviourLog` ≥ 5 dòng | **`FinalScore = ContentScore×0.5 + CollabScore×0.3 + CustomScore×0.2 + (0.15 nếu follow venue)`** ([MLNetRecommendationService.cs:179-180](../src/MusicLounge.Infrastructure/Services/MLNetRecommendationService.cs#L179-L180)) |

**Business rule ẩn**: lọc cộng tác (`CollabScore`) chỉ tham gia công thức khi user đã có **đủ dữ liệu hành vi tối thiểu (5 dòng log)** — dưới ngưỡng đó, hệ thống tự nhận không đủ tín hiệu để chạy collaborative filtering, chỉ dùng content-based. `0.15` cộng thêm cho venue đang follow là hằng số cố định (`FollowedVenueBoost`), không qua system_config.

### 10.2 Owner xem thống kê venue mình / Admin xem thống kê toàn nền tảng

Trả lời dứt điểm câu hỏi mở #11 doc 11 — 2 endpoint tách biệt hoàn toàn theo policy, không phải cùng 1 endpoint với check quyền bên trong:

| Endpoint | Policy | Actor |
|---|---|---|
| `GET /analytics/my-lounge?loungeId=` | `RequireOwner` | Owner (venue của chính mình) hoặc Admin |
| `GET /analytics/platform` | `RequireAdmin` | **Chỉ Admin** — Owner gọi endpoint này bị 403 ngay ở tầng `[Authorize]`, không tới được handler |

---

## 11. Notification

### Danh sách use case con

1. Cơ chế phát thông báo dùng chung (`NotifyAsync`) — nền tảng cho **mọi** notification trong toàn hệ thống
2. Xem danh sách / đánh dấu đã đọc (1 cái / tất cả)
3. Đăng ký / hủy đăng ký thiết bị nhận push (FCM)
4. Cảnh báo vận hành nội bộ gửi tới Admin (trả lời dứt điểm câu hỏi mở #12 doc 11)

---

### 11.1 Cơ chế phát thông báo dùng chung

**Không phải 1 use case người dùng gọi trực tiếp** — mọi nơi trong hệ thống (đã xuất hiện xuyên suốt tài liệu này: xác nhận vé, donate, phạt venue, duyệt show...) đều gọi qua đúng 1 điểm: `NotificationService.NotifyAsync` ([NotificationService.cs](../src/MusicLounge.Application/Common/Services/NotificationService.cs)).

**Luồng chính, mỗi lần gọi**:
1. Ghi (`Add`, chưa `SaveChanges`) 1 row `Notification` (in-app) — **không tự commit riêng**, "ăn theo" transaction của chính hành động nghiệp vụ đang gọi nó (giống cách `ILedgerService.WriteJournalAsync` hoạt động) — đảm bảo thông báo không bao giờ tồn tại nếu hành động gốc bị rollback.
2. Enqueue **ngay lập tức** 1 job nền gửi push FCM (`EnqueueFcmNotification`) — không đợi transaction cha commit xong mới enqueue.

**Bên trong job gửi FCM** (`FcmService.SendAsync`) — **không bao giờ throw ra ngoài**:
- Provider chưa cấu hình (thiếu Firebase credentials) → chỉ log cảnh báo, **thông báo in-app vẫn đã được ghi ở bước 1** (2 kênh độc lập, kênh này lỗi không ảnh hưởng kênh kia).
- User chưa đăng ký `DeviceToken` nào → bỏ qua, log info.
- Gửi thất bại ở tầng batch → bắt lỗi rộng, không để job tự crash/retry vô ích cho lỗi cấu hình.
- Lỗi per-device (token hết hạn...) → không throw, trả về danh sách kết quả từng device; lỗi "nghiêm trọng" (không phải do 1 device chết tự nhiên) mới được ghi vào `PushFailureLog` cho `PushFailureAlertJob` theo dõi (doc 11 §9).

### 11.2 Xem / đánh dấu đã đọc

**Endpoint**: `GET /notifications`, `POST /notifications/{id}/read`, `POST /notifications/read-all` — thao tác đơn giản trên `Notification.IsRead`, chỉ đọc/sửa thông báo của chính user.

### 11.3 Đăng ký / hủy thiết bị nhận push

**Endpoint**: `POST /notifications/device-tokens` → `RegisterDeviceTokenCommandHandler.cs`.

**Business rule ẩn**: `Fid` (Firebase Installation ID) là duy nhất theo **thiết bị vật lý**, không theo user — nếu `Fid` này từng đăng ký dưới 1 tài khoản khác (dùng chung máy, đăng xuất/đăng nhập tài khoản khác), handler **re-point** `DeviceToken.UserId` sang user hiện tại thay vì tạo row mới hay từ chối — tránh 1 thiết bị dùng chung tiếp tục nhận push cho tài khoản đã đăng xuất.

### 11.4 Cảnh báo vận hành nội bộ gửi tới Admin — trả lời dứt điểm câu hỏi mở #12 doc 11

**Đã kiểm tra trực tiếp cả 4 job phát các loại `NotificationType` vận hành** (`SecurityAlert`, `SystemHealthAlert`, `PaymentReconciliationMismatch`, và tương tự): **tất cả cùng 1 khuôn mẫu — gửi tới MỌI Admin đang tồn tại**, không phải 1 Admin cố định:

```
var admins = await _ctx.Users.Where(u => u.Role == UserRole.Admin).ToListAsync(ct);
foreach (var admin in admins)
    await _notifications.NotifyAsync(admin.Id, ...);
```

Xác nhận tại: `AdminRoleDriftDetectionJob.cs:33-70` (`SecurityAlert` khi phát hiện Admin lạ), `LoginSpikeDetectionJob.cs:58-70` (`SecurityAlert` khi dò mật khẩu hàng loạt), `PushFailureAlertJob.cs:56-64` (`SystemHealthAlert`), `VnPayReconciliationJob.cs:88-91` (`PaymentReconciliationMismatch`).

**Ý nghĩa nghiệp vụ**: không có khái niệm "Admin trực" hay phân công theo ca — cảnh báo bảo mật/vận hành được xem là trách nhiệm chung của toàn bộ đội Admin, ai cũng nhận được đồng thời.

---

## 12. Follow/Wishlist

Domain đơn giản nhất trong toàn bộ tài liệu — không có business rule ẩn phức tạp, không tích hợp bên ngoài. Chỉ 1 khác biệt đáng ghi nhận giữa 2 use case:

### 12.1 Follow / Unfollow venue

**Actor**: Audience.
**Endpoint**: `POST`/`DELETE /follows/lounges/{loungeId}` → `FollowLoungeCommandHandler.cs`/`UnfollowLoungeCommandHandler.cs`.
**Luồng chính**: chỉ kiểm tra `MusicLounge` tồn tại (**không** kiểm tra `Status`) và chưa follow trước đó (409 nếu đã follow) → tạo/xoá `Follow`.
**Business rule ẩn**: có thể follow được cả venue đang `Pending`/`Warned`/`Suspended` — không có gate theo trạng thái venue, khác hẳn Wishlist ở §12.2.

### 12.2 Add / Remove Wishlist

**Actor**: Audience.
**Endpoint**: `POST`/`DELETE /wishlist/{showId}` → `AddToWishlistCommandHandler.cs`/`RemoveFromWishlistCommandHandler.cs`.
**Luồng chính**: kiểm tra `LoungeShow` tồn tại **và** `Status ∉ {Draft, Cancelled}` — không wishlist được show chưa công khai hoặc đã huỷ — rồi mới kiểm tra chưa wishlist trước đó → tạo/xoá `ShowWishlist`.

**Tác động dữ liệu**: tạo/xoá `Follow` hoặc `ShowWishlist`. Cả 2 đều là input cho gợi ý cá nhân hoá (doc 14 §10) và trigger thông báo (`NewEvent` khi venue follow có show mới, `WishlistLowStock` khi vé show wishlist sắp hết — cả 2 đã truy vết ở domain liên quan).

---

*Tài liệu này (docs/11 → 14) cùng nhau tạo thành bộ tài liệu bàn giao/phân tích nghiệp vụ hoàn chỉnh của MusicLounge Backend, dựng từ code thực tế thay vì tài liệu SRS gốc. Xem [11-ba-domain-analysis.md](11-ba-domain-analysis.md) cho tổng quan domain, [13-data-model.md](13-data-model.md) cho entity/quan hệ, [12-actors-and-authorization.md](12-actors-and-authorization.md) cho actor/quyền hạn.*
