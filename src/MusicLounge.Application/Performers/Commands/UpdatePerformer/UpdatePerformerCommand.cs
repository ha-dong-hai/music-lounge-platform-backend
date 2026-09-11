using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.UpdatePerformer;

public sealed record UpdatePerformerCommand(
    int PerformerId,
    string Name,
    string? AvatarUrl,
    string? Bio,
    string Type,
    IReadOnlyList<int> GenreIds,
    // MLACP-364: null = giu nguyen, chuoi rong = xoa. Khac cac truong con lai (thay toan bo) co y: client
    // chua biet truong nay khong duoc vo tinh xoa mat email cua nghe si moi lan sua ho so.
    string? ContactEmail = null
) : ICommand;
