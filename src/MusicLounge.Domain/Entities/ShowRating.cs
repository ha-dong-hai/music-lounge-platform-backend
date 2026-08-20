// CoreFlow: CF5 (Interaction & Feedback)
// A star rating and optional review submitted by an audience member after attending a show.
// Only allowed for users who have a checked-in ticket for the show.
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class ShowRating : BaseEntity<int>
{
    public int ShowId { get; set; }
    // Nullable — SET NULL when reviewer account is deleted (BVDLCN 2025)
    public int? UserId { get; set; }
    public short Stars { get; set; }
    public string? ReviewText { get; set; }
    // Soft-delete flag — Admin can remove inappropriate reviews without losing the record
    public bool IsRemoved { get; set; } = false;
    public string? RemovedReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
