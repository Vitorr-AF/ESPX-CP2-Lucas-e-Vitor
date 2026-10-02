using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application;

/// <summary>Administration of users and roles. Restricted to the Admin role.</summary>
internal sealed class UserAdminService
{
    private readonly IIdentityGateway _identity;

    public UserAdminService(IIdentityGateway identity)
    {
        _identity = identity;
    }

    public async Task<IReadOnlyList<UserResponse>> ListUsersAsync(UserContext actor, CancellationToken cancellationToken)
    {
        RequireAdmin(actor);

        return await _identity.ListUsersAsync(cancellationToken);
    }

    public async Task<UserResponse> UpdateRolesAsync(
        UserContext actor,
        string targetUserId,
        UpdateRolesRequest request,
        CancellationToken cancellationToken)
    {
        RequireAdmin(actor);
        RequestValidator.Validate(request);

        List<string> roles = NormalizeRoles(request.Roles!);

        bool removesOwnAdminRole = string.Equals(actor.UserId, targetUserId, StringComparison.Ordinal)
            && !roles.Contains(AppRoles.Admin);

        if (removesOwnAdminRole)
        {
            throw new ValidationFailedException("roles", "Admins cannot remove their own Admin role.");
        }

        return await _identity.ReplaceRolesAsync(targetUserId, roles, cancellationToken);
    }

    private static void RequireAdmin(UserContext actor)
    {
        if (!actor.IsInRole(AppRoles.Admin))
        {
            throw new ForbiddenException("Only administrators can perform this operation.");
        }
    }

    private static List<string> NormalizeRoles(IReadOnlyList<string> requested)
    {
        List<string> normalized = [];

        foreach (string role in requested)
        {
            string? known = AppRoles.All.FirstOrDefault(candidate => string.Equals(candidate, role?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (known is null)
            {
                throw new ValidationFailedException("roles", "Unknown role.");
            }

            if (!normalized.Contains(known))
            {
                normalized.Add(known);
            }
        }

        return normalized;
    }
}
