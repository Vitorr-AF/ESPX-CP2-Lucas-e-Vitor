using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExpenseHub.Api.Application;

/// <summary>Port over the identity store (users, passwords and roles).</summary>
internal interface IIdentityGateway
{
    /// <summary>Creates a user without roles. Throws on duplicated e-mail or weak password.</summary>
    Task<UserResponse> CreateUserAsync(string email, string password);

    /// <summary>Returns the user when the credentials are valid, otherwise <c>null</c>.</summary>
    Task<AuthenticatedUser?> AuthenticateAsync(string email, string password);

    Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the roles of a user. Throws <c>NotFoundException</c> when the user does not exist.</summary>
    Task<UserResponse> ReplaceRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
}
