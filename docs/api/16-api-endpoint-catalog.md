# 16 — Danh mục Endpoint theo Domain

← [11-ba-domain-analysis.md](11-ba-domain-analysis.md) · [12-actors-and-authorization.md](12-actors-and-authorization.md) · [15-risk-audit.md](15-risk-audit.md)

> **Phạm vi**: đọc toàn bộ 24 file Controller trong `src/MusicLounge.Api/Controllers/` (25 class controller — `DonationsController.cs` và `ComplaintsController.cs` mỗi file chứa 2 class). ~209 endpoint (208 tại thời điểm đọc ban đầu + `GET /complaints/lookup` bổ sung 2026-08-13), nhóm theo 10 domain đã xác định ở [11-ba-domain-analysis.md](11-ba-domain-analysis.md) + 3 nhóm bổ sung không nằm trong 10 domain gốc (Khiếu nại & Xử phạt, Vận hành hệ thống/Admin, Hạ tầng dùng chung).
>
> **Loại**: `DS` = Danh sách/tổng quan (list, search, dashboard) · `CT` = Chi tiết 1 bản ghi · `HĐ` = Hành động (tạo/sửa/xoá/thực hiện nghiệp vụ).
>
> **Cột Response**: với DTO tôi đã đọc trực tiếp field trong phiên phân tích này, liệt kê tên field thật. Với DTO chỉ thấy qua `ProducesResponseType<>` (chưa mở file), ghi tên DTO kèm "(chưa xác minh field)" — theo đúng nguyên tắc không suy đoán của bộ tài liệu này.
>
> **Cập nhật**: 2026-08-13.

---

## 1. Auth & Tài khoản

`AuthController` (base `[AllowAnonymous]`, rate-limit `"auth"`) + `MeController` (base `RequireAuthenticated`).

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| HĐ | POST /auth/register | Ai cũng gọi (chưa có tài khoản) | Email, Password, FullName, Phone?, AcceptTerms, Role="Audience"\|"Owner" | RegisterResultDto (chưa xác minh field) | Chỉ tạo tài khoản chưa xác thực, chưa cấp token |
| HĐ | POST /auth/verify-email | Ai cũng gọi | Email, Code | AuthResultDto (token) | Cấp token đăng nhập lần đầu |
| HĐ | POST /auth/resend-verification-code | Ai cũng gọi | Email | 204 | Luôn 204 dù email tồn tại hay không |
| HĐ | POST /auth/login | Ai cũng gọi | Email, Password (+IP lấy từ connection) | AuthResultDto | |
| HĐ | POST /auth/google | Ai cũng gọi | IdToken, AcceptTerms | AuthResultDto | AcceptTerms chỉ bắt buộc lần đầu (tài khoản mới) |
| HĐ | POST /auth/forgot-password | Ai cũng gọi | Email | 204 | Luôn 204 — tránh lộ email đã đăng ký |
| HĐ | POST /auth/reset-password | Ai cũng gọi | Token, NewPassword | 204 | |
| CT | GET /me | Audience/Owner/Staff/Admin (chính mình) | — | UserProfileDto (chưa xác minh field) | |
| CT | GET /me/earnings | RequireOwner | — | EarningsSummaryDto (chưa xác minh field) | |
| HĐ | PUT /me/preferences | RequireAuthenticated | GenreIds[], MoodIds[], AtmosphereIds[], EnableAiConsent | 204 | |
| HĐ | PUT /me/profile | RequireAuthenticated | FullName, Phone, AvatarUrl, DateOfBirth | 204 | |
| HĐ | PUT /me/password | RequireAuthenticated | CurrentPassword, NewPassword | 204 | Chỉ áp dụng tài khoản đăng nhập bằng mật khẩu |
| HĐ | POST /me/email/change-request | RequireAuthenticated | NewEmail | 204 | Bước 1/2 |
| HĐ | POST /me/email/change-confirm | RequireAuthenticated | Code | 204 | Bước 2/2 |
| HĐ | POST /me/citizen-card | RequireAuthenticated | CitizenCardNumber, FrontImageUrl, BackImageUrl | 204 | KYC — URL lấy từ POST /uploads/citizen-card-images |
| CT | GET /me/citizen-card/{side} | RequireAuthenticated (chính chủ) | — | file ảnh | |
| CT | GET /me/data-export | RequireAuthenticated | — | MyDataExportDto (chưa xác minh field) | DSAR — Luật 91/2025/QH15 |
| HĐ | DELETE /me | RequireAuthenticated | — | 204 | Khoá tạm, khôi phục được |
| HĐ | POST /me/data-erasure | RequireAuthenticated | CurrentPassword? | 204 | Xoá vĩnh viễn — ẩn danh hoá, không hard-delete |
| HĐ | POST /me/phone/verification-code | RequireAuthenticated | — | 204 | NĐ 147/2024 |
| HĐ | POST /me/phone/verify | RequireAuthenticated | Code | 204 | |
| DS | GET /me/custom-preferences | RequireAuthenticated | — | UserCustomPreferenceDto[] (chưa xác minh field) | |
| HĐ | PUT /me/custom-preferences/{criteriaId} | RequireAuthenticated | Value, Weight | 204 | |

---

## 2. Venue/Lounge

`LoungesController` + `VenuePenaltiesController`.

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| DS | GET /lounges | Anonymous (Optional Auth) | city?, mine?, page, pageSize | PaginatedResult\<LoungeListItemDto\> | **mine=true** (cần login Owner) trả riêng lounge của chính Owner đó, khác danh sách công khai |
| CT | GET /lounges/{id} | Anonymous (Optional Auth) | — | LoungeDetailDto (chưa xác minh field) | Optional Auth — khả năng Owner xem thêm field quản trị chưa xác minh cụ thể |
| HĐ | POST /lounges | RequireOwner | Name, Description, AtmosphereId, Street/Ward/District/City, Latitude/Longitude | int Id | |
| HĐ | PUT /lounges/{id} | RequireOwner (đúng OwnerId) | (như trên) | 204 | |
| HĐ | DELETE /lounges/{id} | RequireOwner | — | 204 | |
| DS | GET /lounges/{id}/staff | RequireOwner | — | LoungeStaffDto[] (chưa xác minh field) | |
| CT | GET /lounges/staff/lookup | RequireOwner | email (query) | UserLookupDto (chưa xác minh field) | Tra cứu trước khi AssignStaff |
| HĐ | POST /lounges/{id}/staff | RequireOwner | UserId | int staffId | |
| HĐ | DELETE /lounges/{id}/staff/{staffId} | RequireOwner | — | 204 | |
| HĐ | PUT /lounges/{id}/image | RequireOwner | ImageUrl | 204 | |
| HĐ | PUT /lounges/{id}/business-license | RequireOwner | DocumentUrl | 204 | |
| HĐ | PUT /lounges/{id}/model-3d | RequireOwner | ModelUrl? | 204 | Tour 3D (.glb) — khác hẳn tour 360° panorama bên dưới |
| DS | GET /lounges/{id}/zones | Anonymous | activeOnly? | SeatingZoneDto[] (chưa xác minh field) | |
| HĐ | POST /lounges/{id}/zones | RequireOwner | Name, Description, Capacity | int zoneId | |
| HĐ | PUT /lounges/{id}/zones/{zoneId} | RequireOwner | Name, Description, Capacity | 204 | |
| HĐ | DELETE /lounges/{id}/zones/{zoneId} | RequireOwner | — | 204 | |
| HĐ | PUT /lounges/{id}/zones/{zoneId}/layout-2d | RequireOwner | X, Y, Width, Height, RotationDeg, Color | 204 | |
| HĐ | PUT /lounges/{id}/zones/{zoneId}/layout-3d | RequireOwner | X, Y, Z | 204 | Null cả 3 = gỡ marker |
| HĐ | PUT /lounges/{id}/area-layout-image | RequireOwner | ImageUrl? | 204 | |
| CT | GET /lounges/{id}/tour | Anonymous | — | VenueTourDto (chưa xác minh field) | Tour 360° kiểu Louvre — nhiều scene panorama |
| HĐ | POST /lounges/{id}/tour/scenes | RequireOwner | ImageUrl, Name | int sceneId | |
| HĐ | POST /lounges/{id}/tour/scenes/stitch | RequireOwner | SourceImageUrls[], Name | int attemptId (202 Accepted) | Ghép ảnh panorama qua microservice riêng, chạy nền |
| CT | GET /lounges/{id}/tour/scenes/stitch/{attemptId} | RequireOwner | — | VenueTourStitchAttemptDto (chưa xác minh field) | Poll trạng thái Pending/Succeeded/Failed |
| HĐ | DELETE /lounges/{id}/tour/scenes/{sceneId} | RequireOwner | — | 204 | |
| HĐ | PUT /lounges/{id}/tour/scenes/{sceneId}/position | RequireOwner | X?, Y? | 204 | |
| HĐ | POST /lounges/{id}/tour/scenes/{sceneId}/hotspots | RequireOwner | Type, Yaw, Pitch, Label, TargetSceneId, InfoText | int hotspotId | |
| HĐ | DELETE /lounges/{id}/tour/hotspots/{hotspotId} | RequireOwner | — | 204 | |
| HĐ | POST /lounges/{id}/gallery | RequireOwner | ImageUrl, Caption | int imageId | Ảnh showcase, không giới hạn theo gói (khác tour scene) |
| HĐ | DELETE /lounges/{id}/gallery/{imageId} | RequireOwner | — | 204 | |
| DS | GET /lounges/{id}/custom-criteria | RequireOwner | — | CustomCriteriaDto[] (chưa xác minh field) | Tiêu chí gợi ý riêng của venue |
| HĐ | POST /lounges/{id}/custom-criteria | RequireOwner | Name, Key, DataType, Options | int criteriaId | |
| HĐ | PUT /lounges/{id}/custom-criteria/{criteriaId} | RequireOwner | Name, Options, IsActive | 204 | Key/DataType không sửa được sau khi tạo |
| HĐ | POST /venue-penalties | RequireAdmin | LoungeId, PenaltyType, Reason, EvidenceRef, SuspensionDays | int id | |
| CT | GET /venue-penalties/{id} | RequireAuthenticated (chủ venue hoặc Admin — check trong handler) | — | VenuePenaltyDto (chưa xác minh field) | |
| DS | GET /venue-penalties/mine | RequireOwner | page, pageSize | PaginatedResult\<VenuePenaltyDto\> | Mọi trạng thái, mọi lounge của Owner |
| HĐ | POST /venue-penalties/{id}/appeal | RequireOwner | AppealReason | 204 | |
| HĐ | POST /venue-penalties/{id}/appeal/review | RequireAdmin | Decision, ReviewNote | 204 | |

---

## 3. Show/Event

`LoungeShowsController` + `EventModerationsController` (dùng chung với Livestream) + `PerformersController`.

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| DS | GET /lounge-shows | Anonymous (Optional Auth) | page, pageSize, sortBy, includeSoldOut, mine | PaginatedResult\<LoungeShowListItemDto\> | **mine=true** (Owner) trả cả Draft/Pending, khác public chỉ thấy Published |
| DS | GET /lounge-shows/suggestions | Anonymous | q, limit | LoungeShowSuggestionItem[] (chưa xác minh field) | Autocomplete thanh tìm kiếm |
| CT | GET /lounge-shows/filter-options | Anonymous | — | FilterOptionsDto (chưa xác minh field) | Danh mục genre/mood/atmosphere/category cho bộ lọc |
| DS | GET /lounge-shows/search | Anonymous (Optional Auth) | keyword, genreIds/moodIds/atmosphereIds[], performerId, loungeId, city/district/ward, dateFrom/To, format, minPrice/maxPrice, includeSoldOut/Ended, page, pageSize, sortBy | PaginatedResult\<LoungeShowListItemDto\> | Endpoint filter đầy đủ nhất |
| DS | GET /lounge-shows/trending | Anonymous (Optional Auth) | limit, city | LoungeShowListItemDto[] | |
| CT | GET /lounge-shows/{id} | Anonymous (Optional Auth) | — | LoungeShowDetailDto (chưa xác minh field) | Dùng chung cho cả mua vé lẫn xem chi tiết trước khi vào livestream |
| CT | GET /lounge-shows/{id}/seating-map | Anonymous (Optional Auth) | — | SeatingMapDto (chưa xác minh field) | |
| DS | GET /lounge-shows/{id}/orders | RequireOwner | page, pageSize | PaginatedResult\<ShowOrderDto\> | |
| CT | GET /lounge-shows/by-performer/{performerId} | Anonymous (Optional Auth) | includeEnded, page, pageSize | PerformerDetailDto (chưa xác minh field) | |
| HĐ | POST /lounge-shows/{id}/rate | RequireAuthenticated | Score, Comment | 204 | |
| DS | GET /lounge-shows/by-lounge/{loungeId} | Anonymous (Optional Auth) | page, pageSize | PaginatedResult\<LoungeShowListItemDto\> | |
| HĐ | POST /lounge-shows | RequireOwner | Name, Description, Format, ScheduledStart/End, CategoryId, Offline/OnlineQuota, GenreIds[], Performances[] (PerformerId/Name, Role, OrderIndex, SetTime, AcceptsDonation), CustomValues[] | int id | Performances không giới hạn số lượng (0..n) |
| HĐ | PUT /lounge-shows/{id} | RequireOwner | (như trên) | 204 | |
| HĐ | DELETE /lounge-shows/{id} | RequireOwner | — | 204 | |
| HĐ | POST /lounge-shows/{id}/publish | RequireOwner | — | 204 | Cần venue Approved + ≥1 hạng vé |
| HĐ | POST /lounge-shows/{id}/cancel | RequireOwner | — | 204 | |
| HĐ | POST /lounge-shows/{id}/reschedule | RequireOwner | NewScheduledStart | 204 | Áp lại quy tắc 7-ngày-làm-việc cho ngày mới |
| HĐ | POST /lounge-shows/{id}/ai-poster | RequireOwner | StyleHint? | PosterGenerationResultDto (chưa xác minh field) | Gate theo subscription snapshot, kiểm trong handler |
| DS | GET /lounge-shows/{id}/ai-poster/history | RequireOwner | — | PosterGenerationAttemptDto[] (chưa xác minh field) | |
| HĐ | POST /lounge-shows/{id}/start | RequireVenueOperator | — | 204 | Show Offline vẫn có cặp Start/End riêng (không qua Livestream) |
| HĐ | POST /lounge-shows/{id}/end | RequireVenueOperator | — | 204 | |
| HĐ | PUT /lounge-shows/{id}/legal-approval | RequireOwner | LegalApprovalReference | 204 | NĐ 144/2020 |
| HĐ | PUT /lounge-shows/{id}/vcpmc-royalty | RequireOwner | VcpmcRoyaltyReference | 204 | |
| HĐ | PUT /lounge-shows/{id}/cover-image | RequireOwner | ImageUrl | 204 | |
| HĐ | PUT /lounge-shows/{id}/poster | RequireOwner | ImageUrl | 204 | Đối trọng thủ công của POST .../ai-poster |
| HĐ | PUT /lounge-shows/{id}/playback-mode | RequireOwner | PlaybackMode | 204 | 2D/3D cho show Online |
| HĐ | PUT /lounge-shows/{id}/format | RequireOwner | NewFormat | 204 | Offline→Online hoàn 100% vé vật lý |
| DS | GET /moderations/pending | RequireAdmin | targetType?, page, pageSize | PaginatedResult\<EventModerationDto\> | Dùng chung Show + Livestream |
| HĐ | POST /moderations/livestreams/{id}/review | RequireAdmin | Decision, ReviewNote | 204 | **Thuộc cả 2 domain**: Show/Event (kiểm duyệt) và Livestream |
| HĐ | POST /moderations/shows/{id}/review | RequireAdmin | Decision, ReviewNote | 204 | |
| DS | GET /performers | RequireOwner | search, page, pageSize | PaginatedResult\<PerformerDto\> | Catalog dùng chung mọi Owner |
| CT | GET /performers/{id} | RequireOwner | — | PerformerDto (chưa xác minh field) | |
| HĐ | POST /performers | RequireOwner | Name, AvatarUrl, Bio, Type, GenreIds[] | int id | |
| HĐ | PUT /performers/{id} | RequireOwner (chỉ người tạo + Admin — check ở handler) | Name, AvatarUrl, Bio, Type, GenreIds[] | 204 | |
| HĐ | DELETE /performers/{id} | RequireOwner (như trên) | — | 204 | |
| HĐ | PUT /performers/{id}/social-links | RequireOwner | Platform, Url, DisplayName | int linkId | Upsert — trùng platform thì ghi đè |
| HĐ | DELETE /performers/{id}/social-links/{linkId} | RequireOwner | — | 204 | |

---

## 4. Ticket

`TicketsController` (base `RequireAuthenticated`) + `TicketTiersController`.

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| HĐ | POST /tickets/holds | RequireAuthenticated | PriceId, Quantity | HoldTicketResultDto (chưa xác minh field) | Giữ chỗ 15 phút |
| HĐ | POST /tickets/purchase | RequireAuthenticated | HoldId (+IP) | PaymentInitiationDto (chưa xác minh field) | |
| HĐ | DELETE /tickets/holds/{holdId} | RequireAuthenticated | — | 204 | |
| DS | GET /tickets/my | RequireAuthenticated | page, pageSize | PaginatedResult\<TicketListItemDto\> | |
| DS | GET /tickets/refund-requests/my | RequireAuthenticated | page, pageSize | PaginatedResult\<RefundRequestDto\> | |
| DS | GET /tickets/incoming-transfers | RequireAuthenticated | — | IncomingTicketTransferDto[] (chưa xác minh field) | |
| HĐ | POST /tickets/walk-in | RequireVenueOperator | PriceId, Quantity | WalkInSaleResultDto (chưa xác minh field) | Bán vé quầy — không hoa hồng mặc định |
| CT | GET /tickets/by-qr/{qrCode} | RequireAuthenticated (chủ vé hoặc Staff — check handler) | — | TicketDetailDto (chưa xác minh field) | Xem trước, tách khỏi check-in |
| HĐ | POST /tickets/check-in | RequireVenueOperator | QrCode | TicketDetailDto | Scoped theo lounge_id của Staff |
| HĐ | POST /tickets/{id}/cancel | RequireAuthenticated | — | int refundRequestId | |
| CT | GET /tickets/{id} | RequireAuthenticated (chủ vé — check handler) | — | TicketDetailDto | |
| CT | GET /tickets/{id}/qr | RequireAuthenticated (chủ vé) | — | ảnh SVG | |
| HĐ | POST /tickets/{id}/transfer | RequireAuthenticated | RecipientEmail | 204 | |
| HĐ | POST /tickets/{id}/transfer/accept | RequireAuthenticated | — | 204 | |
| HĐ | POST /tickets/{id}/transfer/cancel | RequireAuthenticated | — | 204 | |
| DS | GET /ticket-tiers | Anonymous | showId | TicketTierSummaryDto[] (chưa xác minh field) | |
| CT | GET /ticket-tiers/{id} | Anonymous | — | TicketTierSummaryDto | |
| HĐ | POST /ticket-tiers | RequireOwner | ShowId, Name, Description, AccessType, ZoneId, TotalCapacity, Prices[] | int id | |
| HĐ | PUT /ticket-tiers/{id} | RequireOwner | Name, Description, TotalCapacity, AccessType, ZoneId, Prices[] | 204 | |
| HĐ | DELETE /ticket-tiers/{id} | RequireOwner | — | 204 | |

---

## 5. Payment/Finance (Donate, Settlement, Refund, Subscription)

`PaymentsController` + `DonationsController`/`PerformerDonationsController` + `BankAccountsController` + `SubscriptionsController` + phần tài chính của `AdminController`.

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| HĐ | GET /payments/vnpay/callback | Anonymous (VNPay/trình duyệt) | query params VNPay | redirect 302 | Xác nhận thanh toán vé |
| HĐ | GET /payments/vnpay/ipn | Anonymous (VNPay server-to-server) | query params VNPay | VnPayIpnResponse(RspCode, Message) | Nguồn sự thật, không phụ thuộc trình duyệt buyer |
| HĐ | POST /donations | RequireAuthenticated | PerformanceId, Amount, IsAnonymous, Message, IsMessagePublic | DonationInitiationDto(DonationId, OrderId, Gross, PaymentUrl) | Chỉ khi show Ongoing |
| CT | GET /donations/{id} | RequireAuthenticated (chủ donate/Owner/Admin — check handler) | — | DonationDto(Id, PerformerName, ShowName, Gross, Net, PlatformFee, Tax, PerformerShareRate, PerformerAmount, OwnerRetained, Status, IsAnonymous, DisplayName, IsAmountPublic, Message, CreatedAt) | Đầy đủ breakdown phí |
| DS | GET /donations/public | Anonymous | page, pageSize | PaginatedResult\<PublicDonationTransactionDto\> (Id, PerformerName, ShowName, VenueName, DonorDisplayName, Gross, Net, PlatformFee, Tax, PerformerAmount, OwnerRetained, Status, CreatedAt) | Sổ minh bạch **toàn hệ thống**; chỉ `OwnerReceived`/`PerformerPaid` (loại `PendingOwnerAck` theo pattern pending-vs-posted) |
| HĐ | GET /donations/vnpay-return | Anonymous | query params VNPay | redirect 302 | |
| HĐ | GET /donations/vnpay-ipn | Anonymous | query params VNPay | VnPayIpnResponse | |
| DS | GET /donations/my | RequireAuthenticated | page, pageSize | PaginatedResult\<MyDonationDto\>(Id, PerformerName, ShowName, Gross, Net, Status, IsAnonymous, Message, CreatedAt) | |
| DS | GET /donations/pending-ack | RequireOwner | page, pageSize | PaginatedResult\<PendingDonationDto\>(Id, PerformerName, ShowName, Gross, Net, AmountToPayPerformer, DisplayName, Message, Deadline) | |
| DS | GET /donations/awaiting-payout | RequireOwner | page, pageSize | PaginatedResult\<PendingDonationDto\> | Cùng shape trên, khác filter status |
| HĐ | POST /donations/{id}/acknowledge | RequireOwner | — | 204 | Auto sau 24h nếu im lặng |
| HĐ | POST /donations/{id}/confirm-paid | RequireOwner | PaymentRef, PaymentEvidenceUrl | 204 | |
| DS | GET /performers/{performerId}/donations | Anonymous | page, pageSize | PaginatedResult\<PublicDonationDto\>(Id, ShowName, VenueName, DonorDisplayName, Gross, Status, CreatedAt) | Theo 1 nghệ sĩ, **không có** breakdown phí (khác /donations/public) |
| DS | GET /bank-accounts | RequireOwner | ownerType, ownerId (query) | BankAccountDto[] (chưa xác minh field) | |
| CT | GET /bank-accounts/{id} | RequireOwner | — | BankAccountDto | |
| HĐ | POST /bank-accounts | RequireOwner | OwnerType, OwnerId, BankName, AccountNumber, AccountHolder, IsDefault | int id | Cho venue của mình hoặc Performer mình tạo |
| HĐ | PUT /bank-accounts/{id} | RequireOwner | BankName, AccountNumber, AccountHolder, IsDefault | 204 | |
| DS | GET /subscriptions/packages | Anonymous | activeOnly | SubscriptionPackageDto[] (chưa xác minh field) | |
| HĐ | POST /subscriptions/packages | RequireAdmin | Name, Description, Price, BillingCycle, MaxTicketsPerEvent, HasAiPoster, MaxAiPostersPerMonth, MaxTourScenes | int id | |
| HĐ | PUT /subscriptions/packages/{id} | RequireAdmin | (như trên) + IsActive | 204 | |
| HĐ | POST /subscriptions/subscribe | RequireOwner | PackageId (+IP) | SubscriptionPaymentInitiationDto (chưa xác minh field) | |
| HĐ | POST /subscriptions/renew | RequireOwner | (+IP) | SubscriptionPaymentInitiationDto | Dùng lại gói lần trước, vẫn cần 1 lần OTP VNPay (không tự động được) |
| HĐ | POST /subscriptions/cancel | RequireOwner | — | 204 | |
| HĐ | GET /subscriptions/vnpay-return | Anonymous | query params VNPay | redirect 302 | |
| HĐ | GET /subscriptions/vnpay-ipn | Anonymous | query params VNPay | VnPayIpnResponse | |
| CT | GET /subscriptions/my | RequireOwner | — | MySubscriptionDto (chưa xác minh field) | |
| DS | GET /admin/ledger/integrity-check | RequireAdmin | — | LedgerIntegrityIssueDto[] (chưa xác minh field) | |
| HĐ | POST /admin/bank-accounts/{id}/verify | RequireAdmin | — | 204 | Xác minh thủ công, tự retry settlement đang bị chặn |
| DS | GET /admin/refund-requests | RequireAdmin | page, pageSize | PaginatedResult\<RefundRequestDto\> | |
| HĐ | POST /admin/refund-requests/{id}/process | RequireAdmin | Decision, ApprovedAmount | 204 | |
| HĐ | POST /admin/refund-requests | RequireAdmin | (CreateRefundRequestCommand — chưa xác minh field) | int id | Escape-hatch thủ công |
| HĐ | POST /admin/donations/{id}/refund | RequireAdmin | Reason | 204 | Chỉ trước khi Owner trả nghệ sĩ (chặng 2); đảo bút toán, không gọi VNPay |

---

## 6. Livestream

`LivestreamsController`. (Review kiểm duyệt livestream đã liệt kê ở domain Show/Event §3 vì dùng chung `EventModerationsController`.)

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| CT | GET /livestreams/{id} | RequireAuthenticated | — | LivestreamDetailDto (chưa xác minh field) | **Nội dung khác theo actor** (`LivestreamAccessPolicy`): Admin luôn xem; Staff/Owner chỉ venue mình vận hành; khán giả thường phải là chủ vé thật |
| HĐ | POST /livestreams | RequireVenueOperator | ShowId | int id | |
| HĐ | POST /livestreams/{id}/start | RequireVenueOperator | — | 204 | |
| HĐ | POST /livestreams/{id}/end | RequireVenueOperator | — | 204 | |
| CT | GET /livestreams/{id}/credentials | RequireVenueOperator | — | LivestreamCredentialsDto(RTMP URL, Stream Key) | Không lộ ra viewer |
| DS | GET /livestreams/{id}/chat | RequireAuthenticated | page, pageSize | PaginatedResult\<ChatMessageDto\> (chưa xác minh field) | |
| HĐ | POST /livestreams/{id}/terminate | RequireAdmin | Reason | 204 | Buộc dừng vì vi phạm |

---

## 7. F&B

`FnbMenusController` + `FnbMenuItemsController` + `FnbOrdersController` (base `RequireAuthenticated`).

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| DS | GET /fnb-menus | Anonymous | loungeId, activeOnly | FnbMenuDto[] (chưa xác minh field) | |
| CT | GET /fnb-menus/{id} | Anonymous | — | FnbMenuDto | |
| HĐ | POST /fnb-menus | RequireOwner | LoungeId, Name, Description, DisplayOrder, IsActive | int id | |
| HĐ | PUT /fnb-menus/{id} | RequireOwner | Name, Description, IsActive, DisplayOrder | 204 | |
| HĐ | DELETE /fnb-menus/{id} | RequireOwner | — | 204 | |
| DS | GET /fnb-menu-items | Anonymous | menuId, availableOnly | FnbMenuItemDto[] (chưa xác minh field) | |
| CT | GET /fnb-menu-items/{id} | Anonymous | — | FnbMenuItemDto | |
| HĐ | POST /fnb-menu-items | RequireOwner | MenuId, Category, Name, Description, Price, ImageUrl, DisplayOrder | int id | |
| HĐ | PUT /fnb-menu-items/{id} | RequireOwner | Category, Name, Description, Price, ImageUrl, IsAvailable, DisplayOrder | 204 | |
| HĐ | DELETE /fnb-menu-items/{id} | RequireOwner | — | 204 | |
| HĐ | POST /fnb-orders | RequireAuthenticated | LoungeId, ShowId?, ZoneId?, TableNote, PaymentMethod, Note, Items[] | int id | Khách tự đặt hoặc Staff đặt hộ |
| HĐ | PUT /fnb-orders/{id}/status | RequireVenueOperator | Status | 204 | |
| DS | GET /fnb-orders | RequireAuthenticated (thực chất Owner/Staff — check ở handler, không có policy riêng) | loungeId, status, page, pageSize | PaginatedResult\<FnbOrderDto\> (chưa xác minh field) | |
| DS | GET /fnb-orders/my | RequireAuthenticated (Audience, chính mình) | status, page, pageSize | PaginatedResult\<FnbOrderDto\> | Đối trọng của GetByLounge |
| CT | GET /fnb-orders/{id} | RequireAuthenticated (chủ đơn hoặc Owner/Staff venue — check handler) | — | FnbOrderDto | |

---

## 8. Recommendation/Analytics

`RecommendationsController` + `AnalyticsController`.

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| DS | GET /recommendations | RequireAuthenticated | limit | RecommendedLoungeShowDto[] (chưa xác minh field) | |
| CT | GET /analytics/my-lounge | RequireOwner | loungeId | OwnerAnalyticsDto (chưa xác minh field) | |
| CT | GET /analytics/platform | RequireAdmin | — | PlatformAnalyticsDto (chưa xác minh field) | |

---

## 9. Notification

`NotificationsController` (base `RequireAuthenticated`).

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| DS | GET /notifications | RequireAuthenticated | page, pageSize | PaginatedResult\<NotificationDto\> (chưa xác minh field) | |
| HĐ | POST /notifications/{id}/read | RequireAuthenticated | — | 204 | |
| HĐ | POST /notifications/read-all | RequireAuthenticated | — | 204 | |
| HĐ | POST /notifications/device-tokens | RequireAuthenticated | Fid | 204 | Firebase Installation ID, không phải FCM token cũ |
| HĐ | DELETE /notifications/device-tokens | RequireAuthenticated | Fid | 204 | |

---

## 10. Follow/Wishlist

`FollowsController` + `WishlistController` (base `RequireAuthenticated`).

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| DS | GET /follows/lounges | RequireAuthenticated | page, pageSize | PaginatedResult\<FollowedLoungeDto\> (chưa xác minh field) | |
| HĐ | POST /follows/lounges/{loungeId} | RequireAuthenticated | — | 204 | |
| HĐ | DELETE /follows/lounges/{loungeId} | RequireAuthenticated | — | 204 | |
| DS | GET /wishlist | RequireAuthenticated | page, pageSize | PaginatedResult\<LoungeShowListItemDto\> | |
| HĐ | POST /wishlist/{showId} | RequireAuthenticated | — | 204 | Chặn Draft/Cancelled |
| HĐ | DELETE /wishlist/{showId} | RequireAuthenticated | — | 204 | |

---

## 11. Khiếu nại & Xử phạt (bổ sung, ngoài 10 domain gốc)

`ComplaintsController` + `AdminComplaintsController`. (Xử phạt venue `VenuePenaltiesController` đã liệt kê ở domain Venue/Lounge §2 vì gắn trực tiếp entity `MusicLounge`.)

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| HĐ | POST /complaints | Anonymous (Optional Auth) | TargetType, TargetId, TargetGuid?, Category, ... | int id | Khách vãng lai gửi được (D17), không dùng `TargetGuid` được nếu TargetType≠"ticket" |
| CT | GET /complaints/{id} | RequireAuthenticated (chủ hoặc Admin) | — | ComplaintDto (chưa xác minh field) | Khách vãng lai gửi complaint xong **không** lấy lại được qua route này — dùng `GET /complaints/lookup` bên dưới thay thế |
| CT | GET /complaints/lookup | Anonymous | id (query), phone (query) | ComplaintDto | Lối tra cứu riêng cho khách vãng lai — khớp `id` + `ContactPhone` (loose-match, bỏ qua định dạng +84/0), 404 như nhau dù sai id hay sai phone (chống dò). Mirror "guest order tracking" TMĐT. Rate-limit policy "auth" (10 req/phút/IP) vì id là số nguyên tuần tự |
| DS | GET /complaints/my | RequireAuthenticated | page, pageSize | PaginatedResult\<ComplaintDto\> | |
| DS | GET /admin/complaints | RequireAdmin | page, pageSize | PaginatedResult\<ComplaintDto\> | |
| HĐ | POST /admin/complaints/{id}/resolve | RequireAdmin | Status, Resolution, ResolvedAction, RefundAmount? | 204 | Refund/Compensate tạo RefundRequest thật (chỉ target=ticket) |

---

## 12. Vận hành hệ thống / Admin (bổ sung, ngoài 10 domain gốc)

Phần còn lại của `AdminController` không thuộc Payment/Finance (§5).

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| DS | GET /admin/lounges/pending | RequireAdmin | page, pageSize | PaginatedResult\<PendingLoungeDto\> (chưa xác minh field) | BR-01 |
| HĐ | POST /admin/lounges/{id}/approve | RequireAdmin | — | 204 | |
| HĐ | POST /admin/lounges/{id}/reject | RequireAdmin | Reason | 204 | |
| HĐ | POST /admin/jobs/{jobId}/trigger | RequireAdmin | — | 204 | Ép chạy ngay 1 job Hangfire, không đổi lịch Cron |
| DS | GET /admin/users | RequireAdmin | searchText, role, isActive, page, pageSize | PaginatedResult\<UserAdminDto\> (chưa xác minh field) | |
| CT | GET /admin/users/{id} | RequireAdmin | — | UserAdminDto | |
| CT | GET /admin/users/{id}/citizen-card/{side} | RequireAdmin | — | file ảnh | |
| HĐ | POST /admin/users/{id}/deactivate | RequireAdmin | — | 204 | |
| HĐ | POST /admin/users/{id}/reactivate | RequireAdmin | — | 204 | |
| HĐ | POST /admin/categories | RequireAdmin | (CreateEventCategoryCommand — chưa xác minh field) | int id | Taxonomy dùng chung toàn nền tảng |
| HĐ | POST /admin/genres | RequireAdmin | (CreateMusicGenreCommand — chưa xác minh field) | int id | |
| HĐ | POST /admin/moods | RequireAdmin | (CreateMoodCommand — chưa xác minh field) | int id | |
| HĐ | POST /admin/atmospheres | RequireAdmin | (CreateVenueAtmosphereCommand — chưa xác minh field) | int id | |

---

## 13. Hạ tầng dùng chung (không phải domain nghiệp vụ)

`UploadsController` — dùng bởi nhiều domain khác nhau (avatar, ảnh venue, poster show, CCCD, model 3D...), không thuộc riêng domain nào.

| Loại | Method + Route | Actor | Request (field chính) | Response (field/DTO) | Ghi chú |
|---|---|---|---|---|---|
| HĐ | POST /uploads/images | RequireAuthenticated | file (multipart) | UploadImageResponse(Url) | URL công khai — dùng cho PrimaryImageUrl/CoverImageUrl/avatar |
| HĐ | POST /uploads/citizen-card-images | RequireAuthenticated | file | UploadImageResponse(private ref) | Kho riêng tư, không qua wwwroot công khai |
| HĐ | POST /uploads/models | RequireOwner | file | UploadImageResponse(Url) | .glb/.gltf cho tour 3D |
| DS | GET /uploads/mine | RequireAuthenticated | page, pageSize | PaginatedResult\<UploadedFileDto\> (chưa xác minh field) | |
| HĐ | DELETE /uploads/{id} | RequireAuthenticated (chính người upload) | — | 204 | |

---

## Ghi chú tổng hợp — endpoint dùng chung nhiều domain

- **`GET /lounge-shows/{id}`**: vừa phục vụ luồng mua vé (Ticket) vừa phục vụ xem trước khi vào livestream (Livestream) — 1 endpoint, không tách riêng.
- **`POST/GET /moderations/.../review`**: cùng 1 controller (`EventModerationsController`, `RequireAdmin`) xử lý kiểm duyệt cho cả Show (`shows/{id}/review`) và Livestream (`livestreams/{id}/review`) — 2 route riêng nhưng cùng domain phân quyền.
- **`UploadsController`**: hạ tầng dùng chung cho ít nhất 6 domain khác nhau (Auth-avatar, Venue-image/license/gallery, Show-poster/cover, Livestream — gián tiếp qua RTMP không qua đây, KYC-CCCD, Venue-tour 3D model).
- **`GET /donations/public` vs `GET /performers/{id}/donations`**: cùng mục đích minh bạch donate công khai nhưng khác phạm vi (toàn hệ thống vs 1 nghệ sĩ) và khác độ chi tiết (đầy đủ breakdown phí vs chỉ Gross).

## Endpoint có response khác nhau theo Actor (đáng chú ý)

| Endpoint | Khác biệt theo actor |
|---|---|
| `GET /lounges` (mine=true) | Owner login thấy riêng lounge của mình (mọi trạng thái); không truyền mine hoặc chưa login chỉ thấy danh sách công khai |
| `GET /lounge-shows` (mine=true) | Owner login thấy show Draft/Pending/Published của mình; công khai chỉ thấy Published |
| `GET /livestreams/{id}` | Admin luôn xem; Staff/Owner chỉ venue mình vận hành; khán giả thường bắt buộc là chủ vé thật (`LivestreamAccessPolicy`) |
| `GET /lounges/{id}`, `GET /lounge-shows/{id}` (Optional Auth) | Đánh dấu `SwaggerOptionalAuth` — khả năng cao có field bổ sung cho chủ sở hữu, nhưng **chưa xác minh field cụ thể** trong lượt đọc này |
