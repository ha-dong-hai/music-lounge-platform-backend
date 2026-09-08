using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Complaints.DTOs;

namespace MusicLounge.Application.Complaints.Queries.LookupComplaint;

public sealed record LookupComplaintQuery(string Reference) : IQuery<ComplaintLookupDto>;
