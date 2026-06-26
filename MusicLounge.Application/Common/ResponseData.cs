using System;

namespace MusicLounge.Application.Common;

public class ResponseData<T>
{
    public int result { get; set; }
    public long time { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public T? data { get; set; }
    public object? data2nd { get; set; }
    public ErrorDetail error { get; set; } = new();
}
