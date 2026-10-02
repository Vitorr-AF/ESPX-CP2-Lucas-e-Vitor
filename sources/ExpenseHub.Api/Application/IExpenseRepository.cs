using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application;

/// <summary>
/// Persistence port. Changes are staged with the Add methods and by mutating tracked expenses,
/// and are committed together by <see cref="SaveChangesAsync"/>.
/// </summary>
internal interface IExpenseRepository
{
    /// <summary>Finds an expense by id restricted to a scope that is translated to the data source.</summary>
    Task<Expense?> FindAsync(Guid id, Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken);

    /// <summary>Lists the expenses of a scope, newest first. The scope is applied before materialization.</summary>
    Task<IReadOnlyList<Expense>> ListAsync(Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpenseHistory>> GetHistoryAsync(Guid expenseId, CancellationToken cancellationToken);

    Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken);

    void AddExpense(Expense expense);

    void AddHistory(ExpenseHistory history);

    void AddPayment(PaymentRecord payment);

    /// <summary>Commits every staged change in one atomic operation.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
