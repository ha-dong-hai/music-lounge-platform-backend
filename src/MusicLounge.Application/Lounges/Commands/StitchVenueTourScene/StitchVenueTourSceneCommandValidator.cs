using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.StitchVenueTourScene;

public sealed class StitchVenueTourSceneCommandValidator : AbstractValidator<StitchVenueTourSceneCommand>
{
    public StitchVenueTourSceneCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.Name).MaximumLength(100);

        RuleFor(x => x.SourceImageUrls)
            .NotEmpty().WithMessage("Cần ít nhất 2 ảnh để ghép panorama.")
            .Must(urls => urls.Count >= 2).WithMessage("Cần ít nhất 2 ảnh để ghép panorama.")
            .Must(urls => urls.Count <= 20).WithMessage("Tối đa 20 ảnh cho mỗi lần ghép.");

        // SSRF gate: the panorama-stitcher service fetches whatever URL it's given
        // (requests.get) with no network restriction of its own — without this check, an Owner
        // could pass an internal address (cloud metadata endpoint, internal admin service) and
        // have OUR server request it on their behalf. Requiring a relative "/uploads/..." path
        // forces every source image through the existing authenticated upload endpoint first
        // (POST /uploads/images), which is the only thing that can produce this shape of value -
        // an allowlist by construction, not a blocklist trying to enumerate bad hosts.
        RuleForEach(x => x.SourceImageUrls)
            .NotEmpty()
            .MaximumLength(500)
            .Must(url => url.StartsWith("/uploads/", StringComparison.Ordinal))
            .WithMessage("Ảnh phải được tải lên qua endpoint upload (POST /uploads/images) trước — không chấp nhận URL bên ngoài.");
    }
}
