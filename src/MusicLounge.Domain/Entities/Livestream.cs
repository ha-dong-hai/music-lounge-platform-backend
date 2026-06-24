using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Livestream : BaseEntity<int>
{
    public int ShowId { get; set; }
    // Secret RTMP key — visible to Owner only, never exposed in public API response
    public string StreamKey { get; set; } = string.Empty;
    // HLS playback URL — set after Cloudflare/Mux initializes the stream
    public string? StreamUrl { get; set; }
    public string? RecordingUrl { get; set; }
    // Null = no rewatch; set to allow replay access until this datetime
    public DateTime? RewatchUntil { get; set; }
    // False = requires a Livestream ticket to watch
    public bool IsFree { get; set; } = false;
    public LivestreamStatus Status { get; set; } = LivestreamStatus.Scheduled;
    public int ViewerCount { get; set; } = 0;
    public int PeakViewerCount { get; set; } = 0;
    public int TotalViews { get; set; } = 0;
    public bool ChatEnabled { get; set; } = true;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    // Set when Admin force-terminates the stream for policy violation
    public int? TerminatedById { get; set; }
    public string? TerminatedReason { get; set; }
    // Stored so EndStream always calls the provider that created the stream,
    // even if config changes after stream was created (cloudflare or mux)
    public string Provider { get; set; } = string.Empty;
    public string? ProviderRef { get; set; }
}
