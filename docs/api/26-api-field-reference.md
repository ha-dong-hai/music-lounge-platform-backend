# 26 — API Field-Level Reference cho Frontend

← [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md) (bảng tổng quan, gọn hơn nhưng nhiều field
ghi "chưa xác minh") · [API-STANDARDS.md](API-STANDARDS.md)

> **Nguồn**: đọc trực tiếp toàn bộ 25 Controller (`src/MusicLounge.Api/Controllers/`) + Command/Query
> record + FluentValidation Validator + response DTO thật trong source, **không** copy từ
> `docs/api/16-api-endpoint-catalog.md` hay `README-SETUP.md` (2 tài liệu đó đã xác nhận có chỗ lệch so
> với code hiện tại — vd `CreateSubscriptionPackageCommand` thiếu 2 field bắt buộc trong ví dụ cũ).
> Thực hiện bằng 7 agent đọc song song theo domain, đối chiếu chéo — các quy ước chung bên dưới được
> cả 7 agent xác nhận giống nhau độc lập.
>
> **Cập nhật**: 2026-08-18. Nếu sau này thêm/sửa endpoint, cập nhật đúng phần tương ứng — đừng để tài
> liệu này lặp lại số phận "chưa xác minh" của tài liệu 16.

---

## Changelog 2026-08-18 (đã deploy lên Azure)

| # | Endpoint | Thay đổi |
|---|---|---|
| 💥 | [`POST /donations/{id}/confirm-paid`](#post-apiv1donationsidconfirm-paid) | **BREAKING.** `paymentEvidenceUrl` optional → **required** (thiếu = 400). Đồng thời **bỏ** yêu cầu nghệ sĩ phải có tài khoản ngân hàng mặc định (trước đây thiếu = 422). |
| 🆕 | [`GET /admin/bank-accounts/pending`](#get-apiv1adminbank-accountspending) | Hàng chờ xác minh tài khoản ngân hàng (chỉ `OwnerType = Lounge`). |
| 🆕 | [`GET /venue-penalties`](#get-apiv1venue-penalties) | Danh sách xử phạt/kháng cáo toàn sàn cho Admin. |
| 🆕 | [`POST /moderations/images/{moderationId}/review`](#post-apiv1moderationsimagesmoderationidreview) | Duyệt ảnh `GalleryImage` / `TourScene`. **Chú ý: keyed theo `moderationId`, không phải `targetId`.** |
| ➕ | [`GET /donations/{id}`](#get-apiv1donationsid) | Thêm `paymentRef` + `paymentEvidenceUrl`, **che khỏi donor**. |

Tổng số route đang phục vụ trên Azure: **184** — kiểm chứng bằng `GET /swagger/v1/swagger.json`, không
phải đếm tay. (Lưu ý: status code **không** dùng để kiểm chứng route có tồn tại hay không — deny-by-default
`FallbackPolicy` khiến mọi URL sai cũng trả 401 chứ không phải 404.)

---

## Mục lục

1. [Auth & Account](#part-1--auth--account) — `AuthController`, `MeController`, `BankAccountsController`
2. [Venue / Lounge](#part-2--venue--lounge) — `LoungesController`, `VenuePenaltiesController`, `PerformersController`
3. [Show / Event](#part-3--show--event) — `LoungeShowsController`, `EventModerationsController`
4. [Ticket / Money](#part-4--ticket--money) — `TicketsController`, `TicketTiersController`, `PaymentsController`, `DonationsController`, `SubscriptionsController`
5. [F&B](#part-5--fb) — `FnbMenusController`, `FnbMenuItemsController`, `FnbOrdersController`
6. [Livestream & Social](#part-6--livestream--social) — `LivestreamsController`, `FollowsController`, `WishlistController`, `RecommendationsController`, `NotificationsController`, `AnalyticsController`
7. [Admin / Complaints / Uploads](#part-7--admin--complaints--uploads) — `AdminController`, `ComplaintsController` (2 class), `UploadsController`

---

## Quy ước chung — đọc 1 lần, áp dụng cho MỌI endpoint bên dưới

### Base URL & versioning
- Production: `https://musiclounge-api.azurewebsites.net`
- Local dev: `http://localhost:5289`
- Mọi route bên dưới ghi dạng rút gọn (vd `POST /auth/login`) là viết tắt của
  `POST /api/v1/auth/login` — route pattern thật là `api/v{version:apiVersion}/...`, version mặc
  định `1.0`, đọc từ URL segment (không phải header).

### Xác thực
- Header `Authorization: Bearer <token>`. `<token>` lấy từ field `data.token` trong response của
  `POST /auth/login`, `POST /auth/verify-email`, hoặc `POST /auth/google`.
- **Không có refresh token** — khi `data.expiresAt` (response login) qua, phải đăng nhập lại từ đầu.
- `ActiveUserBehavior` (MediatR pipeline) re-check `User.IsActive` (và với Staff: `LoungeStaff.IsActive`)
  trên **mọi** request đã xác thực, không chỉ lúc login — nghĩa là 1 token còn hạn vẫn có thể nhận
  401 **giữa phiên** nếu Admin khoá tài khoản hoặc Owner gỡ Staff khỏi venue. FE nên coi bất kỳ 401
  nào trên request đã có token là "đăng xuất ngay", không chỉ riêng trường hợp hết hạn.

### Policy → role (từ `Program.cs`, chính xác 100%)
| Policy | Role được chấp nhận |
|---|---|
| `RequireAuthenticated` | Bất kỳ ai đã đăng nhập |
| `RequireStaff` | `Staff`, `Admin` |
| `RequireVenueOperator` | `Staff`, `Owner`, `Admin` |
| `RequireOwner` | `Owner`, `Admin` |
| `RequireAdmin` | `Admin` |

**Cảnh báo quan trọng**: đây chỉ là policy cấp route (attribute `[Authorize]`). Nhiều handler bên
dưới còn tự check quyền sở hữu tài nguyên (`resource.OwnerId != currentUser.Id`) **riêng, không có
Admin bypass** — nghĩa là 1 tài khoản Admin có thể pass được policy route nhưng vẫn nhận 403 từ chính
handler. Việc này được ghi rõ ở mục **Notes** của từng endpoint bị ảnh hưởng (đã xác nhận ở ít nhất:
`AssignStaff`/`DeactivateStaff`/tạo-sửa Custom Criteria/Submit Appeal trong `LoungesController`;
6 handler CRUD menu/menu-item trong F&B; `GetFnbOrdersQuery`/`GetFnbOrderByIdQuery` không có bypass
nào cả).

### Envelope JSON — MỌI response (trừ 204 và 1 ngoại lệ duy nhất)
```json
{ "success": true,  "data": { /* T */ }, "message": null }
{ "success": false, "message": "Mô tả lỗi", "errors": { "TenField": ["chi tiết"] } | null }
```
Action trả `204 No Content` không có body. **Ngoại lệ duy nhất trong toàn bộ API**:
`GET /me/citizen-card/{side}` trả file ảnh nhị phân thật (`Content-Type` = MIME lưu thật), không bọc
JSON.

### Mã lỗi thật theo loại Exception — QUAN TRỌNG, khác với Swagger attribute
`GlobalExceptionHandler.cs` map exception → status code như sau (xác nhận bằng cách đọc trực tiếp
file, không suy đoán từ `[ProducesResponseType]` trên controller — nhiều controller chỉ khai 400 dù
runtime thật trả 422):

| Exception | HTTP status | `errors` |
|---|---|---|
| `ValidationException` (FluentValidation, hoặc ASP.NET Core tự check field bắt buộc bị thiếu) | **400** | `{ "Field": ["msg"] }` |
| `UnauthorizedException` | **401** | `null` |
| `ForbiddenException` | **403** | `null` |
| `NotFoundException` | **404** | `null` |
| `ConflictException` | **409** | `null` |
| `DomainException` (vi phạm business rule — sai trạng thái, hết hạn hold, vượt quota...) | **422** | `null` |
| `ExternalServiceException` | **503** | `null` |
| `DbUpdateException` (race condition hiếm — trùng unique index) | **409** | `null` |
| Khác | **500** | `null` |

**Hệ quả thực tế cho FE**: gần như mọi lỗi "vi phạm nghiệp vụ" (hold hết hạn, show sai trạng thái,
vé đã check-in, gói đã hết hạn mức...) trả về **422**, không phải 400. 400 chỉ dành cho lỗi field
(thiếu field, sai định dạng, âm số, quá độ dài...) — luôn kèm `errors` là dictionary field→message.
2 nguồn (FluentValidation và ASP.NET Core auto-check) đều đổ về cùng 1 shape này, FE chỉ cần 1
interceptor duy nhất dựa vào field `success`.

### Phân trang
Danh sách bọc trong `PaginatedResult<T>` (nằm trong `data`):
```json
{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 137, "totalPages": 7, "hasNextPage": true, "hasPreviousPage": false }
```
`page`/`pageSize` là query param. **`pageSize` mặc định bị chặn tối đa 100** qua 1 filter toàn cục
(`ClampPaginationActionFilter`), nhưng **nhiều handler tự chặn chặt hơn xuống 50** — không đồng nhất
theo từng endpoint, xem ghi chú tại từng endpoint danh sách. Nhóm đã xác nhận chặn ở 50 (không phải
100): hầu hết list trong `LoungesController`/`VenuePenaltiesController`/`PerformersController`, hầu
hết list refund/donation trong nhóm Ticket/Money, và 5 endpoint cụ thể trong nhóm Admin
(`/admin/lounges/pending`, `/admin/refund-requests`, `/admin/users`, `/complaints/my`,
`/admin/complaints`). `page < 1` luôn tự về `1` ở mọi nơi.

### Kiểu dữ liệu khi serialize JSON
- **Enum** → chuỗi tên member C# thật (không phải số) — `JsonStringEnumConverter` đăng ký toàn cục.
  Danh sách giá trị hợp lệ của từng enum được liệt kê đầy đủ tại nơi field đó xuất hiện lần đầu trong
  từng Part bên dưới, lấy trực tiếp từ `src/MusicLounge.Domain/Enums/*.cs`.
- **`DateTimeOffset`** → ISO-8601 kèm offset số, vd `"2026-09-05T19:00:00+07:00"`.
- **`decimal`** → số JSON thuần (vd `150000.00`), không bao giờ là chuỗi.
- **Tên field JSON**: camelCase (mặc định của ASP.NET Core MVC — C# `ShowId` → JSON `showId`).
- **Nullable = optional**: property C# kiểu `string?`/`int?`... là optional trong request body;
  property non-nullable coi như bắt buộc (project bật `<Nullable>enable</Nullable>` — thiếu field
  non-nullable bị model binder chặn 400 **trước khi** vào tới handler/validator, nên không bao giờ
  thành lỗi 422). Ngoại lệ đã xác nhận: `RegisterCommand.Role` là `string` non-nullable nhưng có giá
  trị default C# `"Audience"` — nếu FE không gửi field `role`, server tự hiểu là Audience thay vì lỗi.

### Giới hạn tần suất (rate limit)
Toàn API: 100 request/phút/IP. Riêng nhóm `/auth/*` (toàn bộ `AuthController` + vài action cụ thể
trong `MeController`, ghi chú riêng ở Part 1): 10 request/phút/IP. Response 429 kèm header
`Retry-After: <giây>` và body `{"success":false,"message":"...","errors":null}`.

---

## Part 1 — Auth & Account

## AuthController

Base route: `api/v1/auth`. Controller-level `[AllowAnonymous]` + `[EnableRateLimiting("auth")]` (10 req/min/IP) apply to every action below unless noted otherwise.

### POST /api/v1/auth/register
**Auth**: AllowAnonymous, rate limit policy `auth`
**Request body**:
- `email` (string) — required, valid email format, max 255 chars.
- `password` (string) — required, min 15 chars, max 64 chars. No composition rules (NIST SP 800-63B-4 based).
- `fullName` (string) — required, max 255 chars, must match regex `^[\p{L}\p{M} .'\-]+$` (Unicode letters/marks, space, apostrophe, hyphen, period only).
- `phone` (string, nullable) — optional, max 20 chars if provided.
- `acceptTerms` (bool) — required to be exactly `true` (validator: `.Equal(true)`), otherwise 400.
- `role` (string, optional, default `"Audience"`) — must be exactly `"Audience"` or `"Owner"` (case-sensitive string comparison — `r is "Audience" or "Owner"`; NOT case-insensitive despite the handler's later `Enum.Parse(..., ignoreCase: true)`, because the validator rejects any other casing before the handler ever runs).

Example:
```json
{
  "email": "newowner@example.com",
  "password": "correct horse battery staple 2026",
  "fullName": "Nguyễn Văn A",
  "phone": "0912345678",
  "acceptTerms": true,
  "role": "Owner"
}
```

**Response 200**: `ApiResponse<RegisterResultDto>` — does NOT issue a token. Account is created "unverified"; call `verify-email` with the emailed OTP to get a token.
- `email` (string)
- `fullName` (string)
- `verificationCodeExpiresAt` (string, ISO-8601 DateTimeOffset) — OTP validity window, 10 minutes from send.

```json
{
  "success": true,
  "data": {
    "email": "newowner@example.com",
    "fullName": "Nguyễn Văn A",
    "verificationCodeExpiresAt": "2026-08-17T18:10:00+07:00"
  },
  "message": null
}
```

**Other status codes**: 400 on any validation failure above. **409 is declared in the controller's `[ProducesResponseType]` but is never actually thrown by this handler** — duplicate email is handled via anti-enumeration (see Notes), not a 409. (409 could still occur from the generic `DbUpdateException` race-condition fallback on a true concurrent duplicate-insert race.)
**Notes**: Anti-enumeration by design — if the email is already registered, the handler does NOT create a new user and does NOT reveal this in the response; it returns the exact same 200 shape built from the submitted `email`/`fullName`, and instead emails the REAL account owner a "someone tried to register with your email" alert. FE cannot distinguish "new registration" from "duplicate email" from the response alone.

---

### POST /api/v1/auth/verify-email
**Auth**: AllowAnonymous, rate limit policy `auth`
**Request body**:
- `email` (string) — required, valid email, max 255 chars.
- `code` (string) — required, must match `^\d{6}$` (exactly 6 digits).

```json
{ "email": "newowner@example.com", "code": "483920" }
```

**Response 200**: `ApiResponse<AuthResultDto>` — issues the first login token.
- `token` (string) — JWT bearer token.
- `expiresAt` (string, ISO-8601 DateTimeOffset)
- `userId` (int)
- `email` (string)
- `fullName` (string)
- `role` (string) — one of the `UserRole` enum members: `Audience`, `Staff`, `Owner`, `Admin`.
- `loungeId` (int, nullable) — only non-null when `role == "Staff"` and the user has an active `LoungeStaff` assignment; otherwise `null`.

```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "expiresAt": "2026-08-17T19:10:00+07:00",
    "userId": 42,
    "email": "newowner@example.com",
    "fullName": "Nguyễn Văn A",
    "role": "Owner",
    "loungeId": null
  },
  "message": null
}
```

**Other status codes**:
- 400 — malformed email/code (validator).
- 401 — email/code mismatch (deliberately vague message "Email hoặc mã xác thực không đúng." for both cases — anti-enumeration), code expired ("Mã xác thực đã hết hạn..."), or account temporarily locked from too many wrong attempts ("Tài khoản tạm thời bị khóa..." with minutes remaining).
- 409 — account already verified ("Tài khoản đã được xác thực, vui lòng đăng nhập.").
**Notes**: On success this also clears `EmailVerificationCodeHash`/`ExpiresAt` server-side (code is single-use).

---

### POST /api/v1/auth/resend-verification-code
**Auth**: AllowAnonymous, rate limit policy `auth`
**Request body**:
- `email` (string) — required, valid email, max 255 chars.

```json
{ "email": "newowner@example.com" }
```

**Response 200**: `ApiResponse<ResendVerificationCodeResultDto>`
- `verificationCodeExpiresAt` (string, ISO-8601 DateTimeOffset)

```json
{ "success": true, "data": { "verificationCodeExpiresAt": "2026-08-17T18:25:00+07:00" }, "message": null }
```

**Other status codes**: 400 on malformed email only.
**Notes**: Anti-enumeration — always returns 200 with a freshly-computed expiry regardless of whether the email exists or is already verified; a new code/email is actually sent only if a matching, not-yet-verified account exists. FE cannot use this response to infer account existence.

---

### POST /api/v1/auth/login
**Auth**: AllowAnonymous, rate limit policy `auth`
**Request body** (bound to controller-local `LoginRequest`, not a MediatR command):
- `email` (string) — required.
- `password` (string) — required.
- (No `ipAddress` field — the server captures `HttpContext.Connection.RemoteIpAddress` itself; a client-supplied IP would be rejected/ignored since it isn't part of the request DTO at all.)

```json
{ "email": "newowner@example.com", "password": "correct horse battery staple 2026" }
```

**Response 200**: `ApiResponse<AuthResultDto>` — identical shape to `verify-email`'s response (see above: `token`, `expiresAt`, `userId`, `email`, `fullName`, `role`, `loungeId`).

**Other status codes**:
- 400 — empty email/password (validator).
- 401 — wrong email or password (single generic message "Email hoặc mật khẩu không đúng." for both — anti-enumeration, with a timing-normalized dummy-hash comparison so an unknown email doesn't respond faster than a known one), account locked out from repeated failures, account deactivated ("Tài khoản đã bị khóa do vi phạm quy định sử dụng..."), or email not yet verified ("Vui lòng xác thực email trước khi đăng nhập.").
**Notes**: Every failed attempt (wrong password OR unknown email) is logged server-side to `LoginFailureLog` (keyed by email + captured IP) feeding a login-spike detector — this write commits immediately even though the request as a whole then throws 401 (the command is `INoTransactionCommand`).

---

### POST /api/v1/auth/google
**Auth**: AllowAnonymous, rate limit policy `auth`
**Request body**:
- `idToken` (string) — required, the Google Sign-In ID token from the client SDK.
- `acceptTerms` (bool, optional, default `false`) — only meaningful the first time this Google identity is seen (i.e. when a brand-new `User` row would be created). Existing users (found by `GoogleId`, or an existing local account linked by matching email) never need this — omit/ignore it on every login after the first.

```json
{ "idToken": "eyJhbGciOiJSUzI1NiIs...", "acceptTerms": true }
```

**Response 200**: `ApiResponse<AuthResultDto>` — identical shape to `login`'s response.

**Other status codes**:
- 400 — empty `idToken` (validator); also whatever `IGoogleTokenVerifier.VerifyAsync` raises for an invalid/expired Google token (verifier implementation is outside these 3 controllers' scope).
- 401 — account deactivated.
- **422 (not declared in `[ProducesResponseType]`, but real)** — brand-new Google sign-up with `acceptTerms: false` throws `DomainException` ("Bạn cần đồng ý với Điều khoản dịch vụ và Chính sách bảo mật để đăng ký.").
**Notes**: If a local (password) account already exists with the same email and was never email-verified, this call links it to the Google identity, marks it verified, and **wipes its `PasswordHash`** — this specifically closes a "classic-federated merge" account-takeover pattern where an attacker had pre-registered the victim's email with their own password.

---

### POST /api/v1/auth/forgot-password
**Auth**: AllowAnonymous, rate limit policy `auth`
**Request body**:
- `email` (string) — required, valid email, max 255 chars.

```json
{ "email": "newowner@example.com" }
```

**Response**: **204 No Content**, no body — always, regardless of whether the email exists.
**Other status codes**: 400 on malformed email only.
**Notes**: Anti-enumeration. If a matching account exists, a one-time reset token (30-minute lifetime) is generated and emailed via a background job — never returned in the response.

---

### POST /api/v1/auth/reset-password
**Auth**: AllowAnonymous, rate limit policy `auth`
**Request body**:
- `token` (string) — required. This is the raw token from the emailed reset link's `?token=` query param (base64url of 32 random bytes), NOT the OTP-style 6-digit codes used elsewhere in this API.
- `newPassword` (string) — required, min 15 chars, max 64 chars.

```json
{ "token": "s3cUr3-r4nd0m_t0k3n-fr0m-l1nk", "newPassword": "another very long passphrase 2026" }
```

**Response**: **204 No Content**.
**Other status codes**: 400 on validation. 401 if token is invalid, already used, or expired ("Link đặt lại mật khẩu không hợp lệ hoặc đã hết hạn...").
**Notes**: Token is single-use (cleared immediately after success). Also rotates `SecurityStamp`, which invalidates every JWT issued before this reset on its very next authenticated request (via `ActiveUserBehavior`... actually checked at JWT validation time, `OnTokenValidated`) — any session logged in before the reset is force-logged-out.

---

## MeController

Base route: `api/v1/me`. Controller-level `[Authorize(Policy = "RequireAuthenticated")]` applies to every action unless a stricter policy is noted.

### GET /api/v1/me
**Auth**: RequireAuthenticated
**Response 200**: `ApiResponse<UserProfileDto>`
- `id` (int)
- `fullName` (string)
- `email` (string)
- `avatarUrl` (string, nullable)
- `aiConsent` (bool)
- `favouriteGenreIds` (int[])
- `favouriteMoodIds` (int[])
- `favouriteAtmosphereIds` (int[])
- `phone` (string, nullable)
- `phoneVerified` (bool)
- `dateOfBirth` (string, nullable, `DateOnly` → serializes as `"YYYY-MM-DD"`)

```json
{
  "success": true,
  "data": {
    "id": 42,
    "fullName": "Nguyễn Văn A",
    "email": "newowner@example.com",
    "avatarUrl": null,
    "aiConsent": true,
    "favouriteGenreIds": [1, 3, 7],
    "favouriteMoodIds": [2],
    "favouriteAtmosphereIds": [],
    "phone": "0912345678",
    "phoneVerified": true,
    "dateOfBirth": "1998-05-20"
  },
  "message": null
}
```
**Other status codes**: 401 only (no token / inactive account).

---

### GET /api/v1/me/earnings
**Auth**: RequireOwner (stacks on top of the controller's RequireAuthenticated)
**Response 200**: `ApiResponse<EarningsSummaryDto>`
- `totalEarned` (decimal) — sum of `Released` + `Scheduled`/`PendingReview` settlement net amounts.
- `pendingSettlement` (decimal) — sum of settlements with status `Scheduled` or `PendingReview`.
- `completedSettlement` (decimal) — sum of settlements with status `Released`.
- `pendingSettlementCount` (int)
- `recentSettlements` (array, most recent 10 by descending Id) — each item:
  - `id` (int)
  - `amount` (decimal) — the settlement's `NetAmount`.
  - `status` (string) — `SettlementStatus` enum: `Scheduled`, `Released`, `Cancelled`, `PendingReview`.
  - `scheduledAt` (string, ISO-8601 DateTimeOffset)
  - `paidAt` (string, nullable, ISO-8601 DateTimeOffset) — null until actually released.

```json
{
  "success": true,
  "data": {
    "totalEarned": 15250000.00,
    "pendingSettlement": 3200000.00,
    "completedSettlement": 12050000.00,
    "pendingSettlementCount": 2,
    "recentSettlements": [
      {
        "id": 118,
        "amount": 1800000.00,
        "status": "Scheduled",
        "scheduledAt": "2026-08-20T00:00:00+07:00",
        "paidAt": null
      },
      {
        "id": 117,
        "amount": 2200000.00,
        "status": "Released",
        "scheduledAt": "2026-08-10T00:00:00+07:00",
        "paidAt": "2026-08-10T09:03:11+07:00"
      }
    ]
  },
  "message": null
}
```
**Other status codes**: 401, 403 (non-Owner role).

---

### PUT /api/v1/me/preferences
**Auth**: RequireAuthenticated
**Request body** (`UpdateAiPreferencesCommand`):
- `genreIds` (int[]) — required (not null; empty array clears all), max 10 items.
- `moodIds` (int[]) — required, max 10 items.
- `atmosphereIds` (int[]) — required, max 10 items.
- `enableAiConsent` (bool) — required.

```json
{ "genreIds": [1, 3], "moodIds": [2], "atmosphereIds": [5, 9], "enableAiConsent": true }
```

**Response**: 204 No Content.
**Other status codes**: 400 (>10 items in any list, or a list is null). 404 if any submitted genre/mood/atmosphere id doesn't exist in the catalog ("một hoặc nhiều genre/mood/atmosphere không tồn tại.").
**Notes**: This is a full **replace**, not a merge — omitted ids in any list are removed from that user's favourites. `enableAiConsent` also directly overwrites `User.AiConsent`.

---

### PUT /api/v1/me/profile
**Auth**: RequireAuthenticated
**Request body** (`UpdateMyProfileCommand`):
- `fullName` (string) — required, max 200 chars. (Note: no character-class regex here, unlike `RegisterCommand.FullName`.)
- `phone` (string, nullable) — optional, max 20 chars if provided.
- `avatarUrl` (string, nullable) — optional, max 500 chars if provided.
- `dateOfBirth` (string, nullable, `"YYYY-MM-DD"`) — optional, must be strictly before today if provided.

```json
{ "fullName": "Nguyễn Văn A", "phone": "0912345678", "avatarUrl": "https://cdn.example.com/avatars/42.jpg", "dateOfBirth": "1998-05-20" }
```

**Response**: 204 No Content.
**Other status codes**: 400 (validation), 404 (user not found — should not normally happen for an authenticated caller).
**Notes**: **Changing `phone` to a different value silently resets `phoneVerified` to `false`** and clears any pending phone-verification code — re-verification via `POST /me/phone/verification-code` + `/verify` is required again. Sending the *same* phone value does not reset it.

---

### PUT /api/v1/me/password
**Auth**: RequireAuthenticated, rate limit policy `auth`
**Request body** (`ChangePasswordCommand`):
- `currentPassword` (string) — required.
- `newPassword` (string) — required, min 15 chars, max 64 chars, must differ from `currentPassword` (`.NotEqual`).

```json
{ "currentPassword": "old passphrase here", "newPassword": "a brand new passphrase 2026" }
```

**Response**: 204 No Content.
**Other status codes**: 400 (validation, incl. new == current). 401 (wrong `currentPassword`). **422 (not declared via `[ProducesResponseType]`, but real)** — account has no local password at all (Google-only) → `DomainException` ("Tài khoản đăng nhập bằng Google, không có mật khẩu để đổi.").
**Notes**: Rotates `SecurityStamp` on success — any other active session/JWT is force-invalidated on its next request.

---

### POST /api/v1/me/email/change-request
**Auth**: RequireAuthenticated, rate limit policy `auth`
**Request body** (`RequestChangeEmailCommand`) — step 1 of 2:
- `newEmail` (string) — required, valid email, max 255 chars.

```json
{ "newEmail": "newer-address@example.com" }
```

**Response**: 204 No Content.
**Other status codes**: 400 (validation). 409 — `newEmail` already used by a different account. **422 (not declared, but real)** — `newEmail` equals the current email → `DomainException` ("Đây đã là email hiện tại của bạn.").
**Notes**: Nothing is written to `User.Email` yet. A 6-digit OTP (10-min lifetime) is emailed to `newEmail` (not the current one) and stored as `User.PendingEmail` + a code hash, pending confirmation via the next endpoint.

---

### POST /api/v1/me/email/change-confirm
**Auth**: RequireAuthenticated, rate limit policy `auth`
**Request body** (`ConfirmChangeEmailCommand`) — step 2 of 2:
- `code` (string) — required (non-empty; note this validator does NOT enforce a 6-digit regex like other OTP fields, only `.NotEmpty()`).

```json
{ "code": "719204" }
```

**Response**: 204 No Content.
**Other status codes**: 401 — wrong code (generic "Mã xác thực không đúng."), expired code, or account temporarily locked from repeated wrong attempts. **422 (not declared, but real)** — no pending email-change request exists → `DomainException` ("Bạn chưa yêu cầu đổi email — gọi endpoint yêu cầu đổi email trước.").
**Notes**: On success, `User.Email` is overwritten from `PendingEmail`, `PendingEmail` cleared, and `SecurityStamp` rotated (email is a login credential — old JWTs invalidated).

---

### POST /api/v1/me/citizen-card
**Auth**: RequireAuthenticated
**Request body** (`SubmitCitizenCardCommand`) — KYC submission:
- `citizenCardNumber` (string) — required, must match `^\d{9}$|^\d{12}$` (exactly 9 or 12 digits — old CMND or new CCCD format).
- `frontImageUrl` (string) — required, max 500 chars. Get this URL from an uploads endpoint first (`POST /uploads/...`, out of scope of this document — legacy public `/uploads/...` refs are auto-relocated to private storage server-side; refs already obtained from the private-upload endpoint are used as-is).
- `backImageUrl` (string) — required, max 500 chars, same sourcing as above.

```json
{
  "citizenCardNumber": "079198000123",
  "frontImageUrl": "/uploads/citizen-front-abc123.jpg",
  "backImageUrl": "/uploads/citizen-back-abc123.jpg"
}
```

**Response**: 204 No Content.
**Other status codes**: 400 (validation). 409 — this citizen card number is already registered to a *different* account (uniqueness is enforced via a deterministic hash of the encrypted number).
**Notes**: `citizenCardNumber` is encrypted at rest (`IPiiEncryptionService`); a separate deterministic hash column is used for the uniqueness check since the ciphertext itself is non-deterministic. Re-submitting overwrites the previously submitted number/images and resets `CitizenCardSubmittedAt`.

---

### GET /api/v1/me/citizen-card/{side}
**Auth**: RequireAuthenticated
**Route params**: `side` (string) — must be `"front"` or `"back"` (case-insensitive comparison).
**Response 200**: **Raw binary file**, NOT a JSON envelope — `Content-Type` header is whatever was stored for that image; body is the raw image bytes. Caller (own account only — there is no `userId` param, it's always "my" card) must have previously submitted via `POST /me/citizen-card`.
**Other status codes**: 401. 404 — that side was never submitted (no image on file). **422 (not declared, but real)** — `side` is neither `"front"` nor `"back"` → `DomainException` ("Side phải là 'front' hoặc 'back'.").
**Notes**: Response has `Cache-Control: no-store` (via `[ResponseCache(NoStore = true, ...)]`) — never cache this client-side. File lives outside `wwwroot`; there is no direct/guessable public URL for it.

---

### GET /api/v1/me/data-export
**Auth**: RequireAuthenticated
**Response 200**: `ApiResponse<MyDataExportDto>` — DSAR (Luật 91/2025/QH15) data-portability export, assembled synchronously.
- `profile` (object):
  - `id` (int), `email` (string), `fullName` (string), `phone` (string, nullable), `createdAt` (string, `DateTime` — note: **not** `DateTimeOffset** here, unlike most other timestamp fields in this API; still serializes ISO-8601 but with no explicit UTC offset marker unless the underlying value is already UTC-kind).
- `tickets` (array): `id` (string, `Guid`), `showId` (int), `status` (string — `TicketStatus`: `Pending`, `Confirmed`, `Used`, `Cancelled`, `Refunded`), `createdAt` (DateTimeOffset).
- `donations` (array): `id` (int), `gross` (decimal), `status` (string — `DonationStatus`: `PendingPayment`, `PendingOwnerAck`, `OwnerReceived`, `PerformerPaid`, `Cancelled`, `Refunded`), `createdAt` (DateTimeOffset).
- `ratings` (array): `showId` (int), `score` (int), `comment` (string, nullable), `createdAt` (DateTimeOffset).
- `complaints` (array): `id` (int), `category` (string — `ComplaintCategory`: `EventMisrepresentation`, `RefundDispute`, `DonationNotPaid`, `TechnicalIssue`, `VenueConduct`, `PenaltyAppeal`, `Other`), `status` (string — `ComplaintStatus`: `Open`, `Investigating`, `Resolved`, `Rejected`), `createdAt` (DateTimeOffset).
- `followedLoungeIds` (int[])
- `wishlistedShowIds` (int[])

```json
{
  "success": true,
  "data": {
    "profile": { "id": 42, "email": "newowner@example.com", "fullName": "Nguyễn Văn A", "phone": "0912345678", "createdAt": "2026-01-15T03:22:10" },
    "tickets": [ { "id": "9c1b2a3d-...", "showId": 88, "status": "Used", "createdAt": "2026-02-01T10:00:00+07:00" } ],
    "donations": [ { "id": 5, "gross": 100000.00, "status": "PerformerPaid", "createdAt": "2026-02-01T20:15:00+07:00" } ],
    "ratings": [ { "showId": 88, "score": 5, "comment": "Great show!", "createdAt": "2026-02-02T09:00:00+07:00" } ],
    "complaints": [],
    "followedLoungeIds": [3, 7],
    "wishlistedShowIds": [12]
  },
  "message": null
}
```
**Other status codes**: 401 only.
**Notes**: Covers data-portability (access) only, not erasure — see `POST /me/data-erasure` for the separate, irreversible deletion request.

---

### DELETE /api/v1/me
**Auth**: RequireAuthenticated
**Response**: 204 No Content.
**Other status codes**: 401 only.
**Notes**: **Soft deactivation** — flips `User.IsActive = false`. Recoverable (Admin-side reactivation exists elsewhere, not in this controller). This is NOT the DSAR erasure action — no PII is scrubbed, financial/identity data stays intact. Once inactive, every subsequent authenticated request (including this account's own still-valid JWT) is rejected with 401 by `ActiveUserBehavior`.

---

### POST /api/v1/me/data-erasure
**Auth**: RequireAuthenticated, rate limit policy `auth`
**Request body** (nullable — `RequestDataErasureRequest?`):
- `currentPassword` (string, nullable) — **required only if the account has a local password** (i.e. not Google-only). Ignored for Google-only accounts.

```json
{ "currentPassword": "correct horse battery staple 2026" }
```
or, body omitted entirely (`null`) — only valid for a Google-only account.

**Response**: 204 No Content.
**Other status codes**: 401 — missing/wrong `currentPassword` for a local account. 409 — data already erased previously (`DataErasedAt` already set).
**Notes**: **Irreversible.** DSAR erasure per Luật 91/2025/QH15 Điều 19 — anonymizes the `User` row in place (never hard-deletes it, because Payments/Settlements/Donations/Tickets carry a 10-year Accounting Law retention requirement and still reference this row's FK). After this call: `email` becomes `deleted-user-{id}@musiclounge.local`, `fullName` becomes "Người dùng đã xóa", `phone`/`avatarUrl`/citizen-card fields/password/Google link all nulled, `isActive` set `false`, `securityStamp` rotated (any existing JWT dies immediately), `dataErasedAt` timestamped. Also hard-deletes the user's own preference/behaviour rows (follows, wishlist, favourite genres/moods/atmospheres, custom preferences, AI recommendations, behaviour logs). Ticket/Donation/Complaint/Rating history is preserved but no longer identifies the person.

---

### POST /api/v1/me/phone/verification-code
**Auth**: RequireAuthenticated, rate limit policy `auth`
**Request body**: none.
**Response**: 204 No Content.
**Other status codes**: 401. 409 — phone already verified. 422 — `User.Phone` is empty/not set ("Vui lòng cập nhật số điện thoại trong hồ sơ trước khi yêu cầu xác thực.") — set it first via `PUT /me/profile`.
**Notes**: Sends a 6-digit OTP (10-min lifetime) via SMS to the phone number already on file — NĐ 147/2024 verification. There is no request body to supply a phone number here; it always targets whatever is currently saved on the profile.

---

### POST /api/v1/me/phone/verify
**Auth**: RequireAuthenticated, rate limit policy `auth`
**Request body** (`VerifyPhoneCommand`):
- `code` (string) — required, exact length 6 (validator uses `.Length(6)`, i.e. exactly 6 characters — **not** restricted to digits-only by regex, unlike the email-OTP validators elsewhere in this API).

```json
{ "code": "204981" }
```

**Response**: 204 No Content.
**Other status codes**: 401 — wrong code, expired code, or account temporarily locked from repeated failures. 409 — phone already verified.
**Notes**: On success sets `PhoneVerified = true` and clears the verification code/expiry.

---

### GET /api/v1/me/custom-preferences
**Auth**: RequireAuthenticated
**Response 200**: `ApiResponse<UserCustomPreferenceDto[]>` — the caller's own explicit/learned interest signals for venue-defined custom criteria (e.g. language preference, price sensitivity).
- `criteriaId` (int)
- `criteriaName` (string) — resolved from the `CustomCriteria` catalog; empty string `""` if the criteria row was deleted/not found.
- `value` (string) — the raw stored value; its expected shape depends on the criteria's `CustomCriteriaDataType` (`Select`, `Range`, `Boolean`, `Text` — see `src/MusicLounge.Domain/Enums/CustomCriteriaDataType.cs`), which is NOT included in this DTO — FE must look up the criteria definition separately (e.g. via the lounge's criteria list) to know how to render/validate `value`.
- `source` (string) — `CustomPreferenceSource` enum: `Explicit` (user manually set, via the PUT below) or `Learned` (AI-inferred; no endpoint currently writes this — recommendation engine not yet wired to this table).
- `weight` (decimal) — 0 to 1.

```json
{
  "success": true,
  "data": [
    { "criteriaId": 3, "criteriaName": "Ngôn ngữ trình diễn", "value": "EN", "source": "Explicit", "weight": 0.8 }
  ],
  "message": null
}
```
**Other status codes**: 401 only.

---

### PUT /api/v1/me/custom-preferences/{criteriaId}
**Auth**: RequireAuthenticated
**Route params**: `criteriaId` (int) — the `CustomCriteria` row being set.
**Request body** (`SetMyCustomPreferenceRequest`):
- `value` (string) — required in the sense that it must pass an async validator: the referenced criteria must exist AND be active (`IsActive: true`) AND `value` must be a legal value for that criteria's `DataType`/`Options` (checked via `CustomCriteriaOptionsValidation.IsValidValue` — e.g. for a `Select`-type criteria, `value` must be one of its configured option strings).
- `weight` (decimal) — required, must be in `[0, 1]` inclusive.

```json
{ "value": "EN", "weight": 0.8 }
```

**Response**: 204 No Content.
**Other status codes**: 400 — `criteriaId` ≤ 0, `weight` outside [0,1], OR the criteria doesn't exist/isn't active/`value` doesn't fit its data type (all funneled into one 400 under field name `Value`: "Tiêu chí không tồn tại/đã tắt, hoặc Value không hợp lệ với kiểu dữ liệu của tiêu chí."). 404 — a redundant existence re-check inside the handler itself (`NotFoundException`) that in practice is normally pre-empted by the 400 above since the validator already checks existence; kept as defense-in-depth.
**Notes**: Upsert — creates a new preference row if none exists for `(currentUserId, criteriaId)`, otherwise updates the existing one. Always sets `source` to `Explicit` regardless of what it was before (so a previously "Learned" value gets overwritten and reclassified once the user sets it manually).

---

## BankAccountsController

Base route: `api/v1/bank-accounts`. Controller-level `[Authorize(Policy = "RequireOwner")]` applies to every action. This registers the *payout destination* for either the caller's own venue (`ownerType: "Lounge"`) or a Performer record the caller created (`ownerType: "Performer"`) — this is what `Settlement.BankAccountId` / `Donation.BankAccountId` ultimately point at.

**Authorization model** (`BankAccountAccess.EnsureCanManageAsync`, applies identically to all 4 actions below):
- Admin role bypasses all ownership checks.
- `ownerType == "Lounge"`: caller must be the `Owner` (`MusicLounge.OwnerId == currentUserId`) of that specific venue.
- `ownerType == "Performer"`: caller must be the user who originally created that Performer record (`Performer.CreatedByUserId == currentUserId`) — Performers are a shared catalog; edit rights belong to the creator, not to every Owner who books that performer.
- Violating either → **403 Forbidden**. Referencing a `Lounge`/`Performer` id that doesn't exist → **404 Not Found**.

### GET /api/v1/bank-accounts
**Auth**: RequireOwner
**Query params**:
- `ownerType` (string, enum `BankAccountOwnerType`: `Lounge` | `Performer`) — required; missing or invalid value → 400 (model-binding failure, not a 422).
- `ownerId` (int) — required; missing → 400.

Example: `GET /api/v1/bank-accounts?ownerType=Lounge&ownerId=7`

**Response 200**: `ApiResponse<BankAccountDto[]>`, ordered default-account-first (`IsDefault` desc), then by `Id` asc.
- `id` (int)
- `ownerType` (string) — `Lounge` or `Performer`.
- `ownerId` (int)
- `bankName` (string)
- `accountNumber` (string) — **plaintext** (decrypted at this boundary only; stored encrypted at rest via `IPiiEncryptionService`).
- `accountHolder` (string)
- `isDefault` (bool)
- `isVerified` (bool) — only ever settable `true` by an Admin-only verify action elsewhere (not exposed on this controller); any subsequent `PUT` here resets it back to `false`.

```json
{
  "success": true,
  "data": [
    {
      "id": 12,
      "ownerType": "Lounge",
      "ownerId": 7,
      "bankName": "Vietcombank",
      "accountNumber": "0071001234567",
      "accountHolder": "CONG TY TNHH ABC LOUNGE",
      "isDefault": true,
      "isVerified": true
    }
  ],
  "message": null
}
```
**Other status codes**: 403 (not the venue's Owner / not the performer's creator, and not Admin). 404 (`ownerId` doesn't refer to an existing Lounge/Performer).

---

### GET /api/v1/bank-accounts/{id}
**Auth**: RequireOwner
**Route params**: `id` (int) — the `BankAccount` row id.
**Response 200**: `ApiResponse<BankAccountDto>` — same shape as one array element above.
**Other status codes**: 403, 404 (bank account id itself not found, checked before the ownership check).

---

### POST /api/v1/bank-accounts
**Auth**: RequireOwner
**Request body** (`CreateBankAccountCommand`):
- `ownerType` (string, enum `BankAccountOwnerType`: `Lounge` | `Performer`) — required.
- `ownerId` (int) — required, must be > 0.
- `bankName` (string) — required, non-empty, max 255 chars.
- `accountNumber` (string) — required, must match `^\d{6,19}$` (6–19 digits — covers Napas interbank format for all VN banks).
- `accountHolder` (string) — required, non-empty, max 255 chars.
- `isDefault` (bool) — required.

```json
{
  "ownerType": "Lounge",
  "ownerId": 7,
  "bankName": "Vietcombank",
  "accountNumber": "0071001234567",
  "accountHolder": "CONG TY TNHH ABC LOUNGE",
  "isDefault": true
}
```

**Response 201 Created**: `ApiResponse<int>` — `data` is the new bank account's id. `Location` header points to `GET /api/v1/bank-accounts/{id}`.
```json
{ "success": true, "data": 12, "message": null }
```
**Other status codes**: 400 (validation). 403 (not authorized to manage this owner). 404 (`ownerId` doesn't exist).
**Notes**: New accounts are always created with `isVerified: false` server-side — the request body's lack of that field is intentional, it cannot be set on create. If `isDefault: true` is sent, any other existing default account for the same `(ownerType, ownerId)` pair is atomically un-set first (separate DB round-trip, to satisfy a filtered unique index on "at most one default per owner").

---

### PUT /api/v1/bank-accounts/{id}
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** (`UpdateBankAccountRequest`, controller-local record) — note **`ownerType`/`ownerId` are NOT included and cannot be changed** via this endpoint; the account stays tied to its original owner.
- `bankName` (string) — required, non-empty, max 255 chars.
- `accountNumber` (string) — required, must match `^\d{6,19}$`.
- `accountHolder` (string) — required, non-empty, max 255 chars.
- `isDefault` (bool) — required.

```json
{ "bankName": "Techcombank", "accountNumber": "19001234567890", "accountHolder": "CONG TY TNHH ABC LOUNGE", "isDefault": false }
```

**Response**: 204 No Content.
**Other status codes**: 400 (validation). 403 (not authorized). 404 (`id` not found).
**Notes**: **Any successful update resets `isVerified` back to `false`**, even for fields that look unrelated (e.g. only toggling `isDefault`) — the handler unconditionally sets it, on the reasoning that any change to the account's identifying details invalidates a prior Admin verification. If `isDefault` flips from `false`→`true`, any sibling default for the same owner is un-set first (same two-round-trip pattern as Create).

---

**Endpoint count**: 7 (AuthController) + 16 (MeController) + 4 (BankAccountsController) = 27 endpoints documented.

---

## Part 2 — Venue / Lounge

## LoungesController

Base route: `api/v1/lounges`. 32 actions.

### GET /api/v1/lounges
**Auth**: AllowAnonymous, `[SwaggerOptionalAuth]` (bearer token optional — doesn't change this
endpoint's output today, since `IsFollowing`/follow status isn't in `LoungeListItemDto`; the marker
is present but this list endpoint doesn't currently use the caller identity except via `mine`).
**Query params**:
- `city` (string?, default null) — exact-match filter against `Address.City` (`==`, not a
  contains/LIKE search). Case sensitivity depends on DB collation — don't rely on either.
- `mine` (bool, default false) — if true, returns only lounges owned by the caller, **at every
  status** (Pending/Rejected included), and **requires authentication**: throws
  `UnauthorizedException` (**401**) if the caller has no valid bearer token. If false (default), the
  city/anonymous view only ever returns `Status == Approved` lounges (Pending/Rejected are filtered
  out server-side, not just hidden by other means).
- `page` (int, default 1)
- `pageSize` (int, default 20, clamped to 1–50 server-side)

**Response 200**: `data` = `PaginatedResult<LoungeListItemDto>`. `LoungeListItemDto` fields:
- `id` (int)
- `name` (string)
- `primaryImageUrl` (string?)
- `businessLicenseUrl` (string?)
- `model3DUrl` (string?) — the `.glb` procedural-scene URL (unrelated to the 360° tour scenes below)
- `areaLayoutImageUrl` (string?)
- `street` (string)
- `district` (string)
- `city` (string)
- `followerCount` (int)
- `upcomingShowCount` (int) — count of this lounge's `Published`/`Ongoing` shows with
  `ScheduledStart > now`
- `status` (string, enum `LoungeStatus`) — allowed values: `Pending`, `Approved`, `Rejected`,
  `Warned`, `Suspended`, `Locked`. Only meaningful when `mine=true`; the public/anonymous listing is
  always implicitly `Approved`.
- `rejectionReason` (string?) — Admin's reason if `status == Rejected`; always null otherwise

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 12,
        "name": "Cầm Ca Lounge",
        "primaryImageUrl": "/uploads/images/lounge-12-primary.jpg",
        "businessLicenseUrl": "/uploads/docs/lounge-12-license.pdf",
        "model3DUrl": null,
        "areaLayoutImageUrl": "/uploads/images/lounge-12-layout.jpg",
        "street": "12 Nguyễn Huệ",
        "district": "Quận 1",
        "city": "Hồ Chí Minh",
        "followerCount": 348,
        "upcomingShowCount": 2,
        "status": "Approved",
        "rejectionReason": null
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 57,
    "totalPages": 3,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: **401** if `mine=true` without a valid token.
**Notes**: Not gated behind BR-01 for `mine=true` — an Owner sees their own Pending/Rejected venues
in this list (useful for a "my venues" management screen showing draft/rejected states).

---

### GET /api/v1/lounges/{id}
**Auth**: AllowAnonymous, `[SwaggerOptionalAuth]` — sending a valid bearer token enriches the
response with `isFollowing`; anonymous callers get `isFollowing: null`.
**Route params**: `id` (int) — lounge id.
**Response 200**: `data` = `LoungeDetailDto`:
- `id` (int)
- `name` (string)
- `primaryImageUrl` (string?)
- `model3DUrl` (string?)
- `areaLayoutImageUrl` (string?)
- `street` (string), `ward` (string), `district` (string), `city` (string)
- `fullAddress` (string) — server-composed `"{street}, {ward}, {district}, {city}"`, skipping any
  empty ward/district segment
- `latitude` (double?), `longitude` (double?)
- `followerCount` (int)
- `upcomingShowCount` (int)
- `isFollowing` (bool?) — null when the caller is anonymous; `true`/`false` when authenticated
- `description` (string?)
- `atmosphereName` (string?) — null if the lounge has no `AtmosphereId` set
- `galleryImages` (array of `LoungeGalleryImageDto`):
  - `id` (int), `imageUrl` (string), `caption` (string?), `orderIndex` (int)
- `ownerId` (int)
- `status` (string) — **note the C# type differs from the list DTO**: here it's a plain `string`
  (`lounge.Status.ToString()`), not the `LoungeStatus` enum type — but the JSON value is identical
  either way (e.g. `"Approved"`) since both go through the same enum-to-string rendering.

```json
{
  "success": true,
  "data": {
    "id": 12,
    "name": "Cầm Ca Lounge",
    "primaryImageUrl": "/uploads/images/lounge-12-primary.jpg",
    "model3DUrl": null,
    "areaLayoutImageUrl": "/uploads/images/lounge-12-layout.jpg",
    "street": "12 Nguyễn Huệ",
    "ward": "Bến Nghé",
    "district": "Quận 1",
    "city": "Hồ Chí Minh",
    "fullAddress": "12 Nguyễn Huệ, Bến Nghé, Quận 1, Hồ Chí Minh",
    "latitude": 10.7756,
    "longitude": 106.7019,
    "followerCount": 348,
    "upcomingShowCount": 2,
    "isFollowing": true,
    "description": "Không gian nhạc acoustic ấm cúng giữa lòng thành phố.",
    "atmosphereName": "Ấm cúng",
    "galleryImages": [
      { "id": 4, "imageUrl": "/uploads/images/lounge-12-gallery-1.jpg", "caption": "Sân khấu chính", "orderIndex": 0 }
    ],
    "ownerId": 7,
    "status": "Approved"
  },
  "message": null
}
```
**Other status codes**: **404** if the lounge doesn't exist, OR (BR-01) if it's `Pending`/`Rejected`
and the caller is neither its Owner, an assigned Staff (`currentUser.LoungeId == lounge.Id`), nor
Admin — this is a deliberate existence-leak prevention: a non-privileged caller gets the exact same
404 whether the venue doesn't exist or is just not approved yet.
**Notes**: The `GetLoungeZonesQueryHandler`/tour/detail queries never eager-load navigations — this
handler does 2 extra round trips (upcoming-show-count batch query, gallery images) rather than a
single joined query; irrelevant to FE but explains response latency.

---

### POST /api/v1/lounges
**Auth**: RequireOwner (`Owner` or `Admin` role)
**Request body** — binds `CreateLoungeCommand` directly:
- `name` (string) — required, max 255
- `description` (string?) — optional, max 2000
- `atmosphereId` (int?) — optional; if provided, must reference an existing `VenueAtmosphere` row or
  400 (`"AtmosphereId không tồn tại."`)
- `street` (string) — required, max 255
- `ward` (string) — **non-nullable C# `string`, so technically required by the model binder (missing
  → 400), but the validator itself has no `NotEmpty()` rule for it** — an empty string `""` passes
  FluentValidation.
- `district` (string) — same as `ward`: model-binder-required, but the validator only checks max
  length 100, no `NotEmpty()` (Vietnam's 2025 administrative reform, NQ 1171/NQ-UBTVQH15, dropped
  District/Ward as a mandatory admin level in many provinces — comment in source explicitly notes
  this).
- `city` (string) — required, max 100
- `latitude` (double?) — optional; if present must be in [-90, 90]
- `longitude` (double?) — optional; if present must be in [-180, 180]

```json
{
  "name": "Cầm Ca Lounge",
  "description": "Không gian nhạc acoustic ấm cúng giữa lòng thành phố.",
  "atmosphereId": 3,
  "street": "12 Nguyễn Huệ",
  "ward": "Bến Nghé",
  "district": "Quận 1",
  "city": "Hồ Chí Minh",
  "latitude": 10.7756,
  "longitude": 106.7019
}
```
**Response 201**: `data` = the new lounge's `int` id. `Location` header points to
`GET /api/v1/lounges/{id}`.
**Other status codes**: **400** on any validator failure (empty required field, bad AtmosphereId,
out-of-range lat/long).
**Notes**: `OwnerId` is taken from the JWT, never from the body. New lounge starts `Status = Pending`
(BR-01) — it will 404 for everyone except the Owner/Staff/Admin, and won't appear in the public
`GET /lounges` list, until an Admin approves it via the Admin-group endpoints noted above.

---

### PUT /api/v1/lounges/{id}
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `UpdateLoungeRequest` (identical shape/validation to `CreateLoungeCommand` minus
`AtmosphereId`'s existence check being absent here — the Update validator does NOT re-verify
`atmosphereId` exists, unlike Create):
- `name` (string) — required, max 255
- `description` (string?) — max 2000
- `atmosphereId` (int?) — optional, **no existence check on update** (a stale/invalid id silently
  saves; only Create validates it)
- `street` (string) — required, max 255
- `ward` (string) — required by model binder, no `NotEmpty` validator rule
- `district` (string) — no `NotEmpty` rule, max 100
- `city` (string) — required, max 100
- `latitude` (double?) — [-90, 90] if present
- `longitude` (double?) — [-180, 180] if present

```json
{
  "name": "Cầm Ca Lounge (đổi tên)",
  "description": "Mô tả mới.",
  "atmosphereId": 3,
  "street": "12 Nguyễn Huệ",
  "ward": "Bến Nghé",
  "district": "Quận 1",
  "city": "Hồ Chí Minh",
  "latitude": 10.7756,
  "longitude": 106.7019
}
```
**Response 204**: no body.
**Other status codes**: **400** validator failure; **403** if caller isn't this lounge's Owner
(Admin bypass DOES apply here); **404** if lounge doesn't exist.
**Notes**: Full replace of `Name`/`Description`/`AtmosphereId`/`Address` — there is no PATCH; fields
you omit still need to be resent with their current value or they'll be overwritten (all are
required by the request DTO except `description`/`atmosphereId`/`latitude`/`longitude`).

---

### DELETE /api/v1/lounges/{id}
**Auth**: RequireOwner
**Route params**: `id` (int)
**Response 204**: no body.
**Other status codes**: **403** if not this lounge's Owner (**no Admin bypass** — handler checks
`lounge.OwnerId != _currentUser.UserId` only, ignoring role); **404** if lounge doesn't exist;
**409** (`ConflictException`) if the lounge has EVER had any `LoungeShow`, `LoungeStaff`,
`FnbOrder`, or `VenuePenalty` row — hard delete is only allowed for a venue with zero history of any
kind. Message: `"Venue này đã từng có event, nhân viên, đơn F&B hoặc penalty — không thể xoá, dữ
liệu lịch sử cần được giữ lại."`
**Notes**: This is a genuine hard delete (`DbSet.Remove`), not a soft-delete/status flip — there is
no "undo". Most real venues will hit the 409 almost immediately after any activity.

---

### GET /api/v1/lounges/{id}/staff
**Auth**: RequireOwner
**Route params**: `id` (int)
**Response 200**: `data` = array of `LoungeStaffDto`:
- `id` (int) — the `LoungeStaff` assignment row id (not the user id)
- `userId` (int)
- `fullName` (string) — `"(deleted)"` if the user row is gone
- `email` (string) — `""` if the user row is gone
- `isActive` (bool)
- `assignedAt` (DateTimeOffset)
- `deactivatedAt` (DateTimeOffset?)

```json
{
  "success": true,
  "data": [
    {
      "id": 9,
      "userId": 41,
      "fullName": "Trần Văn A",
      "email": "tranvana@example.com",
      "isActive": true,
      "assignedAt": "2026-07-01T09:00:00+07:00",
      "deactivatedAt": null
    }
  ],
  "message": null
}
```
**Other status codes**: **403** if not this lounge's Owner (Admin bypass applies); **404** if
lounge doesn't exist.
**Notes**: Returns EVERY assignment ever made (active and deactivated), ordered newest-first by
`assignedAt` — filter client-side on `isActive` for a "current staff" view.

---

### GET /api/v1/lounges/staff/lookup
**Auth**: RequireOwner
**Query params**: `email` (string, required — not validated by FluentValidation, just an exact-match
DB lookup)
**Response 200**: `data` = `UserLookupDto`:
- `id` (int)
- `fullName` (string)
- `email` (string)

Deliberately does NOT return `role` — comment in source: exposing role would turn this into a
"probe anyone's role by guessing their email" tool.
**Other status codes**: **404** if no user has that exact email.
**Notes**: Use this before `POST /{id}/staff` to resolve an email to a `userId` — the assign endpoint
takes `userId`, not email.

---

### POST /api/v1/lounges/{id}/staff
**Auth**: RequireOwner
**Route params**: `id` (int) — target lounge
**Request body** — `AssignStaffRequest`:
- `userId` (int) — required (non-nullable int; missing → 400 from model binder)

```json
{ "userId": 41 }
```
**Response 201**: `data` = the new `LoungeStaff` assignment's `int` id. `Location` header →
`GET /api/v1/lounges/{id}/staff`.
**Other status codes**:
- **400** if `LoungeId`/`UserId` don't reference existing rows (`AssignStaffCommandValidator`)
- **403** if caller isn't this lounge's Owner — **no Admin bypass** here (handler checks
  `lounge.OwnerId != _currentUser.UserId` only)
- **404** if lounge or user not found (also covered by the validator's 400 path in practice, but the
  handler re-checks and would 404 in a race)
- **409** in three cases: (a) target user's role is `Owner` or `Admin` — can't be made staff; (b)
  user is already an active staff member of THIS venue; (c) user is an active staff member of a
  DIFFERENT venue — **this system enforces 1 account = 1 active venue for staff** (confirmed
  intentional business rule, not a bug).
**Notes**: Side effect — if the target user's role is currently `Audience`, it's silently promoted
to `Staff` server-side (needed for `RequireStaff`/`RequireVenueOperator` policies to ever grant them
anything). Logged via `ILogger.LogWarning` as a security-relevant event.

---

### DELETE /api/v1/lounges/{id}/staff/{staffId}
**Auth**: RequireOwner
**Route params**: `id` (int) — lounge id (used only for the route shape, not re-validated against
`staffId`'s actual lounge in the handler — the handler resolves `staffId`'s lounge independently);
`staffId` (int) — the `LoungeStaff` assignment row id (NOT the user id).
**Response 204**: no body.
**Other status codes**: **403** if caller isn't the assignment's lounge's Owner — **no Admin
bypass**; **404** if the `LoungeStaff` row doesn't exist; **409** if that assignment is already
`isActive == false`.
**Notes**: Side effect — if this was the user's last active staff assignment anywhere, their role is
demoted back from `Staff` to `Audience` automatically (mirrors the promotion in Assign).

---

### PUT /api/v1/lounges/{id}/image
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `SetLoungeImageRequest`:
- `imageUrl` (string) — required, max 500

```json
{ "imageUrl": "/uploads/images/lounge-12-primary-v2.jpg" }
```
**Response 204**: no body.
**Other status codes**: **400** empty/too-long url; **403** not Owner/Admin; **404** lounge not
found.
**Notes**: Sets `PrimaryImageUrl` — the single card-thumbnail image used in list views (distinct
from gallery images and tour scenes). No image-moderation gate on this one (unlike gallery/tour
uploads) — only those two run `IImageModerationGate`.

---

### PUT /api/v1/lounges/{id}/business-license
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `SetBusinessLicenseRequest`:
- `documentUrl` (string) — required, max 500

```json
{ "documentUrl": "/uploads/docs/lounge-12-license.pdf" }
```
**Response 204**: no body.
**Other status codes**: **400**; **403**; **404**.
**Notes**: No format/extension check server-side — any URL string under 500 chars is accepted.

---

### PUT /api/v1/lounges/{id}/model-3d
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `SetModel3DRequest`:
- `modelUrl` (string?) — optional, max 500. `null` clears it back to the default procedural scene.

```json
{ "modelUrl": "/uploads/models/lounge-12.glb" }
```
**Response 204**: no body.
**Other status codes**: **403**; **404** (this endpoint has no 400 in its `[ProducesResponseType]`
list, and there's genuinely no way to trigger 400 here since the only field is nullable with just a
max-length rule).
**Notes**: This is the **`.glb` procedural 3D model** field — completely separate feature from the
360° photo tour (`/tour/...` endpoints below). Don't conflate the two: `Model3DUrl` is one file a
developer/Owner hand-authors; the tour is many real photos stitched/uploaded by the Owner.

---

### GET /api/v1/lounges/{id}/zones
**Auth**: AllowAnonymous
**Route params**: `id` (int)
**Query params**: `activeOnly` (bool, default false)
**Response 200**: `data` = array of `SeatingZoneDto` (not paginated — returns the full list):
- `id` (int), `loungeId` (int), `name` (string), `description` (string?), `capacity` (int),
  `displayOrder` (int), `isActive` (bool)
- `layoutColor` (string?) — hex color for the 2D zone rectangle
- `layout2DX` / `layout2DY` / `layout2DWidth` / `layout2DHeight` / `layout2DRotationDeg` (double?) —
  all null until `PUT .../layout-2d` is called
- `layout3DX` / `layout3DY` / `layout3DZ` (double?) — all null until `PUT .../layout-3d` is called

```json
{
  "success": true,
  "data": [
    {
      "id": 5,
      "loungeId": 12,
      "name": "Khu VIP",
      "description": "Gần sân khấu",
      "capacity": 20,
      "displayOrder": 0,
      "isActive": true,
      "layoutColor": "#D4AF37",
      "layout2DX": 10.5,
      "layout2DY": 20.0,
      "layout2DWidth": 30.0,
      "layout2DHeight": 15.0,
      "layout2DRotationDeg": 0.0,
      "layout3DX": null,
      "layout3DY": null,
      "layout3DZ": null
    }
  ],
  "message": null
}
```
**Notes**: Sorted by `displayOrder`, which is assigned at creation time = the count of existing
zones at that moment (i.e. creation order); there's no reorder endpoint.

---

### POST /api/v1/lounges/{id}/zones
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `CreateZoneRequest`:
- `name` (string) — required, max 100
- `description` (string?) — max 500
- `capacity` (int) — required (non-nullable), must be > 0

```json
{ "name": "Khu VIP", "description": "Gần sân khấu", "capacity": 20 }
```
**Response 201**: `data` = new zone's `int` id. `Location` → `GET /api/v1/lounges/{id}/zones`.
**Other status codes**: **403** not Owner/Admin; **404** lounge not found. (400 is possible from the
validator — `capacity <= 0` or empty `name` — even though not separately called out, it's the
standard FluentValidation → 400 path.)
**Notes**: `isActive` starts `true`; layout fields all start null.

---

### PUT /api/v1/lounges/{id}/zones/{zoneId}
**Auth**: RequireOwner
**Route params**: `id` (int, unused for lookup — the handler resolves the zone's actual lounge
independently), `zoneId` (int)
**Request body** — `UpdateZoneRequest`: same shape as Create (`name`, `description?`, `capacity`).
**Response 204**.
**Other status codes**: **403** not the zone's lounge's Owner/Admin; **404** zone not found.
**Notes**: Only `Name`/`Description`/`Capacity` are touched — layout fields are untouched by this
endpoint (use the layout-2d/layout-3d endpoints for those).

---

### DELETE /api/v1/lounges/{id}/zones/{zoneId}
**Auth**: RequireOwner
**Route params**: `id` (int, unused for lookup), `zoneId` (int)
**Response 204**.
**Other status codes**: **403**; **404**.
**Notes**: This is a **soft delete** — sets `IsActive = false`, doesn't remove the row. Use
`GET .../zones?activeOnly=true` to filter deactivated zones out of a display list.

---

### PUT /api/v1/lounges/{id}/zones/{zoneId}/layout-2d
**Auth**: RequireOwner
**Route params**: `id` (int, unused for lookup), `zoneId` (int)
**Request body** — `SetZoneLayout2DRequest`:
- `x` (double) — required, [0, 100] — percentage-based coordinate, resolution-independent
- `y` (double) — required, [0, 100]
- `width` (double) — required, (0, 100]
- `height` (double) — required, (0, 100]
- `rotationDeg` (double) — required, [-360, 360]
- `color` (string?) — optional; if present must match `^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$` (hex
  RGB or RGBA)

```json
{ "x": 10.5, "y": 20.0, "width": 30.0, "height": 15.0, "rotationDeg": 0, "color": "#D4AF37" }
```
**Response 204**.
**Other status codes**: **400** out-of-range coordinate or bad color format; **403**; **404**.
**Notes**: This is the flat 2D floor-plan rectangle drawn over `AreaLayoutImageUrl` — there's no
"unset"/null option for this endpoint (unlike layout-3d and position); to remove a 2D rectangle you
must re-draw it or the zone stays positioned.

---

### PUT /api/v1/lounges/{id}/zones/{zoneId}/layout-3d
**Auth**: RequireOwner
**Route params**: `id` (int, unused for lookup), `zoneId` (int)
**Request body** — `SetZoneLayout3DRequest`:
- `x` (double?), `y` (double?), `z` (double?) — must ALL be present or ALL be null; a mix is a 400
  (`"X/Y/Z phải cùng có giá trị hoặc cùng để trống (xóa vị trí)."`). Sending all three `null` clears
  the marker.

```json
{ "x": 1.2, "y": 0.0, "z": -3.4 }
```
**Response 204**.
**Other status codes**: **400** mixed null/non-null; **403**; **404**.
**Notes**: No numeric range validation on X/Y/Z (unlike 2D's 0–100) — these are free 3D-scene-space
coordinates matching whatever the `.glb` model's coordinate system uses.

---

### PUT /api/v1/lounges/{id}/area-layout-image
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `SetAreaLayoutImageRequest`:
- `imageUrl` (string?) — optional, max 500. `null` clears it (zones fall back to auto-layout when
  rendering a floor-plan view).

```json
{ "imageUrl": "/uploads/images/lounge-12-layout.jpg" }
```
**Response 204**.
**Other status codes**: **403**; **404**.
**Notes**: This is the SAME image referenced as `FloorPlanImageUrl` in the 360° tour DTO
(`VenueTourDto.floorPlanImageUrl` below) — one field reused as the background for both the seating
zone map AND the tour scene position markers, not two separate images.

---

## 360° Virtual Tour endpoints (`/lounges/{id}/tour/...`)

Louvre/museum-style panorama tour: multiple 360° photos ("scenes") the Owner captures/uploads,
connected by clickable "hotspots". **Completely separate feature from `model-3d` above** (one
hand-authored `.glb` file vs. many real photos of the actual venue) — gated by the active
subscription's `MaxTourScenesSnapshot` (a value snapshotted at subscribe-time so a later Admin
package edit can't shrink a tour mid-subscription).

### GET /api/v1/lounges/{id}/tour
**Auth**: AllowAnonymous
**Route params**: `id` (int)
**Response 200**: `data` = `VenueTourDto`:
- `loungeId` (int)
- `floorPlanImageUrl` (string?) — same value as `AreaLayoutImageUrl` (see note above)
- `scenes` (array of `VenueTourSceneDto`, ordered by `orderIndex`):
  - `id` (int)
  - `imageUrl` (string)
  - `name` (string?)
  - `orderIndex` (int)
  - `positionX` (double?), `positionY` (double?) — marker position on `floorPlanImageUrl`, null
    until set via the position endpoint
  - `hotspots` (array of `VenueTourHotspotDto`):
    - `id` (int)
    - `type` (string, enum `VenueTourHotspotType`) — allowed values: `Navigate` (jumps viewer to
      `targetSceneId`), `Info` (shows `infoText` in place, no navigation), `LivestreamScreen` (a
      spatial marker for "a stage screen is here"; content resolved dynamically per-request, not
      stored)
    - `yaw` (double), `pitch` (double)
    - `label` (string?)
    - `targetSceneId` (int?) — only meaningful/non-null for `Navigate` hotspots
    - `infoText` (string?)
    - `liveLivestreamId` (int?) — **only ever non-null for a `LivestreamScreen` hotspot**, and only
      when there's a currently-Live show with `PlaybackMode.ThreeD` for this lounge at request time.
      Resolved dynamically each call — never persisted on the hotspot.
    - `liveHlsUrl` (string?) — additionally null unless the caller passes the SAME access check
      `GetLivestreamDetailQuery` uses (`LivestreamAccessPolicy`) — an anonymous/non-ticketholder
      caller sees the screen exists (`liveLivestreamId`/`liveViewerCount` populated) but not the
      playable stream URL.
    - `liveViewerCount` (int?)
  - `completedByAi` (bool) — true if this scene's panorama was partially AI-completed (gap-fill) via
    the stitch pipeline's opt-in `CompleteWithAi` flag
  - `aiDisclosureText` (string?) — non-null (fixed Vietnamese wording) only when `completedByAi` is
    true, per Vietnam AI Law disclosure requirements — FE should render this verbatim when present
    rather than composing its own copy

```json
{
  "success": true,
  "data": {
    "loungeId": 12,
    "floorPlanImageUrl": "/uploads/images/lounge-12-layout.jpg",
    "scenes": [
      {
        "id": 3,
        "imageUrl": "/uploads/images/lounge-12-scene-1.jpg",
        "name": "Sảnh chính",
        "orderIndex": 0,
        "positionX": 40.0,
        "positionY": 55.0,
        "hotspots": [
          {
            "id": 8,
            "type": "Navigate",
            "yaw": 90.0,
            "pitch": 0.0,
            "label": "Đi đến sân khấu",
            "targetSceneId": 4,
            "infoText": null,
            "liveLivestreamId": null,
            "liveHlsUrl": null,
            "liveViewerCount": null
          },
          {
            "id": 9,
            "type": "LivestreamScreen",
            "yaw": -45.0,
            "pitch": 5.0,
            "label": "Màn hình sân khấu",
            "targetSceneId": null,
            "infoText": null,
            "liveLivestreamId": 101,
            "liveHlsUrl": "https://stream.mux.com/abc123.m3u8",
            "liveViewerCount": 24
          }
        ],
        "completedByAi": false,
        "aiDisclosureText": null
      }
    ]
  },
  "message": null
}
```
**Other status codes**: **404** if lounge doesn't exist.
**Notes**: Public/anonymous — used to preview a venue before buying a ticket. Note this endpoint does
NOT apply the BR-01 pending/rejected visibility gate the way `GetLoungeDetail` does (no
`isPendingOrRejected` check here) — worth double-checking with backend if a Pending venue's tour data
should really be publicly fetchable by numeric id guess before its listing goes live.

---

### POST /api/v1/lounges/{id}/tour/scenes
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `AddVenueTourSceneRequest`:
- `imageUrl` (string) — required, max 500 — must already be uploaded via `POST /uploads/images`
  first (no SSRF-style server-side fetch validation on THIS endpoint specifically, unlike stitch
  below, but is expected to be an already-hosted URL)
- `name` (string?) — optional, max 100

```json
{ "imageUrl": "/uploads/images/lounge-12-scene-2.jpg", "name": "Khu vực bar" }
```
**Response 201**: `data` = new scene's `int` id. `Location` → `GET /api/v1/lounges/{id}/tour`.
**Other status codes**:
- **400** empty/too-long `imageUrl`, too-long `name`
- **403** not Owner/Admin
- **404** lounge not found
- **422** (`DomainException`) in two distinct cases: (a) no active subscription supports tour scenes
  at all (`maxScenes == 0`) — message: `"Gói subscription hiện tại không hỗ trợ tour ảo 360° — vui
  lòng nâng cấp gói."`; (b) already at the active subscription's `MaxTourScenesSnapshot` limit —
  message includes the actual limit number. ALSO 422 if the uploaded image fails AI moderation
  (`IImageModerationGate.CheckOrThrowAsync` throws `DomainException` when the moderation score is
  at/above the block threshold — config default 85%) — **this 422 path is NOT listed in the
  controller's `[ProducesResponseType]` attributes for THIS specific endpoint, but the underlying
  `AddVenueTourSceneCommandHandler` code path is identical to what IS declared as 422, so it fires
  the same way.**
**Notes**: `orderIndex` is auto-assigned = current scene count (creation order; no reorder endpoint).
If the image scores in the "review" band (below block, at/above a lower review threshold, default
50%) the scene is still created but silently flagged into the `EventModeration` review queue —
succeeds with 201 but may get pulled later by Admin moderation action (not reflected in this
response).

---

### POST /api/v1/lounges/{id}/tour/scenes/stitch
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `StitchVenueTourSceneRequest`:
- `sourceImageUrls` (string[]) — required, 2–20 items; **each URL must start with `"/uploads/"`**
  (relative path) — this is an intentional SSRF gate: the panorama-stitcher microservice fetches
  whatever URL it's given with no restriction of its own, so only URLs shaped like the existing
  authenticated upload endpoint's output are accepted (an allowlist by construction). An external
  absolute URL (`https://...`) is rejected with 400. Each entry also max 500 chars.
- `name` (string?) — optional, max 100

```json
{
  "sourceImageUrls": [
    "/uploads/images/raw-1.jpg",
    "/uploads/images/raw-2.jpg",
    "/uploads/images/raw-3.jpg"
  ],
  "name": "Sảnh chính (ghép ảnh)"
}
```
**IMPORTANT — a field the command supports but this request body does NOT expose**: the underlying
`StitchVenueTourSceneCommand` also has a `completeWithAi` (bool, default `false`) field for opting
into AI gap-fill of a partial panorama, but the controller's `StitchVenueTourSceneRequest` record
only carries `sourceImageUrls`/`name` — **`completeWithAi` is hardcoded to `false` server-side for
every call through this HTTP endpoint today.** There is currently no way for FE to trigger the AI
completion path via this API; it would need a new field added to the controller's request record
before FE can wire it up.

**Response 202 Accepted**: `data` = the new `VenueTourStitchAttempt`'s `int` id (poll it, see next
endpoint). `Location` header → `GET /api/v1/lounges/{id}/tour/scenes/stitch/{attemptId}`.
**Other status codes**:
- **400** wrong photo count (<2 or >20), a URL not starting with `/uploads/`, too-long name
- **403** not Owner/Admin
- **404** lounge not found
- **422** (`DomainException`): (a) subscription tour-scene quota reached/unsupported (same messages
  as `AddTourScene` above — this is checked BEFORE the attempt row is created, so a quota failure
  never creates a Pending attempt); (b) anti-abuse cap — total stitch attempts (any outcome,
  including still-Pending ones) for this lounge has reached `tour_stitch_max_attempts_per_lounge`
  (config default 20) — message: `"Venue này đã đạt giới hạn {N} lần ghép ảnh. Vui lòng liên hệ hỗ
  trợ nếu cần thêm."`
**Notes**: **Runs in the background** (Hangfire job `StitchVenueTourSceneJob`) — a stitch can take
15–30+ seconds and occasionally brushes the stitcher microservice's 120s HTTP timeout on harder
photo sets. This endpoint does the synchronous checks (ownership, quota, anti-abuse) and creates a
`Pending` attempt row, then returns immediately; FE must poll the next endpoint for the real outcome.
On success the job creates a real `VenueTourScene` (counts against the same `MaxTourScenesSnapshot`
quota as `AddTourScene` — it's still "one more scene" either way).

---

### GET /api/v1/lounges/{id}/tour/scenes/stitch/{attemptId}
**Auth**: RequireOwner
**Route params**: `id` (int), `attemptId` (int)
**Response 200**: `data` = `VenueTourStitchAttemptDto`:
- `id` (int)
- `status` (string, enum `VenueTourStitchStatus`) — allowed values: `Pending`, `Succeeded`, `Failed`
- `resultSceneId` (int?) — only set once `status == "Succeeded"`
- `errorMessage` (string?) — only set once `status == "Failed"`

```json
{
  "success": true,
  "data": { "id": 55, "status": "Succeeded", "resultSceneId": 9, "errorMessage": null }
}
```
**Other status codes**: **403** not Owner/Admin; **404** attempt doesn't exist or belongs to a
different lounge.
**Notes**: Poll this after the 202 from the stitch endpoint. Recommended polling interval isn't
specified server-side — pick something like 2–3s given the 15–30s typical completion window.

---

### DELETE /api/v1/lounges/{id}/tour/scenes/{sceneId}
**Auth**: RequireOwner
**Route params**: `id` (int), `sceneId` (int)
**Response 204**.
**Other status codes**: **403**; **404** (scene not found, or found but belongs to a different
lounge — same 404 either way, no distinguishing information leaked).
**Notes**: Side effects FE should know about since they silently mutate OTHER rows: (1) any hotspot
elsewhere in the tour whose `targetSceneId` pointed at this now-deleted scene is itself DELETED (not
just nulled) — a dangling "Navigate to this scene" hotspot in another scene disappears entirely,
possibly surprising if the Owner expected it to just go inert; (2) any `VenueTourStitchAttempt` log
row whose `resultSceneId` pointed here has that reference nulled (the audit-trail row itself is kept,
only the now-dead scene link is cleared) — a previously-`Succeeded` attempt's `resultSceneId` can go
from an int to `null` after this call.

---

### PUT /api/v1/lounges/{id}/tour/scenes/{sceneId}/position
**Auth**: RequireOwner
**Route params**: `id` (int), `sceneId` (int)
**Request body** — `SetVenueTourScenePositionRequest`:
- `x` (double?), `y` (double?) — must be BOTH present or BOTH null (400 otherwise, same "all or
  nothing" pattern as zone layout-3d); when present, each must be in [0, 100] (percentage coordinate
  on `floorPlanImageUrl`). Both null clears the marker.

```json
{ "x": 40.0, "y": 55.0 }
```
**Response 204**.
**Other status codes**: **400** mixed null/non-null or out-of-range; **403**; **404** (lounge, or
scene not found/belongs to a different lounge).
**Notes**: This positions the scene's marker on the SAME floor-plan image as `SeatingZone` layout-2D
uses (`FloorPlanImageUrl` = `AreaLayoutImageUrl`).

---

### POST /api/v1/lounges/{id}/tour/scenes/{sceneId}/hotspots
**Auth**: RequireOwner
**Route params**: `id` (int), `sceneId` (int)
**Request body** — `AddVenueTourHotspotRequest`:
- `type` (string) — required, must parse (case-insensitive) as `VenueTourHotspotType`: `Navigate`,
  `Info`, or `LivestreamScreen`
- `yaw` (double) — required, [-180, 180]
- `pitch` (double) — required, [-90, 90]
- `label` (string?) — optional, max 100
- `targetSceneId` (int?) — **required when `type == "Navigate"`** (400 if missing in that case);
  ignored/nulled server-side for any other type even if you send it. Must not equal the hotspot's
  own `sceneId` (can't point at itself) — 400 if it does. If it points at a scene id that doesn't
  exist or belongs to a different lounge, that's a **404** (not 400) — a validation-shaped check
  enforced at the handler level via a DB lookup, not FluentValidation.
- `infoText` (string?) — optional, max 2000

```json
{
  "type": "Navigate",
  "yaw": 90.0,
  "pitch": 0.0,
  "label": "Đi đến sân khấu",
  "targetSceneId": 4,
  "infoText": null
}
```
**Response 201**: `data` = new hotspot's `int` id. `Location` → `GET /api/v1/lounges/{id}/tour` (not
a hotspot-specific GET — there isn't one; the tour endpoint is the only way to re-fetch hotspots).
**Other status codes**: **400** bad `type` string, out-of-range yaw/pitch, too-long label/infoText,
missing `targetSceneId` for `Navigate`, or `targetSceneId == sceneId`; **403** not Owner/Admin;
**404** lounge/scene not found, OR `targetSceneId` doesn't resolve to a real scene in this same
lounge.
**Notes**: For `LivestreamScreen` type, `targetSceneId`/`infoText` are purely inert (the dynamic
livestream fields in the response DTO come from `GetVenueTourQuery` resolving a currently-live show
at read time, never from anything stored on the hotspot itself).

---

### DELETE /api/v1/lounges/{id}/tour/hotspots/{hotspotId}
**Auth**: RequireOwner
**Route params**: `id` (int), `hotspotId` (int)
**Response 204**.
**Other status codes**: **403**; **404** (hotspot not found, or found but its scene belongs to a
different lounge).
**Notes**: Deleting a scene (`DELETE .../tour/scenes/{sceneId}`) already cascades to delete this
hotspot if it lived inside that scene — you only need this endpoint to remove one hotspot while
keeping its parent scene.

---

## Gallery endpoints

### POST /api/v1/lounges/{id}/gallery
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `AddLoungeGalleryImageRequest`:
- `imageUrl` (string) — required, max 500
- `caption` (string?) — optional, max 255

```json
{ "imageUrl": "/uploads/images/lounge-12-gallery-2.jpg", "caption": "Không gian ngoài trời" }
```
**Response 201**: `data` = new gallery image's `int` id. `Location` → `GET /api/v1/lounges/{id}`.
**Other status codes**: **400** empty/too-long fields; **403** not Owner/Admin; **404** lounge not
found; **422** (`DomainException`) if the image fails AI moderation at/above the block threshold —
**not declared in this endpoint's `[ProducesResponseType]` list (only 400/403/404 are), but the
handler runs the identical `IImageModerationGate.CheckOrThrowAsync` call `AddTourScene` uses, so it
CAN and does fire 422 in practice.**
**Notes**: Free for every Owner, no subscription gate (unlike tour scenes) — comment in source is
explicit that this is deliberate since gallery photos are just showcase images, not an interactive
feature. `orderIndex` auto-assigned = current gallery count. Same moderation-review-queue side
effect as tour scenes: a mid-threshold image still gets created (201) but is silently flagged for
Admin review.

---

### DELETE /api/v1/lounges/{id}/gallery/{imageId}
**Auth**: RequireOwner
**Route params**: `id` (int), `imageId` (int)
**Response 204**.
**Other status codes**: **403**; **404**.

---

## Custom Criteria endpoints

Owner-defined AI-recommendation criteria scoped to ONE lounge — distinct from the platform-wide
Genre/Mood/Atmosphere/Category taxonomy (Admin-managed, different controller).

### GET /api/v1/lounges/{id}/custom-criteria
**Auth**: RequireOwner
**Route params**: `id` (int)
**Response 200**: `data` = array of `CustomCriteriaDto`:
- `id` (int), `loungeId` (int), `name` (string), `key` (string)
- `dataType` (string, enum `CustomCriteriaDataType`) — allowed values: `Select`, `Range`,
  `Boolean`, `Text`
- `options` (string?) — raw JSON string, shape depends on `dataType` (see below)
- `isActive` (bool)

```json
{
  "success": true,
  "data": [
    { "id": 2, "loungeId": 12, "name": "Ngôn ngữ trình diễn", "key": "performance_language",
      "dataType": "Select", "options": "[\"VI\",\"EN\"]", "isActive": true }
  ]
}
```
**Notes**: **Unlike almost every other Owner-scoped GET in this controller, this handler does NOT
check that the caller owns lounge `{id}`** — `GetCustomCriteriaByLoungeQueryHandler` only filters by
`LoungeId`, with no `lounge.OwnerId != currentUser.UserId` check at all. Any authenticated Owner (or
Admin) can read ANY lounge's custom criteria by id, even one they don't own — worth flagging to
backend if this is unintended, since every sibling endpoint (create/update on this same resource) DOES
enforce ownership.

---

### POST /api/v1/lounges/{id}/custom-criteria
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** — `CreateCustomCriteriaRequest`:
- `name` (string) — required, max 100
- `key` (string) — required, max 100, must match `^[a-z][a-z0-9_]*$` (lowercase, starts with a
  letter, only letters/digits/underscore — e.g. `performance_language`); must be unique within this
  lounge (checked against existing rows; violating this is a 400 with message `"Key này đã tồn tại
  cho venue này."`, not a raw 409 from the DB unique index — the validator pre-checks it)
- `dataType` (string) — required, must parse as `CustomCriteriaDataType`: `Select`, `Range`,
  `Boolean`, `Text`
- `options` (string?) — required JSON shape depends on `dataType`:
  - `Select` → non-empty JSON string array, e.g. `["VI","EN"]`
  - `Range` → JSON object `{"min": <number>, "max": <number>}` with `min < max` (an optional
    `"step"` key is mentioned in a code comment as intended shape but is NOT actually validated/used
    by `CustomCriteriaOptionsValidation.IsValidOptions` — only `min`/`max` are checked)
  - `Boolean` / `Text` → `options` is unused/ignored regardless of what you send

```json
{
  "name": "Ngôn ngữ trình diễn",
  "key": "performance_language",
  "dataType": "Select",
  "options": "[\"VI\",\"EN\"]"
}
```
**Response 201**: `data` = new criteria's `int` id. `Location` → `GET /api/v1/lounges/{id}/custom-criteria`.
**Other status codes**: **400** any validator failure (bad key format, duplicate key, options shape
mismatch for the given dataType); **403** if caller isn't this lounge's Owner — **no Admin bypass**
(handler checks `lounge.OwnerId != _currentUser.UserId` only, unlike most other Owner-write
handlers in this controller); **404** lounge not found.
**Notes**: `isActive` starts `true` automatically.

---

### PUT /api/v1/lounges/{id}/custom-criteria/{criteriaId}
**Auth**: RequireOwner
**Route params**: `id` (int, unused for lookup — handler resolves criteria's actual lounge
independently), `criteriaId` (int)
**Request body** — `UpdateCustomCriteriaRequest`:
- `name` (string) — required, max 100
- `options` (string?) — validated against the EXISTING row's `dataType` (you cannot change
  `dataType` through this endpoint — see below — so `options`' shape must match whatever `dataType`
  was set at creation)
- `isActive` (bool) — required (non-nullable)

```json
{ "name": "Ngôn ngữ trình diễn", "options": "[\"VI\",\"EN\",\"FR\"]", "isActive": true }
```
**Response 204**.
**Other status codes**: **400** empty name / options don't match the existing dataType's shape;
**403** not this criteria's lounge's Owner — **no Admin bypass**; **404** criteria not found (also
implicitly 404 if the criteria id exists but its lounge lookup fails, extremely unlikely in
practice).
**Notes**: **`key` and `dataType` are intentionally NOT editable** — changing either after
`EventCustomValue`/`UserCustomPreference` rows have been saved against the old shape would corrupt
that stored data. To "deactivate" a criterion without deleting history, set `isActive: false` rather
than looking for a DELETE endpoint (there isn't one for this resource).

---

## VenuePenaltiesController

Base route: `api/v1/venue-penalties`. BR-28 — venue warnings/suspensions/bans and their appeals. 6
actions (a 6th was added 2026-08-18).

### GET /api/v1/venue-penalties
_(Added 2026-08-18)_
**Auth**: RequireAdmin — note this differs from the other GETs on this controller, which are
`RequireAuthenticated` (`/{id}`) and `RequireOwner` (`/mine`).
**Route params**: none
**Query params**:
- `status` (enum, optional) — `Active` | `Appealed` | `Overturned` | `Upheld` | `Expired`. Omit for all.
- `page` (int, default 1), `pageSize` (int, default 20, clamped 1–50)

**Response 200**: `PaginatedResult<AdminVenuePenaltyDto>` — same fields as `VenuePenaltyDto` (see
`GET /venue-penalties/{id}`) **plus** `ownerId` (int), `ownerName` (string), `ownerEmail` (string).

**Ordering**: by `Id` ascending (oldest first). Ordering by `IssuedAt` is deliberately avoided — a
`DateTimeOffset` sort does not translate under the SQLite provider the test suite runs on. Penalties are
never backdated (`IssuedAt` is set at insert), so `Id` order and `IssuedAt` order coincide.

**Why this endpoint exists**: the controller could already issue a penalty and review an appeal, but its
only list was `GET /mine`, scoped to the calling Owner — so an appeal an Owner submitted was **invisible to
the Admins meant to act on it**. Use `?status=Appealed` for the actionable queue: that is the only status
`POST /{id}/appeal/review` accepts (anything else → 422).

---

### POST /api/v1/venue-penalties
**Auth**: RequireAdmin
**Request body** — binds `IssuePenaltyRequest`:
- `loungeId` (int) — required, must reference an existing lounge (400 `"LoungeId không tồn tại."`
  otherwise)
- `penaltyType` (string) — required, case-insensitive match to `Warning`, `Suspension`, or `Ban`
  (`"PenaltyType phải là 'Warning', 'Suspension' hoặc 'Ban'."` on mismatch)
- `reason` (string) — required, max 1000
- `evidenceRef` (string?) — optional, max 500
- `suspensionDays` (int?) — **required and must be > 0 when `penaltyType == "Suspension"`**
  (`"Phạt tạm khoá cần khai số ngày tạm khoá."` / `"Số ngày tạm khoá phải lớn hơn 0."`); ignored
  (stored as null) for `Warning`/`Ban` even if sent.

```json
{
  "loungeId": 12,
  "penaltyType": "Suspension",
  "reason": "Vi phạm quy định về an toàn PCCC.",
  "evidenceRef": "/uploads/docs/inspection-report-2026-08.pdf",
  "suspensionDays": 14
}
```
**Response 201**: `data` = new penalty's `int` id. `Location` → `GET /api/v1/venue-penalties/{id}`.
**Other status codes**: **400** validator failure; **404** lounge not found.
**Notes**: Each severity takes effect on its own delay, NOT immediately (except `Warning`):
`effectiveAt` = now for `Warning`, `now + PenaltySuspensionNoticeHours` (config default 24h) for
`Suspension`, `now + PenaltyBanNoticeDays` (config default 7 days) for `Ban` — a background job
(`ApplyDuePenaltiesJob`) applies the actual venue-status change and any subscription compensation
once `effectiveAt` arrives. Only `Warning` is applied synchronously here (`lounge.Status` flips to
`Warned` immediately in this same request) — `Suspension`/`Ban` don't change `lounge.Status` until
the job runs later. The Owner is notified via `INotificationService` immediately regardless of the
delay.

---

### GET /api/v1/venue-penalties/{id}
**Auth**: RequireAuthenticated (any logged-in role) — but see handler-level restriction below
**Route params**: `id` (int)
**Response 200**: `data` = `VenuePenaltyDto`:
- `id` (int), `loungeId` (int), `loungeName` (string)
- `penaltyType` (string, enum `PenaltyType`) — `Warning`, `Suspension`, `Ban`
- `reason` (string), `evidenceRef` (string?)
- `issuedAt` (DateTimeOffset), `effectiveAt` (DateTimeOffset)
- `suspensionDays` (int?), `suspensionEnd` (DateTimeOffset?)
- `status` (string, enum `PenaltyStatus`) — allowed values: `Active`, `Appealed`, `Overturned`,
  `Upheld`, `Expired`
- `appealDeadline` (DateTimeOffset?), `appealedAt` (DateTimeOffset?), `appealReason` (string?),
  `appealResult` (string?) — mirrors the `Decision` string from a review (`"Overturned"`/`"Upheld"`)
- `reviewedAt` (DateTimeOffset?)

```json
{
  "success": true,
  "data": {
    "id": 88,
    "loungeId": 12,
    "loungeName": "Cầm Ca Lounge",
    "penaltyType": "Suspension",
    "reason": "Vi phạm quy định về an toàn PCCC.",
    "evidenceRef": "/uploads/docs/inspection-report-2026-08.pdf",
    "issuedAt": "2026-08-10T09:00:00+07:00",
    "effectiveAt": "2026-08-11T09:00:00+07:00",
    "suspensionDays": 14,
    "suspensionEnd": null,
    "status": "Appealed",
    "appealDeadline": "2026-08-13T09:00:00+07:00",
    "appealedAt": "2026-08-11T10:00:00+07:00",
    "appealReason": "Đã khắc phục toàn bộ lỗi PCCC, có biên bản nghiệm thu kèm theo.",
    "appealResult": null,
    "reviewedAt": null
  },
  "message": null
}
```
**Other status codes**: **403** if caller is neither this penalty's lounge's Owner NOR Admin; **404**
penalty (or its lounge) not found.
**Notes**: This single-item lookup didn't exist before — Owners could only see their own penalties
via `GET /mine`, and Admin had no single-item lookup either. Existence-check ordering means a
non-owner/non-admin gets a 403 (not 404) here — unlike the lounge-detail endpoint's deliberate
404-to-hide-existence pattern, this one DOES leak that penalty id `{id}` exists to any authenticated
caller (just not its details).

---

### GET /api/v1/venue-penalties/mine
**Auth**: RequireOwner
**Query params**: `page` (int, default 1), `pageSize` (int, default 20, clamped 1–50)
**Response 200**: `data` = `PaginatedResult<VenuePenaltyDto>` (same DTO shape as above).
**Notes**: Owner sees EVERY penalty ever issued against ANY of their lounges, at every status
(Active/Appealed/Overturned/Upheld/Expired) — not filtered to "currently in effect" only. Ordered by
`Id` ascending server-side (comment notes `Id` order == `IssuedAt` order always, since penalties are
never backdated, and ordering by `Id` avoids a `DateTimeOffset`-ordering translation issue on the
SQLite test provider).

---

### POST /api/v1/venue-penalties/{id}/appeal
**Auth**: RequireOwner
**Route params**: `id` (int) — the penalty id
**Request body** — `SubmitAppealRequest`:
- `appealReason` (string) — required, max 1000

```json
{ "appealReason": "Đã khắc phục toàn bộ lỗi PCCC, có biên bản nghiệm thu kèm theo." }
```
**Response 204**.
**Other status codes**:
- **403** if caller isn't this penalty's lounge's Owner — **no Admin bypass** (`SubmitAppeal` is
  conceptually Owner-only anyway, but worth noting the pattern continues here too)
- **404** penalty or its lounge not found
- **422** (`DomainException`) in two cases: (a) `penalty.Status != Active` (`"Chỉ có thể kháng cáo
  phạt đang ở trạng thái Active."`) — you can't appeal something already Appealed/Overturned/
  Upheld/Expired; (b) already appealed once before (`penalty.AppealedAt is not null` →
  `"Phạt này đã được kháng cáo trước đó."`) — one appeal attempt per penalty, no resubmission.
**Notes**: Sets a real SLA deadline — `appealDeadline = now + AppealSlaHours` (config default 48h).
If Admin doesn't resolve it by then, a background job (`AutoApproveOverdueAppealsJob`) auto-approves
(Overturned) it in the Owner's favor — protects against an unattended appeal permanently penalizing
a venue. All current Admin accounts are notified on submission.

---

### POST /api/v1/venue-penalties/{id}/appeal/review
**Auth**: RequireAdmin
**Route params**: `id` (int) — the penalty id
**Request body** — `ReviewAppealRequest`:
- `decision` (string) — required, case-insensitive match to `Overturned` or `Upheld`
  (`"Quyết định phải là 'Overturned' hoặc 'Upheld'."` otherwise)
- `reviewNote` (string?) — optional, max 500

```json
{ "decision": "Overturned", "reviewNote": "Đã xác minh biên bản nghiệm thu PCCC hợp lệ." }
```
**Response 204**.
**Other status codes**: **404** penalty (or its lounge) not found; **422** (`DomainException`) if
`penalty.Status != Appealed` (`"Chỉ có thể xử lý kháng cáo đang ở trạng thái Appealed."`) — can't
review something not currently in the Appealed state.
**Notes**: Concurrency-safe via a named async lock (`appeal-review:{penaltyId}`) shared with the
auto-approve background job, specifically to prevent a race where an Admin's manual decision and the
48h SLA auto-approve job both fire near-simultaneously with contradictory outcomes. On `Overturned`,
`lounge.Status` only resets to `Approved` if NO OTHER currently-applied Suspension/Ban penalty still
justifies keeping the venue locked (a venue can have multiple concurrent penalties). If the penalty
being overturned had ALREADY been applied (its `AppliedAt` was already set by `ApplyDuePenaltiesJob`
— i.e. a suspension day-extension or ban pro-rata refund already went through), this endpoint does
**NOT** auto-reverse that subscription/ledger effect — it just notifies all Admins that a manual
reversal is needed. FE showing an "appeal overturned" success message should not imply the venue's
subscription/billing was automatically made whole.

---

## PerformersController

Base route: `api/v1/performers`. Class-level `[Authorize(Policy = Policies.RequireOwner)]` — every
action requires role `Owner` or `Admin`; there is no anonymous/Audience access to any performer
endpoint. 7 actions.

Performers are a **shared catalog across all Owners**, not scoped to one venue — READ/CREATE/ASSIGN
are open to any Owner (or Admin); EDIT/DELETE are additionally restricted at the handler level to
`createdByUserId == caller` OR Admin (Admin bypass DOES apply on every Performers endpoint, unlike
several Lounges/VenuePenalties handlers noted above).

### GET /api/v1/performers
**Auth**: RequireOwner (class-level)
**Query params**: `search` (string?, default null) — substring match against `Name` (`.Contains`,
case sensitivity DB-collation-dependent); `page` (int, default 1); `pageSize` (int, default 20,
clamped 1–50)
**Response 200**: `data` = `PaginatedResult<PerformerDto>`. `PerformerDto` fields:
- `id` (int), `name` (string), `avatarUrl` (string?), `bio` (string?)
- `type` (string, enum `PerformerType`) — allowed values: `Solo`, `Band`
- `createdByUserId` (int?)
- `genreIds` (array of int), `genreNames` (array of string) — parallel arrays, same order/index
- `socialLinks` (array of `PerformerSocialLinkDto`):
  - `id` (int)
  - `platform` (string, enum `SocialPlatform`) — allowed values: `Spotify`, `Youtube`, `Soundcloud`,
    `Facebook`, `Instagram`
  - `url` (string)
  - `displayName` (string?)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 21,
        "name": "Lê Cát Trọng Lý",
        "avatarUrl": "/uploads/images/performer-21.jpg",
        "bio": "Ca sĩ, nhạc sĩ độc lập.",
        "type": "Solo",
        "createdByUserId": 7,
        "genreIds": [3, 8],
        "genreNames": ["Indie", "Acoustic"],
        "socialLinks": [
          { "id": 5, "platform": "Spotify", "url": "https://open.spotify.com/artist/xyz", "displayName": null }
        ]
      }
    ],
    "page": 1, "pageSize": 20, "totalCount": 143, "totalPages": 8,
    "hasNextPage": true, "hasPreviousPage": false
  },
  "message": null
}
```
**Notes**: This is the autocomplete/search Owners use before creating a duplicate performer profile.

---

### GET /api/v1/performers/{id}
**Auth**: RequireOwner (class-level)
**Route params**: `id` (int)
**Response 200**: `data` = single `PerformerDto` (same shape as above).
**Other status codes**: **404** performer not found.

---

### POST /api/v1/performers
**Auth**: RequireOwner (class-level) — open to ANY Owner, not restricted to a creator
**Request body** — binds `CreatePerformerCommand` directly:
- `name` (string) — required, max 200
- `avatarUrl` (string?) — optional, max 500
- `bio` (string?) — optional, max 2000
- `type` (string) — required, case-insensitive match to `Solo` or `Band`
- `genreIds` (int[]) — each id must reference an existing `MusicGenre` row (400 per-item otherwise);
  an empty array `[]` is valid (no genres)

```json
{
  "name": "Lê Cát Trọng Lý",
  "avatarUrl": "/uploads/images/performer-21.jpg",
  "bio": "Ca sĩ, nhạc sĩ độc lập.",
  "type": "Solo",
  "genreIds": [3, 8]
}
```
**Response 201**: `data` = new performer's `int` id. `Location` → `GET /api/v1/performers/{id}`.
**Other status codes**: **400** validator failure (bad type, nonexistent genre id, too-long fields).
**Notes**: `createdByUserId` is taken from the caller's JWT automatically — this is what later gates
edit/delete rights on this specific performer to this specific Owner (+ Admin).

---

### PUT /api/v1/performers/{id}
**Auth**: RequireOwner (class-level) + handler-level `createdByUserId == caller OR Admin`
**Route params**: `id` (int)
**Request body** — `UpdatePerformerRequest`:
- `name` (string) — required, max 200
- `avatarUrl` (string?) — optional, max 500
- `bio` (string?) — optional, max 2000
- `type` (string) — required, `Solo`/`Band`
- `genreIds` (int[]) — replaces the FULL genre set (not a merge/add) — each id must exist

```json
{
  "name": "Lê Cát Trọng Lý",
  "avatarUrl": "/uploads/images/performer-21-v2.jpg",
  "bio": "Cập nhật tiểu sử.",
  "type": "Solo",
  "genreIds": [3, 8, 12]
}
```
**Response 204**.
**Other status codes**: **400** validator failure; **403** if caller is neither the performer's
creator nor Admin (`"Chỉ người tạo hồ sơ nghệ sĩ này hoặc Admin mới có quyền sửa."`); **404**
performer not found.
**Notes**: Genre update is remove-all-then-add-all across TWO separate `SaveChangesAsync` calls (not
one) — a deliberate workaround for a transient unique-index violation risk if remove+add of the same
`(PerformerId, GenreId)` pair happened in a single batch. Not FE-visible behavior, just explains why
this endpoint does 2 round trips to the DB internally.

---

### DELETE /api/v1/performers/{id}
**Auth**: RequireOwner (class-level) + handler-level `createdByUserId == caller OR Admin`
**Route params**: `id` (int)
**Response 204**.
**Other status codes**: **403** not creator/Admin; **404** not found; **409** (`ConflictException`)
if this performer has EVER been booked into any `Performance` (past or upcoming) —
`"Nghệ sĩ này đã từng được xếp lịch biểu diễn, không thể xoá hồ sơ."` — hard delete only works for a
never-booked profile.
**Notes**: `PerformerGenre` rows cascade-delete automatically when the performer itself is removed
(no separate cleanup needed).

---

### PUT /api/v1/performers/{id}/social-links
**Auth**: RequireOwner (class-level) + handler-level `createdByUserId == caller OR Admin`
**Route params**: `id` (int)
**Request body** — `AddPerformerSocialLinkRequest`:
- `platform` (string) — required, case-insensitive match to `Spotify`, `Youtube`, `Soundcloud`,
  `Facebook`, or `Instagram`
- `url` (string) — required, max 500, must be an absolute `http`/`https` URL
  (`Uri.TryCreate(..., UriKind.Absolute, ...)` check)
- `displayName` (string?) — optional, max 255

```json
{ "platform": "Spotify", "url": "https://open.spotify.com/artist/xyz", "displayName": null }
```
**Response 200** (not 201 — this is an upsert, so it's a 200 with the resulting link id even when a
new row is created): `data` = the social link's `int` id.
**Other status codes**: **400** bad platform/url; **403** not creator/Admin; **404** performer not
found.
**Notes**: **Upsert semantics** — setting a link for a platform the performer already has REPLACES
it (matches a unique DB index on `(PerformerId, Platform)`) rather than creating a duplicate/second
entry for that platform. There is no way to have two Spotify links on one performer.

---

### DELETE /api/v1/performers/{id}/social-links/{linkId}
**Auth**: RequireOwner (class-level) + handler-level `createdByUserId == caller OR Admin`
**Route params**: `id` (int) — performer id, `linkId` (int) — social link row id
**Response 204**.
**Other status codes**: **403** not creator/Admin; **404** link not found, or found but belongs to a
different performer than `{id}`.

---

## Part 3 — Show / Event

> Role/policy: xem bảng Policy → role ở đầu tài liệu. Không controller nào trong phần này có `[EnableRateLimiting]` riêng — chỉ áp dụng giới hạn chung 100/phút/IP.

## LoungeShowsController

Base route: `api/v1/lounge-shows`

### GET /api/v1/lounge-shows
**Auth**: AllowAnonymous (optional auth — if a Bearer token is sent, `isWishlisted` per item and `mine=true` become available)
**Route params**: none
**Query params**:
- `page` (int, default `1`)
- `pageSize` (int, default `10`) — clamped server-side to max 100, `page` < 1 becomes 1
- `sortBy` (`LoungeShowSortBy` enum, default `Newest`) — allowed values: `Newest`, `Popular`, `PriceAsc`, `PriceDesc`, `StartingSoon`
- `includeSoldOut` (bool, default `true`)
- `mine` (bool, default `false`) — if `true`, returns only the caller's own shows; **requires auth**, throws 401 if anonymous

**Request body**: none
**Response 200**: `ApiResponse<PaginatedResult<LoungeShowListItemDto>>`. `LoungeShowListItemDto` fields:
- `id` (int)
- `name` (string)
- `coverImageUrl` (string | null)
- `loungeName` (string)
- `loungeDistrict` (string)
- `loungeCity` (string)
- `scheduledStart` (string, ISO-8601 DateTimeOffset)
- `format` (string enum) — `Offline` | `Online`
- `status` (string enum) — `Draft` | `Pending` | `Published` | `Ongoing` | `Ended` | `Cancelled`
- `minPrice` (decimal | null) — null if show has no ticket tiers/prices yet
- `maxPrice` (decimal | null)
- `genres` (array of `{ id: int, name: string }`)
- `performerNames` (array of string) — ordered by `Performance.OrderIndex`
- `offlineQuota` (int | null)
- `onlineQuota` (int | null)
- `isWishlisted` (bool | null) — null when the request is anonymous

Example `data`:
```json
{
  "items": [
    {
      "id": 42,
      "name": "Jazz Night at The Blue Room",
      "coverImageUrl": "https://cdn.musiclounge.vn/shows/42/cover.jpg",
      "loungeName": "The Blue Room",
      "loungeDistrict": "Quận 1",
      "loungeCity": "Hồ Chí Minh",
      "scheduledStart": "2026-09-05T19:00:00+07:00",
      "format": "Offline",
      "status": "Published",
      "minPrice": 150000.00,
      "maxPrice": 500000.00,
      "genres": [{ "id": 3, "name": "Jazz" }],
      "performerNames": ["An Trần Trio"],
      "offlineQuota": 120,
      "onlineQuota": null,
      "isWishlisted": false
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 37,
  "totalPages": 4,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```
**Other status codes**: 401 if `mine=true` without a valid Bearer token.
**Notes**: This is the public listing feed (only `Published`-eligible shows per repository filter). Use `/search` for filters.

---

### GET /api/v1/lounge-shows/suggestions
**Auth**: AllowAnonymous
**Query params**:
- `q` (string, required in practice) — if empty/whitespace, returns `[]` immediately without querying
- `limit` (int, default `8`) — clamped server-side to 1–20

**Response 200**: `ApiResponse<IReadOnlyList<LoungeShowSuggestionItem>>`:
- `id` (int)
- `name` (string)
- `coverImageUrl` (string | null)

Example `data`: `[{ "id": 42, "name": "Jazz Night at The Blue Room", "coverImageUrl": "https://cdn..." }]`
**Notes**: Typeahead/autocomplete endpoint. `q` is capped at 200 chars by FluentValidation (400 if exceeded); empty string is allowed (returns `[]`, no 400).

---

### GET /api/v1/lounge-shows/filter-options
**Auth**: AllowAnonymous
**Response 200**: `ApiResponse<FilterOptionsDto>`:
- `genres` (array of `{ id: int, name: string }`)
- `moods` (array of `{ id: int, name: string }`)
- `atmospheres` (array of `{ id: int, name: string }`)
- `categories` (array of `{ id: int, name: string }`)
- `cities` (array of string)

**Notes**: Feeds the filter UI for `/search`. Static reference data — safe to cache client-side for a session.

---

### GET /api/v1/lounge-shows/search
**Auth**: AllowAnonymous (optional — enables `isWishlisted`)
**Query params** (all optional except paging defaults):
- `keyword` (string?, max 200 chars)
- `genreIds` (int[]?) — repeat param e.g. `?genreIds=1&genreIds=2`
- `moodIds` (int[]?)
- `atmosphereIds` (int[]?)
- `performerId` (int?)
- `loungeId` (int?)
- `city` (string?)
- `district` (string?)
- `ward` (string?)
- `dateFrom` (DateTimeOffset?)
- `dateTo` (DateTimeOffset?)
- `format` (`LoungeShowFormat`? enum) — `Offline` | `Online`
- `minPrice` (decimal?)
- `maxPrice` (decimal?)
- `includeSoldOut` (bool, default `true`)
- `includeEnded` (bool, default `false`)
- `page` (int, default `1`)
- `pageSize` (int, default `10`, clamped to 100)
- `sortBy` (`LoungeShowSortBy`, default `Newest`)

**Response 200**: same `PaginatedResult<LoungeShowListItemDto>` shape as `GET /lounge-shows`.
**Notes**: This is the full filterable catalog search; `GET /lounge-shows` (no query filters) is the simpler "browse published" feed.

---

### GET /api/v1/lounge-shows/trending
**Auth**: AllowAnonymous (optional — enables `isWishlisted`)
**Query params**:
- `limit` (int, default `10`) — clamped server-side to 1–50
- `city` (string?, default null)

**Response 200**: `ApiResponse<IReadOnlyList<LoungeShowListItemDto>>` (not paginated — a flat array, same item shape as above).

---

### GET /api/v1/lounge-shows/{id}
**Auth**: AllowAnonymous (optional — enables `isWishlisted`/`userHasTicket`/`userHasRated`; **Draft shows are hidden from non-operators**)
**Route params**: `id` (int) — show id
**Response 200**: `ApiResponse<LoungeShowDetailDto>`:
- `id` (int)
- `name` (string)
- `description` (string)
- `coverImageUrl` (string | null)
- `scheduledStart` (DateTimeOffset)
- `scheduledEnd` (DateTimeOffset | null)
- `format` (string enum) — `Offline` | `Online`
- `status` (string enum) — `Draft` | `Pending` | `Published` | `Ongoing` | `Ended` | `Cancelled`
- `isOngoing` (bool) — computed as `status == Ongoing`
- `livestreamId` (int | null)
- `lounge` (`LoungeSummaryDto`):
  - `id` (int), `name` (string), `street` (string), `ward` (string), `district` (string), `city` (string), `fullAddress` (string), `latitude` (double | null), `longitude` (double | null), `primaryImageUrl` (string | null), `model3DUrl` (string | null)
- `performers` (array of `PerformerSummaryDto`):
  - `id` (int), `name` (string), `avatarUrl` (string | null), `bio` (string | null), `genres` (array of `{ id, name }`), `performanceId` (int), `acceptsDonation` (bool)
  - ordered by `Performance.OrderIndex`
- `ticketTiers` (array of `TicketTierSummaryDto`):
  - `id` (int), `name` (string), `description` (string | null), `accessType` (string enum: `Physical` | `Livestream`), `totalCapacity` (int | null), `zoneId` (int | null)
  - `prices` (array of `TicketPriceSummaryDto`): `id` (int), `name` (string), `price` (decimal), `quota` (int | null), `saleStart` (DateTimeOffset), `saleEnd` (DateTimeOffset), `purchaseChannel` (string enum: `Online` | `Offline` | `Both`), `availableSlots` (int | null — null means unlimited/no quota; otherwise `max(0, quota - sold - held)`)
- `genres` (array of `{ id, name }`)
- `ratings` (`RatingSummaryDto`): `averageScore` (double), `totalCount` (int) — `0`/`0` if no ratings yet
- `isWishlisted` (bool | null) — null if anonymous
- `userHasTicket` (bool | null) — null if anonymous; true if caller has a `Confirmed` or `Used` ticket
- `userHasRated` (bool | null) — null if anonymous
- `legalApprovalReference` (string | null)
- `legalApprovalConfirmed` (bool) — true once Admin approved during moderation (not just Owner-declared)
- `vcpmcRoyaltyReference` (string | null)
- `playbackMode` (string enum) — `TwoD` | `ThreeD`
- `customValues` (array of `EventCustomValueDto`): `criteriaId` (int), `criteriaName` (string), `value` (string)

Example `data` (abridged):
```json
{
  "id": 42,
  "name": "Jazz Night at The Blue Room",
  "description": "An intimate evening of live jazz.",
  "coverImageUrl": "https://cdn.musiclounge.vn/shows/42/cover.jpg",
  "scheduledStart": "2026-09-05T19:00:00+07:00",
  "scheduledEnd": "2026-09-05T22:00:00+07:00",
  "format": "Offline",
  "status": "Published",
  "isOngoing": false,
  "livestreamId": null,
  "lounge": {
    "id": 7, "name": "The Blue Room",
    "street": "12 Nguyễn Huệ", "ward": "Bến Nghé", "district": "Quận 1", "city": "Hồ Chí Minh",
    "fullAddress": "12 Nguyễn Huệ, Bến Nghé, Quận 1, Hồ Chí Minh",
    "latitude": 10.7756, "longitude": 106.7019,
    "primaryImageUrl": "https://cdn.../lounge7.jpg", "model3DUrl": null
  },
  "performers": [
    { "id": 5, "name": "An Trần Trio", "avatarUrl": null, "bio": "Jazz trio from Saigon.",
      "genres": [{ "id": 3, "name": "Jazz" }], "performanceId": 91, "acceptsDonation": true }
  ],
  "ticketTiers": [
    { "id": 200, "name": "Standard", "description": null, "accessType": "Physical",
      "totalCapacity": 100, "zoneId": 1,
      "prices": [
        { "id": 500, "name": "Early Bird", "price": 150000.00, "quota": 50,
          "saleStart": "2026-08-01T00:00:00+07:00", "saleEnd": "2026-09-01T00:00:00+07:00",
          "purchaseChannel": "Online", "availableSlots": 12 }
      ] }
  ],
  "genres": [{ "id": 3, "name": "Jazz" }],
  "ratings": { "averageScore": 4.5, "totalCount": 12 },
  "isWishlisted": false,
  "userHasTicket": false,
  "userHasRated": null,
  "legalApprovalReference": "VB-2026-0042",
  "legalApprovalConfirmed": true,
  "vcpmcRoyaltyReference": "VCPMC-2026-0042",
  "playbackMode": "TwoD",
  "customValues": []
}
```
**Other status codes**: 404 if not found, **or if the show is `Draft` and the caller isn't the venue's Owner/Staff/Admin** (returned as 404, not 403, to avoid leaking existence).
**Notes**: Viewing a show logs a background behaviour event (`ViewEvent` or `ViewAfterWishlist`) for the recommendation pipeline when authenticated — no client action needed, happens automatically.

---

### GET /api/v1/lounge-shows/{id}/seating-map
**Auth**: AllowAnonymous (Draft shows hidden the same way as the detail endpoint)
**Route params**: `id` (int)
**Response 200**: `ApiResponse<SeatingMapDto>`:
- `areaLayoutImageUrl` (string | null) — from the venue's floor-plan image
- `zones` (array of `ZoneMapEntryDto`):
  - `zoneId` (int), `name` (string), `capacity` (int), `color` (string | null)
  - `layout2DX`/`layout2DY`/`layout2DWidth`/`layout2DHeight`/`layout2DRotationDeg` (double | null)
  - `layout3DX`/`layout3DY`/`layout3DZ` (double | null)
  - `availableCount` (int | null) — null means unlimited (at least one price in the zone has no quota)
  - `minPrice` (decimal | null), `maxPrice` (decimal | null)

**Notes**: Only includes zones that have at least one ticket tier attached to THIS show (not the venue's full zone list). Zones ordered by `DisplayOrder`. A zone the Owner has since deactivated still appears if a tier already references it.

---

### GET /api/v1/lounge-shows/{id}/orders
**Auth**: RequireOwner (Owner or Admin — handler additionally checks the lounge's actual `OwnerId` matches, OR caller role is Admin)
**Route params**: `id` (int) — show id
**Query params**: `page` (int, default `1`), `pageSize` (int, default `50`, clamped to 100)
**Response 200**: `ApiResponse<PaginatedResult<ShowOrderDto>>`:
- `ticketId` (string, GUID)
- `buyerName` (string | null)
- `buyerEmail` (string | null)
- `tierName` (string)
- `priceName` (string)
- `pricePaid` (decimal)
- `status` (string) — ticket status as plain string: `Pending` | `Confirmed` | `Used` | `Cancelled` | `Refunded`
- `purchaseChannel` (string) — `Ticket.PurchaseChannel` (`PurchaseChannel` enum), values: `Online` | `Offline` | `Both`
- `createdAt` (DateTimeOffset)
- `checkedInAt` (DateTimeOffset | null)

**Other status codes**: 403 if caller doesn't own the venue and isn't Admin; 404 if show not found.
**Notes**: `status`/`purchaseChannel` are plain strings (not JsonStringEnumConverter-typed fields in the DTO — the handler calls `.ToString()` explicitly), but the value text is identical to the enum member name either way.

---

### GET /api/v1/lounge-shows/by-performer/{performerId}
**Auth**: AllowAnonymous (optional — enables `isWishlisted` on the nested shows)
**Route params**: `performerId` (int)
**Query params**: `includeEnded` (bool, default `false`), `page` (int, default `1`), `pageSize` (int, default `10`, clamped to 100)
**Response 200**: `ApiResponse<PerformerDetailDto>`:
- `id` (int)
- `name` (string)
- `avatarUrl` (string | null)
- `bio` (string | null)
- `genres` (array of `{ id, name }`)
- `shows` (`PaginatedResult<LoungeShowListItemDto>`) — same paginated wrapper/item shape as above

**Other status codes**: 404 if performer not found.

---

### POST /api/v1/lounge-shows/{id}/rate
**Auth**: RequireAuthenticated
**Route params**: `id` (int) — show id
**Request body** (`RateShowRequest`):
- `score` (int) — required, must be 1–5 inclusive (`InclusiveBetween(1,5)`)
- `comment` (string | null) — optional, max 1000 chars

```json
{ "score": 5, "comment": "Amazing set, would come again!" }
```
**Response 204**: no body.
**Other status codes**:
- 400 — `showId` invalid/not found (via validator's `MustAsync` existence check, funnels into `errors.ShowId`), score out of range, comment too long
- 403 — caller has no `Confirmed`/`Used` ticket for this show (`ForbiddenException`)
- 409 — caller already rated this show (`ConflictException`)
- 400 (business, no field) — show isn't `Ended` yet, or the 7-day rating window (`RatingOpenUntil`, config-driven, default 7 days after `ActualEnd`) has expired
**Notes**: Only allowed once per user per show; window is fixed at `EndLoungeShow`/livestream-end time + N days (system_config, default 7).

---

### GET /api/v1/lounge-shows/by-lounge/{loungeId}
**Auth**: AllowAnonymous (optional — enables `isWishlisted`)
**Route params**: `loungeId` (int)
**Query params**: `page` (int, default `1`), `pageSize` (int, default `10`, clamped to 100)
**Response 200**: `ApiResponse<PaginatedResult<LoungeShowListItemDto>>` — same item shape as `GET /lounge-shows`.

---

### POST /api/v1/lounge-shows
**Auth**: RequireOwner
**Request body** (`CreateLoungeShowCommand` — bound directly, no separate request DTO):
- `loungeId` (int) — required, must be `> 0` and the caller must own it (403 otherwise)
- `name` (string) — required, max 255 chars
- `description` (string) — required, max 4000 chars
- `format` (string) — required, must case-insensitively equal `"Offline"` or `"Online"` (validator checks against a string list, NOT `IsInEnum` — a value like `"offline"` is accepted, `"Hybrid"` is rejected with 400)
- `scheduledStart` (DateTimeOffset) — required, must be in the future
- `scheduledEnd` (DateTimeOffset | null) — optional, if present must be after `scheduledStart`
- `categoryId` (int | null) — optional, if present must reference an existing `EventCategory`
- `offlineQuota` (int | null) — optional, if present must be `>= 0`
- `onlineQuota` (int | null) — optional, if present must be `>= 0`
- `genreIds` (int[]) — required (can be empty array), every id must exist as a `MusicGenre`
- `performances` (array of `PerformanceInput`) — required (can be empty array); each item:
  - `performerId` (int | null) — if provided, must be `> 0` and reference an existing `Performer`
  - `performerName` (string | null) — if `performerId` is null, this is used to create a brand-new `Performer` on the fly (max 255 chars); **exactly one of `performerId`/`performerName` must be meaningfully set** — validator rejects if both are empty
  - `role` (string) — required, must case-insensitively match one of the `PerformerRole` enum names: `Main`, `Guest`, `Host`
  - `orderIndex` (int) — required, display/performance order
  - `setTime` (string | null, `TimeOnly` — serializes as `"HH:mm:ss"`) — optional
  - `acceptsDonation` (bool) — required
- `customValues` (array of `CustomCriteriaValueInput`) — required (can be empty array); each item:
  - `criteriaId` (int) — required, `> 0`, must belong to the SAME `loungeId` as this show and be `IsActive`
  - `value` (string) — required, non-empty; format depends on the criteria's `DataType` (looked up server-side): `Select` → must be one of the criteria's JSON `options` array; `Range` → numeric string within `options.min`/`options.max`; `Boolean` → literally `"true"` or `"false"`; `Text` → non-empty, max 500 chars

Full example:
```json
{
  "loungeId": 7,
  "name": "Jazz Night at The Blue Room",
  "description": "An intimate evening of live jazz.",
  "format": "Offline",
  "scheduledStart": "2026-09-05T19:00:00+07:00",
  "scheduledEnd": "2026-09-05T22:00:00+07:00",
  "categoryId": 2,
  "offlineQuota": 120,
  "onlineQuota": null,
  "genreIds": [3, 8],
  "performances": [
    { "performerId": 5, "performerName": null, "role": "Main", "orderIndex": 0, "setTime": "19:30:00", "acceptsDonation": true },
    { "performerId": null, "performerName": "Guest DJ Linh", "role": "Guest", "orderIndex": 1, "setTime": null, "acceptsDonation": false }
  ],
  "customValues": [
    { "criteriaId": 11, "value": "VI" }
  ]
}
```
**Response 201**: `ApiResponse<int>` — the new show's id. `Location` header points to `GET /lounge-shows/{id}`.
```json
{ "success": true, "data": 42, "message": null }
```
**Other status codes**:
- 400 — any field validation failure above, funneled into `errors` (e.g. `errors.Format`, `errors.CustomValues`)
- 403 — caller doesn't own `loungeId` (`ForbiddenException`, business error, no `errors` field)
- 400 (business) — Owner has no currently-`Active` subscription with `ExpiresAt > now` (`DomainException`: "Bạn cần có gói subscription đang hoạt động để tạo event mới.")
**Notes**: New show is created with `status = Draft`. Owner must separately call `PUT .../legal-approval` and create ≥1 ticket tier (via `TicketTiersController`, not documented here) before `/publish` will succeed. A `Performance` created inline (no `performerId`) creates a brand-new global `Performer` row owned by the caller — it is NOT scoped to just this show.

---

### PUT /api/v1/lounge-shows/{id}
**Auth**: RequireOwner
**Route params**: `id` (int) — show id
**Request body** (`UpdateLoungeShowRequest`):
- `name` (string) — required, max 255
- `description` (string) — required, max 4000
- `scheduledStart` (DateTimeOffset) — required, must be in the future
- `scheduledEnd` (DateTimeOffset | null) — optional, must be after `scheduledStart` if set
- `categoryId` (int | null) — optional, must exist if set
- `offlineQuota` (int | null) — optional, `>= 0` if set
- `onlineQuota` (int | null) — optional, `>= 0` if set
- `genreIds` (int[]) — required; **full replace** of the show's genre list
- `performances` (array of `PerformanceInput`, same shape as Create) — required; **full replace**
- `customValues` (array of `CustomCriteriaValueInput`, same shape as Create) — required; **full replace**, revalidated against this show's actual `loungeId`

Note: unlike Create, there is no `loungeId` or `format` field in the update body — those cannot be changed here (format changes go through `PUT .../format`, and a show can't move to a different lounge at all).

**Response 204**: no body.
**Other status codes**:
- 400 — same validation rules as Create (minus `loungeId`/`format`)
- 403 — caller isn't the venue owner
- 404 — show not found
- 400 (business) — show is not currently `Draft` (`DomainException`: "Chỉ có thể sửa event khi còn ở trạng thái Draft.")
**Notes**: Only callable while `Status == Draft`. Genres/performances/customValues are fully replaced (old rows deleted, new rows inserted) — this is safe specifically because Draft shows can have no tickets/donations referencing their `Performance` rows yet.

---

### DELETE /api/v1/lounge-shows/{id}
**Auth**: RequireOwner
**Route params**: `id` (int)
**Response 204**: no body.
**Other status codes**: 403 not owner; 404 not found; 400 business — show is not `Draft` ("Chỉ có thể xoá event khi còn ở trạng thái Draft. Event đã Published cần được huỷ (Cancel), không xoá.").
**Notes**: Hard delete — only permitted in `Draft` because no other entity can reference the show yet. Published+ shows must use `/cancel` instead.

---

### POST /api/v1/lounge-shows/{id}/publish
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body**: none
**Response 204**: no body.
**Other status codes** (all `DomainException` → 400 business error unless noted, checked in this exact order):
1. 403 — caller doesn't own the venue
2. 400 — venue `Status == Pending` ("Phòng trà đang chờ Admin duyệt...")
3. 400 — venue `Status == Rejected` ("Phòng trà đã bị Admin từ chối...")
4. 400 — venue `Status == Suspended` or `Locked` (message includes the actual status)
5. 400 — show `Status != Draft` ("Chỉ có thể nộp duyệt event đang ở trạng thái Draft.")
6. 400 — show has zero `TicketTier` rows ("Event phải có ít nhất 1 hạng vé trước khi nộp duyệt.")
7. 400 — `legalApprovalReference` is null/whitespace (NĐ 144/2020 Điều 10 reference)
8. 400 — fewer than `PublishMinBusinessDaysLeadTime` (system_config, default **7**) business days remain until `scheduledStart` (message reports how many business days are actually left)
9. 400 — show is `Online` format OR has any tier with `accessType == Livestream`, AND no `Livestream` record exists yet for the show ("Event online hoặc có vé livestream phải được thiết lập Livestream trước khi nộp duyệt.")
- 404 — show not found
**Notes**: On success, show moves `Draft → Pending` and an `EventModeration` row is created (or reopened if this is a resubmission after a prior rejection — `AdminDecision`/`ReviewNote`/AI-scoring fields are all reset). SLA deadline = now + `ModerationSlaHours` (config, default **24**). A background job (`EnqueueModerationAiScoring`) is queued to AI-score the submission; this is async and does not block the response. Concurrent double-publish calls are serialized via a per-show lock.

---

### POST /api/v1/lounge-shows/{id}/cancel
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body**: none
**Response 204**: no body.
**Other status codes**:
- 403 — caller is neither the venue owner nor Admin
- 404 — show not found
- 400 — show already `Cancelled` or `Ended`
- 400 — the show has a livestream currently `Live` (must terminate the livestream first)
**Notes**: Sets `Status = Cancelled`. Every `Confirmed` ticket (Physical AND Livestream — unlike format-change, which is Physical-only) is set to `Cancelled` and a `RefundRequest` is auto-created at 100% (`RefundPercentage = 100`), plus an `EventCancelled` notification per buyer. Refund still requires separate Admin/finance processing (`RefundRequestStatus.Pending`) — this endpoint does not itself move money.

---

### POST /api/v1/lounge-shows/{id}/reschedule
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** (`RescheduleLoungeShowRequest`):
- `newScheduledStart` (DateTimeOffset) — required, must be in the future

```json
{ "newScheduledStart": "2026-10-01T19:00:00+07:00" }
```
**Response 204**: no body.
**Other status codes**:
- 403 — not the venue owner
- 404 — show not found
- 400 — show `Status != Published` (Draft/Ongoing/Ended/Cancelled all rejected — Ongoing is deliberately excluded, not just disallowed by accident)
- 400 — fewer than `PublishMinBusinessDaysLeadTime` (default 7) business days between now and the NEW date (same NĐ 144/2020 rule re-applied to prevent a "publish on time then reschedule to tomorrow" loophole)
**Notes**: `scheduledEnd` (if set) shifts by the same delta as `scheduledStart`. Re-enables `CancellationAllowed = true` so ticket holders can cancel-for-refund against the new date. Every buyer with a `Confirmed` ticket gets an `EventRescheduled` notification (a distinct `NotificationType` from `EventReminder`, chosen specifically to avoid colliding with the reminder job's own dedup key).

---

### POST /api/v1/lounge-shows/{id}/ai-poster
**Auth**: RequireOwner (entitlement additionally gated inside the handler by the Owner's active subscription snapshot — the route policy alone does not guarantee access)
**Route params**: `id` (int)
**Request body** (`GeneratePosterRequest`, nullable body allowed):
- `styleHint` (string | null) — optional, max 500 chars, freeform extra instruction appended to the AI prompt

```json
{ "styleHint": "vintage vinyl aesthetic, warm amber tones" }
```
(or send `{}` / omit body entirely — `body` is nullable in the action signature)

**Response 200**: `ApiResponse<PosterGenerationResultDto>`:
- `imageUrl` (string)
- `remainingThisMonth` (int) — Owner's remaining AI-poster quota for the current calendar month after this call

```json
{ "success": true, "data": { "imageUrl": "https://cdn.musiclounge.vn/posters/42/ai-3.png", "remainingThisMonth": 2 }, "message": null }
```
**Other status codes**:
- 400 — `styleHint` too long
- 403 — caller doesn't own the show's venue
- 404 — show not found
- 400 (business) — Owner has no active subscription, or active subscription's `HasAiPosterSnapshot == false` ("Gói subscription hiện tại của bạn không bao gồm tính năng tạo poster AI.")
- 400 (business) — this SHOW has hit `ai_poster_max_attempts_per_show` (system_config, default **5**) total attempts (success+failure combined) — anti-abuse cap, independent of the monthly billing quota
- 400 (business) — Owner has used all `MaxAiPostersPerMonthSnapshot` (subscription-tier-specific) **succeeded** generations this calendar month
- 422 — the AI image-generation call itself fails (`ExternalServiceException`, e.g. Gemini error/quota) — a `Failed` `AiPosterGeneration` attempt row IS still recorded (counts against the per-show anti-abuse cap, NOT against the monthly billing quota)
- 503 — AI image service unreachable/unavailable
**Notes**: On success, `show.posterUrl` and `show.posterByAi = true` are updated automatically — no separate call to `PUT .../poster` needed. This command runs outside the normal DB transaction wrapper (`INoTransactionCommand`) specifically so a Failed attempt log survives even when the handler re-throws.

---

### GET /api/v1/lounge-shows/{id}/ai-poster/history
**Auth**: RequireOwner
**Route params**: `id` (int)
**Response 200**: `ApiResponse<IReadOnlyList<PosterGenerationAttemptDto>>`, newest first:
- `id` (int)
- `status` (string) — `Succeeded` | `Failed` (from `AiPosterGenerationStatus` enum, serialized via `.ToString()`)
- `imageUrl` (string | null) — null when `status == Failed`
- `errorMessage` (string | null) — populated when `status == Failed`
- `createdAt` (DateTimeOffset)

**Other status codes**: 403 not the venue owner; 404 show not found.

---

### POST /api/v1/lounge-shows/{id}/start
**Auth**: RequireVenueOperator (Staff, Owner, or Admin — AND must actually be assigned to/own THIS show's venue)
**Route params**: `id` (int)
**Request body**: none
**Response 204**: no body.
**Other status codes**:
- 403 — caller isn't an operator of this specific venue
- 404 — show not found
- 400 — show `Status != Published`
- 400 — show has an associated `Livestream` record at all (use the livestream-specific start endpoint instead — this action is offline-only)
- 400 — `vcpmcRoyaltyReference` is null/whitespace (must be declared before the show can go Ongoing)
**Notes**: Sets `Status = Ongoing`, `ActualStart = now`. Offline-only counterpart; a show with any livestream tier must go through the Livestream module's own start command (not covered in this doc).

---

### POST /api/v1/lounge-shows/{id}/end
**Auth**: RequireVenueOperator
**Route params**: `id` (int)
**Request body**: none
**Response 204**: no body.
**Other status codes**:
- 403 — not an operator of this venue
- 404 — show not found
- 400 — show `Status != Ongoing`
- 400 — show has an associated `Livestream` record (use the livestream end endpoint instead)
**Notes**: Sets `Status = Ended`, `ActualEnd = now`, and `RatingOpenUntil = now + RatingWindowDays` (system_config, default **7** days) — this is what gates `POST /{id}/rate`.

---

### PUT /api/v1/lounge-shows/{id}/legal-approval
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** (`SetLegalApprovalReferenceRequest`):
- `legalApprovalReference` (string) — required, non-empty, max 500 chars

```json
{ "legalApprovalReference": "VB-2026-0042" }
```
**Response 204**: no body.
**Other status codes**: 400 empty/too long; 403 not owner; 404 not found; 400 business — show `Status != Draft` ("Chỉ có thể khai báo văn bản chấp thuận khi event còn ở trạng thái Draft.").
**Notes**: This is the "văn bản chấp thuận tổ chức biểu diễn" reference required by NĐ 144/2020/NĐ-CP Điều 10, which `/publish` checks is non-empty. Only editable while Draft — becomes locked once submitted for review, and `legalApprovalConfirmed` on the detail DTO only flips to `true` after an Admin actually approves the moderation (separate from just declaring the reference here).

---

### PUT /api/v1/lounge-shows/{id}/vcpmc-royalty
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** (`SetVcpmcRoyaltyReferenceRequest`):
- `vcpmcRoyaltyReference` (string) — required, non-empty, max 500 chars

```json
{ "vcpmcRoyaltyReference": "VCPMC-2026-0042" }
```
**Response 204**: no body.
**Other status codes**: 400 empty/too long; 403 not owner; 404 not found; 400 business — show `Status` is `Ongoing`, `Ended`, or `Cancelled` (can be set any time before the show actually starts, unlike legal-approval which is Draft-only).
**Notes**: Music-copyright royalty (VCPMC) reference; `/start` (the non-livestream start action) requires this to be set before a show can go `Ongoing`.

---

### PUT /api/v1/lounge-shows/{id}/cover-image
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** (`SetShowCoverImageRequest`):
- `imageUrl` (string) — required, non-empty, max 500 chars

```json
{ "imageUrl": "https://cdn.musiclounge.vn/shows/42/cover.jpg" }
```
**Response 204**: no body.
**Other status codes**: 403 caller neither venue owner nor Admin; 404 not found. (No status-based business restriction — can be changed any time, including on Published/Ongoing shows.)
**Notes**: The client is expected to have already uploaded the file through the generic uploads endpoint (`UploadsController`, not covered here) and pass the resulting URL in.

---

### PUT /api/v1/lounge-shows/{id}/poster
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** (`SetShowPosterRequest`):
- `imageUrl` (string) — required, non-empty, max 500 chars

```json
{ "imageUrl": "https://cdn.musiclounge.vn/posters/42/manual.jpg" }
```
**Response 204**: no body.
**Other status codes**: 403 not owner/Admin; 404 not found.
**Notes**: Manual counterpart to `POST .../ai-poster` — for Owners without the AI-poster subscription tier, or who just want to upload their own. Sets `posterByAi = false` (distinguishing it from AI-generated posters in the history/detail views).

---

### PUT /api/v1/lounge-shows/{id}/playback-mode
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** (`SetPlaybackModeRequest`):
- `playbackMode` (string) — required, must be exactly `"TwoD"` or `"ThreeD"` (case-sensitive string match, not `IsInEnum`)

```json
{ "playbackMode": "ThreeD" }
```
**Response 204**: no body.
**Other status codes**:
- 400 — value isn't exactly `"TwoD"`/`"ThreeD"`
- 403 — caller neither owns the venue nor is Admin
- 404 — not found
- 400 (business) — show `Status` is `Ended` or `Cancelled`
- 400 (business) — requesting `"ThreeD"` while `show.Format == Offline` ("Chỉ show Online mới có thể phát dạng 3D.")
**Notes**: Governs the 3D virtual-tour-style stage playback vs. flat 2D HLS for Online shows. Not related to the venue's separate `Model3DUrl` 360°-tour feature.

---

### PUT /api/v1/lounge-shows/{id}/format
**Auth**: RequireOwner
**Route params**: `id` (int)
**Request body** (`ChangeLoungeShowFormatRequest`):
- `newFormat` (string enum) — required, must be a valid `LoungeShowFormat` member: `Offline` | `Online` (`IsInEnum` validator — invalid string funnels to 400 via model binding before the validator even runs, since this one IS bound as the real enum type)

```json
{ "newFormat": "Online" }
```
**Response 204**: no body.
**Other status codes**:
- 403 — not the venue owner
- 404 — not found
- 400 — show `Status` is not `Published` or `Ongoing`
- 400 — anything other than an `Offline → Online` transition (`Online → Offline` and no-op are both rejected: "Chỉ hỗ trợ đổi hình thức từ Offline sang Online sau khi đã bán vé.")
**Notes**: One-directional only. Every `Confirmed` ticket whose tier `accessType == Physical` is force-`Cancelled` with an auto-created 100%-refund `RefundRequest` and an `EventFormatChanged` notification — Livestream-access tickets are untouched. Concurrent calls (or a race with `/cancel`) are serialized via the same `show-status-change:{showId}` lock key.

---

## EventModerationsController

Base route: `api/v1/moderations`. **Controller-level `[Authorize(Policy = RequireAdmin)]`** — every action below requires Admin role; not restated per endpoint.

### GET /api/v1/moderations/pending
**Auth**: RequireAdmin
**Query params**:
- `targetType` (string?, default null) — if provided, must parse (case-insensitive) as a `ModerationTargetType` member: `Show`, `Livestream`, `GalleryImage`, `TourScene`; an unparseable value is silently ignored (treated as "no filter"), NOT a 400
- `page` (int, default `1`)
- `pageSize` (int, default `20`, clamped to 100)

**Response 200**: `ApiResponse<PaginatedResult<EventModerationDto>>`:
- `id` (int)
- `targetType` (string) — `Show` | `Livestream` | `GalleryImage` | `TourScene`
- `targetId` (int)
- `aiScore` (float | null)
- `riskLevel` (string | null) — `Low` | `Medium` | `High` | `Critical` (from `ModerationRiskLevel`), null until AI scoring has run
- `flagReason` (string | null)
- `aiRecommendation` (string | null) — `SuggestApprove` | `NeedsReview` | `SuggestReject` (from `AiModerationRecommendation`)
- `adminId` (int | null) — null until reviewed
- `adminDecision` (string | null) — `Approved` | `Rejected` | `Terminated` (from `ModerationDecision`), null until reviewed
- `reviewNote` (string | null)
- `createdAt` (DateTimeOffset)
- `slaDeadline` (DateTimeOffset | null) — `createdAt + ModerationSlaHours` (config, default 24h)
- `reviewedAt` (DateTimeOffset | null)

Example `data`:
```json
{
  "items": [
    {
      "id": 15, "targetType": "Show", "targetId": 42,
      "aiScore": 0.12, "riskLevel": "Low", "flagReason": null, "aiRecommendation": "SuggestApprove",
      "adminId": null, "adminDecision": null, "reviewNote": null,
      "createdAt": "2026-08-15T10:00:00+07:00",
      "slaDeadline": "2026-08-16T10:00:00+07:00",
      "reviewedAt": null
    }
  ],
  "page": 1, "pageSize": 20, "totalCount": 3, "totalPages": 1,
  "hasNextPage": false, "hasPreviousPage": false
}
```
**Notes**: "Pending" here means `AdminDecision IS NULL` (queued for review), not the `LoungeShowStatus.Pending` show status specifically — this endpoint surfaces both Show and Livestream moderation queue items depending on `targetType`.

---

### POST /api/v1/moderations/livestreams/{livestreamId}/review
**Auth**: RequireAdmin
**Route params**: `livestreamId` (int)
**Request body** (`ReviewLivestreamRequest` — also reused verbatim for the show-review endpoint below):
- `decision` (string) — required, must case-insensitively be `"Approved"` or `"Rejected"` (NOT `"Terminated"` — that's a valid `ModerationDecision` enum member but explicitly rejected by both the validator's allow-list AND a second handler-side check)
- `reviewNote` (string | null) — max 1000 chars; **required (non-empty) when `decision == "Rejected"`**, optional when `"Approved"`

```json
{ "decision": "Rejected", "reviewNote": "Nội dung quảng bá không phù hợp, cần chỉnh sửa hình ảnh." }
```
**Response 204**: no body.
**Other status codes**:
- 400 — `decision` not in `{Approved, Rejected}`, or `reviewNote` missing while rejecting, or `reviewNote` > 1000 chars
- 404 — livestream not found, or no `EventModeration` row exists yet for this livestream ("EventModeration for Livestream" not found)
- 409 — moderation already has a non-null `AdminDecision` ("Livestream này đã được duyệt trước đó."); OR livestream `Status` is already `Live`, `Ended`, or `Terminated` ("Không thể thay đổi quyết định khi livestream đã/đang phát sóng.")
**Notes**: Concurrent double-review on the same livestream is serialized via a per-livestream lock. Sends a `ModerationResult` notification to the venue Owner either way.

---

### POST /api/v1/moderations/shows/{showId}/review
**Auth**: RequireAdmin
**Route params**: `showId` (int)
**Request body**: identical shape to the livestream-review endpoint above (`decision`, `reviewNote`; same validation rules, same "Terminated" rejection).
```json
{ "decision": "Approved", "reviewNote": null }
```
**Response 204**: no body.
**Other status codes**:
- 400 — same field-validation rules as livestream review
- 404 — show not found, or no `EventModeration` row exists for it yet
- 409 — moderation already decided; OR show `Status != Pending` at review time
**Notes**: On `Approved`: show → `Published`, `LegalApprovalConfirmedByAdminId`/`LegalApprovalConfirmedAt` are stamped (this is the actual mechanism behind `LoungeShowDetailDto.legalApprovalConfirmed`), Owner gets a `ModerationResult` notification, AND every user following the venue gets a `NewEvent` notification. On `Rejected`: show → back to `Draft` (Owner can edit and resubmit via `/publish` again, which reopens the same `EventModeration` row rather than creating a new one). Concurrent double-review is serialized via a per-show lock (same lock key namespace `moderation:show:{id}` used by `/publish`'s moderation-row creation).

---

### POST /api/v1/moderations/images/{moderationId}/review
_(Added 2026-08-18)_
**Auth**: RequireAdmin
**Route params**: `moderationId` (int) — **the `EventModeration` row's own id (`items[].id` from
`GET /moderations/pending`), NOT `targetId`.** See the id-convention warning below.
**Request body**: identical shape to the show/livestream review endpoints (`decision`, `reviewNote`).
```json
{ "decision": "Rejected", "reviewNote": "Ảnh chứa nội dung không phù hợp." }
```
**Response 204**: no body.
**Other status codes**:
- 400 — `decision` not `Approved`/`Rejected`; `reviewNote` missing when rejecting; `reviewNote` > 1000 chars
- 404 — no `EventModeration` row with that id
- 409 — already decided
- 422 — the row's `targetType` is `Show` or `Livestream` (use the dedicated endpoints for those)

> **⚠️ The three review endpoints are NOT keyed the same way.**
>
> | Endpoint | Id to send |
> |---|---|
> | `/moderations/shows/{showId}/review` | `targetId` |
> | `/moderations/livestreams/{livestreamId}/review` | `targetId` |
> | `/moderations/images/{moderationId}/review` | **`id`** (the moderation row) |
>
> Images are keyed on the moderation row because `GalleryImage` and `TourScene` live in **different
> tables** — a bare `targetId` would be ambiguous between them. Sending the wrong one is a silent 404.

**Why this endpoint exists**: `IImageModerationGate` blocks images at/above the block threshold outright,
and for anything between the review and block thresholds it stores the image **and creates an
`EventModeration` row for a human to review** (`AddLoungeGalleryImage`, `AddVenueTourScene`,
`StitchVenueTourSceneJob` all do this, stamping `SlaDeadline` from `system_config`'s
`moderation_sla_hours`). Before this endpoint, those `GalleryImage`/`TourScene` rows appeared in
`GET /moderations/pending` but **no endpoint could ever resolve them** — they accumulated permanently and
blew the NĐ 147/2024 24-hour review SLA that `SlaDeadline` exists to track.

**Notes**: `Approved` only closes the moderation row — these images are already publicly visible (unlike a
`Pending` show), so approval changes nothing else. **`Rejected` deletes the image**, matching what the gate
already does automatically above the block threshold; for a `TourScene` this also clears inbound
`VenueTourHotspot.TargetSceneId` references (FK is `Restrict`) and nulls `VenueTourStitchAttempt.ResultSceneId`
(FK is `NoAction`). The audit trail survives deletion: the `EventModeration` row keeps `AiScore`,
`FlagReason`, the Admin's `ReviewNote` and `ReviewedAt`. The venue Owner is notified either way. A moderation
row whose target was already deleted by the Owner is not an error — the decision still records.

---

*End of Part 3 — 27 `LoungeShowsController` endpoints + 4 `EventModerationsController` endpoints, 31 total.*

---

## Part 4 — Ticket / Money

> Không controller nào trong 5 controller phần này có `[EnableRateLimiting]` riêng — chỉ áp dụng giới hạn chung. `pageSize` clamp tối đa khác nhau theo endpoint: hầu hết list refund/donation clamp 50, riêng `GetMyTickets`/donation feed công khai/lịch sử performer công khai clamp 100 — xem ghi chú từng endpoint.

## TicketsController
Base route: `api/v1/tickets`. Class-level `[Authorize(Policy = RequireAuthenticated)]` — every action
requires a valid bearer token unless noted; two actions additionally require `RequireVenueOperator`
(class + method `[Authorize]` combine, so those two effectively need Staff/Owner/Admin, not just any
authenticated user).

### POST /api/v1/tickets/holds
**Auth**: RequireAuthenticated
**Route params**: none
**Query params**: none
**Request body**: `HoldTicketCommand`
- `priceId` (int) — required, must be > 0 and reference an existing `TicketPrice.Id`.
- `quantity` (int) — required, must be ≥ 1 and ≤ `system_config TicketHoldMaxQuantity` (default 10 if unset).

```json
{ "priceId": 42, "quantity": 2 }
```

**Response 201**: `HoldTicketResultDto`
- `holdId` (int) — pass this to `POST /tickets/purchase` or `DELETE /tickets/holds/{holdId}`.
- `expiresAt` (string, DateTimeOffset) — hold auto-expires at this instant (window from `system_config
  TicketHoldMinutes`, default 15 min).

```json
{ "success": true, "data": { "holdId": 501, "expiresAt": "2026-08-17T15:30:00+07:00" }, "message": null }
```

**Other status codes**:
- 400 — `priceId` ≤ 0 / not found, `quantity` < 1 or > max (FluentValidation).
- 404 — `TicketPrice`, `TicketTier`, or `LoungeShow` for the price not found.
- 422 — show is not `Published`/`Ongoing`; sale window (`SaleStart`/`SaleEnd`) not open; any of 5 layered
  quota checks fails (per-price quota, tier `TotalCapacity`, zone physical capacity, show-level
  online/offline quota, Owner's active-subscription `MaxTicketsPerEventSnapshot`).

**Notes**: This does NOT create a Payment yet — it's a soft reservation. Quota checks are serialized
per-show via a distributed lock, so no overselling under concurrency. If the buyer is authenticated (always
true here since the whole controller requires auth) this also enqueues a `ClickTicket` behaviour-log event
for the recommendation pipeline. If stock crosses a low-stock threshold (`system_config
TicketLowStockThresholdRatio`, default 10%), every wishlister for the show gets a `WishlistLowStock`
notification as a side effect.

---

### POST /api/v1/tickets/purchase
**Auth**: RequireAuthenticated
**Route params**: none
**Query params**: none
**Request body**: `PurchaseTicketRequest` (controller-local record; server derives `ClientIpAddress` from the
request, do NOT send it)
- `holdId` (int) — required, the `holdId` returned by `POST /tickets/holds`.

```json
{ "holdId": 501 }
```

**Response 201**: `PaymentInitiationDto`
- `paymentId` (int)
- `orderId` (string) — VNPay order ref, format `ML-yyyyMMddHHmmss-<guid32>` truncated to 40 chars.
- `amount` (decimal) — `price.Price * hold.Quantity`.
- `paymentUrl` (string) — redirect the buyer's browser here to complete VNPay checkout.
- `ticketIds` (Guid[]) — the Pending `Ticket` rows created for this purchase; they flip to `Confirmed` only
  after the VNPay callback/IPN succeeds.

```json
{
  "success": true,
  "data": {
    "paymentId": 9001,
    "orderId": "ML-20260817143000-a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4",
    "amount": 300000.00,
    "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?...",
    "ticketIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6", "3fa85f64-5717-4562-b3fc-2c963f66afa7"]
  },
  "message": null
}
```

**Other status codes**:
- 403 — hold doesn't belong to the caller.
- 404 — hold / price / tier / show not found.
- 409 — hold already used by a prior purchase (`hold.IsReleased == true`).
- 422 — hold expired (`ExpiresAt <= now`); show no longer `Published`/`Ongoing`.

**Notes**: A hold can back at most one `Payment` — enforced both by an in-request `IAsyncKeyedLock` (keyed
`purchase-hold:{holdId}`) AND a DB-level filtered unique index on `Payment.IdempotencyKey =
"hold:{holdId}"`. Tickets are created `Pending` here; `PurchaseChannel = Online`. The hold row itself is
marked `IsReleased = true` immediately (not deleted) so quota math doesn't double-count it. Ticket QR codes
are NOT assigned yet — only after the VNPay callback confirms payment.

---

### DELETE /api/v1/tickets/holds/{holdId}
**Auth**: RequireAuthenticated
**Route params**: `holdId` (int) — the hold to release.
**Query params**: none
**Request body**: none
**Response 204**: no body.
**Other status codes**:
- 401 — not authenticated.
- 403 — hold doesn't belong to the caller.
- 404 — hold not found.
- 409 — hold already spent by a completed purchase (`IsReleased == true`) — cannot cancel a hold that
  already backs a real Payment/Pending-tickets.

**Notes**: Hard-deletes the `TicketHold` row (not a status flip). Same distributed lock key as
`purchase-hold:{holdId}` — racing this against a concurrent `POST /tickets/purchase` on the same hold is
safe; one of the two cleanly fails.

---

### GET /api/v1/tickets/my
**Auth**: RequireAuthenticated
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 10, clamped 1–100)
**Request body**: none
**Response 200**: `PaginatedResult<TicketListItemDto>` — `items[]` fields:
- `id` (Guid)
- `showId` (int)
- `showName` (string)
- `loungeName` (string)
- `loungeCity` (string)
- `showScheduledStart` (DateTimeOffset)
- `tierName` (string)
- `priceName` (string)
- `pricePaid` (decimal)
- `accessType` (string enum) — `Physical` | `Livestream`
- `status` (string enum) — `Pending` | `Confirmed` | `Used` | `Cancelled` | `Refunded`
- `qrCode` (string, nullable) — null until payment confirmed
- `purchasedAt` (DateTimeOffset)
- `hasPendingTransfer` (bool) — true if someone else has been invited to accept this ticket via transfer

```json
{
  "success": true,
  "data": {
    "items": [{
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "showId": 12,
      "showName": "Jazz Night",
      "loungeName": "Blue Note Saigon",
      "loungeCity": "Ho Chi Minh City",
      "showScheduledStart": "2026-09-05T19:00:00+07:00",
      "tierName": "VIP",
      "priceName": "Early Bird",
      "pricePaid": 300000.00,
      "accessType": "Physical",
      "status": "Confirmed",
      "qrCode": "a1b2c3d4e5f6478a9b0c1d2e3f4a5b6c",
      "purchasedAt": "2026-08-17T14:30:00+07:00",
      "hasPendingTransfer": false
    }],
    "page": 1, "pageSize": 10, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: 401 only.

---

### GET /api/v1/tickets/refund-requests/my
**Auth**: RequireAuthenticated
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 10, clamped 1–**50** — different ceiling
than `GetMyTickets`)
**Request body**: none
**Response 200**: `PaginatedResult<RefundRequestDto>` — `items[]` fields:
- `id` (int)
- `paymentId` (int)
- `requestedBy` (int, nullable)
- `reason` (string)
- `amountRequested` (decimal)
- `amountApproved` (decimal, nullable) — null until Admin resolves
- `refundPercentage` (decimal, nullable) — snapshot of the show's `RefundPercentage` at cancel time
- `status` (string enum) — `Pending` | `Approved` | `Rejected`
- `createdAt` (DateTimeOffset)
- `resolvedAt` (DateTimeOffset, nullable)
- `requiresManualTransfer` (bool) — true if VNPay's automatic refund couldn't be used and Admin must wire
  the money manually
- `gatewayRefundResponseCode` (string, nullable)

```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 88, "paymentId": 9001, "requestedBy": 205, "reason": "Audience yêu cầu hủy vé",
      "amountRequested": 300000.00, "amountApproved": null, "refundPercentage": 100.0,
      "status": "Pending", "createdAt": "2026-08-17T16:00:00+07:00", "resolvedAt": null,
      "requiresManualTransfer": false, "gatewayRefundResponseCode": null
    }],
    "page": 1, "pageSize": 10, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: 401 only.
**Notes**: This is created as a side effect of `POST /tickets/{id}/cancel` on a Confirmed ticket — there is
no direct "create refund request" endpoint for Audience.

---

### GET /api/v1/tickets/incoming-transfers
**Auth**: RequireAuthenticated
**Route params**: none
**Query params**: none (not paginated)
**Request body**: none
**Response 200**: `IReadOnlyList<IncomingTicketTransferDto>`
- `ticketId` (Guid)
- `showName` (string)
- `loungeName` (string)
- `showScheduledStart` (DateTimeOffset)
- `tierName` (string)
- `priceName` (string)
- `pricePaid` (decimal)
- `initiatedAt` (DateTimeOffset)

```json
{
  "success": true,
  "data": [{
    "ticketId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "showName": "Jazz Night", "loungeName": "Blue Note Saigon",
    "showScheduledStart": "2026-09-05T19:00:00+07:00",
    "tierName": "VIP", "priceName": "Early Bird", "pricePaid": 300000.00,
    "initiatedAt": "2026-08-17T10:00:00+07:00"
  }],
  "message": null
}
```
**Other status codes**: 401 only.
**Notes**: Lists tickets where the caller is the invited recipient (`PendingTransferToUserId == me`),
waiting on `POST /tickets/{id}/transfer/accept` or `.../cancel`.

---

### POST /api/v1/tickets/walk-in
**Auth**: RequireAuthenticated **+** RequireVenueOperator (Staff, Owner, or Admin of the venue)
**Route params**: none
**Query params**: none
**Request body**: `SellWalkInTicketCommand`
- `priceId` (int) — required, > 0.
- `quantity` (int) — required, > 0 and ≤ `system_config WalkInTicketMaxQuantity` (default 20).

```json
{ "priceId": 45, "quantity": 3 }
```

**Response 201**: `WalkInSaleResultDto`
- `paymentId` (int)
- `amount` (decimal)
- `ticketIds` (Guid[]) — already `Confirmed` (no pending state — walk-in is cash, settled instantly).

```json
{ "success": true, "data": { "paymentId": 9010, "amount": 450000.00, "ticketIds": ["..."] }, "message": null }
```

**Other status codes**:
- 400 — validation (`priceId` ≤ 0, `quantity` out of range).
- 403 — caller is not an operator (Staff/Owner/Admin) for this venue.
- 404 — price/tier/show not found.
- 422 — tier is not `Physical` AccessType; the price's `purchaseChannel` is `Online`-only (walk-in requires
  `Offline` or `Both`); show not `Published`/`Ongoing`; sale window closed; any quota check fails (same 4
  layers as `HoldTicket` minus the online-quota check, plus subscription `MaxTicketsPerEventSnapshot`).

**Notes**: Creates a `Payment` with `Method = Cash`, `Status = Confirmed` immediately — no VNPay round-trip.
Tickets get a real `QrCode` right away and a `PhysicalTicketDetail` row is attached
(`SoldByStaffId = caller`). `BuyerId` is `null` (no Audience account attached to a walk-in sale) — this is
a box-office sale, not a subscription-funded commission event (per the walk-in revenue model note: no
platform commission by default on this channel).

---

### GET /api/v1/tickets/by-qr/{qrCode}
**Auth**: RequireAuthenticated
**Route params**: `qrCode` (string) — the ticket's QR payload.
**Query params**: none
**Request body**: none
**Response 200**: `TicketDetailDto` (see shape under `GET /tickets/{id}` below — identical DTO).
**Other status codes**:
- 401 — not authenticated.
- 403 — caller is neither the ticket's buyer nor a venue operator (Staff/Owner/Admin) for that show's
  venue.
- 404 — no ticket with that QR code.

**Notes**: Broader authorization than `GetById`/`GetQrImage` — intended for Staff to preview a scanned QR
(who does it belong to? already used?) before committing to `POST /tickets/check-in`. Also usable by the
ticket's own buyer to self-lookup by QR.

---

### POST /api/v1/tickets/check-in
**Auth**: RequireAuthenticated **+** RequireVenueOperator
**Route params**: none
**Query params**: none
**Request body**: `CheckInTicketRequest` (controller-local record)
- `qrCode` (string) — required, non-empty.

```json
{ "qrCode": "a1b2c3d4e5f6478a9b0c1d2e3f4a5b6c" }
```

**Response 200**: `TicketDetailDto` (same shape as `GET /tickets/{id}`, now with `status: "Used"` and
`physicalDetail.checkedInAt` populated).
**Other status codes**:
- 400 — `qrCode` empty (FluentValidation).
- 403 — caller is not an operator for this show's venue (scoped by the operator's assigned `LoungeId`).
- 404 — no ticket with that QR code.
- 409 — ticket already checked in (`PhysicalDetail.CheckedInAt` already set).
- 422 — show is not `Ongoing`; tier's `AccessType` is `Livestream` (online tickets never need check-in);
  ticket status is not `Confirmed`; ticket has a pending transfer in progress.

**Notes**: Sets `Status = Used` and stamps `PhysicalTicketDetail.CheckedInAt`/`CheckedInByStaffId`. Guarded
by a distributed lock keyed `checkin:{qrCode}` so the same QR scanned at two doors simultaneously can't
double-check-in.

---

### POST /api/v1/tickets/{id}/cancel
**Auth**: RequireAuthenticated
**Route params**: `id` (Guid) — ticket ID.
**Query params**: none
**Request body**: none
**Response 200**: `int` — a `RefundRequestId` (>0) if the ticket was `Confirmed` and a refund request was
created; `0` if the ticket was still `Pending` (no payment was ever completed, so it's just cancelled
outright with no refund request).

```json
{ "success": true, "data": 88, "message": null }
```

**Other status codes**:
- 403 — ticket doesn't belong to the caller.
- 404 — ticket not found.
- 422 — ticket status is neither `Pending` nor `Confirmed`; ticket has a pending transfer; show doesn't
  allow cancellation (`CancellationAllowed == false`); past the show's `CancellationDeadlineHours` cutoff;
  ticket has no valid `PaymentId` to refund against.

**Notes**: Two very different paths depending on ticket status:
- `Pending` (never paid) → immediately flips to `Cancelled`, the linked Pending Payment (if any) flips to
  `Failed`. No `RefundRequest` created, returns `0`. No deadline/`CancellationAllowed` check applies to this
  path.
- `Confirmed` (real money moved) → flips to `Cancelled`, creates a `RefundRequest` with
  `AmountRequested = Round(price * show.RefundPercentage / 100, 2)` (defaults to 100% if
  `show.RefundPercentage` is unset), `Status = Pending`. An Admin resolves it separately (not covered by
  this controller — see the RefundRequests processing endpoint elsewhere). Idempotency against double-click
  is enforced via a distributed lock keyed `cancel-ticket:{ticketId}`.

---

### GET /api/v1/tickets/{id}
**Auth**: RequireAuthenticated
**Route params**: `id` (Guid) — ticket ID.
**Query params**: none
**Request body**: none
**Response 200**: `TicketDetailDto`
- `id` (Guid)
- `showName` (string)
- `loungeName` (string)
- `loungeAddress` (string) — full formatted address
- `showScheduledStart` (DateTimeOffset)
- `showScheduledEnd` (DateTimeOffset, nullable)
- `tierName` (string)
- `priceName` (string)
- `pricePaid` (decimal)
- `accessType` (string enum) — `Physical` | `Livestream`
- `status` (string enum) — `Pending` | `Confirmed` | `Used` | `Cancelled` | `Refunded`
- `qrCode` (string, nullable)
- `purchasedAt` (DateTimeOffset)
- `physicalDetail` (object, nullable) — only present for Physical-tier tickets:
  - `seatInfo` (string, nullable)
  - `checkedInAt` (DateTimeOffset, nullable)
- `livestreamDetail` (object, nullable) — only present for Livestream-tier tickets:
  - `accessToken` (string, nullable) — used by the FE player to authorize the stream

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "showName": "Jazz Night", "loungeName": "Blue Note Saigon",
    "loungeAddress": "123 Nguyen Hue, District 1, Ho Chi Minh City",
    "showScheduledStart": "2026-09-05T19:00:00+07:00", "showScheduledEnd": "2026-09-05T22:00:00+07:00",
    "tierName": "VIP", "priceName": "Early Bird", "pricePaid": 300000.00,
    "accessType": "Physical", "status": "Confirmed",
    "qrCode": "a1b2c3d4e5f6478a9b0c1d2e3f4a5b6c", "purchasedAt": "2026-08-17T14:30:00+07:00",
    "physicalDetail": { "seatInfo": null, "checkedInAt": null },
    "livestreamDetail": null
  },
  "message": null
}
```
**Other status codes**: 401; 403 (not the buyer — this endpoint is buyer-only, stricter than `by-qr`); 404.

---

### GET /api/v1/tickets/{id}/qr
**Auth**: RequireAuthenticated
**Route params**: `id` (Guid) — ticket ID.
**Query params**: none
**Request body**: none
**Response 200**: `image/svg+xml` — raw SVG body, **not** wrapped in `ApiResponse<T>` (this is the one
endpoint in this whole document that returns a raw content-type instead of JSON).
**Other status codes**: 401; 403 (buyer-only, same rule as `GetDetail`); 404; **422** — `Vé chưa được xác
nhận thanh toán, chưa có mã QR để hiển thị` when `Ticket.QrCode` is still null (Pending/Cancelled ticket).
**Notes**: Renders the exact same `QrCode` string already present in `GetDetail`'s response as a scannable
SVG image via `Net.Codecrete.QrCodeGenerator` (not QRCoder).

---

### POST /api/v1/tickets/{id}/transfer
**Auth**: RequireAuthenticated
**Route params**: `id` (Guid) — ticket to transfer.
**Query params**: none
**Request body**: `InitiateTicketTransferRequest`
- `recipientEmail` (string) — required, must be a valid email format.

```json
{ "recipientEmail": "friend@example.com" }
```

**Response 204**: no body.
**Other status codes**:
- 400 — `recipientEmail` empty or not a valid email.
- 403 — ticket doesn't belong to caller.
- 404 — ticket not found; or no registered user with that email.
- 409 — a transfer is already pending on this ticket (atomic re-check at write time, not just in-memory).
- 422 — ticket status not `Confirmed`; show has `Ended`/`Cancelled`; ticket already checked in; ticket
  already used for livestream; recipient email is the caller's own account.

**Notes**: Regenerates `QrCode` and (if applicable) `LivestreamDetail.AccessToken` only on **accept**, not
here — the sender's current QR/token stays valid until the recipient actually accepts. Sends the recipient
an `EventReminder` notification.

---

### POST /api/v1/tickets/{id}/transfer/accept
**Auth**: RequireAuthenticated
**Route params**: `id` (Guid) — ticket ID.
**Query params**: none
**Request body**: none
**Response 204**: no body.
**Other status codes**: 400 (n/a — no validator beyond MediatR base); 403 — caller is not the invited
recipient (`PendingTransferToUserId`); 404 — ticket not found; 422 — ticket status not `Confirmed`; show
`Ended`/`Cancelled`.
**Notes**: Reassigns `BuyerId` to the caller, regenerates `QrCode` and `LivestreamDetail.AccessToken` (for
security — the previous owner had seen the old codes), clears the pending-transfer fields. Notifies the
previous buyer.

---

### POST /api/v1/tickets/{id}/transfer/cancel
**Auth**: RequireAuthenticated
**Route params**: `id` (Guid) — ticket ID.
**Query params**: none
**Request body**: none
**Response 204**: no body.
**Other status codes**: 403 — caller is neither the sender (current `BuyerId`) nor the invited recipient;
404 — ticket not found; 422 — no pending transfer exists on this ticket.
**Notes**: Dual-purpose endpoint — the sender calling it means "I cancel my own transfer offer"; the
recipient calling it means "I decline this invite." Both land on the identical result (pending fields
cleared, ticket stays with the original buyer). Notifies whichever side didn't call it.

---

## TicketTiersController
Base route: `api/v1/ticket-tiers`. No class-level `[Authorize]` — each action declares its own.

### GET /api/v1/ticket-tiers
**Auth**: AllowAnonymous
**Route params**: none
**Query params**: `showId` (int, required — no default; omitting it sends `0` and returns an empty list,
does not error)
**Request body**: none
**Response 200**: `IReadOnlyList<TicketTierSummaryDto>`
- `id` (int)
- `name` (string)
- `description` (string, nullable)
- `accessType` (string enum) — `Physical` | `Livestream`
- `totalCapacity` (int, nullable)
- `zoneId` (int, nullable)
- `prices` (array of `TicketPriceSummaryDto`):
  - `id` (int)
  - `name` (string)
  - `price` (decimal)
  - `quota` (int, nullable) — null means unlimited within this tier
  - `saleStart` (DateTimeOffset)
  - `saleEnd` (DateTimeOffset)
  - `purchaseChannel` (string enum) — `Online` | `Offline` | `Both`
  - `availableSlots` (int, nullable) — **live-computed** (`quota - reservedCount`, floored at 0), null if
    `quota` is null

```json
{
  "success": true,
  "data": [{
    "id": 45, "name": "VIP", "description": "Front-row seating", "accessType": "Physical",
    "totalCapacity": 50, "zoneId": 3,
    "prices": [{
      "id": 90, "name": "Early Bird", "price": 300000.00, "quota": 30,
      "saleStart": "2026-08-01T00:00:00+07:00", "saleEnd": "2026-09-01T00:00:00+07:00",
      "purchaseChannel": "Both", "availableSlots": 22
    }]
  }],
  "message": null
}
```
**Other status codes**: none beyond default framework errors.
**Notes**: `availableSlots` deliberately does NOT use a stored `Sold` column (that column is dead/always 0
in this codebase) — it's derived live from actual confirmed/pending tickets + active holds.

---

### GET /api/v1/ticket-tiers/{id}
**Auth**: AllowAnonymous
**Route params**: `id` (int) — tier ID.
**Query params**: none
**Request body**: none
**Response 200**: `TicketTierSummaryDto` — identical shape to one item of the list above.
**Other status codes**: 404 — tier not found.

---

### POST /api/v1/ticket-tiers
**Auth**: RequireOwner (Owner or Admin)
**Route params**: none
**Query params**: none
**Request body**: `CreateTicketTierCommand`
- `showId` (int) — required, > 0, must reference an existing `LoungeShow` owned by the caller.
- `name` (string) — required, non-empty, max 100 chars.
- `description` (string, nullable) — optional.
- `accessType` (string) — required. Allowed values (case-insensitive): `"Physical"`, `"Livestream"`. **Must
  match the show's `Format`**: `Offline` show → must be `Physical`; `Online` show → must be `Livestream`
  (validator cross-checks against the show).
- `zoneId` (int, nullable) — must reference an existing `SeatingZone` if provided. Only meaningful when
  `accessType = Physical`; silently dropped (set to null) server-side if `accessType = Livestream`.
- `totalCapacity` (int, nullable) — if provided, must be > 0.
- `prices` (array, required, min 1 element) — each `TicketPriceInput`:
  - `name` (string) — required, max 255 chars.
  - `price` (decimal) — required, > 0.
  - `quota` (int, nullable) — if provided, must be > 0.
  - `purchaseChannel` (string) — required. Allowed values (case-insensitive): `"Online"`, `"Offline"`,
    `"Both"`.
  - `saleStart` (DateTimeOffset) — required.
  - `saleEnd` (DateTimeOffset) — required, must be strictly after `saleStart`.

```json
{
  "showId": 12,
  "name": "VIP",
  "description": "Front-row seating",
  "accessType": "Physical",
  "zoneId": 3,
  "totalCapacity": 50,
  "prices": [
    {
      "name": "Early Bird",
      "price": 300000.00,
      "quota": 30,
      "purchaseChannel": "Both",
      "saleStart": "2026-08-01T00:00:00+07:00",
      "saleEnd": "2026-09-01T00:00:00+07:00"
    }
  ]
}
```

**Response 201**: `int` — the new `TicketTier.Id`.
```json
{ "success": true, "data": 45, "message": null }
```
**Other status codes**:
- 400 — any FluentValidation rule above fails, including the AccessType/Show.Format cross-check.
- 403 — caller doesn't own the show's venue.
- 404 (thrown as domain exception, not declared on the controller attribute but real) — show or zone not
  found.
- 422 — show is not in `Draft` status (tiers can only be added while the show is still Draft); adding this
  tier's `totalCapacity` would push the show's total ticket capacity past the Owner's active-subscription
  `MaxTicketsPerEventSnapshot`.

**Notes**: `Location` header points to `GET /ticket-tiers/{id}`. Creates the `TicketTier` row first, then
one `TicketPrice` row per array element in the same request.

---

### PUT /api/v1/ticket-tiers/{id}
**Auth**: RequireOwner (Owner or Admin)
**Route params**: `id` (int) — tier ID.
**Query params**: none
**Request body**: `UpdateTicketTierRequest` (controller-local record; `TierId` comes from the route, not the
body)
- `name` (string) — required, non-empty, max 100 chars.
- `description` (string, nullable).
- `totalCapacity` (int, nullable) — if provided, must be > 0.
- `accessType` (string) — required, `"Physical"` | `"Livestream"`, same show-format cross-check as Create.
- `zoneId` (int, nullable) — must exist if provided.
- `prices` (array, required, min 1) — **full replace**: existing `TicketPrice` rows for this tier are
  deleted and recreated from this array (same per-item shape/rules as Create).

```json
{
  "name": "VIP", "description": "Front-row seating, updated", "totalCapacity": 60,
  "accessType": "Physical", "zoneId": 3,
  "prices": [{ "name": "Standard", "price": 350000.00, "quota": 40, "purchaseChannel": "Both",
    "saleStart": "2026-08-01T00:00:00+07:00", "saleEnd": "2026-09-01T00:00:00+07:00" }]
}
```

**Response 204**: no body.
**Other status codes**: 400 (validation, same rules as Create); 403 (not the owner); 404 (tier not found,
or `zoneId` not found); 422 (show not `Draft`; new `totalCapacity` exceeds subscription cap, checked against
the OTHER tiers on the show plus this new value).
**Notes**: Only reachable while the show is `Draft` — since a real ticket can never have been sold against a
Draft show's `TicketPrice` rows yet, the full delete+recreate of Prices is safe.

---

### DELETE /api/v1/ticket-tiers/{id}
**Auth**: RequireOwner (Owner or Admin)
**Route params**: `id` (int) — tier ID.
**Query params**: none
**Request body**: none
**Response 204**: no body.
**Other status codes**: 403 (not the owner); 404 (tier not found); 422 (show not `Draft`).
**Notes**: `TicketPrice` rows cascade-delete with the tier at the DB level. Only safe pre-Draft since
`Ticket.PriceId` is `ON DELETE RESTRICT` — no real ticket could exist yet.

---

## PaymentsController
Base route: `api/v1/payments`. Both actions `[AllowAnonymous]`.

**These two endpoints are browser-redirect / server-to-server targets, not endpoints the FE app calls
directly via fetch/axios.** The FE only ever needs the `paymentUrl` string returned by
`POST /tickets/purchase` — redirect the user's browser to it; VNPay itself calls back to these URLs.

### GET /api/v1/payments/vnpay/callback
**Auth**: AllowAnonymous
**Route params**: none
**Query params**: VNPay's own query string params (`vnp_TxnRef`, `vnp_Amount`, `vnp_ResponseCode`,
`vnp_SecureHash`, etc.) — FE never constructs this call.
**Request body**: none
**Response**: `302 Found` — browser redirect to `BusinessSettings.PaymentSuccessUrl` or
`.PaymentFailedUrl` (FE-configured app URLs), never JSON.
**Other status codes**: n/a (always redirects; internal processing failure still redirects to the failed
URL rather than erroring).
**Notes**: Fires only if the buyer's browser makes it back to this server after paying. Idempotent —
processes the same `ProcessVnPayCallbackCommand` as the IPN endpoint below; whichever of the two arrives
first wins, the other is a no-op.

### GET /api/v1/payments/vnpay/ipn
**Auth**: AllowAnonymous
**Route params**: none
**Query params**: VNPay's server-to-server IPN params.
**Request body**: none
**Response 200**: `VnPayIpnResponse` (plain, NOT wrapped in `ApiResponse<T>` — this is VNPay's own expected
contract)
- `rspCode` (string) — `"00"` on success, `"99"` on any failure/rejection.
- `message` (string) — `"Confirm Success"` or `"Unknown error"`.

```json
{ "rspCode": "00", "message": "Confirm Success" }
```
**Other status codes**: always `200` — VNPay reads `RspCode` in the body, not the HTTP status, to decide
whether to keep retrying.
**Notes**: **This is the URL that must be registered as the order's IPN URL in the VNPay merchant portal**
(not `vnpay/callback`) — it fires independent of the buyer's browser (survives closed tabs, lost
connectivity, backgrounded apps), which the callback endpoint cannot guarantee. On success: `Payment.Status
→ Confirmed`, all linked `Ticket`s → `Confirmed` with a freshly generated `QrCode`, and — depending on the
tier's `AccessType` — either a `LivestreamTicketDetail` (with `AccessToken`) or `PhysicalTicketDetail` row
is attached to each ticket. Verified idempotent against VNPay's retry storms via a distributed lock keyed
`vnpay-ticket:{txnRef}` plus a `Payment.Status != Pending` replay check. Amount-mismatch between what VNPay
reports and the original `Payment.GrossAmount` fails closed (treated as unconfirmed).

---

## DonationsController
Base route: `api/v1/donations`. No class-level `[Authorize]` — each action declares its own. (Also includes
`PerformerDonationsController` at `api/v1/performers`, documented at the end of this section — same source
file, second controller class.)

### POST /api/v1/donations
**Auth**: RequireAuthenticated
**Route params**: none
**Query params**: none
**Request body**: `CreateDonationRequest` (controller-local record; server derives `ClientIpAddress`)
- `performanceId` (int) — required, > 0, must reference an existing `Performance`.
- `amount` (decimal) — required, > 0 and ≤ `system_config DonationMaxAmount` (default 50,000,000 VND).
- `isAnonymous` (bool) — optional, default `false`. When `true`, `displayName` is not populated even if the
  caller has a `FullName`.
- `message` (string, nullable) — optional, max 500 chars.
- `isMessagePublic` (bool) — optional, default `true`.

```json
{ "performanceId": 77, "amount": 100000.00, "isAnonymous": false, "message": "Great show!", "isMessagePublic": true }
```

**Response 201**: `DonationInitiationDto`
- `donationId` (int)
- `orderId` (string) — format `DON-yyyyMMddHHmmss-<guid32>`, truncated to 40 chars.
- `gross` (decimal) — the amount the donor pays (echoes request `amount`).
- `paymentUrl` (string) — redirect the donor's browser here.

```json
{ "success": true, "data": { "donationId": 301, "orderId": "DON-20260817150000-...", "gross": 100000.00, "paymentUrl": "https://sandbox.vnpayment.vn/..." }, "message": null }
```

**Other status codes**:
- 400 — `performanceId` invalid/not found, `amount` out of range, `message` too long.
- 404 — performance or its show not found.
- 422 — show is not `Ongoing`; performer (`Performance.AcceptsDonation == false`) doesn't accept donations.

**Notes**: No `Location` header is set (still Pending at creation time — RFC 7231 doesn't mandate one).
`Net`/`PlatformFee`/`Tax` are computed here as an **estimate** using the current `PlatformCommissionRate`/
`TaxRate` system_config values — `ProcessDonationPaymentCommandHandler` overwrites them with the
authoritative figures once VNPay actually confirms (rates could drift in between). `Status` starts at
`PendingPayment`.

---

### GET /api/v1/donations/{id}
**Auth**: RequireAuthenticated
**Route params**: `id` (int) — donation ID.
**Query params**: none
**Request body**: none
**Response 200**: `DonationDto`
- `id` (int)
- `performanceId` (int)
- `performerName` (string)
- `showName` (string)
- `gross` (decimal) — what the donor paid
- `net` (decimal) — Owner's chặng-1 take (gross minus platform fee minus tax)
- `platformFee` (decimal)
- `tax` (decimal)
- `performerShareRate` (decimal, nullable) — the frozen % of gross promised to the performer
- `performerAmount` (decimal, nullable) — derived payout amount once known
- `ownerRetained` (decimal, nullable)
- `status` (string) — **plain string, NOT the raw enum name in all cases** — sourced from
  `DonationStatus.ToString()` via the repository projection: `PendingPayment` | `PendingOwnerAck` |
  `OwnerReceived` | `PerformerPaid` | `Cancelled` | `Refunded`
- `autoConfirmed` (bool)
- `ownerAckAt` (DateTimeOffset, nullable)
- `ownerPaidAt` (DateTimeOffset, nullable)
- `isAnonymous` (bool)
- `displayName` (string, nullable)
- `isAmountPublic` (bool)
- `message` (string, nullable)
- `createdAt` (DateTimeOffset)
- `paymentRef` (string, nullable) — **added 2026-08-18**
- `paymentEvidenceUrl` (string, nullable) — **added 2026-08-18**

> **⚠️ `paymentRef` / `paymentEvidenceUrl` are redacted per caller.** Both are the chặng-2 payout receipt
> the Owner uploaded when confirming they paid the performer. They are `null` in two distinct cases:
>
> 1. The donation has not reached `PerformerPaid` yet (nothing recorded).
> 2. **The caller is only the donor.** A donor is otherwise authorised to read this donation, but the
>    receipt is a bank document between the Owner and the performer showing their account details — none
>    of the donor's business. Only **Admin** (tracing a dispute back to the venue) and the **receiving
>    Owner** (reviewing what they submitted) ever see values here.
>
> FE must therefore treat `null` as "not available to you", not as "the Owner didn't upload anything".
> Redaction happens in `GetDonationByIdQueryHandler`, after the repository projection.

```json
{
  "success": true,
  "data": {
    "id": 301, "performanceId": 77, "performerName": "Nguyen Van A", "showName": "Jazz Night",
    "gross": 100000.00, "net": 90000.00, "platformFee": 5000.00, "tax": 5000.00,
    "performerShareRate": 0.88, "performerAmount": 79200.00, "ownerRetained": 10800.00,
    "status": "OwnerReceived", "autoConfirmed": false,
    "ownerAckAt": "2026-08-17T15:10:00+07:00", "ownerPaidAt": null,
    "isAnonymous": false, "displayName": "Tran Thi B", "isAmountPublic": true,
    "message": "Great show!", "createdAt": "2026-08-17T15:00:00+07:00"
  },
  "message": null
}
```
**Other status codes**: 403 — caller is neither the donor, the receiving venue's Owner, nor Admin; 404 —
donation not found.
**Notes**: `DonationStatus` enum full value list (from `DonationStatus.cs`): `PendingPayment` (just
created, awaiting VNPay), `PendingOwnerAck` (VNPay succeeded, awaiting Owner ack), `OwnerReceived` (Owner
acked), `PerformerPaid` (Owner confirmed forwarding to performer), `Cancelled` (VNPay failed/expired),
`Refunded` (Admin-reversed, only reachable before `PerformerPaid`).

---

### GET /api/v1/donations/public
**Auth**: AllowAnonymous
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 20, clamped 1–100)
**Request body**: none
**Response 200**: `PaginatedResult<PublicDonationTransactionDto>` — `items[]` fields:
- `id` (int)
- `performerName` (string)
- `showName` (string)
- `venueName` (string)
- `showDate` (DateTimeOffset)
- `donorDisplayName` (string, nullable)
- `gross` (decimal, nullable)
- `net` (decimal, nullable)
- `platformFee` (decimal, nullable)
- `tax` (decimal, nullable)
- `performerShareRate` (decimal, nullable)
- `performerAmount` (decimal, nullable)
- `ownerRetained` (decimal, nullable)
- `status` (string)
- `createdAt` (DateTimeOffset)

**IMPORTANT**: all `decimal?` money fields null **as a group** when the donor set `IsAmountPublic = false`
— never partially, since revealing `Net`/`PlatformFee`/`PerformerAmount` while hiding `Gross` would let
`Gross` be reverse-derived from the others.

```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 301, "performerName": "Nguyen Van A", "showName": "Jazz Night", "venueName": "Blue Note Saigon",
      "showDate": "2026-09-05T19:00:00+07:00", "donorDisplayName": "Tran Thi B",
      "gross": 100000.00, "net": 90000.00, "platformFee": 5000.00, "tax": 5000.00,
      "performerShareRate": 0.88, "performerAmount": 79200.00, "ownerRetained": 10800.00,
      "status": "OwnerReceived", "createdAt": "2026-08-17T15:00:00+07:00"
    }],
    "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: none (public, no auth failures possible).
**Notes**: System-wide feed across every venue/performer (unlike `GET /performers/{id}/donations`, which is
scoped to one performer). Only shows donations that reached `OwnerReceived`/`PerformerPaid` — earlier
statuses aren't in this feed.

---

### GET /api/v1/donations/vnpay-return
**Auth**: AllowAnonymous
**Response**: `302 Found` redirect to `PaymentSuccessUrl`/`PaymentFailedUrl`. Browser-redirect target, not
FE-called directly. Same idempotent processing as `vnpay-ipn` below.

### GET /api/v1/donations/vnpay-ipn
**Auth**: AllowAnonymous
**Response 200**: `VnPayIpnResponse` (`{"rspCode": "00"|"99", "message": "..."}`), unwrapped.
**Notes**: **Register this URL** (not `vnpay-return`) as the donation order's IPN URL in the VNPay merchant
portal. On success: `Donation.Status → PendingOwnerAck`, writes the chặng-1 ledger journal (Gateway debit /
Platform commission / Tax / Owner net credit), overwrites the `Net`/`PlatformFee`/`Tax` estimate with the
authoritative rate-at-confirmation figures, freezes `PerformerShareRateSnapshot`, notifies both the Owner
(`DonationReceived`) and the donor (`DonationConfirmed`), and broadcasts to the public donation feed
(SignalR `PublicDonationHub`) plus the livestream donation-alert overlay if the show is currently `Live`.
Idempotent via lock `vnpay-donation:{txnRef}` + `Status != PendingPayment` replay guard. On failure:
`Donation.Status → Cancelled`.

---

### GET /api/v1/donations/my
**Auth**: RequireAuthenticated
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 20, clamped 1–**50**)
**Request body**: none
**Response 200**: `PaginatedResult<MyDonationDto>` — `items[]` fields:
- `id` (int)
- `performerName` (string)
- `showName` (string)
- `gross` (decimal)
- `net` (decimal)
- `status` (string)
- `isAnonymous` (bool)
- `message` (string, nullable)
- `paymentConfirmedAt` (DateTimeOffset, nullable)
- `createdAt` (DateTimeOffset)

```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 301, "performerName": "Nguyen Van A", "showName": "Jazz Night",
      "gross": 100000.00, "net": 90000.00, "status": "OwnerReceived", "isAnonymous": false,
      "message": "Great show!", "paymentConfirmedAt": "2026-08-17T15:05:00+07:00",
      "createdAt": "2026-08-17T15:00:00+07:00"
    }],
    "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: 401 only.
**Notes**: Shows all statuses, not just confirmed ones (so the donor can see a still-`PendingPayment` or
`Cancelled` attempt too).

---

### GET /api/v1/donations/pending-ack
**Auth**: RequireOwner (Owner or Admin)
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 20, clamped 1–50)
**Request body**: none
**Response 200**: `PaginatedResult<PendingDonationDto>` — `items[]` fields:
- `id` (int)
- `performerName` (string)
- `showName` (string)
- `gross` (decimal)
- `net` (decimal)
- `amountToPayPerformer` (decimal) — projected payout using the current
  `DonationPerformerShareRate` config (default 0.88)
- `isAnonymous` (bool)
- `displayName` (string, nullable)
- `message` (string, nullable)
- `paymentConfirmedAt` (DateTimeOffset, nullable)
- `autoConfirmDeadline` (DateTimeOffset, nullable) — if unattended past this point, a background job
  auto-acknowledges on the Owner's behalf

**Other status codes**: 401, 403 (not Owner/Admin).
**Notes**: Only donations in `PendingOwnerAck` for venues the caller owns. This is the queue
`POST /donations/{id}/acknowledge` acts on.

---

### GET /api/v1/donations/awaiting-payout
**Auth**: RequireOwner (Owner or Admin)
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 20, clamped 1–50)
**Request body**: none
**Response 200**: `PaginatedResult<PendingDonationDto>` — identical shape to `pending-ack` above.
**Other status codes**: 401 (not declared on controller attributes but real via policy), 403.
**Notes**: Donations already `OwnerReceived`, awaiting the Owner to actually forward money to the performer
and call `POST /donations/{id}/confirm-paid`.

---

### POST /api/v1/donations/{id}/acknowledge
**Auth**: RequireOwner (Owner or Admin)
**Route params**: `id` (int) — donation ID.
**Query params**: none
**Request body**: none
**Response 204**: no body.
**Other status codes**: 403 — caller doesn't own the receiving venue; 404 — donation not found; 422 —
donation is `Cancelled`, or not currently in `PendingOwnerAck`.
**Notes**: `Status → OwnerReceived`, stamps `OwnerAckAt`. Side effects: broadcasts a `DonationAlertDto` to
the livestream hub if the show is currently `Live` (donor name shown as `"Ẩn danh"` if anonymous, message
included only if `IsMessagePublic`), and publishes to the public donation transparency feed.

---

### POST /api/v1/donations/{id}/confirm-paid
**Auth**: RequireOwner (Owner or Admin)
**Route params**: `id` (int) — donation ID.
**Query params**: none
**Request body**: `ConfirmDonationPaidRequest`
- `paymentRef` (string) — required, non-empty, max 255 chars.
- `paymentEvidenceUrl` (string) — **REQUIRED as of 2026-08-18** (was optional), max 500 chars. Upload the
  receipt via `POST /api/v1/uploads/images` first and send the returned URL here.

```json
{ "paymentRef": "TXN-20260817-001", "paymentEvidenceUrl": "https://cdn.example.com/proof.jpg" }
```

**Response 204**: no body.
**Other status codes**: 400 — `paymentRef` empty/too long, `paymentEvidenceUrl` **missing**/too long;
403 — not the receiving Owner; 404 — donation not found; 422 — donation not currently `OwnerReceived`.

> **⚠️ BREAKING CHANGE 2026-08-18** — two changes that pull in opposite directions:
>
> 1. `paymentEvidenceUrl` went **optional → required**. Omitting it now fails validation with **400**
>    (a validator rule, so it fires before the handler — not the 422 you would get from a domain rule).
> 2. The performer no longer needs a registered default bank account. This previously threw **422**
>    (*"Nghệ sĩ chưa đăng ký tài khoản ngân hàng mặc định…"*) and left the donation stuck at
>    `OwnerReceived` forever.
>
> **Why:** the platform never transfers money to a performer bank account — `SettlementSchedulingService`
> only ever resolves `BankAccountOwnerType.Lounge`. Chặng 2 is the **Owner paying the performer directly**
> by whatever means they already use (bank transfer, e-wallet, cash after the set). Demanding a registered
> account before recording a transfer that had already happened blocked a completed real-world payment on
> a record the platform does not act on. An uploaded receipt is what actually evidences that money moved,
> so it became the required artefact instead.

**Notes**: `Status → PerformerPaid`, writes the chặng-2 ledger journal (Owner debit / Performer credit for
`performerAmount = Gross * performerShareRateSnapshot`, falling back to live `DonationPerformerShareRate`
config only for pre-migration donations with no frozen snapshot). Broadcasts the final leg of the public
donation transparency timeline. `BankAccountId` is still snapshotted **when the performer happens to have a
default account on file**, and left `null` otherwise — it is a record, never a precondition.

---

### GET /api/v1/performers/{performerId}/donations
_(Second controller class in the same file: `PerformerDonationsController`)_
**Auth**: AllowAnonymous
**Route params**: `performerId` (int) — the performer whose donation history to show.
**Query params**: `page` (int, default 1), `pageSize` (int, default 20, clamped 1–100)
**Request body**: none
**Response 200**: `PaginatedResult<PublicDonationDto>` — `items[]` fields:
- `id` (int)
- `showName` (string)
- `venueName` (string)
- `showDate` (DateTimeOffset)
- `donorDisplayName` (string, nullable)
- `gross` (decimal, nullable) — null when donor set `IsAmountPublic = false`
- `status` (string)
- `createdAt` (DateTimeOffset)

```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 301, "showName": "Jazz Night", "venueName": "Blue Note Saigon",
      "showDate": "2026-09-05T19:00:00+07:00", "donorDisplayName": "Tran Thi B",
      "gross": 100000.00, "status": "OwnerReceived", "createdAt": "2026-08-17T15:00:00+07:00"
    }],
    "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: none (public).
**Notes**: Narrower `PublicDonationDto` than the fee-breakdown-carrying `PublicDonationTransactionDto` used
by `GET /donations/public` — no fee split fields, since this is meant as a performer's public profile
donation history, not a platform-wide transparency ledger.

---

## SubscriptionsController
Base route: `api/v1/subscriptions`. No class-level `[Authorize]` — each action declares its own.

### GET /api/v1/subscriptions/packages
**Auth**: AllowAnonymous
**Route params**: none
**Query params**: `activeOnly` (bool, default `true`)
**Request body**: none
**Response 200**: `IReadOnlyList<SubscriptionPackageDto>`
- `id` (int)
- `name` (string)
- `description` (string, nullable)
- `price` (decimal)
- `billingCycle` (string enum) — `Monthly` | `Quarterly` | `Yearly`
- `maxTicketsPerEvent` (int)
- `hasAiPoster` (bool)
- `maxAiPostersPerMonth` (int)
- `maxTourScenes` (int)
- `isActive` (bool)

```json
{
  "success": true,
  "data": [{
    "id": 3, "name": "Pro", "description": "For growing venues", "price": 990000.00,
    "billingCycle": "Monthly", "maxTicketsPerEvent": 500, "hasAiPoster": true,
    "maxAiPostersPerMonth": 10, "maxTourScenes": 5, "isActive": true
  }],
  "message": null
}
```
**Other status codes**: none.
**Notes**: `activeOnly=false` is only honored for callers with the `Admin` role — everyone else (including
anonymous callers) is silently forced to active-only regardless of what they pass, so retired pricing
tiers aren't publicly enumerable.

---

### POST /api/v1/subscriptions/packages
**Auth**: RequireAdmin
**Route params**: none
**Query params**: none
**Request body**: `CreateSubscriptionPackageCommand` — **8 fields total** (confirmed against source;
matches the previously-verified shape — this replaces the stale example in README-SETUP.md):
- `name` (string) — required, non-empty, max 100 chars.
- `description` (string, nullable) — optional, max 2000 chars.
- `price` (decimal) — required, > 0.
- `billingCycle` (string) — required. Allowed values (case-insensitive): `"Monthly"`, `"Quarterly"`,
  `"Yearly"`.
- `maxTicketsPerEvent` (int) — required, > 0.
- `hasAiPoster` (bool) — required (non-nullable bool — omitting it binds to `false`, not a 400).
- `maxAiPostersPerMonth` (int) — required, ≥ 0 in general, but **must be > 0 if `hasAiPoster = true`**
  (conditional FluentValidation rule — this is the exact gotcha discovered earlier: forgetting this field,
  or setting it to 0 while `hasAiPoster = true`, produces a real 400).
- `maxTourScenes` (int) — required, ≥ 0.

```json
{
  "name": "Pro",
  "description": "For growing venues",
  "price": 990000.00,
  "billingCycle": "Monthly",
  "maxTicketsPerEvent": 500,
  "hasAiPoster": true,
  "maxAiPostersPerMonth": 10,
  "maxTourScenes": 5
}
```

**Response 201**: `int` — new `SubscriptionPackage.Id`.
```json
{ "success": true, "data": 3, "message": null }
```
**Other status codes**: 400 (any FluentValidation rule above); 403 (not Admin).
**Notes**: `IsActive` is always set `true` on creation — there's no field for it in the create command (only
in Update). `Location` header points at `GET /subscriptions/packages` (list, not a single-item route — no
single-package GET exists).

---

### PUT /api/v1/subscriptions/packages/{id}
**Auth**: RequireAdmin
**Route params**: `id` (int) — package ID.
**Query params**: none
**Request body**: `UpdateSubscriptionPackageRequest` (controller-local record; `PackageId` from route)
- `name` (string) — required, non-empty, max 100.
- `description` (string, nullable) — max 2000.
- `price` (decimal) — required, > 0.
- `billingCycle` (string) — required, `"Monthly"` | `"Quarterly"` | `"Yearly"`.
- `maxTicketsPerEvent` (int) — required, > 0.
- `hasAiPoster` (bool) — required.
- `maxAiPostersPerMonth` (int) — required, ≥ 0, must be > 0 if `hasAiPoster = true`.
- `maxTourScenes` (int) — required, ≥ 0.
- `isActive` (bool) — required (this field only exists on Update, not Create).

```json
{
  "name": "Pro", "description": "For growing venues, updated", "price": 1090000.00,
  "billingCycle": "Monthly", "maxTicketsPerEvent": 600, "hasAiPoster": true,
  "maxAiPostersPerMonth": 15, "maxTourScenes": 8, "isActive": true
}
```

**Response 204**: no body.
**Other status codes**: 400 (validation); 403 (not Admin); 404 (package not found); **422** — if any Owner
currently has an `Active` subscription on this package, `price`/`billingCycle`/`maxTicketsPerEvent`/
`hasAiPoster`/`maxAiPostersPerMonth`/`maxTourScenes` become locked — changing ANY of them throws. Only
`name`/`description`/`isActive` remain editable once the package has an active subscriber.

**Notes**: To really change locked terms on an in-use package, create a new package and deactivate the old
one instead — this is by design (D12), not a bug.

---

### POST /api/v1/subscriptions/subscribe
**Auth**: RequireOwner (Owner or Admin)
**Route params**: none
**Query params**: none
**Request body**: `SubscribeToPackageRequest` (controller-local record; server derives `ClientIpAddress`)
- `packageId` (int) — required, > 0, must reference an existing `SubscriptionPackage`.

```json
{ "packageId": 3 }
```

**Response 201**: `SubscriptionPaymentInitiationDto`
- `paymentId` (int)
- `orderId` (string) — format `SUB-yyyyMMddHHmmss-<guid32>`, truncated to 40 chars.
- `amount` (decimal) — the package's `Price`.
- `paymentUrl` (string) — redirect target.

```json
{ "success": true, "data": { "paymentId": 9100, "orderId": "SUB-20260817160000-...", "amount": 990000.00, "paymentUrl": "https://sandbox.vnpayment.vn/..." }, "message": null }
```

**Other status codes**: 400 — `packageId` invalid/not found; 403 (not Owner/Admin); 404 — package not
found; 409 — caller already has an unexpired `Active` `OwnerSubscription` (must cancel or wait for expiry
first); 422 — package `IsActive == false`.
**Notes**: Snapshots `MaxTicketsPerEvent`/`HasAiPoster`/`MaxAiPostersPerMonth`/`MaxTourScenes` onto the
`Payment` row at checkout time (not just at confirmation) — protects against an Admin editing the package
mid-checkout.

---

### POST /api/v1/subscriptions/renew
**Auth**: RequireOwner (Owner or Admin)
**Route params**: none
**Query params**: none
**Request body**: none (server derives `ClientIpAddress`; re-uses the Owner's most recent package)
**Response 201**: `SubscriptionPaymentInitiationDto` — identical shape to `subscribe` above.
**Other status codes**: 400 (n/a — no body to validate); 403; 404 — package from the last subscription no
longer exists; 409 — already has an active unexpired subscription; 422 — caller has never subscribed to
anything before (nothing to renew), or the last package is now `IsActive == false`.
**Notes**: NOT truly automatic — VNPay's `token_pay` flow has no silent merchant-initiated charge, so this
still requires the Owner to complete one OTP/3DS confirmation; it just skips re-picking a package from the
catalog. `SubscriptionExpiryWarningJob` sends 30/7/1-day-before reminders separately.

---

### POST /api/v1/subscriptions/cancel
**Auth**: RequireOwner (Owner or Admin)
**Route params**: none
**Query params**: none
**Request body**: none
**Response 204**: no body.
**Other status codes**: 403; 422 — caller has no active unexpired subscription to cancel.
**Notes**: Does NOT refund or prorate — the Owner already paid for the full period. Cancelling just flips
`Status → Cancelled` so it stops blocking a new `subscribe`/`renew` call instead of the entitlement simply
lapsing on its own at `ExpiresAt`.

---

### GET /api/v1/subscriptions/vnpay-return
**Auth**: AllowAnonymous
**Response**: `302 Found` redirect. Browser-redirect target, not FE-called directly.

### GET /api/v1/subscriptions/vnpay-ipn
**Auth**: AllowAnonymous
**Response 200**: `VnPayIpnResponse` (`{"rspCode": "00"|"99", "message": "..."}`), unwrapped.
**Notes**: **Register this URL** as the subscription order's VNPay IPN URL. Locking strategy differs from
the ticket/donation IPN handlers: locked by **owner ID**, not `txnRef` — this specifically closes a
double-submit race where an Owner double-clicks "Subscribe," gets two different Payment rows with two
different `txnRef`s, and both VNPay confirmations could otherwise race independently. If the owner already
has an Active subscription by the time this callback lands (the other half of the double-submit won the
race), this payment is still booked as `Confirmed` with its own "duplicate — needs refund" ledger journal
and a `DuplicatePaymentDetected` notification — no silent money loss, no duplicate `OwnerSubscription`
created. On genuine first-confirmation: creates the `OwnerSubscription` (`ExpiresAt` computed from
`BillingCycle`: `Monthly` → +1 month, `Quarterly` → +3 months, `Yearly` → +1 year), snapshotting
entitlement fields from the `Payment` row (not a fresh package re-fetch, to survive concurrent admin edits),
and writes a 2-line ledger journal (subscription revenue is 100% platform's, unlike ticket/donation's 3-way
split).

---

### GET /api/v1/subscriptions/my
**Auth**: RequireOwner (Owner or Admin)
**Route params**: none
**Query params**: none
**Request body**: none
**Response 200**: `MySubscriptionDto` — **can be `null`** inside `data` if the Owner has never subscribed
(the query returns `MySubscriptionDto?`, controller wraps it as `ApiResponse<MySubscriptionDto?>.Ok(null)`
rather than 404).
- `id` (int) — `OwnerSubscription.Id`
- `packageId` (int)
- `packageName` (string) — `""` if the original package was since deleted (defensive fallback, not
  expected in practice since packages are soft-deactivated, never hard-deleted)
- `startedAt` (DateTimeOffset)
- `expiresAt` (DateTimeOffset)
- `status` (string enum) — `Active` | `Suspended` | `Expired` | `Cancelled`
- `maxTicketsPerEventSnapshot` (int)
- `hasAiPosterSnapshot` (bool)
- `maxAiPostersPerMonthSnapshot` (int)
- `maxTourScenesSnapshot` (int)

```json
{
  "success": true,
  "data": {
    "id": 55, "packageId": 3, "packageName": "Pro",
    "startedAt": "2026-08-17T16:05:00+07:00", "expiresAt": "2026-09-17T16:05:00+07:00",
    "status": "Active", "maxTicketsPerEventSnapshot": 500, "hasAiPosterSnapshot": true,
    "maxAiPostersPerMonthSnapshot": 10, "maxTourScenesSnapshot": 5
  },
  "message": null
}
```

If never subscribed:
```json
{ "success": true, "data": null, "message": null }
```

**Other status codes**: 403 (not Owner/Admin).
**Notes**: Always returns the Owner's MOST RECENT subscription by `StartedAt`, regardless of status — so a
`Cancelled`/`Expired` one is still returned (not filtered to Active-only) if it's the latest. FE must check
`status`/`expiresAt` itself to decide if the entitlement is currently usable — this endpoint does not
pre-filter that.

---

## Enum quick-reference (all values sourced from `src/MusicLounge.Domain/Enums/*.cs`)

| Enum | Values |
|---|---|
| `AccessType` | `Physical`, `Livestream` |
| `TicketStatus` | `Pending`, `Confirmed`, `Used`, `Cancelled`, `Refunded` |
| `PurchaseChannel` | `Online`, `Offline`, `Both` |
| `PaymentStatus` | `Pending`, `Confirmed`, `Failed`, `Refunded` |
| `PaymentMethod` | `Gateway`, `Cash` |
| `PaymentSettlementStatus` | `NotApplicable`, `Collected`, `PartiallyReleased`, `FullyReleased`, `Refunded` |
| `RefundRequestStatus` | `Pending`, `Approved`, `Rejected` |
| `DonationStatus` | `PendingPayment`, `PendingOwnerAck`, `OwnerReceived`, `PerformerPaid`, `Cancelled`, `Refunded` |
| `SubscriptionBillingCycle` | `Monthly`, `Quarterly`, `Yearly` |
| `SubscriptionStatus` | `Active`, `Suspended`, `Expired`, `Cancelled` |
| `LoungeShowStatus` (referenced in preconditions above) | `Draft`, `Pending`, `Published`, `Ongoing`, `Ended`, `Cancelled` |
| `LoungeShowFormat` (referenced in tier AccessType cross-check) | `Offline`, `Online` |

## Auth policy → role mapping (from `Program.cs` `AddAuthorization`)

| Policy | Roles allowed |
|---|---|
| `RequireAuthenticated` | any authenticated user |
| `RequireStaff` | `Staff`, `Admin` |
| `RequireVenueOperator` | `Staff`, `Owner`, `Admin` |
| `RequireOwner` | `Owner`, `Admin` |
| `RequireAdmin` | `Admin` |

---

## Part 5 — F&B

> `pageSize` clamp tối đa 100 (không bị siết chặt hơn như Part 2/4). Handler `Create`/`UpdateStatus` đơn hàng F&B dùng `VenueOperatorAccess.CanOperate` (có Admin bypass); nhưng `GetFnbOrdersQuery`/`GetFnbOrderByIdQuery` (đọc) và 6 handler CRUD menu/menu-item **không có Admin bypass** — xem Notes từng endpoint.

## FnbMenusController
Base route: `api/v1/fnb-menus`

### GET /api/v1/fnb-menus
**Auth**: AllowAnonymous
**Route params**: none
**Query params**:
- `loungeId` (int, required — no default, missing value binds to `0`) — venue to list menus for.
- `activeOnly` (bool, default `true`) — when `true`, only returns menus where `IsActive == true`.

**Request body**: none
**Response 200**: `data` is `FnbMenuDto[]`, ordered ascending by `DisplayOrder`.
- `id` (int)
- `loungeId` (int)
- `name` (string)
- `description` (string, nullable)
- `isActive` (bool)
- `displayOrder` (int)

Example `data`:
```json
[
  {
    "id": 45,
    "loungeId": 12,
    "name": "Đồ uống",
    "description": "Menu thức uống chính",
    "isActive": true,
    "displayOrder": 1
  },
  {
    "id": 46,
    "loungeId": 12,
    "name": "Món ăn nhẹ",
    "description": null,
    "isActive": true,
    "displayOrder": 2
  }
]
```
**Other status codes**: none besides 200 — if `loungeId` matches no menus (or doesn't exist), returns `data: []`, not 404.
**Notes**: No existence check on `loungeId` — an unknown lounge id just returns an empty array.

---

### GET /api/v1/fnb-menus/{id}
**Auth**: AllowAnonymous
**Route params**: `id` (int) — menu id.
**Query params**: none
**Request body**: none
**Response 200**: single `FnbMenuDto` (same shape as above), regardless of `IsActive` — unlike the list endpoint there is no active-only filtering here, so a hidden/inactive menu is still readable by direct id with zero auth.
**Other status codes**: 404 (`NotFoundException`) if no menu with that id exists.
**Notes**: Publicly readable, no ownership check.

---

### POST /api/v1/fnb-menus
**Auth**: RequireOwner (role `Owner` or `Admin`; handler additionally requires `lounge.OwnerId == currentUser.UserId` — see Global conventions, no Admin bypass)
**Route params**: none
**Request body** — `CreateFnbMenuCommand`:
- `loungeId` (number, int) — required, must be `> 0` (FluentValidation `GreaterThan(0)`). **No existence check in the validator** — a non-existent lounge id passes validation and only fails later in the handler with 404.
- `name` (string) — required, `NotEmpty`, max length 255.
- `description` (string, nullable) — optional, max length 500.
- `displayOrder` (number, int) — required by JSON shape (non-nullable), **no range/validator rule at all** — any int including negative is accepted.
- `isActive` (bool) — optional, **defaults to `true` if omitted** (record default parameter).

Example request:
```json
{
  "loungeId": 12,
  "name": "Đồ uống",
  "description": "Menu thức uống chính",
  "displayOrder": 1,
  "isActive": true
}
```
**Response 201**: `data` is the new menu's `id` (plain int). `Location` header points to `GET /api/v1/fnb-menus/{id}`.
```json
{ "success": true, "data": 45, "message": null }
```
**Other status codes**:
- 400 — FluentValidation failure (`loungeId <= 0`, empty/too-long `name`, too-long `description`) → `errors` populated.
- 404 — `loungeId` doesn't exist (`NotFoundException` from the handler, not the validator).
- 403 — authenticated Owner does not own that lounge (`ForbiddenException`, message "Bạn không có quyền quản lý menu cho venue này.").
**Notes**: `isActive` was previously Update-only; Create now accepts it too (see comment in `CreateFnbMenuCommand.cs`).

---

### PUT /api/v1/fnb-menus/{id}
**Auth**: RequireOwner (same ownership caveat as POST — no Admin bypass)
**Route params**: `id` (int) — menu id.
**Request body** — controller-local `UpdateFnbMenuRequest` (mapped 1:1 into `UpdateFnbMenuCommand`):
- `name` (string) — required, `NotEmpty`, max length 255.
- `description` (string, nullable) — optional, max length 500.
- `isActive` (bool) — required by JSON shape; **no record default** — if the field is omitted from the JSON body, it deserializes to `false` (System.Text.Json default for missing non-nullable bool), silently deactivating the menu. Always send this field explicitly.
- `displayOrder` (number, int) — required by JSON shape, must be `>= 0` (`GreaterThanOrEqualTo(0)`) — stricter than Create, which has no rule at all.

Example request:
```json
{
  "name": "Đồ uống",
  "description": "Menu thức uống chính - cập nhật",
  "isActive": true,
  "displayOrder": 1
}
```
**Response 204**: No Content, no body.
**Other status codes**:
- 400 — FluentValidation failure.
- 404 — menu id not found, or (rare) the menu's parent lounge not found.
- 403 — not the lounge's owner.
**Notes**: Full replace — every field must be sent; there is no PATCH/partial-update endpoint.

---

### DELETE /api/v1/fnb-menus/{id}
**Auth**: RequireOwner (same ownership caveat)
**Route params**: `id` (int) — menu id.
**Response 204**: No Content.
**Other status codes**:
- 404 — menu or its lounge not found.
- 403 — not the lounge's owner (message "Bạn không có quyền xoá menu này.").
- 409 — `ConflictException` if **any** menu item under this menu was ever ordered (checked across all `FnbMenuItem` rows belonging to the menu via `OrderItem.MenuItemId`). Message tells the caller to set `IsActive=false` instead of deleting.
**Notes**: `fnb_menu_items.menu_id` cascades at the DB level, but `fnb_order_items.menu_item_id` is `ON DELETE RESTRICT`, so a menu with order history can never be hard-deleted — this 409 is a pre-check to give a clean error instead of letting the DB reject the cascade.

---

## FnbMenuItemsController
Base route: `api/v1/fnb-menu-items`

### GET /api/v1/fnb-menu-items
**Auth**: AllowAnonymous
**Query params**:
- `menuId` (int, required — missing binds to `0`) — parent menu.
- `availableOnly` (bool, default `true`) — filters `IsAvailable == true` when true.

**Response 200**: `data` is `FnbMenuItemDto[]`, ordered ascending by `DisplayOrder`:
- `id` (int)
- `menuId` (int)
- `category` (string) — free-text category label (e.g. "Cocktail", "Snack") — no enum, no fixed taxonomy.
- `name` (string)
- `description` (string, nullable)
- `price` (decimal)
- `imageUrl` (string, nullable)
- `isAvailable` (bool)
- `displayOrder` (int)

Example `data`:
```json
[
  {
    "id": 101,
    "menuId": 45,
    "category": "Cocktail",
    "name": "Mojito",
    "description": "Bạc hà, chanh, rum trắng",
    "price": 120000.00,
    "imageUrl": "https://cdn.musiclounge.vn/fnb/mojito.jpg",
    "isAvailable": true,
    "displayOrder": 1
  }
]
```
**Other status codes**: none — unknown `menuId` returns `data: []`.

---

### GET /api/v1/fnb-menu-items/{id}
**Auth**: AllowAnonymous
**Route params**: `id` (int) — menu item id.
**Response 200**: single `FnbMenuItemDto` (shape above), regardless of `IsAvailable` — no availability filter on the by-id read, same pattern as menus.
**Other status codes**: 404 if not found.

---

### POST /api/v1/fnb-menu-items
**Auth**: RequireOwner (role gate) + handler requires `lounge.OwnerId == currentUser.UserId` where `lounge` is resolved via `item.MenuId → menu.LoungeId`, no Admin bypass.
**Request body** — `CreateMenuItemCommand`:
- `menuId` (number, int) — required, `> 0`, **and must reference an existing `FnbMenu`** — validated via an async FluentValidation rule (`MustAsync`) that queries the DB. If it doesn't exist, this is a **400** with `errors.MenuId: ["MenuId không tồn tại."]` — unlike `CreateFnbMenuCommand.loungeId`, which has no such check and instead 404s from the handler. Different failure mode for a superficially similar field — don't assume consistency.
- `category` (string) — required, `NotEmpty`, **max length 50** (not 255 — DB column is `HasMaxLength(50)`, comment in the validator explicitly calls out this was previously mismatched at 100).
- `name` (string) — required, `NotEmpty`, max length 255.
- `description` (string, nullable) — optional, max length 500.
- `price` (decimal) — required, must be `> 0` (`GreaterThan(0)` — zero and negative both rejected).
- `imageUrl` (string, nullable) — optional, max length 500. No URL-format validation, no allowed-scheme check.
- `displayOrder` (number, int) — required by JSON shape, **no validator rule**.
- `isAvailable` (bool) — optional, defaults to `true` if omitted.

Example request:
```json
{
  "menuId": 45,
  "category": "Cocktail",
  "name": "Mojito",
  "description": "Bạc hà, chanh, rum trắng",
  "price": 120000.00,
  "imageUrl": "https://cdn.musiclounge.vn/fnb/mojito.jpg",
  "displayOrder": 1,
  "isAvailable": true
}
```
**Response 201**: `data` is the new item's `id` (int).
**Other status codes**:
- 400 — validation failure, including nonexistent `menuId`.
- 404 — (rare) the menu's parent lounge itself not found.
- 403 — caller doesn't own the venue that owns the menu.
**Notes**: `price` is server-trusted at order time by copying it into `OrderItem.UnitPrice` at the moment an order is placed — see `FnbOrdersController` below. Changing a menu item's price later never retroactively changes past orders.

---

### PUT /api/v1/fnb-menu-items/{id}
**Auth**: RequireOwner (same ownership caveat)
**Route params**: `id` (int) — menu item id.
**Request body** — controller-local `UpdateMenuItemRequest` → `UpdateMenuItemCommand`:
- `category` (string) — required, max length 50.
- `name` (string) — required, max length 255.
- `description` (string, nullable) — optional, max length 500.
- `price` (decimal) — required, `> 0`.
- `imageUrl` (string, nullable) — optional, max length 500.
- `isAvailable` (bool) — required by JSON shape; **no default** — omitting it deserializes to `false` (silently marks the item unavailable). Always send explicitly.
- `displayOrder` (number, int) — required by JSON shape, **no validator rule**, same as Create.

Example request:
```json
{
  "category": "Cocktail",
  "name": "Mojito",
  "description": "Bạc hà, chanh, rum trắng - đã cập nhật giá",
  "price": 130000.00,
  "imageUrl": "https://cdn.musiclounge.vn/fnb/mojito.jpg",
  "isAvailable": true,
  "displayOrder": 1
}
```
**Response 204**: No Content.
**Other status codes**: 400 validation; 404 item/menu/lounge not found; 403 not owner.

---

### DELETE /api/v1/fnb-menu-items/{id}
**Auth**: RequireOwner (same ownership caveat)
**Response 204**: No Content.
**Other status codes**:
- 404 — item/menu/lounge not found.
- 403 — not owner (message "Bạn không có quyền xoá món này.").
- 409 — `ConflictException` if this item was ever ordered (`OrderItem.MenuItemId` reference exists). Message tells the caller to use `isAvailable=false` instead.
**Notes**: `fnb_order_items.menu_item_id` is `ON DELETE RESTRICT` at the DB level, so this 409 pre-check exists to avoid a raw FK-violation 500/409 from EF.

---

## FnbOrdersController
Base route: `api/v1/fnb-orders`. **Controller-level `[Authorize(Policy = Policies.RequireAuthenticated)]`** — every action requires *some* logged-in user (any role); individual actions layer additional checks.

### POST /api/v1/fnb-orders
**Auth**: RequireAuthenticated (any role — this is how an Audience customer places their own order, and also how Staff/Owner/Admin place a counter/dine-in order on a guest's behalf)
**Request body** — `CreateFnbOrderCommand`:
- `loungeId` (number, int) — required, `> 0`, must reference an existing `MusicLounge` (async existence check, else `errors.LoungeId: ["LoungeId không tồn tại."]`, 400).
- `showId` (number, int, nullable) — optional. If present, must reference an existing `LoungeShow` (400 if not) **and** that show must belong to `loungeId` (checked in the handler, not the validator → **422** `DomainException` "Show này không thuộc venue này." if it belongs to a different venue).
- `zoneId` (number, int, nullable) — optional. If present, must reference an existing `SeatingZone` (400 if not) **and** must belong to `loungeId` (handler check → **422** "Khu vực này không thuộc venue này." if mismatched).
- `tableNote` (string, nullable) — optional free text (e.g. `"Bàn A3"`). DB column is `HasMaxLength(100)` but **there is no FluentValidation length rule** — a value over 100 chars is not caught cleanly and will surface as a `DbUpdateException` → generic 409 ("Dữ liệu đã tồn tại hoặc xung đột...") rather than a field-level 400. Keep client-side under 100 chars defensively.
- `paymentMethod` (string) — required, must equal `"Cash"` (case-insensitive). This is **stricter than the underlying `PaymentMethod` enum**, which also has a `"Gateway"` member — `"Gateway"` is explicitly rejected by this validator (message: "PaymentMethod phải là 'Cash' — F&B chưa hỗ trợ thanh toán qua cổng VNPay."). F&B orders never go through the VNPay gateway; only `"Cash"` is accepted at this endpoint.
- `note` (string, nullable) — optional, order-level free text. DB max length 500, again **no FluentValidation rule** — same DbUpdateException risk as `tableNote` if oversized.
- `items` (array, required) — must be non-empty (`NotEmpty`, message "Đơn hàng phải có ít nhất 1 món."). Each element (`OrderItemInput`):
  - `menuItemId` (number, int) — required, `> 0`. Existence/venue-ownership is checked in the **handler**, not the validator: unknown id → 404 `NotFoundException`; id belongs to a menu item outside `loungeId`'s menus → **422** `DomainException` ("Món #{id} không thuộc venue này."); item exists but `IsAvailable == false` → **422** ("Món '{name}' hiện không có sẵn.").
  - `quantity` (number, int) — required, `> 0`, and `<= MaxQuantity` where `MaxQuantity` is read from `system_config` key `fnb_order_item_max_quantity` at request time (default fallback **50** if the config key is absent). Message: "Số lượng mỗi món không được vượt quá {MaxQuantity}." — the actual limit is not a compile-time constant, so if a future config change lowers/raises it, the client should read the number out of the error message rather than hardcoding 50.
  - `note` (string, nullable) — optional, per-line-item text. DB max length 255, **no FluentValidation rule**.

Example request:
```json
{
  "loungeId": 12,
  "showId": 78,
  "zoneId": 5,
  "tableNote": "Bàn A3",
  "paymentMethod": "Cash",
  "note": "Không đá",
  "items": [
    { "menuItemId": 101, "quantity": 2, "note": "Ít đường" },
    { "menuItemId": 108, "quantity": 1, "note": null }
  ]
}
```
**Response 201**: `data` is the new order's `id` (int).
```json
{ "success": true, "data": 501, "message": null }
```
**Other status codes**:
- 400 — FluentValidation failures listed above.
- 403 — `ForbiddenException` if caller's role is `Staff`/`Owner`/`Admin` and `VenueOperatorAccess.CanOperate(currentUser, loungeId, lounge.OwnerId)` is false (i.e., Staff scoped to a different lounge, or Owner who doesn't own this lounge). **Audience callers never hit this check at all** — any authenticated Audience user can place an order against any `loungeId` with no venue-membership requirement (matches the walk-in/at-venue ordering model — presence isn't verified server-side).
- 404 — a `menuItemId` in `items` doesn't exist at all.
- 422 — `DomainException` cases: mismatched `zoneId`/`showId` venue, `menuItemId` belongs to a different venue's menu, or item `IsAvailable == false`. **Not declared in the controller's Swagger attributes**, which list 400/403/404 only.
**Notes**:
- Who the order is attributed to is derived server-side from the JWT, never from the request body: if role is `Staff`/`Owner`/`Admin`, `StaffId = currentUser.UserId` and `AudienceUserId = null`; otherwise (`Audience`), `AudienceUserId = currentUser.UserId` and `StaffId = null`. There is no field in the request to set this explicitly.
- `totalAmount` is always computed server-side as `Σ(menuItem.Price × quantity)` using each item's **current** `Price` at the moment of order creation — the client never supplies a price, so there's no client-side price-tampering surface.
- New orders always start at `status = "Pending"`.
- `unitPrice` is **snapshotted** into each `OrderItem` at creation time (comment in `OrderItem.cs`: "snapshot at order time — never recomputed") — later menu-item price changes do not retroactively affect this order's totals or `orderItem.unitPrice`.

---

### PUT /api/v1/fnb-orders/{id}/status
**Auth**: RequireVenueOperator (role `Staff`/`Owner`/`Admin`) + handler requires `VenueOperatorAccess.CanOperate(currentUser, order.LoungeId, lounge.OwnerId)` (Admin always passes; Staff must be scoped via JWT `lounge_id` claim to this exact lounge; Owner must own this lounge).
**Route params**: `id` (int) — order id.
**Request body** — controller-local `UpdateFnbOrderStatusRequest`:
- `status` (string) — required, must be one of (case-insensitive): `"Pending"`, `"Preparing"`, `"Served"`, `"Paid"`, `"Cancelled"` (validated against a hardcoded allow-list, not literally the C# enum reflection, but it matches every `FnbOrderStatus` member exactly).

Example request:
```json
{ "status": "Preparing" }
```
**Response 204**: No Content.
**Other status codes**:
- 400 — `status` not one of the 5 allowed strings.
- 403 — not an authorized operator for this order's venue.
- 404 — order not found, or its parent lounge not found.
- 422 — `DomainException` for an invalid transition (see state machine below) — **not declared in the controller's Swagger attributes**.
**Notes — order status state machine** (this is the part FE will trip on most):
- The **only** forward path is strictly sequential and single-step: `Pending → Preparing → Served → Paid`. A request must target **exactly the next status** in this sequence from the order's current status — you cannot skip (e.g. `Pending → Served` is rejected), cannot go backward, and cannot re-apply the current status. Rejected transitions throw 422 with message: `"Không thể chuyển từ '{current}' sang '{target}'. Chỉ được chuyển tuần tự Pending → Preparing → Served → Paid."`
- `"Cancelled"` is a **side-exit**, not part of the sequence — it is allowed from **any** status except `Paid` or `Cancelled` itself (i.e., `Pending`, `Preparing`, or `Served` can all cancel directly). Attempting to cancel an already-`Paid` or already-`Cancelled` order → 422 `"Không thể hủy order đang ở trạng thái '{status}'."` Cancelling also sets `cancelled = true` on **every** `OrderItem` under the order (bulk side effect, not reflected as a separate call).
- Transitioning **to** `"Paid"` has a real side effect: the handler creates a `Payment` record server-side (`ReferenceType = "FnbOrder"`, `ReferenceId = order.Id`, `GrossAmount = order.TotalAmount`, `Method = order.PaymentMethod`, `Status = "Confirmed"`) purely as an audit trail — comment in the handler notes this does **not** feed the platform ledger/settlement/commission pipeline (F&B is not a commission product, same as walk-in ticket sales). This Payment record is not returned by any F&B endpoint; it exists for reconciliation/audit only.
- There is **no polling/webhook/SignalR push** wired for order status in this controller or handler — nothing under `FnbOrders` references a Hub. If the FE needs live status updates (e.g. Audience watching their order move from Pending → Served), it must poll `GET /api/v1/fnb-orders/{id}` or `GET /api/v1/fnb-orders/my` itself; there is no push channel today.

---

### GET /api/v1/fnb-orders
**Auth**: RequireAuthenticated (role gate only) — **handler enforces its own narrower check with no Admin bypass** (see Notes).
**Query params**:
- `loungeId` (int, required — missing binds to `0`, which then 404s as a nonexistent lounge).
- `status` (string, optional, default `null`) — filter by `FnbOrderStatus`. **Invalid/unrecognized values are silently ignored** (`Enum.TryParse` failure just leaves the filter unset) rather than erroring — passing `status=bogus` returns the unfiltered list, not a 400.
- `page` (int, default 1).
- `pageSize` (int, default 20, clamped 1-100).

**Response 200**: `data` is `PaginatedResult<FnbOrderDto>` — see `FnbOrderDto` shape under the `GET /{id}` entry below.
**Other status codes**:
- 403 — caller is neither the lounge's `Owner` nor Staff whose JWT `lounge_id` claim equals `loungeId`. **`Admin` is not special-cased here** — an Admin account without a matching `lounge_id` claim (the normal case; Admins don't carry that claim) gets a 403 from this endpoint, unlike the Create/UpdateStatus order endpoints which use `VenueOperatorAccess.CanOperate` and do bypass for Admin. This is an inconsistency versus the rest of the F&B surface — flag if Admin needs to browse all venues' orders.
- 404 — `loungeId` doesn't reference an existing lounge.
**Notes**: This is the Owner/Staff "kitchen/floor view" — lounge-scoped, not self-scoped. See `GET /my` below for the Audience counterpart.

---

### GET /api/v1/fnb-orders/my
**Auth**: RequireAuthenticated (any role — intended for `Audience`, but any authenticated caller can call it; it just returns orders where `AudienceUserId == callingUser.Id`, which will be empty for a Staff/Owner/Admin account that never orders as Audience).
**Query params**: `status` (string, optional, same silent-invalid-ignore behavior as above), `page` (default 1), `pageSize` (default 20, clamped 1-100).
**Response 200**: `data` is `PaginatedResult<FnbOrderDto>`.
**Other status codes**: none beyond 200 — always returns a (possibly empty) page, no 403/404.
**Notes**: This is the endpoint the ordering customer uses to check their own order's status after placing it — previously this had **no counterpart at all** (only the Owner/Staff lounge-scoped list existed, per the comment left in the controller: *"the placing customer got a hard 403 from GetByLounge"*), so this is a relatively new addition.

---

### GET /api/v1/fnb-orders/{id}
**Auth**: RequireAuthenticated (role gate only) — handler does its own resource check, **no Admin bypass** (same caveat as the lounge-scoped list above).
**Route params**: `id` (int) — order id.
**Response 200**: `data` is a single `FnbOrderDto`:
- `id` (int)
- `loungeId` (int)
- `showId` (int, nullable)
- `audienceUserId` (int, nullable) — set when an Audience user placed the order, else `null`.
- `staffId` (int, nullable) — set when Staff/Owner/Admin placed the order on a guest's behalf, else `null`.
- `tableNote` (string, nullable)
- `status` (string) — one of `Pending` / `Preparing` / `Served` / `Paid` / `Cancelled`.
- `paymentMethod` (string) — one of `Gateway` / `Cash` (the `PaymentMethod` enum has both members, but in practice this will always be `"Cash"` since `CreateFnbOrder` rejects `"Gateway"` — see above).
- `totalAmount` (decimal)
- `note` (string, nullable)
- `createdAt` (DateTimeOffset, ISO-8601) — **note: the entity also has `UpdatedAt`, but it is not exposed anywhere in `FnbOrderDto`** — FE cannot see when a status last changed via this API, only `createdAt`.
- `items` (array of `OrderItemDto`):
  - `id` (int)
  - `menuItemId` (int)
  - `menuItemName` (string) — denormalized snapshot resolved by joining to `FnbMenuItem` at read time (not stored on the order item itself); renders as the literal string `"(deleted)"` if the menu item no longer exists. In practice this should be unreachable in normal use, since `DeleteMenuItemCommand` refuses to delete any item that was ever ordered — but it is still defensively coded.
  - `quantity` (int)
  - `unitPrice` (decimal) — snapshotted at order-creation time, independent of the menu item's current price.
  - `cancelled` (bool) — per-line cancel flag, set `true` for every item when the whole order transitions to `Cancelled`.
  - `note` (string, nullable)

**Important gap**: `FnbOrderDto` does **not** include `zoneId` anywhere, even though `CreateFnbOrderCommand` accepts it and it's persisted on the `FnbOrder` entity. FE cannot read back which seating zone an order was placed for through any F&B endpoint — only the free-text `tableNote` survives to the response.

Example `data`:
```json
{
  "id": 501,
  "loungeId": 12,
  "showId": 78,
  "audienceUserId": 33,
  "staffId": null,
  "tableNote": "Bàn A3",
  "status": "Pending",
  "paymentMethod": "Cash",
  "totalAmount": 360000.00,
  "note": "Không đá",
  "createdAt": "2026-08-17T20:15:00+07:00",
  "items": [
    {
      "id": 900,
      "menuItemId": 101,
      "menuItemName": "Mojito",
      "quantity": 2,
      "unitPrice": 120000.00,
      "cancelled": false,
      "note": "Ít đường"
    },
    {
      "id": 901,
      "menuItemId": 108,
      "menuItemName": "Khoai tây chiên",
      "quantity": 1,
      "unitPrice": 120000.00,
      "cancelled": false,
      "note": null
    }
  ]
}
```
**Other status codes**:
- 403 — caller is none of: the placing Audience user (`order.AudienceUserId == callingUser.Id`), Staff scoped to `order.LoungeId` via JWT claim, or the lounge's Owner. **No Admin bypass** — same inconsistency as `GET /fnb-orders` above.
- 404 — order id not found.

Example full paginated envelope (`GET /fnb-orders` or `GET /fnb-orders/my`):
```json
{
  "success": true,
  "data": {
    "items": [ { "id": 501, "loungeId": 12, "...": "FnbOrderDto as above" } ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 47,
    "totalPages": 3,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "message": null
}
```

---

## Part 6 — Livestream & Social

> Phân trang có 2 lớp clamp: lớp filter chung (`[1,100]`) rồi nhiều handler tự siết chặt hơn — xem ghi chú từng endpoint danh sách.

## LivestreamsController

Base route: `api/v1/livestreams`. No controller-level `[Authorize]` — every action declares its own policy.

### GET /api/v1/livestreams/{id}
**Auth**: RequireAuthenticated
**Route params**: `id` (int) — Livestream.Id (NOT the show id).
**Query params**: none.
**Request body**: none.
**Response 200**: `LivestreamDetailDto`
- `id` (number) — Livestream.Id
- `loungeShowId` (number) — the LoungeShow this stream belongs to
- `showName` (string) — LoungeShow.Name
- `status` (string enum) — `LivestreamStatus`: one of `Scheduled` | `Live` | `Ended` | `Terminated`
- `hlsUrl` (string | null) — **only populated if the caller has access** (see Notes); otherwise `null` even while `Live`
- `viewerCount` (number, int) — live count maintained by `LivestreamHub` connect/disconnect
- `startedAt` (string, ISO-8601 | null)
- `endedAt` (string, ISO-8601 | null)
- `terminatedReason` (string | null) — set only when `status = Terminated`
- `userHasAccess` (boolean) — whether `hlsUrl` was allowed to be populated for this caller

Example:
```json
{
  "success": true,
  "data": {
    "id": 15,
    "loungeShowId": 42,
    "showName": "Jazz Night Vol. 3",
    "status": "Live",
    "hlsUrl": "https://stream.mux.com/abc123.m3u8",
    "viewerCount": 128,
    "startedAt": "2026-08-17T19:05:00+07:00",
    "endedAt": null,
    "terminatedReason": null,
    "userHasAccess": true
  },
  "message": null
}
```
**Other status codes**: `404` if the Livestream id doesn't exist (`NotFoundException`).
**Notes**:
- Access rule (`LivestreamAccessPolicy.EvaluateAsync`, source `src/MusicLounge.Application/Livestreams/Common/LivestreamAccessPolicy.cs`): `userHasAccess = true` when the caller is **Admin**, OR is a Staff/Owner who operates **this exact venue** (`VenueOperatorAccess.CanOperate` — Staff must have the matching `lounge_id` JWT claim; Owner must own the lounge), OR is a genuine ticket holder with `HasViewerAccessAsync` returning true (i.e. holds a confirmed Livestream-tier ticket with a `LivestreamTicketDetail.AccessToken`).
- A **genuine ticket-holder** view (not an operator/Admin monitoring) fires a background job (`EnqueueLogUserBehaviour` with `BehaviourAction.WatchLivestream`) that feeds the recommendation engine — hitting this endpoint as Staff/Owner/Admin does NOT count as a watch signal.
- Even when `status = Live`, `hlsUrl` is `null` for a caller without access — FE must not assume "Live" implies a playable URL.

### POST /api/v1/livestreams
**Auth**: RequireVenueOperator (Staff of the venue with matching `lounge_id` claim, Owner of the venue, or Admin)
**Route params**: none.
**Query params**: none.
**Request body**: `CreateLivestreamCommand`
- `showId` (number, int) — required, must be `> 0` and reference an existing `LoungeShow` (validated async against the DB by `CreateLivestreamCommandValidator`).

Example:
```json
{ "showId": 42 }
```
**Response 201**: `data` is a plain **int** — the new `Livestream.Id`. `Location` header points to `GET /api/v1/livestreams/{id}` (via `CreatedAtAction`).
```json
{ "success": true, "data": 15, "message": null }
```
**Other status codes**:
- `400` — `showId` missing/≤0 or doesn't exist (FluentValidation).
- `403` — caller doesn't operate this show's venue (`ForbiddenException`).
- `422` — show `Format = Online`... wait, actually: show's `Format = Offline` ("Offline shows cannot have a livestream"), OR show `Status` is `Cancelled`/`Ended` (`DomainException`).
- `409` — a Livestream already exists for this `showId` (`ConflictException`).
- `404` — `showId`/lounge not found.
**Notes**:
- Concurrency-safe: acquires a per-`showId` async keyed lock before checking "does a livestream already exist" so two simultaneous calls for the same show can't both create one.
- Livestream is created in `Scheduled` status with a fresh `EventModeration` row (Admin must approve before `POST .../start` will succeed — see W08 moderation flow) and enqueues an AI moderation-scoring background job.
- Draft/Pending shows ARE allowed (a livestream must exist before the show is even submitted for approval) — only `Cancelled`/`Ended` shows are blocked.
- `Provider` (Mux vs Cloudflare) is decided server-side from config (`Livestream:Provider` appsetting, default `"cloudflare"`) — not client-controlled.

### POST /api/v1/livestreams/{id}/start
**Auth**: RequireVenueOperator
**Route params**: `id` (int) — Livestream.Id.
**Request body**: none.
**Response 204**: no body.
**Other status codes**:
- `404` — livestream/show/lounge not found.
- `403` — caller doesn't operate this venue.
- `422` — current `status != Scheduled`, OR the moderation record is missing/not yet `AdminDecision = Approved`, OR `LoungeShow.VcpmcRoyaltyReference` is empty (VCPMC copyright fee not declared as paid) — all `DomainException`.
**Notes**:
- On success: `Livestream.Status → Live`, `StartedAt` set; `LoungeShow.Status → Ongoing`, `ActualStart` set.
- Backfills `LivestreamTicketDetail` (with a fresh `AccessToken`) for any already-confirmed Livestream-tier tickets that predate the stream starting.
- Sends an in-app `NotificationType.EventLive` notification to every confirmed ticket holder AND every follower of the venue's lounge (via `INotificationService`, not SignalR) — see NotificationsController below for how FE reads these.
- **Must be approved by Admin first** (separate Admin moderation flow, not in this doc) and **VCPMC royalty reference must be set on the show** (also not exposed by a controller in this doc) before this call will succeed — a common source of a surprise 422 for FE testing against fresh seed data.

### POST /api/v1/livestreams/{id}/end
**Auth**: RequireVenueOperator
**Route params**: `id` (int) — Livestream.Id (note: **body/route param name is `id` but the command field is `LivestreamId`** — same value).
**Request body**: none.
**Response 204**: no body.
**Other status codes**:
- `404` — livestream/show/lounge not found.
- `403` — caller doesn't operate this venue.
- `422` — current `status != Live` (`DomainException`).
**Notes**:
- On success: `Livestream.Status → Ended`, `EndedAt` set, `ViewerCount` reset to `0`; `LoungeShow.Status → Ended`, `ActualEnd` set, and `RatingOpenUntil = now + RatingWindowDays` (config-driven, default 7 days) — this is what opens the post-show rating window.
- Best-effort deletes the stream on the external provider (Mux/Cloudflare); failure there is only logged, never surfaces to the caller.

### GET /api/v1/livestreams/{id}/credentials
**Auth**: RequireVenueOperator
**Route params**: `id` (int) — Livestream.Id.
**Request body**: none.
**Response 200**: `LivestreamCredentialsDto` — **sensitive, never exposed to viewers**.
- `id` (number) — Livestream.Id
- `provider` (string | null) — raw config string, e.g. `"cloudflare"` or `"mux"` (this is a plain settings string, NOT a `JsonStringEnumConverter`-backed C# enum, so don't expect PascalCase)
- `rtmpUrl` (string | null) — RTMP ingest URL to plug into OBS/streaming software
- `streamKey` (string | null) — stream key, paired with `rtmpUrl`

Example:
```json
{
  "success": true,
  "data": {
    "id": 15,
    "provider": "mux",
    "rtmpUrl": "rtmp://global-live.mux.com:5222/app",
    "streamKey": "abcd1234-efgh-live-stream-key"
  },
  "message": null
}
```
**Other status codes**: `404` livestream/show/lounge not found; `403` caller doesn't operate this venue.
**Notes**: response has `Cache-Control: no-store` (`[ResponseCache(NoStore = true)]`) — never cache this client-side.

### GET /api/v1/livestreams/{id}/chat
**Auth**: RequireAuthenticated (further access-gated inside the handler, see Notes)
**Route params**: `id` (int) — Livestream.Id.
**Query params**: `page` (int, default `1`), `pageSize` (int, default `50`) — global filter clamps `pageSize` to `[1,100]`; handler re-clamps to the same `[1,100]` (no further tightening here).
**Request body**: none.
**Response 200**: `PaginatedResult<ChatMessageDto>`
- `items[]`:
  - `messageId` (number) — `LivestreamChatMessage.Id`
  - `userId` (number)
  - `displayName` (string) — sender's `User.FullName`
  - `message` (string)
  - `sentAt` (string, ISO-8601)
- `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`

Example:
```json
{
  "success": true,
  "data": {
    "items": [
      { "messageId": 301, "userId": 8, "displayName": "Nguyễn Văn A", "message": "Hay quá!", "sentAt": "2026-08-17T19:10:22+07:00" }
    ],
    "page": 1, "pageSize": 50, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**:
- `404` — livestream/show/lounge not found (when caller isn't Admin, the handler must resolve the venue to check operator scope).
- `403` — `"Bạn cần có vé livestream để xem nội dung này."` when the caller is neither Admin, an operator of this exact venue, nor a genuine ticket-holding viewer (`ForbiddenException`).
**Notes**: uses the same access rule family as `GetLivestreamDetail`/`LivestreamAccessPolicy` (Admin bypass, venue-scoped Staff/Owner, or ticket-holder) — this was previously a cross-venue leak (any Staff/Admin account could read any venue's paid chat) and has been fixed to scope by venue.

### POST /api/v1/livestreams/{id}/terminate
**Auth**: RequireAdmin
**Route params**: `id` (int) — Livestream.Id.
**Request body**: `TerminateLivestreamRequest` (a controller-local record, NOT the MediatR command directly)
- `reason` (string) — required, `NotEmpty`, `MaximumLength(1000)`. Free-text moderation reason.

Example:
```json
{ "reason": "Vi phạm nội dung theo Nghị định 147/2024 — nội dung phản cảm." }
```
**Response 204**: no body.
**Other status codes**:
- `404` — livestream not found.
- `422` — current `status != Live` ("Chỉ có thể terminate livestream đang phát sóng (status = Live)." — `DomainException`).
- `400` — `reason` empty or > 1000 chars (FluentValidation).
**Notes**:
- W22 — Admin-only forced stop for policy violations. Sets `Status → Terminated`, `EndedAt`, `TerminatedById = <admin's userId>`, `TerminatedReason = reason`.
- Also flips the owning `LoungeShow.Status → Ended` (a terminated-but-happened show still opens the rating window, same `RatingWindowDays` logic as a normal `end`).
- Broadcasts `LivestreamTerminated` over the `/hubs/livestream` SignalR group so connected viewers stop retrying the HLS URL — see SignalR Hubs section below.
- Best-effort provider stream deletion; failures are swallowed (not even logged beyond a warning trail already covered by the audit log line).

---

## FollowsController

Base route: `api/v1/follows`. Controller-level `[Authorize(Policy = Policies.RequireAuthenticated)]` — applies to all 3 actions.

### GET /api/v1/follows/lounges
**Auth**: RequireAuthenticated
**Query params**: `page` (int, default `1`), `pageSize` (int, default `20`) — global filter clamps to `[1,100]`, handler re-clamps `pageSize` to `[1,50]` (effective max is **50**, not 100).
**Request body**: none.
**Response 200**: `PaginatedResult<FollowedLoungeDto>`
- `items[]`:
  - `id` (number) — `MusicLounge` (venue) id
  - `name` (string)
  - `primaryImageUrl` (string | null)
  - `district` (string)
  - `city` (string)
  - `followedAt` (string, ISO-8601) — when the caller followed this lounge
- `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`

Example:
```json
{
  "success": true,
  "data": {
    "items": [
      { "id": 3, "name": "Cafe Số Đỏ", "primaryImageUrl": "https://cdn.example/venues/3/cover.jpg", "district": "Quận 1", "city": "TP. Hồ Chí Minh", "followedAt": "2026-07-01T10:00:00+07:00" }
    ],
    "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: `401` if unauthenticated.

### POST /api/v1/follows/lounges/{loungeId}
**Auth**: RequireAuthenticated
**Route params**: `loungeId` (int) — `MusicLounge.Id`.
**Request body**: none.
**Response 204**: no body.
**Other status codes**:
- `404` — lounge id doesn't exist (`NotFoundException`).
- `409` — already following this lounge (`"Bạn đã theo dõi phòng trà này."`, `ConflictException`).
- `400` — `loungeId ≤ 0` (validator; unreachable via the `{loungeId:int}` route constraint from negative-but-valid ints, but `0` or route-bypass scenarios still validate).

### DELETE /api/v1/follows/lounges/{loungeId}
**Auth**: RequireAuthenticated
**Route params**: `loungeId` (int).
**Request body**: none.
**Response 204**: no body.
**Other status codes**: `404` — no existing follow row for `(caller, loungeId)` (`NotFoundException`).
**Notes**: idempotent is NOT guaranteed here — unfollowing something you don't follow is a 404, not a silent no-op (unlike `UnregisterDeviceToken` below, which is intentionally silent).

---

## WishlistController

Base route: `api/v1/wishlist`. Controller-level `[Authorize(Policy = Policies.RequireAuthenticated)]`.

### GET /api/v1/wishlist
**Auth**: RequireAuthenticated
**Query params**: `page` (int, default `1`), `pageSize` (int, default `10`) — global filter clamps to `[1,100]`, handler re-clamps to `[1,100]` (no further tightening; effective max **100**).
**Request body**: none.
**Response 200**: `PaginatedResult<LoungeShowListItemDto>`
- `items[]`:
  - `id` (number) — `LoungeShow.Id`
  - `name` (string)
  - `coverImageUrl` (string | null)
  - `loungeName` (string)
  - `loungeDistrict` (string)
  - `loungeCity` (string)
  - `scheduledStart` (string, ISO-8601)
  - `format` (string enum) — `LoungeShowFormat`: `Offline` | `Online`
  - `status` (string enum) — `LoungeShowStatus`: `Draft` | `Pending` | `Published` | `Ongoing` | `Ended` | `Cancelled`
  - `minPrice` (number | null) — decimal
  - `maxPrice` (number | null) — decimal
  - `genres[]` — `{ "id": number, "name": string }`
  - `performerNames[]` (string[])
  - `offlineQuota` (number | null, int) — remaining/total offline capacity, if applicable
  - `onlineQuota` (number | null, int)
  - `isWishlisted` (boolean | null) — **always `true`** on this endpoint (the query hard-codes it since every item returned IS the caller's wishlist)
- `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`

Example:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 42, "name": "Jazz Night Vol. 3", "coverImageUrl": "https://cdn.example/shows/42/cover.jpg",
        "loungeName": "Cafe Số Đỏ", "loungeDistrict": "Quận 1", "loungeCity": "TP. Hồ Chí Minh",
        "scheduledStart": "2026-09-01T19:00:00+07:00", "format": "Offline", "status": "Published",
        "minPrice": 150000, "maxPrice": 500000,
        "genres": [{ "id": 2, "name": "Jazz" }],
        "performerNames": ["Trần Thị B"],
        "offlineQuota": 80, "onlineQuota": null, "isWishlisted": true
      }
    ],
    "page": 1, "pageSize": 10, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: `401` if unauthenticated.

### POST /api/v1/wishlist/{showId}
**Auth**: RequireAuthenticated
**Route params**: `showId` (int) — `LoungeShow.Id`.
**Request body**: none.
**Response 204**: no body.
**Other status codes**:
- `404` — show doesn't exist, OR exists but is `Draft`/`Cancelled` (both treated as "not found" — `NotFoundException`, since those aren't publicly wishlist-able states).
- `409` — already wishlisted (`"LoungeShow đã có trong danh sách yêu thích."`, `ConflictException`).
- `401` unauthenticated.
- `400` — `showId ≤ 0`.

### DELETE /api/v1/wishlist/{showId}
**Auth**: RequireAuthenticated
**Route params**: `showId` (int).
**Request body**: none.
**Response 204**: no body.
**Other status codes**: `404` — no wishlist entry for `(caller, showId)`; `401` unauthenticated.

---

## RecommendationsController

Base route: `api/v1/recommendations`. Controller-level `[Authorize(Policy = Policies.RequireAuthenticated)]`.

### GET /api/v1/recommendations
**Auth**: RequireAuthenticated
**Query params**: `limit` (int, default `10`) — global filter clamps `limit` to `[1,100]`, handler re-clamps to `[1,50]` (effective max **50**).
**Request body**: none.
**Response 200**: `data` is a **plain array** (NOT a `PaginatedResult` — no `items`/`page` wrapper), `IReadOnlyList<RecommendedLoungeShowDto>`.
- `id` (number) — `LoungeShow.Id`
- `name` (string)
- `coverImageUrl` (string | null)
- `loungeName` (string)
- `loungeDistrict` (string)
- `loungeCity` (string)
- `scheduledStart` (string, ISO-8601)
- `format` (string enum) — `LoungeShowFormat`: `Offline` | `Online`
- `status` (string enum) — `LoungeShowStatus`: `Draft` | `Pending` | `Published` | `Ongoing` | `Ended` | `Cancelled`
- `minPrice` (number | null)
- `maxPrice` (number | null)
- `genres[]` — `{ "id": number, "name": string }`
- `performerNames[]` (string[])
- `recommendationScore` (number, float) — `0` when the item is a trending fallback (see Notes)
- `recommendationReason` (string) — either the AI-generated reason string, or the literal fallback string `"Đang thịnh hành"`

Example:
```json
{
  "success": true,
  "data": [
    {
      "id": 55, "name": "Acoustic Sundays", "coverImageUrl": "https://cdn.example/shows/55/cover.jpg",
      "loungeName": "Blue Note Saigon", "loungeDistrict": "Quận 3", "loungeCity": "TP. Hồ Chí Minh",
      "scheduledStart": "2026-08-24T19:00:00+07:00", "format": "Offline", "status": "Published",
      "minPrice": 200000, "maxPrice": 600000,
      "genres": [{ "id": 5, "name": "Acoustic" }],
      "performerNames": ["Lê Văn C"],
      "recommendationScore": 0.87,
      "recommendationReason": "Vì bạn từng xem show Jazz tương tự"
    }
  ],
  "message": null
}
```
**Other status codes**: `401` unauthenticated.
**Notes** (source: `GetRecommendedLoungeShowsQueryHandler`):
- Falls back to a **trending-shows list** (`recommendationScore = 0`, `recommendationReason = "Đang thịnh hành"`) in three cases: the user record is missing, `User.AiConsent = false`, or there are no non-expired cached `AiRecommendation` rows for the user.
- When the cache is empty (but consent is given), the handler enqueues a background recommendation-refresh job (`EnqueueRecommendationRefresh`) and STILL returns the trending fallback for this call — i.e. the first call after consent/cache-expiry never returns personalized results; a subsequent call (after the background job completes) will.
- Personalized results are sorted by `AiRecommendation.FinalScore` descending, then truncated to `limit`.

---

## NotificationsController

Base route: `api/v1/notifications`. Controller-level `[Authorize(Policy = Policies.RequireAuthenticated)]`. W23 in-app notification inbox.

### GET /api/v1/notifications
**Auth**: RequireAuthenticated
**Query params**: `page` (int, default `1`), `pageSize` (int, default `20`) — global filter clamps to `[1,100]`, handler re-clamps to `[1,50]` (effective max **50**).
**Request body**: none.
**Response 200**: `PaginatedResult<NotificationDto>`
- `items[]`:
  - `id` (number)
  - `type` (string enum) — `NotificationType`, exact allowed values (source `src/MusicLounge.Domain/Enums/NotificationType.cs`):
    `TicketConfirmed`, `EventReminder`, `EventRescheduled`, `EventCancelled`, `EventFormatChanged`, `EventLive`, `NewEvent`, `WishlistLowStock`, `DonationReceived`, `DonationConfirmed`, `DonationPending`, `SettlementReleased`, `ModerationResult`, `PenaltyWarning`, `PenaltyIssued`, `AppealResolved`, `VenueApproved`, `VenueRejected`, `RefundRequiresManualTransfer`, `SettlementSchedulingBlocked`, `DonationRefunded`, `LedgerIntegrityIssueDetected`, `ComplaintUpdate`, `SubscriptionExpiring`, `DuplicatePaymentDetected`, `ModerationSlaBreached`, `SecurityAlert`, `SystemHealthAlert`, `PaymentReconciliationMismatch`
  - `title` (string)
  - `body` (string)
  - `referenceType` (string | null) — e.g. `"show"`; a free-text discriminator FE can switch on to build a deep link
  - `referenceId` (string | null) — the id of whatever `referenceType` points to (stored as a string even when the underlying id is numeric/GUID)
  - `isRead` (boolean)
  - `createdAt` (string, ISO-8601)
- `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`

Example:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 901, "type": "EventLive", "title": "Đang phát trực tiếp!",
        "body": "\"Jazz Night Vol. 3\" đang livestream ngay bây giờ.",
        "referenceType": "show", "referenceId": "42",
        "isRead": false, "createdAt": "2026-08-17T19:05:01+07:00"
      }
    ],
    "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1,
    "hasNextPage": false, "hasPreviousPage": false
  },
  "message": null
}
```
**Other status codes**: `401` unauthenticated. No documented explicit ordering guarantee in the handler signature itself — assume newest-first (verify against `INotificationRepository.GetMyNotificationsAsync` implementation if exact order matters for FE).

### POST /api/v1/notifications/{id}/read
**Auth**: RequireAuthenticated
**Route params**: `id` (int) — `Notification.Id`.
**Request body**: none.
**Response 204**: no body.
**Other status codes**:
- `404` — notification id doesn't exist.
- `403` — notification belongs to a different user (`"Thông báo này không thuộc về bạn."`, `ForbiddenException`).
**Notes**: idempotent — calling again on an already-read notification is a silent no-op (no DB write, still 204).

### POST /api/v1/notifications/read-all
**Auth**: RequireAuthenticated
**Request body**: none.
**Response 204**: no body.
**Other status codes**: none beyond `401`.
**Notes**: bulk-marks every notification belonging to the caller as read via `INotificationRepository.MarkAllAsReadAsync` — no per-item response, so FE should just clear all "unread" badges optimistically after a 204.

### POST /api/v1/notifications/device-tokens
**Auth**: RequireAuthenticated
**Request body**: `RegisterDeviceTokenCommand`
- `fid` (string) — required, `NotEmpty`, `MaximumLength(450)`. This is a **Firebase Installation ID** (Fid) from the client-side Firebase Installations SDK's `onRegistered()` flow — NOT the legacy FCM `getToken()` registration token (deprecated as of FirebaseAdmin .NET SDK 3.6.0).

Example:
```json
{ "fid": "fis-abcdef1234567890" }
```
**Response 204**: no body.
**Other status codes**: `400` — `fid` empty or > 450 chars.
**Notes**:
- Idempotent upsert keyed on `fid` (unique per device install, not per user — see `DeviceTokenConfiguration`). If the same `fid` was previously registered to a different account on the same physical device (e.g. logout → login as someone else), it is **re-pointed** to the current caller rather than duplicated.
- Call this right after login, and again whenever Firebase re-registers the installation.

### DELETE /api/v1/notifications/device-tokens
**Auth**: RequireAuthenticated
**Request body**: `UnregisterDeviceTokenCommand`
- `fid` (string) — required, `NotEmpty`, `MaximumLength(450)`.

Example:
```json
{ "fid": "fis-abcdef1234567890" }
```
**Response 204**: no body.
**Other status codes**: `400` — `fid` empty or > 450 chars. **No 404** — deliberately silent no-op if the `(fid, caller)` pair doesn't exist (so logout never fails just because the token was already cleaned up).
**Notes**: scoped to `(fid, callerUserId)` — a caller cannot unregister another user's device token even if they somehow know its `fid`. Call this on logout/sign-out.

---

## AnalyticsController

Base route: `api/v1/analytics`. No controller-level `[Authorize]` — CF7 reporting dashboards, per-action policy. Neither action is paginated.

### GET /api/v1/analytics/my-lounge
**Auth**: RequireOwner
**Query params**: `loungeId` (int) — **required**, no default; identifies which of the Owner's venues to report on.
**Request body**: none.
**Response 200**: `OwnerAnalyticsDto`
- `totalShows` (number, int) — all shows ever created for this lounge
- `upcomingShows` (number, int) — `ScheduledStart > now` AND `Status` in `Published`/`Pending`/`Draft`
- `pastShows` (number, int) — `Status` in `Ended`/`Cancelled`, OR `ScheduledStart <= now`
- `totalTicketsSold` (number, int) — confirmed tickets only
- `offlineTicketsSold` (number, int) — confirmed tickets whose tier `AccessType = Physical`
- `onlineTicketsSold` (number, int) — confirmed tickets whose tier `AccessType != Physical`
- `totalRevenue` (number, decimal) — `ticketRevenue + fnbRevenue`
- `ticketRevenue` (number, decimal)
- `fnbRevenue` (number, decimal) — sum of `FnbOrder.TotalAmount` where `Status = Paid`
- `averageRating` (number | null, decimal) — rounded to 2 decimals; `null` if zero ratings (excludes `IsRemoved` ratings)
- `totalRatings` (number, int)
- `pendingArtistPayoutCount` (number, int) — donations in `Status = OwnerReceived` (owner acked, not yet paid to performer) across the lounge's performances
- `pendingArtistPayoutAmount` (number, decimal) — sum of `Donation.Net` for those same rows
- `revenueTrend[]` — always exactly **6 entries**, oldest-to-newest calendar month (VN local time, UTC+7), each:
  - `year` (number, int)
  - `month` (number, int, 1-12)
  - `fnbRevenue` (number, decimal)
  - `offlineTicketRevenue` (number, decimal)
  - `onlineTicketRevenue` (number, decimal)
- `topShows[]` — top 5 by revenue, descending, each:
  - `showId` (number)
  - `name` (string)
  - `scheduledStart` (string, ISO-8601)
  - `mainPerformerName` (string | null) — the `Performance` with `Role = Main`, if any
  - `ticketsSold` (number, int)
  - `totalCapacity` (number | null, int) — sum of tier capacities; `null` if `0`/unset
  - `averageRating` (number | null, decimal) — rounded to 2 decimals; `null` if no ratings
  - `revenue` (number, decimal)

Example:
```json
{
  "success": true,
  "data": {
    "totalShows": 12, "upcomingShows": 3, "pastShows": 9,
    "totalTicketsSold": 540, "offlineTicketsSold": 400, "onlineTicketsSold": 140,
    "totalRevenue": 125000000, "ticketRevenue": 98000000, "fnbRevenue": 27000000,
    "averageRating": 4.62, "totalRatings": 87,
    "pendingArtistPayoutCount": 2, "pendingArtistPayoutAmount": 3500000,
    "revenueTrend": [
      { "year": 2026, "month": 3, "fnbRevenue": 4000000, "offlineTicketRevenue": 12000000, "onlineTicketRevenue": 2000000 },
      { "year": 2026, "month": 4, "fnbRevenue": 4200000, "offlineTicketRevenue": 13000000, "onlineTicketRevenue": 1800000 },
      { "year": 2026, "month": 5, "fnbRevenue": 4500000, "offlineTicketRevenue": 14000000, "onlineTicketRevenue": 2200000 },
      { "year": 2026, "month": 6, "fnbRevenue": 4700000, "offlineTicketRevenue": 15500000, "onlineTicketRevenue": 2500000 },
      { "year": 2026, "month": 7, "fnbRevenue": 4900000, "offlineTicketRevenue": 16000000, "onlineTicketRevenue": 2600000 },
      { "year": 2026, "month": 8, "fnbRevenue": 4700000, "offlineTicketRevenue": 17500000, "onlineTicketRevenue": 2400000 }
    ],
    "topShows": [
      { "showId": 42, "name": "Jazz Night Vol. 3", "scheduledStart": "2026-09-01T19:00:00+07:00", "mainPerformerName": "Trần Thị B", "ticketsSold": 80, "totalCapacity": 100, "averageRating": 4.8, "revenue": 20000000 }
    ]
  },
  "message": null
}
```
**Other status codes**:
- `404` — `loungeId` doesn't exist.
- `403` — caller doesn't own this lounge (`lounge.OwnerId != callerId` — **note: this is Owner-of-record only, NOT the broader `VenueOperatorAccess`/Staff pattern used elsewhere** — Staff cannot call this endpoint at all regardless of assignment).
**Notes**: month-boundary bug class already fixed here — all date bucketing converts UTC-stored timestamps to VN local (UTC+7) before extracting year/month, so a ticket bought at 00:30 VN time lands in the correct calendar month rather than the previous UTC day's month.

### GET /api/v1/analytics/platform
**Auth**: RequireAdmin
**Query params**: none.
**Request body**: none.
**Response 200**: `PlatformAnalyticsDto`
- `totalVenues` (number, int) — all `MusicLounge` rows, unfiltered by approval status
- `totalPublishedShows` (number, int) — `Status` in `Published`/`Ongoing`/`Ended`
- `totalUsers` (number, int) — all `User` rows
- `totalTicketsSold` (number, int) — confirmed tickets, both online (`TicketHold`) and walk-in/box-office (`WalkIn`) channels
- `totalGrossMerchandiseValue` (number, decimal) — sum of `Payment.GrossAmount` where `Status = Confirmed` and `ReferenceType` in `TicketHold`/`WalkIn` (deliberately the same two channels as `totalTicketsSold`, so the two numbers never contradict each other)
- `totalDonationVolume` (number, decimal) — sum of `Donation.Gross` excluding `PendingPayment`/`Cancelled` statuses
- `pendingModerationsCount` (number, int) — `EventModeration` rows with `AdminDecision = null` (undecided)

Example:
```json
{
  "success": true,
  "data": {
    "totalVenues": 34, "totalPublishedShows": 210, "totalUsers": 5600,
    "totalTicketsSold": 12500, "totalGrossMerchandiseValue": 2100000000,
    "totalDonationVolume": 95000000, "pendingModerationsCount": 4
  },
  "message": null
}
```
**Other status codes**: none beyond `403` (non-Admin caller rejected by the policy itself, before the handler runs).

---

## SignalR Hubs

### /hubs/livestream
**Auth**: required (`[Authorize]` on the hub class — same JWT bearer as REST, but for the WebSocket handshake it MUST be passed as a query string parameter: `wss://.../hubs/livestream?livestreamId=15&access_token=<token>`, not the `Authorization` header — this is a hard requirement of the browser WebSocket API, verified in `Program.cs`: `OnMessageReceived` reads `context.Request.Query["access_token"]` specifically for paths starting with `/hubs`).
**Connection query params**: `livestreamId` (int) — **required**; the hub aborts the connection (`Context.Abort()`) if missing/unparseable, or if the caller fails the same access check used by `GET /livestreams/{id}` (Admin bypass, venue-operator bypass, or genuine ticket holder via `HasViewerAccessAsync`).
**Session cap**: a genuine ticket holder (not an operator/Admin) is limited to `LivestreamSettings.MaxConcurrentLivestreamSessionsPerTicket` concurrent connections for the same `(livestreamId, userId)` — exceeding it aborts the new connection. Operators/Admin are exempt (legitimately monitor from multiple devices/tabs).

**Client → server hub methods** (invoke via `connection.invoke("MethodName", ...)`):
- `SendMessage(message: string)` — sends a chat message as the connected user. Server-side validation: `NotEmpty`, `MaximumLength(500)`; also requires `Livestream.Status = Live` and passes the per-user chat rate limiter (**1 message / 2 seconds**, source comment: "§6.10"). On any business-rule violation (`DomainException`/`NotFoundException`/`ForbiddenException`/`ConflictException`/`UnauthorizedException`) the hub re-throws as a `HubException` carrying the same Vietnamese user-facing message the REST API would return — catch this client-side (e.g. `connection.on` doesn't catch it; wrap the `.invoke()` call in try/catch).
- `SendReaction(reactionType: string)` — fire-and-forget; silently ignored (no error, no broadcast) if `reactionType` isn't one of the allowed set: `"like"`, `"heart"`, `"fire"`, `"wow"` (exact lowercase strings, hard-coded `HashSet<string>` in `LivestreamHub`).

**Server → client events** (subscribe via `connection.on("EventName", handler)`), source `src/MusicLounge.Infrastructure/Services/LivestreamHubService.cs`, all scoped to the group `livestream-{livestreamId}`:
- `ReceiveMessage` — payload is a `ChatMessageDto` (same shape as the REST chat-history `items[]` entries: `messageId`, `userId`, `displayName`, `message`, `sentAt`). Fired on every successful `SendMessage`.
- `ReceiveReaction` — payload `{ "reactionType": string }`. Fired on every accepted `SendReaction`.
- `DonationAlert` — payload is a `DonationAlertDto`: `donorName` (string), `amount` (number, decimal), `message` (string | null). NOT wired to any controller in this doc — triggered from the Donations feature when a donation lands during a live stream.
- `ViewerCountUpdated` — payload `{ "count": number }`. Fired on every connect/disconnect that actually changed the count (atomic DB increment/decrement, floored at 0).
- `LivestreamTerminated` — payload `{ "reason": string }`. Fired once by `POST /api/v1/livestreams/{id}/terminate`; FE should stop retrying the HLS URL on receipt of this event.

### /hubs/public-donations
**Auth**: none — `[AllowAnonymous]` explicitly on the `MapHub` call in `Program.cs` (the hub class itself carries no `[Authorize]`; this is a deliberate public transparency feed, not paid content). Because the API's `FallbackPolicy` deny-by-default only covers attribute-routed controller actions, minimal-API hub mappings need this explicit override or they silently 401 — already hit and fixed once in this codebase, per the source comment.
**Connection query params**: `loungeShowId` (int) — **required**; connection is aborted if missing/unparseable. Scoped by `LoungeShowId`, not `LivestreamId` — donation transparency for a show isn't conditional on a livestream currently being live.
**Client → server hub methods**: none — connect-only, no invokable methods.
**Server → client events**, source `src/MusicLounge.Infrastructure/Services/PublicDonationHubService.cs`, scoped to group `public-donations-{loungeShowId}`:
- `PublicDonationAlert` — payload is a `PublicDonationAlertDto`:
  - `donationId` (number) — stable id to correlate repeated events for the same donation across status transitions (treat as one timeline entry per `donationId`, not one event = one new donation)
  - `donorDisplayName` (string | null) — `null` if the donor chose `IsAnonymous`
  - `amount` (number | null, decimal) — `null` if the donor chose NOT `IsAmountPublic`
  - `message` (string | null) — `null` if the donor chose NOT `IsMessagePublic`
  - `status` (string) — the `Donation.Status` enum's `.ToString()` value (plain string, e.g. `"PendingOwnerAck"`, `"OwnerReceived"`, `"PerformerPaid"` — check `src/MusicLounge.Domain/Enums/DonationStatus.cs` directly if you need the exhaustive list, it's outside this doc's assigned controllers)
  - `occurredAt` (string, ISO-8601)
- Fired at every point a donation's status changes where real money actually moved (VNPay confirm → `PendingOwnerAck`, Owner ack → `OwnerReceived`, performer payout confirm → `PerformerPaid`) — never on `PendingPayment`/`Cancelled`.
- **No batching/windowing** — one SignalR message per event, by deliberate design (see source comment: each event is gated behind a real VNPay payment round-trip, which already spaces donations out; revisit only if usage scale changes).

---

## Part 7 — Admin / Complaints / Uploads

> `pageSize` clamp 50 (không phải 100) cho: `GET /admin/lounges/pending`, `/admin/refund-requests`, `/admin/users`, `/complaints/my`, `/admin/complaints`. `GET /uploads/mine` vẫn clamp 100 (khớp mặc định chung). Endpoint upload có thể trả 400 (thiếu file/quá size — FluentValidation) HOẶC 422 (sai extension hoặc sai file-signature/magic-bytes — `DomainException` từ `LocalFileStorageService`) tuỳ loại lỗi.

## AdminController

Base route: `api/v1/admin`. Class-level `[Authorize(Policy = Policies.RequireAdmin)]` — every action below requires Admin role unless noted otherwise (none override it).

### GET /api/v1/admin/ledger/integrity-check
**Auth**: RequireAdmin
**Route params**: none
**Query params**: none
**Request body**: none
**Response 200**: `data` is `LedgerIntegrityIssueDto[]` (not paginated — full list every call).
- `issueType` (string) — kind of integrity problem found (free-form string from `ILedgerIntegrityService`, not an enum).
- `journalId` (string) — the ledger journal group id affected.
- `debitTotal` (decimal) — sum of debit lines in that journal.
- `creditTotal` (decimal) — sum of credit lines in that journal.
- `detail` (string, nullable) — extra context, defaults to `null`.

Example `data`:
```json
[
  {"issueType": "UnbalancedJournal", "journalId": "a1b2c3d4e5f6", "debitTotal": 150000, "creditTotal": 140000, "detail": null}
]
```
**Other status codes**: none beyond the standard 401/403 auth failures.
**Notes**: Read-only diagnostic endpoint; no pagination even for large result sets.

---

### GET /api/v1/admin/lounges/pending
**Auth**: RequireAdmin
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 20; effective max **50**, see cross-cutting note #2)
**Request body**: none
**Response 200**: `data` is `PaginatedResult<PendingLoungeDto>`. Each item:
- `id` (int)
- `name` (string)
- `description` (string, nullable)
- `ownerId` (int)
- `ownerName` (string)
- `ownerEmail` (string)
- `businessLicenseUrl` (string, nullable)
- `street` (string)
- `ward` (string)
- `district` (string)
- `city` (string)
- `createdAt` (**`DateTime`**, not `DateTimeOffset` — no numeric offset suffix guaranteed; serializes per `DateTimeKind` of the stored value)

Example `data.items[0]`:
```json
{
  "id": 42, "name": "Lounge Cổ Điển", "description": "Phòng trà nhạc trữ tình",
  "ownerId": 7, "ownerName": "Nguyễn Văn A", "ownerEmail": "owner@example.com",
  "businessLicenseUrl": "/uploads/abc123.jpg",
  "street": "12 Lê Lợi", "ward": "Bến Nghé", "district": "Quận 1", "city": "TP.HCM",
  "createdAt": "2026-08-10T09:15:00"
}
```
**Other status codes**: none besides auth.
**Notes**: Only ever returns venues with `LoungeStatus == Pending` (see `ILoungeRepository.GetPendingAsync`). No `status` field in the DTO since it's always Pending by construction.

---

### POST /api/v1/admin/lounges/{id:int}/approve
**Auth**: RequireAdmin
**Route params**: `id` (int) — the Lounge id.
**Request body**: none
**Response 204**: no body.
**Other status codes**:
- 404 — Lounge not found.
- 409 — `lounge.Status != LoungeStatus.Pending` (already approved/rejected/etc).
**Notes**: The *only* code path anywhere that moves a venue Pending → Approved. Sends `NotificationType.VenueApproved` to the owner on success.

---

### POST /api/v1/admin/lounges/{id:int}/reject
**Auth**: RequireAdmin
**Route params**: `id` (int) — the Lounge id.
**Request body**: `RejectLoungeBody`
- `reason` (string) — required, `NotEmpty`, `MaximumLength(1000)`.

Example:
```json
{"reason": "Giấy phép kinh doanh không hợp lệ."}
```
**Response 204**: no body.
**Other status codes**:
- 400 — `reason` missing/empty or over 1000 chars (FluentValidation).
- 404 — Lounge not found.
- 409 — `lounge.Status != LoungeStatus.Pending`.
**Notes**: Sets `lounge.RejectionReason = reason` and `Status = Rejected`. Sends `NotificationType.VenueRejected` to the owner, including the reason text in the message body.

---

### GET /api/v1/admin/bank-accounts/pending
_(Added 2026-08-18)_
**Auth**: RequireAdmin
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 20, clamped 1–50)
**Request body**: none
**Response 200**: `PaginatedResult<PendingBankAccountDto>` — `items[]` fields:
- `id` (int) — the BankAccount id; pass this to the `/verify` endpoint below.
- `loungeId` (int), `loungeName` (string)
- `ownerId` (int), `ownerName` (string), `ownerEmail` (string)
- `businessLicenseUrl` (string, nullable) — the document to compare the account holder against
- `bankName` (string)
- `accountNumber` (string) — **decrypted PII**. It is returned in plaintext because it is the value being
  verified against the business licence; a masked value would make the screen useless. Never log it or put
  it in a URL.
- `accountHolder` (string)
- `isDefault` (bool) — only a *default* account gates settlement, so this decides whether verifying will
  release blocked payouts
- `createdAt` (DateTimeOffset)

**Ordering**: oldest first (by `Id`) — this is a queue of venues whose payouts are currently blocked, so
the longest-waiting one comes first.

**Scope**: `OwnerType = Lounge` **only**. Performer accounts are deliberately excluded — the platform never
transfers to one (donation chặng 2 is the Owner paying the performer directly, evidenced by an uploaded
receipt), so verifying a performer account would gate nothing and would only pad the queue with rows an
Admin can take no meaningful action on.

**Why this endpoint exists**: `SettlementSchedulingService` refuses to schedule a payout while a venue's
default account is unverified, but the only other read (`GET /api/v1/bank-accounts`) requires
`ownerType` + `ownerId` — so an Admin had to already know which venue to inspect, and an Owner's money
could sit blocked indefinitely with nobody able to see the backlog.

**Notes**: A row whose lounge no longer exists (orphaned account) is skipped rather than throwing — one bad
row must not take down the whole queue, and there would be nothing to verify against anyway. `totalCount`
counts unverified Lounge accounts before that skip.

---

### POST /api/v1/admin/bank-accounts/{id:int}/verify
**Auth**: RequireAdmin
**Route params**: `id` (int) — the BankAccount id (from `GET /admin/bank-accounts/pending` above).
**Request body**: none
**Response 204**: no body.
**Other status codes**:
- 404 — BankAccount not found.
- 409 — `account.IsVerified` already `true`.
**Notes**: The *only* place `BankAccount.IsVerified` is ever set. Manual/out-of-band verification — there is no automated bank-verification API integration. **Side effect**: if the account is the Lounge's default account (`OwnerType == Lounge && IsDefault == true`), this retries any settlement that was previously blocked waiting on this verification (`ISettlementSchedulingService.RetryBlockedForLoungeAsync`).

---

### GET /api/v1/admin/refund-requests
**Auth**: RequireAdmin
**Route params**: none
**Query params**: `page` (int, default 1), `pageSize` (int, default 20; effective max **50**)
**Request body**: none
**Response 200**: `data` is `PaginatedResult<RefundRequestDto>`. Only ever returns `Status == Pending` requests. Each item:
- `id` (int)
- `paymentId` (int)
- `requestedBy` (int, nullable) — buyer's UserId, null if it couldn't be derived.
- `reason` (string)
- `amountRequested` (decimal)
- `amountApproved` (decimal, nullable) — always `null` while `Pending`.
- `refundPercentage` (decimal, nullable) — e.g. `100` when it's a full-refund flow; otherwise null.
- `status` (string enum: `Pending` | `Approved` | `Rejected`) — always `"Pending"` in this list.
- `createdAt` (DateTimeOffset)
- `resolvedAt` (DateTimeOffset, nullable) — always `null` here.
- `requiresManualTransfer` (bool) — always `false` here (only set on processing).
- `gatewayRefundResponseCode` (string, nullable) — always `null` here.

Example `data.items[0]`:
```json
{
  "id": 15, "paymentId": 88, "requestedBy": 22, "reason": "Khách yêu cầu huỷ vé",
  "amountRequested": 500000, "amountApproved": null, "refundPercentage": null,
  "status": "Pending", "createdAt": "2026-08-15T14:20:00+07:00", "resolvedAt": null,
  "requiresManualTransfer": false, "gatewayRefundResponseCode": null
}
```
**Other status codes**: none besides auth.

---

### POST /api/v1/admin/refund-requests/{id:int}/process
**Auth**: RequireAdmin
**Route params**: `id` (int) — the RefundRequest id.
**Request body**: `ProcessRefundRequestBody`
- `decision` (string) — required, `Must(d => d is "Approved" or "Rejected")`. **Case-sensitive exact literal match** — this is a plain validated string, not a JsonStringEnumConverter-backed enum field.
- `approvedAmount` (decimal, nullable) — optional; when `decision == "Approved"` and omitted, defaults to the request's original `AmountRequested`. Validated `GreaterThan(0)` only when present.

Example (approve with a partial amount):
```json
{"decision": "Approved", "approvedAmount": 300000}
```
Example (reject):
```json
{"decision": "Rejected", "approvedAmount": null}
```
**Response 204**: no body.
**Other status codes**:
- 400 — `decision` not exactly `"Approved"`/`"Rejected"`, or `approvedAmount` present and `<= 0` (FluentValidation).
- 404 — RefundRequest not found, or (on Approve path) its Payment not found.
- 409 — `refund.Status != Pending` (already processed).
- **422 (not declared in Swagger attrs, but real)** — `DomainException` thrown when `amountApproved > payment.GrossAmount`, or when the running total of all approved refunds for that Payment would exceed `GrossAmount`, or when the show/lounge owner can't be derived for the payment.
**Notes**:
- Concurrency-safe: acquires a distributed lock keyed `refund:{id}` before reading, so two simultaneous "Approve" clicks can't double-refund.
- On Approve: reverses the original purchase ledger journal proportionally (platform fee, tax, owner's held share, all reversed by the same ratio `amountApproved / GrossAmount`), shrinks any not-yet-released `Settlement` rows for the same payment by the same ratio, and only flips `Payment.Status` to `Refunded` once cumulative approved refunds cover the full `GrossAmount` (a payment can back multiple tickets refunded independently).
- If `payment.Method == Gateway`, actually calls VNPay's refund API. If VNPay doesn't confirm success (`IsSuccess == false`) or the payment was `Cash`, `RequiresManualTransfer` is set `true` and all Admins get a `RefundRequiresManualTransfer` notification. The ledger reversal is committed either way — a failed/unreachable gateway never rolls back the accounting entry, it only flags manual follow-up.

---

### POST /api/v1/admin/refund-requests
**Auth**: RequireAdmin
**Request body**: `CreateRefundRequestCommand`
- `paymentId` (int) — required, `GreaterThan(0)`, must reference an existing `Payment` (async DB check).
- `amountRequested` (decimal) — required, `GreaterThan(0)`, must be `<=` the payment's `GrossAmount` (async DB check against the referenced payment).
- `reason` (string) — required, `NotEmpty`, `MaximumLength(1000)`.

Example:
```json
{"paymentId": 88, "amountRequested": 500000, "reason": "Khiếu nại đã được xác minh — hoàn tiền thủ công theo yêu cầu Admin."}
```
**Response 201**: `data` is `int` — the new `RefundRequest.Id`.
```json
{"success": true, "data": 16, "message": null}
```
**Other status codes**: 400 — any validator rule fails (bad `paymentId`, `amountRequested` out of range, missing `reason`).
**Notes**: Manual escape-hatch for refunds that don't fit the 3 automatic creation paths (buyer self-cancel, Owner show-cancel, Offline→Online format change). Creates the `RefundRequest` as `Pending` — it still has to go through `POST /admin/refund-requests/{id}/process` afterward, same approval step as any other refund request. `RequestedBy` is auto-derived from the first Ticket found for that Payment (not taken from the request body).

---

### POST /api/v1/admin/donations/{id:int}/refund
**Auth**: RequireAdmin
**Route params**: `id` (int) — the Donation id.
**Request body**: `RefundDonationBody`
- `reason` (string) — required, `NotEmpty`, `MaximumLength(1000)`.

Example:
```json
{"reason": "Donor yêu cầu hoàn do sự kiện bị huỷ."}
```
**Response 204**: no body.
**Other status codes**:
- 400 — `reason` missing/too long.
- 404 — Donation not found.
- 409 — `donation.Status` is already `Refunded` or `Cancelled`.
- **422 (not declared, but real)** — `DomainException` when `donation.Status == PendingPayment` ("chưa thanh toán — không có gì để hoàn"), when `donation.Status == PerformerPaid` (chặng-2 already forwarded to performer — must reconcile with performer manually first), or when no chặng-1 ledger entries exist for this donation to reverse.
**Notes**: Never calls VNPay automatically — reverses only the chặng-1 (donor→platform) ledger journal by flipping every original entry's `IsDebit`; the actual bank transfer back to the donor is a manual step. Notifies the donor (`DonationRefunded`) if `DonorUserId` is set, and separately notifies the venue owner to actually make the manual transfer.

---

### POST /api/v1/admin/jobs/{jobId}/trigger
**Auth**: RequireAdmin
**Route params**: `jobId` (string) — must exactly match one of the whitelisted Hangfire recurring job ids (case-sensitive, exact string):
`release-expired-holds`, `refresh-recommendations`, `auto-confirm-donations`, `expire-stuck-donations`, `cancel-abandoned-payments`, `release-due-settlements`, `send-event-reminders`, `check-overdue-donations`, `expire-ticket-transfers`, `warn-expiring-subscriptions`, `expire-subscriptions`.
**Request body**: none
**Response 204**: no body.
**Other status codes**: 400 — `jobId` not in the whitelist (FluentValidation `Must`).
**Notes**: Forces an immediate run of an already-registered recurring job without changing its Cron schedule (`IBackgroundJobService.TriggerRecurringJobNow`). If `jobId` is spelled subtly wrong but *does* pass the whitelist check somehow it would be a silent Hangfire no-op — but the whitelist should prevent that in practice since it's an exact-match list.

---

### GET /api/v1/admin/users
**Auth**: RequireAdmin
**Query params**:
- `searchText` (string, optional) — free-text search (matches implementation in `IUserRepository.SearchAsync`, likely name/email — not independently verified here).
- `role` (string enum, optional) — one of `Audience` | `Staff` | `Owner` | `Admin`. Bound as `UserRole?` via ASP.NET Core's standard query-string enum binder (not `JsonStringEnumConverter` — this is request binding, not JSON body deserialization).
- `isActive` (bool, optional)
- `page` (int, default 1)
- `pageSize` (int, default 20; effective max **50**)

**Response 200**: `data` is `PaginatedResult<UserAdminDto>`. Each item:
- `id` (int)
- `email` (string)
- `fullName` (string)
- `phone` (string, nullable)
- `avatarUrl` (string, nullable)
- `role` (string) — one of `Audience` | `Staff` | `Owner` | `Admin` (plain `string` property populated via `user.Role.ToString()`, not the enum type itself, but identical values/casing).
- `isActive` (bool)
- `isEmailVerified` (bool)
- `createdAt` (**`DateTime`**, not `DateTimeOffset`)

Example `data.items[0]`:
```json
{
  "id": 22, "email": "user@example.com", "fullName": "Trần Thị B", "phone": "0912345678",
  "avatarUrl": "/uploads/xyz.png", "role": "Audience", "isActive": true, "isEmailVerified": true,
  "createdAt": "2026-01-05T08:00:00"
}
```
**Other status codes**: none besides auth.

---

### GET /api/v1/admin/users/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int) — UserId.
**Response 200**: `data` is `UserAdminDto` (same shape as the list item above).
**Other status codes**: 404 — user not found.

---

### GET /api/v1/admin/users/{id:int}/citizen-card/{side}
**Auth**: RequireAdmin
**Route params**: `id` (int) — target UserId. `side` (string) — must be `"front"` or `"back"` (case-insensitive `OrdinalIgnoreCase` match).
**Response 200**: **not `ApiResponse<T>`** — this action returns the raw file bytes directly via `File(content, contentType)`. `Content-Type` is whatever the stored image's real type is (`image/jpeg`, `image/png`, etc, resolved from the file's extension). No JSON envelope. Response also carries `Cache-Control: no-store` (`[ResponseCache(NoStore = true, ...)]`).
**Other status codes**:
- 404 — user not found, or that side's citizen-card image was never uploaded (`CitizenCardFrontImageUrl`/`BackImageUrl` empty on the User row).
- **422 (not declared)** — `side` is neither `"front"` nor `"back"` → `DomainException`.
- 403 declared in principle by the shared query handler (`ForbiddenException` if requester is neither the target user nor Admin) but **practically unreachable through this specific route**, since the whole controller already requires `RequireAdmin` — the handler's own guard can never fail here.
**Notes**: File lives outside `wwwroot` (private store), so there is no guessable public URL — this endpoint is the only way to fetch it. Every Admin access to *someone else's* card is logged with a `LogWarning` audit line (`TargetUserId`, `Side`, `AdminUserId`) for BVDLCN-2025 traceability; a user viewing their own card (not reachable via this Admin route, but via the shared handler elsewhere) is not logged.

---

### POST /api/v1/admin/users/{id:int}/deactivate
**Auth**: RequireAdmin
**Route params**: `id` (int) — UserId to deactivate.
**Request body**: none
**Response 204**: no body.
**Other status codes**:
- 404 — user not found. (Controller only declares 204/404 in Swagger.)
- **422 (not declared at all)** — `DomainException` in two cases: (a) `id == currentUser.UserId` ("cannot lock your own account"), or (b) target is the **last remaining active Admin** (`otherActiveAdmins == 0` among other active Admins) — blocked to prevent locking out all admin access with no in-app recovery.
**Notes**: `IsActive = false` revokes access immediately mid-session (JWT `OnTokenValidated` checks `IsActive` on every request, per `ActiveUserBehavior`), not just on next login. Every call is `LogWarning`'d with `TargetUserId` + `AdminUserId`.

---

### POST /api/v1/admin/users/{id:int}/reactivate
**Auth**: RequireAdmin
**Route params**: `id` (int) — UserId to reactivate.
**Request body**: none
**Response 204**: no body.
**Other status codes**: 404 — user not found. No other business-rule guards (unlike Deactivate).
**Notes**: Sets `IsActive = true` unconditionally. `LogWarning`'d the same way as Deactivate.

---

### POST /api/v1/admin/categories
**Auth**: RequireAdmin
**Request body**: `CreateEventCategoryCommand`
- `name` (string) — required, `NotEmpty`, `MaximumLength(100)`.
- `description` (string, nullable) — optional, `MaximumLength(500)` when present.

Example: `{"name": "Nhạc Trữ Tình", "description": "Các đêm nhạc trữ tình, bolero"}`
**Response 201**: `data` is `int` (new `EventCategory.Id`). New category is always created with `IsActive = true`.
**Other status codes**: 400 — validator failure.

---

### POST /api/v1/admin/genres
**Auth**: RequireAdmin
**Request body**: `CreateMusicGenreCommand`
- `name` (string) — required, `NotEmpty`, `MaximumLength(100)`.
- `nameEn` (string, nullable) — optional, `MaximumLength(100)` when present.

Example: `{"name": "Bolero", "nameEn": "Bolero"}`
**Response 201**: `data` is `int` (new `MusicGenre.Id`).
**Other status codes**: 400 — validator failure.

---

### POST /api/v1/admin/moods
**Auth**: RequireAdmin
**Request body**: `CreateMoodCommand`
- `name` (string) — required, `NotEmpty`, `MaximumLength(100)`.

Example: `{"name": "Lãng mạn"}`
**Response 201**: `data` is `int` (new `Mood.Id`).
**Other status codes**: 400 — validator failure.

---

### POST /api/v1/admin/atmospheres
**Auth**: RequireAdmin
**Request body**: `CreateVenueAtmosphereCommand`
- `name` (string) — required, `NotEmpty`, `MaximumLength(100)`.

Example: `{"name": "Ấm cúng"}`
**Response 201**: `data` is `int` (new `VenueAtmosphere.Id`).
**Other status codes**: 400 — validator failure.

---

### PUT /api/v1/admin/categories/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int)
**Request body**: `UpdateCategoryBody`
- `name` (string) — required, `NotEmpty`, `MaximumLength(100)`.
- `description` (string, nullable) — `MaximumLength(500)` when present.
- `isActive` (bool) — required (non-nullable).

Example: `{"name": "Nhạc Trữ Tình", "description": "Cập nhật mô tả", "isActive": false}`
**Response 204**: no body.
**Other status codes**: 400 — validator failure. 404 — category not found.

---

### DELETE /api/v1/admin/categories/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int)
**Response 204**: no body.
**Other status codes**:
- 404 — category not found.
- 409 — refused because at least one `LoungeShow.CategoryId` still references it. Message suggests setting `IsActive=false` instead of deleting.
**Notes**: "In-use? refuse" policy shared identically across categories/genres/moods/atmospheres.

---

### PUT /api/v1/admin/genres/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int)
**Request body**: `UpdateGenreBody`
- `name` (string) — required, `NotEmpty`, `MaximumLength(100)`.
- `nameEn` (string, nullable) — `MaximumLength(100)` when present.

**Response 204**: no body.
**Other status codes**: 400 — validator failure. 404 — genre not found.

---

### DELETE /api/v1/admin/genres/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int)
**Response 204**: no body.
**Other status codes**:
- 404 — genre not found.
- 409 — in use by any `LoungeShowGenre`, `PerformerGenre`, or `UserFavouriteGenre` row.

---

### PUT /api/v1/admin/moods/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int)
**Request body**: `UpdateMoodBody`
- `name` (string) — required, `NotEmpty`, `MaximumLength(100)`.

**Response 204**: no body.
**Other status codes**: 400 — validator failure. 404 — mood not found.

---

### DELETE /api/v1/admin/moods/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int)
**Response 204**: no body.
**Other status codes**:
- 404 — mood not found.
- 409 — in use by any `LoungeShowMood` or `UserFavouriteMood` row.

---

### PUT /api/v1/admin/atmospheres/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int)
**Request body**: `UpdateAtmosphereBody`
- `name` (string) — required, `NotEmpty`, `MaximumLength(100)`.

**Response 204**: no body.
**Other status codes**: 400 — validator failure. 404 — atmosphere not found.

---

### DELETE /api/v1/admin/atmospheres/{id:int}
**Auth**: RequireAdmin
**Route params**: `id` (int)
**Response 204**: no body.
**Other status codes**:
- 404 — atmosphere not found.
- 409 — in use by any `LoungeShowAtmosphere`, `UserFavouriteAtmosphere` row, **or** as a venue's own `MusicLounge.AtmosphereId` (checked in addition to the two M2M join tables, unlike genre/mood/category which are only referenced via join tables).

---

## ComplaintsController

Two separate controller classes live in `ComplaintsController.cs`: the public-facing `ComplaintsController` (`api/v1/complaints`, no class-level `[Authorize]`) and `AdminComplaintsController` (`api/v1/admin/complaints`, class-level `[Authorize(Policy = Policies.RequireAdmin)]`).

### POST /api/v1/complaints
**Auth**: AllowAnonymous (also tagged `[SwaggerOptionalAuth]` — the handler still reads `ICurrentUserService` if a valid Bearer token IS sent, to attach `ComplainantUserId`)
**Request body**: `CreateComplaintCommand`
- `targetType` (string) — required, must be one of: `show`, `venue`, `donation`, `ticket`, `penalty` (case-sensitive exact match against this literal list).
- `targetId` (int) — required, `GreaterThan(0)`. Must reference an existing row for `show`/`venue`/`donation`/`penalty` (async existence check per type). **Ignored when `targetType == "ticket"`** — use `targetGuid` instead, since `Ticket.Id` is a `Guid` PK.
- `targetGuid` (Guid, nullable) — required (`NotNull`) only when `targetType == "ticket"`; when present for a ticket target it must reference an existing `Ticket`. Ignored/optional for every other `targetType`.
- `category` (string) — required, must parse (case-insensitive) to one of the `ComplaintCategory` enum names: `EventMisrepresentation`, `RefundDispute`, `DonationNotPaid`, `TechnicalIssue`, `VenueConduct`, `PenaltyAppeal`, `Other`.
- `description` (string) — required, `NotEmpty`, `MaximumLength(2000)`.
- `evidenceUrls` (string, nullable) — no length/format validation found (free text, presumably a delimited list of URLs the FE should format itself).
- `contactPhone` (string, nullable) — `MaximumLength(20)` when present. **Effectively required for anonymous/guest callers** — see the 422 note below.

Example (authenticated, targeting a show):
```json
{"targetType": "show", "targetId": 12, "targetGuid": null, "category": "EventMisrepresentation",
 "description": "Nội dung chương trình không đúng như quảng cáo.", "evidenceUrls": null, "contactPhone": null}
```
Example (guest, targeting a ticket):
```json
{"targetType": "ticket", "targetId": 0, "targetGuid": "5f2c1e3a-...", "category": "RefundDispute",
 "description": "Vé bị lỗi khi check-in.", "evidenceUrls": null, "contactPhone": "0912345678"}
```
**Response 201**: `data` is `int` (new `Complaint.Id`). `Location` header points to `GET /complaints/{id}` (only actually usable by an authenticated complainant — guest complaints can't be fetched through that route, see below).
**Other status codes**:
- 400 — any FluentValidation rule fails (bad `targetType`, nonexistent target, missing `targetGuid` for a ticket target, bad `category`, empty `description`, over-length `contactPhone`).
- **422 (not declared)** — `DomainException`: unauthenticated caller (`!IsAuthenticated`) with a blank/whitespace `contactPhone` — "Vui lòng để lại số điện thoại liên hệ nếu không đăng nhập."
**Notes**: `SlaDeadline = now + ConfigKeys.ComplaintSlaHours` (system_config, default 72h, not exposed on this DTO). `Status` always starts `Open`. `ComplainantUserId` is set from the JWT if present, otherwise `null` (guest complaint) — a guest **must** supply `contactPhone` (enforced by the 422 above) so `GET /complaints/lookup` can retrieve it later.

---

### GET /api/v1/complaints/{id:int}
**Auth**: RequireAuthenticated
**Route params**: `id` (int) — Complaint id.
**Response 200**: `data` is `ComplaintDto`:
- `id` (int)
- `targetType` (string) — one of `show`|`venue`|`donation`|`ticket`|`penalty`.
- `targetId` (int)
- `targetGuid` (Guid, nullable, serializes as a GUID string)
- `category` (string enum) — one of `EventMisrepresentation`|`RefundDispute`|`DonationNotPaid`|`TechnicalIssue`|`VenueConduct`|`PenaltyAppeal`|`Other`.
- `description` (string)
- `evidenceUrls` (string, nullable)
- `contactPhone` (string, nullable)
- `status` (string enum) — one of `Open`|`Investigating`|`Resolved`|`Rejected`.
- `complainantName` (string, nullable) — null for guest complaints.
- `adminName` (string, nullable) — null until an Admin has acted on it.
- `resolution` (string, nullable)
- `resolvedAction` (string enum, nullable) — one of `Refund`|`IssueWarning`|`Dismiss`|`Compensate`|`TakeDownContent`, null until resolved.
- `resolvedAt` (DateTimeOffset, nullable)
- `createdAt` (DateTimeOffset)

Example:
```json
{
  "id": 12, "targetType": "show", "targetId": 5, "targetGuid": null,
  "category": "EventMisrepresentation", "description": "Nội dung không đúng quảng cáo.",
  "evidenceUrls": null, "contactPhone": null, "status": "Open",
  "complainantName": "Trần Thị B", "adminName": null, "resolution": null,
  "resolvedAction": null, "resolvedAt": null, "createdAt": "2026-08-14T10:00:00+07:00"
}
```
**Other status codes**:
- 403 — caller is authenticated but is neither the complaint's own `ComplainantUserId` nor Admin.
- 404 — complaint not found.
**Notes**: Guest-filed complaints (`ComplainantUserId == null`) can **never** be fetched here even by the right person, since there's no account to own them — those go through `GET /complaints/lookup` instead.

---

### GET /api/v1/complaints/my
**Auth**: RequireAuthenticated
**Query params**: `page` (int, default 1), `pageSize` (int, default 20; effective max **50**)
**Response 200**: `data` is `PaginatedResult<ComplaintDto>` (same DTO shape as above), scoped to `ComplaintantUserId == currentUser.UserId`.
**Other status codes**: none besides auth.

---

### GET /api/v1/complaints/lookup
**Auth**: AllowAnonymous. **Rate limit**: `[EnableRateLimiting("auth")]` — the 10 req/min/IP policy, deliberately reused here (not the global 100/min) because `id` is a small sequential int and `phone` is brute-forceable otherwise.
**Query params**: `id` (int, required) — the Complaint id returned at creation time. `phone` (string, required) — the `contactPhone` given at creation.
**Response 200**: `data` is `ComplaintDto` (same shape as `GET /complaints/{id}`).
**Other status codes**: 404 — returned identically whether the id doesn't exist, the complaint actually belongs to a logged-in account (not a guest one), or the phone just doesn't match (`PhoneNumberComparer.LooselyEquals`) — deliberately indistinguishable to prevent enumeration.
**Notes**: This is the *only* way a guest complainant (no account) can check status/resolution afterward — "guest order tracking" pattern (id + phone, no login). Phone comparison is "loose" (presumably normalizes formatting/country-code — see `PhoneNumberComparer`), not exact string match.

---

### GET /api/v1/admin/complaints
**Auth**: RequireAdmin
**Query params**: `page` (int, default 1), `pageSize` (int, default 20; effective max **50**)
**Response 200**: `data` is `PaginatedResult<ComplaintDto>`, scoped to pending/unresolved complaints (`IComplaintRepository.GetPendingAsync` — status filter not independently re-verified beyond the method name, but the DTO shape is identical to the ones above).
**Other status codes**: none besides auth.

---

### POST /api/v1/admin/complaints/{id:int}/resolve
**Auth**: RequireAdmin
**Route params**: `id` (int) — Complaint id.
**Request body**: `ResolveComplaintRequest`
- `status` (string) — required, must be exactly one of `Investigating`, `Resolved`, `Rejected` (cannot set back to `Open`).
- `resolution` (string, nullable) — `MaximumLength(2000)`.
- `resolvedAction` (string, nullable) — when present must parse (case-insensitive) to a `ComplaintResolvedAction` name: `Refund`|`IssueWarning`|`Dismiss`|`Compensate`|`TakeDownContent`. **Required** (`NotNull`) when `status == "Resolved"`.
- `refundAmount` (decimal, nullable) — **required** (`NotNull`) when `resolvedAction == "Compensate"` (case-insensitive compare) since Compensate has no sane default amount. When present, must be `GreaterThan(0)`. For `resolvedAction == "Refund"` it's optional — omitted means "use the ticket's full price".

Example (resolve with a refund, defaulting to full ticket price):
```json
{"status": "Resolved", "resolution": "Đã xác minh khiếu nại hợp lệ.", "resolvedAction": "Refund", "refundAmount": null}
```
Example (resolve with a specific compensation amount):
```json
{"status": "Resolved", "resolution": "Bồi thường thiện chí.", "resolvedAction": "Compensate", "refundAmount": 100000}
```
Example (take down a violating show — 100% refund to every confirmed ticket holder):
```json
{"status": "Resolved", "resolution": "Nội dung vi phạm.", "resolvedAction": "TakeDownContent", "refundAmount": null}
```
**Response 204**: no body.
**Other status codes**:
- 400 — validator failure (bad `status`, bad `resolvedAction`, missing `resolvedAction` on Resolved, missing/`<=0` `refundAmount` when required).
- 404 — complaint not found.
- 409 — complaint already `Resolved` or `Rejected`.
- **422 (not declared)** — `DomainException` in 3 cases: (a) `resolvedAction == "TakeDownContent"` but `complaint.TargetType != "show"`; (b) (nested) the target show is already `Cancelled`/`Ended`; (c) (nested) the show's livestream is currently `Live` — must be stopped/terminated first before takedown can proceed.
**Notes**:
- `Refund`/`Compensate` only actually create a `RefundRequest` when `complaint.TargetType == "ticket"` AND `TargetGuid` is set — every other target type (show/venue/donation/penalty) resolves with the label recorded but **no money movement**; Admin has to separately call `POST /admin/refund-requests` for those. The created `RefundRequest` is `Pending` — still needs its own `POST /admin/refund-requests/{id}/process` approval step afterward (this endpoint never itself moves money).
- `TakeDownContent` cancels the show, cancels every `Confirmed` ticket, and auto-creates a 100%-refund `RefundRequest` per ticket (mirrors `CancelLoungeShowCommandHandler`'s exact logic) — this happens synchronously as part of the same request/transaction.
- Notifies the complainant in-app (`ComplaintUpdate`) if they're a registered user; sends an SMS instead if they're a guest with a `contactPhone`.

---

## UploadsController

Base route: `api/v1/uploads`. All actions accept **multipart/form-data**, not JSON, with a single form field named exactly `file` bound to `IFormFile file` (ASP.NET Core's default form-field-name-matches-parameter-name binding — send the field literally named `file`).

Underlying storage is `LocalFileStorageService` (`src/MusicLounge.Infrastructure/Services/LocalFileStorageService.cs`) — files land on local disk under `wwwroot/uploads` (public, served via `UseStaticFiles()`) or `App_Data/private-uploads` (never statically served, only readable via an authenticated controller action). Every upload is validated in two layers: (1) a FluentValidation `AbstractValidator<IFormFile>` checking presence + size only → **400** on failure; (2) inside `LocalFileStorageService`, extension whitelist + actual file-signature ("magic bytes") sniffing → **422 `DomainException`** on failure, even though the controller's Swagger attributes only declare 400. A renamed file (e.g. a `.exe` renamed to `photo.jpg`) passes step 1 but fails step 2.

### POST /api/v1/uploads/images
**Auth**: RequireAuthenticated
**Request body (multipart/form-data)**:
- `file` — the image, content-type per actual file. **Allowed extensions**: `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif` (case-insensitive). **Max size**: 5 MB (`UploadImageValidator.MaxSizeBytes = 5 * 1024 * 1024`), enforced both by FluentValidation and by `[RequestSizeLimit(5MB)]` on the action.
- File content is signature-checked (JPEG `FF D8 FF`, PNG's 8-byte signature, GIF `GIF87a`/`GIF89a`, WEBP `RIFF....WEBP`) — must genuinely match the claimed extension.
**Response 200**: `data` is `UploadImageResponse`:
- `url` (string) — public relative path, e.g. `/uploads/3fa85f64....jpg`. Use directly as `PrimaryImageUrl`/`CoverImageUrl`/avatar/etc in other endpoints.
```json
{"success": true, "data": {"url": "/uploads/3fa85f64-5717-4562-b3fc-2c963f66afa6.jpg"}, "message": null}
```
**Other status codes**:
- 400 — no file / zero-length file / file over 5 MB (FluentValidation, `errors` dict populated with key `""` or `Length` depending on which rule fired — check the property name used: rules are on `f` and `f.Length`).
- **422 (not declared)** — extension not in the allowed set, or file content doesn't match its claimed extension (`DomainException`, plain message, no `errors` dict).
**Notes**: Also records an `UploadedFile` row via `RecordUploadCommand(url, "Image")` so it shows up in `GET /uploads/mine` and can later be deleted via `DELETE /uploads/{id}`.

---

### POST /api/v1/uploads/citizen-card-images
**Auth**: RequireAuthenticated
**Request body (multipart/form-data)**: same `file` field, same validation as `/uploads/images` (5 MB, same 5 image extensions, same magic-byte checks — reuses `UploadImageValidator`).
**Response 200**: `data` is `UploadImageResponse`:
- `url` (string) — **NOT a public URL** despite the field name — this is an opaque private storage reference (e.g. a GUID+extension) usable only as input to `SubmitCitizenCardCommand` elsewhere. Do not attempt to render it directly as an `<img src>`.
**Other status codes**: same as `/uploads/images` (400 for size/missing, 422 for bad extension/signature).
**Notes**: Saved straight to the private store (`App_Data/private-uploads`), never touches `wwwroot`/public static files — avoids a window where a CCCD/CMND photo would be briefly reachable via a guessable public URL. **Deliberately not recorded via `RecordUploadCommand`** — this file will never appear in `GET /uploads/mine`, since it's PII with exactly one intended destination (citizen-card verification) and shouldn't show up in a general upload gallery.

---

### POST /api/v1/uploads/models
**Auth**: RequireOwner
**Request body (multipart/form-data)**:
- `file` — a 3D model. **Allowed extension: `.glb` only** (`.gltf` deliberately rejected — it's JSON that references separate `.bin`/texture files a single-file upload flow can't carry along, so it would silently fail to load client-side). **Max size**: 30 MB (`UploadModel3DValidator.MaxSizeBytes = 30 * 1024 * 1024`).
- Signature check: first 4 bytes must be ASCII `glTF` (the binary glTF magic).
**Response 200**: `data` is `UploadImageResponse`:
- `url` (string) — public relative path under a `models` subfolder, e.g. `/uploads/models/3fa85f64....glb`. Feed directly to a Three.js `GLTFLoader`.
**Other status codes**:
- 400 — no file / zero-length / over 30 MB.
- **422 (not declared)** — extension isn't `.glb`, or content doesn't start with the `glTF` magic bytes.
**Notes**: Also records an `UploadedFile` row (`Kind = "Model3D"`) via `RecordUploadCommand`.

---

### GET /api/v1/uploads/mine
**Auth**: RequireAuthenticated
**Query params**: `page` (int, default 1), `pageSize` (int, default 20; effective max **100** — this handler's own clamp is `[1,100]`, matching the global filter, no extra tightening unlike the Admin/Complaints list endpoints above).
**Response 200**: `data` is `PaginatedResult<UploadedFileDto>`. Each item:
- `id` (int)
- `url` (string) — the stored path/reference as recorded at upload time.
- `kind` (string) — `"Image"` or `"Model3D"` (from `UploadKind` enum, via `.ToString()`).
- `createdAt` (DateTimeOffset)

Example `data.items[0]`:
```json
{"id": 9, "url": "/uploads/3fa85f64....jpg", "kind": "Image", "createdAt": "2026-08-16T11:00:00+07:00"}
```
**Other status codes**: none besides auth.
**Notes**: Scoped to `UploaderUserId == currentUser.UserId` — never shows another user's uploads. Sorted by `Id` ascending (not `CreatedAt`) — a documented SQLite/`DateTimeOffset` ORDER BY limitation shared with several other list handlers in this codebase; order should still be correct on the real SQL Server target. **Citizen-card images never appear here** (see `/uploads/citizen-card-images` note above).

---

### DELETE /api/v1/uploads/{id:int}
**Auth**: RequireAuthenticated
**Route params**: `id` (int) — the `UploadedFile.Id` (from `GetMine`'s response, not derivable from the `url` alone).
**Response 204**: no body.
**Other status codes**:
- 403 — `file.UploaderUserId != currentUser.UserId` (only the original uploader may delete it — Admin has no override here).
- 404 — no `UploadedFile` row with that id.
**Notes**: Deletes both the physical file (`IFileStorageService.DeleteAsync` — idempotent, silently no-ops if the file's already gone) and the DB row. **Does not check whether the URL is still referenced elsewhere** (avatar, cover image, F&B item image, tour/gallery images, etc — none of those are real foreign keys to this table, they're all plain string fields) — deleting a still-in-use upload is entirely on the caller; the resulting dangling reference will just 404 wherever it's rendered.
