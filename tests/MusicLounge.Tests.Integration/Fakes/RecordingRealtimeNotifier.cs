using System.Collections.Concurrent;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.Fakes;

/// <summary>MLACP-669: ghi lại sự kiện thời gian thực thay vì gửi qua SignalR, để test khẳng định ai nhận gì và lúc nào.</summary>
public sealed class RecordingRealtimeNotifier : IRealtimeNotifier
{
    public ConcurrentQueue<RealtimeEvent> Events { get; } = new();

    public Task PublishAsync(IReadOnlyCollection<RealtimeEvent> events, CancellationToken ct = default)
    {
        foreach (var e in events) Events.Enqueue(e);
        return Task.CompletedTask;
    }
}
