namespace MusicLounge.Domain.Enums;

/// <summary>
/// Where a submitted identity or tax document stands with the platform.
///
/// Absent (null) means nothing has been submitted, which is deliberately NOT a member here: a person
/// who has never submitted anything and a person waiting on a decision are different situations, and
/// collapsing them would leave the review queue unable to tell them apart.
/// </summary>
public enum KycReviewStatus
{
    /// <summary>Submitted and waiting on a human. Nothing is unlocked in this state.</summary>
    Pending,
    Approved,
    /// <summary>Turned down with a reason the submitter can read and act on.</summary>
    Rejected
}
