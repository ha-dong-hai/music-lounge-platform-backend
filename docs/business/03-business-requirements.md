# Business Requirements

← [02-database.md](02-database.md) | Tiếp theo: [04-design-decisions.md](04-design-decisions.md) →

---

## 33 BRs trong 7 nhóm

**Single source of truth** cho toàn bộ schema.

| Nhóm | BR | Chủ đề |
|---|---|---|
| 1 | BR-01–05 | Quản lý Phòng trà |
| 2 | BR-06–10 | Tổ chức đêm nhạc |
| 3 | BR-11–15 | Khám phá & đặt vé |
| 4 | BR-16–20 | Trải nghiệm tại Show |
| 5 | BR-21–25 | Sau Show |
| 6 | BR-26–29 | Vận hành Nền tảng |
| **7** | **BR-30–33** | **Thời gian bán vé – đặc thù Phòng trà** |

---

## BR-30 đến BR-33 – Domain đặc thù (quan trọng nhất)

| BR | Nội dung | Impact trên schema |
|---|---|---|
| BR-30 | Vé hợp lệ cả đêm – không gắn giờ vào cố định | `physical_ticket_details.checked_in_at` không có time window constraint |
| BR-31 | Tiếp tục bán walk-in khi show đang chạy | `ticket_prices.sale_end` có thể NULL hoặc sau `events.actual_start` |
| BR-32 | Giá theo khung giờ (Optional – Owner tự chọn) | Multiple `ticket_prices` rows cho cùng 1 area, khác nhau sale_start/sale_end |
| BR-33 | Không chặn check-in muộn | Hệ quả của BR-30 – Staff quét QR bất kỳ lúc nào trong ca |

---

## 31 Workflows

| Giai đoạn | Workflows | Số |
|---|---|---|
| Trước Show | W01–W14 | 14 |
| Trong Show | W15–W23 | 9 |
| Sau Show | W24–W31 | 8 |

---

← [02-database.md](02-database.md) | Tiếp theo: [04-design-decisions.md](04-design-decisions.md) →
