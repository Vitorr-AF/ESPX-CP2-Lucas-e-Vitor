using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

/// <summary>Names of the roles known by the application.</summary>
internal static class AppRoles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
    public const string Approver = "Approver";
    public const string Finance = "Finance";
    public const string Auditor = "Auditor";

    public static IReadOnlyList<string> All { get; } = [Admin, Employee, Approver, Finance, Auditor];
}
