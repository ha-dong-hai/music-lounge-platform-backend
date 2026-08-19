# Kiến trúc Database

← [01-overview.md](01-overview.md) | Tiếp theo: [03-business-requirements.md](03-business-requirements.md) →

---

## Tổng quan

- **54 bảng core** + 3 bảng optional (venue_3d_config, virtual_seats, performer_social_links)
- 17 nhóm chức năng
- ~420 fields, ~85 FK, 43 enums
- 16 junction tables, 2 detail tables (1:1)

---

## Conventions chốt

| Vấn đề | Quyết định | Lý do |
|---|---|---|
| UUID | `UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID()` | Sequential – B-tree performance tốt hơn |
| JSON | `nvarchar(max)` | SQL Server không có jsonb |
| Partial unique index | `WHERE [column] = 1` | SQL Server syntax (không dùng `column = true`) |
| Immutable rows | Không có `updated_at` – append-only | `ledger_entry` – sổ cái kép |
| ON DELETE | `SET NULL` cho PII fields | BVDLCN 2025 anonymization |

---

## 17 Nhóm bảng

| Nhóm | Bảng | Số |
|---|---|---|
| N1 – Định danh | `users`, `lounge_staff`, `bank_accounts` | 3 |
| N2 – Catalog AI | `music_genres`, `moods`, `venue_atmospheres` | 3 |
| N3 – Venue | `music_lounges`, `lounge_images`, `seating_areas`, `event_categories` | 4 |
| N4 – Gói dịch vụ | `subscription_packages`, `owner_subscriptions` | 2 |
| N5 – Nghệ sĩ, Event & Livestream | `performers`, `performer_genres`, `events`, `performance`, `livestreams`, `livestream_chat_messages`, `song_requests` | 7 |
| N6 – Tag AI (Junctions) | `event_genres`, `event_moods`, `event_atmospheres`, `user_favourite_genres`, `user_favourite_moods`, `user_favourite_atmospheres` | 6 |
| N7 – Vé | `ticket_tiers`, `ticket_prices`, `tickets` (UUID PK), `physical_ticket_details`, `livestream_ticket_details` | 5 |
| N8 – Checkout | `ticket_holds` | 1 |
| N9 – Giao dịch & Sổ cái | `payments`, `donations`, `account`, `ledger_entry`, `settlement` | 5 |
| N10 – Hoàn tiền | `refund_requests` | 1 |
| N11 – F&B | `fnb_menu_items`, `fnb_orders`, `order_items` | 3 |
| N12 – Tương tác & AI Input | `event_ratings`, `follows`, `event_wishlists`, `user_behaviour_log`, `user_event_scores` | 5 |
| N13 – AI Output | `ai_recommendations` | 1 |
| N14 – Thông báo | `notifications` | 1 |
| N15 – Kiểm duyệt | `event_moderations`, `complaints`, `venue_penalties` | 3 |
| N16 – Cấu hình | `system_config` | 1 |
| N17 – AI Custom | `custom_criteria`, `event_custom_values`, `user_custom_preferences` | 3 |

---

## Polymorphic References

Các bảng dưới đây dùng `(owner_type, owner_id)` / `(reference_type, reference_id)` – **KHÔNG có FK constraint**, enforce tại application layer:

| Bảng | Fields | Targets |
|---|---|---|
| `bank_accounts` | owner_type + owner_id | lounge / performer |
| `payments` | reference_type + reference_id | ticket / donation / fnb / subscription / refund |
| `event_moderations` | target_type + target_id | event / livestream |
| `complaints` | target_type + target_id | event / venue / donation / ticket / penalty |
| `account` | owner_type + owner_id | gateway / platform / tax / user / performer |

---

## PII Fields với ON DELETE SET NULL

> Chi tiết compliance xem [06-compliance.md](06-compliance.md)

```
tickets.buyer_id               → users ON DELETE SET NULL
donations.donor_user_id        → users ON DELETE SET NULL
event_ratings.user_id          → users ON DELETE SET NULL
complaints.complainant_user_id → users ON DELETE SET NULL
```

---

← [01-overview.md](01-overview.md) | Tiếp theo: [03-business-requirements.md](03-business-requirements.md) →
