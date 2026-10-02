using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerArtistDonationStats;

public sealed class GetOwnerArtistDonationStatsQueryValidator
    : AbstractValidator<GetOwnerArtistDonationStatsQuery>
{
    public GetOwnerArtistDonationStatsQueryValidator()
    {
        RuleFor(x => x.LoungeId).NotEmpty().WithMessage("LoungeId không hợp lệ.");
    }
}
