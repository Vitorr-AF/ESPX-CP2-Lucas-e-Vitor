using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Application;
using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Infrastructure;

/// <summary>Entity Framework Core implementation of <see cref="IExpenseRepository"/>.</summary>
internal sealed class EfExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _db;

    public EfExpenseRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Expense?> FindAsync(Guid id, Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken)
    {
        return await _db.Expenses
            .Where(expense => expense.Id == id)
            .Where(scope)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> ListAsync(Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken)
    {
        return await _db.Expenses
            .AsNoTracking()
            .Where(scope)
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseHistory>> GetHistoryAsync(Guid expenseId, CancellationToken cancellationToken)
    {
        return await _db.ExpenseHistories
            .AsNoTracking()
            .Where(history => history.ExpenseId == expenseId)
            .OrderBy(history => history.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        return await _db.ExpenseCategories.AnyAsync(category => category.Id == categoryId, cancellationToken);
    }

    public void AddExpense(Expense expense)
    {
        _db.Expenses.Add(expense);
    }

    public void AddHistory(ExpenseHistory history)
    {
        _db.ExpenseHistories.Add(history);
    }

    public void AddPayment(PaymentRecord payment)
    {
        _db.PaymentRecords.Add(payment);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The expense was changed by another request. Reload it and try again.");
        }
    }
}
