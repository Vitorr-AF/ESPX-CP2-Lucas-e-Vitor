using System;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests.Support;

/// <summary>Factories for callers, requests and a ready-to-use service graph.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    public static UserContext User(string id, params string[] roles)
    {
        return new UserContext(id, roles);
    }

    public static UserContext Employee(string id = "employee-1")
    {
        return User(id, AppRoles.Employee);
    }

    public static UserContext Approver(string id = "approver-1")
    {
        return User(id, AppRoles.Approver);
    }

    public static UserContext Finance(string id = "finance-1")
    {
        return User(id, AppRoles.Finance);
    }

    public static UserContext Auditor(string id = "auditor-1")
    {
        return User(id, AppRoles.Auditor);
    }

    public static UserContext Admin(string id = "admin-1")
    {
        return User(id, AppRoles.Admin);
    }

    public static ExpenseRequest Request(
        string? description = "Taxi from the airport to the client",
        decimal? amount = 150.75m,
        DateOnly? expenseDate = null,
        int? categoryId = null)
    {
        return new ExpenseRequest
        {
            Description = description,
            Amount = amount,
            ExpenseDate = expenseDate ?? DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-1),
            CategoryId = categoryId,
        };
    }

    public static RejectExpenseRequest Rejection(string? reason = "Missing the receipt for this expense")
    {
        return new RejectExpenseRequest { Reason = reason };
    }
}
