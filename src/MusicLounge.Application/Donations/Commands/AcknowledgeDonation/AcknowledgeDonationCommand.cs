using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Donations.Commands.AcknowledgeDonation;

public sealed record AcknowledgeDonationCommand(int DonationId) : ICommand;
