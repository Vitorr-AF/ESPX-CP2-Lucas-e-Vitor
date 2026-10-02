using System;
using System.Linq.Expressions;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application;

/// <summary>Builds the query scopes that decide which expenses a caller can see or act on.</summary>
internal static class ExpenseVisibility
{
    /// <summary>
    /// Union of the scopes of every role of the caller: Employee sees its own expenses, Approver the submitted ones,
    /// Finance the approved and paid ones and Auditor everything. Admin alone sees nothing.
    /// </summary>
    public static Expression<Func<Expense, bool>> ForReading(UserContext user)
    {
        string userId = user.UserId;
        bool isEmployee = user.IsInRole(AppRoles.Employee);
        bool isApprover = user.IsInRole(AppRoles.Approver);
        bool isFinance = user.IsInRole(AppRoles.Finance);
        bool isAuditor = user.IsInRole(AppRoles.Auditor);

        return expense => isAuditor
            || (isEmployee && expense.OwnerId == userId)
            || (isApprover && expense.Status == ExpenseStatus.Submitted)
            || (isFinance && (expense.Status == ExpenseStatus.Approved || expense.Status == ExpenseStatus.Paid));
    }

    /// <summary>
    /// Scope used by approve, reject and pay. Everything that already left Draft can be targeted, so repeating a
    /// transition yields a conflict instead of a not found. Drafts of other users stay invisible.
    /// </summary>
    public static Expression<Func<Expense, bool>> ForTransition(UserContext user)
    {
        string userId = user.UserId;

        return expense => expense.Status != ExpenseStatus.Draft || expense.OwnerId == userId;
    }
}
