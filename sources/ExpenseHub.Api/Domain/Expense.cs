using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExpenseHub.Api.Domain;

/// <summary>Reimbursement request. All state changes go through the transition methods.</summary>
internal sealed class Expense
{
    private Expense()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public DateOnly ExpenseDate { get; private set; }

    public int? CategoryId { get; private set; }

    public ExpenseStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Optimistic concurrency token, incremented on every change.</summary>
    public int Version { get; private set; }

    public static Expense CreateDraft(
        string ownerId,
        string? description,
        decimal amount,
        DateOnly expenseDate,
        int? categoryId,
        DateTime nowUtc)
    {
        string normalizedDescription = ExpenseRules.NormalizeDescription(description);
        ExpenseRules.ValidateAmount(amount);
        ExpenseRules.ValidateExpenseDate(expenseDate, nowUtc);

        return new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Description = normalizedDescription,
            Amount = amount,
            ExpenseDate = expenseDate,
            CategoryId = categoryId,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1,
        };
    }

    /// <summary>Edits a draft and returns a human readable summary of what changed.</summary>
    public string UpdateDraft(
        string? description,
        decimal amount,
        DateOnly expenseDate,
        int? categoryId,
        DateTime nowUtc)
    {
        EnsureStatus(ExpenseStatus.Draft, "Only drafts can be edited.");

        string normalizedDescription = ExpenseRules.NormalizeDescription(description);
        ExpenseRules.ValidateAmount(amount);
        ExpenseRules.ValidateExpenseDate(expenseDate, nowUtc);

        List<string> changes = [];

        if (!string.Equals(Description, normalizedDescription, StringComparison.Ordinal))
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"Description: '{Description}' -> '{normalizedDescription}'"));
        }

        if (Amount != amount)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"Amount: {Amount} -> {amount}"));
        }

        if (ExpenseDate != expenseDate)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"ExpenseDate: {ExpenseDate:yyyy-MM-dd} -> {expenseDate:yyyy-MM-dd}"));
        }

        if (CategoryId != categoryId)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"CategoryId: {CategoryId} -> {categoryId}"));
        }

        Description = normalizedDescription;
        Amount = amount;
        ExpenseDate = expenseDate;
        CategoryId = categoryId;
        Touch(nowUtc);

        return changes.Count == 0 ? "No field changes." : string.Join("; ", changes);
    }

    public void Submit(DateTime nowUtc)
    {
        EnsureStatus(ExpenseStatus.Draft, "Only drafts can be submitted.");
        Status = ExpenseStatus.Submitted;
        Touch(nowUtc);
    }

    public void Approve(DateTime nowUtc)
    {
        EnsureStatus(ExpenseStatus.Submitted, "Only submitted expenses can be approved.");
        Status = ExpenseStatus.Approved;
        Touch(nowUtc);
    }

    /// <summary>Rejects the expense and returns the normalized reason.</summary>
    public string Reject(string? reason, DateTime nowUtc)
    {
        EnsureStatus(ExpenseStatus.Submitted, "Only submitted expenses can be rejected.");

        string normalizedReason = ExpenseRules.NormalizeReason(reason);
        Status = ExpenseStatus.Rejected;
        RejectionReason = normalizedReason;
        Touch(nowUtc);

        return normalizedReason;
    }

    public PaymentRecord Pay(string actorId, DateTime nowUtc)
    {
        EnsureStatus(ExpenseStatus.Approved, "Only approved expenses can be paid.");
        Status = ExpenseStatus.Paid;
        Touch(nowUtc);

        return PaymentRecord.Create(Id, actorId, Amount, nowUtc);
    }

    private void EnsureStatus(ExpenseStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new ConflictException(message);
        }
    }

    private void Touch(DateTime nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}
