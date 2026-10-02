using ExpenseHub.Api.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseHub.Api.Endpoints;

/// <summary>Authorization policies used by the endpoints. They only check roles; ownership and state live in the services.</summary>
internal static class AuthPolicies
{
    public const string Admin = "AdminOnly";
    public const string Employee = "EmployeeOnly";
    public const string Approver = "ApproverOnly";
    public const string Finance = "FinanceOnly";
    public const string ExpenseReader = "ExpenseReader";

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Admin, policy => policy.RequireRole(AppRoles.Admin))
            .AddPolicy(Employee, policy => policy.RequireRole(AppRoles.Employee))
            .AddPolicy(Approver, policy => policy.RequireRole(AppRoles.Approver))
            .AddPolicy(Finance, policy => policy.RequireRole(AppRoles.Finance))
            .AddPolicy(
                ExpenseReader,
                policy => policy.RequireRole(AppRoles.Employee, AppRoles.Approver, AppRoles.Finance, AppRoles.Auditor));

        return services;
    }
}
