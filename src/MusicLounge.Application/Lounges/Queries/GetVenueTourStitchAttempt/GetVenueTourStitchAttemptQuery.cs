using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Lounges.DTOs;

namespace MusicLounge.Application.Lounges.Queries.GetVenueTourStitchAttempt;

// Owner polls this after StitchTourScene returns an attempt id, since stitching now runs in the
// background (see StitchVenueTourSceneCommandHandler) instead of blocking the original request.
public sealed record GetVenueTourStitchAttemptQuery(int LoungeId, int AttemptId) : IQuery<VenueTourStitchAttemptDto>;
