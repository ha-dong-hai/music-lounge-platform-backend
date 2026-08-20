namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// Encrypts short-lived secrets (password-reset tokens, email-verification codes) before they
/// cross into Hangfire's persistent job storage. Hangfire.Enqueue&lt;T&gt; serializes every
/// argument of the call into its SQL Server-backed job table as plaintext JSON — a raw token
/// passed straight through would sit there, readable by DB access alone, for the job's retention
/// window, defeating the point of only ever storing a hash of it on the User row itself.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedText);
}
