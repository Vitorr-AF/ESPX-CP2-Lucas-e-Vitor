using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application;

/// <summary>
/// Use cases of the expense workflow. Role, ownership and state rules are enforced here, in this order:
/// role (403), visibility (404), ownership (403) and state (409).
/// </summary>
internal sealed class ExpenseService
{
    private readonly IExpenseRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ExpenseService(IExpenseRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<ExpenseResponse> CreateAsync(UserContext user, ExpenseRequest request, CancellationToken cancellationToken)
    {
        RequireRole(user, AppRoles.Employee);
        RequestValidator.Validate(request);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        DateTime now = Now();
        Expense expense = Expense.CreateDraft(
            user.UserId,
            request.Description,
            request.Amount.GetValueOrDefault(),
            request.ExpenseDate.GetValueOrDefault(),
            request.CategoryId,
            now);

        _repository.AddExpense(expense);
        _repository.AddHistory(ExpenseHistory.Create(
            expense.Id,
            ExpenseAction.Created,
            user.UserId,
            now,
            previousStatus: null,
            ExpenseStatus.Draft,
            reason: null,
            changes: null));
        await _repository.SaveChangesAsync(cancellationToken);

        return ExpenseResponse.From(expense);
    }

    public async Task<ExpenseResponse> UpdateAsync(UserContext user, Guid id, ExpenseRequest request, CancellationToken cancellationToken)
    {
        RequireRole(user, AppRoles.Employee);
        RequestValidator.Validate(request);

        Expense expense = await FindVisibleAsync(user, id, cancellationToken);
        RequireOwner(user, expense);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        DateTime now = Now();
        ExpenseStatus previousStatus = expense.Status;
        string changes = expense.UpdateDraft(
            request.Description,
            request.Amount.GetValueOrDefault(),
            request.ExpenseDate.GetValueOrDefault(),
            request.CategoryId,
            now);

        _repository.AddHistory(ExpenseHistory.Create(
            expense.Id,
            ExpenseAction.Updated,
            user.UserId,
            now,
            previousStatus,
            expense.Status,
            reason: null,
            changes));
        await _repository.SaveChangesAsync(cancellationToken);

        return ExpenseResponse.From(expense);
    }

    public async Task<IReadOnlyList<ExpenseResponse>> ListAsync(UserContext user, CancellationToken cancellationToken)
    {
        RequireReaderRole(user);

        IReadOnlyList<Expense> expenses = await _repository.ListAsync(ExpenseVisibility.ForReading(user), cancellationToken);

        return expenses.Select(ExpenseResponse.From).ToList();
    }

    public async Task<ExpenseResponse> GetAsync(UserContext user, Guid id, CancellationToken cancellationToken)
    {
        RequireReaderRole(user);

        Expense expense = await FindVisibleAsync(user, id, cancellationToken);

        return ExpenseResponse.From(expense);
    }

    public async Task<ExpenseResponse> SubmitAsync(UserContext user, Guid id, CancellationToken cancellationToken)
    {
        RequireRole(user, AppRoles.Employee);

        Expense expense = await FindVisibleAsync(user, id, cancellationToken);
        RequireOwner(user, expense);

        DateTime now = Now();
        ExpenseStatus previousStatus = expense.Status;
        expense.Submit(now);

        await RecordTransitionAsync(expense, ExpenseAction.Submitted, user, previousStatus, now, reason: null, cancellationToken);

        return ExpenseResponse.From(expense);
    }

    public async Task<ExpenseResponse> ApproveAsync(UserContext user, Guid id, CancellationToken cancellationToken)
    {
        RequireRole(user, AppRoles.Approver);

        Expense expense = await FindForTransitionAsync(user, id, cancellationToken);
        RequireNotOwner(user, expense, "Approvers cannot approve their own expenses.");

        DateTime now = Now();
        ExpenseStatus previousStatus = expense.Status;
        expense.Approve(now);

        await RecordTransitionAsync(expense, ExpenseAction.Approved, user, previousStatus, now, reason: null, cancellationToken);

        return ExpenseResponse.From(expense);
    }

    public async Task<ExpenseResponse> RejectAsync(UserContext user, Guid id, RejectExpenseRequest request, CancellationToken cancellationToken)
    {
        RequireRole(user, AppRoles.Approver);
        RequestValidator.Validate(request);

        Expense expense = await FindForTransitionAsync(user, id, cancellationToken);
        RequireNotOwner(user, expense, "Approvers cannot reject their own expenses.");

        DateTime now = Now();
        ExpenseStatus previousStatus = expense.Status;
        string reason = expense.Reject(request.Reason, now);

        await RecordTransitionAsync(expense, ExpenseAction.Rejected, user, previousStatus, now, reason, cancellationToken);

        return ExpenseResponse.From(expense);
    }

    public async Task<ExpenseResponse> PayAsync(UserContext user, Guid id, CancellationToken cancellationToken)
    {
        RequireRole(user, AppRoles.Finance);

        Expense expense = await FindForTransitionAsync(user, id, cancellationToken);
        RequireNotOwner(user, expense, "Finance users cannot pay their own expenses.");

        DateTime now = Now();
        ExpenseStatus previousStatus = expense.Status;
        PaymentRecord payment = expense.Pay(user.UserId, now);

        _repository.AddPayment(payment);
        await RecordTransitionAsync(expense, ExpenseAction.Paid, user, previousStatus, now, reason: null, cancellationToken);

        return ExpenseResponse.From(expense);
    }

    public async Task<IReadOnlyList<ExpenseHistoryResponse>> GetHistoryAsync(UserContext user, Guid id, CancellationToken cancellationToken)
    {
        RequireReaderRole(user);

        Expense expense = await FindVisibleAsync(user, id, cancellationToken);
        IReadOnlyList<ExpenseHistory> history = await _repository.GetHistoryAsync(expense.Id, cancellationToken);

        return history.Select(ExpenseHistoryResponse.From).ToList();
    }

    private static void RequireRole(UserContext user, string role)
    {
        if (!user.IsInRole(role))
        {
            throw new ForbiddenException("The current user does not have permission to perform this operation.");
        }
    }

    private static void RequireReaderRole(UserContext user)
    {
        bool canRead = user.IsInRole(AppRoles.Employee)
            || user.IsInRole(AppRoles.Approver)
            || user.IsInRole(AppRoles.Finance)
            || user.IsInRole(AppRoles.Auditor);

        if (!canRead)
        {
            throw new ForbiddenException("The current user does not have permission to read expenses.");
        }
    }

    private static void RequireOwner(UserContext user, Expense expense)
    {
        if (!string.Equals(expense.OwnerId, user.UserId, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Only the owner can change this expense.");
        }
    }

    private static void RequireNotOwner(UserContext user, Expense expense, string message)
    {
        if (string.Equals(expense.OwnerId, user.UserId, StringComparison.Ordinal))
        {
            throw new ForbiddenException(message);
        }
    }

    private DateTime Now()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private async Task<Expense> FindVisibleAsync(UserContext user, Guid id, CancellationToken cancellationToken)
    {
        Expense? expense = await _repository.FindAsync(id, ExpenseVisibility.ForReading(user), cancellationToken);

        return expense ?? throw new NotFoundException("Expense not found.");
    }

    private async Task<Expense> FindForTransitionAsync(UserContext user, Guid id, CancellationToken cancellationToken)
    {
        Expense? expense = await _repository.FindAsync(id, ExpenseVisibility.ForTransition(user), cancellationToken);

        return expense ?? throw new NotFoundException("Expense not found.");
    }

    private async Task EnsureCategoryExistsAsync(int? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return;
        }

        if (!await _repository.CategoryExistsAsync(categoryId.Value, cancellationToken))
        {
            throw new ValidationFailedException("categoryId", "The category does not exist.");
        }
    }

    private async Task RecordTransitionAsync(
        Expense expense,
        ExpenseAction action,
        UserContext user,
        ExpenseStatus previousStatus,
        DateTime now,
        string? reason,
        CancellationToken cancellationToken)
    {
        _repository.AddHistory(ExpenseHistory.Create(
            expense.Id,
            action,
            user.UserId,
            now,
            previousStatus,
            expense.Status,
            reason,
            changes: null));
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
