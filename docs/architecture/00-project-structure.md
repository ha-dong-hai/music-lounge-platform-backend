# Cấu trúc tổ chức code — MusicLounge

← [README_SU26SE039.md](../README_SU26SE039.md) | Tiếp theo: [01-overview.md](01-overview.md) →

> Đây là sơ đồ cây toàn bộ project khi hoàn chỉnh.
> Hiện tại: 4 projects đã scaffold, chưa có source files. Xem trạng thái tại [07-project-status.md](07-project-status.md).

---

## Toàn bộ repository

```
MusicLounge/
│
├── MusicLounge.sln
├── README_SU26SE039.md              ← Mục lục trung tâm
├── CODING_STANDARDS.md              ← Tiêu chuẩn viết code toàn team
│
├── docs/                            ← Tài liệu thiết kế
│   ├── 00-project-structure.md      ← File này
│   ├── 01-overview.md
│   ├── 02-database.md
│   ├── 03-business-requirements.md
│   ├── 04-design-decisions.md
│   ├── 05-architecture.md
│   ├── 06-compliance.md
│   ├── 07-project-status.md
│   └── 08-changelog.md
│
├── src/                             ← Source code
│   ├── MusicLounge.Domain/
│   ├── MusicLounge.Application/
│   ├── MusicLounge.Infrastructure/
│   └── MusicLounge.Api/
│
└── tests/                           ← (chưa tạo)
    ├── MusicLounge.Domain.Tests/
    ├── MusicLounge.Application.Tests/
    └── MusicLounge.Api.IntegrationTests/
```

---

## Dependency giữa các layer

```
┌─────────────────────────────────────────────────────────┐
│                      MusicLounge.Api                    │
│              Controllers · Middleware · DI              │
└──────────────────────────┬──────────────────────────────┘
                           │ references
┌──────────────────────────▼──────────────────────────────┐
│                 MusicLounge.Infrastructure               │
│     DbContext · Repositories · Services · Jobs · Hubs   │
└──────────────┬────────────────────────┬─────────────────┘
               │ references             │ references
┌──────────────▼──────────┐   ┌─────────▼───────────────┐
│  MusicLounge.Application │   │   MusicLounge.Domain    │
│  Commands · Queries      │   │   Entities · Enums      │
│  Behaviors · Interfaces  ├───►   Domain Events         │
│  Handlers · DTOs         │   │   Exceptions            │
└─────────────────────────┘   └─────────────────────────┘

Quy tắc: Mũi tên chỉ chiều phụ thuộc (→ = "cần đến")
Domain không phụ thuộc ai — pure C# class, không có NuGet ngoài.
```

---

## MusicLounge.Domain

```
MusicLounge.Domain/
│
├── Common/
│   ├── BaseEntity.cs                ← abstract BaseEntity<TKey> { TKey Id }
│   └── AuditableEntity.cs           ← + CreatedAt, UpdatedAt, CreatedBy
│
├── Entities/                        ← 1 file = 1 entity, tên = tên bảng (PascalCase)
│   │
│   ├── [N1 – Định danh]
│   │   ├── User.cs
│   │   ├── LoungeStaff.cs
│   │   └── BankAccount.cs
│   │
│   ├── [N2 – Catalog AI]
│   │   ├── MusicGenre.cs
│   │   ├── Mood.cs
│   │   └── VenueAtmosphere.cs
│   │
│   ├── [N3 – Venue]
│   │   ├── MusicLounge.cs
│   │   ├── LoungeImage.cs
│   │   ├── SeatingArea.cs
│   │   └── EventCategory.cs
│   │
│   ├── [N4 – Subscription]
│   │   ├── SubscriptionPackage.cs
│   │   └── OwnerSubscription.cs
│   │
│   ├── [N5 – Nghệ sĩ, Event & Livestream]
│   │   ├── Performer.cs
│   │   ├── PerformerGenre.cs
│   │   ├── Event.cs
│   │   ├── Performance.cs
│   │   ├── Livestream.cs
│   │   ├── LivestreamChatMessage.cs
│   │   └── SongRequest.cs
│   │
│   ├── [N6 – Tag AI Junctions]
│   │   ├── EventGenre.cs
│   │   ├── EventMood.cs
│   │   ├── EventAtmosphere.cs
│   │   ├── UserFavouriteGenre.cs
│   │   ├── UserFavouriteMood.cs
│   │   └── UserFavouriteAtmosphere.cs
│   │
│   ├── [N7 – Vé]
│   │   ├── TicketTier.cs
│   │   ├── TicketPrice.cs
│   │   ├── Ticket.cs                ← UUID PK (Guid)
│   │   ├── PhysicalTicketDetail.cs
│   │   └── LivestreamTicketDetail.cs
│   │
│   ├── [N8 – Checkout]
│   │   └── TicketHold.cs
│   │
│   ├── [N9 – Giao dịch & Sổ cái]
│   │   ├── Payment.cs
│   │   ├── Donation.cs
│   │   ├── Account.cs
│   │   ├── LedgerEntry.cs           ← append-only, không có UpdatedAt
│   │   └── Settlement.cs
│   │
│   ├── [N10 – Hoàn tiền]
│   │   └── RefundRequest.cs
│   │
│   ├── [N11 – F&B]
│   │   ├── FnbMenuItem.cs
│   │   ├── FnbOrder.cs
│   │   └── OrderItem.cs
│   │
│   ├── [N12 – Tương tác & AI Input]
│   │   ├── EventRating.cs
│   │   ├── Follow.cs
│   │   ├── EventWishlist.cs
│   │   ├── UserBehaviourLog.cs
│   │   └── UserEventScore.cs
│   │
│   ├── [N13 – AI Output]
│   │   └── AiRecommendation.cs
│   │
│   ├── [N14 – Thông báo]
│   │   └── Notification.cs
│   │
│   ├── [N15 – Kiểm duyệt]
│   │   ├── EventModeration.cs
│   │   ├── Complaint.cs
│   │   └── VenuePenalty.cs
│   │
│   ├── [N16 – Cấu hình]
│   │   └── SystemConfig.cs
│   │
│   └── [N17 – AI Custom]
│       ├── CustomCriteria.cs
│       ├── EventCustomValue.cs
│       └── UserCustomPreference.cs
│
├── Enums/                           ← 1 file = 1 enum
│   ├── TicketStatus.cs              ← pending / confirmed / used / cancelled / refunded
│   ├── EventStatus.cs               ← draft / pending_review / published / ongoing / ended / cancelled
│   ├── EventFormat.cs               ← offline / online / hybrid
│   ├── AccessType.cs                ← physical / livestream / combo
│   ├── PurchaseChannel.cs           ← online / offline / both
│   ├── PaymentStatus.cs
│   ├── AccountType.cs               ← gateway / platform / tax / user / performer
│   ├── PenaltyType.cs               ← warning / suspension / ban
│   └── ...                          ← (43 enums tổng)
│
├── Events/                          ← Domain Events (INotification)
│   ├── TicketPaymentConfirmed.cs
│   ├── EventPublished.cs
│   └── DonationReceived.cs
│
└── Exceptions/
    ├── DomainException.cs           ← HTTP 422
    ├── NotFoundException.cs         ← HTTP 404
    ├── ForbiddenException.cs        ← HTTP 403
    ├── ConflictException.cs         ← HTTP 409
    └── ExternalServiceException.cs  ← HTTP 503
```

---

## MusicLounge.Application

```
MusicLounge.Application/
│
├── Common/
│   │
│   ├── Abstractions/
│   │   ├── ICommand.cs              ← ICommand<TResponse> : IRequest<TResponse>
│   │   └── IQuery.cs                ← IQuery<TResponse>   : IRequest<TResponse>
│   │
│   ├── Behaviors/                   ← Thứ tự: Logging → Validation → Transaction
│   │   ├── LoggingBehavior.cs       ← log tên request + elapsed ms
│   │   ├── ValidationBehavior.cs    ← chạy FluentValidation, throw nếu invalid
│   │   └── TransactionBehavior.cs   ← chỉ wrap ICommand (không wrap IQuery)
│   │
│   ├── Exceptions/
│   │   └── ValidationException.cs   ← HTTP 400, chứa danh sách lỗi field
│   │
│   ├── Interfaces/
│   │   ├── IRepository.cs           ← IRepository<T, TKey> where T : BaseEntity<TKey>
│   │   ├── IUnitOfWork.cs           ← Repository<T,TKey>() + SaveChanges + Transaction
│   │   ├── ICurrentUserService.cs   ← UserId, Role, LoungeId từ JWT claims
│   │   ├── IVnPayService.cs         ← tạo URL thanh toán, verify callback
│   │   └── IFcmService.cs           ← gửi push notification
│   │
│   ├── Interfaces/Repositories/     ← Specific repos cho Include phức tạp
│   │   ├── IEventRepository.cs      ← GetByIdWithDetailsAsync, GetPublishedUpcomingAsync
│   │   ├── ITicketRepository.cs     ← GetByQrCodeAsync, GetByBuyerAsync
│   │   └── ILoungeRepository.cs     ← GetByOwnerWithStatsAsync
│   │
│   └── Models/
│       ├── ApiResponse.cs           ← { success, data, message, errors, pagination }
│       └── PaginatedResult.cs       ← { items, page, pageSize, totalCount, totalPages }
│
│   [Feature folders — 1 folder = 1 domain, cấu trúc giống nhau]
│
├── Auth/
│   └── Commands/
│       ├── Login/
│       │   ├── LoginCommand.cs
│       │   ├── LoginCommandHandler.cs
│       │   └── LoginCommandValidator.cs
│       └── RefreshToken/
│           └── ...
│
├── Events/
│   ├── Commands/
│   │   ├── CreateEvent/
│   │   │   ├── CreateEventCommand.cs
│   │   │   ├── CreateEventCommandHandler.cs
│   │   │   └── CreateEventCommandValidator.cs
│   │   ├── PublishEvent/
│   │   │   └── ...
│   │   └── EndEvent/               ← set actual_end + rating_open_until
│   │       └── ...
│   └── Queries/
│       ├── GetEventDetail/
│       │   ├── GetEventDetailQuery.cs
│       │   ├── GetEventDetailQueryHandler.cs
│       │   └── EventDetailDto.cs
│       └── GetPublishedEvents/
│           └── ...
│
├── Tickets/
│   ├── Commands/
│   │   └── PurchaseTicket/
│   │       ├── PurchaseTicketCommand.cs      ← : ICommand<Guid>
│   │       ├── PurchaseTicketCommandHandler.cs
│   │       └── PurchaseTicketCommandValidator.cs
│   ├── Queries/
│   │   └── GetTicketByQr/
│   │       └── ...
│   └── DomainEventHandlers/         ← xử lý TicketPaymentConfirmed
│       ├── WriteTicketLedgerHandler.cs
│       ├── ScheduleSettlementHandler.cs
│       ├── SendFcmConfirmHandler.cs
│       └── BroadcastDonationAlertHandler.cs
│
├── Payments/
│   └── Commands/
│       └── ProcessVnPayCallback/
│           └── ...
│
├── Donations/
│   └── Commands/
│       └── CreateDonation/
│           └── ...
│
├── Livestreams/
│   └── Commands/
│       └── StartLivestream/
│           └── ...
│
├── Users/
│   └── Commands/
│       └── UpdateAiConsent/        ← BVDLCN 2025: opt-in/out AI tracking
│           └── ...
│
├── Lounges/
├── TicketTiers/
├── FnbOrders/
├── Ratings/
└── DependencyInjection.cs          ← AddApplication(): MediatR + FluentValidation + Behaviors
```

---

## MusicLounge.Infrastructure

```
MusicLounge.Infrastructure/
│
├── Persistence/
│   │
│   ├── ApplicationDbContext.cs      ← : DbContext, chứa 54 DbSet<T>
│   ├── UnitOfWork.cs                ← : IUnitOfWork — wrap DbContext, quản lý transaction
│   │
│   ├── Configurations/              ← IEntityTypeConfiguration<T> — 1 file = 1 entity
│   │   ├── UserConfiguration.cs
│   │   ├── EventConfiguration.cs    ← format enum→string, rating_open_until, index
│   │   ├── TicketConfiguration.cs   ← NEWSEQUENTIALID(), buyer_id ON DELETE SET NULL
│   │   ├── LedgerEntryConfiguration.cs  ← không có updated_at (append-only)
│   │   ├── LoungeImageConfiguration.cs  ← partial unique index [is_primary] = 1
│   │   └── ...                      ← (1 file per entity)
│   │
│   ├── Repositories/
│   │   ├── Repository.cs            ← generic base: IRepository<T,TKey> implementation
│   │   ├── EventRepository.cs       ← Include(e => e.Lounge).Include(e => e.Tiers)...
│   │   ├── TicketRepository.cs      ← GetByQrCodeAsync: Include PhysicalDetail
│   │   └── LoungeRepository.cs
│   │
│   └── Migrations/                  ← dotnet ef migrations add <Name>
│       ├── 20260615000000_InitialSchema.cs
│       ├── 20260615000001_AddTicketTiers.cs
│       └── ...
│
├── Services/
│   ├── CurrentUserService.cs        ← : ICurrentUserService — đọc JWT claims từ IHttpContextAccessor
│   ├── VnPayService.cs              ← : IVnPayService — tạo URL + verify HMAC SHA512
│   └── FcmService.cs                ← : IFcmService — gửi FCM via Firebase Admin SDK
│
├── Jobs/                            ← Hangfire job classes
│   ├── ReleaseExpiredHoldsJob.cs    ← chạy mỗi phút, giải phóng hold hết timeout
│   └── ProcessSettlementsJob.cs     ← chạy 2h sáng, nhả tiền settlement đến hạn
│
├── Hubs/                            ← SignalR
│   └── LivestreamHub.cs             ← /hubs/livestream — chat, reactions, donate alert, song request
│
├── Factories/
│   └── LedgerJournalFactory.cs      ← tạo double-entry journal entries (static class)
│
└── DependencyInjection.cs           ← AddInfrastructure(): DbContext + UoW + Repos + Jobs + Hubs
```

---

## MusicLounge.Api

```
MusicLounge.Api/
│
├── Controllers/                     ← Mỏng: nhận → ISender.Send() → trả response
│   ├── AuthController.cs
│   ├── EventsController.cs
│   ├── TicketsController.cs
│   ├── TicketTiersController.cs
│   ├── TicketHoldsController.cs
│   ├── PaymentsController.cs
│   ├── DonationsController.cs
│   ├── LivestreamsController.cs
│   ├── FnbOrdersController.cs
│   ├── LoungesController.cs
│   ├── UsersController.cs
│   ├── RatingsController.cs
│   └── AdminController.cs
│
├── Middleware/
│   ├── GlobalExceptionHandler.cs    ← IExceptionHandler — map exception → HTTP status + ApiResponse
│   └── RequestLoggingMiddleware.cs  ← log mọi request (method, path, status, duration)
│
├── Filters/
│   └── ApiResponseFilter.cs         ← tự động wrap result vào ApiResponse<T>
│
├── Properties/
│   └── launchSettings.json
│
├── appsettings.json                 ← ConnectionStrings, Jwt, VnPay, Firebase, Mux, Hangfire, Serilog
├── appsettings.Development.json     ← override cho môi trường dev
└── Program.cs                       ← builder.Services.AddApplication() + AddInfrastructure() + middleware
```

---

## tests/ (chưa tạo — Sprint sau)

```
tests/
│
├── MusicLounge.Domain.Tests/
│   └── Entities/
│       └── LedgerJournalFactoryTests.cs   ← SUM(debit) == SUM(credit)
│
├── MusicLounge.Application.Tests/
│   ├── Tickets/
│   │   └── PurchaseTicketCommandHandlerTests.cs
│   ├── Events/
│   │   └── CreateEventCommandHandlerTests.cs
│   └── Behaviors/
│       └── ValidationBehaviorTests.cs
│
└── MusicLounge.Api.IntegrationTests/
    └── Controllers/
        └── EventsControllerTests.cs       ← WebApplicationFactory + real SQL Server
```

---

## Luồng xử lý 1 request điển hình

```
HTTP Request
    │
    ▼
[Controller]
    │  .Send(new PurchaseTicketCommand(...))
    ▼
[LoggingBehavior]          ← log tên request, bắt đầu timer
    │
    ▼
[ValidationBehavior]       ← FluentValidation → throw 400 nếu sai
    │
    ▼
[TransactionBehavior]      ← BeginTransaction (chỉ với ICommand)
    │
    ▼
[PurchaseTicketCommandHandler]
    │  uow.Repository<Ticket, Guid>().Add(ticket)
    │  await uow.SaveChangesAsync()
    │  await publisher.Publish(new TicketPaymentConfirmed(...))
    │
    ▼
[Domain Event Handlers]    ← chạy song song sau khi handler return
    ├── WriteTicketLedgerHandler     → ghi ledger_entry
    ├── ScheduleSettlementHandler    → tạo settlement row
    ├── SendFcmConfirmHandler        → enqueue Hangfire job (FCM)
    └── BroadcastDonationAlertHandler→ enqueue Hangfire job (SignalR)
    │
    ▼
[TransactionBehavior]      ← CommitTransaction
    │
    ▼
[LoggingBehavior]          ← log elapsed ms
    │
    ▼
[Controller]
    │  return Created(ApiResponse.Succeed(ticketId))
    ▼
HTTP Response 201
```

---

← [README_SU26SE039.md](../README_SU26SE039.md) | Tiếp theo: [01-overview.md](01-overview.md) →
