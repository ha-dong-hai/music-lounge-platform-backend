# 15 — Rủi ro phi chức năng & Nghiệp vụ (Risk Audit)

← [11-ba-domain-analysis.md](11-ba-domain-analysis.md) · [12-actors-and-authorization.md](12-actors-and-authorization.md) · [13-data-model.md](13-data-model.md) · [14-usecase-traces.md](14-usecase-traces.md)

> **Phạm vi**: rà soát các yếu tố phi chức năng có ảnh hưởng nghiệp vụ — không đi sâu performance/kỹ thuật thuần túy. 4 trục: (1) tính nhất quán giao dịch tiền (transaction/rollback), (2) validate dữ liệu đầu vào, (3) TODO/FIXME/hardcode/tính năng dang dở, (4) lỗ hổng phân quyền.
> **Phương pháp**: 4 lượt rà độc lập + 1 lượt re-check toàn bộ, mỗi lượt đọc trực tiếp code (không suy đoán). Nhiều finding đã được sửa **ngay trong lúc audit đang diễn ra** (working tree thay đổi song song) — mỗi mục dưới đây đã được xác minh lại lần cuối tại thời điểm ghi.
> **Cập nhật lần cuối**: 2026-08-13, dựa trên working tree hiện tại.

## Mục lục

1. [Tóm tắt](#1-tóm-tắt)
2. [Đã tìm & đã sửa](#2-đã-tìm--đã-sửa) — 16 mục
3. [Đối chứng tích cực](#3-đối-chứng-tích-cực) — phần đã làm tốt sẵn, không phải finding
4. [Ghi chú thực trạng khác biệt với code](#4-ghi-chú-thực-trạng-khác-biệt-với-code)
5. [Quy trình rà soát — để tái sử dụng sau này](#5-quy-trình-rà-soát--để-tái-sử-dụng-sau-này)

---

## 1. Tóm tắt

Qua 4 lượt rà độc lập (mỗi lượt đào sâu thêm 1 tầng: luồng vé/VNPay cốt lõi → luồng Subscription/Donation/Complaint chưa soi tới → background jobs/SMS/mã hoá PII → re-check toàn bộ 4 mục còn treo), phát hiện **14 vấn đề thật**. Sau đó, trong lúc xây tính năng sổ minh bạch donate công khai (2026-08-13), phát hiện thêm **1 vấn đề thứ 15** (donor không được báo khi donate thành công) khi đọc lại `ProcessDonationPaymentCommandHandler`. Tiếp đó, trong lúc viết journey đầy đủ cho actor "Khách chưa đăng nhập" ([21-anonymous-journey.md](21-anonymous-journey.md), cùng ngày), phát hiện **vấn đề thứ 16**: khiếu nại của khách vãng lai không có kênh nào để biết kết quả xử lý và không tra cứu lại được. Toàn bộ **16 vấn đề** đã được xác nhận **sửa xong** tính đến thời điểm ghi tài liệu này. Không còn finding mức Cao/Trung bình nào mở.

**Chủ đề xuyên suốt của các finding**: điểm yếu nhất quán không nằm ở tầng nền (transaction wrapping, ownership checks, FluentValidation, `FallbackPolicy` — đều làm chắc từ đầu) mà ở **các nhánh rẽ ít đi qua** — venue quên đăng ký/xác minh bank account, khiếu nại cần hoàn tiền ngoài kịch bản hủy-vé-tự-nguyện, donate cần hoàn tiền, dữ liệu ML cho gợi ý (`UserEventScore`) chưa từng được ghi. Đây là những "nhánh phụ" dễ bị bỏ sót khi code nhanh (vibe coding) vì happy-path chính vẫn chạy đúng.

---

## 2. Đã tìm & đã sửa

| # | Vấn đề (Category) | Vị trí | Mức ảnh hưởng gốc | Cách đã sửa |
|---|---|---|---|---|
| 1 | **[Cat 1]** Xác nhận vé (`ProcessVnPayCallbackCommandHandler`) bị rollback toàn bộ nếu venue chưa có bank account mặc định — `ScheduleSettlementHandler` (chạy trong cùng transaction) `throw`, cuốn theo cả việc xác nhận Payment/Ticket dù VNPay đã thu tiền thật | `ScheduleSettlementHandler.cs` (cũ) | Cao | Tách thành `SettlementSchedulingService.TryScheduleAsync` — **không throw nữa**, skip + báo Owner và mọi Admin (`SettlementSchedulingBlocked`), tự động retry qua `RetryBlockedForLoungeAsync` khi điều kiện được giải quyết |
| 2 | **[Cat 1/2]** `BankAccount.IsVerified` không bao giờ được set `true`, không nơi nào kiểm tra trước khi trả tiền | `BankAccount.cs` | Cao | `VerifyBankAccountCommandHandler` mới (Admin xác minh thủ công) + `SettlementSchedulingService` chặn cứng nếu chưa verified, không chỉ chặn nếu thiếu tài khoản |
| 3 | **[Cat 2]** `AccountNumber` không validate định dạng | `CreateBankAccountCommandValidator.cs` | Trung bình | `Matches(@"^\d{6,19}$")` — chuẩn Napas liên ngân hàng VN |
| 4 | **[Cat 1]** VNPay không có API hoàn tiền thật — "Approve" refund chỉ đảo bút toán nội bộ, tiền không thật sự về tay khán giả | `IVnPayService.cs` | Cao | `RefundAsync` mới, gọi thật qua merchant_webapi. `ProcessRefundRequestCommandHandler` tách 2 bước (đảo bút toán trước, gọi gateway sau — lỗi gateway không rollback kế toán); thất bại/sandbox không hỗ trợ → `RequiresManualTransfer=true` + báo mọi Admin |
| 5 | **[Cat 1]** `ComplaintResolvedAction.Refund`/`Compensate` chỉ là nhãn — Admin duyệt "hoàn tiền" qua khiếu nại không tạo `RefundRequest` nào | `ResolveComplaintCommandHandler.cs` | Cao | Tự tạo `RefundRequest` thật khi khiếu nại nhắm vào 1 vé cụ thể (`TargetType=="ticket"`) |
| 6 | **[Cat 1]** Không có cách nào cho Admin tự tạo `RefundRequest` thủ công (kẹt nếu đã quá hạn khán giả tự hủy) | `AdminController.cs` | Cao | `POST /admin/refund-requests` — escape hatch thủ công, vẫn qua đúng quy trình `Pending → Process` |
| 7 | **[Cat 1]** Donation hoàn toàn không có cơ chế hoàn tiền (không có `Payment` row nên `RefundRequest` không áp dụng được) | Kiến trúc `Donation.cs` | Cao | `POST /admin/donations/{id}/refund` mới — chỉ hợp lệ trước khi Owner đã chuyển tiền cho nghệ sĩ (chặng 2), đảo bút toán chặng 1 |
| 8 | **[Cat 3]** `RecomputeUserEventScoresJob` không tồn tại → **`CollabScore` (30% công thức `FinalScore` gợi ý) luôn bằng 0 cho mọi user** kể từ ngày công thức được viết | (thiếu job) | Cao (âm thầm, không lỗi rõ ràng) | Job mới ghi `UserEventScore` thật từ hành vi/rating/donate/wishlist; trọng số đọc từ `system_config` (không hardcode) |
| 9 | **[Cat 2]** Không giới hạn số lượng tối đa/món trong đơn F&B | `CreateFnbOrderCommandValidator.cs` | Thấp | `FnbOrderItemMaxQuantity` (config, mặc định 50) |
| 10 | **[Cat 1]** Ledger integrity check (`SUM(debit)=SUM(credit)`) chỉ chạy thủ công khi Admin tự bấm | (thiếu job) | Trung bình | `LedgerIntegrityCheckJob` — tự động định kỳ, báo mọi Admin nếu lệch (cùng kiến trúc `VnPayReconciliationJob`) |
| 11 | **[Cat 3]** Khoá mã hoá PII (CCCD, số tài khoản NH qua Data Protection API) không cấu hình lưu bền vững — redeploy/đổi máy có thể làm mất khả năng giải mã dữ liệu đã mã hoá | `DependencyInjection.cs` | Trung bình-Cao | `PersistKeysToFileSystem` + `ProtectKeysWithDpapi(protectToLocalMachine: true)`, ghi rõ giới hạn (chỉ đúng cho single-machine) |
| 12 | **[Cat 3]** `ISmsService` từng là stub thuần — OTP xác thực SĐT không gửi được | `SmsService.cs` | Cao | Tích hợp thật SpeedSMS.vn (lý do chọn thay Twilio: VN chặn SMS long-code, cần đăng ký Sender ID ~5 tuần) — **xem [§4](#4-ghi-chú-thực-trạng-khác-biệt-với-code)** về khác biệt với pipeline demo thật |
| 13 | **[Cat 3]** `DeviceToken` không có cơ chế dọn token cũ | `DeviceToken.cs` (comment cũ) | Thấp | `PruneStaleDeviceTokensJob` (30 ngày, configurable) |
| 14 | **[Cat 4]** Gán Owner/Admin làm Staff venue khác tạo `LoungeStaff` vô nghĩa (không thật sự cấp quyền gì do `VenueOperatorAccess` chỉ nhận Role=Staff) | `AssignStaffCommandHandler.cs` | Trung bình (toàn vẹn dữ liệu, không phải leo thang quyền) | Chặn cứng bằng `ConflictException` nếu `User.Role` là `Owner`/`Admin` |
| 15 | **[Cat 1/4]** VNPay confirm donate thành công chỉ báo Owner (`DonationReceived`) — donor không nhận thông báo nào, chỉ có redirect trình duyệt 1 lần (mất nếu đóng tab) hoặc tự vào `GET /donations/my` xem | `ProcessDonationPaymentCommandHandler.cs` | Trung bình (trải nghiệm, không phải mất tiền — tiền vẫn xử lý đúng) | `NotificationType.DonationConfirmed` mới — `NotifyAsync(donorId, ...)` riêng tư ngay sau bước báo Owner, không phụ thuộc `IsAnonymous`/`IsAmountPublic` vì là kênh 1-1 |
| 16 | **[Cat 1/4]** Khiếu nại khách vãng lai (`ComplainantUserId=null`, D17/NĐ 85/2021) không có kênh nào để biết kết quả Admin xử lý, và **không tra cứu lại được** dù đã có `id` từ response lúc gửi — kể cả sau này tự tạo tài khoản cũng không nhận lại được khiếu nại cũ | `ResolveComplaintCommandHandler.cs`, `ComplaintsController.cs` | Trung bình (tuân thủ NĐ 85/2021 + trải nghiệm, không phải mất tiền) | Research thực tế (xem [§3](#3-đối-chứng-tích-cực)) chỉ ra 2 pattern bổ sung nhau, cả hai đều đã làm: (1) "đẩy" — SMS tới `ContactPhone` khi Admin resolve (`ISmsService.SendComplaintResolutionAsync`, đã có sẵn từ vòng sửa #5); (2) "kéo" — `GET /complaints/lookup?id=&phone=` mới, khớp `id`+`ContactPhone` (mirror "guest order tracking" TMĐT: Order Number + Email/Zip), cùng 404 dù sai id hay sai phone để tránh dò quét, thêm rate-limit policy "auth" (10 req/phút/IP, sẵn có, dùng lại từ login) vì `id` là số nguyên tuần tự dễ đoán |

---

## 3. Đối chứng tích cực

Những phần sau **đã làm đúng/chắc ngay từ đầu**, không phải finding — nêu ra để tránh audit sau này tốn công rà lại:

- **`TransactionBehavior`** (MediatR pipeline) bọc thật mọi `ICommand` trong 1 DB transaction thật (Begin/Commit/Rollback) — chỉ 5 command tự nguyện opt-out (`INoTransactionCommand`), đều có lý do hợp lý (Login/VerifyEmail cần commit lockout counter ngay, job kickoff).
- **`FallbackPolicy`** yêu cầu đăng nhập mặc định — endpoint nào lỡ quên `[Authorize]` vẫn an toàn (fail-closed).
- Validate số tiền donate/số lượng giữ vé/giá vé/số tiền refund duyệt đều enforce ở **backend** qua FluentValidation + `system_config`, không chỉ dựa frontend.
- IDOR đã spot-check sạch ở ticket detail, bank account detail/list — đều có kiểm tra chủ sở hữu thật ở tầng Application (`BankAccountAccess.EnsureCanManageAsync`, `BuyerId == currentUser`).
- Luồng Subscription payment (`ProcessSubscriptionPaymentCommandHandler`) tự phát hiện + xử lý đúng race double-submit, không để tiền "biến mất không dấu vết".
- Các job dọn dẹp (`ReleaseExpiredHoldsJob`, `CancelAbandonedPaymentsJob`, `ExpireStuckDonationsJob`, `ApplyDuePenaltiesJob`) đều khoá đúng key tránh race với luồng chính, idempotent qua timestamp/`AppliedAt`.
- Không tìm thấy TODO/FIXME/HACK dạng comment thô nào trong toàn bộ `src/` — kỷ luật viết comment giải thích lý do thay vì để lại ghi chú tạm.
- **Sổ donate công khai** (`GetPublicFeedAsync`) cố ý loại `PendingOwnerAck`, chỉ nhận `OwnerReceived`/`PerformerPaid` — ban đầu trông như copy-paste tình cờ từ endpoint cũ, nhưng research thực tế xác nhận đây đúng pattern "pending vs. posted" ngành ngân hàng (Experian/PNC/SoFi): giao dịch pending còn có thể bị `Refunded`/`Cancelled` nên không vào sổ chính thức. Không phải bug, không cần sửa cho khớp với luồng realtime.
- **Mô hình ẩn danh 2 cờ độc lập** (`IsAnonymous` ẩn tên, `IsAmountPublic` ẩn cả cụm 7 field tiền) đối chiếu thực tế khớp đúng GoFundMe (2 tuỳ chọn tách biệt) và Streamlabs (donation-alert widget cùng logic); Ko-fi siết chặt hơn (không cho ẩn số tiền) — nếu sau này muốn nâng minh bạch tài chính, đây là hướng tham khảo.
- **Tra cứu khiếu nại khách vãng lai** (`GET /complaints/lookup`) đối chiếu 2 pattern thực tế độc lập: TMĐT dùng "guest order tracking" (Order Number + Email/Zip, không cần tài khoản — Baymard Institute có 165 ví dụ nghiên cứu) cho việc **khách tự tra**; hệ thống ticketing dạng email (Zendesk/Freshdesk/Help Scout) **chủ động báo lại** qua đúng kênh khách đã cung cấp thay vì bắt khách tự kiểm tra. MusicLounge làm cả 2 (lookup theo id+phone, và SMS khi resolve) vì đã sẵn `ContactPhone` + `ISmsService` — không phải tự nghĩ ra cơ chế mới.

---

## 4. Ghi chú thực trạng khác biệt với code

- **SMS**: code hiện gọi SpeedSMS.vn thật (`SmsService.cs`), nhưng theo xác nhận trực tiếp — **pipeline demo thật đang dùng Twilio, số nước ngoài gửi tới số nước ngoài** (Twilio không gửi được số VN qua long code mà không đăng ký Alphanumeric Sender ID ~5 tuần — đúng như chính comment trong `SmsSettings.cs` cũng nêu lý do chuyển sang SpeedSMS). Đây là khác biệt giữa **trạng thái code** và **pipeline vận hành demo thật**, không phải lỗi cần sửa trong code — ghi lại để tránh nhầm lẫn ở lần đọc code sau.
- **VNPay Refund/Query**: `RefundAsync`/`QueryTransactionAsync` đúng cấu trúc kỹ thuật nhưng **không kiểm chứng được end-to-end** — theo xác nhận trực tiếp, VNPay sandbox không cấp chức năng refund cho môi trường này. Khi `RequiresManualTransfer=true` (sẽ luôn đúng trong sandbox), Admin xử lý chuyển khoản thủ công — đây là hành vi đúng/đủ trong phạm vi capstone, không phải việc dang dở.

---

## 5. Quy trình rà soát — để tái sử dụng sau này

4 câu hỏi đã dùng, hiệu quả với codebase kiểu MediatR/Clean Architecture như dự án này:

1. **Cơ chế xử lý lỗi & giao dịch tiền**: tìm `TransactionBehavior`/tương đương trước, xác định phạm vi bọc transaction thật sự tới đâu; sau đó lần theo từng `INotificationHandler`/domain event được `Publish` bên trong 1 command tiền — đây là nơi dễ ẩn bug "1 nhánh phụ throw làm rollback cả nhánh chính".
2. **Validate input**: đọc `*CommandValidator.cs` cạnh mỗi handler xử lý tiền/số lượng — so sánh xem field nào có `system_config`-driven bound, field nào chỉ có `NotEmpty`.
3. **TODO/hardcode**: grep `TODO|FIXME|HACK` thường ra rỗng ở codebase kỷ luật — đổi hướng sang grep `private const (decimal|float|int)` và các cụm "isn't implemented yet"/"stub"/"chưa tích hợp" trong comment.
4. **Phân quyền**: kiểm tra có `FallbackPolicy` không trước (quyết định mức độ nghiêm trọng của 1 `[Authorize]` bị thiếu); sau đó spot-check IDOR ở các query theo ID do client truyền (ticket detail, bank account, refund request).

---

*Xem [11-ba-domain-analysis.md](11-ba-domain-analysis.md), [12-actors-and-authorization.md](12-actors-and-authorization.md), [13-data-model.md](13-data-model.md), [14-usecase-traces.md](14-usecase-traces.md) cho các mặt còn lại của bộ tài liệu bàn giao.*
