using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Commands.SetPlaybackMode;

public sealed class SetPlaybackModeCommandValidator : AbstractValidator<SetPlaybackModeCommand>
{
    public SetPlaybackModeCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);

        // Đọc thẳng từ enum thay vì chép lại danh sách chuỗi: thêm một chế độ phát mới mà quên sửa
        // validator sẽ thành một giá trị hợp lệ bị từ chối, và không ai nhớ ra chỗ này.
        RuleFor(x => x.PlaybackMode)
            .Must(m => Enum.TryParse<LivestreamPlaybackMode>(m, ignoreCase: true, out _))
            .WithMessage($"PlaybackMode phải là một trong: {string.Join(", ", Enum.GetNames<LivestreamPlaybackMode>())}.");
    }
}
