using FluentValidation;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerArtistDonationStats;

public sealed class GetOwnerArtistDonationStatsQueryValidator
    : AbstractValidator<GetOwnerArtistDonationStatsQuery>
{
    public GetOwnerArtistDonationStatsQueryValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0).WithMessage("LoungeId không hợp lệ.");
    }
}
