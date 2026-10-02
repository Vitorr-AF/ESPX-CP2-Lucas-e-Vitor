using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ExpenseHub.Api.Infrastructure;

/// <summary>ASP.NET Core Identity implementation of <see cref="IIdentityGateway"/>.</summary>
internal sealed class IdentityGateway : IIdentityGateway
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;

    public IdentityGateway(UserManager<ApplicationUser> users, AppDbContext db)
    {
        _users = users;
        _db = db;
    }

    public async Task<UserResponse> CreateUserAsync(string email, string password)
    {
        ApplicationUser user = new() { UserName = email, Email = email };
        IdentityResult result = await _users.CreateAsync(user, password);

        if (result.Succeeded)
        {
            return new UserResponse(user.Id, email, []);
        }

        if (result.Errors.Any(error => error.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            throw new ConflictException("The e-mail is already registered.");
        }

        throw new ValidationFailedException(ToErrors(result));
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(string email, string password)
    {
        ApplicationUser? user = await _users.FindByEmailAsync(email);

        if (user is null || await _users.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await _users.CheckPasswordAsync(user, password))
        {
            await _users.AccessFailedAsync(user);

            return null;
        }

        await _users.ResetAccessFailedCountAsync(user);
        IList<string> roles = await _users.GetRolesAsync(user);

        return new AuthenticatedUser(user.Id, user.Email ?? email, roles.ToArray());
    }

    public async Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .Select(user => new { user.Id, user.Email })
            .ToListAsync(cancellationToken);

        var assignments = await _db.UserRoles
            .AsNoTracking()
            .Join(_db.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);

        ILookup<string, string> rolesByUser = assignments
            .Where(assignment => assignment.Name is not null)
            .ToLookup(assignment => assignment.UserId, assignment => assignment.Name!, StringComparer.Ordinal);

        return users
            .Select(user => new UserResponse(user.Id, user.Email ?? string.Empty, rolesByUser[user.Id].OrderBy(role => role, StringComparer.Ordinal).ToArray()))
            .ToList();
    }

    public async Task<UserResponse> ReplaceRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        ApplicationUser user = await _users.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");

        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        IList<string> current = await _users.GetRolesAsync(user);
        string[] toRemove = current.Except(roles, StringComparer.Ordinal).ToArray();
        string[] toAdd = roles.Except(current, StringComparer.Ordinal).ToArray();

        if (toRemove.Length > 0)
        {
            EnsureSucceeded(await _users.RemoveFromRolesAsync(user, toRemove));
        }

        if (toAdd.Length > 0)
        {
            EnsureSucceeded(await _users.AddToRolesAsync(user, toAdd));
        }

        EnsureSucceeded(await _users.UpdateSecurityStampAsync(user));
        await transaction.CommitAsync(cancellationToken);

        return new UserResponse(user.Id, user.Email ?? string.Empty, roles.OrderBy(role => role, StringComparer.Ordinal).ToArray());
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new ValidationFailedException(ToErrors(result));
        }
    }

    private static Dictionary<string, string[]> ToErrors(IdentityResult result)
    {
        return result.Errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray(), StringComparer.Ordinal);
    }
}
