using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Donations.Commands.HideDonationMessage;

/// <summary>MLACP-360 — gỡ lời nhắn của một donate khỏi livestream.</summary>
public sealed record HideDonationMessageCommand(int DonationId) : ICommand;
