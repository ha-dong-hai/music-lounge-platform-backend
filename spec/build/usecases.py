"""Use-case catalogue, grouped by feature area.

Held as data rather than as 97 hand-numbered table rows so the identifiers cannot drift:
UC-01 upward is assigned by position at build time, and the count is derived, never typed.
"""

# feature area -> [(use case name, actors, description)]
GROUPS = [
    ("Account & Identity", [
        ("Register account", "Guest", "Create an Audience or Owner account; Staff and Admin cannot self-register."),
        ("Verify email", "Guest", "Enter the emailed one-time code to activate the account and receive the first token."),
        ("Verify phone number", "Audience, Owner", "Confirm a phone number by SMS code, required before certain actions."),
        ("Sign in", "Audience, Owner, Staff, Admin", "Authenticate with email and password and receive a session token."),
        ("Sign in with Google", "Guest", "Authenticate through Google as an alternative to a local password."),
        ("Recover password", "Guest", "Request a reset link and set a new password; all existing sessions are invalidated."),
        ("Edit profile", "Audience, Owner, Staff, Admin", "Update display name, contact details and avatar."),
        ("Upload identity document", "Owner", "Submit identity images required before payout can be verified."),
        ("Set AI preferences", "Audience", "Opt in or out of behaviour logging and personalised recommendations."),
        ("Export my data", "Audience, Owner, Staff", "Download a machine-readable copy of one's own personal data."),
        ("Erase my data", "Audience, Owner, Staff", "Irreversibly anonymise the account while retaining legally required financial records."),
        ("Sign out", "Audience, Owner, Staff, Admin", "End the current session."),
    ]),
    ("Discovery", [
        ("Browse shows", "Guest, Audience", "List published shows with paging."),
        ("Search and filter shows", "Guest, Audience", "Narrow by genre, mood, date, city or price."),
        ("View show detail", "Guest, Audience", "See line-up, tiers, venue and availability."),
        ("View venue detail", "Guest, Audience", "See a venue profile, gallery and upcoming shows."),
        ("Explore 360° virtual tour", "Guest, Audience", "Navigate panorama scenes and hotspots of a venue."),
        ("View performer profile", "Guest, Audience", "See a performer's biography, links and upcoming appearances."),
        ("Follow a venue", "Audience", "Subscribe to a venue's updates."),
        ("Add show to wishlist", "Audience", "Save a show for later."),
        ("View personalised recommendations", "Audience", "See an opt-in ranked feed of suggested shows."),
        ("Rate a show", "Audience", "Leave a star rating and comment after a show ends."),
    ]),
    ("Ticketing", [
        ("Hold tickets", "Audience", "Reserve a tier and quantity for a fixed countdown before payment."),
        ("Pay for a ticket", "Audience", "Complete payment through the gateway and receive a confirmed ticket."),
        ("Cancel a hold", "Audience", "Release a reservation before it expires."),
        ("View my tickets", "Audience", "List confirmed, used and cancelled tickets."),
        ("View ticket QR", "Audience", "Show the code presented at the door."),
        ("Transfer a ticket", "Audience", "Offer a ticket to another account; it is frozen while pending."),
        ("Accept a transferred ticket", "Audience", "Take ownership of a ticket offered by another account."),
        ("Cancel a ticket", "Audience", "Request cancellation, creating a refund request for review."),
        ("Request a refund", "Audience", "Ask for money back against a confirmed ticket."),
        ("Sell a walk-in ticket", "Staff, Owner", "Take cash at the counter and issue a confirmed ticket immediately."),
        ("Check in a ticket", "Staff", "Scan a code at the door and mark the ticket used."),
    ]),
    ("Venue Management", [
        ("Register a venue", "Owner", "Create a venue with address and business licence, pending approval."),
        ("Edit venue profile", "Owner", "Update description, atmosphere, contact and imagery."),
        ("Manage venue gallery", "Owner", "Add or remove showcase photographs."),
        ("Create seating zone", "Owner", "Define a named area with a real physical capacity."),
        ("Arrange seating layout", "Owner", "Position zones on the floor-plan image."),
        ("Upload panorama scene", "Owner", "Add a finished 360° image to the virtual tour."),
        ("Auto-stitch panorama", "Owner", "Submit several photos for the imaging service to assemble."),
        ("Place tour hotspot", "Owner", "Link scenes together or attach an information popup."),
        ("Define custom criteria", "Owner", "Add venue-specific tags used by the recommender."),
        ("Assign staff", "Owner", "Grant an existing account Staff access to this venue."),
        ("Deactivate staff", "Owner", "Revoke a staff assignment."),
        ("Register bank account", "Owner", "Add the payout account for venue earnings."),
    ]),
    ("Show Management", [
        ("Create a show", "Owner", "Draft a show with schedule, description and format."),
        ("Build performer line-up", "Owner", "Add performers and their donation eligibility per slot."),
        ("Create performer profile", "Owner", "Add a performer to the catalogue."),
        ("Define ticket tier", "Owner", "Create a priced access tier with capacity."),
        ("Define pricing window", "Owner", "Set sale start, end and channel for a tier."),
        ("Generate show poster with AI", "Owner", "Produce a poster image within the plan quota."),
        ("Upload show poster", "Owner", "Attach an existing image instead."),
        ("Declare legal permit", "Owner", "Record the performance permit and royalty references."),
        ("Submit show for review", "Owner", "Move a draft into the moderation queue."),
        ("Reschedule a show", "Owner", "Change the date or time of a published show."),
        ("Change show format", "Owner", "Switch between in-person and online, refunding as required."),
        ("Cancel a show", "Owner", "Cancel and refund every confirmed ticket."),
    ]),
    ("Livestream & Donation", [
        ("Start broadcast", "Owner, Staff", "Open the stream for a published show."),
        ("End broadcast", "Owner, Staff", "Close the stream normally."),
        ("Watch a livestream", "Audience", "View a stream as a genuine ticket holder within the device limit."),
        ("Send chat message", "Audience", "Post to the live chat, subject to rate limiting."),
        ("View viewer count", "Owner, Staff", "See how many are currently watching."),
        ("Donate to a performer", "Audience", "Tip a performer in the current line-up."),
        ("Set donation privacy", "Audience", "Choose anonymity, amount visibility and message publication."),
        ("View public donation ticker", "Guest, Audience", "See donations as they clear."),
        ("View donation transparency feed", "Guest, Audience", "Review settled donations across the platform."),
        ("Acknowledge a donation", "Owner", "Confirm receipt of a donation on the performer's behalf."),
        ("Confirm performer payout", "Owner", "Record that the performer's share has been transferred, with evidence."),
        ("View my donations", "Audience", "Track the status of tips given."),
        ("Force-stop a livestream", "Admin", "Terminate a stream that breaches policy."),
    ]),
    ("Food & Beverage", [
        ("Manage F&B menu", "Owner", "Create menus and items with prices."),
        ("Browse menu", "Audience", "View the venue's menu while seated."),
        ("Place an F&B order", "Audience", "Order items to the table."),
        ("Track my order", "Audience", "Follow the order's status."),
        ("Advance order status", "Staff", "Move an order through preparing, served and paid."),
        ("Cancel an order", "Staff", "Void an order before it is paid."),
    ]),
    ("Payments & Finance", [
        ("Process gateway callback", "System", "Confirm or fail a payment from the gateway's server-to-server notification."),
        ("Post ledger journal", "System", "Write a balanced double-entry journal for a confirmed payment."),
        ("Release settlement tranche", "System", "Pay an owner's share on the configured schedule."),
        ("Reconcile with gateway", "System", "Compare platform records against the gateway's own."),
        ("View earnings", "Owner", "See revenue, fees and settlements for a venue."),
        ("Process a refund request", "Admin", "Approve or reject a refund, in full or in part."),
        ("Reverse a donation", "Admin", "Post a compensating journal for a donation."),
        ("Verify a bank account", "Admin", "Mark a payout account verified after off-system reconciliation."),
        ("Check ledger integrity", "Admin", "Confirm every journal balances."),
    ]),
    ("Subscription", [
        ("View subscription plans", "Owner", "Compare available plans and their entitlements."),
        ("Subscribe to a plan", "Owner", "Pay for and activate a plan."),
        ("Renew a subscription", "Owner", "Extend an active plan with a fresh payment."),
        ("Cancel a subscription", "Owner", "End the current plan."),
        ("Manage subscription plans", "Admin", "Create and adjust the plan catalogue."),
    ]),
    ("Moderation & Compliance", [
        ("Approve or reject a venue", "Admin", "Decide whether a venue may operate publicly."),
        ("Score content with AI", "System", "Produce a risk score and recommendation for a submission."),
        ("Review a show", "Admin", "Approve or reject a submitted show."),
        ("Review a livestream", "Admin", "Approve or reject a livestream before it may start."),
        ("Issue a venue penalty", "Admin", "Record a warning, suspension or ban with evidence."),
        ("Appeal a penalty", "Owner", "Contest a penalty within the appeal window."),
        ("Review an appeal", "Admin", "Uphold or overturn a contested penalty."),
        ("File a complaint", "Guest, Audience", "Report an issue against a ticket, show or venue."),
        ("Look up a complaint", "Guest", "Retrieve a complaint by reference number and phone."),
        ("Resolve a complaint", "Admin", "Close a complaint with an outcome and notify the complainant."),
    ]),
    ("Platform Administration", [
        ("Manage users", "Admin", "Search accounts, deactivate and reactivate them."),
        ("Manage taxonomy", "Admin", "Create, edit and delete genre, mood, atmosphere and category tags."),
        ("View platform analytics", "Admin", "Review platform-wide activity and revenue."),
        ("Monitor background jobs", "Admin", "Inspect scheduled job outcomes and trigger a run."),
        ("Receive notifications", "Audience, Owner, Staff, Admin", "Read in-app notifications across surfaces."),
        ("Register device for push", "Audience, Staff", "Enrol a mobile device for push notifications."),
        ("Detect login anomaly", "System", "Alert on a spike of failed sign-ins from one source."),
        ("Detect admin drift", "System", "Alert if an Admin account appears outside the known set."),
        ("Prune stale device tokens", "System", "Remove device registrations that persistently fail."),
    ]),
]


def numbered():
    """[(uc_id, feature, name, actors, description)] with IDs assigned by position."""
    out, n = [], 0
    for feature, cases in GROUPS:
        for name, actors, desc in cases:
            n += 1
            out.append((f"UC-{n:02d}", feature, name, actors, desc))
    return out


TOTAL = sum(len(cases) for _, cases in GROUPS)
