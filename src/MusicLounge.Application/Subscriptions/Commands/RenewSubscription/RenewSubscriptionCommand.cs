using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Subscriptions.DTOs;

namespace MusicLounge.Application.Subscriptions.Commands.RenewSubscription;

// The honest, VNPay-shaped replacement for the old dead OwnerSubscription.AutoRenew field: VNPay's
// token_pay flow still demands a fresh bank OTP/3DS confirmation on every charge (no silent
// merchant-initiated debit), so a truly automatic renewal isn't achievable with this integration —
// see SubscriptionExpiryWarningJob's 30/7/1-day reminders for the notice side of that trade-off.
// This is the convenience half: skip re-browsing the package list, re-use whatever package the
// Owner was last on, and go straight to a fresh VNPay checkout (still one OTP tap, just no
// re-picking).
public sealed record RenewSubscriptionCommand(
    string ClientIpAddress
) : ICommand<SubscriptionPaymentInitiationDto>;
