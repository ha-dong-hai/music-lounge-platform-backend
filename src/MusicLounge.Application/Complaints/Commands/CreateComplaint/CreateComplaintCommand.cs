using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Complaints.Commands.CreateComplaint;

public sealed record CreateComplaintCommand(
    string TargetType,
    int TargetId,
    string Category,
    string Description,
    string? EvidenceUrls,
    string? ContactPhone
) : ICommand<int>;
