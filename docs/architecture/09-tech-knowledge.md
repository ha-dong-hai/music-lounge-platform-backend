# Công nghệ · Kiến thức · Tiêu chuẩn — MusicLounge

← [README_SU26SE039.md](../README_SU26SE039.md)

> Tài liệu này liệt kê **toàn bộ** công nghệ, kiến thức kỹ thuật, kiến trúc và tiêu chuẩn đang và sẽ dùng trong project.
> Mỗi mục ghi rõ: vai trò, layer áp dụng, và mức độ cần biết.

---

## Mức độ cần biết

| Ký hiệu | Nghĩa |
|---|---|
| ★★★ | **Bắt buộc** — không biết không làm được |
| ★★☆ | **Quan trọng** — cần hiểu để review và maintain |
| ★☆☆ | **Tham khảo** — biết là tốt, không cần thành thạo ngay |

---

## 1. Ngôn ngữ & Runtime

| Công nghệ | Phiên bản | Vai trò | Layer | Mức độ |
|---|---|---|---|---|
| **C#** | 12 (với .NET 8) | Ngôn ngữ backend chính | Tất cả | ★★★ |
| **ASP.NET Core** | 8.0 | Web API framework | Api | ★★★ |
| **Python** | 3.11+ | AI/ML services (BE2) | Tách biệt | ★★★ (BE2) |
| **Dart / Flutter** | 3.x | Mobile app | Tách biệt | ★★★ (FE1) |

### C# features đang dùng

```
record          → Command, Query, DTO, Domain Event (immutable)
primary constructor → DI injection ngắn gọn
pattern matching    → switch expression trong GlobalExceptionHandler
file-scoped namespace → namespace MusicLounge.Application.Events;
nullable reference  → Nullable enable toàn project
collection expression → return [ Debit(...), Credit(...) ]
```

---

## 2. Database & ORM

| Công nghệ | Phiên bản | Vai trò | Mức độ |
|---|---|---|---|
| **SQL Server** | Developer Edition | RDBMS chính | ★★★ |
| **Entity Framework Core** | 8.0 | ORM, migrations, query | ★★★ |
| **EF Core Tools** | 8.0 | `dotnet ef migrations add` | ★★★ |
| **DBML** | — | Visualize schema (dbdiagram.io) | ★☆☆ |

### Kiến thức EF Core cụ thể cần biết

```
IEntityTypeConfiguration<T>  → tách config khỏi entity
HasConversion<string>()       → enum lưu dạng string
HasDefaultValueSql("NEWSEQUENTIALID()") → UUID sequential PK
HasFilter("[col] = 1")        → partial unique index (SQL Server syntax)
OnDelete(DeleteBehavior.SetNull) → PII anonymization
AsNoTracking()                → query read-only, không track change
Include() / ThenInclude()     → eager loading (tránh N+1)
Select()                      → projection, chỉ load field cần
ExecuteSqlInterpolated()      → raw SQL an toàn (parameterized)
Code-First Migrations         → schema-as-code, rollback được
```

---

## 3. Kiến trúc phần mềm

### 3.1 Clean Architecture (Onion Architecture)

```
Nguồn gốc: Robert C. Martin ("Uncle Bob") — Clean Architecture (2017)

Nguyên tắc:
  - Dependency chỉ đi vào trong (inner layers)
  - Domain không phụ thuộc framework nào
  - Business logic không phụ thuộc UI, DB, hay external services

Áp dụng:
  Domain → Application → Infrastructure → Api
```

| ★★★ | Toàn team phải hiểu — sai dependency rule là vi phạm kiến trúc |

### 3.2 CQRS — Command Query Responsibility Segregation

```
Nguồn gốc: Greg Young (2010), dựa trên CQS của Bertrand Meyer

Nguyên tắc:
  - Command: thay đổi state, có transaction, không trả data phức tạp
  - Query: chỉ đọc, AsNoTracking, không có transaction
  - Tách biệt hoàn toàn về file và handler

Áp dụng: MediatR ICommand<T> / IQuery<T>
Layer: Application
```

| ★★★ | Bắt buộc — mọi use case đều qua CQRS |

### 3.3 Domain-Driven Design (DDD) — một phần

```
Áp dụng có chọn lọc (không dùng toàn bộ DDD):

✓ Dùng:
  - Entities với typed PK (BaseEntity<TKey>)
  - Domain Events (INotification) cho side effects
  - Domain Exceptions (DomainException, NotFoundException...)
  - Ubiquitous Language (tên class = tên nghiệp vụ)
  - Aggregates đơn giản

✗ Không dùng:
  - Value Objects (quá phức tạp cho team size này)
  - Aggregate Root strict (EF Core quản lý đủ)
  - Domain Services tách biệt
```

| ★★☆ | Cần hiểu Domain Events và Entities |

---

## 4. Design Patterns

### Patterns bắt buộc (★★★)

| Pattern | Áp dụng ở đâu | Vấn đề giải quyết |
|---|---|---|
| **Generic Repository** | `IRepository<T, TKey>` | Chuẩn hóa data access, dễ mock test |
| **Unit of Work** | `IUnitOfWork` | 1 SaveChanges duy nhất cho toàn bộ request |
| **CQRS** | MediatR handlers | Tách đọc và ghi |
| **Pipeline (Decorator)** | MediatR Behaviors | Cross-cutting concerns: logging, validation, transaction |
| **Observer** | `INotification` + `INotificationHandler` | Domain Events — side effects không coupling |
| **Factory (static)** | `LedgerJournalFactory` | Tạo journal entries đúng double-entry |

### Patterns quan trọng (★★☆)

| Pattern | Áp dụng ở đâu | Vấn đề giải quyết |
|---|---|---|
| **Outbox** | Hangfire enqueue trong transaction | FCM/SignalR guaranteed delivery sau crash |
| **Strategy** | AI Hybrid Scorer | Hoán đổi content/collab/custom scorer độc lập |
| **Cache-Aside** | `ai_recommendations.expires_at` TTL 6h | AI score tốn CPU, cache 6h là đủ |
| **Chain of Responsibility** | Quota validation 3 tầng | Mỗi rule validate độc lập, thêm rule không sửa rule cũ |

### Patterns tham khảo (★☆☆)

| Pattern | Áp dụng ở đâu | Vấn đề giải quyết |
|---|---|---|
| **Circuit Breaker** | Polly → Python AI HTTP client | AI service down → fallback, không crash request |
| **Idempotency** | VNPay callback dùng `gateway_transaction_id` | Xử lý duplicate webhook an toàn |

---

## 5. Libraries & Packages

### Backend — Core

| Package | NuGet | Vai trò | Layer | Mức độ |
|---|---|---|---|---|
| **MediatR** | `MediatR` 12.x | CQRS dispatcher, Pipeline Behaviors, Domain Events | Application | ★★★ |
| **FluentValidation** | `FluentValidation` 11.x | Validation rules cho Command/Query | Application | ★★★ |
| **EF Core SQL Server** | `Microsoft.EntityFrameworkCore.SqlServer` 8.x | ORM + migrations | Infrastructure | ★★★ |
| **Hangfire** | `Hangfire.AspNetCore` + `Hangfire.SqlServer` 1.8.x | Background jobs, Outbox pattern | Infrastructure | ★★☆ |
| **SignalR** | `Microsoft.AspNetCore.SignalR` | Real-time: chat, reactions, donate alert | Infrastructure + Api | ★★☆ |
| **Serilog** | `Serilog.AspNetCore` + `Serilog.Sinks.File` | Structured logging | Api | ★★☆ |
| **Polly** | `Polly` 8.x | Circuit Breaker, Retry cho HTTP calls | Infrastructure | ★☆☆ |

### Backend — API

| Package | NuGet | Vai trò | Mức độ |
|---|---|---|---|
| **JWT Bearer** | `Microsoft.AspNetCore.Authentication.JwtBearer` 8.x | Xác thực JWT token | ★★★ |
| **API Versioning** | `Asp.Versioning.Mvc` 8.x | `/api/v1/`, `/api/v2/` | ★★☆ |
| **Swagger / OpenAPI** | `Swashbuckle.AspNetCore` 6.x | API documentation tự động | ★★☆ |

### Mobile (FE1)

| Package | pub.dev | Vai trò | Mức độ |
|---|---|---|---|
| **signalr_netcore** | signalr_netcore | Kết nối LivestreamHub | ★★★ |
| **dio** | dio | HTTP client | ★★★ |

---

## 6. External Services & Integrations

| Service | Vai trò | Protocol | Mức độ |
|---|---|---|---|
| **VNPay** (sandbox) | Cổng thanh toán — mua vé, donate | HTTPS redirect + webhook callback | ★★★ |
| **FCM** (Firebase Cloud Messaging) | Push notification iOS/Android | Firebase Admin SDK (HTTP v1) | ★★☆ |
| **Mux / Agora** | Livestream video infrastructure | REST API + SDK | ★★☆ |
| **Google OAuth 2.0** | Đăng nhập bằng Google | OAuth 2.0 Authorization Code Flow | ★★☆ |
| **Google Maps** | Hiển thị vị trí venue | Maps JavaScript API (FE) | ★☆☆ |

### VNPay — kiến thức cụ thể

```
Flow:
  1. Server tạo payment URL (HMAC-SHA512 signature)
  2. User redirect đến VNPay sandbox
  3. VNPay POST callback về ReturnUrl
  4. Server verify signature, xử lý payment
  5. Idempotency: check gateway_transaction_id trước khi xử lý

Kiến thức cần: HMAC-SHA512, query string encoding, idempotency
```

---

## 7. Kiến trúc & Tiêu chuẩn API

### REST API Design

```
Chuẩn tham chiếu: RESTful Web APIs (Richardson Maturity Model Level 2)

Áp dụng:
  - Resource-based URLs (noun, plural, kebab-case)
  - HTTP methods đúng ngữ nghĩa (GET/POST/PUT/PATCH/DELETE)
  - HTTP status codes chuẩn (200/201/204/400/401/403/404/409/422/500)
  - URL versioning (/api/v1/)
  - Pagination bắt buộc cho list endpoints
  - ApiResponse<T> envelope thống nhất
```

| ★★★ | Toàn team phải biết, FE cũng cần đọc được |

### RFC 9457 — Problem Details

```
Chuẩn: RFC 9457 "Problem Details for HTTP APIs"
Áp dụng: GlobalExceptionHandler trả về format chuẩn cho lỗi

{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Validation Error",
  "status": 400,
  "detail": "Số lượng vé phải lớn hơn 0.",
  "traceId": "00-abc123..."
}
```

| ★☆☆ | Biết để hiểu format lỗi, không cần implement thủ công |

---

## 8. Tiêu chuẩn Code

### Conventional Commits

```
Chuẩn: conventionalcommits.org v1.0.0
Format: <type>(<scope>): <subject>
Ví dụ: feat(ticket): add ticket hold mechanism with 15-minute timeout

Types dùng: feat / fix / docs / style / refactor / test / chore / perf
Scopes: auth / event / ticket / venue / payment / donation / livestream / fnb / ai
```

| ★★★ | Bắt buộc với mọi commit |

### C# Coding Convention

```
Chuẩn tham chiếu: Microsoft C# Coding Conventions
                  + Clean Code (Robert C. Martin)

Key rules:
  - PascalCase: Class, Method, Property, Interface, Enum
  - _camelCase: private field
  - camelCase: parameter, local variable
  - Async method: suffix Async + nhận CancellationToken
  - No magic number — đọc từ SystemConfig
  - Guard clause (throw early, không lồng if sâu)
  - Record cho immutable: Command, Query, DTO, Domain Event
  - Expression body cho method 1 dòng
```

| ★★★ | Chi tiết trong [CODING_STANDARDS.md](../CODING_STANDARDS.md) |

### Database Naming Convention

```
Chuẩn: snake_case cho SQL Server (theo convention của EF Core team)

  Bảng  : snake_case, số nhiều   → music_lounges, ticket_tiers
  Cột   : snake_case             → buyer_id, sale_end_date
  Index : IX_{table}_{columns}   → IX_tickets_buyer_id
  FK    : FK_{child}_{parent}    → FK_tickets_ticket_prices
```

| ★★★ | BE1 bắt buộc — schema là contract với DB |

---

## 9. Security

| Kỹ thuật | Vai trò | Mức độ |
|---|---|---|
| **JWT** (JSON Web Token) | Xác thực stateless — access + refresh token | ★★★ |
| **Google OAuth 2.0** | Đăng nhập bằng Google | ★★☆ |
| **Policy-based Authorization** | `[Authorize(Policy = "OwnerOnly")]` | ★★★ |
| **HMAC-SHA512** | Verify VNPay callback signature | ★★☆ |
| **Parameterized SQL** | Chống SQL Injection | ★★★ |
| **Input Validation** | FluentValidation cho mọi request | ★★★ |
| **PII Anonymization** | ON DELETE SET NULL cho BVDLCN 2025 | ★★☆ |
| **Structured Logging (no PII)** | Không log email/phone/tên | ★★★ |

---

## 10. Tài chính & Kế toán

| Khái niệm | Áp dụng | Mức độ |
|---|---|---|
| **Double-Entry Bookkeeping** | `ledger_entry` — mỗi giao dịch có Debit = Credit | ★★★ (BE1) |
| **Settlement** | Giữ 30%, nhả 70% trước show, 30% sau show | ★★★ (BE1) |
| **Idempotency** | Payment callback chỉ xử lý 1 lần dù gọi nhiều lần | ★★★ (BE1) |
| **Pro-rata refund** | Hoàn tiền subscription khi bị ban | ★★☆ (BE1) |

---

## 11. AI/ML (BE2)

| Kỹ thuật | Vai trò | Mức độ |
|---|---|---|
| **TF-IDF** | Content-based: tính similarity giữa tag sets | ★★★ (BE2) |
| **ALS** (Alternating Least Squares) | Collaborative filtering — gợi ý theo hành vi tương tự | ★★★ (BE2) |
| **Jaccard Similarity** | So sánh tag sets (genre, mood, atmosphere) | ★★★ (BE2) |
| **Cosine Similarity** | So sánh vector embedding | ★★☆ (BE2) |
| **PhoBERT** | NLP tiếng Việt — phân tích mô tả event | ★★☆ (BE2) |
| **Hybrid Scorer** | Kết hợp content(50%) + collab(30%) + custom(20%) | ★★★ (BE2) |
| **Strategy Pattern** | Cho phép hoán đổi scorer không sửa orchestrator | ★★☆ (BE2) |

---

## 12. Compliance — Pháp lý phải biết

| Nghị định | Điều khoản quan trọng | Impact code | Mức độ |
|---|---|---|---|
| **NĐ 52/2024** | Thanh toán phải qua gateway licensed | Bắt buộc VNPay, không cash | ★★★ |
| **NĐ 117/2025** | Thuế khấu trừ tại nguồn | `payments.tax_withheld` = 5% | ★★★ |
| **NĐ 147/2024** | SLA 24h gỡ nội dung vi phạm; phone verified | `event_moderations.sla_deadline` | ★★★ |
| **NĐ 85/2021** | Phải có kênh khiếu nại | `complaints` table bắt buộc | ★★☆ |
| **BVDLCN 2025** | AI consent opt-out mặc định; anonymize PII khi xóa | `users.ai_consent = false`; ON DELETE SET NULL | ★★★ |

---

## 13. Real-time

| Công nghệ | Vai trò | Mức độ |
|---|---|---|
| **ASP.NET Core SignalR** | Hub `/hubs/livestream` — chat, reactions, donate alert, song requests, viewer count | ★★★ (BE1 + FE1) |
| **Mux / Agora** | Video stream infrastructure (external) | ★★☆ |
| **Flutter signalr_netcore** | Client kết nối LivestreamHub | ★★★ (FE1) |

---

## 14. Background Jobs

| Công nghệ | Vai trò | Pattern | Mức độ |
|---|---|---|---|
| **Hangfire** | Job scheduling + retry + dashboard | Outbox, Recurring | ★★★ (BE1) |
| **Recurring Job** | Release expired holds (minutely), process settlements (daily 2h) | Cron | ★★★ |
| **Background Job (Outbox)** | FCM send, SignalR broadcast — enqueue trong DB transaction | Outbox | ★★★ |

---

## 15. Tooling & DevOps

| Tool | Vai trò | Mức độ |
|---|---|---|
| **Visual Studio / VS Code** | IDE | ★★★ |
| **dotnet CLI** | build, test, migrations, run | ★★★ |
| **SQL Server Management Studio** | Xem DB, query trực tiếp | ★★★ |
| **Postman / .http file** | Test API thủ công | ★★★ |
| **Swagger UI** | API documentation tự động | ★★☆ |
| **Hangfire Dashboard** | `/hangfire` — monitor jobs | ★★☆ |
| **dbdiagram.io** | Visualize schema từ DBML | ★☆☆ |
| **Git + GitHub** | Version control, PR workflow | ★★★ |

---

## Tóm tắt — Ai cần biết gì

| Kiến thức | BE1 | BE2 | FE1 | FE2 |
|---|---|---|---|---|
| C# / ASP.NET Core 8 | ★★★ | — | — | — |
| Clean Architecture + CQRS | ★★★ | ★☆☆ | ★☆☆ | ★☆☆ |
| Generic Repository + UoW | ★★★ | — | — | — |
| EF Core + SQL Server | ★★★ | — | — | — |
| MediatR (Command/Query/Event) | ★★★ | — | — | — |
| FluentValidation | ★★★ | — | — | — |
| Hangfire (Outbox + Recurring) | ★★★ | — | — | — |
| SignalR Hub | ★★★ | — | ★★★ | ★☆☆ |
| JWT + Policy Auth | ★★★ | — | ★★☆ | ★★☆ |
| VNPay integration | ★★★ | — | ★★☆ | ★★☆ |
| Double-Entry Bookkeeping | ★★★ | — | — | — |
| AI/ML (TF-IDF, ALS, Hybrid) | ★☆☆ | ★★★ | — | — |
| REST API Design | ★★★ | ★★☆ | ★★★ | ★★★ |
| Conventional Commits | ★★★ | ★★★ | ★★★ | ★★★ |
| Flutter / Dart | — | — | ★★★ | — |
| Web (React/Angular/Vue) | — | — | — | ★★★ |
| Python + ML libs | — | ★★★ | — | — |
| Compliance (BVDLCN, NĐ...) | ★★★ | ★★☆ | ★★☆ | ★★☆ |

---

← [README_SU26SE039.md](../README_SU26SE039.md)
