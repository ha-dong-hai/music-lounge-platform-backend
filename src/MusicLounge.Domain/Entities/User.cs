using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class User : Common.AuditableEntity<int>
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Audience;
    public string AuthProvider { get; set; } = "local";     // "local" | "google"
    public string? GoogleId { get; set; }
    public bool IsActive { get; set; } = true;
    // Luật 91/2025/QH15 Điều 19 + Luật Kế toán (10-year retention on accounting records): erasure
    // scrubs identifying fields on this row in place rather than deleting it — Tickets/Donations/
    // Payments/Settlements/LedgerEntries/OwnerSubscription/MusicLounge/VenuePenalty/SystemConfig
    // FKs still resolve to this Id, so financial/audit history stays intact and referentially valid,
    // but no longer identifies a natural person. Null = never erased; set = irreversible, distinct
    // from IsActive (which DeactivateMyAccountCommand also flips false, but reversibly).
    public DateTimeOffset? DataErasedAt { get; set; }
    public bool PhoneVerified { get; set; } = false;   // NĐ 147/2024
    public string? PhoneVerificationCodeHash { get; set; }      // SHA256 hex của mã OTP 6 số — không lưu mã thô
    public DateTimeOffset? PhoneVerificationCodeExpiresAt { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool AiConsent { get; set; } = false;
    // Luật 91/2025/QH15's lawful-basis requirement for the DSAR machinery above — was previously
    // entirely absent (no field, no document, no endpoint) despite erasure/export being implemented
    // carefully. Set only on explicit, non-pre-ticked affirmative consent at registration (both
    // local and Google sign-up), never defaulted true. TermsVersion snapshots which version was
    // agreed to (system_config current_terms_version) so a later document change doesn't silently
    // retroact onto users who agreed to an earlier one.
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public string? TermsVersion { get; set; }
    public string? PasswordResetTokenHash { get; set; }        // SHA256 hex cua token thô gửi qua email — không lưu token thô
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string? EmailVerificationCodeHash { get; set; }      // SHA256 hex cua ma OTP 6 so — khong luu ma tho
    public DateTimeOffset? EmailVerificationCodeExpiresAt { get; set; }
    // Change-email flow reuses EmailVerificationCodeHash/ExpiresAt above (same OTP mechanism as
    // initial registration) but must not overwrite the current, already-verified Email until the
    // OTP sent to the NEW address is actually confirmed — this holds the candidate in the meantime.
    public string? PendingEmail { get; set; }
    // CMND (9 so) hoac CCCD (12 so) — ma hoa tai nghi (IPiiEncryptionService), khong luu plaintext.
    // Ma hoa khong deterministic nen khong the doi chieu/unique truc tiep tren cot nay — dung
    // CitizenCardNumberHash (SHA256, deterministic) cho viec do.
    public string? CitizenCardNumber { get; set; }
    public string? CitizenCardNumberHash { get; set; }
    public string? CitizenCardFrontImageUrl { get; set; }
    public string? CitizenCardBackImageUrl { get; set; }
    public DateTimeOffset? CitizenCardSubmittedAt { get; set; }

    // Embedded in every issued JWT as "sec_stamp" and re-checked against this column on every
    // authenticated request (JwtBearerEvents.OnTokenValidated) — rotating it is how a password
    // reset revokes tokens issued before the reset, since the JWTs themselves are stateless and
    // otherwise stay valid until they naturally expire.
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    // Brute-force lockout (IAuthAttemptTracker) — shared across Login (wrong password) and
    // VerifyEmail (wrong OTP), since both are guessing attacks against the same account.
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }

    public ICollection<UserFavouriteGenre> FavouriteGenres { get; set; } = [];
    public ICollection<UserFavouriteMood> FavouriteMoods { get; set; } = [];
    public ICollection<UserFavouriteAtmosphere> FavouriteAtmospheres { get; set; } = [];
    public ICollection<ShowWishlist> Wishlists { get; set; } = [];
    public ICollection<Follow> Follows { get; set; } = [];
    public ICollection<UserBehaviourLog> BehaviourLogs { get; set; } = [];
    public ICollection<AiRecommendation> AiRecommendations { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<TicketHold> TicketHolds { get; set; } = [];
}
