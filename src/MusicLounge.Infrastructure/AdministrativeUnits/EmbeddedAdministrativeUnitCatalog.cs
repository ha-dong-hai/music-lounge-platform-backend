using System.Text.Json;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.AdministrativeUnits;

/// <summary>
/// MLACP-521. Đọc danh mục hành chính từ tệp nhúng <c>don-vi-hanh-chinh-2025.json</c> một lần khi khởi động.
/// Nguồn và kết quả đối chiếu với văn bản chính thức ghi ngay trong tệp (trường "nguon").
/// Tệp hỏng/thiếu thì ném lỗi lúc khởi động — thà không chạy còn hơn chạy với danh mục rỗng rồi từ chối mọi địa chỉ.
/// </summary>
internal sealed class EmbeddedAdministrativeUnitCatalog : IAdministrativeUnitCatalog
{
    internal const string ResourceName = "MusicLounge.AdministrativeUnits.2025.json";

    private readonly Dictionary<string, AdministrativeProvince> _provinces;
    private readonly Dictionary<string, AdministrativeWard> _wards;
    private readonly Dictionary<string, IReadOnlyList<AdministrativeWard>> _wardsByProvince;

    public EmbeddedAdministrativeUnitCatalog()
    {
        using var stream = typeof(EmbeddedAdministrativeUnitCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Thiếu tài nguyên nhúng {ResourceName}.");
        using var doc = JsonDocument.Parse(stream);

        var provinces = new List<AdministrativeProvince>();
        var wardsByProvince = new Dictionary<string, IReadOnlyList<AdministrativeWard>>();
        foreach (var p in doc.RootElement.GetProperty("tinh").EnumerateArray())
        {
            var province = new AdministrativeProvince(
                p.GetProperty("ma").GetString()!, p.GetProperty("ten").GetString()!, p.GetProperty("loai").GetString()!);
            provinces.Add(province);
            wardsByProvince[province.Code] = p.GetProperty("xa").EnumerateArray()
                .Select(w => new AdministrativeWard(w[0].GetString()!, w[1].GetString()!, w[2].GetString()!, province.Code))
                .ToList();
        }

        Provinces = provinces;
        _provinces = provinces.ToDictionary(p => p.Code);
        _wardsByProvince = wardsByProvince;
        _wards = wardsByProvince.Values.SelectMany(w => w).ToDictionary(w => w.Code);
    }

    public IReadOnlyList<AdministrativeProvince> Provinces { get; }

    public AdministrativeProvince? FindProvince(string? code)
        => code is not null && _provinces.TryGetValue(code.Trim(), out var p) ? p : null;

    public AdministrativeWard? FindWard(string? code)
        => code is not null && _wards.TryGetValue(code.Trim(), out var w) ? w : null;

    public IReadOnlyList<AdministrativeWard> WardsOf(string provinceCode)
        => _wardsByProvince.TryGetValue(provinceCode.Trim(), out var w) ? w : [];
}
