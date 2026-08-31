using System.Globalization;
using System.Text;
using MediatR;
using MusicLounge.Application.Analytics.Common;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.ExportOwnerRevenueReport;

internal sealed class ExportOwnerRevenueReportQueryHandler
    : IRequestHandler<ExportOwnerRevenueReportQuery, ExportedFileDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IOwnerRevenueReportBuilder _builder;

    public ExportOwnerRevenueReportQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IOwnerRevenueReportBuilder builder)
    {
        _uow = uow;
        _currentUser = currentUser;
        _builder = builder;
    }

    public async Task<ExportedFileDto> Handle(ExportOwnerRevenueReportQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xuất báo cáo doanh thu của venue này.");

        // Same builder GetOwnerRevenueReportQueryHandler uses for the on-screen version — the file
        // an Owner downloads can never show different numbers than what they saw before exporting.
        var report = await _builder.BuildAsync(request.LoungeId, request.From, request.To, ct);

        var csv = RenderCsv(lounge.Name, request.From, request.To, report);

        // BOM so Excel (the realistic destination for "nộp cho kế toán") detects UTF-8 instead of
        // misreading diacritics as the system ANSI codepage — a bare UTF8Encoding without BOM opens
        // as mojibake in Excel on a vi-VN/en-US Windows install, which is exactly this report's
        // primary audience.
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv);

        var periodTag = request.From.HasValue || request.To.HasValue
            ? $"{request.From?.ToString("yyyyMMdd") ?? "start"}-{request.To?.ToString("yyyyMMdd") ?? "now"}"
            : "toan-bo";
        var fileName = $"bao-cao-doanh-thu-{Slugify(lounge.Name)}-{periodTag}.csv";

        return new ExportedFileDto(bytes, fileName, "text/csv");
    }

    private static string RenderCsv(
        string loungeName, DateTimeOffset? from, DateTimeOffset? to, OwnerRevenueReportDto report)
    {
        var sb = new StringBuilder();

        sb.AppendLine(Csv("Báo cáo doanh thu", loungeName));
        sb.AppendLine(Csv("Kỳ", $"{FormatDate(from)} - {FormatDate(to)}"));
        sb.AppendLine(Csv("Xuất lúc", DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd HH:mm")));
        sb.AppendLine();

        sb.AppendLine(Csv("Tổng hợp"));
        sb.AppendLine(Csv("Loại doanh thu", "Số tiền (VNĐ)"));
        sb.AppendLine(Csv("Vé", report.TotalTicketRevenue.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("F&B", report.TotalFnbRevenue.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("Donate", report.TotalDonationRevenue.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("Tổng cộng", report.GrandTotal.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine();

        sb.AppendLine(Csv("Theo sự kiện"));
        sb.AppendLine(Csv(
            "Mã sự kiện", "Tên sự kiện", "Ngày diễn", "Doanh thu vé", "Doanh thu F&B", "Doanh thu donate", "Tổng"));
        foreach (var e in report.ByEvent)
        {
            sb.AppendLine(Csv(
                e.ShowId.ToString(CultureInfo.InvariantCulture),
                e.ShowName,
                e.ScheduledStart.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd"),
                e.TicketRevenue.ToString(CultureInfo.InvariantCulture),
                e.FnbRevenue.ToString(CultureInfo.InvariantCulture),
                e.DonationRevenue.ToString(CultureInfo.InvariantCulture),
                e.TotalRevenue.ToString(CultureInfo.InvariantCulture)));
        }
        sb.AppendLine();

        sb.AppendLine(Csv("Theo tháng"));
        sb.AppendLine(Csv(
            "Năm", "Tháng", "Doanh thu vé", "Doanh thu F&B", "Doanh thu donate", "Tổng"));
        foreach (var m in report.ByMonth)
        {
            sb.AppendLine(Csv(
                m.Year.ToString(CultureInfo.InvariantCulture),
                m.Month.ToString(CultureInfo.InvariantCulture),
                m.TicketRevenue.ToString(CultureInfo.InvariantCulture),
                m.FnbRevenue.ToString(CultureInfo.InvariantCulture),
                m.DonationRevenue.ToString(CultureInfo.InvariantCulture),
                m.TotalRevenue.ToString(CultureInfo.InvariantCulture)));
        }

        return sb.ToString();
    }

    private static string FormatDate(DateTimeOffset? d) =>
        d.HasValue ? d.Value.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd") : "—";

    // RFC 4180-style field escaping: quote a field if it contains a comma, quote, or newline;
    // double up any internal quotes. Minimal on purpose — this report has no library dependency
    // (CsvHelper etc.) anywhere in the codebase, and the field set here is simple enough not to
    // need one.
    private static string Csv(params string[] fields) => string.Join(",", fields.Select(EscapeCsvField));

    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    private static string Slugify(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
