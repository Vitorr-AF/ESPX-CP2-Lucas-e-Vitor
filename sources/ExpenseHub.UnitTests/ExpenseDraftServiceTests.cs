using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseDraftServiceTests
{
    private readonly ExpenseWorld _world = new();

    [TestMethod]
    public async Task Create_StoresADraftOwnedByTheAuthenticatedUser()
    {
        ExpenseResponse created = await _world.Service.CreateAsync(TestData.Employee("emp-7"), TestData.Request(), CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Draft, created.Status);
        Assert.AreEqual("emp-7", created.OwnerId);
        Assert.AreEqual(TestData.Now.UtcDateTime, created.CreatedAtUtc);
        Assert.HasCount(1, _world.Repository.Expenses);
        Assert.AreEqual(created.Id, _world.Repository.Expenses[0].Id);
    }

    [TestMethod]
    public async Task Create_AcceptsAnExistingCategory()
    {
        ExpenseResponse created = await _world.Service.CreateAsync(TestData.Employee(), TestData.Request(categoryId: 2), CancellationToken.None);

        Assert.AreEqual(2, created.CategoryId);
    }

    [TestMethod]
    public async Task Create_RejectsAnUnknownCategory()
    {
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(
            () => _world.Service.CreateAsync(TestData.Employee(), TestData.Request(categoryId: 99), CancellationToken.None));

        Assert.IsEmpty(_world.Repository.Expenses);
    }

    [TestMethod]
    public async Task Create_RejectsInvalidInputBeforeTouchingTheRepository()
    {
        UserContext employee = TestData.Employee();

        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.CreateAsync(employee, TestData.Request(description: null), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.CreateAsync(employee, TestData.Request(description: "short"), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.CreateAsync(employee, TestData.Request(amount: null), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.CreateAsync(employee, TestData.Request(amount: 0m), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.CreateAsync(employee, TestData.Request(amount: 2147483648m), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidationFailedException>(() => _world.Service.CreateAsync(employee, TestData.Request(expenseDate: DateOnly.FromDateTime(TestData.Now.UtcDateTime).AddDays(1)), CancellationToken.None));

        Assert.AreEqual(0, _world.Repository.SaveCalls);
    }

    [TestMethod]
    public async Task Create_ReportsTheInvalidFieldsInCamelCase()
    {
        ValidationFailedException error = await Assert.ThrowsExactlyAsync<ValidationFailedException>(
            () => _world.Service.CreateAsync(TestData.Employee(), TestData.Request(description: null, amount: 0m), CancellationToken.None));

        Assert.IsTrue(error.Errors.ContainsKey("description"));
        Assert.IsTrue(error.Errors.ContainsKey("amount"));
    }

    [TestMethod]
    public async Task Create_RecordsHistoryInTheSameSaveAsTheExpense()
    {
        ExpenseResponse created = await _world.Service.CreateAsync(TestData.Employee("emp-1"), TestData.Request(), CancellationToken.None);

        Assert.AreEqual(1, _world.Repository.SaveCalls);
        Assert.HasCount(1, _world.Repository.History);
        ExpenseHistory entry = _world.Repository.History[0];
        Assert.AreEqual(created.Id, entry.ExpenseId);
        Assert.AreEqual(ExpenseAction.Created, entry.Action);
        Assert.AreEqual("emp-1", entry.ActorId);
        Assert.IsNull(entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, entry.NewStatus);
        Assert.AreEqual(TestData.Now.UtcDateTime, entry.OccurredAtUtc);
    }

    [TestMethod]
    public async Task Create_PersistsNothingWhenTheSaveFails()
    {
        _world.Repository.FailOnSave = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _world.Service.CreateAsync(TestData.Employee(), TestData.Request(), CancellationToken.None));

        Assert.IsEmpty(_world.Repository.Expenses);
        Assert.IsEmpty(_world.Repository.History);
    }

    [TestMethod]
    public async Task Update_ChangesTheOwnDraftAndRecordsTheChanges()
    {
        UserContext owner = TestData.Employee("emp-1");
        ExpenseResponse draft = await _world.DraftAsync(owner);
        _world.Clock.Advance(TimeSpan.FromMinutes(10));

        ExpenseResponse updated = await _world.Service.UpdateAsync(
            owner,
            draft.Id,
            TestData.Request(description: "Taxi and parking for the client visit", amount: 200m),
            CancellationToken.None);

        Assert.AreEqual("Taxi and parking for the client visit", updated.Description);
        Assert.AreEqual(200m, updated.Amount);
        Assert.AreEqual(ExpenseStatus.Draft, updated.Status);
        Assert.AreEqual("emp-1", updated.OwnerId);
        Assert.AreEqual(TestData.Now.UtcDateTime.AddMinutes(10), updated.UpdatedAtUtc);

        ExpenseHistory entry = _world.Repository.History[^1];
        Assert.AreEqual(ExpenseAction.Updated, entry.Action);
        Assert.AreEqual(ExpenseStatus.Draft, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, entry.NewStatus);
        Assert.IsTrue(entry.Changes!.Contains("Amount", StringComparison.Ordinal));
        Assert.IsTrue(entry.Changes!.Contains("Description", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Update_IsNotFoundForAnotherEmployeesDraft()
    {
        ExpenseResponse draft = await _world.DraftAsync(TestData.Employee("emp-1"));

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _world.Service.UpdateAsync(TestData.Employee("emp-2"), draft.Id, TestData.Request(amount: 1m), CancellationToken.None));

        Assert.AreEqual(150.75m, _world.Repository.Expenses[0].Amount);
    }

    [TestMethod]
    public async Task Update_IsForbiddenForVisibleExpensesOfSomeoneElse()
    {
        ExpenseResponse submitted = await _world.SubmittedAsync(TestData.Employee("emp-1"));
        UserContext employeeAndApprover = TestData.User("emp-2", AppRoles.Employee, AppRoles.Approver);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => _world.Service.UpdateAsync(employeeAndApprover, submitted.Id, TestData.Request(amount: 1m), CancellationToken.None));
    }

    [TestMethod]
    public async Task Update_IsConflictOnceTheExpenseLeftDraft()
    {
        UserContext owner = TestData.Employee("emp-1");
        ExpenseResponse submitted = await _world.SubmittedAsync(owner);

        await Assert.ThrowsExactlyAsync<ConflictException>(
            () => _world.Service.UpdateAsync(owner, submitted.Id, TestData.Request(amount: 1m), CancellationToken.None));

        Assert.AreEqual(150.75m, _world.Repository.Expenses[0].Amount);
    }

    [TestMethod]
    public async Task Update_IsNotFoundWhenTheExpenseDoesNotExist()
    {
        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _world.Service.UpdateAsync(TestData.Employee(), Guid.NewGuid(), TestData.Request(), CancellationToken.None));
    }

    [TestMethod]
    public async Task Update_RejectsInvalidInputAndKeepsTheDraft()
    {
        UserContext owner = TestData.Employee();
        ExpenseResponse draft = await _world.DraftAsync(owner);

        await Assert.ThrowsExactlyAsync<ValidationFailedException>(
            () => _world.Service.UpdateAsync(owner, draft.Id, TestData.Request(amount: -1m), CancellationToken.None));

        Assert.AreEqual(150.75m, _world.Repository.Expenses[0].Amount);
        Assert.HasCount(1, _world.Repository.History);
    }

    [TestMethod]
    public async Task Submit_MovesTheOwnDraftToSubmittedAndRecordsHistory()
    {
        UserContext owner = TestData.Employee("emp-1");
        ExpenseResponse draft = await _world.DraftAsync(owner);

        ExpenseResponse submitted = await _world.Service.SubmitAsync(owner, draft.Id, CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Submitted, submitted.Status);
        ExpenseHistory entry = _world.Repository.History[^1];
        Assert.AreEqual(ExpenseAction.Submitted, entry.Action);
        Assert.AreEqual(ExpenseStatus.Draft, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.NewStatus);
        Assert.AreEqual("emp-1", entry.ActorId);
    }

    [TestMethod]
    public async Task Submit_IsNotFoundForAnotherEmployeesDraft()
    {
        ExpenseResponse draft = await _world.DraftAsync(TestData.Employee("emp-1"));

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => _world.Service.SubmitAsync(TestData.Employee("emp-2"), draft.Id, CancellationToken.None));

        Assert.AreEqual(ExpenseStatus.Draft, _world.Repository.Expenses[0].Status);
    }

    [TestMethod]
    public async Task Submit_Twice_IsConflictAndDoesNotDuplicateHistory()
    {
        UserContext owner = TestData.Employee();
        ExpenseResponse draft = await _world.DraftAsync(owner);
        await _world.Service.SubmitAsync(owner, draft.Id, CancellationToken.None);
        int historyCount = _world.Repository.History.Count;

        await Assert.ThrowsExactlyAsync<ConflictException>(() => _world.Service.SubmitAsync(owner, draft.Id, CancellationToken.None));

        Assert.HasCount(historyCount, _world.Repository.History);
    }
}
