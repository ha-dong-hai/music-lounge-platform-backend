using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Queries.GetPerformerSuggestions;

/// <summary>
/// MLACP-682. Gợi ý nghệ sĩ cho ô tìm kiếm chung của trang công khai (chủ dự án 06/10/2026: "thanh search chỉ đang search
/// được show, không có phòng trà, nghệ sĩ"). Phòng trà đã có đường công khai riêng (<c>GET /lounges?keyword=</c>); nghệ sĩ
/// thì chưa — <c>GET /performers</c> là danh mục nội bộ của chủ phòng trà (RequireOwner).
/// </summary>
public sealed record GetPerformerSuggestionsQuery(string Keyword, int Limit = 5)
    : IQuery<IReadOnlyList<PerformerSuggestionItem>>;

/// <summary>Chỉ những gì trang công khai <c>/performers/{id}</c> vốn đã hiện — KHÔNG có email liên hệ, người tạo.</summary>
public sealed record PerformerSuggestionItem(Guid Id, string Name, string? AvatarUrl);
