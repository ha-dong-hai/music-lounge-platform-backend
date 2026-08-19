# Compliance & Pháp lý

← [05-architecture.md](05-architecture.md) | Tiếp theo: [07-project-status.md](07-project-status.md) →

---

## Nghị định áp dụng

| Nghị định | Nội dung | Impact trên schema |
|---|---|---|
| NĐ 52/2024 | Thanh toán qua gateway licensed | Bắt buộc dùng VNPay, không thanh toán trực tiếp |
| NĐ 117/2025 | Thuế khấu trừ | `payments.tax_withheld`, tax account trong `ledger_entry` |
| NĐ 147/2024 | SLA 24h gỡ nội dung vi phạm; xác thực phone | `event_moderations.sla_deadline`, `users.phone_verified` |
| NĐ 85/2021 | Kênh khiếu nại bắt buộc | `complaints` table |
| BVDLCN 2025 | Bảo vệ dữ liệu cá nhân; AI consent | `users.ai_consent = false` (default); `ON DELETE SET NULL` cho PII fields |

---

## PII ON DELETE SET NULL (BVDLCN 2025)

Khi user xóa tài khoản, các dòng lịch sử được giữ lại nhưng PII được anonymize:

```sql
tickets.buyer_id               → users ON DELETE SET NULL
donations.donor_user_id        → users ON DELETE SET NULL
event_ratings.user_id          → users ON DELETE SET NULL
complaints.complainant_user_id → users ON DELETE SET NULL
```

---

## AI Consent (BVDLCN 2025)

```
users.ai_consent = false  (default – opt-out by default)
```

Khi `ai_consent = false` → KHÔNG ghi `user_behaviour_log` → AI không tracking hành vi.
User có thể bật trong Settings để nhận gợi ý cá nhân hóa tốt hơn.

---

## SLA Timelines

| Loại | SLA | Nguồn | Hành động quá hạn |
|---|---|---|---|
| Gỡ nội dung vi phạm | 24h | NĐ 147/2024 | Cảnh báo sau 20h |
| Xử lý Appeal penalty | 48h | Nội bộ | Auto-approve (6.17) |
| Donate chưa trả cho Performer | 7 ngày | BR-05 | Notification |
| Donate chưa trả >14 ngày | 14 ngày | BR-05 | venue_penalties(warning) |

Tất cả các giá trị SLA được cấu hình tại `system_config` – không hardcode.

---

← [05-architecture.md](05-architecture.md) | Tiếp theo: [07-project-status.md](07-project-status.md) →
