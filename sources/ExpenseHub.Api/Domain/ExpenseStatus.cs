namespace ExpenseHub.Api.Domain;

/// <summary>Lifecycle states of an expense. <see cref="Rejected"/> and <see cref="Paid"/> are final.</summary>
internal enum ExpenseStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Paid,
}
