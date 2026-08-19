# 13 — Data Model Reference (dựng lại từ code, không dựa ERD cũ)

← [11-ba-domain-analysis.md](11-ba-domain-analysis.md) · [12-actors-and-authorization.md](12-actors-and-authorization.md)

> **Phương pháp**: đọc trực tiếp toàn bộ 68 file trong `src/MusicLounge.Domain/Entities/`, tất cả 46 enum trong `src/MusicLounge.Domain/Enums/`, `ApplicationDbContext.cs` (67 `DbSet` + 1 entity truy cập qua `Set<T>()`), và các file `*Configuration.cs` liên quan (Fluent API — nguồn sự thật cho cascade/cardinality/unique index, không phải navigation property một mình). **Không dựa vào ERD/SRS cũ** — đây là những gì code hiện tại (working tree, xem [11 §0.3](11-ba-domain-analysis.md#03-ghi-chú-về-trạng-thái-repo)) thực sự có.
> **Cập nhật lần cuối**: 2026-08-13.

## Mục lục

1. [Entity catalog theo domain](#1-entity-catalog-theo-domain)
2. [Quan hệ giữa các entity](#2-quan-hệ-giữa-các-entity) (để dựng ERD)
3. [Enum & state machine](#3-enum--state-machine)
4. [Dead data & missing field](#4-dead-data--missing-field)

---

## 1. Entity catalog theo domain

Bỏ qua field kỹ thuật thuần (`Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy` chuẩn `AuditableEntity`) trừ khi có logic nghiệp vụ đặc biệt gắn vào field đó.

### 1.1 Auth & User

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **User** | Danh tính người dùng — mọi role (Audience/Staff/Owner/Admin) đều là 1 row trong bảng này | `Role`, `AuthProvider`+`GoogleId` (local vs Google), `SecurityStamp` (đổi = thu hồi JWT cũ), `FailedLoginAttempts`+`LockedUntil` (brute-force), `IsActive`, `DataErasedAt` (DSAR, không hard-delete), `EmailVerifiedAt`, `PhoneVerified`, `CitizenCardNumber`/`CitizenCardNumberHash` (mã hoá, hash để đối chiếu), `TermsAcceptedAt`+`TermsVersion`, `PendingEmail` (đổi email 2 bước) |

### 1.2 Venue/Lounge

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **MusicLounge** (bảng `Lounges`) | Phòng trà/venue | `Status` (state machine, xem §3), `ReputationScore` (cache hiển thị, xem §4), `Model3DUrl` (.glb dựng sẵn, khác tour ảnh), `BusinessLicenseUrl` |
| **LoungeStaff** | Phân công 1 User làm Staff cho 1 venue | `IsActive` (unique filtered index — 1 user chỉ active ở 1 venue), `AssignedBy`, `DeactivatedAt` |
| **SeatingZone** | Khu vực chỗ ngồi cấp venue (VIP/Standard...), dùng lại cho nhiều show | `Capacity` (an toàn PCCC, không chỉ số bán vé), `Layout2DX/Y/Width/Height/RotationDeg`, `Layout3DX/Y/Z` |
| **VenueTourScene** | 1 ảnh panorama 360° — 1 điểm dừng trong tour ảo | `PositionX/Y` (marker trên floor-plan), `CompletedByAi` (bắt buộc hiển thị disclosure badge theo Luật AI VN) |
| **VenueTourHotspot** | Điểm bấm được trong 1 scene | `Type` (Navigate/Info/LivestreamScreen), `Yaw`/`Pitch`, `TargetSceneId` (chỉ dùng khi Navigate) |
| **VenueTourStitchAttempt** | Log mỗi lần thử ghép ảnh 360° (không chỉ lưu kết quả cuối) | `Status`, `CompletionStatus` (bước AI gap-fill, độc lập với Status), `ErrorMessage` |
| **LoungeGalleryImage** | Nhiều ảnh showcase cho trang chi tiết venue | `OrderIndex` |
| **LoungeImage** | ⚠️ Xem [§4](#4-dead-data--missing-field) — không dùng | — |
| **VenuePenalty** | 1 lần venue bị xử phạt | `PenaltyType` (Warning/Suspension/Ban), `Status` (state machine), `EffectiveAt` (có độ trễ báo trước theo mức phạt), `SuspensionDays`, kháng cáo: `AppealedAt`/`AppealReason`/`AppealDecision` |

### 1.3 Catalog / Taxonomy dùng chung

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **MusicGenre**, **Mood**, **VenueAtmosphere** | 3 bảng taxonomy độc lập (thể loại nhạc / tâm trạng / không khí) — Admin quản lý, dùng chung toàn hệ thống cho cả show, performer, sở thích user | `Name`, `NameEn` (chỉ MusicGenre có) |
| **EventCategory** | Danh mục loại sự kiện (Admin tạo) | `IsActive` |
| **CustomCriteria** | Tiêu chí gợi ý **tự định nghĩa riêng theo từng venue** (không phải taxonomy toàn cục) | `Key` (machine-readable), `DataType` (Select/Range/Boolean/Text), `Options` (JSON schema) |
| **EventCustomValue** | Giá trị cụ thể của 1 `CustomCriteria` gắn cho 1 show | `Value` (JSON) |
| **UserCustomPreference** | Mức độ quan tâm của 1 User với 1 `CustomCriteria` | `Source` (Explicit user tự set / Learned AI suy ra), `Weight` (EMA: `0.3×tín_hiệu_mới + 0.7×trọng_số_cũ`) |

### 1.4 Show / Event

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **LoungeShow** | 1 buổi diễn cụ thể | `Status` (state machine chính), `Format` (Offline/Online — không có Hybrid, 1 show chỉ 1 trong 2, không bao giờ trộn cả 2 loại vé), `OfflineQuota`/`OnlineQuota`, `LegalApprovalReference`+`LegalApprovalConfirmedByAdminId` (NĐ 144/2020), `VcpmcRoyaltyReference` (tác quyền, kiểm tra lúc show bắt đầu — khác thời điểm với giấy phép biểu diễn), `CancellationAllowed`/`CancellationDeadlineHours`/`RefundPercentage` (chính sách hoàn vé riêng từng show), `RatingOpenUntil` (mở đánh giá 7 ngày sau khi kết thúc), `PlaybackMode` (2D/3D), `PosterByAi` |
| **LoungeShowGenre / LoungeShowMood / LoungeShowAtmosphere** | 3 bảng nối show ↔ taxonomy (n-n) | — |
| **LoungeShowRating** | Đánh giá của khán giả cho show | `Score`, `IsRemoved`+`RemovedReason` (Admin gỡ review vi phạm) |
| **EventModeration** | Hồ sơ kiểm duyệt AI+Admin cho 1 target (Show/Livestream/GalleryImage/TourScene) | `AiScore`, `RiskLevel`, `AiRecommendation`, `AdminDecision`, `SlaDeadline` (NĐ 147/2024, 24h) |
| **Performance** | 1 lượt nghệ sĩ biểu diễn trong 1 show (line-up) | `Role` (Main/Guest/Host), `OrderIndex`, `SetTime`, `AcceptsDonation` (theo từng lượt diễn, không phải theo Performer) |
| **AiPosterGeneration** | Log mỗi lần thử tạo poster AI (không chỉ ảnh cuối) | `Status` (Succeeded/Failed), `Prompt`, `ErrorMessage` — chỉ `Succeeded` tính vào quota tháng |

### 1.5 Performer (nghệ sĩ)

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **Performer** | Catalog nghệ sĩ dùng chung mọi venue — **không có tài khoản đăng nhập** | `Type` (Solo/Band), `CreatedByUserId` (Owner nào tạo — cũng là người duy nhất sửa được cùng Admin) |
| **PerformerGenre** | Nối Performer ↔ MusicGenre (n-n) | — |
| **PerformerSocialLink** | Link mạng xã hội của performer | `Platform` (enum đóng: Spotify/Youtube/Soundcloud/Facebook/Instagram), upsert theo platform |

### 1.6 Ticket

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **TicketTier** | Hạng vé (VIP/Standard...) của 1 show | `AccessType` (Physical/Livestream), `TotalCapacity`, `ZoneId` (null = online) |
| **TicketPrice** | 1 đợt bán giá cụ thể của 1 tier | `Quota`, `SaleStart`/`SaleEnd`, `PurchaseChannel` (Online/Offline/Both), `Sold` (⚠️ xem §4) |
| **Ticket** | 1 vé thật, PK là `Guid` (chống đoán ID) | `Status` (state machine), `BuyerId` (nullable — null = khách walk-in không có tài khoản), `QrCode` (sinh khi Confirmed), `PendingTransferToUserId` (chuyển nhượng) |
| **TicketHold** | Giữ chỗ tạm trước khi thanh toán | `ExpiresAt` (mặc định 15 phút, config được), `IsReleased` |
| **PhysicalTicketDetail** | Chi tiết riêng cho vé vật lý — **1-1 shared PK với Ticket** (không có `Id` riêng, PK = `TicketId`) | `SeatInfo`, `SoldByStaffId`, `CheckedInAt`+`CheckedInByStaffId` |
| **LivestreamTicketDetail** | Chi tiết riêng cho vé xem online — **1-1 shared PK với Ticket** | `AccessToken` (⚠️ trường cũ từng vô nghĩa, đã vá bằng session-limit khác — xem [11 §Livestream](11-ba-domain-analysis.md)), `FirstAccessedAt`/`LastAccessedAt` |

### 1.7 Payment / Finance

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **Account** (bảng `ledger_accounts`) | Tài khoản sổ cái kế toán (không phải bank account) | `OwnerType` (Gateway/Platform/Tax/User/Performer), `OwnerId` (null cho 3 loại hệ thống) |
| **LedgerEntry** | 1 bút toán ghi sổ — **append-only**, `SUM(debit)=SUM(credit)` theo `JournalId` | `JournalId`, `IsDebit`, `ReferenceType`/`ReferenceId` (truy vết về nghiệp vụ gốc) |
| **Payment** | 1 giao dịch VNPay | `GrossAmount`/`GatewayFee`/`PlatformFee`/`TaxWithheld`/`NetAmount`, `Status`, `SettlementStatus`, `IdempotencyKey`, `Subscription*Snapshot` (4 field, chỉ có giá trị khi `ReferenceType="Subscription"`) |
| **Donation** | 1 lần khán giả donate cho 1 `Performance` | `Status` (state machine 5 bước), `PerformerShareRateSnapshot` (chốt tỷ lệ tại lúc xác nhận), `IsAnonymous`/`IsAmountPublic`/`IsMessagePublic`, `PaymentEvidenceUrl` (Owner chứng minh đã trả nghệ sĩ) |
| **Settlement** | 1 tranche tiền trả cho Owner từ doanh thu bán vé (không phải venue tự đặt cọc) | `ReleaseType` (Partial70 trả ngay ~T+48h / Final30 giữ lại lâu hơn ~T+14 ngày), `PreRateApplied` (% trả ngay, theo tier uy tín venue) / `PostRateApplied` = 1−`PreRateApplied` (% giữ lại làm bộ đệm an toàn), `Status` (có `PendingReview` — chỉ áp dụng tranche Final30, khi tỉ lệ thời lượng show thực tế/dự kiến < 70%) |
| **RefundRequest** | Yêu cầu hoàn tiền | `AmountRequested` ≠ `AmountApproved` (duyệt được 1 phần) |
| **BankAccount** | Tài khoản nhận tiền — polymorphic cho Lounge hoặc Performer | `OwnerType`+`OwnerId` (không FK thật), `IsDefault` (unique filtered index/owner), `IsVerified` |
| **SubscriptionPackage** | Gói dịch vụ cho Owner — **immutable khi đã có người subscribe** | `MaxTicketsPerEvent`, `HasAiPoster`+`MaxAiPostersPerMonth`, `MaxTourScenes` |
| **OwnerSubscription** | 1 lần Owner đăng ký gói | 4 field `*Snapshot` (chốt quyền lợi tại lúc đăng ký, không đổi ngược nếu Admin sửa gói gốc sau), `Status`, `ExpiresAt` (kéo dài nếu venue bị phạt Suspension) |

### 1.8 Livestream

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **Livestream** | 1 buổi phát trực tiếp gắn 1 show | `Status` (state machine, có `Terminated` khác `Ended`), `IsFree`, `ViewerCount`/`PeakViewerCount`/`TotalViews`, `TerminatedById`+`TerminatedReason` |
| **LivestreamChatMessage** | 1 tin nhắn chat trong luồng | `Message`, `SentAt` |

### 1.9 F&B

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **FnbMenu** | 1 menu của venue (có thể nhiều menu/venue) | `IsActive`, `DisplayOrder` |
| **FnbMenuItem** | 1 món trong menu | `Category` (tự do), `Price`, `IsAvailable` |
| **FnbOrder** | 1 đơn hàng | `Status` (state machine), `ShowId` (nullable — bán ngoài giờ diễn được), `AudienceUserId` (nullable) vs `StaffId` (Staff đặt hộ khách), `ZoneId`/`TableNote` |
| **OrderItem** | 1 dòng trong đơn | `UnitPrice` (snapshot lúc đặt, không tính lại nếu giá đổi), `Cancelled` |

### 1.10 Recommendation / Analytics

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **AiRecommendation** | 1 gợi ý show cho 1 user | `FinalScore` = f(`ContentScore`, `CollabScore`, `CustomScore`), `Algorithm`+`Reason` (giải thích được), `ExpiresAt` |
| **UserBehaviourLog** | 1 sự kiện hành vi (lưu 6 tháng) | `Action` (9 loại, xem §3), `DurationSeconds`, `Metadata` |
| **UserEventScore** | Ma trận điểm quan tâm User↔Show, tổng hợp từ `UserBehaviourLog` | `Score`, `Breakdown` (JSON: attended/rating/donated/wishlist/view) |
| **UserFavouriteGenre / UserFavouriteMood / UserFavouriteAtmosphere** | 3 bảng nối User ↔ taxonomy (n-n, sở thích tường minh) | — |

### 1.11 Notification

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **Notification** | 1 thông báo gửi 1 User | `Type` (25 giá trị), `ReferenceType`/`ReferenceId` (deep-link), `IsRead`, `SentAt` (null = chờ gửi FCM) |
| **DeviceToken** | 1 Firebase Installation ID (Fid) của 1 thiết bị | `Fid` (unique — device đổi chủ thì re-point sang UserId mới), `RegisteredAt`/`LastSeenAt` (⚠️ xem §4) |

### 1.12 Follow / Wishlist

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **Follow** | User theo dõi 1 Venue | — |
| **ShowWishlist** | User lưu quan tâm 1 Show | — |

### 1.13 Bảo mật & vận hành nội bộ (không phải nghiệp vụ hiển thị cho user)

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **LoginFailureLog** | Mọi lần đăng nhập sai (ngắn hạn — job tự dọn) | `Email`, `IpAddress` — phát hiện credential-stuffing xuyên nhiều tài khoản |
| **LoginSpikeAlertState** | Chống báo trùng cho `LoginSpikeDetectionJob`, key theo IP | `IpAddress`, `LastAlertedAt` |
| **KnownAdminSnapshot** | Baseline "Admin nào đã biết từ trước" cho job phát hiện Admin lạ xuất hiện | `UserId`, `FirstDetectedAt` — vì **không có API nào để thăng cấp Admin**, mọi Admin hiện tại đều được tạo bằng sửa DB trực tiếp |
| **PushFailureLog** | Lỗi gửi FCM thuộc về hệ thống push (không phải thiết bị 1 user chết) | `ErrorCode` |
| **PushFailureAlertState** | Chống báo trùng cho `PushFailureAlertJob` — 1 row toàn cục (không theo ai) | `LastAlertedAt` |

### 1.14 Complaint

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **Complaint** | Khiếu nại (NĐ 85/2021 — nền tảng phải là đầu mối tiếp nhận) | `ComplainantUserId` (nullable = khách vãng lai, dùng `ContactPhone` thay), `TargetType`+`TargetId`/`TargetGuid` (polymorphic, Ticket dùng Guid riêng), `Status`, `ResolvedAction` (có `TakeDownContent` — gỡ show vi phạm, hoàn 100% vé đã bán), `SlaDeadline` |

### 1.15 Cấu hình hệ thống

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **SystemConfig** | Toàn bộ tham số nghiệp vụ (D9: không hardcode) | `ConfigKey` (unique), `ConfigValue`, `DataType` |
| **SystemConfigHistory** | ⚠️ Xem [§4](#4-dead-data--missing-field) — thiết kế cho audit trail nhưng chưa có gì ghi vào | — |

### 1.16 Upload

| Entity | Mục đích nghiệp vụ | Field nghiệp vụ quan trọng |
|---|---|---|
| **UploadedFile** | Tracking file đã upload — biết ai upload để cho xoá đúng quyền | `UploaderUserId`, `Kind` (Image/Model3D) |

---

## 2. Quan hệ giữa các entity

Định dạng `Entity A (bên 1) — (bên n) Entity B: ý nghĩa`. Chỉ liệt kê quan hệ có ý nghĩa nghiệp vụ (bỏ FK thuần kỹ thuật kiểu `CreatedBy`).

### Venue & Show

- `User (1) — (n) MusicLounge`: 1 Owner sở hữu nhiều venue (`OwnerId`).
- `MusicLounge (1) — (n) LoungeStaff — (n) User`: n-n giữa User↔MusicLounge qua bảng trung gian, nhưng **ràng buộc unique filtered index chỉ cho 1 User active ở 1 venue tại 1 thời điểm** — về nghiệp vụ gần với 1-n hơn n-n thật.
- `MusicLounge (1) — (n) SeatingZone`: zone cấp venue, dùng lại cho mọi show của venue đó.
- `MusicLounge (1) — (n) VenueTourScene (1) — (n) VenueTourHotspot`: tour 360° lồng 2 cấp; `VenueTourHotspot (n) — (0..1) VenueTourScene` (qua `TargetSceneId`, chỉ khi `Type=Navigate`) tạo đồ thị điều hướng giữa các scene.
- `MusicLounge (1) — (n) VenueTourStitchAttempt (0..1) — (0..1) VenueTourScene`: 1 attempt có thể sinh ra tối đa 1 scene (`ResultSceneId`) nếu thành công.
- `MusicLounge (1) — (n) VenuePenalty`, `MusicLounge (1) — (n) LoungeGalleryImage`.
- `MusicLounge (1) — (n) LoungeShow`: 1 venue tổ chức nhiều show.
- `LoungeShow (1) — (n) TicketTier — (n) TicketPrice`: show → hạng vé → đợt giá, 3 cấp.
- `LoungeShow (1) — (n) Performance — (n) Performer`: n-n giữa Show↔Performer qua `Performance`, có thêm thuộc tính riêng của quan hệ (`Role`, `SetTime`, `AcceptsDonation`) — không phải bảng nối thuần.
- `LoungeShow (1) — (0..1) EventModeration`: 1 show có tối đa 1 hồ sơ kiểm duyệt đang mở (khoá tránh trùng — xem `ReviewShowCommandHandler`).
- `LoungeShow (1) — (n) AiPosterGeneration`: log mọi lần thử, không chỉ ảnh cuối.
- `LoungeShow (1) — (0..1) Livestream`: chỉ có khi `Format` là Online.
- `LoungeShow (n) — (n) MusicGenre/Mood/VenueAtmosphere`: qua 3 bảng nối riêng (`LoungeShowGenre`/`Mood`/`Atmosphere`).
- `LoungeShow (1) — (n) EventCustomValue — (n) CustomCriteria`: n-n Show↔CustomCriteria (tiêu chí do venue tự định nghĩa) có giá trị cụ thể trên quan hệ.

### Performer

- `User (0..1) — (n) Performer` (qua `CreatedByUserId`): Owner nào tạo performer — cũng là người duy nhất sửa được (cùng Admin).
- `Performer (n) — (n) MusicGenre` qua `PerformerGenre`.
- `Performer (1) — (n) PerformerSocialLink`.
- `Performer (1) — (n) BankAccount` (polymorphic, không FK thật — qua `OwnerType=Performer` + `OwnerId`).

### Ticket

- `TicketTier (0..1) — (n) SeatingZone`: null nếu tier online.
- `TicketPrice (1) — (n) Ticket`, `TicketPrice (1) — (n) TicketHold`.
- `Ticket (0..1) — (n) User` (qua `BuyerId`, nullable = khách walk-in không tài khoản).
- `Ticket (1) — (0..1) PhysicalTicketDetail`, `Ticket (1) — (0..1) LivestreamTicketDetail`: **1-1 thật** (shared primary key = `TicketId`, không phải FK thường), loại trừ lẫn nhau theo `TicketTier.AccessType`.
- `Ticket (0..1) — (1) Payment`: nhiều vé có thể cùng 1 Payment (mua combo/nhiều vé 1 lần).
- `Ticket (0..1) — (0..1) User` (qua `PendingTransferToUserId`): trạng thái chờ chuyển nhượng, tạm thời.

### Payment / Finance

- `Account (1) — (n) LedgerEntry`: 1 tài khoản sổ cái có nhiều bút toán.
- `Payment (1) — (n) LedgerEntry`: 1 giao dịch gateway sinh ra ≥1 bút toán kép (`PaymentId` chỉ set khi journal xuất phát từ 1 payment gateway, không phải mọi LedgerEntry đều có).
- `Payment (1) — (n) Ticket`: 1 payment có thể trả cho nhiều vé.
- `Payment (1) — (n) Settlement`: 1 payment có thể giải ngân nhiều đợt (2 tranche theo D3).
- `Performance (1) — (n) Donation`: donate luôn gắn 1 lượt diễn cụ thể, không gắn thẳng Performer hay Show.
- `Donation (0..1) — (1) BankAccount` (snapshot FK — tài khoản performer nhận tại thời điểm đó).
- `User (1) — (n) OwnerSubscription — (n) SubscriptionPackage`: n-n User↔Package qua OwnerSubscription, có snapshot quyền lợi trên quan hệ.
- `MusicLounge/Performer (1) — (n) BankAccount`: polymorphic qua `OwnerType`+`OwnerId`, **không có FK constraint thật ở tầng DB** — toàn vẹn dữ liệu phụ thuộc hoàn toàn vào tầng ứng dụng.

### Livestream

- `Livestream (1) — (n) LivestreamChatMessage — (n) User`.
- `Livestream (1) — (n) LivestreamTicketDetail`: qua `Ticket`, xác định ai được xem.

### F&B

- `MusicLounge (1) — (n) FnbMenu (1) — (n) FnbMenuItem`.
- `FnbOrder (1) — (n) OrderItem (n) — (1) FnbMenuItem`.
- `FnbOrder (0..1) — (n) LoungeShow`, `FnbOrder (0..1) — (n) User` (khách) và `(0..1) — (n) User` (staff đặt hộ) — 2 quan hệ độc lập tới cùng bảng User.

### Recommendation

- `User (1) — (n) UserBehaviourLog (n) — (1) LoungeShow`.
- `User (1) — (n) UserEventScore (n) — (1) LoungeShow`: ma trận tổng hợp, khác bảng log thô ở trên.
- `User (1) — (n) AiRecommendation (n) — (1) LoungeShow`.

### Notification / Complaint / Config

- `User (1) — (n) Notification`, `User (1) — (n) DeviceToken`.
- `User (0..1) — (n) Complaint` (nullable = khách vãng lai).
- `SystemConfig (1) — (n) SystemConfigHistory` (qua `ConfigKey`, không phải FK số).

---

## 3. Enum & state machine

### 3.1 State machine (có logic chuyển trạng thái xác nhận được trong code)

| Entity.Field | Giá trị đầy đủ | Điều kiện chuyển (tóm tắt, chi tiết xem doc 11) |
|---|---|---|
| `MusicLounge.Status` | `Pending, Approved, Rejected, Warned, Suspended, Locked` | Tạo mới → `Pending`. Admin approve/reject → `Approved`/`Rejected`. Bị phạt: Warning → `Warned` ngay; Suspension/Ban → `Suspended`/`Locked` khi tới `EffectiveAt` (qua job). Kháng cáo thắng → về `Approved`. |
| `LoungeShow.Status` | `Draft, Pending, Published, Ongoing, Ended, Cancelled` | Owner tạo → `Draft`. Nộp duyệt (cần venue Approved + ≥1 tier) → `Pending`. Admin duyệt → `Published`/về `Draft`. Start/End job → `Ongoing`/`Ended`. Huỷ → `Cancelled` (bất kỳ lúc nào trước Ended, theo chính sách hoàn vé riêng show). |
| `VenuePenalty.Status` | `Active, Appealed, Overturned, Upheld, Expired` | Issue → `Active`. Owner kháng cáo → `Appealed`. Admin review → `Overturned` (venue về Approved) hoặc `Upheld`. Tự hết hạn → `Expired`. |
| `TicketStatus` | `Pending, Confirmed, Used, Cancelled, Refunded` | Hold+Purchase → `Pending` → VNPay confirm → `Confirmed` (sinh QR). Check-in → `Used`. Huỷ/hoàn → `Cancelled`/`Refunded`. |
| `DonationStatus` | `PendingPayment, PendingOwnerAck, OwnerReceived, PerformerPaid, Cancelled, Refunded` | VNPay confirm → `PendingOwnerAck`. Owner ack (hoặc tự động sau 24h) → `OwnerReceived`. Owner xác nhận đã trả nghệ sĩ → `PerformerPaid`. VNPay fail/hết hạn → `Cancelled`. Admin hoàn tiền donor (chỉ được phép trước `PerformerPaid`) → `Refunded`. Sổ công khai `GET /donations/public` chỉ hiện `OwnerReceived`/`PerformerPaid` — loại `PendingOwnerAck` theo đúng pattern "pending vs posted" của ngành ngân hàng, vì trạng thái đó còn có thể bị `Refunded`/`Cancelled` trước khi thành bản ghi chốt. |
| `SettlementStatus` | `Scheduled, Released, Cancelled, PendingReview` | Tạo cùng lúc payment → `Scheduled`. Tới hạn → `Released`. Show huỷ/hoàn → `Cancelled`. Riêng tranche `Final30`: tỉ lệ `thời lượng thực tế / thời lượng dự kiến < 70%` (config `SettlementCompletionThresholdPct`, mặc định 0.70) → `PendingReview` thay vì tự release, chờ Admin (`SettlementReleaseJob.IsShowCompletionAcceptableAsync`). Tranche `Partial70` không bị check này, luôn release đúng lịch. |
| `LivestreamStatus` | `Scheduled, Live, Ended, Terminated` | Start/End job → `Live`/`Ended`. Admin/Owner buộc dừng → `Terminated` (khác `Ended` tự nhiên). |
| `FnbOrderStatus` | `Pending, Preparing, Served, Paid, Cancelled` | Staff cập nhật tuần tự qua `PUT /fnb-orders/{id}/status`. |
| `ComplaintStatus` | `Open, Investigating, Resolved, Rejected` | Admin xử lý thủ công. |
| `RefundRequestStatus` | `Pending, Approved, Rejected` | Admin xử lý, `AmountApproved` set khi Approved (có thể < `AmountRequested`). |
| `SubscriptionStatus` | `Active, Suspended, Expired, Cancelled` | Venue bị phạt Suspension → subscription `Suspended` + `ExpiresAt` kéo dài bù. Hết hạn tự nhiên → `Expired`. |
| `VenueTourStitchStatus` | `Pending, Succeeded, Failed` | Kết quả gọi microservice Python ghép ảnh. |
| `TourCompletionStatus` | `NotRequested, Succeeded, FailedKeptPartial` | Độc lập với `VenueTourStitchStatus` — bước AI gap-fill tuỳ chọn, fail bước này không làm fail attempt gốc. |
| `AiPosterGenerationStatus` | `Succeeded, Failed` | Chỉ `Succeeded` tính vào quota tháng. |
| `EventModeration` (`AdminDecision`) | `ModerationDecision`: `Approved, Rejected, Terminated` | `Terminated` chỉ dùng cho luồng Livestream (buộc dừng đang phát), không hợp lệ cho luồng Show. |
| `PaymentStatus` | `Pending, Confirmed, Failed, Refunded` | VNPay callback/IPN cập nhật. |
| `PaymentSettlementStatus` | `NotApplicable, Collected, PartiallyReleased, FullyReleased, Refunded` | Theo dõi tiến độ giải ngân của 1 Payment qua nhiều `Settlement` tranche. |

### 3.2 Enum phân loại tĩnh (không có "chuyển trạng thái", chỉ là lựa chọn cố định)

| Enum | Giá trị |
|---|---|
| `UserRole` | `Audience, Staff, Owner, Admin` |
| `PerformerType` | `Solo, Band` |
| `PerformerRole` | `Main, Guest, Host` |
| `AccessType` | `Physical, Livestream` |
| `PurchaseChannel` | `Online, Offline, Both` |
| `PaymentMethod` | `Gateway, Cash` |
| `AccountType` | `Gateway, Platform, Tax, User, Performer` |
| `BankAccountOwnerType` | `Lounge, Performer` |
| `PenaltyType` | `Warning, Suspension, Ban` |
| `ComplaintCategory` | `EventMisrepresentation, RefundDispute, DonationNotPaid, TechnicalIssue, VenueConduct, PenaltyAppeal, Other` |
| `ComplaintResolvedAction` | `Refund, IssueWarning, Dismiss, Compensate, TakeDownContent` |
| `ModerationTargetType` | `Show, Livestream, GalleryImage, TourScene` |
| `ModerationRiskLevel` | `Low, Medium, High, Critical` |
| `AiModerationRecommendation` | `SuggestApprove, NeedsReview, SuggestReject` |
| `LoungeShowFormat` | `Offline, Online` |
| `LivestreamPlaybackMode` | `TwoD, ThreeD` |
| `VenueTourHotspotType` | `Navigate, Info, LivestreamScreen` |
| `SubscriptionBillingCycle` | `Monthly, Quarterly, Yearly` |
| `ConfigDataType` | `Decimal, Integer, Boolean, String, Json` |
| `CustomCriteriaDataType` | `Select, Range, Boolean, Text` |
| `CustomPreferenceSource` | `Explicit, Learned` |
| `SocialPlatform` | `Spotify, Youtube, Soundcloud, Facebook, Instagram` |
| `UploadKind` | `Image, Model3D` |
| `BehaviourAction` | `ViewEvent, ViewEventLong, ViewLineup, ViewVenue, SearchGenre, WatchLivestream, ShareEvent, ClickTicket, ViewAfterWishlist` |
| `NotificationType` | 26 giá trị — xem [11 §9](11-ba-domain-analysis.md#9-domain-thông-báo-notification) |
| `SettlementReleaseType` | `Partial70, Final30` |

---

## 4. Dead data & missing field

### 4.1 Có trong schema nhưng không dùng ở code nghiệp vụ

| # | Đối tượng | Bằng chứng | Ghi chú |
|---|---|---|---|
| 1 | Entity **`LoungeImage`** (bảng, migration, Configuration đầy đủ) | Grep toàn `src/MusicLounge.Application` cho `LoungeImage` → 0 kết quả. `SetLoungeImageCommandHandler.cs` (tên gợi ý liên quan) thực chất chỉ set `MusicLounge.PrimaryImageUrl` (field chuỗi trên chính entity venue), không đụng tới bảng `LoungeImage` | Có vẻ bị thay thế bởi `LoungeGalleryImage` (đang dùng thật qua `AddGalleryImage`/`RemoveGalleryImage`) nhưng bảng cũ chưa được dọn |
| 2 | Entity **`SystemConfigHistory`** | Grep chỉ thấy trong migrations/DbContext/Configuration — không handler nào tạo row mới | Hạ tầng audit trail dựng sẵn cho 1 API ghi `SystemConfig` **chưa tồn tại** (`system_config` hiện chỉ đọc, không có endpoint ghi — xem [12 §2](12-actors-and-authorization.md)) |
| 3 | Field **`TicketPrice.Sold`** | Tự comment trong code: *"Legacy field — never written anywhere in the codebase (audited 2026-08-05), always 0. Do NOT read this for availability."* | Nguồn sự thật thật sự là tính live từ `Ticket`+`TicketHold` qua `ITicketRepository.GetReservedQuantitiesByPriceIdsAsync` |
| 4 | Enum **`ReleaseType`** (`Enums/ReleaseType.cs`, giá trị y hệt `SettlementReleaseType`) | Grep chỉ thấy `Settlement.ReleaseType` (tên **property**, kiểu thật là `SettlementReleaseType`) — không có chỗ nào dùng type `ReleaseType` độc lập | Bản nháp/duplicate bị bỏ lại, `SettlementReleaseType` mới là enum đang chạy thật |

### 4.2 Có logic nghiệp vụ nhưng thiếu/từng thiếu chỗ lưu — đã tìm thấy đang được vá trong working tree hiện tại

| # | Đối tượng | Trạng thái |
|---|---|---|
| 1 | `MusicLounge.ReputationScore` | Field đã tồn tại từ trước nhưng tự comment "written nowhere in this codebase, so it would always read 0". **Working tree hiện tại đã vá**: `ScheduleSettlementHandler.cs` giờ tính điểm live từ rating/số show và ghi ngược vào field này — dùng để xác định payout-speed tier (venue uy tín cao được giải ngân đợt đầu lớn hơn). |
| 2 | `DeviceToken.RegisteredAt`/`LastSeenAt` | Field đã có, nhưng comment tự nhận "the actual staleness sweep isn't implemented yet" — chưa có job nào dọn token cũ dựa trên 2 field này. Chưa vá (khác với ReputationScore ở trên). |

---

*Xem [11-ba-domain-analysis.md](11-ba-domain-analysis.md) cho nghiệp vụ theo domain, [12-actors-and-authorization.md](12-actors-and-authorization.md) cho actor/quyền hạn. File này chỉ tập trung mô hình dữ liệu.*
