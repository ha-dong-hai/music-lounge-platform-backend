using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Infrastructure;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// Hangfire activates a job class through the DI container. A job that is enqueued or scheduled but
/// never registered therefore fails at RUN time, not at startup — and every one of these jobs is
/// fire-and-forget, so nothing surfaces the failure to a caller. It just silently never happens.
///
/// This codebase has hit that exact bug five times now: EventReminderJob, DonationOverdueCheckJob
/// and LogUserBehaviourJob were each found and fixed individually, then the đợt-2 audit found
/// LivestreamReconnectTimeoutJob and CheckInLivestreamViewerJob still missing. Each fix carried a
/// comment telling the next person to remember; none of them stopped the next occurrence.
///
/// This test replaces that discipline with an assertion: every *Job type in the Application and
/// Infrastructure assemblies must resolve from the real container. Adding a job now either
/// registers it or fails the build.
/// </summary>
[Collection("Integration")]
public sealed class BackgroundJobRegistrationTests
{
    private readonly ApiFactory _factory;

    public BackgroundJobRegistrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void EveryJobClass_IsResolvableFromTheContainer()
    {
        var jobTypes = new[]
            {
                typeof(DependencyInjection).Assembly,                       // MusicLounge.Infrastructure
                typeof(Application.DependencyInjection).Assembly           // MusicLounge.Application
            }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Job", StringComparison.Ordinal))
            .OrderBy(t => t.Name)
            .ToList();

        jobTypes.Should().NotBeEmpty("the scan itself must not silently match nothing");

        using var scope = _factory.Services.CreateScope();

        var unresolvable = jobTypes
            .Where(t => scope.ServiceProvider.GetService(t) is null)
            .Select(t => t.Name)
            .ToList();

        unresolvable.Should().BeEmpty(
            "Hangfire resolves job classes through DI, so an unregistered job throws " +
            "\"No service for type...\" the first time it fires in production — silently, because " +
            "every job here is fire-and-forget. Register it in Infrastructure/DependencyInjection.cs.");
    }
}
