using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application;

/// <summary>Body of <c>POST /register</c>. Roles are intentionally not accepted.</summary>
internal sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; init; }
}

/// <summary>Body of <c>POST /login</c>.</summary>
internal sealed class LoginRequest
{
    [Required]
    [StringLength(256)]
    public string? Email { get; init; }

    [Required]
    [StringLength(100)]
    public string? Password { get; init; }
}

/// <summary>Body of <c>PUT /api/admin/users/{id}/roles</c>. Replaces the roles of the user.</summary>
internal sealed class UpdateRolesRequest
{
    [Required]
    public IReadOnlyList<string>? Roles { get; init; }
}

/// <summary>Body used to create and edit drafts. Owner, status, actors and timestamps are never accepted.</summary>
internal sealed class ExpenseRequest
{
    [Required]
    [StringLength(ExpenseRules.DescriptionMaxLength, MinimumLength = ExpenseRules.DescriptionMinLength)]
    public string? Description { get; init; }

    [Required]
    [Range(typeof(decimal), "0.01", "2147483647", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? Amount { get; init; }

    [Required]
    public DateOnly? ExpenseDate { get; init; }

    public int? CategoryId { get; init; }
}

/// <summary>Body of <c>POST /api/expenses/{id}/reject</c>.</summary>
internal sealed class RejectExpenseRequest
{
    [Required]
    [StringLength(ExpenseRules.ReasonMaxLength, MinimumLength = ExpenseRules.ReasonMinLength)]
    public string? Reason { get; init; }
}

/// <summary>User returned by the account and admin endpoints.</summary>
internal sealed record UserResponse(string Id, string Email, IReadOnlyList<string> Roles);

/// <summary>Result of a successful credential check.</summary>
internal sealed record AuthenticatedUser(string Id, string Email, IReadOnlyList<string> Roles);

/// <summary>Expense returned by the API.</summary>
internal sealed record ExpenseResponse(
    Guid Id,
    string OwnerId,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate,
    int? CategoryId,
    ExpenseStatus Status,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static ExpenseResponse From(Expense expense)
    {
        return new ExpenseResponse(
            expense.Id,
            expense.OwnerId,
            expense.Description,
            expense.Amount,
            expense.ExpenseDate,
            expense.CategoryId,
            expense.Status,
            expense.RejectionReason,
            expense.CreatedAtUtc,
            expense.UpdatedAtUtc);
    }
}

/// <summary>History entry returned by the API.</summary>
internal sealed record ExpenseHistoryResponse(
    long Id,
    Guid ExpenseId,
    ExpenseAction Action,
    string ActorId,
    DateTime OccurredAtUtc,
    ExpenseStatus? PreviousStatus,
    ExpenseStatus NewStatus,
    string? Reason,
    string? Changes)
{
    public static ExpenseHistoryResponse From(ExpenseHistory history)
    {
        return new ExpenseHistoryResponse(
            history.Id,
            history.ExpenseId,
            history.Action,
            history.ActorId,
            history.OccurredAtUtc,
            history.PreviousStatus,
            history.NewStatus,
            history.Reason,
            history.Changes);
    }
}
