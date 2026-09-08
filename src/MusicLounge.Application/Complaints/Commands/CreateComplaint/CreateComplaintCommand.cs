using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Complaints.DTOs;

namespace MusicLounge.Application.Complaints.Commands.CreateComplaint;

public sealed record CreateComplaintCommand(
    string TargetType,
    int TargetId,
    string Category,
    string Description,
    string? EvidenceUrls,
    string? ContactPhone
) : ICommand<ComplaintCreatedDto>;
