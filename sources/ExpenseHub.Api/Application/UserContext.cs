using System.Collections.Generic;
using System.Linq;

namespace ExpenseHub.Api.Application;

/// <summary>Identity of the caller, always derived from the authenticated principal.</summary>
internal sealed record UserContext(string UserId, IReadOnlyCollection<string> Roles)
{
    public bool IsInRole(string role)
    {
        return Roles.Contains(role);
    }
}
