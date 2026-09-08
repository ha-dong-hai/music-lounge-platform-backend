using FluentValidation;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Lounges.Commands.StitchVenueTourScene;

public sealed class StitchVenueTourSceneCommandValidator : AbstractValidator<StitchVenueTourSceneCommand>
{
    public StitchVenueTourSceneCommandValidator(IFileStorageService fileStorage)
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
        // have OUR server request it on their behalf. Every source image must therefore be one our
        // own authenticated upload endpoint produced: an allowlist by construction, not a blocklist
        // trying to enumerate bad hosts.
        //
        // The storage layer answers this rather than a string check here, because what a URL we
        // issued looks like depends on where files are being kept. This used to test for a leading
        // "/uploads/", which was the same test only while files lived on local disk — once storage
        // moved to Firebase that shape stopped appearing and the gate would have refused every
        // legitimate image, taking tour stitching down with it.
        RuleForEach(x => x.SourceImageUrls)
            .NotEmpty()
            .MaximumLength(500)
            .Must(fileStorage.IsOwnUploadUrl)
            .WithMessage("Ảnh phải được tải lên qua endpoint upload (POST /uploads/images) trước — không chấp nhận URL bên ngoài.");
    }
}
