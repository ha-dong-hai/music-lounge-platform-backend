# Project Status

← [06-compliance.md](06-compliance.md) | Tiếp theo: [08-changelog.md](08-changelog.md) →

---

## Artifacts đã tạo

| Artifact | File | Trạng thái |
|---|---|---|
| Schema chi tiết (markdown) | THONG_KE_TONG_HOP_SCHEMA_SU26SE039.md | Hoàn chỉnh |
| Schema giải thích nghiệp vụ | GIAI_THICH_CHI_TIET_SCHEMA_SU26SE039.md | Hoàn chỉnh |
| Business Requirements | BUSINESS_REQUIREMENTS_SU26SE039.md | Hoàn chỉnh (33 BR) |
| Concept Diagram (draw.io XML) | *(đã giao, chưa lưu trong folder này)* | Hoàn chỉnh (vòng 2) |
| Concept Diagram (SVG) | CONCEPT_DIAGRAM_SU26SE039.svg | Hoàn chỉnh |
| Giải thích Concept Diagram | GIAI_THICH_CONCEPT_DIAGRAM_SU26SE039.md | Hoàn chỉnh |
| Word doc giải thích Concept Diagram | *(đã giao)* | Hoàn chỉnh (phong cách Nhật Bản) |
| Master document tổng hợp | MASTER_SU26SE039_FINAL.md | Hoàn chỉnh |
| Tổng hợp chính thức (cũ) | TONG_HOP_CHINH_THUC_SU26SE039.md | **Deprecated** – dùng PostgreSQL (cũ) |
| Sprint 1 Backlog | *(trong MASTER_SU26SE039_FINAL.md)* | Hoàn chỉnh |
| **DBML file** | DBDIAGRAM_SU26SE039.dbml | **Cần cập nhật** – thêm ticket_tiers |
| Sprint 2 Backlog | – | **Chưa tạo** |
| **Project scaffold** | `C:\Users\harry\source\repos\MusicLounge\` | 4 projects tạo xong, chưa có source files |
| **Coding Standards** | CODING_STANDARDS.md | Hoàn chỉnh |
| **README tài liệu** | README_SU26SE039.md + docs/ | Hoàn chỉnh |

> **Lưu ý:** TONG_HOP_CHINH_THUC_SU26SE039.md vẫn ghi "PostgreSQL" – deprecated, không dùng làm reference.

---

## Sprint Status

### Sprint 1 – 08/06/2026 đến 13/06/2026 (6 ngày)

| Epic | Nội dung | SP | Người làm |
|---|---|---|---|
| CF1 | Auth + Onboarding | ~15 SP | BE1 + FE1 |
| CF2 | Venue Management | ~20 SP | BE1 + FE2 |
| CF6 | Subscription | ~10 SP | BE1 |
| AI Setup | Catalog + Tag Infrastructure | ~20 SP | BE2 |
| **Tổng** | | **~65 SP / 45 tasks** | |

**BE1 Sprint 1:** 19 SP – DB Schema + EF Core setup + Auth core APIs

### Sprint 2 – (chưa tạo)

Phạm vi dự kiến: CF3 Ticket Booking, CF4 Livestream, CF5 Feedback/Rating

---

## Pending – Chưa chốt

| # | Hạng mục | Ghi chú |
|---|---|---|
| P1 | **Sprint 2 Backlog** | CF3 (W13–15), CF4 (W06–08, W19–20), CF5 (W24–26) |
| P2 | **Subscription pricing** | Giá VNĐ cụ thể cho Basic/Pro/Premium |
| P3 | **Subscription feature matrix** | max_tickets_per_event, has_ai_poster, các feature khác |
| ~~P4~~ | ~~Thời hạn đánh giá sau event~~ | **CHỐT**: 7 ngày, field `events.rating_open_until` – xem [04 §6.13](04-design-decisions.md) |
| P5 | **DBML cập nhật** | Thêm `ticket_tiers`, sửa `ticket_prices`, thêm fields mới trên `events` |
| P6 | **Bảng optional thứ 3** `performer_social_links` | Đề xuất – cần team xác nhận (xem [04-design-decisions.md#614](04-design-decisions.md)) |

---

## Câu hỏi cần xác nhận

| # | Câu hỏi | Trạng thái |
|---|---|---|
| Q1 | Owner B có thể edit performer do Owner A tạo không? | **CHỐT**: chỉ `created_by_user_id` + Admin (xem [04 §6.12](04-design-decisions.md)) |
| Q2 | `events.offline_quota` hay `SUM(seating_areas.capacity)`? | **CHỐT**: dùng cả 2 (xem [04 §6.11](04-design-decisions.md)) |
| Q3 | Bao nhiêu ngày sau show để submit đánh giá? | **CHỐT**: 7 ngày, `events.rating_open_until` (xem [04 §6.13](04-design-decisions.md)) |
| Q4 | Bảng optional thứ 3 tên gì? | **ĐỀ XUẤT**: `performer_social_links` – cần team xác nhận |
| Q5 | `fnb_orders.total_amount` set bởi C# hay SQL trigger? | **CHỐT**: C# application layer (xem [04 §6.15](04-design-decisions.md)) |
| Q6 | DBML cần update SQL Server syntax? | **CHỐT**: DBML là visualization, EF migrations là authority |
| Q7 | Migration strategy: Code-First hay DB-First? | **CHỐT**: Code-First (xem [04 §6.16](04-design-decisions.md)) |

---

← [06-compliance.md](06-compliance.md) | Tiếp theo: [08-changelog.md](08-changelog.md) →
