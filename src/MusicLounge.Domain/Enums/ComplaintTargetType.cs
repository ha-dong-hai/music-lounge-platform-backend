// CoreFlow: CF5 (Interaction & Feedback), CF6 (Payment & Revenue)
// What the complaint is filed against.
// Used with target_id in the polymorphic complaints table.
namespace MusicLounge.Domain.Enums;

public enum ComplaintTargetType
{
    Event = 1,
    Venue = 2,
    Donation = 3,
    Ticket = 4,
    Penalty = 5
}
