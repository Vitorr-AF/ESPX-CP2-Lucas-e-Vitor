using System;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Tests for request validator behavior.</summary>
[TestClass]
public sealed class RequestValidatorTests
{
    /// <summary>Validate: accepts a valid expense request.</summary>
    [TestMethod]
    public void Validate_AcceptsAValidExpenseRequest()
    {
        RequestValidator.Validate(TestData.Request());
    }

    /// <summary>Validate: reports every invalid field with camel case keys.</summary>
    [TestMethod]
    public void Validate_ReportsEveryInvalidFieldWithCamelCaseKeys()
    {
        ExpenseRequest request = new() { Description = "short", Amount = 0m, ExpenseDate = null };

        ValidationFailedException error = Assert.ThrowsExactly<ValidationFailedException>(() => RequestValidator.Validate(request));

        Assert.IsTrue(error.Errors.ContainsKey("description"));
        Assert.IsTrue(error.Errors.ContainsKey("amount"));
        Assert.IsTrue(error.Errors.ContainsKey("expenseDate"));
    }

    /// <summary>Validate: accepts the amount boundaries.</summary>
    [TestMethod]
    public void Validate_AcceptsTheAmountBoundaries()
    {
        RequestValidator.Validate(TestData.Request(amount: 0.01m));
        RequestValidator.Validate(TestData.Request(amount: int.MaxValue));
    }

    /// <summary>Validate: rejects amounts outside the boundaries.</summary>
    [TestMethod]
    public void Validate_RejectsAmountsOutsideTheBoundaries()
    {
        Assert.ThrowsExactly<ValidationFailedException>(() => RequestValidator.Validate(TestData.Request(amount: 0.009m)));
        Assert.ThrowsExactly<ValidationFailedException>(() => RequestValidator.Validate(TestData.Request(amount: 2147483647.01m)));
    }

    /// <summary>Validate: enforces the description limits.</summary>
    [TestMethod]
    public void Validate_EnforcesTheDescriptionLimits()
    {
        RequestValidator.Validate(TestData.Request(description: new string('a', 10)));
        RequestValidator.Validate(TestData.Request(description: new string('a', 500)));

        Assert.ThrowsExactly<ValidationFailedException>(() => RequestValidator.Validate(TestData.Request(description: new string('a', 9))));
        Assert.ThrowsExactly<ValidationFailedException>(() => RequestValidator.Validate(TestData.Request(description: new string('a', 501))));
    }

    /// <summary>Validate: requires a reason for rejections.</summary>
    [TestMethod]
    public void Validate_RequiresAReasonForRejections()
    {
        RequestValidator.Validate(TestData.Rejection("A perfectly valid reason"));

        Assert.ThrowsExactly<ValidationFailedException>(() => RequestValidator.Validate(TestData.Rejection(null)));
        Assert.ThrowsExactly<ValidationFailedException>(() => RequestValidator.Validate(TestData.Rejection("short")));
    }

    /// <summary>Expense response: does not expose anything beyond the contract.</summary>
    [TestMethod]
    public void ExpenseResponse_DoesNotExposeAnythingBeyondTheContract()
    {
        DateTime now = TestData.Now.UtcDateTime;
        Expense expense = Expense.CreateDraft("owner-1", "Valid description here", 10m, DateOnly.FromDateTime(now), 1, now);

        ExpenseResponse response = ExpenseResponse.From(expense);

        Assert.AreEqual(expense.Id, response.Id);
        Assert.AreEqual("owner-1", response.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, response.Status);
        Assert.AreEqual(1, response.CategoryId);
    }
}
