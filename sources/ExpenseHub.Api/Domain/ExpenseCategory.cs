namespace ExpenseHub.Api.Domain;

/// <summary>Category used to classify expenses.</summary>
internal sealed class ExpenseCategory
{
    public ExpenseCategory(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }
}
