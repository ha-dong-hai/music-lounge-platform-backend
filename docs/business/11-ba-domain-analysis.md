# 11 — Phân tích nghiệp vụ theo Domain (BA Handover)

← [10-schema-analysis.md](10-schema-analysis.md) · Xem thêm: [12-actors-and-authorization.md](12-actors-and-authorization.md) (actor/quyền hạn) · [13-data-model.md](13-data-model.md) (entity/quan hệ/state machine) · [14-usecase-traces.md](14-usecase-traces.md) (truy vết use-case mức Controller→Handler→Entity) · [15-risk-audit.md](15-risk-audit.md) (rủi ro phi chức năng — transaction, validate, phân quyền)

> **Phạm vi**: đọc trực tiếp mã nguồn (entity, migration ngầm định qua entity, controller/route, command/query handler, DTO, config) để mô tả lại nghiệp vụ đang **thực sự chạy**, không phải nghiệp vụ dự định ban đầu.
> **Phương pháp**: mọi nhận định đều trích dẫn file + dòng/hàm cụ thể. Mục nào không xác minh được ghi rõ trong "Chưa rõ / cần hỏi lại" thay vì suy đoán.
> **Thời điểm phân tích**: 2026-08-13, dựa trên **working tree hiện tại** (bao gồm các sửa đổi chưa commit), không phải bản commit gần nhất (`b521a0c`). Xem [§0.3](#03-ghi-chú-về-trạng-thái-repo).

---

## 0. Tổng quan hệ thống

### 0.1 Stack công nghệ

| Thành phần | Công nghệ | Bằng chứng |
|---|---|---|
| API chính | .NET 8, Clean Architecture 4 layer | `src/MusicLounge.{Domain,Application,Infrastructure,Api}` |
| Database | SQL Server qua EF Core 8 | `Microsoft.EntityFrameworkCore.SqlServer 8.0.11` |
| Xác thực | JWT Bearer | `Microsoft.AspNetCore.Authentication.JwtBearer`, `AuthController.cs` |
| CQRS / business logic | MediatR 12.4.1 + FluentValidation 11.11 | `src/MusicLounge.Application` |
| Background jobs | Hangfire (SQL Server storage) | config key `Hangfire` trong `appsettings.json` |
| Thanh toán | VNPay | config key `VnPay` |
| Video/Livestream | Mux | config key `Mux`, `LivestreamsController.cs` |
| Push notification | Firebase Cloud Messaging | `FirebaseAdmin 3.6.0` |
| Lưu trữ ảnh/file | Cloudflare | config key `Cloudflare` |
| AI | Gemini + OpenAI | config key `Gemini`, `OpenAi` |
| Gợi ý cá nhân hoá | ML.NET (Recommender) | `Microsoft.ML.Recommender` |
| Microservice riêng | Python/FastAPI — ghép ảnh 360° | `services/panorama-stitcher/` |

### 0.2 Vai trò người dùng

4 vai trò trong `UserRole` (`src/MusicLounge.Domain/Enums/UserRole.cs`): `Audience`, `Staff`, `Owner`, `Admin`. Không có vai trò "Performer" — nghệ sĩ là entity nghiệp vụ (`Performer.cs`), không có tài khoản đăng nhập riêng.

### 0.3 Ghi chú về trạng thái repo

`git status` cho thấy phần lớn Controllers và nhiều Application handler đang có sửa đổi **chưa commit** so với `b521a0c` (commit gần nhất). Tài liệu này mô tả theo **working tree hiện tại**, tức đã bao gồm các fix chưa commit (ví dụ luồng duyệt venue ở §2.2). Nếu đọc lại tài liệu này sau khi các thay đổi được commit hoặc rollback, cần đối chiếu lại.

`docs/architecture/00-project-structure.md` tự ghi "hiện tại 4 project đã scaffold, chưa có source files" — đã lỗi thời so với code thực tế (65 entity, 24 controller) — không dùng làm căn cứ.

---

## 1. Domain: Xác thực & Tài khoản người dùng (Auth & User)

**Entity chính**: `User.cs` (không phải `Account.cs` — xem §5).

**Đăng ký** (`RegisterCommandHandler.cs`):
- Tự đăng ký chỉ được chọn `Audience` hoặc `Owner` — validator chặn cứng (`RegisterCommandValidator.cs:26-28`).
- Tài khoản mới **chưa xác thực**, không cấp token ngay — gửi OTP 6 số qua email (10 phút), phải `POST /auth/verify-email` mới nhận token đăng nhập lần đầu.
- Bắt buộc `AcceptTerms=true`, lưu `TermsAcceptedAt` + `TermsVersion` (Luật 91/2025/QH15).

**Đăng nhập** (`LoginCommandHandler.cs`) — 3 lớp phòng vệ:
1. Khoá tạm sau nhiều lần sai (`IAuthAttemptTracker`), báo số phút còn lại.
2. Chống dò thời gian phản hồi để không lộ email đã đăng ký (dummy hash khi email không tồn tại).
3. Mỗi lần sai ghi vào `LoginFailureLog` kèm IP — phục vụ phát hiện tấn công hàng loạt.
- Chặn đăng nhập nếu `IsActive=false` hoặc email chưa xác thực.
- JWT của `Staff` gắn kèm `loungeId` đang được phân công (dòng 98-104) — giới hạn thao tác trong 1 venue.

**Tự phục vụ tài khoản** (`/me/*`, `MeController.cs`):
- Đổi mật khẩu/email (2 bước OTP), KYC (nộp CCCD/CMND + ảnh 2 mặt).
- DSAR (91/2025/QH15): `GET /me/data-export` (xuất ngay), `POST /me/data-erasure` (xoá danh tính vĩnh viễn, giữ lịch sử vé/thanh toán ẩn danh) — phân biệt với `DELETE /me` (chỉ tạm khoá, khôi phục được).
- Xác thực số điện thoại OTP (NĐ 147/2024).
- `GET /me/earnings` chỉ dành `Owner`.

**Gán Staff cho venue** thuộc domain Venue (§2), không phải Auth: `Staffing/Commands/AssignStaff`.

**Đã xác nhận: tài khoản `Admin` không có API tạo/thăng cấp nào cả** — comment tự ghi trong `KnownAdminSnapshot.cs:4-6` ("There is currently no API path to promote a user to Admin at all, so every admin today only exists via a direct database edit"). `KnownAdminSnapshot` tồn tại chính là để `AdminRoleDriftDetectionJob` phát hiện 1 Admin "mới xuất hiện" (dấu hiệu tấn công) khác với Admin đã biết từ trước — do không có endpoint chính thức nào tạo ra Admin để đối chiếu.

> Bảng tra cứu đầy đủ actor/role/policy/JWT claims/`[Authorize]` theo từng endpoint, trạng thái tài khoản đặc biệt, và các actor có biến thể dữ liệu (audience đăng ký vs walk-in, complaint có tài khoản vs khách vãng lai...): xem [12-actors-and-authorization.md](12-actors-and-authorization.md).

---

## 2. Domain: Venue / Lounge (Phòng trà)

**Entity chính**: `MusicLounge.cs` — có `BusinessLicenseUrl`, `Model3DUrl` (.glb, khác tour ảnh 360°), `ReputationScore` (0–5, ngưỡng 3.5/4.2 — quyết định tốc độ giải ngân, xem §2.4.1).

### 2.1 Vòng đời trạng thái venue

`LoungeStatus`: `Pending, Approved, Rejected, Warned, Suspended, Locked` (`LoungeStatus.cs`).

### 2.2 Duyệt venue mới (đã có, đã xác nhận với chủ dự án là chủ đích + đã implement)

- Venue mới mặc định `Pending` (`CreateLoungeCommandHandler.cs` không set Status).
- Admin duyệt: `POST /admin/lounges/{id}/approve` → `Approved`, hoặc `POST /admin/lounges/{id}/reject` → `Rejected` (`AdminController.cs:60-80`, `ApproveLoungeCommandHandler.cs`, `RejectLoungeCommandHandler.cs`). Danh sách chờ: `GET /admin/lounges/pending`.
- **Gate chặn hoạt động khi chưa duyệt**: `PublishLoungeShowCommandHandler.cs:58-65` (BR-01) — venue `Pending` hoặc `Rejected` không thể nộp duyệt show mới (vẫn tạo Draft được); venue `Suspended`/`Locked` cũng bị chặn tương tự (§6.8).
- Thông báo kết quả: `NotificationType.VenueApproved` / `VenueRejected`.

### 2.3 Xử phạt venue (§6.8)

3 mức (`IssuePenaltyCommandHandler.cs`): `Warning` (hiệu lực ngay, venue vẫn hoạt động) → `Suspension` (báo trước, mặc định 24h, config được) → `Ban` (báo trước mặc định 7 ngày). Show đã Published trước đó không bị ảnh hưởng khi venue bị phạt sau này. Owner kháng cáo được; nếu Admin không xử lý trong hạn, hệ thống **tự động** approve lại (`AutoApproveOverdueAppealsJob.cs`).

### 2.4 Gói subscription (đòn bẩy kinh doanh của Owner)

`SubscriptionPackage.cs` (comment D12: "Immutable when owners are subscribed — create new version instead of editing"): định nghĩa `MaxTicketsPerEvent`, `HasAiPoster` + `MaxAiPostersPerMonth`, `MaxTourScenes`. **Snapshot tại thời điểm đăng ký** vào `OwnerSubscription` — sửa gói gốc sau này không ảnh hưởng ngược Owner đang subscribe.

#### 2.4.1 `ReputationScore` quyết định % tiền bán vé bị giữ lại làm bộ đệm an toàn (D3, D16)

`ScheduleSettlementHandler.cs:141-167` (`ResolveTierPreRateAsync`) — khi lên lịch trả tiền vé cho Owner, hệ thống tính điểm uy tín live từ rating trung bình (loại review đã bị gỡ) + số show đã `Ended`, rồi xếp venue vào 1 trong 3 tier. **Lưu ý thuật ngữ**: đây không phải khoản Owner tự bỏ tiền đặt cọc — là phần doanh thu bán vé (tiền khán giả đã trả qua VNPay, nền tảng đang giữ hộ) mà hệ thống khoan chưa trả, đóng vai trò như 1 khoản đặt cọc an toàn nhưng chiều dòng tiền ngược lại:

| Tier | Điều kiện | % trả ngay (đợt 1, `PreRateApplied`) | **% giữ lại (đợt 2, `PostRateApplied`)** |
|---|---|---|---|
| Mới | `score < 3.5` | 50% | **50%** |
| Standard | `score ≥ 3.5` | 70% | **30%** |
| Premium | `score ≥ 4.2` **và** ≥10 show đã kết thúc | 80% | **20%** |

Venue càng uy tín + nhiều kinh nghiệm càng được giữ lại ít hơn (rủi ro thấp) và nhận tiền nhanh hơn. `MusicLounge.ReputationScore` được ghi ngược lại làm cache hiển thị mỗi lần tính — trước đây field này tồn tại nhưng không nơi nào ghi vào (luôn đọc ra 0), đã được vá trong working tree hiện tại (xem [13-data-model.md §4.2](13-data-model.md#42-có-logic-nghiệp-vụ-nhưng-thiếu-chỗ-lưu--đã-tìm-thấy-đang-được-vá-trong-working-tree-hiện-tại)).

**Khoản giữ lại (đợt 2) không tự động giải ngân nếu show bất thường** — `SettlementReleaseJob.cs` (`IsShowCompletionAcceptableAsync`) kiểm tra `thời lượng thực tế / thời lượng dự kiến ≥ 70%` (config `SettlementCompletionThresholdPct`, mặc định 70%, câu hỏi #8 cũ đã được trả lời tại đây) trước khi giải ngân đợt 2 — nếu show kết thúc sớm bất thường (dấu hiệu huỷ ngang/lừa đảo), khoản giữ lại bị đóng băng thành `SettlementStatus.PendingReview`, chờ Admin quyết định thủ công thay vì tự động trả. Kiểm tra này **chỉ áp dụng cho đợt 2** — đợt 1 vẫn giải ngân đúng lịch (mặc định T+48h sau show) bất kể show diễn ra thế nào.

### 2.5 Staff & phân quyền venue

`POST /lounges/{id}/staff` (`AssignStaffCommandHandler.cs`) — Owner gán User làm Staff cho venue mình; JWT Staff mang `loungeId` (§1).

### 2.6 Sơ đồ mặt bằng

`SeatingZone.cs` — zone cấp venue (VIP/Standard/Bar Area), dùng lại cho nhiều show, có `Capacity` (an toàn phòng cháy chữa cháy, không chỉ số liệu bán vé) + toạ độ layout 2D và 3D.

### 2.7 Tour ảo 360°

Venue-scoped, gated theo `MaxTourScenes` của gói subscription, tách biệt với `Model3DUrl`. Ghép ảnh qua microservice Python riêng (`services/panorama-stitcher/`).

**Chưa rõ / cần hỏi lại**:
- ~~Vai trò nào được phép gán làm Staff.~~ Phần lớn đã trả lời — xem [14-usecase-traces.md §7.4](14-usecase-traces.md#74-gán--gỡ-staff-cho-venue): chỉ `Audience` được tự động đổi thành `Staff`; hành vi khi gán 1 `Owner` làm Staff (Role có đổi hay không) vẫn còn mở.

---

## 3. Domain: Chương trình biểu diễn (Show/Event)

**Vòng đời** (`LoungeShowStatus.cs`): `Draft → Pending → Published → Ongoing → Ended`, hoặc `Cancelled`.

1. **Draft**: Owner tạo (`CreateLoungeShowCommandHandler.cs`).
2. **Draft → Pending**: Owner nộp duyệt (`PublishLoungeShowCommandHandler.cs`) — venue phải đã Approved, show phải có ít nhất 1 `TicketTier`.
3. **Pending → Published/Draft**: Admin duyệt qua `POST /moderations/shows/{id}/review` (`ReviewShowCommandHandler.cs`) — `Approved` → `Published` + thông báo follower; `Rejected` → về `Draft` để sửa.
4. **Ongoing/Ended**: qua `StartLoungeShowCommand`/`EndLoungeShowCommand`.

**Kiểm duyệt 2 lớp (AI + Admin)**: `EventModeration.cs` — AI chấm điểm rủi ro trước (`AiScore`, `RiskLevel`, `AiRecommendation`), Admin quyết định cuối. SLA 24h bắt buộc (NĐ 147/2024, `SlaDeadline` từ system_config).

**Duyệt nội dung = duyệt pháp lý luôn** (`ReviewShowCommandHandler.cs:73-79`, D18): quyết định `Approved` đồng thời xác nhận văn bản chấp thuận biểu diễn (`LegalApprovalReference`, NĐ 144/2020/NĐ-CP Điều 8-10) — không tách quy trình riêng.

**Chống race-condition**: cả `PublishLoungeShowCommandHandler` và `ReviewShowCommandHandler` dùng lock theo `moderation:show:{showId}`.

**2 nghĩa vụ pháp lý riêng biệt**: `LegalApprovalReference` (D18, xác nhận trước Published) khác `VcpmcRoyaltyReference` (D19, phí tác quyền VCPMC/RIAV, kiểm tra lúc show bắt đầu).

**2 định dạng**: `Offline`, `Online` — không có `Hybrid`; nghiên cứu đối chiếu Eventbrite/Luma/Zoom Events cho thấy không nền tảng nào coi hybrid là 1 lựa chọn ngang hàng thật sự (luôn là 2 sự kiện riêng, hoặc ghép từ 2 loại vé/session độc lập) — 1 show ở đây chỉ bán 1 trong 2 loại vé (Physical hoặc Livestream), `CreateTicketTierCommandValidator`/`UpdateTicketTierCommandValidator` chặn cứng việc trộn.

**Line-up nghệ sĩ**: `Performance.cs` — nối `LoungeShow` ↔ `Performer`, có `OrderIndex`/`SetTime`/`Role`, và `AcceptsDonation` theo từng lượt diễn.

**AI Poster** (sub-flow, gated theo subscription — xem `GeneratePosterCommandHandler.cs`):
- `HasAiPosterSnapshot` — bật/tắt tính năng theo gói.
- `MaxAiPostersPerMonthSnapshot` — quota tháng, chỉ tính lần `Succeeded`.
- `ai_poster_max_attempts_per_show` (system_config) — chống lạm dụng, tính mọi lần thử kể cả fail, riêng theo từng show.
- Kết quả ghi vào `show.PosterUrl` + `show.PosterByAi = true`.

**Chính sách huỷ/hoàn vé nằm trên từng show**: `CancellationAllowed`, `CancellationDeadlineHours`, `RefundPercentage`.

**Chưa rõ / cần hỏi lại**:
- Chưa đọc `StartLoungeShowCommandHandler`/`EndLoungeShowCommandHandler` chi tiết và tác động tới `RatingOpenUntil` (§6.13: mở đánh giá 7 ngày sau kết thúc).
- ~~Điều kiện set `PlaybackMode = ThreeD` có gate theo subscription không.~~ ✅ Đã trả lời — xem [14-usecase-traces.md §8.5](14-usecase-traces.md#85-đổi-chế-độ-phát-2d3d): **không** gate theo subscription, chỉ yêu cầu `show.Format != Offline`.

---

## 4. Domain: Vé (Ticketing)

**Cấu trúc**: `TicketTier` (hạng vé, gắn `SeatingZone` nếu vật lý) → `TicketPrice` (đợt bán, `SaleStart/SaleEnd`, `Quota` riêng) → `Ticket` (khoá `Guid`).

**Trạng thái vé** (`TicketStatus.cs`): `Pending → Confirmed → Used`, hoặc `Cancelled`/`Refunded`. QR chỉ sinh khi `Confirmed`.

**Giữ chỗ tạm thời**: `POST /tickets/holds` (`HoldTicketCommandHandler.cs`) tạo `TicketHold` hết hạn sau 15 phút (config).

**5 lớp kiểm tra sức chứa trước khi giữ chỗ** (`ValidateQuotaAsync`):
1. Quota đợt bán cụ thể.
2. Sức chứa tổng hạng vé (`TotalCapacity`).
3. **Sức chứa thật của khu vực vật lý** (§6.11) — chặn 2 tier khác nhau cùng zone cộng lại vượt sức chứa phòng thật (an toàn vật lý, không chỉ logic bán hàng).
4. Quota show theo kênh (`OfflineQuota`/`OnlineQuota`).
5. Hạn mức vé/event theo gói subscription (`MaxTicketsPerEventSnapshot`) — check lại ở bước giữ chỗ để không bị lách qua field tuỳ chọn.

Toàn bộ khoá theo `IShowBookingLock` (per-show semaphore) tránh oversell khi nhiều người tranh mua cùng lúc.

**2 kênh bán**: `POST /tickets/purchase` (online) và `POST /tickets/walk-in` (tại quầy — không hoa hồng nền tảng mặc định, mô hình thu tiền qua subscription Owner).

**Check-in QR** (`CheckInTicketCommandHandler.cs`): khoá theo mã QR chặn double check-in; chỉ check-in được khi show `Ongoing`, vé `Physical`, `Confirmed`, chưa check-in, không đang chuyển nhượng. Không có offline fallback nếu mất mạng.

**Chuyển nhượng vé**: `initiate → accept/cancel`, vé đang chờ chuyển nhượng không check-in được.

**Chưa rõ / cần hỏi lại**: chưa đọc `PurchaseTicketCommandHandler` chi tiết (khớp nối với domain Payment §5).

---

## 5. Domain: Thanh toán & Tài chính (Payment/Finance)

**Sổ cái kế toán kép**: `Account.cs` (comment "D8: Logic ledger account — NOT a bank account") có 5 loại (`AccountType.cs`): `Gateway, Platform, Tax, User, Performer`. `LedgerEntry.cs` **append-only**, `SUM(debit) == SUM(credit)` theo mỗi `JournalId`.

**Payment (VNPay)**: `GrossAmount − GatewayFee − PlatformFee − TaxWithheld = NetAmount`. Có `IdempotencyKey` chống double-charge. **Snapshot quyền lợi gói subscription tại checkout** — Admin sửa gói giữa lúc thanh toán không ảnh hưởng ngược. 2 callback công khai bắt buộc: `GET /payments/vnpay/callback` (redirect) và `GET /payments/vnpay/ipn` (server-to-server, nguồn sự thật).

**Donation — 6 trạng thái** (`DonationStatus.cs`): `PendingPayment → PendingOwnerAck → OwnerReceived` (có `AutoConfirmed` nếu Owner không thao tác trong 24h) `→ PerformerPaid` (kèm `PaymentEvidenceUrl`), hoặc `Cancelled`/`Refunded` (chỉ hoàn được trước `PerformerPaid`). Tỷ lệ chia nghệ sĩ (`PerformerShareRateSnapshot`) chốt tại thời điểm xác nhận thanh toán, mặc định 88%/2% (configurable). Có tuỳ chọn ẩn danh (`IsAnonymous`), ẩn số tiền (`IsAmountPublic`), ẩn lời nhắn (`IsMessagePublic`) — 3 cờ độc lập nhau, không ràng buộc nhau.

**Minh bạch công khai (thêm 2026-08-13)** — 2 kênh song song, cố ý lệch pha: `PublicDonationHub` (SignalR realtime) bắn alert ở **cả 3 mốc tiền thật sự di chuyển** (VNPay confirm, Owner ack, trả nghệ sĩ); còn sổ `GET /donations/public` (toàn hệ thống, mọi nghệ sĩ — khác `GET /performers/{id}/donations` theo riêng 1 nghệ sĩ) chỉ ghi nhận từ `OwnerReceived` trở đi — đúng pattern "pending vs posted" ngành ngân hàng (giao dịch pending còn có thể bị hoàn/huỷ nên chưa vào sổ chính thức). Donor giờ cũng được báo riêng tư khi thanh toán thành công (`NotificationType.DonationConfirmed`) — trước đây chỉ Owner được báo, donor chỉ biết qua redirect 1 lần (mất nếu đóng tab) hoặc tự vào `GET /donations/my` xem.

**Settlement (Owner nhận tiền vé) — 2 đợt**: `Partial70` + `Final30`, tỷ lệ snapshot tại thời điểm settlement. Trạng thái `PendingReview` (D16): nếu thời lượng show thực tế ngắn hơn ngưỡng cho phép (dấu hiệu gian lận), đợt cuối **không tự động release**, chờ Admin quyết định.

**RefundRequest**: `AmountRequested` tách biệt `AmountApproved` — Admin duyệt được một phần, không phải toàn-hoặc-không.

**Chưa rõ / cần hỏi lại**:
- Chưa trace handler xử lý VNPay IPN để xác nhận chữ ký callback được verify đúng cách.
- Ngưỡng "actual_duration" kích hoạt `PendingReview` là bao nhiêu.

---

## 6. Domain: Livestream

**Trạng thái** (`LivestreamStatus.cs`): `Scheduled → Live → Ended`, hoặc `Terminated` (bị buộc dừng, có `TerminatedById`/`TerminatedReason`, khác `Ended` tự nhiên).

**Kiểm soát truy cập dùng chung 1 policy** (`LivestreamAccessPolicy.cs`, gộp từ 3-4 nơi lặp code trước đó):
- Admin: luôn xem được.
- Staff/Owner: chỉ venue mình vận hành (fix lỗi cũ: Staff venue A từng xem được stream trả phí venue B).
- Người dùng thường: phải là chủ vé thật (`isGenuineTicketHolder` — phân biệt với vai trò vận hành vì chỉ lượt xem thật mới tính tín hiệu gợi ý).

**Giới hạn thiết bị xem cùng lúc** (`LivestreamHub.cs:76-88`): field `AccessToken` trước đây tồn tại nhưng chưa bao giờ được kiểm tra thật — đã vá bằng `MaxConcurrentLivestreamSessionsPerTicket` (config), chỉ áp dụng khán giả có vé thật.

**Đếm viewer an toàn dưới tải**: `ExecuteUpdateAsync` tăng `ViewerCount` nguyên tử tại DB, tránh mất đếm khi nhiều người vào cùng lúc.

**Chưa rõ / cần hỏi lại**: giá trị hiện tại của `MaxConcurrentLivestreamSessionsPerTicket`; cơ chế ghi `RecordingUrl` (replay/VOD) — đã biết trước đây là tính năng hoãn lại theo yêu cầu, không phải thiếu sót.

---

## 7. Domain: F&B (Đồ ăn/thức uống)

**Cấu trúc**: `FnbMenu` (nhiều menu/venue) → `FnbMenuItem` → `FnbOrder` → `OrderItem` (`UnitPrice` snapshot lúc đặt, không tính lại nếu giá đổi sau).

**Trạng thái đơn** (`FnbOrderStatus.cs`): `Pending → Preparing → Served → Paid`, hoặc `Cancelled`.

**2 cách tạo đơn**: khán giả tự đặt hoặc Staff đặt hộ tại quầy/bàn (`ZoneId`/`TableNote`).

**Không bắt buộc gắn show**: `ShowId` nullable — venue bán F&B ngoài giờ diễn được.

---

## 8. Domain: Gợi ý cá nhân hoá & Phân tích (Recommendation/Analytics)

**Mô hình lai 3 thành phần** (`AiRecommendation.cs`): `ContentScore` (nội dung) + `CollabScore` (lọc cộng tác, ML.NET) + `CustomScore` (tiêu chí riêng venue) → `FinalScore`, kèm `Algorithm`/`Reason` (giải thích được) và `ExpiresAt`.

**Nguồn hành vi**: `UserBehaviourLog.cs` — 9 loại hành vi (`BehaviourAction.cs`), lưu 6 tháng rồi tổng hợp vào `UserEventScore.cs` (breakdown JSON: `attended, rating, donated, wishlist, view`).

**Tiêu chí gợi ý riêng theo venue** (`CustomCriteria.cs`, comment "each venue defines its own criteria"): Owner tự tạo tiêu chí (vd "Ngôn ngữ biểu diễn"), show gắn giá trị (`EventCustomValue`), user set mức quan tâm riêng (`UserCustomPreference`) — cơ chế mở rộng ngoài genre/mood/atmosphere cứng của hệ thống.

**Analytics**: `GET /analytics/my-lounge` (Owner) và `GET /analytics/platform` (✅ xác nhận Admin-only, `RequireAdmin`).

**Đã trả lời đầy đủ ở [14-usecase-traces.md §10](14-usecase-traces.md#10-recommendationanalytics)**: công thức `FinalScore = ContentScore×0.5 + CollabScore×0.3 + CustomScore×0.2 + 0.15 (nếu follow venue)`, chỉ áp dụng khi user có ≥5 dòng `UserBehaviourLog` (dưới ngưỡng đó dùng content-based thuần); và gợi ý cá nhân hoá bị tắt hoàn toàn (fallback Trending) nếu `User.AiConsent=false`.

---

## 9. Domain: Thông báo (Notification)

26 loại (`NotificationType.cs`) phủ mọi domain — mới nhất là `DonationConfirmed` (2026-08-13, donor-facing, riêng tư — báo donor chính chủ donate của họ đã thanh toán thành công, đối xứng với `DonationReceived` vốn chỉ báo Owner). Đáng chú ý nhóm **cảnh báo vận hành nội bộ cho Admin**:
- `DuplicatePaymentDetected` — Owner bấm Subscribe 2 lần, cả 2 giao dịch VNPay đều thành công.
- `ModerationSlaBreached` — nội dung gắn cờ quá hạn 24h (NĐ 147/2024) chưa ai duyệt.
- `SecurityAlert` — dò mật khẩu hàng loạt, hoặc Admin mới bất thường.
- `SystemHealthAlert` — sự cố vận hành (không phải bảo mật), vd FCM push lỗi.
- `PaymentReconciliationMismatch` — đối soát VNPay (`querydr`) không khớp `Payment.Status` (có `VnPayReconciliationJob` chạy định kỳ).

**Chưa rõ / cần hỏi lại**: các loại cảnh báo vận hành trên gửi tới `UserId` cụ thể nào (1 Admin cố định hay mọi Admin).

---

## 10. Domain: Theo dõi & Yêu thích (Follow/Wishlist)

- **Follow**: User theo dõi Venue → nhận `NewEvent` khi venue có show mới được duyệt.
- **Wishlist**: User lưu quan tâm 1 Show → nhận `WishlistLowStock` khi vé sắp hết; là 1 trong 5 tín hiệu cấu thành `UserEventScore.Breakdown`.

Không có quy tắc nghiệp vụ phức tạp.

---

## 11. Tổng hợp "Chưa rõ / cần hỏi lại"

| # | Domain | Câu hỏi | Trạng thái |
|---|---|---|---|
| ~~1~~ | ~~Auth~~ | ~~Tài khoản `Admin` được tạo bằng cách nào?~~ | ✅ Đã trả lời — xem §1, không có API, chỉ sửa DB trực tiếp |
| ~~2~~ | ~~Venue~~ | ~~Ngưỡng `ReputationScore` 3.5/4.2 dùng để làm gì?~~ | ✅ Đã trả lời — xem §2.4.1, quyết định tier tốc độ giải ngân |
| 3 | Venue | Vai trò nào được phép gán làm Staff? | Phần lớn đã trả lời — xem §2.5, chỉ `Audience` auto-đổi role |
| ~~4~~ | ~~Show~~ | ~~Điều kiện chuyển `Ongoing`/`Ended` và tác động tới `RatingOpenUntil`?~~ | ✅ Đã trả lời — xem [14-usecase-traces.md §2.2](14-usecase-traces.md#22-bắt-đầu-phát)/[§2.2.1](14-usecase-traces.md#221-kết-thúc-phát), 2 cặp lệnh loại trừ nhau (có/không livestream), `RatingOpenUntil = ActualEnd + 7 ngày` |
| ~~5~~ | ~~Show~~ | ~~Set `PlaybackMode = ThreeD` có gate theo subscription không?~~ | ✅ Đã trả lời — không, chỉ yêu cầu `Format != Offline` |
| ~~6~~ | ~~Ticket~~ | ~~Chi tiết `PurchaseTicketCommandHandler` (chuyển Pending→Confirmed)?~~ | ✅ Đã trả lời — xem [14-usecase-traces.md §1.3](14-usecase-traces.md#13-mua-vé--khởi-tạo-thanh-toán)/[§1.4](14-usecase-traces.md#14-xác-nhận-thanh-toán-vnpay-callbackipn) |
| ~~7~~ | ~~Payment~~ | ~~Chữ ký callback VNPay IPN được verify ra sao?~~ | ✅ Đã trả lời — xem [14-usecase-traces.md §1.4](14-usecase-traces.md#14-xác-nhận-thanh-toán-vnpay-callbackipn), 2 lớp: verify HMAC + đối chiếu Amount, fail-closed |
| ~~8~~ | ~~Payment~~ | ~~Ngưỡng "actual_duration" kích hoạt `Settlement.PendingReview` là bao nhiêu?~~ | ✅ Đã trả lời — xem §2.4.1, 70% (`SettlementCompletionThresholdPct`) |
| ~~9~~ | ~~Livestream~~ | ~~Giá trị hiện tại của `MaxConcurrentLivestreamSessionsPerTicket`?~~ | ✅ Đã trả lời — **2**, `LivestreamSettings.cs:12` + `appsettings.json:71` |
| ~~10~~ | ~~Recommendation~~ | ~~Công thức trọng số `FinalScore`?~~ | ✅ Đã trả lời — 0.5/0.3/0.2 + boost 0.15, xem §8 |
| ~~11~~ | ~~Recommendation~~ | ~~Phân quyền chính xác `GET /analytics/platform`?~~ | ✅ Đã trả lời — `RequireAdmin` (chỉ Admin) |
| ~~12~~ | ~~Notification~~ | ~~Cảnh báo vận hành nội bộ gửi tới `UserId` nào?~~ | ✅ Đã trả lời — **mọi Admin hiện có** (`Role==Admin`, lặp gửi từng người), không phải 1 Admin cố định — xem 4 job liên quan trong [14-usecase-traces.md §11](14-usecase-traces.md#11-notification) |

---

*Tài liệu này được tạo bằng cách đọc trực tiếp mã nguồn (không dựa trên tài liệu yêu cầu gốc), phục vụ mục đích bàn giao/đối chiếu nghiệp vụ. Xem [10-schema-analysis.md](10-schema-analysis.md) để đối chiếu schema database.*
