using FluentValidation;

namespace MusicLounge.Application.Follows.Queries.GetLoungeFollowStatus;

public sealed class GetLoungeFollowStatusQueryValidator : AbstractValidator<GetLoungeFollowStatusQuery>
{
    // Một trang danh sách lớn nhất của hệ thống là 100 — đủ cho mọi màn, và chặn câu IN (...) dài vô hạn.
    public const int ToiDa = 100;

    public GetLoungeFollowStatusQueryValidator()
    {
        RuleFor(x => x.LoungeIds)
            .NotEmpty().WithMessage("Cần ít nhất một loungeIds.")
            .Must(ids => ids.Count <= ToiDa).WithMessage("Hỏi tối đa 100 phòng trà mỗi lần.");
    }
}
