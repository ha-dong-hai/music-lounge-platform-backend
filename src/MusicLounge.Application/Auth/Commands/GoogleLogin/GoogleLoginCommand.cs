using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Auth.Commands.GoogleLogin;

public sealed record GoogleLoginCommand(
    string IdToken,
    // Only meaningful (and required — see validator) the first time this Google identity is seen,
    // i.e. when this call creates a brand-new User row. An existing user re-logging in via Google
    // doesn't need to re-consent, so the FE only needs to surface a consent checkbox on a NEW
    // Google sign-up, not on every login.
    bool AcceptTerms = false
) : ICommand<AuthResultDto>;
