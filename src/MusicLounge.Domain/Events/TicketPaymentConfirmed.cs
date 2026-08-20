// Domain Events are defined in the Application layer (MusicLounge.Application.Tickets.Events)
// because INotification belongs to MediatR, which Domain must not reference.
// See: docs/architecture/05-architecture.md — "Domain không phụ thuộc bất kỳ layer nào"
namespace MusicLounge.Domain.Events;
