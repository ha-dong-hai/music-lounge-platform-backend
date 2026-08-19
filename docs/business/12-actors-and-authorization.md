# 12 — Actor, Role & Authorization Reference

← [11-ba-domain-analysis.md](11-ba-domain-analysis.md) (§1 Auth & Tài khoản) · Xem thêm: [13-data-model.md](13-data-model.md) (§1.1 entity `User` — field nghiệp vụ đầy đủ)

> **Mục đích**: bảng tra cứu (reference), không phải bài viết tường thuật — dùng khi cần trả lời nhanh "Actor X có gọi được endpoint Y không", hoặc khi thêm 1 actor/policy mới cần biết chỗ nào phải sửa đồng bộ.
> **Phương pháp**: đọc trực tiếp `Program.cs` (khai báo policy), `[Authorize]` trên từng controller, các helper phân quyền (`VenueOperatorAccess`, `ICurrentUserService`), và entity liên quan. Mọi dòng đều trích file/dòng cụ thể.
> **Cập nhật lần cuối**: 2026-08-13, dựa trên working tree hiện tại (xem [11-ba-domain-analysis.md §0.3](11-ba-domain-analysis.md#03-ghi-chú-về-trạng-thái-repo)).

## Mục lục

1. [Cơ chế phân quyền nền (đọc trước khi đọc phần còn lại)](#1-cơ-chế-phân-quyền-nền)
2. [Bảng tra cứu Actor](#2-bảng-tra-cứu-actor)
3. [Trạng thái tài khoản đặc biệt](#3-trạng-thái-tài-khoản-đặc-biệt)
4. [Actor có nhiều biến thể dữ liệu](#4-actor-có-nhiều-biến-thể-dữ-liệu)
5. [Vấn đề đã biết](#5-vấn-đề-đã-biết)
6. [Chưa rõ / cần xác nhận](#6-chưa-rõ--cần-xác-nhận)
7. [Khi thêm actor/policy mới — checklist](#7-khi-thêm-actorpolicy-mới--checklist)

---

## 1. Cơ chế phân quyền nền

3 lớp, theo thứ tự kiểm tra:

**Lớp 1 — Role** (`src/MusicLounge.Domain/Enums/UserRole.cs`): `Audience, Staff, Owner, Admin`. Ghi vào JWT claim `Role` lúc login (`src/MusicLounge.Infrastructure/Security/JwtTokenService.cs:22-30`).

**Lớp 2 — Policy** (`src/MusicLounge.Api/Program.cs:217-221`) — ánh xạ N role → 1 policy. Toàn bộ Controllers dùng `[Authorize(Policy = Policies.X)]`, **không có nơi nào dùng `[Authorize(Roles=...)]` trực tiếp**:

| Policy | Role được chấp nhận | Định nghĩa |
|---|---|---|
| `RequireAuthenticated` | Bất kỳ role đã đăng nhập | `Program.cs:217` |
| `RequireStaff` | `Staff`, `Admin` | `Program.cs:218` — ⚠️ xem [§5](#5-vấn-đề-đã-biết), không dùng ở đâu |
| `RequireVenueOperator` | `Staff`, `Owner`, `Admin` | `Program.cs:219` |
| `RequireOwner` | `Owner`, `Admin` | `Program.cs:220` |
| `RequireAdmin` | `Admin` | `Program.cs:221` |

**Lớp 3 — Venue scoping** (`src/MusicLounge.Application/Common/VenueOperatorAccess.cs`) — Policy chỉ chứng minh *role*, không chứng minh *venue nào*. Mọi handler thao tác 1 venue cụ thể phải tự gọi thêm:

```
CanOperate(currentUser, loungeId, loungeOwnerId) =
    Role == Admin
    OR (Role == Staff AND currentUser.LoungeId == loungeId)   // từ JWT claim "lounge_id"
    OR (Role == Owner AND currentUser.UserId == loungeOwnerId)
```

`lounge_id` chỉ được gắn vào JWT khi login **nếu** Role = Staff, lấy từ assignment `IsActive=true` (`LoginCommandHandler.cs:98-104`). Owner không có claim này vì có thể sở hữu nhiều venue — Owner luôn được so trực tiếp với `loungeOwnerId` đọc từ DB tại thời điểm gọi.

**Background job (Hangfire) không đi qua 3 lớp trên** — không có `HttpContext` nên `ICurrentUserService` không hoạt động (`src/MusicLounge.Infrastructure/Services/CurrentUserService.cs:24-27` throw nếu bị gọi). Job có toàn quyền ghi dữ liệu theo thiết kế, không "đóng vai" bất kỳ User nào.

---

## 2. Bảng tra cứu Actor

| Actor | Là gì | Quyền hạn chính (được làm) | Không được làm | Bằng chứng |
|---|---|---|---|---|
| **Admin** | `UserRole.Admin` | Duyệt/từ chối venue mới; xử phạt & xử lý kháng cáo venue; duyệt nội dung show (2 lớp AI+Admin); xử lý refund request; khoá/mở `IsActive` của User bất kỳ; xem ảnh CCCD người dùng; tạo category/genre/mood/atmosphere; trigger job thủ công; xem `ledger/integrity-check` | Không có endpoint nào cho Admin *thay* Owner tạo show/lounge | `AdminController.cs` (class-level `RequireAdmin`, dòng 35); `EventModerationsController.cs:16`; `VenuePenaltiesController.cs:27,75` |
| **Owner** | `UserRole.Owner` | Tạo/sửa lounge, gán/gỡ Staff, tạo show + hạng vé + F&B menu, đăng ký bank account (venue lẫn Performer mình tạo), xem earnings, quản lý Performer catalog, subscribe gói, kháng cáo phạt | Không tự duyệt venue của mình (`Approve` chỉ Admin); không tự gỡ phạt (`ReviewAppeal` chỉ Admin); không thao tác venue của Owner khác (check `OwnerId` trong từng handler) | `Program.cs:220`; `LoungesController.cs`, `LoungeShowsController.cs`, `BankAccountsController.cs`, `PerformersController.cs`, `SubscriptionsController.cs` (class-level `RequireOwner`) |
| **Staff** | `UserRole.Staff`, **scoped 1 venue** qua JWT claim `lounge_id` | Vận hành sàn diễn qua `RequireVenueOperator`: check-in vé, bán vé quầy, cập nhật đơn F&B, start/end show, điều khiển livestream | Không quản lý venue (sửa lounge, tạo hạng vé, subscription — không nằm trong whitelist role của các policy đó); không thao tác venue khác venue được gán | `LoginCommandHandler.cs:98-104`; `VenueOperatorAccess.cs`; `TicketsController.cs:106,134`; `FnbOrdersController.cs:39`; `LoungeShowsController.cs:329,341` |
| **Audience** | `UserRole.Audience` | Mua vé, donate, đặt F&B, follow/wishlist, đánh giá show, xem gợi ý cá nhân hoá, gửi complaint có định danh | Không chạm bất kỳ policy `RequireOwner`/`RequireAdmin`/`RequireVenueOperator` nào | Hầu hết `[Authorize(Policy = Policies.RequireAuthenticated)]` |
| **Khách chưa đăng nhập** | Không có `User`/token — pseudo-actor cho endpoint `[AllowAnonymous]` | Duyệt catalog công khai (lounge, show, F&B menu, ticket tier, sơ đồ chỗ, tour 360°, gói subscription); xem lịch sử donate công khai — **theo 1 nghệ sĩ** (`GetPublicHistory`) hoặc **sổ minh bạch toàn hệ thống, mọi nghệ sĩ** (`GetPublicFeed`, thêm 2026-08-13 — full breakdown phí, field tiền/tên donor null theo cặp khi donor chọn ẩn); **gửi complaint không cần tài khoản** | Không mua vé/donate/follow/wishlist (đều `RequireAuthenticated`) | `LoungesController.cs:56,71,202,293`; `LoungeShowsController.cs:54,71,86,95,129,142,154,177,208`; `TicketTiersController.cs:26,36`; `ComplaintsController.cs:28`; `DonationsController.cs:76` (`GetPublicFeed`), `:192` (`PerformerDonationsController.GetPublicHistory`) |
| **Performer (nghệ sĩ)** | Entity nghiệp vụ — **không có tài khoản đăng nhập** | Không tự gọi API nào — Owner/Admin thao tác thay hoàn toàn | Không tự đăng nhập/sửa hồ sơ/nhận donation trực tiếp | `Performer.cs` (không FK tới `User` để login); `PerformersController.cs:26` (`RequireOwner` cho mọi action) — chi tiết đầy đủ (touchpoint, dòng tiền, công khai tới đâu, 1 gap đã tìm thấy): [22-performer-presence.md](22-performer-presence.md) |
| **Cổng thanh toán VNPay** | Server VNPay gọi callback — không phải người dùng | Xác nhận đúng 1 giao dịch nó sở hữu (payment/donation/subscription) | Không có quyền nghiệp vụ nào khác | `PaymentsController.cs:26,50`; `DonationsController.cs:87` (`vnpay-return`), `:103` (`vnpay-ipn`); `SubscriptionsController.cs:122,138` (đều `[AllowAnonymous]`) |
| **Background Job (Hangfire)** | Chạy nội bộ, không qua HTTP pipeline | Toàn quyền ghi dữ liệu nội bộ: auto-approve venue quá hạn kháng cáo, escalate phạt, đối soát VNPay, gửi email/SMS/push | Không được (và không thể) đọc `ICurrentUserService.UserId` — throw nếu handler bên trong job cố gọi | `CurrentUserService.cs:24-27`; `AutoApproveOverdueAppealsJob.cs`; `ApplyDuePenaltiesJob.cs` |

---

## 3. Trạng thái tài khoản đặc biệt

Không phải role riêng, nhưng thay đổi được-làm-gì của actor đang giữ trạng thái đó.

| Trạng thái | Field | Tác động | Bằng chứng |
|---|---|---|---|
| Chưa xác thực email | `User.EmailVerifiedAt = null` | Chặn đăng nhập dù đúng mật khẩu | `LoginCommandHandler.cs:95-96` |
| Khoá tạm (brute-force) | `User.LockedUntil` | Chặn đăng nhập tạm thời, tự hết hạn | `LoginCommandHandler.cs:46-53` |
| Vô hiệu hoá | `User.IsActive = false` | Chặn đăng nhập — do Admin (`DeactivateUserAccount`) hoặc tự user (`DeactivateMyAccountCommand`, khôi phục được) | `LoginCommandHandler.cs:92-93`; `AdminController.cs:112-124` |
| Đã xoá dữ liệu (DSAR) | `User.DataErasedAt` set | **Không hoàn tác được** — đồng thời set `IsActive=false`, `PasswordHash=null`, `EmailVerifiedAt=null`, xoay `SecurityStamp` (thu hồi JWT đang hiệu lực ngay) — 3 cơ chế độc lập cùng chặn | `RequestDataErasureCommandHandler.cs:80-103` |
| Staff bị gỡ khỏi venue | `LoungeStaff.IsActive = false` | Vẫn giữ `UserRole.Staff`, nhưng lần login kế tiếp không còn nhận `lounge_id` (chỉ gắn từ assignment đang active) → `VenueOperatorAccess` luôn fail | `LoginCommandHandler.cs:99-103`; `LoungeStaff.cs` |

---

## 4. Actor có nhiều biến thể dữ liệu

**Đã kiểm tra riêng gợi ý "Performer chính thức vs khách mời" — không tồn tại.** `Performer.cs` chỉ có 1 hình dạng dữ liệu duy nhất, luôn do Owner/Admin tạo thay (`CreatedByUserId`), không có cờ phân loại. Biến thể thực sự tồn tại ở 2 actor khác:

| Actor | Biến thể A | Biến thể B | Khác biệt dữ liệu | Khác biệt luồng | Bằng chứng |
|---|---|---|---|---|---|
| **Audience / chủ vé** | Đăng ký mua online | Mua tại quầy ("walk-in") | A: `Ticket.BuyerId` = User.Id thật, `Payment.PayerId` set. B: `Ticket.BuyerId = null`, `Payment.PayerId = null`, `PaymentMethod = Cash`, thêm `PhysicalTicketDetail.SoldByStaffId` — **B không tồn tại dưới dạng `User` nào** | A: `HoldTicketCommandHandler` → `Purchase` (qua VNPay, có giữ chỗ 15 phút). B: `SellWalkInTicketCommandHandler` — 1 bước, chỉ Staff/Owner/Admin venue đó gọi được, xác nhận ngay | `SellWalkInTicketCommandHandler.cs:82-115` |
| **Người khiếu nại** | User đã đăng ký | Khách vãng lai | `Complaint.ComplainantUserId` nullable — B là `null`, dùng `ContactPhone` thay thế | B gọi cùng route `POST /complaints` nhưng nhánh `[AllowAnonymous]` riêng, không cần token. Xem lại kết quả cũng khác: A dùng `GET /complaints/{id}` (auth); B dùng `GET /complaints/lookup?id=&phone=` (Anonymous, khớp `id`+`ContactPhone`) — 2 route riêng, không dùng chung | `Complaint.cs:8,15` ("D17: guest reporter without account"); `ComplaintsController.cs:28,72` |
| **User (đăng nhập)** | Tài khoản local | Tài khoản Google | `AuthProvider="local"` có `PasswordHash`, `GoogleId=null`. `AuthProvider="google"` thì ngược lại | Đổi mật khẩu chỉ áp dụng local; xoá dữ liệu DSAR yêu cầu xác nhận mật khẩu hiện tại cho local, bỏ qua cho Google | `User.cs:13-14`; `MeController.cs:245` |

---

## 5. Vấn đề đã biết

| # | Vấn đề | Chi tiết | Bằng chứng |
|---|---|---|---|
| 1 | Policy `RequireStaff` không dùng | Định nghĩa ở `Program.cs:218` (`Staff` + `Admin`) nhưng grep toàn `src/` không thấy `[Authorize(Policy = Policies.RequireStaff)]` ở bất kỳ controller nào — có vẻ đã bị thay thế hoàn toàn bởi `RequireVenueOperator` (cũng gồm Owner) nhưng hằng số cũ chưa dọn | `Program.cs:218`; comment giải thích trong `VenueOperatorAccess.cs:6-9` |

---

## 6. Chưa rõ / cần xác nhận

- ~~Tài khoản `Admin` được cấp/tạo bằng cách nào?~~ ✅ Đã trả lời — **không có API/endpoint nào cả**. `KnownAdminSnapshot.cs:4-6` tự ghi rõ: "There is currently no API path to promote a user to Admin at all, so every admin today only exists via a direct database edit." Entity này tồn tại chính là baseline để `AdminRoleDriftDetectionJob` phát hiện 1 Admin mới xuất hiện ngoài danh sách đã biết — dấu hiệu tấn công/chỉnh sửa DB trái phép, vì không có con đường hợp lệ nào khác để trở thành Admin.
- `RequireStaff` (§5-1) — dọn field chết hay để nguyên có lý do (dự phòng dùng lại)?

---

## 7. Khi thêm actor/policy mới — checklist

Rút ra từ cách hệ thống hiện đang tổ chức 3 lớp phân quyền ở [§1](#1-cơ-chế-phân-quyền-nền) — dùng khi cần thêm 1 role/policy mới để không bỏ sót chỗ nào:

1. Thêm giá trị vào `UserRole` enum (`src/MusicLounge.Domain/Enums/UserRole.cs`) + hằng số tương ứng trong `Roles.cs` (`src/MusicLounge.Application/Common/Constants/Roles.cs`).
2. Nếu cần 1 tổ hợp role mới (khác các policy đã có) — thêm `Policies.X` (`src/MusicLounge.Api/Authorization/Policies.cs`) + đăng ký `AddPolicy` trong `Program.cs`.
3. Nếu role mới cần scope theo venue (như Staff) — quyết định có nạp claim tương tự `lounge_id` lúc login không (`LoginCommandHandler.cs`), và có cần mở rộng `VenueOperatorAccess.CanOperate` không.
4. Cập nhật bảng ở [§2](#2-bảng-tra-cứu-actor) trong file này.
5. Nếu role mới tự đăng ký được — thêm vào whitelist trong `RegisterCommandValidator.cs:26-28` (mặc định chỉ `Audience`/`Owner`).

---

*Xem [11-ba-domain-analysis.md](11-ba-domain-analysis.md) cho phân tích nghiệp vụ theo domain (Venue, Show, Ticket, Payment...). File này chỉ tập trung actor/quyền hạn.*
