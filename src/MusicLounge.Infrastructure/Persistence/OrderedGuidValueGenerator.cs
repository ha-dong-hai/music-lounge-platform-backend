using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using MusicLounge.Domain.Common;

namespace MusicLounge.Infrastructure.Persistence;

/// <summary>MLACP-515: sinh khoá chính GUID có thứ tự ở phía ứng dụng — xem <see cref="OrderedGuid"/> vì sao không dùng
/// NEWSEQUENTIALID hay SequentialGuidValueGenerator của EF.</summary>
internal sealed class OrderedGuidValueGenerator : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;

    public override Guid Next(EntityEntry entry) => OrderedGuid.New();
}
