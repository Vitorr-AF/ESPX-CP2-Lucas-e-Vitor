using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseDomainTests
{
    private static readonly DateTime _now = TestData.Now.UtcDateTime;
    private static readonly DateOnly _today = DateOnly.FromDateTime(_now);

    [TestMethod]
    public void CreateDraft_SetsServerControlledFields()
    {
        Expense expense = Expense.CreateDraft("owner-1", "  Lunch with the customer  ", 42.50m, _today, 2, _now);

        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual("owner-1", expense.OwnerId);
        Assert.AreEqual("Lunch with the customer", expense.Description);
        Assert.AreEqual(_now, expense.CreatedAtUtc);
        Assert.AreEqual(_now, expense.UpdatedAtUtc);
        Assert.AreNotEqual(Guid.Empty, expense.Id);
        Assert.IsNull(expense.RejectionReason);
    }

    [TestMethod]
    public void CreateDraft_AcceptsBoundaryValues()
    {
        Expense lowest = Expense.CreateDraft("owner-1", new string('a', 10), 0.01m, _today, null, _now);
        Expense highest = Expense.CreateDraft("owner-1", new string('a', 500), int.MaxValue, _today, null, _now);

        Assert.AreEqual(0.01m, lowest.Amount);
        Assert.AreEqual((decimal)int.MaxValue, highest.Amount);
    }

    [TestMethod]
    public void CreateDraft_RejectsShortDescription()
    {
        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", "too short", 10m, _today, null, _now));
    }

    [TestMethod]
    public void CreateDraft_RejectsDescriptionThatIsOnlyWhitespace()
    {
        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", new string(' ', 20), 10m, _today, null, _now));
    }

    [TestMethod]
    public void CreateDraft_RejectsLongDescription()
    {
        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", new string('a', 501), 10m, _today, null, _now));
    }

    [TestMethod]
    public void CreateDraft_RejectsMissingDescription()
    {
        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", null, 10m, _today, null, _now));
    }

    [TestMethod]
    public void CreateDraft_RejectsAmountBelowMinimum()
    {
        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", "Valid description here", 0.00m, _today, null, _now));
        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", "Valid description here", -5m, _today, null, _now));
    }

    [TestMethod]
    public void CreateDraft_RejectsAmountAboveInt32MaxValue()
    {
        decimal tooMuch = (decimal)int.MaxValue + 0.01m;

        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", "Valid description here", tooMuch, _today, null, _now));
    }

    [TestMethod]
    public void CreateDraft_RejectsMoreThanTwoDecimalPlaces()
    {
        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", "Valid description here", 10.005m, _today, null, _now));
    }

    [TestMethod]
    public void CreateDraft_RejectsFutureDate()
    {
        Assert.ThrowsExactly<ValidationFailedException>(() => Expense.CreateDraft("owner-1", "Valid description here", 10m, _today.AddDays(1), null, _now));
    }

    [TestMethod]
    public void CreateDraft_AcceptsTodayAndPastDates()
    {
        Expense today = Expense.CreateDraft("owner-1", "Valid description here", 10m, _today, null, _now);
        Expense past = Expense.CreateDraft("owner-1", "Valid description here", 10m, _today.AddYears(-1), null, _now);

        Assert.AreEqual(_today, today.ExpenseDate);
        Assert.AreEqual(_today.AddYears(-1), past.ExpenseDate);
    }

    [TestMethod]
    public void UpdateDraft_ChangesFieldsAndDescribesTheDifferences()
    {
        Expense expense = Expense.CreateDraft("owner-1", "Original description", 10m, _today, null, _now);
        DateTime later = _now.AddMinutes(5);

        string changes = expense.UpdateDraft("Updated description", 20m, _today, 1, later);

        Assert.AreEqual("Updated description", expense.Description);
        Assert.AreEqual(20m, expense.Amount);
        Assert.AreEqual(1, expense.CategoryId);
        Assert.AreEqual(later, expense.UpdatedAtUtc);
        Assert.AreEqual(_now, expense.CreatedAtUtc);
        Assert.IsTrue(changes.Contains("Description", StringComparison.Ordinal));
        Assert.IsTrue(changes.Contains("Amount", StringComparison.Ordinal));
        Assert.IsTrue(changes.Contains("CategoryId", StringComparison.Ordinal));
        Assert.IsFalse(changes.Contains("ExpenseDate", StringComparison.Ordinal));
    }

    [TestMethod]
    public void UpdateDraft_ReportsWhenNothingChanged()
    {
        Expense expense = Expense.CreateDraft("owner-1", "Original description", 10m, _today, null, _now);

        string changes = expense.UpdateDraft("Original description", 10m, _today, null, _now);

        Assert.AreEqual("No field changes.", changes);
    }

    [TestMethod]
    public void UpdateDraft_KeepsTheOwnerAndStatus()
    {
        Expense expense = Expense.CreateDraft("owner-1", "Original description", 10m, _today, null, _now);

        expense.UpdateDraft("Updated description", 20m, _today, null, _now);

        Assert.AreEqual("owner-1", expense.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
    }

    [TestMethod]
    public void UpdateDraft_ValidatesTheNewValues()
    {
        Expense expense = Expense.CreateDraft("owner-1", "Original description", 10m, _today, null, _now);

        Assert.ThrowsExactly<ValidationFailedException>(() => expense.UpdateDraft("short", 20m, _today, null, _now));
        Assert.ThrowsExactly<ValidationFailedException>(() => expense.UpdateDraft("Updated description", 0m, _today, null, _now));
        Assert.ThrowsExactly<ValidationFailedException>(() => expense.UpdateDraft("Updated description", 20m, _today.AddDays(2), null, _now));
        Assert.AreEqual("Original description", expense.Description);
    }

    [TestMethod]
    public void Submit_MovesDraftToSubmitted()
    {
        Expense expense = NewDraft();

        expense.Submit(_now.AddMinutes(1));

        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.AreEqual(_now.AddMinutes(1), expense.UpdatedAtUtc);
    }

    [TestMethod]
    public void Approve_MovesSubmittedToApproved()
    {
        Expense expense = NewSubmitted();

        expense.Approve(_now);

        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
    }

    [TestMethod]
    public void Reject_MovesSubmittedToRejectedAndKeepsTheReason()
    {
        Expense expense = NewSubmitted();

        string reason = expense.Reject("  The receipt is missing  ", _now);

        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual("The receipt is missing", reason);
        Assert.AreEqual("The receipt is missing", expense.RejectionReason);
    }

    [TestMethod]
    public void Reject_RequiresAValidReasonAndKeepsTheState()
    {
        Expense expense = NewSubmitted();

        Assert.ThrowsExactly<ValidationFailedException>(() => expense.Reject(null, _now));
        Assert.ThrowsExactly<ValidationFailedException>(() => expense.Reject("too short", _now));
        Assert.ThrowsExactly<ValidationFailedException>(() => expense.Reject(new string('x', 501), _now));
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.IsNull(expense.RejectionReason);
    }

    [TestMethod]
    public void Pay_MovesApprovedToPaidAndBuildsThePaymentRecord()
    {
        Expense expense = NewSubmitted();
        expense.Approve(_now);

        PaymentRecord payment = expense.Pay("finance-1", _now.AddHours(1));

        Assert.AreEqual(ExpenseStatus.Paid, expense.Status);
        Assert.AreEqual(expense.Id, payment.ExpenseId);
        Assert.AreEqual("finance-1", payment.PaidByUserId);
        Assert.AreEqual(expense.Amount, payment.Amount);
        Assert.AreEqual(_now.AddHours(1), payment.PaidAtUtc);
    }

    [TestMethod]
    public void EveryTransitionIncrementsTheVersion()
    {
        Expense expense = NewDraft();
        int version = expense.Version;

        expense.UpdateDraft("Updated description", 20m, _today, null, _now);
        Assert.AreEqual(version + 1, expense.Version);

        expense.Submit(_now);
        Assert.AreEqual(version + 2, expense.Version);

        expense.Approve(_now);
        Assert.AreEqual(version + 3, expense.Version);

        expense.Pay("finance-1", _now);
        Assert.AreEqual(version + 4, expense.Version);
    }

    [TestMethod]
    public void Submit_FailsOutsideDraft()
    {
        Expense submitted = NewSubmitted();
        Expense approved = NewSubmitted();
        approved.Approve(_now);

        Assert.ThrowsExactly<ConflictException>(() => submitted.Submit(_now));
        Assert.ThrowsExactly<ConflictException>(() => approved.Submit(_now));
    }

    [TestMethod]
    public void UpdateDraft_FailsOutsideDraft()
    {
        Expense expense = NewSubmitted();

        Assert.ThrowsExactly<ConflictException>(() => expense.UpdateDraft("Updated description", 20m, _today, null, _now));
    }

    [TestMethod]
    public void Approve_FailsOutsideSubmitted()
    {
        Expense draft = NewDraft();
        Expense approved = NewSubmitted();
        approved.Approve(_now);
        Expense rejected = NewSubmitted();
        rejected.Reject("The receipt is missing", _now);

        Assert.ThrowsExactly<ConflictException>(() => draft.Approve(_now));
        Assert.ThrowsExactly<ConflictException>(() => approved.Approve(_now));
        Assert.ThrowsExactly<ConflictException>(() => rejected.Approve(_now));
    }

    [TestMethod]
    public void Reject_FailsOutsideSubmitted()
    {
        Expense draft = NewDraft();
        Expense approved = NewSubmitted();
        approved.Approve(_now);

        Assert.ThrowsExactly<ConflictException>(() => draft.Reject("The receipt is missing", _now));
        Assert.ThrowsExactly<ConflictException>(() => approved.Reject("The receipt is missing", _now));
    }

    [TestMethod]
    public void Pay_FailsOutsideApproved()
    {
        Expense draft = NewDraft();
        Expense submitted = NewSubmitted();
        Expense rejected = NewSubmitted();
        rejected.Reject("The receipt is missing", _now);
        Expense paid = NewSubmitted();
        paid.Approve(_now);
        paid.Pay("finance-1", _now);

        Assert.ThrowsExactly<ConflictException>(() => draft.Pay("finance-1", _now));
        Assert.ThrowsExactly<ConflictException>(() => submitted.Pay("finance-1", _now));
        Assert.ThrowsExactly<ConflictException>(() => rejected.Pay("finance-1", _now));
        Assert.ThrowsExactly<ConflictException>(() => paid.Pay("finance-1", _now));
    }

    [TestMethod]
    public void RejectedAndPaidAreFinalStates()
    {
        Expense rejected = NewSubmitted();
        rejected.Reject("The receipt is missing", _now);
        Expense paid = NewSubmitted();
        paid.Approve(_now);
        paid.Pay("finance-1", _now);

        foreach (Expense expense in new[] { rejected, paid })
        {
            Assert.ThrowsExactly<ConflictException>(() => expense.UpdateDraft("Updated description", 20m, _today, null, _now));
            Assert.ThrowsExactly<ConflictException>(() => expense.Submit(_now));
            Assert.ThrowsExactly<ConflictException>(() => expense.Approve(_now));
            Assert.ThrowsExactly<ConflictException>(() => expense.Reject("The receipt is missing", _now));
            Assert.ThrowsExactly<ConflictException>(() => expense.Pay("finance-1", _now));
        }
    }

    private static Expense NewDraft()
    {
        return Expense.CreateDraft("owner-1", "Valid description here", 10m, _today, null, _now);
    }

    private static Expense NewSubmitted()
    {
        Expense expense = NewDraft();
        expense.Submit(_now);

        return expense;
    }
}
