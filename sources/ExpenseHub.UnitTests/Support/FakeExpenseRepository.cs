using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests.Support;

/// <summary>
/// In-memory repository. Added entities are only visible after <see cref="SaveChangesAsync"/>,
/// mimicking a unit of work, and a save can be forced to fail.
/// </summary>
internal sealed class FakeExpenseRepository : IExpenseRepository
{
    private readonly List<Expense> _expenses = [];
    private readonly List<ExpenseHistory> _history = [];
    private readonly List<PaymentRecord> _payments = [];
    private readonly List<Expense> _pendingExpenses = [];
    private readonly List<ExpenseHistory> _pendingHistory = [];
    private readonly List<PaymentRecord> _pendingPayments = [];

    public IReadOnlyList<Expense> Expenses => _expenses;

    public IReadOnlyList<ExpenseHistory> History => _history;

    public IReadOnlyList<PaymentRecord> Payments => _payments;

    public int SaveCalls { get; private set; }

    public bool FailOnSave { get; set; }

    public HashSet<int> Categories { get; } = [1, 2];

    public void Seed(Expense expense)
    {
        _expenses.Add(expense);
    }

    public Task<Expense?> FindAsync(Guid id, Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken)
    {
        Func<Expense, bool> filter = scope.Compile();

        return Task.FromResult(_expenses.FirstOrDefault(expense => expense.Id == id && filter(expense)));
    }

    public Task<IReadOnlyList<Expense>> ListAsync(Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken)
    {
        IReadOnlyList<Expense> result = _expenses.AsQueryable().Where(scope).OrderByDescending(expense => expense.CreatedAtUtc).ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ExpenseHistory>> GetHistoryAsync(Guid expenseId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExpenseHistory> result = _history.Where(history => history.ExpenseId == expenseId).ToList();

        return Task.FromResult(result);
    }

    public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Categories.Contains(categoryId));
    }

    public void AddExpense(Expense expense)
    {
        _pendingExpenses.Add(expense);
    }

    public void AddHistory(ExpenseHistory history)
    {
        _pendingHistory.Add(history);
    }

    public void AddPayment(PaymentRecord payment)
    {
        _pendingPayments.Add(payment);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;

        if (FailOnSave)
        {
            _pendingExpenses.Clear();
            _pendingHistory.Clear();
            _pendingPayments.Clear();

            throw new InvalidOperationException("Simulated persistence failure.");
        }

        _expenses.AddRange(_pendingExpenses);
        _history.AddRange(_pendingHistory);
        _payments.AddRange(_pendingPayments);
        _pendingExpenses.Clear();
        _pendingHistory.Clear();
        _pendingPayments.Clear();

        return Task.CompletedTask;
    }
}
