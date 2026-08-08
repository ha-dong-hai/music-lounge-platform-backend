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
    public bool PhoneVerified { get; set; } = false;   // NĐ 147/2024
    public DateOnly? DateOfBirth { get; set; }
    public bool AiConsent { get; set; } = false;
    public string? PasswordResetTokenHash { get; set; }        // SHA256 hex cua token thô gửi qua email — không lưu token thô
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string? EmailVerificationCodeHash { get; set; }      // SHA256 hex cua ma OTP 6 so — khong luu ma tho
    public DateTimeOffset? EmailVerificationCodeExpiresAt { get; set; }
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
