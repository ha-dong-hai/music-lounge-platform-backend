using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Complaints.Commands.ResolveComplaint;

public sealed record ResolveComplaintCommand(
    int ComplaintId,
    string Status,
    string? Resolution,
    string? ResolvedAction
) : ICommand;
