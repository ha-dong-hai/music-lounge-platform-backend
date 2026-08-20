using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Security;

/// <summary>Global security-header middleware (Program.cs) applied to every response.</summary>
[Collection("Integration")]
public sealed class SecurityHeadersTests
{
    private readonly ApiFactory _factory;

    public SecurityHeadersTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AnyResponse_IncludesContentSecurityPolicyDenyingEverything()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/health");

        res.Headers.TryGetValues("Content-Security-Policy", out var values).Should().BeTrue();
        var csp = values!.Single();
        csp.Should().Contain("default-src 'none'");
        csp.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task AnyResponse_IncludesOtherStandardSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/health");

        res.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).Should().BeTrue();
        nosniff!.Single().Should().Be("nosniff");

        res.Headers.TryGetValues("X-Frame-Options", out var frameOptions).Should().BeTrue();
        frameOptions!.Single().Should().Be("DENY");
    }
}
