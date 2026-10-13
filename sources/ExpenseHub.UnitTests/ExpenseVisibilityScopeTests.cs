using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Checks the query scopes directly, as they are the only thing that filters data before materialization.</summary>
[TestClass]
public sealed class ExpenseVisibilityScopeTests
{
    private readonly List<Expense> _expenses = [];

    /// <summary>Initializes a new instance of the <see cref="ExpenseVisibilityScopeTests"/> class.</summary>
    public ExpenseVisibilityScopeTests()
    {
        foreach (ExpenseStatus status in Enum.GetValues<ExpenseStatus>())
        {
            _expenses.Add(Build("emp-1", status));
            _expenses.Add(Build("emp-2", status));
        }
    }

    /// <summary>For reading: employee: matches only owned expenses in any state.</summary>
    [TestMethod]
    public void ForReading_Employee_MatchesOnlyOwnedExpensesInAnyState()
    {
        List<Expense> visible = _expenses.AsQueryable().Where(ExpenseVisibility.ForReading(TestData.Employee("emp-1"))).ToList();

        Assert.HasCount(5, visible);
        Assert.IsTrue(visible.All(expense => expense.OwnerId == "emp-1"));
    }

    /// <summary>For reading: approver: matches submitted only.</summary>
    [TestMethod]
    public void ForReading_Approver_MatchesSubmittedOnly()
    {
        List<Expense> visible = _expenses.AsQueryable().Where(ExpenseVisibility.ForReading(TestData.Approver())).ToList();

        Assert.HasCount(2, visible);
        Assert.IsTrue(visible.All(expense => expense.Status == ExpenseStatus.Submitted));
    }

    /// <summary>For reading: finance: matches approved and paid only.</summary>
    [TestMethod]
    public void ForReading_Finance_MatchesApprovedAndPaidOnly()
    {
        List<Expense> visible = _expenses.AsQueryable().Where(ExpenseVisibility.ForReading(TestData.Finance())).ToList();

        Assert.HasCount(4, visible);
        Assert.IsTrue(visible.All(expense => expense.Status is ExpenseStatus.Approved or ExpenseStatus.Paid));
    }

    /// <summary>For reading: auditor: matches everything.</summary>
    [TestMethod]
    public void ForReading_Auditor_MatchesEverything()
    {
        List<Expense> visible = _expenses.AsQueryable().Where(ExpenseVisibility.ForReading(TestData.Auditor())).ToList();

        Assert.HasCount(_expenses.Count, visible);
    }

    /// <summary>For reading: admin or no roles: matches nothing.</summary>
    [TestMethod]
    public void ForReading_AdminOrNoRoles_MatchesNothing()
    {
        Assert.IsEmpty(_expenses.AsQueryable().Where(ExpenseVisibility.ForReading(TestData.Admin())).ToList());
        Assert.IsEmpty(_expenses.AsQueryable().Where(ExpenseVisibility.ForReading(TestData.User("nobody-1"))).ToList());
    }

    /// <summary>For transition: hides drafts of other users only.</summary>
    [TestMethod]
    public void ForTransition_HidesDraftsOfOtherUsersOnly()
    {
        List<Expense> visible = _expenses.AsQueryable().Where(ExpenseVisibility.ForTransition(TestData.Approver("emp-1"))).ToList();

        Assert.HasCount(9, visible);
        Assert.IsFalse(visible.Any(expense => expense.Status == ExpenseStatus.Draft && expense.OwnerId == "emp-2"));
        Assert.IsTrue(visible.Any(expense => expense.Status == ExpenseStatus.Draft && expense.OwnerId == "emp-1"));
    }

    private static Expense Build(string ownerId, ExpenseStatus status)
    {
        DateTime now = TestData.Now.UtcDateTime;
        Expense expense = Expense.CreateDraft(ownerId, "Valid description here", 10m, DateOnly.FromDateTime(now), null, now);

        if (status is ExpenseStatus.Draft)
        {
            return expense;
        }

        expense.Submit(now);

        switch (status)
        {
            case ExpenseStatus.Approved:
                expense.Approve(now);
                break;
            case ExpenseStatus.Rejected:
                expense.Reject("The receipt is missing", now);
                break;
            case ExpenseStatus.Paid:
                expense.Approve(now);
                expense.Pay("finance-1", now);
                break;
            default:
                break;
        }

        return expense;
    }
}
