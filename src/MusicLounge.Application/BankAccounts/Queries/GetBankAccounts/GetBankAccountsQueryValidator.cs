using FluentValidation;

namespace MusicLounge.Application.BankAccounts.Queries.GetBankAccounts;

/// <summary>
/// MLACP-405. Tham số id đi qua query dưới dạng <c>int</c>, nên thiếu tham số thì nhận 0 và rơi xuống handler: trả 404
/// "không tìm thấy … 0" hoặc một danh sách rỗng, trong khi lỗi thật là thiếu tham số.
/// </summary>
public sealed class GetBankAccountsQueryValidator : AbstractValidator<GetBankAccountsQuery>
{
    public GetBankAccountsQueryValidator()
    {
        RuleFor(x => x.OwnerId).GreaterThan(0).WithMessage("OwnerId không hợp lệ.");
    }
}
