using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence;

public static class DefaultAdminAccountSeeder
{
    private const string DefaultAdminEmail = "adminmusiclounge@gmail.com";
    private const string DefaultAdminPassword = "12345612";
    private const string DefaultAdminRole = "Admin";
    private const string DefaultAdminFullName = "Music Lounge Admin";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasherService = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        var email = DefaultAdminEmail.Trim().ToLowerInvariant();
        var isExists = await context.Users.AnyAsync(x => x.Email == email);
        if (isExists)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = email,
            PasswordHash = passwordHasherService.HashPassword(DefaultAdminPassword),
            FullName = DefaultAdminFullName,
            Role = DefaultAdminRole,
            AuthProvider = "SystemSeed",
            IsActive = true,
            EmailVerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
    }
}
