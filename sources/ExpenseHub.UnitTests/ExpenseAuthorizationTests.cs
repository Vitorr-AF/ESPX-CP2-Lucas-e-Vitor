using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseAuthorizationTests
{
    private readonly ExpenseWorld _world = new();

    [TestMethod]
    public async Task Create_RequiresTheEmployeeRole()
    {
        foreach (UserContext user in new[] { TestData.Approver(), TestData.Finance(), TestData.Auditor(), TestData.Admin(), TestData.User("nobody-1") })
        {
            await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.CreateAsync(user, TestData.Request(), CancellationToken.None));
        }

        Assert.IsEmpty(_world.Repository.Expenses);
        Assert.AreEqual(0, _world.Repository.SaveCalls);
    }

    [TestMethod]
    public async Task Update_AndSubmit_RequireTheEmployeeRole()
    {
        ExpenseResponse draft = await _world.DraftAsync(TestData.Employee("emp-1"));

        foreach (UserContext user in new[] { TestData.Approver(), TestData.Finance(), TestData.Auditor(), TestData.Admin() })
        {
            await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.UpdateAsync(user, draft.Id, TestData.Request(), CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.SubmitAsync(user, draft.Id, CancellationToken.None));
        }

        Assert.AreEqual(ExpenseStatus.Draft, _world.Repository.Expenses[0].Status);
    }

    [TestMethod]
    public async Task Approve_AndReject_RequireTheApproverRole()
    {
        ExpenseResponse submitted = await _world.SubmittedAsync(TestData.Employee("emp-1"));

        foreach (UserContext user in new[] { TestData.Employee("emp-2"), TestData.Finance(), TestData.Auditor(), TestData.Admin() })
        {
            await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.ApproveAsync(user, submitted.Id, CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.RejectAsync(user, submitted.Id, TestData.Rejection(), CancellationToken.None));
        }

        Assert.AreEqual(ExpenseStatus.Submitted, _world.Repository.Expenses[0].Status);
    }

    [TestMethod]
    public async Task Pay_RequiresTheFinanceRole()
    {
        ExpenseResponse approved = await _world.ApprovedAsync(TestData.Employee("emp-1"), TestData.Approver());

        foreach (UserContext user in new[] { TestData.Employee("emp-2"), TestData.Approver("app-2"), TestData.Auditor(), TestData.Admin() })
        {
            await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.PayAsync(user, approved.Id, CancellationToken.None));
        }

        Assert.AreEqual(ExpenseStatus.Approved, _world.Repository.Expenses[0].Status);
        Assert.IsEmpty(_world.Repository.Payments);
    }

    [TestMethod]
    public async Task Auditor_CanOnlyRead()
    {
        UserContext auditor = TestData.Auditor();
        ExpenseResponse draft = await _world.DraftAsync(TestData.Employee());
        int savesBefore = _world.Repository.SaveCalls;

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.CreateAsync(auditor, TestData.Request(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.UpdateAsync(auditor, draft.Id, TestData.Request(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.SubmitAsync(auditor, draft.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.ApproveAsync(auditor, draft.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.RejectAsync(auditor, draft.Id, TestData.Rejection(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.PayAsync(auditor, draft.Id, CancellationToken.None));

        Assert.AreEqual(savesBefore, _world.Repository.SaveCalls);
    }

    [TestMethod]
    public async Task Admin_AloneHasNoFunctionalAccess()
    {
        UserContext admin = TestData.Admin();
        ExpenseResponse draft = await _world.DraftAsync(TestData.Employee());

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.ListAsync(admin, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.GetAsync(admin, draft.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.GetHistoryAsync(admin, draft.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task UserWithoutRoles_IsForbiddenEverywhere()
    {
        UserContext nobody = TestData.User("nobody-1");
        ExpenseResponse draft = await _world.DraftAsync(TestData.Employee());

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.ListAsync(nobody, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.GetAsync(nobody, draft.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.ApproveAsync(nobody, draft.Id, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.PayAsync(nobody, draft.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task AccumulatedRoles_GrantTheUnionOfPermissionsButNeverSelfApprovalOrSelfPayment()
    {
        UserContext everything = TestData.User("boss-1", AppRoles.Employee, AppRoles.Approver, AppRoles.Finance);
        ExpenseResponse own = await _world.SubmittedAsync(everything);
        ExpenseResponse others = await _world.SubmittedAsync(TestData.Employee("emp-2"));

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.ApproveAsync(everything, own.Id, CancellationToken.None));

        ExpenseResponse approved = await _world.Service.ApproveAsync(everything, others.Id, CancellationToken.None);
        ExpenseResponse paid = await _world.Service.PayAsync(everything, approved.Id, CancellationToken.None);

        Assert.AreEqual(ExpenseStatus.Paid, paid.Status);
    }

    [TestMethod]
    public async Task OwnerWhoIsAlsoApprover_CannotPayTheirOwnApprovedExpense()
    {
        UserContext everything = TestData.User("boss-1", AppRoles.Employee, AppRoles.Approver, AppRoles.Finance);
        ExpenseResponse own = await _world.SubmittedAsync(everything);
        _world.Repository.Expenses[0].Approve(TestData.Now.UtcDateTime);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => _world.Service.PayAsync(everything, own.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task Create_NeverTrustsTheOwnerSentByTheClient()
    {
        ExpenseResponse created = await _world.Service.CreateAsync(TestData.Employee("emp-real"), TestData.Request(), CancellationToken.None);

        Assert.AreEqual("emp-real", created.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, created.Status);
        Assert.AreNotEqual(Guid.Empty, created.Id);
    }
}
