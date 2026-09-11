using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Donations.Queries.GetPerformerDonationSummary;

internal sealed class GetPerformerDonationSummaryQueryHandler
    : IRequestHandler<GetPerformerDonationSummaryQuery, PerformerDonationSummaryDto>
{
    private readonly IDonationRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public GetPerformerDonationSummaryQueryHandler(IDonationRepository repo, IUnitOfWork uow, ISystemConfigService config)
    {
        _repo = repo;
        _uow = uow;
        _config = config;
    }

    public async Task<PerformerDonationSummaryDto> Handle(GetPerformerDonationSummaryQuery request, CancellationToken ct)
    {
        var performer = await _uow.Repository<Performer, int>().GetByIdAsync(request.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), request.PerformerId);

        // Tổng hợp cần mọi khoản của nghệ sĩ, không chỉ một trang — số khoản mỗi nghệ sĩ có giới hạn tự
        // nhiên (theo buổi diễn), không phải một nhật ký tăng vô hạn.
        var rows = await _repo.ListPublicByPerformerAsync(request.PerformerId, ct);
        var entries = await PublicDonationStatement.BuildAsync(_uow, _config, rows, ct);

        return PublicDonationStatement.Summarize(
            performer.Id, performer.Name, entries, await PublicDonationStatement.PolicyAsync(_config, ct));
    }
}
