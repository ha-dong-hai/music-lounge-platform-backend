namespace MusicLounge.Domain.Enums;

public enum ReportTargetType
{
    Show,
    Livestream,
    Rating,

    /// <summary>MLACP-456: mot tin nhan chat cu the trong livestream.</summary>
    ChatMessage
}
