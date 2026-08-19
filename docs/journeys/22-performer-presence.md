# 22 — Sự hiện diện của Performer (không có tài khoản đăng nhập)

← [12-actors-and-authorization.md](12-actors-and-authorization.md) · [16-api-endpoint-catalog.md](16-api-endpoint-catalog.md) · [18-owner-journey.md](18-owner-journey.md) · [21-anonymous-journey.md](21-anonymous-journey.md) · → [23-view-catalog.md](23-view-catalog.md)

> **Vì sao đây không phải 1 "journey"**: Performer **không có `User`/JWT nào cả** — không đăng nhập được, không tự gọi API nào (đã xác nhận: `Performer.cs` không có field liên kết `User` ngoài `CreatedByUserId`, tức người **tạo hộ**). Mọi hành động liên quan tới Performer đều do **Owner** (chủ yếu) hoặc **Admin** thực hiện thay. Tài liệu này khác các journey 17-21 ở chỗ: thay vì "actor tự đi qua từng bước", đây là **bản đồ touchpoint** — Performer xuất hiện ở đâu, do ai thao tác, và quan trọng nhất: **công khai lộ ra ngoài tới đâu** cho Audience/Khách vãng lai xem.
>
> **Cập nhật**: 2026-08-13.

---

## 1. Ai thao tác thay Performer (không tự làm được gì)

| Việc | Endpoint | Ai được phép | Ghi chú |
|---|---|---|---|
| Tạo hồ sơ | `POST /performers` | Bất kỳ Owner nào (hoặc Admin) — `RequireOwner` | Catalog dùng chung giữa mọi Owner, không riêng theo venue (§6.12) |
| Xem danh sách/tìm kiếm catalog | `GET /performers`, `GET /performers/{id}` | Owner/Admin — **`RequireOwner`, KHÔNG public** | Xem [§3](#3-nơi-công-khai-thấy-được-performer) — đây không phải nơi Audience nhìn thấy performer |
| Sửa hồ sơ (tên/avatar/bio/type/genres) | `PUT /performers/{id}` | Chỉ **đúng Owner đã tạo** (`CreatedByUserId`) hoặc Admin — kiểm tra ở tầng handler, không phải policy | Owner khác **không sửa được** hồ sơ performer do Owner kia tạo, dù cả 2 đều xếp performer đó vào show của mình được (ASSIGN mở cho mọi Owner, EDIT thì không — 2 quyền tách biệt) |
| Xoá hồ sơ | `DELETE /performers/{id}` | Như trên | Chặn cứng (409) nếu performer **đã từng** được xếp lịch ở bất kỳ show nào (kể cả show cũ đã kết thúc) — `performances.performer_id` là `ON DELETE RESTRICT`, giữ nguyên lịch sử |
| Gắn/gỡ social links | `PUT /performers/{id}/social-links`, `DELETE .../social-links/{linkId}` | Như trên | Upsert theo platform — đặt lại link cho 1 platform đã có sẽ ghi đè, không tạo trùng |
| Xếp vào lineup 1 show | Gộp trong `POST /lounge-shows` / `PUT /lounge-shows/{id}` (field `Performers[]`) | Owner của venue đó | Không giới hạn số lượng nghệ sĩ/show (đã xác nhận trước đó), chỉ ràng buộc 1 performer không trùng lặp trong cùng show (DB unique index) |
| Đăng ký tài khoản ngân hàng nhận tiền | `POST /bank-accounts` (`ownerType=Performer`, `ownerId=performerId`) | Owner đã tạo performer đó | Performer **không tự đăng ký được** — bắt buộc phải có trước khi Owner xác nhận đã trả donate cho performer (xem [§2](#2-dòng-tiền--performer-là-1-tài-khoản-sổ-cái-thật)), nếu chưa có thì bị chặn cứng bằng `DomainException` |

---

## 2. Dòng tiền — Performer là 1 "tài khoản sổ cái" thật

Performer không có `User` nhưng **có** 1 identity tài chính thật trong hệ thống:

- `AccountType.Performer` — 1 loại tài khoản riêng trong sổ cái double-entry (`LedgerEntry`), tách biệt với `AccountType.User`.
- Donate đi qua đúng 2 chặng trước khi tới performer: chặng 1 (donor → Owner, lúc VNPay xác nhận), chặng 2 (Owner → Performer, lúc `ConfirmDonationPaidCommandHandler` chạy) — chặng 2 **fail-closed** nếu performer chưa có `BankAccount` mặc định đã đăng ký (xem bảng trên), không cho Owner "xác nhận đã trả" khống mà không có nơi ghi nhận đã trả vào đâu.
- Tỷ lệ chia (`donation_performer_share_rate`, mặc định 88%) chốt tại **thời điểm donate được xác nhận** (`PerformerShareRateSnapshot`), không đọc lại config sống ở chặng 2 — đổi rate giữa chừng không ảnh hưởng ngược các donate đã cam kết trước đó.

`ConfirmDonationPaidCommandHandler.cs:56-66` (comment gốc): *"Donation.BankAccountId was previously defined and commented 'D12: FK snapshot' but never assigned anywhere... Fail closed rather than confirm a payment silently going nowhere on record."*

---

## 3. Nơi công khai thấy được Performer

**Điểm dễ hiểu nhầm nhất của tài liệu này**: `GET /performers`/`GET /performers/{id}` (bảng §1) là route **quản trị catalog** (`RequireOwner`), **Audience/Khách vãng lai không bao giờ gọi được route này**. Toàn bộ những gì công khai nhìn thấy về 1 performer đến từ domain **Show**, không phải domain **Performer**:

| # | Nơi xuất hiện | Endpoint | Cần token? | DTO | Field có |
|---|---|---|---|---|---|
| 1 | Trong lineup của 1 show cụ thể | `GET /lounge-shows/{id}` | Không cần | `PerformerSummaryDto` | Id, Name, AvatarUrl, Bio, Genres, PerformanceId, **AcceptsDonation** (cờ theo từng lượt diễn, không phải theo performer) |
| 2 | "Trang cá nhân" performer (danh sách toàn bộ show đã/sắp diễn) | `GET /lounge-shows/by-performer/{id}` | Không cần | `PerformerDetailDto` | Id, Name, AvatarUrl, Bio, Genres, Shows (phân trang) |
| 3 | Sổ lịch sử donate riêng của performer | `GET /performers/{performerId}/donations` | Không cần | (xem [21 §Journey 2](21-anonymous-journey.md#journey-2--xem-minh-bạch-donate-công-khai)) | Chỉ `Gross`, không breakdown phí |
| 4 | Ticker donate realtime của 1 show | `PublicDonationHub` | Không cần | `PublicDonationAlertDto` | Tên performer **luôn hiện** — `IsAnonymous` chỉ ẩn danh tính **donor**, không liên quan tới performer |

**⚠️ Gap tìm thấy khi viết tài liệu này (chưa sửa)**: `PerformerSocialLinkDto` (Platform/Url/DisplayName — Spotify, Instagram, Facebook...) được Owner nhập và lưu thật qua `PUT /performers/{id}/social-links`, nhưng **không field nào trong 2 DTO công khai duy nhất** (`PerformerSummaryDto` ở dòng 1, `PerformerDetailDto` ở dòng 2) chứa `SocialLinks`. Chỉ `PerformerDto` (route `RequireOwner`, không public) mới có field này. Kết quả: link mạng xã hội của nghệ sĩ nhập vào hệ thống **hiện không có cách nào để khán giả xem được** qua API hiện tại — không phải cố ý ẩn (không có cờ riêng tư nào như `IsAmountPublic` bên Donation), chỉ đơn giản là 2 DTO công khai chưa từng được thêm field này. Nếu team FE định thiết kế màn hình "trang cá nhân nghệ sĩ" có khối "theo dõi trên mạng xã hội", cần báo lại để bổ sung field trước — **đừng tự suy đoán field này đã có sẵn để công khai**.

**Điểm khác cần lưu ý**: `Performer.cs` **không có field liên hệ nào cả** (không SĐT/email/địa chỉ) — chỉ Name/AvatarUrl/Bio/Type. Khác với `Complaint.ContactPhone` (khiếu nại khách vãng lai) hay `Ticket.BuyerId=null` (vé walk-in), đây **không phải** trường hợp "có dữ liệu liên hệ nhưng bị ẩn" — dữ liệu liên hệ của performer đơn giản là chưa từng được mô hình hoá trong hệ thống.

---

## 4. Tổng hợp — Performer khác gì 1 actor có tài khoản

| | Actor có `User` (Owner/Staff/Audience/Admin) | Performer |
|---|---|---|
| Đăng nhập | Có | **Không, vĩnh viễn** (không phải "chưa" như Khách vãng lai — không có cơ chế nào để trở thành có) |
| Tự sửa hồ sơ mình | Có (`PUT /me`...) | Không — Owner tạo/sửa hộ |
| Nhận tiền | Qua `User`/`BankAccount` của chính mình | Qua `BankAccount` do Owner đăng ký hộ (`OwnerType=Performer`) |
| Trong sổ cái | `AccountType.User` | `AccountType.Performer` — loại tài khoản riêng |
| Công khai xem được | Trang hồ sơ riêng (`/me` không public, nhưng có trang show/venue public) | Chỉ qua domain Show (§3) — không có "trang catalog performer" công khai |

---

*Xem thêm: [11-ba-domain-analysis.md](11-ba-domain-analysis.md) (biến thể dữ liệu không-có-User), [15-risk-audit.md](15-risk-audit.md) (nếu gap SocialLinks ở §3 được quyết định sửa, sẽ ghi nhận tại đây).*
