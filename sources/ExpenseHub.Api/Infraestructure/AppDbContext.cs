using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Infrastructure;

/// <summary>Entity Framework Core context holding the Identity tables and the expense workflow.</summary>
internal sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<ExpenseHistory> ExpenseHistories => Set<ExpenseHistory>();

    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(category => category.Name).IsUnique();
            entity.HasData(
                new ExpenseCategory(1, "Travel"),
                new ExpenseCategory(2, "Meals"),
                new ExpenseCategory(3, "Lodging"),
                new ExpenseCategory(4, "Transport"),
                new ExpenseCategory(5, "Other"));
        });

        builder.Entity<Expense>(entity =>
        {
            entity.HasKey(expense => expense.Id);
            entity.Property(expense => expense.Id).ValueGeneratedNever();
            entity.Property(expense => expense.OwnerId).IsRequired().HasMaxLength(450);
            entity.Property(expense => expense.Description).IsRequired().HasMaxLength(ExpenseRules.DescriptionMaxLength);
            entity.Property(expense => expense.Amount).HasPrecision(18, 2);
            entity.Property(expense => expense.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(expense => expense.RejectionReason).HasMaxLength(ExpenseRules.ReasonMaxLength);
            entity.Property(expense => expense.Version).IsConcurrencyToken();
            entity.HasIndex(expense => expense.OwnerId);
            entity.HasIndex(expense => expense.Status);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(expense => expense.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExpenseCategory>().WithMany().HasForeignKey(expense => expense.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExpenseHistory>(entity =>
        {
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).ValueGeneratedOnAdd();
            entity.Property(history => history.Action).HasConversion<string>().HasMaxLength(20);
            entity.Property(history => history.ActorId).IsRequired().HasMaxLength(450);
            entity.Property(history => history.PreviousStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(history => history.NewStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(history => history.Reason).HasMaxLength(ExpenseRules.ReasonMaxLength);
            entity.Property(history => history.Changes).HasMaxLength(2000);
            entity.HasIndex(history => history.ExpenseId);
            entity.HasOne<Expense>().WithMany().HasForeignKey(history => history.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentRecord>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Id).ValueGeneratedNever();
            entity.Property(payment => payment.PaidByUserId).IsRequired().HasMaxLength(450);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.HasOne<Expense>().WithOne().HasForeignKey<PaymentRecord>(payment => payment.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
