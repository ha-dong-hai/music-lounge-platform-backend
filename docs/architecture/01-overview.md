# Tổng quan dự án

← [README_SU26SE039.md](../README_SU26SE039.md) | Tiếp theo: [02-database.md](02-database.md) →

---

## 1. Tổng quan

Marketplace 3 chiều kết nối:
- **Chủ phòng trà (Owner)** – tổ chức đêm nhạc, bán vé, quản lý venue
- **Khán giả (Audience)** – khám phá, mua vé, donate, đánh giá
- **Nền tảng** – thu hoa hồng, vận hành AI gợi ý, kiểm duyệt nội dung

**Domain đặc thù** (khác rạp chiếu phim): Vé hợp lệ **cả đêm** – không gắn giờ vào cụ thể, walk-in khi show đang chạy, giá theo khung giờ tùy chọn.

### Actors

| Actor | Loại | Phạm vi |
|---|---|---|
| Guest | Human | Xem public event, chưa đăng nhập |
| Audience | Human | Mobile + Web: mua vé, donate, F&B, đánh giá |
| Staff | Human | Venue-scoped – check-in, phục vụ F&B |
| Owner | Human | Web dashboard – quản lý venue của mình |
| Admin | Human | Toàn hệ thống – **1 Admin duy nhất** |
| Performer | **Data Entity** | KHÔNG đăng nhập, tạo inline khi Owner lập line-up |
| VNPay | External | Cổng thanh toán |
| Mux / Agora | External | Livestream infrastructure |
| FCM | External | Push notification |
| Google Maps | External | Hiển thị vị trí venue |

---

## 2. Team & Phân công

| Vai trò | Trách nhiệm |
|---|---|
| BE1 Leader | Backend core + DB Schema (19 SP Sprint 1) |
| BE2 | Backend AI/ML (Python services) |
| FE1 | Flutter mobile |
| FE2 | Web dashboard (Owner/Admin) |

---

## 3. Tech Stack

> **CHỐT CUỐI CÙNG** – không thay đổi trừ khi ghi vào [Changelog](08-changelog.md)

| Layer | Technology | Ghi chú |
|---|---|---|
| Backend | C# / ASP.NET Core 8 | Web API |
| Mobile | Flutter | iOS + Android |
| AI/ML | Python | TF-IDF, ALS, PhoBERT |
| **Database** | **SQL Server Developer Edition** | ~~PostgreSQL~~ – đã đổi |
| ORM | Entity Framework Core | `Microsoft.EntityFrameworkCore.SqlServer` |
| Scheduled Jobs | Hangfire | `Hangfire.SqlServer` |
| Payment | VNPay sandbox | NĐ 52/2024 |
| Livestream | Mux / Agora | |
| Push Notification | FCM (Firebase Cloud Messaging) | |
| Auth | JWT + Google OAuth | |
| Maps | Google Maps | |

### Connection String (SQL Server)
```
Server=localhost;Database=SU26SE039;Trusted_Connection=True;TrustServerCertificate=True;
```

### EF Core Key Configurations
```csharp
// UUID với NEWSEQUENTIALID() – sequential, tốt hơn cho B-tree index
modelBuilder.Entity<Ticket>()
    .Property(t => t.Id)
    .HasDefaultValueSql("NEWSEQUENTIALID()");

// Partial unique index – SQL Server dùng [column]
modelBuilder.Entity<LoungeImage>()
    .HasIndex(x => new { x.LoungeId, x.IsPrimary })
    .HasFilter("[is_primary] = 1")
    .IsUnique();

// JSON column – nvarchar(max) thay jsonb
modelBuilder.Entity<Payment>()
    .Property(p => p.Breakdown)
    .HasColumnType("nvarchar(max)");
```

---

← [README_SU26SE039.md](../README_SU26SE039.md) | Tiếp theo: [02-database.md](02-database.md) →
