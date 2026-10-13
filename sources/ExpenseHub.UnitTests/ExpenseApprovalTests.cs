using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Tests for expense approval behavior.</summary>
[TestClass]
public sealed class ExpenseApprovalTests
{
    private readonly ExpenseWorld _world = new();

    /// <summary>Approve: moves submitted to approved and records the approver.</summary>
    [TestMethod]
    public async Task Approve_MovesSubmittedToApprovedAndRecordsTheApprover()
    {
        ExpenseResponse submitted = await _world.SubmittedAsync(TestData.Employee("emp-1"));

        ExpenseResponse approved = await _world.Service.ApproveAsync(TestData.Approver("app-1"), submitted.Id, CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Approved, approved.Status);
        ExpenseHistory entry = _world.Repository.History[^1];
        Assert.AreEqual(ExpenseAction.Approved, entry.Action);
        Assert.AreEqual("app-1", entry.ActorId);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Approved, entry.NewStatus);
        Assert.AreEqual(TestData.Now.UtcDateTime, entry.OccurredAtUtc);
    }

    /// <summary>Approve: is forbidden for the owner.</summary>
    [TestMethod]
    public async Task Approve_IsForbiddenForTheOwner()
    {
        UserContext employeeAndApprover = TestData.User("both-1", AppRoles.Employee, AppRoles.Approver);
        ExpenseResponse submitted = await _world.SubmittedAsync(employeeAndApprover);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => _world.Service.ApproveAsync(employeeAndApprover, submitted.Id, CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Submitted, _world.Repository.Expenses[0].Status);
    }

    /// <summary>Approve: twice: is conflict and does not duplicate history.</summary>
    [TestMethod]
    public async Task Approve_Twice_IsConflictAndDoesNotDuplicateHistory()
    {
        ExpenseResponse submitted = await _world.SubmittedAsync(TestData.Employee());
        UserContext approver = TestData.Approver();
        await _world.Service.ApproveAsync(approver, submitted.Id, CancellationToken.None);
        int historyCount = _world.Repository.History.Count;

        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.ApproveAsync(approver, submitted.Id, CancellationToken.None));

        Assert.HasCount(historyCount, _world.Repository.History);
    }

    /// <summary>Approve: is conflict for rejected and paid expenses.</summary>
    [TestMethod]
    public async Task Approve_IsConflictForRejectedAndPaidExpenses()
    {
        UserContext approver = TestData.Approver();
        ExpenseResponse rejected = await _world.RejectedAsync(TestData.Employee("emp-1"), approver);
        ExpenseResponse paid = await _world.PaidAsync(TestData.Employee("emp-2"), approver, TestData.Finance());

        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.ApproveAsync(approver, rejected.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.ApproveAsync(approver, paid.Id, CancellationToken.None));
    }

    /// <summary>Approve: does not reveal drafts of other users.</summary>
    [TestMethod]
    public async Task Approve_DoesNotRevealDraftsOfOtherUsers()
    {
        ExpenseResponse draft = await _world.DraftAsync(TestData.Employee("emp-1"));

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.ApproveAsync(TestData.Approver(), draft.Id, CancellationToken.None));
    }

    /// <summary>Approve: is not found for unknown expenses.</summary>
    [TestMethod]
    public async Task Approve_IsNotFoundForUnknownExpenses()
    {
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.ApproveAsync(TestData.Approver(), Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>Reject: moves submitted to rejected and persists the reason.</summary>
    [TestMethod]
    public async Task Reject_MovesSubmittedToRejectedAndPersistsTheReason()
    {
        ExpenseResponse submitted = await _world.SubmittedAsync(TestData.Employee());

        ExpenseResponse rejected = await _world.Service.RejectAsync(
            TestData.Approver("app-1"),
            submitted.Id,
            TestData.Rejection("The receipt is missing"),
            CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Rejected, rejected.Status);
        Assert.AreEqual("The receipt is missing", rejected.RejectionReason);
        Assert.AreEqual("The receipt is missing", _world.Repository.Expenses[0].RejectionReason);
        ExpenseHistory entry = _world.Repository.History[^1];
        Assert.AreEqual(ExpenseAction.Rejected, entry.Action);
        Assert.AreEqual("The receipt is missing", entry.Reason);
        Assert.AreEqual("app-1", entry.ActorId);
    }

    /// <summary>Reject: requires a reason within the limits.</summary>
    [TestMethod]
    public async Task Reject_RequiresAReasonWithinTheLimits()
    {
        ExpenseResponse submitted = await _world.SubmittedAsync(TestData.Employee());
        UserContext approver = TestData.Approver();

        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.RejectAsync(approver, submitted.Id, TestData.Rejection(null), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.RejectAsync(approver, submitted.Id, TestData.Rejection("short"), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.RejectAsync(approver, submitted.Id, TestData.Rejection(new string('x', 501)), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.RejectAsync(approver, submitted.Id, TestData.Rejection(new string(' ', 20)), CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Submitted, _world.Repository.Expenses[0].Status);
        Assert.HasCount(2, _world.Repository.History);
    }

    /// <summary>Reject: is forbidden for the owner.</summary>
    [TestMethod]
    public async Task Reject_IsForbiddenForTheOwner()
    {
        UserContext employeeAndApprover = TestData.User("both-1", AppRoles.Employee, AppRoles.Approver);
        ExpenseResponse submitted = await _world.SubmittedAsync(employeeAndApprover);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => _world.Service.RejectAsync(employeeAndApprover, submitted.Id, TestData.Rejection(), CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Submitted, _world.Repository.Expenses[0].Status);
    }

    /// <summary>Reject: twice: is conflict and does not duplicate history.</summary>
    [TestMethod]
    public async Task Reject_Twice_IsConflictAndDoesNotDuplicateHistory()
    {
        UserContext approver = TestData.Approver();
        ExpenseResponse rejected = await _world.RejectedAsync(TestData.Employee(), approver);
        int historyCount = _world.Repository.History.Count;

        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.RejectAsync(approver, rejected.Id, TestData.Rejection(), CancellationToken.None));

        Assert.HasCount(historyCount, _world.Repository.History);
    }

    /// <summary>Reject: is conflict for approved expenses.</summary>
    [TestMethod]
    public async Task Reject_IsConflictForApprovedExpenses()
    {
        UserContext approver = TestData.Approver();
        ExpenseResponse approved = await _world.ApprovedAsync(TestData.Employee(), approver);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.RejectAsync(approver, approved.Id, TestData.Rejection(), CancellationToken.None));
    }

    /// <summary>Pay: moves approved to paid and creates the payment record.</summary>
    [TestMethod]
    public async Task Pay_MovesApprovedToPaidAndCreatesThePaymentRecord()
    {
        ExpenseResponse approved = await _world.ApprovedAsync(TestData.Employee("emp-1"), TestData.Approver());
        _world.Clock.Advance(TimeSpan.FromHours(2));

        ExpenseResponse paid = await _world.Service.PayAsync(TestData.Finance("fin-1"), approved.Id, CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Paid, paid.Status);
        Assert.HasCount(1, _world.Repository.Payments);
        PaymentRecord payment = _world.Repository.Payments[0];
        Assert.AreEqual(approved.Id, payment.ExpenseId);
        Assert.AreEqual("fin-1", payment.PaidByUserId);
        Assert.AreEqual(approved.Amount, payment.Amount);
        Assert.AreEqual(TestData.Now.UtcDateTime.AddHours(2), payment.PaidAtUtc);
        ExpenseHistory entry = _world.Repository.History[^1];
        Assert.AreEqual(ExpenseAction.Paid, entry.Action);
        Assert.AreEqual(ExpenseStatus.Approved, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Paid, entry.NewStatus);
    }

    /// <summary>Pay: is forbidden for the owner.</summary>
    [TestMethod]
    public async Task Pay_IsForbiddenForTheOwner()
    {
        UserContext employeeAndFinance = TestData.User("both-1", AppRoles.Employee, AppRoles.Finance);
        ExpenseResponse approved = await _world.ApprovedAsync(employeeAndFinance, TestData.Approver());

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.PayAsync(employeeAndFinance, approved.Id, CancellationToken.None));

        Assert.IsEmpty(_world.Repository.Payments);
        Assert.AreEqual(ExpenseStatus.Approved, _world.Repository.Expenses[0].Status);
    }

    /// <summary>Pay: is conflict for submitted and rejected expenses.</summary>
    [TestMethod]
    public async Task Pay_IsConflictForSubmittedAndRejectedExpenses()
    {
        UserContext finance = TestData.Finance();
        ExpenseResponse submitted = await _world.SubmittedAsync(TestData.Employee("emp-1"));
        ExpenseResponse rejected = await _world.RejectedAsync(TestData.Employee("emp-2"), TestData.Approver());

        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.PayAsync(finance, submitted.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.PayAsync(finance, rejected.Id, CancellationToken.None));

        Assert.IsEmpty(_world.Repository.Payments);
    }

    /// <summary>Pay: twice: is conflict and does not duplicate payment or history.</summary>
    [TestMethod]
    public async Task Pay_Twice_IsConflictAndDoesNotDuplicatePaymentOrHistory()
    {
        UserContext finance = TestData.Finance();
        ExpenseResponse paid = await _world.PaidAsync(TestData.Employee(), TestData.Approver(), finance);
        int historyCount = _world.Repository.History.Count;

        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.PayAsync(finance, paid.Id, CancellationToken.None));

        Assert.HasCount(1, _world.Repository.Payments);
        Assert.HasCount(historyCount, _world.Repository.History);
    }

    /// <summary>Pay: does not reveal drafts of other users.</summary>
    [TestMethod]
    public async Task Pay_DoesNotRevealDraftsOfOtherUsers()
    {
        ExpenseResponse draft = await _world.DraftAsync(TestData.Employee());

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.PayAsync(TestData.Finance(), draft.Id, CancellationToken.None));
    }

    /// <summary>Transition: persists state and history in a single save.</summary>
    [TestMethod]
    public async Task Transition_PersistsStateAndHistoryInASingleSave()
    {
        ExpenseResponse approved = await _world.ApprovedAsync(TestData.Employee(), TestData.Approver());
        int savesBefore = _world.Repository.SaveCalls;
        int historyBefore = _world.Repository.History.Count;

        await _world.Service.PayAsync(TestData.Finance(), approved.Id, CancellationToken.None);

        Assert.AreEqual(savesBefore + 1, _world.Repository.SaveCalls);
        Assert.AreEqual(historyBefore + 1, _world.Repository.History.Count);
        Assert.HasCount(1, _world.Repository.Payments);
    }

    /// <summary>Transition: with a failing save does not add history or payment.</summary>
    [TestMethod]
    public async Task Transition_WithAFailingSaveDoesNotAddHistoryOrPayment()
    {
        ExpenseResponse approved = await _world.ApprovedAsync(TestData.Employee(), TestData.Approver());
        int historyBefore = _world.Repository.History.Count;
        _world.Repository.FailOnSave = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _world.Service.PayAsync(TestData.Finance(), approved.Id, CancellationToken.None));

        Assert.HasCount(historyBefore, _world.Repository.History);
        Assert.IsEmpty(_world.Repository.Payments);
    }
}
