using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace otw.fings.api.management.Infrastructure.Data;

public sealed class FingsDbContext(DbContextOptions<FingsDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateOnly, DateTime> DateOnlyConverter = new(
        value => value.ToDateTime(TimeOnly.MinValue),
        value => DateOnly.FromDateTime(value));

    private static readonly ValueConverter<DateOnly?, DateTime?> NullableDateOnlyConverter = new(
        value => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : null,
        value => value.HasValue ? DateOnly.FromDateTime(value.Value) : null);

    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<BudgetCategoryAllocation> BudgetCategoryAllocations => Set<BudgetCategoryAllocation>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseLine> ExpenseLines => Set<ExpenseLine>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<ReceiptParseRecord> ReceiptParseRecords => Set<ReceiptParseRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Username).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.Property(x => x.Role).HasMaxLength(100);
            entity.Property(x => x.Profile).HasMaxLength(100);
            entity.Property(x => x.InsertedUser).HasMaxLength(200);
            entity.Property(x => x.UpdatedUser).HasMaxLength(200);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.Property(x => x.PermissionKey).HasMaxLength(150);
            entity.HasIndex(x => new { x.UserId, x.PermissionKey }).IsUnique();
        });

        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.Property(x => x.Code).HasMaxLength(6);
            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasIndex(x => new { x.Email, x.CreatedAt });
        });

        modelBuilder.Entity<Household>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.TimeZone).HasMaxLength(100);
        });

        modelBuilder.Entity<HouseholdMember>(entity =>
        {
            entity.HasIndex(x => new { x.HouseholdId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Household).WithMany(x => x.Members)
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Color).HasMaxLength(20);
            entity.Property(x => x.Icon).HasMaxLength(100);
            entity.HasIndex(x => new { x.HouseholdId, x.Name }).IsUnique();
            entity.HasOne(x => x.Household).WithMany().HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subcategory>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.HasIndex(x => new { x.CategoryId, x.Name }).IsUnique();
            entity.HasOne(x => x.Category).WithMany(x => x.Subcategories)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetPeriod>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.StartMonth).HasConversion(DateOnlyConverter).HasColumnType("date");
            entity.Property(x => x.EndMonth).HasConversion(DateOnlyConverter).HasColumnType("date");
            entity.Property(x => x.MonthlyAmount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.HouseholdId, x.StartMonth, x.EndMonth });
            entity.HasOne(x => x.Household).WithMany().HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetCategoryAllocation>(entity =>
        {
            entity.Property(x => x.MonthlyAmount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.BudgetPeriodId, x.CategoryId }).IsUnique();
            entity.HasOne(x => x.BudgetPeriod).WithMany(x => x.Allocations)
                .HasForeignKey(x => x.BudgetPeriodId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Income>(entity =>
        {
            entity.Property(x => x.Date).HasConversion(DateOnlyConverter).HasColumnType("date");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => new { x.BudgetPeriodId, x.Date }).IsUnique();
            entity.HasOne(x => x.BudgetPeriod).WithMany(x => x.GeneratedIncomes)
                .HasForeignKey(x => x.BudgetPeriodId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(x => x.Date).HasConversion(DateOnlyConverter).HasColumnType("date");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.MerchantName).HasMaxLength(250);
            entity.Property(x => x.MerchantTaxNumber).HasMaxLength(32);
            entity.HasIndex(x => new { x.HouseholdId, x.Date });
            entity.HasIndex(x => new { x.RecurringExpenseId, x.Date }).IsUnique();
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Subcategory).WithMany().HasForeignKey(x => x.SubcategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RecurringExpense).WithMany(x => x.Expenses).HasForeignKey(x => x.RecurringExpenseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseLine>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.ExpenseId, x.Position }).IsUnique();
            entity.HasOne(x => x.Expense).WithMany(x => x.Lines).HasForeignKey(x => x.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Subcategory).WithMany().HasForeignKey(x => x.SubcategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecurringExpense>(entity =>
        {
            entity.Property(x => x.StartDate).HasConversion(DateOnlyConverter).HasColumnType("date");
            entity.Property(x => x.EndDate).HasConversion(NullableDateOnlyConverter).HasColumnType("date");
            entity.Property(x => x.NextOccurrenceDate).HasConversion(DateOnlyConverter).HasColumnType("date");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.MerchantName).HasMaxLength(250);
            entity.Property(x => x.MerchantTaxNumber).HasMaxLength(32);
            entity.HasOne(x => x.Household).WithMany().HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Subcategory).WithMany().HasForeignKey(x => x.SubcategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReceiptParseRecord>(entity =>
        {
            entity.Property(x => x.Model).HasMaxLength(100);
            entity.Property(x => x.AverageConfidence).HasPrecision(5, 4);
            entity.Property(x => x.ValidationNotes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.HouseholdId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.HouseholdId, x.ValidationStatus });
            entity.HasOne(x => x.Household).WithMany().HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ValidatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
