using MusicLounge.Domain.Common;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// MLACP-515: id số viết thẳng trong test (Id = TestId.Of(42), OwnerId = 5...) đổi sang GUID bằng MỘT công thức xác định — cùng số luôn
/// ra cùng GUID, nên test viết OwnerId = TestId.Of(5) để trỏ tới user Id = TestId.Of(5) vẫn khớp nhau như khi còn là số. Dùng chung công thức với
/// dữ liệu thật (<see cref="OrderedGuid.FromLegacy"/>) nên thứ tự sắp theo Id cũng giữ như cũ (số nhỏ đứng trước).
/// </summary>
public static class TestId
{
    public static Guid Of(long n) => OrderedGuid.FromLegacy("test", n);
}
