using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExpenseHub.Api.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/users")
            .WithTags("Admin")
            .RequireAuthorization(AuthPolicies.Admin);

        group.MapGet("/", ListAsync).WithName("ListUsers");
        group.MapPut("/{id}/roles", UpdateRolesAsync).WithName("UpdateUserRoles");

        return app;
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal principal, UserAdminService admin, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await admin.ListUsersAsync(principal.ToUserContext(), cancellationToken));
    }

    private static async Task<IResult> UpdateRolesAsync(
        string id,
        UpdateRolesRequest request,
        ClaimsPrincipal principal,
        UserAdminService admin,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await admin.UpdateRolesAsync(principal.ToUserContext(), id, request, cancellationToken));
    }
}
