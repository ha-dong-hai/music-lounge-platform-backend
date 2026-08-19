# Quyết định thiết kế then chốt

← [03-business-requirements.md](03-business-requirements.md) | Tiếp theo: [05-architecture.md](05-architecture.md) →

> Mỗi quyết định ở đây đã được chốt. Thay đổi → ghi vào [Changelog](08-changelog.md).

---

## 6.1 tickets – UUID PK vì bảo mật QR

```
tickets.id = UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID()
```

Lý do: INT (id=123) → kẻ gian đoán 122, 124 → giả mạo QR. UUID 128-bit không đoán được.

`qr_code` trong `physical_ticket_details` là UUID **riêng** (có thể reset nếu bị lộ, không tạo lại vé).

`tickets` **KHÔNG có event_id trực tiếp** – derive qua: `tickets → ticket_prices → ticket_tiers → events` (2 JOIN).

---

## 6.2 ticket_tiers + ticket_prices – Kiến trúc 3 tầng

**Phân tầng rõ ràng:**
```
ticket_tiers  → Zone/Type: VIP physical, Standard physical, Livestream PPV
ticket_prices → Mức giá: Early Bird 200k, Walk-in 250k, Late Entry 100k
tickets       → Vé thực: ai mua, trạng thái, QR
```

**ticket_tiers** giữ: `access_type` (physical/livestream/combo), `area_id`, `livestream_id`

**ticket_prices** giữ: `purchase_channel` (online/offline/both), `price`, `quota`, `sale_start`, `sale_end`

- **Không có `tier.total_quota`** – dùng `seating_areas.capacity` trực tiếp làm cap (single source of truth).
- **`ticket_prices.quota` nullable** – `NULL` = không giới hạn (chỉ dùng cho tier livestream).

---

## 6.3 ticket_holds – Chống oversell concurrent

```
-- Chỉ áp dụng khi ticket_prices.quota IS NOT NULL
quota_available = quota - sold
               - SUM(quantity) WHERE is_released = false AND held_until > NOW()
-- Nếu quota IS NULL (livestream) → bỏ qua quota check, không cần hold
```

Hold timeout: 15 phút (từ `system_config.ticket_hold_minutes`). Hangfire giải phóng expired holds.

---

## 6.4 ledger_entry – Sổ cái bất biến (Double-Entry Bookkeeping)

- Chỉ INSERT, không UPDATE/DELETE
- Không có `updated_at` – đây là tín hiệu cho developer: append-only
- Sai → ghi dòng đảo (reversal entry)
- Invariant: `SUM(debit) = SUM(credit)` per `journal_id`
- 5 loại account: `gateway`, `platform`, `tax`, `user`, `performer`

---

## 6.5 Donate D15 – 2 chặng qua Owner

```
Chặng 1 (tức thì): Audience → VNPay → platform(5%) + tax(5%) + owner(90%)
Chặng 2 (Owner xác nhận): owner → performer(88%)
```

Alert >7 ngày không trả → notification. >14 ngày → venue_penalties(warning).

---

## 6.6 Settlement 70/30

```
Khi mua vé    → tạo settlement(partial_70): ngày = scheduled_start – 3 ngày
Khi set actual_end → tạo settlement(final_30): ngày = actual_end + 3 ngày
Hangfire (hàng ngày) → nhả tiền khi scheduled_date ≤ hôm nay
```

Lý do giữ 30%: bảo đảm hoàn tiền nếu event bị hủy sau khi 70% đã nhả.

---

## 6.7 system_config – Không hardcode bất kỳ số nào

```
platform_commission_rate = '0.05'    (5%)
tax_rate                 = '0.05'    (5% – NĐ 117/2025)
settlement_partial_pct   = '0.70'    (70%)
settlement_days_before   = '3'
settlement_days_after    = '3'
ai_auto_pass_threshold   = '0.20'
ai_auto_reject_threshold = '0.80'
moderation_sla_hours     = '24'      (NĐ 147/2024)
ticket_hold_minutes      = '15'
donation_hold_days       = '7'
rating_window_days       = '7'
appeal_sla_hours         = '48'
appeal_auto_approve      = 'true'
```

Khi pháp luật thay đổi → Admin sửa DB, không cần deploy lại code.

---

## 6.8 venue_penalties – Bù subscription khi suspension

```
warning:    ngay lập tức, venue vẫn hoạt động, subscription không đổi
suspension: +24h delay, venue.status='suspended', expires_at += suspension_days
ban:        +7 ngày delay, venue.status='locked', hoàn pro-rata subscription
```

Events hiện tại **KHÔNG bị hủy** khi suspension – bảo vệ khán giả đã mua vé.

---

## 6.9 AI Recommendations – Hybrid 3 thành phần

```
content_score  = genre×0.4 + mood×0.4 + atmosphere×0.2  (Jaccard + Cosine)
collab_score   = ALS collaborative filtering
custom_score   = Σ(match × weight) từ user_custom_preferences

final_score    = content×0.5 + collab×0.3 + custom×0.2

Cache TTL: 6 giờ (ai_recommendations.expires_at)
```

**AI Consent (BVDLCN 2025):** `users.ai_consent = false` (default) → KHÔNG ghi `user_behaviour_log`.

Xem thêm: [06-compliance.md](06-compliance.md)

---

## 6.10 Livestream Interactive Layer – SignalR

**SignalR Hub** (`/hubs/livestream`) xử lý real-time interaction song song với video stream (Mux/Agora):

| Feature | Lưu DB | Broadcast |
|---|---|---|
| Chat messages | ✓ `livestream_chat_messages` | Tất cả viewer trong group |
| Emoji reactions | ✗ ephemeral | Tất cả viewer |
| Donate alert | ✗ (donation đã có bảng riêng) | Tất cả viewer |
| Song requests | ✓ `song_requests` | Viewer + Staff dashboard |
| Viewer count | ✗ in-memory | Tất cả viewer khi có thay đổi |

**Group naming**: `ls_{livestreamId}` cho viewers, `ls_{livestreamId}_staff` cho Staff/Owner dashboard

**Rate limit**: 1 tin nhắn / 2 giây per user (in-memory dictionary, không cần Redis)

**Donate integration**: VNPay callback xong → `IHubContext<LivestreamHub>` broadcast `DonationAlert` vào group

**Flutter package**: `signalr_netcore` (pub.dev)

**2 bảng thêm vào** (tổng 54 bảng core):
- `livestream_chat_messages` – lịch sử chat, hỗ trợ moderation
- `song_requests` – yêu cầu bài, Staff xem trên dashboard riêng

---

## 6.11 Validation 3 tầng khi tạo/sửa ticket_prices

| Tầng | Validate gì | Nguồn dữ liệu | Enforce |
|---|---|---|---|
| Event | Tổng physical quota ≤ `events.offline_quota` | events | App |
| Event | Tổng online quota ≤ `events.online_quota` | events | App |
| Event | Tổng tất cả quota ≤ `subscription.max_tickets_per_event` | owner_subscriptions | App |
| Tier | `SUM(price.quota)` ≤ `seating_areas.capacity` | seating_areas | App |
| Tier | `access_type` tương thích `events.format` | events | App + CHECK |
| Price | `sold < quota` (khi quota NOT NULL) | ticket_prices | App (SELECT FOR UPDATE) |
| Mua vé | `events.format = 'offline'` → không có tier livestream | events | App |
| Mua vé | `livestreams.is_free = true` → không có tier PPV | livestreams | App |

### AI Moderation Score – Event content review (tách biệt với quota validation)

Khi Owner submit event để duyệt, AI scorer trả về score và quyết định:

```
AI score < 0.20  → auto_pass      (event được duyệt tự động)
AI score 0.20–0.80 → review_needed (Admin xem xét thủ công)
AI score > 0.80  → auto_reject    (event bị từ chối tự động)
SLA Admin review: 24h (NĐ 147/2024) → cảnh báo sau 20h nếu chưa xử lý
```

Xem cấu hình threshold tại §6.7 (`ai_auto_pass_threshold`, `ai_auto_reject_threshold`, `moderation_sla_hours`).

---

## 6.12 Performers – Shared catalog, edit rights theo người tạo

```
READ / ASSIGN  → tất cả Owner (autocomplete, gắn vào event của mình)
EDIT           → chỉ created_by_user_id + Admin
CREATE         → bất kỳ Owner nào (khi autocomplete không tìm thấy)
```

Lý do: Performers là catalog dùng chung. `created_by_user_id` đã đủ – không cần bảng hay field mới.

---

## 6.13 Rating window – `events.rating_open_until`

```sql
rating_open_until DATETIME2 NULL
-- NULL = show chưa kết thúc, cổng chưa mở
```

Set khi Owner gọi "Kết thúc show": `rating_open_until = actual_end + rating_window_days (= 7)`

Validate khi submit: `NOW() < rating_open_until AND checked_in_at IS NOT NULL`

Admin có thể gia hạn bằng cách sửa trực tiếp field này.

---

## 6.14 Bảng optional thứ 3 – `performer_social_links`

```sql
CREATE TABLE performer_social_links (
    id           INT IDENTITY(1,1) PRIMARY KEY,
    performer_id INT           NOT NULL REFERENCES performers(id) ON DELETE CASCADE,
    platform     NVARCHAR(50)  NOT NULL,  -- spotify | youtube | soundcloud | facebook | instagram
    url          NVARCHAR(500) NOT NULL,
    display_name NVARCHAR(255) NULL
);
```

Optional – không ảnh hưởng logic core. Cần team xác nhận trước khi implement.

---

## 6.15 `fnb_orders.total_amount` – Set tại C# application layer

Set khi `status` chuyển sang `'confirmed'`: `total_amount = SUM(item.quantity × item.unit_price)`

Immutable sau confirm. Không dùng SQL trigger vì business logic phải nằm trong application layer.

---

## 6.16 Migration strategy – Code-First

```bash
dotnet ef migrations add <TênChange> --project Infrastructure --startup-project Api
dotnet ef database update
```

Naming: `InitialSchema`, `AddTicketTiers`, `AddEventsFormatField`, `AddRatingOpenUntil`

DB-First không dùng: schema 54 bảng đã thiết kế trước, scaffold tạo code generic, mất business rules.

---

## 6.17 Appeal SLA – 48 giờ, auto-approve nếu quá hạn

```
Moderation SLA (nội dung vi phạm): 24h – pháp lý bắt buộc (NĐ 147/2024)
Appeal SLA (khiếu nại penalty):    48h – cần Admin đọc context, check lịch sử
```

Nếu Admin không xử lý sau 48h → `appeal_status = 'auto_approved'` (bảo vệ Owner khỏi bị phạt oan).

---

← [03-business-requirements.md](03-business-requirements.md) | Tiếp theo: [05-architecture.md](05-architecture.md) →
