using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;

namespace ExpenseHub.UnitTests.Support;

/// <summary>A service wired to in-memory fakes plus helpers that move expenses through the workflow.</summary>
internal sealed class ExpenseWorld
{
    public ExpenseWorld()
    {
        Repository = new FakeExpenseRepository();
        Clock = new FixedTimeProvider(TestData.Now);
        Service = new ExpenseService(Repository, Clock);
    }

    public FakeExpenseRepository Repository { get; }

    public FixedTimeProvider Clock { get; }

    public ExpenseService Service { get; }

    public async Task<ExpenseResponse> DraftAsync(UserContext owner)
    {
        return await Service.CreateAsync(owner, TestData.Request(), CancellationToken.None);
    }

    public async Task<ExpenseResponse> SubmittedAsync(UserContext owner)
    {
        ExpenseResponse draft = await DraftAsync(owner);

        return await Service.SubmitAsync(owner, draft.Id, CancellationToken.None);
    }

    public async Task<ExpenseResponse> ApprovedAsync(UserContext owner, UserContext approver)
    {
        ExpenseResponse submitted = await SubmittedAsync(owner);

        return await Service.ApproveAsync(approver, submitted.Id, CancellationToken.None);
    }

    public async Task<ExpenseResponse> RejectedAsync(UserContext owner, UserContext approver)
    {
        ExpenseResponse submitted = await SubmittedAsync(owner);

        return await Service.RejectAsync(approver, submitted.Id, TestData.Rejection(), CancellationToken.None);
    }

    public async Task<ExpenseResponse> PaidAsync(UserContext owner, UserContext approver, UserContext finance)
    {
        ExpenseResponse approved = await ApprovedAsync(owner, approver);

        return await Service.PayAsync(finance, approved.Id, CancellationToken.None);
    }
}
