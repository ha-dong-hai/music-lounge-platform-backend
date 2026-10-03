using FluentValidation;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Catalog.Commands.SetMusicGenreImage;

public sealed class SetMusicGenreImageCommandValidator : AbstractValidator<SetMusicGenreImageCommand>
{
    public SetMusicGenreImageCommandValidator(IFileStorageService fileStorage)
    {
        RuleFor(x => x.Id).NotEmpty();

        // Chỉ nhận ảnh do chính endpoint upload của ta trả về (POST /uploads/images) — cùng cổng với ảnh ghép panorama.
        // URL ngoài thì trang chủ công khai sẽ nhúng ảnh từ một máy chủ ta không kiểm soát (ảnh có thể bị thay nội dung
        // sau khi duyệt, hoặc bị dùng để dò người xem).
        //
        // Không qua IImageModerationGate: cổng đó dành cho ảnh do Owner tải lên; ở đây chỉ Admin đặt ảnh.
        When(x => x.ImageUrl is not null, () =>
            RuleFor(x => x.ImageUrl!)
                .NotEmpty()
                .MaximumLength(500)
                .Must(fileStorage.IsOwnUploadUrl)
                .WithMessage("Ảnh phải được tải lên qua POST /uploads/images trước — không nhận đường dẫn ảnh bên ngoài."));
    }
}
