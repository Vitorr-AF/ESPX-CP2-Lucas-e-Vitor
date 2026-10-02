using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Infrastructure;

/// <summary>
/// Creates the database, the roles and the initial Admin account. Safe to run on every start.
/// The Admin password is read from configuration (<c>Seed:AdminPassword</c>) and never stored in the repository.
/// </summary>
internal static class DataSeeder
{
    private const string DefaultAdminEmail = "admin@expensehub.local";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IServiceProvider provider = scope.ServiceProvider;
        ILogger logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DataSeeder));

        AppDbContext db = provider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        await EnsureRolesAsync(provider.GetRequiredService<RoleManager<IdentityRole>>());
        await EnsureAdminAsync(provider.GetRequiredService<UserManager<ApplicationUser>>(), configuration, logger);
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roles)
    {
        foreach (string role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roles.CreateAsync(new IdentityRole(role)));
            }
        }
    }

    private static async Task EnsureAdminAsync(UserManager<ApplicationUser> users, IConfiguration configuration, ILogger logger)
    {
        string email = configuration["Seed:AdminEmail"] ?? DefaultAdminEmail;
        ApplicationUser? admin = await users.FindByEmailAsync(email);

        if (admin is null)
        {
            string? password = configuration["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Seed:AdminPassword is not configured. The initial Admin account was not created.");

                return;
            }

            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            EnsureSucceeded(await users.CreateAsync(admin, password));
        }

        if (!await users.IsInRoleAsync(admin, AppRoles.Admin))
        {
            EnsureSucceeded(await users.AddToRoleAsync(admin, AppRoles.Admin));
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            string errors = string.Join("; ", result.Errors.Select(error => error.Description));

            throw new InvalidOperationException($"Seeding failed: {errors}");
        }
    }
}
