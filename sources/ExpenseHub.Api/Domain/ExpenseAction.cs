namespace ExpenseHub.Api.Domain;

/// <summary>Actions recorded in the expense history.</summary>
internal enum ExpenseAction
{
    Created,
    Updated,
    Submitted,
    Approved,
    Rejected,
    Paid,
}
