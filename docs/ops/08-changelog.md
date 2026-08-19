# Changelog

← [07-project-status.md](07-project-status.md) | [README_SU26SE039.md](../README_SU26SE039.md) →

> Mọi quyết định thay đổi → thêm 1 dòng tại đây với ngày và lý do.

---

| Ngày | Thay đổi | Lý do |
|---|---|---|
| (trước 2026-06-08) | Đổi RDBMS từ PostgreSQL → **SQL Server Developer Edition** | Team đã cài SQL Server, tránh cài thêm PostgreSQL |
| (trước 2026-06-08) | Concept Diagram vòng 1 bị bác, làm lại vòng 2 từ BR | Vòng 1 dùng tên kỹ thuật, không đúng cấp Conceptual |
| (trước 2026-06-08) | Tách `ticket_type` → `purchase_channel` + `access_type` | Hai khái niệm độc lập, gộp gây nhầm lẫn |
| (trước 2026-06-08) | Xóa `tickets.event_id` | Denormalized, có thể không đồng bộ → derive qua JOIN |
| (trước 2026-06-08) | Word document style: Harvard → Nhật Bản (Times New Roman, 3 tone xám) | Theo yêu cầu trình bày học thuật của nhóm |
| (trước 2026-06-08) | BR-30–33 thêm vào sau như Nhóm 7 | Domain insight phòng trà phát sinh sau khi đã có 29 BR |
| 2026-06-15 | Thêm bảng `ticket_tiers` (bảng #52) – kiến trúc 3 tầng | Cần enforce `SUM(price.quota) ≤ area.capacity` khi 1 zone có nhiều mức giá |
| 2026-06-15 | `ticket_prices`: bỏ `event_id`, `area_id`, `access_type` → đổi sang `tier_id` | Tránh denormalize, access_type chuyển lên ticket_tiers |
| 2026-06-15 | `ticket_prices.quota` đổi thành NULLABLE | NULL = không giới hạn (chỉ dùng cho tier livestream) |
| 2026-06-15 | Thêm `events.format` và `events.online_quota` | Validate access_type của tier và cap livestream quota |
| 2026-06-15 | Bỏ `ticket_tiers.total_quota` – dùng `seating_areas.capacity` trực tiếp | Single source of truth |
| 2026-06-15 | Performers: edit rights = `created_by_user_id` + Admin | Shared catalog, không cho sửa chéo record của nhau |
| 2026-06-15 | Thêm `events.rating_open_until DATETIME2 NULL` | 7 ngày rating window, Admin có thể override per-event |
| 2026-06-15 | Bảng optional thứ 3 đề xuất: `performer_social_links` | Touchpoint sau show cho khán giả follow nghệ sĩ |
| 2026-06-15 | `fnb_orders.total_amount` set tại C# khi status='confirmed' | Business logic thuộc application layer |
| 2026-06-15 | Migration strategy: Code-First | Schema đã thiết kế trước, DB-First re-scaffold là waste |
| 2026-06-15 | Appeal SLA: 48h, auto-approve nếu quá hạn | Penalty appeal cần đọc context, pháp lý không bắt 24h |
| 2026-06-15 | `system_config` thêm: `rating_window_days`, `appeal_sla_hours`, `appeal_auto_approve` | Config-driven, không hardcode |
| 2026-06-15 | Thêm SignalR Hub `/hubs/livestream` – chat, reactions, donate alert, song requests | Viewer cần tương tác real-time trong show |
| 2026-06-15 | Thêm 2 bảng: `livestream_chat_messages`, `song_requests` → tổng **54 bảng core** | Chat cần lưu để moderation; song request để Staff xem |
| 2026-06-15 | Chốt kiến trúc Clean Architecture 4 projects | Onion Architecture, dependency chỉ đi vào trong |
| 2026-06-15 | Chốt MediatR CQRS: `ICommand<T>` + `IQuery<T>` | Single Responsibility, test độc lập từng handler |
| 2026-06-15 | Chốt 3 Pipeline Behaviors: Logging → Validation → Transaction | Cross-cutting concerns viết 1 lần |
| 2026-06-15 | Domain Events qua `INotification`: 4 handlers post-payment | Tách side effects khỏi core handler |
| 2026-06-15 | FCM + SignalR broadcast → Hangfire Outbox | Guaranteed delivery kể cả khi crash |
| 2026-06-15 | `LedgerJournalFactory` – Factory Pattern | Tập trung logic double-entry |
| 2026-06-15 | Tạo project scaffold `C:\Users\harry\source\repos\MusicLounge\` | 4 projects dotnet new |
| 2026-06-15 | Tạo CODING_STANDARDS.md | Chuẩn hóa trước khi team bắt đầu code |
| 2026-06-15 | **Chốt Generic Repository + IUnitOfWork** thay IApplicationDbContext trực tiếp | Team quen với Repository Pattern; IUnitOfWork quản lý transaction rõ ràng |
| 2026-06-15 | Tách README_SU26SE039.md thành index + 8 file docs/ | 1 file duy nhất quá dài, khó tìm thông tin cụ thể |
| 2026-06-15 | Đồng bộ CODING_STANDARDS.md – thay `IApplicationDbContext` bằng `IUnitOfWork` ở §1.1, §2.2, §11.1, §13.2 | Mâu thuẫn với Generic Repository Pattern đã chốt |
| 2026-06-15 | Thêm `ProjectReference` và NuGet packages vào 4 `.csproj` files | Scaffold mặc định không có dependency giữa projects |
| 2026-06-15 | Xóa `Class1.cs` placeholder khỏi Domain, Application, Infrastructure | Vi phạm CODING_STANDARDS §2.3 |
| 2026-06-15 | Cập nhật `appsettings.json` theo cấu trúc CODING_STANDARDS §10.3 | Scaffold mặc định chỉ có Logging config |

---

← [07-project-status.md](07-project-status.md) | [README_SU26SE039.md](../README_SU26SE039.md) →
