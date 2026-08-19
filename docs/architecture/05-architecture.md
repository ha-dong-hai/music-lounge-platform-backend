# Kiến trúc ứng dụng – Clean Architecture

← [04-design-decisions.md](04-design-decisions.md) | Tiếp theo: [06-compliance.md](06-compliance.md) →

> **CHỐT** – không thay đổi trừ khi ghi vào [Changelog](08-changelog.md).

---

## 12.1 Cấu trúc 4 Projects

```
MusicLounge/
├── src/
│   ├── MusicLounge.Domain/          # Entities, Enums, Domain Events, Exceptions
│   ├── MusicLounge.Application/     # Commands, Queries, Handlers, Interfaces
│   ├── MusicLounge.Infrastructure/  # DbContext, EF Config, Repositories, External Services
│   └── MusicLounge.Api/             # Controllers, Middleware, DI wiring
└── MusicLounge.sln
```

**Dependency rule** – chỉ đi vào trong, không đi ra ngoài:
```
Api → Infrastructure → Application → Domain
```

Domain không phụ thuộc bất kỳ layer nào – pure C# class.

**Project scaffold tại:** `C:\Users\harry\source\repos\MusicLounge\`

---

## 12.2 Generic Repository + Unit of Work Pattern (chốt 2026-06-15)

Dùng **Generic Repository Pattern** kết hợp **Unit of Work** để quản lý data access – pattern quen thuộc, dễ tiếp cận, đồng bộ toàn team.

**3 thành phần:**
```
IRepository<T, TKey>   → CRUD cho 1 entity
IUnitOfWork            → transaction + SaveChanges + truy cập repository
IEventRepository       → specific repository khi cần Include phức tạp
```

### IRepository\<T, TKey\>

Định nghĩa trong `Application/Common/Interfaces/`:

```csharp
public interface IRepository<T, TKey> where T : BaseEntity<TKey>
{
    Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
}
```

### IUnitOfWork

Định nghĩa trong `Application/Common/Interfaces/`:

```csharp
public interface IUnitOfWork : IDisposable
{
    IRepository<T, TKey> Repository<T, TKey>() where T : BaseEntity<TKey>;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
```

### Specific Repository (khi cần Include phức tạp)

```csharp
// Application/Common/Interfaces/Repositories/ILoungeShowRepository.cs
public interface ILoungeShowRepository : IRepository<LoungeShow, int>
{
    Task<LoungeShow?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<LoungeShow>> GetPublishedUpcomingAsync(
        int? genreId, int page, int pageSize, CancellationToken ct = default);
}
```

### Dùng trong Handler

```csharp
// Command – dùng IUnitOfWork, transaction do TransactionBehavior lo
public class CreateLoungeShowCommandHandler : IRequestHandler<CreateLoungeShowCommand, int>
{
    private readonly IUnitOfWork _uow;

    public CreateLoungeShowCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<int> Handle(CreateLoungeShowCommand request, CancellationToken ct)
    {
        var show = new LoungeShow { Name = request.Name, LoungeId = request.LoungeId };
        _uow.Repository<LoungeShow, int>().Add(show);
        await _uow.SaveChangesAsync(ct);
        return show.Id;
    }
}

// Query – inject specific repository, AsNoTracking trong implementation
public class GetLoungeShowDetailQueryHandler : IRequestHandler<GetLoungeShowDetailQuery, LoungeShowDetailDto>
{
    private readonly ILoungeShowRepository _showRepo;

    public GetLoungeShowDetailQueryHandler(ILoungeShowRepository showRepo) => _showRepo = showRepo;

    public async Task<LoungeShowDetailDto> Handle(GetLoungeShowDetailQuery request, CancellationToken ct)
    {
        var show = await _showRepo.GetByIdWithDetailsAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);
        return new LoungeShowDetailDto(show.Id, show.Name, ...);
    }
}

// Ticket dùng Guid PK
_uow.Repository<Ticket, Guid>().Add(ticket);
```

---

## 12.3 Folder Structure

```
MusicLounge.Application/
├── Common/
│   ├── Abstractions/
│   │   ├── ICommand.cs
│   │   └── IQuery.cs
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs
│   │   ├── ValidationBehavior.cs
│   │   └── TransactionBehavior.cs
│   ├── Exceptions/
│   │   ├── NotFoundException.cs
│   │   ├── DomainException.cs
│   │   ├── ForbiddenException.cs
│   │   ├── ConflictException.cs
│   │   └── ValidationException.cs
│   ├── Interfaces/
│   │   ├── IRepository.cs              ← Generic Repository interface
│   │   ├── IUnitOfWork.cs              ← Unit of Work interface
│   │   ├── ICurrentUserService.cs
│   │   ├── IVnPayService.cs
│   │   └── IFcmService.cs
│   ├── Interfaces/Repositories/        ← Specific repository interfaces
│   │   ├── ILoungeShowRepository.cs
│   │   ├── ITicketRepository.cs
│   │   └── ILoungeRepository.cs
│   └── Models/
│       ├── ApiResponse.cs
│       └── PaginatedResult.cs
├── LoungeShows/
│   ├── Commands/
│   │   └── CreateLoungeShow/
│   │       ├── CreateLoungeShowCommand.cs
│   │       ├── CreateLoungeShowCommandHandler.cs
│   │       └── CreateLoungeShowCommandValidator.cs
│   └── Queries/
│       └── GetLoungeShowDetail/
│           ├── GetLoungeShowDetailQuery.cs
│           ├── GetLoungeShowDetailQueryHandler.cs
│           └── LoungeShowDetailDto.cs
├── Tickets/
│   ├── Commands/
│   └── DomainEventHandlers/
│       ├── WriteTicketLedgerHandler.cs
│       ├── ScheduleSettlementHandler.cs
│       ├── SendFcmConfirmHandler.cs
│       └── BroadcastDonationAlertHandler.cs
└── DependencyInjection.cs

MusicLounge.Infrastructure/
├── Persistence/
│   ├── ApplicationDbContext.cs
│   ├── UnitOfWork.cs                   ← IUnitOfWork implementation
│   ├── Configurations/                 ← IEntityTypeConfiguration<T>
│   │   ├── LoungeShowConfiguration.cs
│   │   └── TicketConfiguration.cs
│   ├── Repositories/                   ← Repository implementations
│   │   ├── Repository.cs               ← Generic base implementation
│   │   ├── LoungeShowRepository.cs
│   │   ├── TicketRepository.cs
│   │   └── LoungeRepository.cs
│   └── Migrations/
├── Services/
│   ├── CurrentUserService.cs
│   ├── VnPayService.cs
│   └── FcmService.cs
├── Jobs/
│   ├── ReleaseExpiredHoldsJob.cs
│   └── ProcessSettlementsJob.cs
├── Hubs/
│   └── LivestreamHub.cs
└── DependencyInjection.cs
```

---

## 12.4 MediatR CQRS – Commands vs Queries

```csharp
public interface ICommand<TResponse> : IRequest<TResponse> { }
public interface IQuery<TResponse>   : IRequest<TResponse> { }

// Command – thay đổi state, có transaction
public record PurchaseTicketCommand(int PriceId, int UserId, int Quantity) : ICommand<Guid>;

// Query – chỉ đọc, AsNoTracking, KHÔNG có transaction
public record GetEventDetailQuery(int EventId) : IQuery<EventDetailDto>;
```

**Naming convention:**

| Loại | Pattern | Ví dụ |
|---|---|---|
| Command | `{Verb}{Noun}Command` | `PurchaseTicketCommand`, `CreateEventCommand` |
| Query | `Get{Noun}Query` | `GetEventDetailQuery`, `GetTicketsByEventQuery` |
| Handler | `{CommandOrQuery}Handler` | `PurchaseTicketCommandHandler` |
| DTO | `{Noun}Dto` | `EventDetailDto`, `TicketSummaryDto` |
| Domain Event | `{Noun}{PastTense}` | `TicketPaymentConfirmed`, `EventPublished` |

---

## 12.5 Pipeline Behaviors

Thứ tự thực thi (outer → inner):
```
[Request]
  → LoggingBehavior       (log request + response time)
  → ValidationBehavior    (FluentValidation – throw nếu invalid)
  → TransactionBehavior   (chỉ wrap ICommand, không wrap IQuery)
    → Handler
  ← TransactionBehavior   (commit nếu không có exception)
[Response]
```

```csharp
public class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly IUnitOfWork _uow;

    public TransactionBehavior(IUnitOfWork uow) => _uow = uow;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var response = await next();
        await _uow.CommitTransactionAsync(ct);
        return response;
    }
}
```

---

## 12.6 Domain Events – Post-payment side effects

```csharp
public record TicketPaymentConfirmed(int PaymentId, int UserId, int? LivestreamId) : INotification;

// 4 Handlers độc lập – chạy song song, không block nhau
public class WriteTicketLedgerHandler     : INotificationHandler<TicketPaymentConfirmed> { }
public class ScheduleSettlementHandler    : INotificationHandler<TicketPaymentConfirmed> { }
public class SendFcmConfirmHandler        : INotificationHandler<TicketPaymentConfirmed> { }
public class BroadcastDonationHandler     : INotificationHandler<TicketPaymentConfirmed> { }
```

FCM và SignalR broadcast enqueue vào **Hangfire** (Outbox) – ngoài DB transaction, đảm bảo delivery kể cả khi crash.

---

## 12.7 LedgerJournalFactory – Double-entry bookkeeping

```csharp
public static class LedgerJournalFactory
{
    public static IEnumerable<LedgerEntry> CreateTicketPurchase(
        Payment payment, SystemConfig config, string journalId)
    {
        var commission = payment.Amount * config.CommissionRate;    // 5%
        var tax        = payment.Amount * config.TaxRate;           // 5%
        var ownerNet   = payment.Amount - commission - tax;         // 90%

        return
        [
            Debit (journalId, AccountType.Gateway,  payment.Amount),
            Credit(journalId, AccountType.Platform, commission),
            Credit(journalId, AccountType.Tax,      tax),
            Credit(journalId, AccountType.Owner,    ownerNet),
        ];
        // Invariant: SUM(debit) == SUM(credit) == payment.Amount
    }
}
```

---

## 12.8 Design Patterns tổng hợp

| Pattern | Áp dụng ở đâu | Vấn đề giải quyết |
|---|---|---|
| **Generic Repository** | `IRepository<T, TKey>` + implementations | Chuẩn hóa data access, dễ mock trong test |
| **Unit of Work** | `IUnitOfWork` – wrap DbContext + transaction | 1 commit duy nhất cho mọi thay đổi trong 1 request |
| **CQRS** | Mọi use case qua MediatR | Tách đọc (AsNoTracking) và ghi (transaction) |
| **Pipeline** (Decorator) | MediatR Pipeline Behaviors | Cross-cutting concerns 1 lần cho mọi command/query |
| **Domain Events + Observer** | `INotification` + `INotificationHandler` | Post-payment side effects không coupling vào handler chính |
| **Factory** | `LedgerJournalFactory` | Tạo journal entries đúng double-entry, tập trung logic |
| **Outbox** | Hangfire enqueue trong DB transaction | FCM/SignalR guaranteed delivery |
| **Strategy** | AI hybrid scorer | Hoán đổi content/collab/custom weight mà không sửa orchestrator |
| **Cache-Aside** | `ai_recommendations.expires_at` TTL 6h | AI score đắt để tính, cache 6h đủ freshness |
| **Circuit Breaker** | Polly cho Python AI HTTP client | Python AI down → fallback score, không crash request |
| **Chain of Responsibility** | Quota validation 3 tầng | Mỗi rule độc lập, thêm rule mới không sửa rule cũ |

---

## 12.9 DependencyInjection Registration

```csharp
// Application/DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    // ValidationBehavior, TransactionBehavior — thêm khi implement

    return services;
}

// Infrastructure/DependencyInjection.cs
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services, IConfiguration config)
{
    services.AddDbContext<ApplicationDbContext>(opts =>
        opts.UseSqlServer(config.GetConnectionString("DefaultConnection")));

    // Generic Repository — dùng cho UoW.Repository<T,TKey>() và entity không có specific repo
    services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
    services.AddScoped<IUnitOfWork, UnitOfWork>();

    // Specific Repositories — chỉ register entity cần Include phức tạp
    services.AddScoped<ILoungeShowRepository, LoungeShowRepository>();
    services.AddScoped<ITicketRepository, TicketRepository>();
    services.AddScoped<ILoungeRepository, LoungeRepository>();

    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUserService, CurrentUserService>();
    services.AddScoped<IVnPayService, VnPayService>();
    services.AddScoped<IFcmService, FcmService>();

    services.AddHangfire(cfg =>
        cfg.UseSqlServerStorage(config.GetConnectionString("DefaultConnection")));
    services.AddHangfireServer();

    return services;
}

// Api/Program.cs
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

---

← [04-design-decisions.md](04-design-decisions.md) | Tiếp theo: [06-compliance.md](06-compliance.md) →
