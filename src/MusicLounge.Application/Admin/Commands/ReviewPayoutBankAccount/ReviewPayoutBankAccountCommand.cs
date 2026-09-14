using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Commands.ReviewPayoutBankAccount;

/// <summary>MLACP-395: Admin xác minh (hoặc từ chối) tài khoản nhận tiền quyết toán của một phòng trà.</summary>
/// <param name="Approve">False là từ chối, và khi đó <paramref name="Note"/> bắt buộc.</param>
/// <param name="Note">Bắt buộc khi từ chối — chủ phòng trà cần biết phải sửa gì.</param>
public sealed record ReviewPayoutBankAccountCommand(int BankAccountId, bool Approve, string? Note) : ICommand;
