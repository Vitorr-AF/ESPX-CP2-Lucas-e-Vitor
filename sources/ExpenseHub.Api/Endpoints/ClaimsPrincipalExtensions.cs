using System;
using System.Linq;
using System.Security.Claims;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Endpoints;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>Builds the caller identity exclusively from the authenticated principal.</summary>
    public static UserContext ToUserContext(this ClaimsPrincipal principal)
    {
        string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedException("The access token does not identify a user.");
        }

        string[] roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new UserContext(userId, roles);
    }
}
