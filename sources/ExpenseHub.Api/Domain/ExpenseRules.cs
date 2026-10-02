using System;

namespace ExpenseHub.Api.Domain;

/// <summary>Validation rules shared by the expense aggregate.</summary>
internal static class ExpenseRules
{
    public const decimal MinAmount = 0.01m;
    public const decimal MaxAmount = int.MaxValue;
    public const int DescriptionMinLength = 10;
    public const int DescriptionMaxLength = 500;
    public const int ReasonMinLength = 10;
    public const int ReasonMaxLength = 500;

    public static string NormalizeDescription(string? description)
    {
        string trimmed = description?.Trim() ?? string.Empty;

        if (trimmed.Length is < DescriptionMinLength or > DescriptionMaxLength)
        {
            throw new ValidationFailedException("description", "Description must have between 10 and 500 characters.");
        }

        return trimmed;
    }

    public static string NormalizeReason(string? reason)
    {
        string trimmed = reason?.Trim() ?? string.Empty;

        if (trimmed.Length is < ReasonMinLength or > ReasonMaxLength)
        {
            throw new ValidationFailedException("reason", "Reason must have between 10 and 500 characters.");
        }

        return trimmed;
    }

    public static void ValidateAmount(decimal amount)
    {
        if (amount is < MinAmount or > MaxAmount)
        {
            throw new ValidationFailedException("amount", "Amount must be between 0.01 and 2147483647.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ValidationFailedException("amount", "Amount must have at most two decimal places.");
        }
    }

    public static void ValidateExpenseDate(DateOnly expenseDate, DateTime nowUtc)
    {
        DateOnly today = DateOnly.FromDateTime(nowUtc);

        if (expenseDate > today)
        {
            throw new ValidationFailedException("expenseDate", "Expense date cannot be in the future.");
        }
    }
}
