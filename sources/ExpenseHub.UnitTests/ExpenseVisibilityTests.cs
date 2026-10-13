using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseVisibilityTests
{
    private readonly ExpenseWorld _world = new();
    private ExpenseResponse _draftOfEmp1 = null!;
    private ExpenseResponse _submittedOfEmp1 = null!;
    private ExpenseResponse _approvedOfEmp2 = null!;
    private ExpenseResponse _rejectedOfEmp2 = null!;
    private ExpenseResponse _paidOfEmp3 = null!;

    [TestInitialize]
    public async Task SeedExpenses()
    {
        UserContext approver = TestData.Approver("app-9");
        UserContext finance = TestData.Finance("fin-9");

        _draftOfEmp1 = await _world.DraftAsync(TestData.Employee("emp-1"));
        _submittedOfEmp1 = await _world.SubmittedAsync(TestData.Employee("emp-1"));
        _approvedOfEmp2 = await _world.ApprovedAsync(TestData.Employee("emp-2"), approver);
        _rejectedOfEmp2 = await _world.RejectedAsync(TestData.Employee("emp-2"), approver);
        _paidOfEmp3 = await _world.PaidAsync(TestData.Employee("emp-3"), approver, finance);
    }

    [TestMethod]
    public async Task Employee_ListsOnlyTheirOwnExpenses()
    {
        IReadOnlyList<ExpenseResponse> list = await _world.Service.ListAsync(TestData.Employee("emp-1"), CancellationToken.None);

        AssertSameExpenses(list, _draftOfEmp1, _submittedOfEmp1);
    }

    [TestMethod]
    public async Task Employee_WithoutExpensesSeesAnEmptyList()
    {
        IReadOnlyList<ExpenseResponse> list = await _world.Service.ListAsync(TestData.Employee("emp-new"), CancellationToken.None);

        Assert.IsEmpty(list);
    }

    [TestMethod]
    public async Task Approver_ListsOnlySubmittedExpenses()
    {
        IReadOnlyList<ExpenseResponse> list = await _world.Service.ListAsync(TestData.Approver("app-1"), CancellationToken.None);

        AssertSameExpenses(list, _submittedOfEmp1);
    }

    [TestMethod]
    public async Task Finance_ListsOnlyApprovedAndPaidExpenses()
    {
        IReadOnlyList<ExpenseResponse> list = await _world.Service.ListAsync(TestData.Finance("fin-1"), CancellationToken.None);

        AssertSameExpenses(list, _approvedOfEmp2, _paidOfEmp3);
    }

    [TestMethod]
    public async Task Auditor_ListsEverything()
    {
        IReadOnlyList<ExpenseResponse> list = await _world.Service.ListAsync(TestData.Auditor(), CancellationToken.None);

        AssertSameExpenses(list, _draftOfEmp1, _submittedOfEmp1, _approvedOfEmp2, _rejectedOfEmp2, _paidOfEmp3);
    }

    [TestMethod]
    public async Task AccumulatedRoles_ListTheUnionOfTheirScopes()
    {
        UserContext user = TestData.User("emp-1", AppRoles.Employee, AppRoles.Finance);

        IReadOnlyList<ExpenseResponse> list = await _world.Service.ListAsync(user, CancellationToken.None);

        AssertSameExpenses(list, _draftOfEmp1, _submittedOfEmp1, _approvedOfEmp2, _paidOfEmp3);
    }

    [TestMethod]
    public async Task Employee_CannotReadAnotherEmployeesExpense()
    {
        UserContext other = TestData.Employee("emp-2");

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(other, _draftOfEmp1.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(other, _submittedOfEmp1.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetHistoryAsync(other, _draftOfEmp1.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task Employee_ReadsTheirOwnExpenseInAnyState()
    {
        UserContext owner = TestData.Employee("emp-2");

        ExpenseResponse approved = await _world.Service.GetAsync(owner, _approvedOfEmp2.Id, CancellationToken.None);
        ExpenseResponse rejected = await _world.Service.GetAsync(owner, _rejectedOfEmp2.Id, CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Approved, approved.Status);
        Assert.AreEqual(ExpenseStatus.Rejected, rejected.Status);
    }

    [TestMethod]
    public async Task Approver_CannotReadDraftsApprovedRejectedOrPaidExpenses()
    {
        UserContext approver = TestData.Approver();

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(approver, _draftOfEmp1.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(approver, _approvedOfEmp2.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(approver, _rejectedOfEmp2.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(approver, _paidOfEmp3.Id, CancellationToken.None));

        ExpenseResponse visible = await _world.Service.GetAsync(approver, _submittedOfEmp1.Id, CancellationToken.None);
        Assert.AreEqual(_submittedOfEmp1.Id, visible.Id);
    }

    [TestMethod]
    public async Task Finance_CannotReadDraftsSubmittedOrRejectedExpenses()
    {
        UserContext finance = TestData.Finance();

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(finance, _draftOfEmp1.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(finance, _submittedOfEmp1.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(finance, _rejectedOfEmp2.Id, CancellationToken.None));

        ExpenseResponse approved = await _world.Service.GetAsync(finance, _approvedOfEmp2.Id, CancellationToken.None);
        ExpenseResponse paid = await _world.Service.GetAsync(finance, _paidOfEmp3.Id, CancellationToken.None);
        Assert.AreEqual(ExpenseStatus.Approved, approved.Status);
        Assert.AreEqual(ExpenseStatus.Paid, paid.Status);
    }

    [TestMethod]
    public async Task Auditor_ReadsAnyExpenseAndItsHistory()
    {
        UserContext auditor = TestData.Auditor();

        ExpenseResponse draft = await _world.Service.GetAsync(auditor, _draftOfEmp1.Id, CancellationToken.None);
        IReadOnlyList<ExpenseHistoryResponse> history = await _world.Service.GetHistoryAsync(auditor, _paidOfEmp3.Id, CancellationToken.None);

        Assert.AreEqual(_draftOfEmp1.Id, draft.Id);
        Assert.HasCount(4, history);
    }

    [TestMethod]
    public async Task Get_IsNotFoundForUnknownIds()
    {
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetAsync(TestData.Auditor(), Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetHistoryAsync(TestData.Auditor(), Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod]
    public async Task History_FollowsTheVisibilityOfTheExpense()
    {
        UserContext approver = TestData.Approver();
        UserContext finance = TestData.Finance();

        IReadOnlyList<ExpenseHistoryResponse> submittedHistory = await _world.Service.GetHistoryAsync(approver, _submittedOfEmp1.Id, CancellationToken.None);
        IReadOnlyList<ExpenseHistoryResponse> paidHistory = await _world.Service.GetHistoryAsync(finance, _paidOfEmp3.Id, CancellationToken.None);

        Assert.HasCount(2, submittedHistory);
        Assert.HasCount(4, paidHistory);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetHistoryAsync(approver, _paidOfEmp3.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => _world.Service.GetHistoryAsync(finance, _submittedOfEmp1.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task History_ListsTheEntriesOfTheFullWorkflowInOrder()
    {
        IReadOnlyList<ExpenseHistoryResponse> history = await _world.Service.GetHistoryAsync(TestData.Employee("emp-3"), _paidOfEmp3.Id, CancellationToken.None);

        Assert.HasCount(4, history);
        Assert.AreEqual(ExpenseAction.Created, history[0].Action);
        Assert.AreEqual(ExpenseAction.Submitted, history[1].Action);
        Assert.AreEqual(ExpenseAction.Approved, history[2].Action);
        Assert.AreEqual(ExpenseAction.Paid, history[3].Action);
        Assert.AreEqual("emp-3", history[0].ActorId);
        Assert.AreEqual("fin-9", history[^1].ActorId);
        Assert.AreEqual(ExpenseStatus.Paid, history[^1].NewStatus);
    }

    private static void AssertSameExpenses(IReadOnlyList<ExpenseResponse> actual, params ExpenseResponse[] expected)
    {
        HashSet<Guid> actualIds = actual.Select(expense => expense.Id).ToHashSet();
        HashSet<Guid> expectedIds = expected.Select(expense => expense.Id).ToHashSet();

        Assert.HasCount(expected.Length, actual);
        Assert.IsTrue(actualIds.SetEquals(expectedIds));
    }
}
