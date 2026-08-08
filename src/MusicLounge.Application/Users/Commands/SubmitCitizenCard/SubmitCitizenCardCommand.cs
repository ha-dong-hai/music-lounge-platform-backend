using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.SubmitCitizenCard;

public sealed record SubmitCitizenCardCommand(
    string CitizenCardNumber,
    string FrontImageUrl,
    string BackImageUrl) : ICommand;
