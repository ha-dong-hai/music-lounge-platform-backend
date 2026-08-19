# 27 — API Cheat Sheet (bản ngắn gọn cho FE)

Bản rút gọn của [26-api-field-reference.md](26-api-field-reference.md) — chỉ liệt kê route, field
chính, và ví dụ. **Không cần đọc hết 1 lượt** — dùng để tra nhanh khi code 1 màn hình cụ thể. Cần chi
tiết hơn (rule validate chính xác, mọi mã lỗi, enum đầy đủ...) thì mở file 26.

## 5 điều cần nhớ trước khi bắt đầu

1. **Base URL**: `https://musiclounge-api.azurewebsites.net`. Mọi route dưới đây viết tắt, thêm
   `/api/v1` phía trước (vd `/auth/login` → gọi thật `https://.../api/v1/auth/login`).
2. **Đăng nhập xong** → lấy `token` trong response → mọi API khác gắn header
   `Authorization: Bearer <token>`.
3. **Mọi response** đều có dạng `{ success, data, message }`. Lỗi thì `success: false`, có thêm
   `errors` (dict theo tên field) hoặc `null` nếu là lỗi nghiệp vụ chung chung.
4. **Danh sách** (list) luôn có `data.items`, `data.page`, `data.totalPages`... — chuẩn phân trang
   giống nhau ở mọi nơi.
5. Trường nào có dấu **`?`** trong cheat sheet này là **optional**, không có dấu `?` là **bắt buộc**.

---

## Thay đổi mới nhất — 2026-08-18 (đã deploy lên Azure)

**1 thay đổi BREAKING, đọc trước nếu FE đã gọi `confirm-paid`:**

| Thay đổi | Chi tiết |
|---|---|
| 💥 `POST /donations/{id}/confirm-paid` | `paymentEvidenceUrl` **từ tuỳ chọn → BẮT BUỘC** (thiếu = 400). Đổi lại, nghệ sĩ **không còn cần** tài khoản ngân hàng mặc định (trước thiếu = 422). |
| 🆕 `GET /admin/bank-accounts/pending` | Hàng chờ xác minh tài khoản ngân hàng phòng trà. |
| 🆕 `GET /venue-penalties?status=` | Danh sách xử phạt/kháng cáo cho Admin (trước chỉ có `GET /mine` của Owner). |
| 🆕 `POST /moderations/images/{moderationId}/review` | Duyệt ảnh gallery / cảnh tour 360°. |
| ➕ `GET /donations/{id}` | Thêm `paymentRef` + `paymentEvidenceUrl`. **Donor luôn nhận `null`** (che chứng từ ngân hàng), Admin/Owner thấy giá trị thật. |

Tổng số route đang phục vụ: **184** (kiểm chứng bằng `GET /swagger/v1/swagger.json`, không phải đếm tay).

---

## Phần 1 — Đăng ký / Đăng nhập / Hồ sơ cá nhân

### Đăng ký tài khoản
`POST /auth/register`
Body: `{ email, password (≥15 ký tự), fullName, phone?, acceptTerms: true, role?: "Audience"|"Owner" }`
Trả về: `{ email, fullName, verificationCodeExpiresAt }` — **chưa có token**, phải xác thực email mới đăng nhập được.

### Xác thực email (lấy token lần đầu)
`POST /auth/verify-email`
Body: `{ email, code (mã 6 số gửi qua email) }`
Trả về: `{ token, expiresAt, userId, email, fullName, role, loungeId? }` ← đây là token dùng cho mọi API sau này.

### Gửi lại mã OTP
`POST /auth/resend-verification-code` — Body: `{ email }` → Trả về: `{ verificationCodeExpiresAt }`

### Đăng nhập
`POST /auth/login` — Body: `{ email, password }` → Trả về: giống hệt verify-email (`token`, `role`...).

### Đăng nhập Google
`POST /auth/google` — Body: `{ idToken, acceptTerms? }` → Trả về: giống login. `acceptTerms` chỉ cần khi tài khoản Google này lần đầu xuất hiện.

### Quên mật khẩu / Đặt lại mật khẩu
`POST /auth/forgot-password` — Body: `{ email }` → 204, luôn trả 204 dù email có tồn tại hay không.
`POST /auth/reset-password` — Body: `{ token (lấy từ link email), newPassword }` → 204.

### Xem hồ sơ của mình
`GET /me` → Trả về: `{ id, fullName, email, avatarUrl?, aiConsent, favouriteGenreIds[], favouriteMoodIds[], favouriteAtmosphereIds[], phone?, phoneVerified, dateOfBirth? }`

### Xem doanh thu (chỉ Owner)
`GET /me/earnings` → Trả về: `{ totalEarned, pendingSettlement, completedSettlement, pendingSettlementCount, recentSettlements: [{id, amount, status, scheduledAt, paidAt?}] }`

### Sửa sở thích gợi ý AI
`PUT /me/preferences` — Body: `{ genreIds[], moodIds[], atmosphereIds[], enableAiConsent }` → 204. **Là thay thế toàn bộ**, không phải cộng dồn — gửi thiếu id nào thì id đó bị xoá khỏi sở thích.

### Sửa hồ sơ
`PUT /me/profile` — Body: `{ fullName, phone?, avatarUrl?, dateOfBirth? }` → 204. Đổi `phone` khác giá trị cũ sẽ tự reset `phoneVerified` về false.

### Đổi mật khẩu
`PUT /me/password` — Body: `{ currentPassword, newPassword (≥15 ký tự, khác mật khẩu cũ) }` → 204. Tài khoản đăng nhập bằng Google sẽ bị lỗi 422 (không có mật khẩu để đổi).

### Đổi email (2 bước)
`POST /me/email/change-request` — Body: `{ newEmail }` → 204, gửi OTP tới email mới.
`POST /me/email/change-confirm` — Body: `{ code }` → 204, chính thức đổi email.

### Nộp CCCD (KYC)
`POST /me/citizen-card` — Body: `{ citizenCardNumber (9 hoặc 12 số), frontImageUrl, backImageUrl }` → 204. Ảnh phải upload trước qua endpoint upload.
`GET /me/citizen-card/{side}` (`side` = "front" hoặc "back") → trả về **file ảnh trực tiếp**, không phải JSON.

### Xuất dữ liệu cá nhân (DSAR)
`GET /me/data-export` → Trả về toàn bộ: `profile`, `tickets[]`, `donations[]`, `ratings[]`, `complaints[]`, `followedLoungeIds[]`, `wishlistedShowIds[]`.

### Khoá tài khoản tạm thời
`DELETE /me` → 204. Khôi phục được (khác với xoá vĩnh viễn bên dưới).

### Xoá tài khoản vĩnh viễn (DSAR erasure)
`POST /me/data-erasure` — Body: `{ currentPassword? }` (bỏ qua nếu tài khoản Google-only) → 204. **Không thể hoàn tác** — ẩn danh hoá toàn bộ thông tin cá nhân, không xoá lịch sử vé/donation (giữ 10 năm theo Luật Kế toán).

### Xác thực số điện thoại
`POST /me/phone/verification-code` → 204, gửi OTP SMS tới SĐT đã lưu trong hồ sơ.
`POST /me/phone/verify` — Body: `{ code (6 ký tự) }` → 204.

### Sở thích tuỳ chỉnh theo venue
`GET /me/custom-preferences` → Trả về: `[{ criteriaId, criteriaName, value, source, weight }]`
`PUT /me/custom-preferences/{criteriaId}` — Body: `{ value, weight (0-1) }` → 204.

### Tài khoản ngân hàng nhận tiền (Owner/Performer)
`GET /bank-accounts?ownerType=Lounge|Performer&ownerId={id}` → Trả về danh sách `{ id, ownerType, ownerId, bankName, accountNumber, accountHolder, isDefault, isVerified }`.
`GET /bank-accounts/{id}` → 1 bản ghi như trên.
`POST /bank-accounts` — Body: `{ ownerType, ownerId, bankName, accountNumber (6-19 số), accountHolder, isDefault }` → trả về `id` mới. Luôn tạo với `isVerified: false`.
`PUT /bank-accounts/{id}` — Body: `{ bankName, accountNumber, accountHolder, isDefault }` → 204. **Sửa xong `isVerified` tự về false** dù chỉ đổi 1 field.

---

## Phần 2 — Phòng trà (Lounge/Venue)

### Danh sách & chi tiết phòng trà
`GET /lounges?city?&mine?&page&pageSize` → danh sách. `mine=true` (cần login Owner) trả cả phòng trà Pending/Rejected của chính mình; mặc định chỉ trả phòng trà đã `Approved`.
`GET /lounges/{id}` → chi tiết: `{ id, name, primaryImageUrl?, model3DUrl?, street/ward/district/city, fullAddress, latitude?, longitude?, followerCount, upcomingShowCount, isFollowing?, description?, atmosphereName?, galleryImages[], ownerId, status }`. Phòng trà Pending/Rejected trả 404 nếu không phải Owner/Staff/Admin.

### Tạo / sửa / xoá phòng trà
`POST /lounges` (RequireOwner) — Body: `{ name, description?, atmosphereId?, street, ward, district, city, latitude?, longitude? }` → `id` mới. **Mặc định status = Pending, cần Admin duyệt mới public** (xem Phần 7).
`PUT /lounges/{id}` — Body giống Create → 204. Là thay thế toàn bộ, không phải PATCH.
`DELETE /lounges/{id}` → 204. Chỉ xoá được nếu phòng trà **chưa từng có** show/staff/đơn F&B/penalty nào (409 nếu có).

### Quản lý nhân viên (Staff)
`GET /lounges/{id}/staff` → danh sách `{ id, userId, fullName, email, isActive, assignedAt, deactivatedAt? }` (toàn bộ lịch sử, kể cả đã gỡ).
`GET /lounges/staff/lookup?email=...` → `{ id, fullName, email }` — tra userId từ email trước khi gán.
`POST /lounges/{id}/staff` — Body: `{ userId }` → 201. Tự nâng role user lên Staff. **1 tài khoản chỉ làm Staff cho 1 venue đang active** (409 nếu đã là staff venue khác).
`DELETE /lounges/{id}/staff/{staffId}` → 204. Gỡ hết staff cuối cùng thì tự hạ role về Audience.

### Ảnh / giấy phép / model 3D
`PUT /lounges/{id}/image` — Body: `{ imageUrl }` → 204 (ảnh đại diện).
`PUT /lounges/{id}/business-license` — Body: `{ documentUrl }` → 204.
`PUT /lounges/{id}/model-3d` — Body: `{ modelUrl? }` (file `.glb`) → 204. **Khác hoàn toàn tour 360° ảnh thật bên dưới.**
`PUT /lounges/{id}/area-layout-image` — Body: `{ imageUrl? }` → 204 (ảnh sơ đồ mặt bằng, dùng chung cho cả zone 2D lẫn tour 360°).

### Khu vực chỗ ngồi (Zone)
`GET /lounges/{id}/zones?activeOnly?` → danh sách zone (không phân trang), có sẵn field layout 2D/3D (null nếu chưa set).
`POST /lounges/{id}/zones` — Body: `{ name, description?, capacity }` → `id` mới.
`PUT /lounges/{id}/zones/{zoneId}` — Body giống Create → 204.
`DELETE /lounges/{id}/zones/{zoneId}` → 204 (soft-delete, `isActive=false`).
`PUT .../zones/{zoneId}/layout-2d` — Body: `{ x, y, width, height, rotationDeg, color? }` (toạ độ % 0-100) → 204.
`PUT .../zones/{zoneId}/layout-3d` — Body: `{ x?, y?, z? }` (phải cùng có giá trị hoặc cùng null) → 204.

### Tour 360° (nhiều ảnh panorama, khác model-3d)
`GET /lounges/{id}/tour` → `{ loungeId, floorPlanImageUrl?, scenes: [{ id, imageUrl, name?, orderIndex, positionX?, positionY?, hotspots[], completedByAi, aiDisclosureText? }] }`.
`POST /lounges/{id}/tour/scenes` — Body: `{ imageUrl, name? }` → `id` mới. Giới hạn theo gói subscription (422 nếu vượt/không hỗ trợ).
`POST /lounges/{id}/tour/scenes/stitch` — Body: `{ sourceImageUrls[] (2-20 ảnh, phải bắt đầu bằng "/uploads/"), name? }` → trả `attemptId` (202 Accepted, chạy nền 15-30s).
`GET .../tour/scenes/stitch/{attemptId}` → poll: `{ id, status: "Pending"|"Succeeded"|"Failed", resultSceneId?, errorMessage? }`.
`DELETE .../tour/scenes/{sceneId}` → 204.
`PUT .../tour/scenes/{sceneId}/position` — Body: `{ x?, y? }` (0-100, cùng có/cùng null) → 204.
`POST .../tour/scenes/{sceneId}/hotspots` — Body: `{ type: "Navigate"|"Info"|"LivestreamScreen", yaw, pitch, label?, targetSceneId? (bắt buộc nếu Navigate), infoText? }` → `id` mới.
`DELETE .../tour/hotspots/{hotspotId}` → 204.

### Gallery (ảnh showcase, không giới hạn gói)
`POST /lounges/{id}/gallery` — Body: `{ imageUrl, caption? }` → `id` mới.
`DELETE /lounges/{id}/gallery/{imageId}` → 204.

### Tiêu chí gợi ý tuỳ chỉnh (Custom Criteria)
`GET /lounges/{id}/custom-criteria` → danh sách `{ id, loungeId, name, key, dataType, options?, isActive }`.
`POST /lounges/{id}/custom-criteria` — Body: `{ name, key (chữ thường, không dấu), dataType: "Select"|"Range"|"Boolean"|"Text", options? }` → `id` mới. `options` tuỳ `dataType`: Select = mảng chuỗi JSON, Range = `{min,max}`.
`PUT .../custom-criteria/{criteriaId}` — Body: `{ name, options?, isActive }` → 204. `key`/`dataType` không sửa được sau khi tạo.

### Phạt phòng trà (Venue Penalty)
`POST /venue-penalties` (RequireAdmin) — Body: `{ loungeId, penaltyType: "Warning"|"Suspension"|"Ban", reason, evidenceRef?, suspensionDays? (bắt buộc nếu Suspension) }` → `id` mới.
`GET /venue-penalties?status?&page&pageSize` (**Admin**) → **MỚI 2026-08-18** — toàn bộ penalty mọi phòng trà, kèm `ownerName`/`ownerEmail`. `status` nhận `Active`|`Appealed`|`Overturned`|`Upheld`|`Expired`; bỏ trống = tất cả. Dùng `status=Appealed` để lấy đúng hàng chờ xử lý (trạng thái duy nhất `appeal/review` chấp nhận).
&nbsp;&nbsp;Trước đây chỉ có `GET /mine` (scope theo Owner) nên đơn kháng cáo gửi lên **Admin không nhìn thấy được**.
`GET /venue-penalties/{id}` → chi tiết đầy đủ (status, appealDeadline, appealResult...).
`GET /venue-penalties/mine` (Owner) → toàn bộ penalty của các phòng trà mình sở hữu.
`POST /venue-penalties/{id}/appeal` (Owner) — Body: `{ appealReason }` → 204. Chỉ kháng cáo được khi đang Active, 1 lần duy nhất. Admin không xử lý trong 48h → tự động Overturned.
`POST /venue-penalties/{id}/appeal/review` (Admin) — Body: `{ decision: "Overturned"|"Upheld", reviewNote? }` → 204. Penalty không ở trạng thái `Appealed` → **422**.
&nbsp;&nbsp;⚠️ **Dễ bấm ngược:** `decision` là kết cục cho **án phạt**, không phải cho đơn kháng cáo. `Overturned` = **kháng cáo THẮNG**, huỷ án phạt (và nếu đó là án `Active` duy nhất thì phòng trà được khôi phục hoạt động). `Upheld` = giữ án phạt, **kháng cáo THUA**. Đừng đưa nguyên chữ enum lên nút bấm.

### Nghệ sĩ (Performer) — danh mục dùng chung mọi Owner
`GET /performers?search?&page&pageSize` → danh sách.
`GET /performers/{id}` → chi tiết `{ id, name, avatarUrl?, bio?, type: "Solo"|"Band", createdByUserId?, genreIds[], genreNames[], socialLinks[] }`.
`POST /performers` — Body: `{ name, avatarUrl?, bio?, type, genreIds[] }` → `id` mới. Ai cũng tạo được, nhưng chỉ người tạo (+Admin) mới sửa/xoá được.
`PUT /performers/{id}` — Body giống Create → 204.
`DELETE /performers/{id}` → 204. Chỉ xoá được nếu chưa từng được xếp lịch diễn (409 nếu có).
`PUT /performers/{id}/social-links` — Body: `{ platform: "Spotify"|"Youtube"|"Soundcloud"|"Facebook"|"Instagram", url, displayName? }` → upsert, trùng platform thì ghi đè.
`DELETE /performers/{id}/social-links/{linkId}` → 204.

---

## Phần 3 — Chương trình (Show/Event)

### Danh sách & tìm kiếm show
`GET /lounge-shows?page&pageSize&sortBy?&includeSoldOut?&mine?` → feed công khai (chỉ show Published). `sortBy`: `Newest`/`Popular`/`PriceAsc`/`PriceDesc`/`StartingSoon`.
`GET /lounge-shows/suggestions?q&limit?` → gợi ý gõ tìm kiếm `[{ id, name, coverImageUrl? }]`.
`GET /lounge-shows/filter-options` → dữ liệu cho bộ lọc: `{ genres[], moods[], atmospheres[], categories[], cities[] }` (cache được cả phiên).
`GET /lounge-shows/search?keyword?&genreIds[]?&...&page&pageSize` → tìm kiếm đầy đủ bộ lọc (giá, ngày, địa điểm, format...).
`GET /lounge-shows/trending?limit?&city?` → mảng show hot, không phân trang.
`GET /lounge-shows/by-lounge/{loungeId}` / `GET /lounge-shows/by-performer/{performerId}` → show theo phòng trà / theo nghệ sĩ.

### Chi tiết show
`GET /lounge-shows/{id}` → chi tiết đầy đủ: thông tin phòng trà, danh sách nghệ sĩ, `ticketTiers[]` (kèm giá + `availableSlots`), `genres[]`, `ratings`, `isWishlisted?`, `userHasTicket?`, `legalApprovalReference?`, `playbackMode`. Show Draft trả 404 nếu không phải chủ/staff/admin.
`GET /lounge-shows/{id}/seating-map` → sơ đồ zone kèm giá min/max từng zone và số chỗ còn trống.
`GET /lounge-shows/{id}/orders` (Owner) → danh sách vé đã bán cho show này.

### Tạo & sửa show (Owner)
`POST /lounge-shows` — Body: `{ loungeId, name, description, format: "Offline"|"Online", scheduledStart, scheduledEnd?, categoryId?, offlineQuota?, onlineQuota?, genreIds[], performances[]: [{ performerId? hoặc performerName?, role: "Main"|"Guest"|"Host", orderIndex, setTime?, acceptsDonation }], customValues[]: [{ criteriaId, value }] }` → `id` mới, status = **Draft**. Cần Owner có subscription Active mới tạo được.
`PUT /lounge-shows/{id}` — Body giống Create (bỏ `loungeId`/`format`) → 204. **Chỉ sửa được khi còn Draft.**
`DELETE /lounge-shows/{id}` → 204. Chỉ xoá được khi còn Draft (Published rồi phải Cancel).

### Vòng đời show — Draft → Pending → Published
```
Draft → (Owner) legal-approval + ≥1 hạng vé + POST /publish → Pending
      → (Admin) POST /moderations/shows/{id}/review {decision:"Approved"} → Published (MỚI hiện GET /lounge-shows)
      → (Admin reject) → về lại Draft
```
`PUT /lounge-shows/{id}/legal-approval` — Body: `{ legalApprovalReference }` → 204 (chỉ sửa được lúc Draft, NĐ 144/2020).
`POST /lounge-shows/{id}/publish` → 204, chuyển Pending. Điều kiện: venue đã Approved, ≥1 hạng vé, đã khai legal-approval, ngày diễn cách hiện tại ≥7 ngày làm việc, show Online/có vé livestream phải đã tạo Livestream trước.
`POST /moderations/shows/{id}/review` (Admin) — Body: `{ decision: "Approved"|"Rejected", reviewNote? (bắt buộc nếu Rejected) }` → 204.
`GET /moderations/pending?targetType?&page&pageSize` (Admin) → hàng chờ duyệt (Show/Livestream/GalleryImage/TourScene).
`POST /moderations/livestreams/{id}/review` (Admin) — giống review show, áp cho livestream.
`POST /moderations/images/{moderationId}/review` (Admin) — **MỚI 2026-08-18** — Body giống 2 cái trên. Dùng cho `targetType` = `GalleryImage` và `TourScene`, trước đây 2 loại này vào hàng chờ nhưng **không có endpoint nào duyệt được** (tồn vĩnh viễn, quá hạn SLA 24h của NĐ 147/2024). `Rejected` sẽ **xoá hẳn ảnh**.
&nbsp;&nbsp;⚠️ **Bẫy id:** 3 endpoint review dùng 2 quy ước khác nhau — show/livestream lấy **`targetId`**, còn ảnh lấy **`moderationId`** (chính `id` của bản ghi moderation), vì GalleryImage và TourScene nằm ở 2 bảng khác nhau nên `targetId` trần sẽ nhập nhằng. Gửi nhầm → 404 im lặng.

### Vận hành show
`POST /lounge-shows/{id}/cancel` (Owner/Admin) → 204. Huỷ toàn bộ vé Confirmed, tự tạo yêu cầu hoàn 100%.
`POST /lounge-shows/{id}/reschedule` — Body: `{ newScheduledStart }` → 204. Chỉ khi đang Published, ngày mới cũng phải cách ≥7 ngày làm việc.
`POST /lounge-shows/{id}/start` / `POST /lounge-shows/{id}/end` (Staff/Owner/Admin) → 204. Chỉ dùng cho show Offline (show có Livestream dùng API Livestream riêng).
`PUT /lounge-shows/{id}/format` — Body: `{ newFormat: "Online" }` → 204. Chỉ đổi được chiều Offline→Online, tự hoàn 100% vé Physical.
`PUT /lounge-shows/{id}/playback-mode` — Body: `{ playbackMode: "TwoD"|"ThreeD" }` → 204 (chỉ show Online mới dùng ThreeD).
`PUT /lounge-shows/{id}/vcpmc-royalty` — Body: `{ vcpmcRoyaltyReference }` → 204 (bắt buộc trước khi `/start`).
`PUT /lounge-shows/{id}/cover-image` / `PUT /lounge-shows/{id}/poster` — Body: `{ imageUrl }` → 204.
`POST /lounge-shows/{id}/ai-poster` — Body: `{ styleHint? }` → `{ imageUrl, remainingThisMonth }`. Cần gói có `hasAiPoster`, giới hạn theo tháng + theo show.
`GET /lounge-shows/{id}/ai-poster/history` → lịch sử các lần tạo poster AI.
`POST /lounge-shows/{id}/rate` (đã mua vé) — Body: `{ score (1-5), comment? }` → 204. Chỉ đánh giá được sau khi show Ended, trong vòng 7 ngày, 1 lần/người.

---

## Phần 4 — Vé / Thanh toán / Donation / Subscription

### Mua vé online (giữ chỗ → thanh toán)
`POST /tickets/holds` — Body: `{ priceId, quantity }` → `{ holdId, expiresAt }`. **Giữ chỗ 15 phút**, hết hạn tự huỷ.
`POST /tickets/purchase` — Body: `{ holdId }` → `{ paymentId, orderId, amount, paymentUrl, ticketIds[] }`. Redirect trình duyệt sang `paymentUrl` để thanh toán VNPay. Vé vẫn `Pending` cho tới khi VNPay xác nhận.
`DELETE /tickets/holds/{holdId}` → 204, huỷ giữ chỗ trước khi thanh toán.

### Vé của tôi
`GET /tickets/my?page&pageSize` → danh sách vé đã mua: `{ id, showId, showName, tierName, priceName, pricePaid, accessType, status, qrCode?, purchasedAt, hasPendingTransfer }`.
`GET /tickets/{id}` → chi tiết 1 vé (chỉ chủ vé xem được), kèm `physicalDetail`/`livestreamDetail`.
`GET /tickets/{id}/qr` → trả về **ảnh SVG QR trực tiếp**, không phải JSON.
`GET /tickets/by-qr/{qrCode}` → tra vé theo QR (chủ vé hoặc staff venue đều xem được).
`POST /tickets/{id}/cancel` → huỷ vé. Vé chưa thanh toán thì huỷ thẳng; vé đã Confirmed thì tự tạo `RefundRequest` (chờ Admin xử lý).

### Chuyển nhượng vé
`POST /tickets/{id}/transfer` — Body: `{ recipientEmail }` → 204, gửi lời mời.
`POST /tickets/{id}/transfer/accept` → 204, nhận vé (QR/token đổi mới).
`POST /tickets/{id}/transfer/cancel` → 204, huỷ lời mời (cả 2 bên đều gọi được).
`GET /tickets/incoming-transfers` → danh sách vé người khác đang mời mình nhận.

### Bán vé tại quầy / Check-in (Staff)
`POST /tickets/walk-in` (Staff/Owner/Admin) — Body: `{ priceId, quantity }` → `{ paymentId, amount, ticketIds[] }`. Xác nhận ngay (tiền mặt), không qua VNPay.
`POST /tickets/check-in` (Staff/Owner/Admin) — Body: `{ qrCode }` → trả chi tiết vé, đổi status thành `Used`.
`GET /tickets/refund-requests/my` → danh sách yêu cầu hoàn tiền của mình.

### Hạng vé (Owner, chỉ sửa được khi show còn Draft)
`GET /ticket-tiers?showId` → danh sách hạng vé + giá của 1 show.
`GET /ticket-tiers/{id}` → chi tiết 1 hạng vé.
`POST /ticket-tiers` — Body: `{ showId, name, description?, accessType: "Physical"|"Livestream" (phải khớp format show), zoneId?, totalCapacity?, prices: [{ name, price, quota?, purchaseChannel: "Online"|"Offline"|"Both", saleStart, saleEnd }] }` → `id` mới.
`PUT /ticket-tiers/{id}` — Body giống Create (thay thế toàn bộ `prices`) → 204.
`DELETE /ticket-tiers/{id}` → 204.

### VNPay callback/IPN (backend tự xử lý — FE KHÔNG gọi trực tiếp)
`GET /payments/vnpay/callback`, `GET /donations/vnpay-return`, `GET /subscriptions/vnpay-return` — chỉ là URL redirect trình duyệt sau khi thanh toán xong.
`GET /payments/vnpay/ipn`, `GET /donations/vnpay-ipn`, `GET /subscriptions/vnpay-ipn` — VNPay tự gọi server-to-server để xác nhận thật. FE chỉ cần biết: sau khi redirect tới `paymentUrl`, chờ 1 lúc rồi gọi lại API tương ứng (vd `GET /tickets/{id}`) để thấy status đã cập nhật.

### Donation cho nghệ sĩ
`POST /donations` — Body: `{ performanceId, amount, isAnonymous?, message?, isMessagePublic? }` → `{ donationId, orderId, gross, paymentUrl }`.
`GET /donations/{id}` → chi tiết (chỉ donor/Owner/Admin xem được), kèm breakdown `net`/`platformFee`/`tax`/`performerAmount`.
&nbsp;&nbsp;⚠️ **Cập nhật 2026-08-18:** thêm 2 field `paymentRef` + `paymentEvidenceUrl` (bằng chứng chặng 2). **Admin và Owner nhận tiền thấy giá trị thật; donor luôn nhận `null`** — biên nhận là chứng từ ngân hàng giữa Owner và nghệ sĩ. Cả 2 cũng `null` khi donation chưa tới `PerformerPaid`.
`GET /donations/my?page&pageSize` → lịch sử donation của mình (mọi trạng thái).
`GET /donations/public?page&pageSize` → feed công khai toàn hệ thống. Field tiền tệ **ẩn cả nhóm** nếu donor chọn không công khai số tiền.
`GET /performers/{performerId}/donations?page&pageSize` → lịch sử donation công khai của 1 nghệ sĩ.

### Xử lý donation (Owner)
`GET /donations/pending-ack` / `GET /donations/awaiting-payout` → hàng chờ xử lý (chưa ack / đã ack chưa trả nghệ sĩ).
`POST /donations/{id}/acknowledge` → 204, xác nhận đã nhận tiền donation.
`POST /donations/{id}/confirm-paid` — Body: `{ paymentRef, paymentEvidenceUrl }` → 204, xác nhận đã chuyển tiền cho nghệ sĩ.
&nbsp;&nbsp;⚠️ **BREAKING 2026-08-18** — 2 thay đổi ngược nhau:
&nbsp;&nbsp;• `paymentEvidenceUrl` từ **tuỳ chọn → BẮT BUỘC**. Thiếu → **400**. Upload ảnh biên nhận qua `POST /uploads/images` rồi truyền URL trả về vào đây.
&nbsp;&nbsp;• Nghệ sĩ **KHÔNG còn cần** tài khoản ngân hàng mặc định (trước đây thiếu là 422). Sàn không bao giờ chuyển tiền vào tài khoản nghệ sĩ — chặng 2 là Owner tự chuyển, nên bằng chứng mới là thứ chứng minh tiền đã đi, không phải số tài khoản. Nếu nghệ sĩ vô tình có tài khoản mặc định thì `donation.bankAccountId` vẫn được ghi nhận, không có thì để `null`.

### Gói Subscription (Owner)
`GET /subscriptions/packages?activeOnly?` → danh sách gói `{ id, name, description?, price, billingCycle, maxTicketsPerEvent, hasAiPoster, maxAiPostersPerMonth, maxTourScenes, isActive }`.
`POST /subscriptions/subscribe` — Body: `{ packageId }` → `{ paymentId, orderId, amount, paymentUrl }`.
`POST /subscriptions/renew` → giống subscribe nhưng dùng lại gói cũ, không cần chọn lại.
`POST /subscriptions/cancel` → 204. Không hoàn tiền phần đã dùng.
`GET /subscriptions/my` → gói hiện tại (hoặc `data: null` nếu chưa từng đăng ký). Trả về cả gói đã hết hạn/huỷ nếu là gói gần nhất — FE tự check `status`/`expiresAt`.

### Quản lý gói (Admin)
`POST /subscriptions/packages` — Body: `{ name, description?, price, billingCycle, maxTicketsPerEvent, hasAiPoster, maxAiPostersPerMonth (>0 nếu hasAiPoster=true), maxTourScenes }` → `id` mới.
`PUT /subscriptions/packages/{id}` — Body giống Create + `isActive` → 204. **Nếu đang có Owner active dùng gói này thì khoá sửa giá/quyền lợi**, chỉ đổi được tên/mô tả/isActive.

---

## Phần 5 — F&B (đồ ăn/uống tại venue)

### Menu
`GET /fnb-menus?loungeId&activeOnly?` → danh sách menu của 1 venue.
`GET /fnb-menus/{id}` → chi tiết 1 menu.
`POST /fnb-menus` (Owner) — Body: `{ loungeId, name, description?, displayOrder, isActive? }` → `id` mới.
`PUT /fnb-menus/{id}` — Body: `{ name, description?, isActive, displayOrder }` → 204. **Phải gửi `isActive` rõ ràng — thiếu field này tự hiểu là `false`.**
`DELETE /fnb-menus/{id}` → 204. 409 nếu menu đã từng có món được đặt (dùng `isActive=false` thay vì xoá).

### Món ăn/uống (Menu Item)
`GET /fnb-menu-items?menuId&availableOnly?` → danh sách món.
`GET /fnb-menu-items/{id}` → chi tiết 1 món.
`POST /fnb-menu-items` (Owner) — Body: `{ menuId, category, name, description?, price, imageUrl?, displayOrder, isAvailable? }` → `id` mới.
`PUT /fnb-menu-items/{id}` — Body: `{ category, name, description?, price, imageUrl?, isAvailable, displayOrder }` → 204. **Cũng phải gửi `isAvailable` rõ ràng — thiếu tự thành `false`.**
`DELETE /fnb-menu-items/{id}` → 204. 409 nếu món đã từng được đặt.

### Đặt món (Order)
`POST /fnb-orders` — Body: `{ loungeId, showId?, zoneId?, tableNote?, paymentMethod: "Cash" (bắt buộc, chưa hỗ trợ VNPay), note?, items: [{ menuItemId, quantity, note? }] }` → `id` đơn mới. Giá tự tính từ giá món hiện tại, không nhận giá từ client.
`GET /fnb-orders/{id}` → chi tiết đơn: `{ id, loungeId, showId?, status, paymentMethod, totalAmount, items[] }`.
`GET /fnb-orders/my?status?&page&pageSize` → đơn của chính mình (khách đặt).
`GET /fnb-orders?loungeId&status?&page&pageSize` (Owner/Staff) → đơn theo venue.
`PUT /fnb-orders/{id}/status` (Staff/Owner/Admin) — Body: `{ status }` → 204. **Chỉ đi tuần tự**: `Pending → Preparing → Served → Paid` (không nhảy cóc/lùi lại). `Cancelled` huỷ được từ bất kỳ trạng thái nào trừ `Paid`. **Không có push realtime — FE phải tự poll để cập nhật trạng thái đơn.**

---

## Phần 6 — Livestream & Xã hội (Follow/Wishlist/Thông báo)

### Livestream
`GET /livestreams/{id}` → `{ id, loungeShowId, showName, status, hlsUrl?, viewerCount, startedAt?, endedAt?, userHasAccess }`. **`hlsUrl` chỉ có khi `userHasAccess=true`** (Admin, staff/owner venue đó, hoặc người có vé livestream) — dù status đang Live vẫn có thể null nếu không có quyền.
`POST /livestreams` (Staff/Owner/Admin) — Body: `{ showId }` → `id` mới, status = Scheduled. Cần Admin duyệt trước khi Start được.
`POST /livestreams/{id}/start` / `POST /livestreams/{id}/end` → 204. Start cần đã duyệt + đã khai VCPMC royalty trên show.
`GET /livestreams/{id}/credentials` (Staff/Owner/Admin) → `{ provider, rtmpUrl, streamKey }` — nhạy cảm, không cache.
`GET /livestreams/{id}/chat?page&pageSize` → lịch sử chat (chỉ người có quyền xem: vé/staff/admin).
`POST /livestreams/{id}/terminate` (Admin) — Body: `{ reason }` → 204, dừng khẩn cấp vì vi phạm nội dung.

### Theo dõi phòng trà (Follow) & Yêu thích show (Wishlist)
`GET /follows/lounges?page&pageSize` → danh sách phòng trà đang theo dõi.
`POST /follows/lounges/{loungeId}` / `DELETE /follows/lounges/{loungeId}` → 204 (theo dõi/bỏ theo dõi). Bỏ theo dõi thứ chưa follow → 404 (không im lặng).
`GET /wishlist?page&pageSize` → danh sách show đã thích.
`POST /wishlist/{showId}` / `DELETE /wishlist/{showId}` → 204.

### Gợi ý show (AI)
`GET /recommendations?limit?` → mảng show gợi ý (không phân trang), kèm `recommendationScore`, `recommendationReason`. Chưa bật AI consent hoặc chưa có cache thì tự trả về show trending thay thế (`recommendationScore: 0`).

### Thông báo (Notification)
`GET /notifications?page&pageSize` → danh sách thông báo: `{ id, type, title, body, referenceType?, referenceId?, isRead, createdAt }`.
`POST /notifications/{id}/read` → 204, đánh dấu đã đọc 1 cái.
`POST /notifications/read-all` → 204, đánh dấu tất cả đã đọc.
`POST /notifications/device-tokens` — Body: `{ fid (Firebase Installation ID) }` → 204. Gọi ngay sau khi login.
`DELETE /notifications/device-tokens` — Body: `{ fid }` → 204. Gọi lúc logout (im lặng nếu không tồn tại, không lỗi).

### Thống kê (Analytics)
`GET /analytics/my-lounge?loungeId` (Owner) → doanh thu, số vé, rating, top 5 show, xu hướng 6 tháng gần nhất.
`GET /analytics/platform` (Admin) → thống kê toàn hệ thống: tổng venue/user/vé/doanh thu/donation.

### SignalR — kết nối realtime
`wss://.../hubs/livestream?livestreamId={id}&access_token={token}` (bắt buộc có vé/quyền vận hành venue). Gửi: `SendMessage(message)`, `SendReaction(reactionType: "like"|"heart"|"fire"|"wow")`. Nhận: `ReceiveMessage`, `ReceiveReaction`, `DonationAlert`, `ViewerCountUpdated`, `LivestreamTerminated`.
`wss://.../hubs/public-donations?loungeShowId={id}` (public, không cần token). Nhận: `PublicDonationAlert` (donation transparency realtime).
**Lưu ý chung cho cả 2 hub**: token JWT truyền qua **query string** `?access_token=`, không phải header `Authorization` — giới hạn kỹ thuật của WebSocket trên trình duyệt.

---

## Phần 7 — Quản trị (Admin) / Khiếu nại / Upload file

### Duyệt phòng trà (BR-01)
`GET /admin/lounges/pending?page&pageSize` (Admin) → danh sách phòng trà chờ duyệt.
`POST /admin/lounges/{id}/approve` → 204. `POST /admin/lounges/{id}/reject` — Body: `{ reason }` → 204.

### Tài khoản ngân hàng & Hoàn tiền
`GET /admin/bank-accounts/pending?page&pageSize` (Admin) → **MỚI 2026-08-18** — hàng chờ xác minh, cũ nhất trước. Mỗi dòng kèm `loungeName`, `ownerName`/`ownerEmail`, `businessLicenseUrl` (để đối chiếu), `bankName`, `accountNumber` (**đã giải mã**, PII — không log/không đưa lên URL), `accountHolder`, `isDefault`.
&nbsp;&nbsp;Chỉ trả tài khoản của **phòng trà** (`OwnerType = Lounge`). Tài khoản **nghệ sĩ không nằm ở đây** — sàn không chuyển tiền vào đó nên xác minh chẳng chặn/mở gì.
&nbsp;&nbsp;Trước đây không có endpoint này: `GET /bank-accounts` bắt buộc truyền `ownerType`+`ownerId` nên Admin phải biết trước phòng trà nào, trong khi tiền của họ đang bị treo.
`POST /admin/bank-accounts/{id}/verify` (Admin) → 204, xác minh tài khoản ngân hàng. Đã xác minh rồi → **409**.
&nbsp;&nbsp;⚠️ Xác minh tài khoản **mặc định** của phòng trà sẽ **tự động chạy lại các khoản thanh toán bị treo** của phòng trà đó (`RetryBlockedForLoungeAsync`) — tức là giải phóng tiền thật. Không có endpoint hoàn tác.
`GET /admin/refund-requests?page&pageSize` (Admin) → danh sách chờ xử lý (luôn Pending).
`POST /admin/refund-requests` — Body: `{ paymentId, amountRequested, reason }` → `id` mới (tạo yêu cầu hoàn tiền thủ công).
`POST /admin/refund-requests/{id}/process` — Body: `{ decision: "Approved"|"Rejected", approvedAmount? }` → 204. Không điền `approvedAmount` khi Approve = hoàn toàn bộ.
`POST /admin/donations/{id}/refund` — Body: `{ reason }` → 204. Chỉ đảo ngược sổ sách, **không tự chuyển khoản** — Admin phải chuyển tay.

### Người dùng
`GET /admin/users?searchText?&role?&isActive?&page&pageSize` (Admin) → danh sách user.
`GET /admin/users/{id}` → chi tiết 1 user.
`GET /admin/users/{id}/citizen-card/{side}` → ảnh CCCD (trả file trực tiếp, mọi lượt xem đều bị ghi log).
`POST /admin/users/{id}/deactivate` / `POST /admin/users/{id}/reactivate` → 204. Không tự khoá được chính mình, không khoá được Admin cuối cùng còn active.

### Danh mục (Category/Genre/Mood/Atmosphere) — thao tác giống nhau cho cả 4 loại
`POST /admin/categories` (hoặc `/genres`, `/moods`, `/atmospheres`) — Body: `{ name, description? }` (genre có thêm `nameEn?`) → `id` mới.
`PUT /admin/{loại}/{id}` — Body giống Create + `isActive` (riêng category) → 204.
`DELETE /admin/{loại}/{id}` → 204. 409 nếu tag đang được dùng ở đâu đó (show/nghệ sĩ/sở thích user...).

### Ledger & Background jobs
`GET /admin/ledger/integrity-check` (Admin) → danh sách bất thường sổ sách (nếu có).
`POST /admin/jobs/{jobId}/trigger` (Admin) → 204, chạy ngay 1 job nền theo danh sách cố định (`release-expired-holds`, `refresh-recommendations`...).

### Khiếu nại (Complaint)
`POST /complaints` (ai cũng gọi được, kể cả khách chưa đăng nhập) — Body: `{ targetType: "show"|"venue"|"donation"|"ticket"|"penalty", targetId, targetGuid? (bắt buộc nếu ticket), category, description, evidenceUrls?, contactPhone? (bắt buộc nếu chưa đăng nhập) }` → `id` mới.
`GET /complaints/{id}` (đã login) → chi tiết (chỉ người khiếu nại hoặc Admin xem được).
`GET /complaints/my?page&pageSize` → khiếu nại của mình.
`GET /complaints/lookup?id&phone` (khách, không cần login) → tra cứu khiếu nại bằng id+SĐT đã khai lúc gửi.
`GET /admin/complaints?page&pageSize` (Admin) → hàng chờ xử lý.
`POST /admin/complaints/{id}/resolve` (Admin) — Body: `{ status: "Investigating"|"Resolved"|"Rejected", resolution?, resolvedAction?: "Refund"|"IssueWarning"|"Dismiss"|"Compensate"|"TakeDownContent" (bắt buộc nếu Resolved), refundAmount? (bắt buộc nếu Compensate) }` → 204.

### Upload file (multipart/form-data, field tên `file`)
`POST /uploads/images` — ảnh (.jpg/.jpeg/.png/.webp/.gif, ≤5MB) → `{ url }` dùng thẳng cho các field ảnh khác.
`POST /uploads/citizen-card-images` — ảnh CCCD, lưu riêng tư, **`url` trả về KHÔNG phải link công khai**, chỉ dùng để gửi `POST /me/citizen-card`.
`POST /uploads/models` (Owner) — model 3D (.glb only, ≤30MB) → `{ url }`.
`GET /uploads/mine?page&pageSize` → danh sách file mình đã upload.
`DELETE /uploads/{id}` → 204. Chỉ người upload xoá được (Admin không có quyền đặc biệt ở đây). **Không kiểm tra file có đang được dùng ở nơi khác hay không** — xoá xong chỗ nào tham chiếu tới sẽ tự 404.

---

*Hết cheat sheet — 7 phần, khớp với 7 phần trong [26-api-field-reference.md](26-api-field-reference.md). Thiếu field/rule nào thì mở file đó tra chi tiết đầy đủ.*
