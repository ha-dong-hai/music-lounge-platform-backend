using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeModel3D;

/// <param name="ModelUrl">null = gỡ mô hình thật, quay về dùng scene mẫu dựng bằng code.</param>
public sealed record SetLoungeModel3DCommand(int LoungeId, string? ModelUrl) : ICommand;
