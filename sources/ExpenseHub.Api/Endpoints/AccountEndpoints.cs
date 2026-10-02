using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExpenseHub.Api.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithTags("Account")
            .AllowAnonymous();

        app.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithTags("Account")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, AccountService accounts)
    {
        UserResponse user = await accounts.RegisterAsync(request);

        return TypedResults.Json(user, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, AccountService accounts)
    {
        AuthenticatedUser user = await accounts.LoginAsync(request);

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Email),
            .. user.Roles.Select(role => new Claim(ClaimTypes.Role, role)),
        ];

        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme));

        return TypedResults.SignIn(principal, authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
    }
}
