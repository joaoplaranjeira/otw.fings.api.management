using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

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
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<BudgetCategoryAllocation> BudgetCategoryAllocations => Set<BudgetCategoryAllocation>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseLine> ExpenseLines => Set<ExpenseLine>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<ReceiptParseRecord> ReceiptParseRecords => Set<ReceiptParseRecord>();
    public DbSet<NotificationEventOutbox> NotificationEventOutbox => Set<NotificationEventOutbox>();
    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

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
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.BirthDate).HasConversion(NullableDateOnlyConverter).HasColumnType("date");
            entity.HasIndex(x => new { x.HouseholdId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Household).WithMany(x => x.Members)
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdInvitation>(entity =>
        {
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.CodeHash).HasMaxLength(64).IsFixedLength();
            entity.HasIndex(x => x.CodeHash).IsUnique();
            entity.HasIndex(x => new { x.HouseholdId, x.Email, x.Status });
            entity.HasOne(x => x.Household).WithMany(x => x.Invitations)
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AcceptedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<NotificationEventOutbox>(entity =>
        {
            entity.Property(x => x.EventType).HasMaxLength(100);
            entity.Property(x => x.AggregateType).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(300);
            entity.Property(x => x.PayloadJson).HasColumnType("json");
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.ProcessedAtUtc, x.AvailableAtUtc });
            entity.HasIndex(x => new { x.HouseholdId, x.EventType, x.OccurredAtUtc });
        });

        modelBuilder.Entity<NotificationRule>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.EventType).HasMaxLength(100);
            entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.ConditionType).HasMaxLength(100);
            entity.Property(x => x.ConditionJson).HasColumnType("json");
            entity.Property(x => x.RecipientPolicy).HasMaxLength(100);
            entity.Property(x => x.RecipientConfigurationJson).HasColumnType("json");
            entity.Property(x => x.TemplateKey).HasMaxLength(150);
            entity.Property(x => x.ChannelsJson).HasColumnType("json");
            entity.HasIndex(x => new { x.EventType, x.IsEnabled, x.HouseholdId });
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.Property(x => x.NotificationType).HasMaxLength(100);
            entity.Property(x => x.Channel).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(x => new { x.UserId, x.HouseholdId, x.NotificationType, x.Channel }).IsUnique();
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.Property(x => x.TemplateKey).HasMaxLength(150);
            entity.Property(x => x.Channel).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Locale).HasMaxLength(20);
            entity.Property(x => x.TitleTemplate).HasMaxLength(250);
            entity.Property(x => x.BodyTemplate).HasMaxLength(1000);
            entity.Property(x => x.ActionUrlTemplate).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TemplateKey, x.Channel, x.Locale }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(x => x.NotificationType).HasMaxLength(100);
            entity.Property(x => x.Title).HasMaxLength(250);
            entity.Property(x => x.Body).HasMaxLength(1000);
            entity.Property(x => x.ActionUrl).HasMaxLength(1000);
            entity.Property(x => x.DeduplicationKey).HasMaxLength(400);
            entity.HasIndex(x => new { x.UserId, x.DeduplicationKey }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ReadAtUtc, x.CreatedAtUtc });
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.Property(x => x.Channel).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.Property(x => x.ProviderMessageId).HasMaxLength(500);
            entity.HasIndex(x => new { x.Status, x.AvailableAtUtc });
            entity.HasIndex(x => new { x.NotificationId, x.Channel, x.DestinationId }).IsUnique();
            entity.HasOne(x => x.Notification).WithMany(x => x.Deliveries)
                .HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.Property(x => x.Endpoint).HasMaxLength(2048);
            entity.Property(x => x.EndpointHash).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.P256dh).HasMaxLength(255);
            entity.Property(x => x.Auth).HasMaxLength(255);
            entity.Property(x => x.DeviceName).HasMaxLength(200);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasIndex(x => x.EndpointHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.IsActive });
        });

        var seededAt = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        modelBuilder.Entity<NotificationTemplate>().HasData(
            new NotificationTemplate
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), TemplateKey = "expense-created",
                Channel = NotificationChannelType.WebPush, Locale = "pt-PT",
                TitleTemplate = "Nova despesa em {{householdName}}",
                BodyTemplate = "{{actorName}} adicionou {{amount}} € — {{description}}",
                ActionUrlTemplate = "/?view=expenses&expenseId={{expenseId}}", IsActive = true, CreatedAtUtc = seededAt
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), TemplateKey = "expense-created",
                Channel = NotificationChannelType.InApp, Locale = "pt-PT",
                TitleTemplate = "Nova despesa em {{householdName}}",
                BodyTemplate = "{{actorName}} adicionou {{amount}} € — {{description}}",
                ActionUrlTemplate = "/?view=expenses&expenseId={{expenseId}}", IsActive = true, CreatedAtUtc = seededAt
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), TemplateKey = "budget-threshold-reached",
                Channel = NotificationChannelType.WebPush, Locale = "pt-PT",
                TitleTemplate = "Orçamento em {{currentPercentage}}%",
                BodyTemplate = "O agregado {{householdName}} gastou {{currentSpentAmount}} € do orçamento.",
                ActionUrlTemplate = "/?view=budget", IsActive = true, CreatedAtUtc = seededAt
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), TemplateKey = "budget-threshold-reached",
                Channel = NotificationChannelType.InApp, Locale = "pt-PT",
                TitleTemplate = "Orçamento em {{currentPercentage}}%",
                BodyTemplate = "O agregado {{householdName}} gastou {{currentSpentAmount}} € do orçamento.",
                ActionUrlTemplate = "/?view=budget", IsActive = true, CreatedAtUtc = seededAt
            });
        modelBuilder.Entity<NotificationRule>().HasData(
            new NotificationRule
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Name = "Nova despesa",
                EventType = "Expense.Created", Scope = NotificationScope.System, ConditionType = "Always",
                ConditionJson = "{}", RecipientPolicy = "HouseholdMembersExceptActor", TemplateKey = "expense-created",
                ChannelsJson = "[\"WebPush\",\"InApp\"]", Priority = 100, IsEnabled = true, CreatedAtUtc = seededAt
            },
            new NotificationRule
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Name = "Orçamento a 80%",
                EventType = "Budget.UsageChanged", Scope = NotificationScope.System, ConditionType = "BudgetThresholdCrossed",
                ConditionJson = "{\"thresholdPercentage\":80,\"direction\":\"up\"}", RecipientPolicy = "AllHouseholdMembers",
                TemplateKey = "budget-threshold-reached", ChannelsJson = "[\"WebPush\",\"InApp\"]",
                CooldownSeconds = 86400, Priority = 100, IsEnabled = true, CreatedAtUtc = seededAt
            },
            new NotificationRule
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Name = "Orçamento a 100%",
                EventType = "Budget.UsageChanged", Scope = NotificationScope.System, ConditionType = "BudgetThresholdCrossed",
                ConditionJson = "{\"thresholdPercentage\":100,\"direction\":\"up\"}", RecipientPolicy = "AllHouseholdMembers",
                TemplateKey = "budget-threshold-reached", ChannelsJson = "[\"WebPush\",\"InApp\"]",
                CooldownSeconds = 86400, Priority = 100, IsEnabled = true, CreatedAtUtc = seededAt
            },
            new NotificationRule
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Name = "Orçamento a 50%",
                EventType = "Budget.UsageChanged", Scope = NotificationScope.System, ConditionType = "BudgetThresholdCrossed",
                ConditionJson = "{\"thresholdPercentage\":50,\"direction\":\"up\"}", RecipientPolicy = "AllHouseholdMembers",
                TemplateKey = "budget-threshold-reached", ChannelsJson = "[\"WebPush\",\"InApp\"]",
                CooldownSeconds = 86400, Priority = 100, IsEnabled = true, CreatedAtUtc = seededAt
            });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await AddNotificationEventsAsync(now, cancellationToken);
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

        var result = await base.SaveChangesAsync(cancellationToken);
        foreach (var expense in ChangeTracker.Entries<Expense>()) expense.Entity.NotificationEventsCaptured = false;
        return result;
    }

    private async Task AddNotificationEventsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        var existingKeys = ChangeTracker.Entries<NotificationEventOutbox>()
            .Select(x => x.Entity.IdempotencyKey).ToHashSet(StringComparer.Ordinal);
        var mutations = ChangeTracker.Entries<Expense>()
            .Where(x => (x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) && !x.Entity.NotificationEventsCaptured)
            .Select(entry => ExpenseMutation.From(entry))
            .Where(x => x.HasNotificationChange)
            .ToArray();

        foreach (var mutation in mutations)
        {
            var eventType = mutation.IsDeleted ? "Expense.Deleted" : mutation.IsCreated ? "Expense.Created" : "Expense.Updated";
            var key = mutation.IsCreated
                ? $"expense-created:{mutation.Id}"
                : $"{eventType.ToLowerInvariant()}:{mutation.Id}:{Guid.NewGuid():N}";
            if (!existingKeys.Add(key)) continue;
            NotificationEventOutbox.Add(CreateOutbox(
                eventType, mutation.HouseholdId, mutation.ActorUserId, mutation.Id, "Expense", key, now,
                new
                {
                    expenseId = mutation.Id,
                    amount = mutation.NewAmount ?? mutation.OldAmount,
                    previousAmount = mutation.OldAmount,
                    currency = "EUR",
                    description = mutation.Description,
                    merchantName = mutation.MerchantName,
                    categoryId = mutation.NewCategoryId ?? mutation.OldCategoryId,
                    date = mutation.NewDate ?? mutation.OldDate,
                    origin = mutation.Origin.ToString()
                }));

            if (mutation.IsCreated && mutation.Origin == ExpenseOrigin.Recurring)
            {
                var recurringKey = $"recurring-materialized:{mutation.Id}";
                if (existingKeys.Add(recurringKey))
                {
                    NotificationEventOutbox.Add(CreateOutbox(
                        "RecurringExpense.Materialized", mutation.HouseholdId, mutation.ActorUserId,
                        mutation.Id, "Expense", recurringKey, now,
                        new { expenseId = mutation.Id, recurringExpenseId = mutation.RecurringExpenseId, amount = mutation.NewAmount, date = mutation.NewDate }));
                }
            }
        }

        foreach (var entry in ChangeTracker.Entries<Expense>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
            entry.Entity.NotificationEventsCaptured = true;

        foreach (var entry in ChangeTracker.Entries<HouseholdMember>().Where(x => x.State == EntityState.Added && x.Entity.UserId.HasValue).ToArray())
        {
            var member = entry.Entity;
            var key = $"household-member-added:{member.Id}";
            if (existingKeys.Add(key)) NotificationEventOutbox.Add(CreateOutbox(
                "Household.MemberAdded", member.HouseholdId, member.UserId, member.Id, "HouseholdMember", key, now,
                new { memberId = member.Id, userId = member.UserId, role = member.Role?.ToString(), name = member.Name }));
        }

        foreach (var entry in ChangeTracker.Entries<HouseholdInvitation>().Where(x => x.State == EntityState.Modified &&
                     x.Entity.Status == HouseholdInvitationStatus.Accepted &&
                     x.OriginalValues.GetValue<HouseholdInvitationStatus>(nameof(HouseholdInvitation.Status)) != HouseholdInvitationStatus.Accepted).ToArray())
        {
            var invitation = entry.Entity;
            var key = $"household-invitation-accepted:{invitation.Id}";
            if (existingKeys.Add(key)) NotificationEventOutbox.Add(CreateOutbox(
                "Household.InvitationAccepted", invitation.HouseholdId, invitation.AcceptedByUserId,
                invitation.Id, "HouseholdInvitation", key, now,
                new { invitationId = invitation.Id, userId = invitation.AcceptedByUserId, role = invitation.Role.ToString() }));
        }

        await AddBudgetUsageEventsAsync(mutations, existingKeys, now, cancellationToken);
    }

    private async Task AddBudgetUsageEventsAsync(
        IReadOnlyList<ExpenseMutation> mutations,
        HashSet<string> existingKeys,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var affected = mutations.SelectMany(x => x.AffectedMonths())
            .Distinct().ToArray();
        foreach (var item in affected)
        {
            var monthStart = new DateOnly(item.Month.Year, item.Month.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var budget = await BudgetPeriods.AsNoTracking().Include(x => x.Allocations).SingleOrDefaultAsync(x =>
                x.HouseholdId == item.HouseholdId && x.Status == BudgetStatus.Active &&
                x.StartMonth <= monthStart && x.EndMonth >= monthStart, cancellationToken);
            if (budget is null || budget.MonthlyAmount <= 0) continue;

            var persistedSpent = await Expenses.AsNoTracking().Where(x =>
                x.HouseholdId == item.HouseholdId && x.Date >= monthStart && x.Date <= monthEnd &&
                x.Status == FinancialRecordStatus.Confirmed).SumAsync(x => x.Amount, cancellationToken);
            var oldContribution = mutations.Sum(x => x.OldContribution(item.HouseholdId, monthStart, monthEnd));
            var newContribution = mutations.Sum(x => x.NewContribution(item.HouseholdId, monthStart, monthEnd));
            var previous = persistedSpent;
            var current = persistedSpent - oldContribution + newContribution;
            if (previous == current) continue;

            var key = $"budget-usage:{budget.Id}:{monthStart:yyyy-MM}:{Guid.NewGuid():N}";
            if (!existingKeys.Add(key)) continue;
            NotificationEventOutbox.Add(CreateOutbox(
                "Budget.UsageChanged", item.HouseholdId, mutations.LastOrDefault()?.ActorUserId,
                budget.Id, "Budget", key, now,
                new
                {
                    budgetId = budget.Id,
                    period = monthStart.ToString("yyyy-MM"),
                    categoryId = (Guid?)null,
                    budgetAmount = budget.MonthlyAmount,
                    previousSpentAmount = previous,
                    currentSpentAmount = current,
                    previousPercentage = decimal.Round(previous / budget.MonthlyAmount * 100, 2),
                    currentPercentage = decimal.Round(current / budget.MonthlyAmount * 100, 2)
                }));

            var categoryIds = mutations.SelectMany(x => x.AffectedCategories(item.HouseholdId, monthStart, monthEnd)).Distinct().ToArray();
            foreach (var categoryId in categoryIds)
            {
                var allocation = budget.Allocations.SingleOrDefault(x => x.CategoryId == categoryId);
                if (allocation is null || allocation.MonthlyAmount <= 0) continue;
                var persistedCategorySpent = await Expenses.AsNoTracking().Where(x =>
                    x.HouseholdId == item.HouseholdId && x.Date >= monthStart && x.Date <= monthEnd &&
                    x.CategoryId == categoryId && x.Status == FinancialRecordStatus.Confirmed)
                    .SumAsync(x => x.Amount, cancellationToken);
                var oldCategoryContribution = mutations.Sum(x => x.OldCategoryContribution(item.HouseholdId, categoryId, monthStart, monthEnd));
                var newCategoryContribution = mutations.Sum(x => x.NewCategoryContribution(item.HouseholdId, categoryId, monthStart, monthEnd));
                var previousCategory = persistedCategorySpent;
                var currentCategory = persistedCategorySpent - oldCategoryContribution + newCategoryContribution;
                if (previousCategory == currentCategory) continue;
                var categoryKey = $"budget-usage:{budget.Id}:{monthStart:yyyy-MM}:category:{categoryId}:{Guid.NewGuid():N}";
                if (!existingKeys.Add(categoryKey)) continue;
                NotificationEventOutbox.Add(CreateOutbox(
                    "Budget.UsageChanged", item.HouseholdId, mutations.LastOrDefault()?.ActorUserId,
                    budget.Id, "Budget", categoryKey, now,
                    new
                    {
                        budgetId = budget.Id,
                        period = monthStart.ToString("yyyy-MM"),
                        categoryId,
                        budgetAmount = allocation.MonthlyAmount,
                        previousSpentAmount = previousCategory,
                        currentSpentAmount = currentCategory,
                        previousPercentage = decimal.Round(previousCategory / allocation.MonthlyAmount * 100, 2),
                        currentPercentage = decimal.Round(currentCategory / allocation.MonthlyAmount * 100, 2)
                    }));
            }
        }
    }

    private static NotificationEventOutbox CreateOutbox(
        string eventType, Guid? householdId, long? actorUserId, Guid? aggregateId,
        string aggregateType, string key, DateTimeOffset now, object payload) => new()
        {
            EventType = eventType,
            SchemaVersion = 1,
            HouseholdId = householdId,
            ActorUserId = actorUserId,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            IdempotencyKey = key,
            PayloadJson = JsonSerializer.Serialize(payload),
            OccurredAtUtc = now,
            AvailableAtUtc = now
        };

    private sealed record ExpenseMutation(
        Guid Id, Guid HouseholdId, long? ActorUserId, ExpenseOrigin Origin, Guid? RecurringExpenseId,
        string Description, string? MerchantName, DateOnly? OldDate, DateOnly? NewDate,
        decimal? OldAmount, decimal? NewAmount, Guid? OldCategoryId, Guid? NewCategoryId,
        FinancialRecordStatus? OldStatus, FinancialRecordStatus? NewStatus, bool IsCreated, bool IsDeleted)
    {
        public bool HasNotificationChange => true;

        public static ExpenseMutation From(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Expense> entry)
        {
            var expense = entry.Entity;
            var added = entry.State == EntityState.Added;
            var deleted = entry.State == EntityState.Deleted ||
                (!added && entry.OriginalValues.GetValue<FinancialRecordStatus>(nameof(Expense.Status)) != FinancialRecordStatus.Cancelled &&
                 expense.Status == FinancialRecordStatus.Cancelled);
            return new ExpenseMutation(
                expense.Id, expense.HouseholdId, expense.NotificationActorUserId ?? expense.CreatedByUserId,
                expense.Origin, expense.RecurringExpenseId, expense.Description, expense.MerchantName,
                added ? null : entry.OriginalValues.GetValue<DateOnly>(nameof(Expense.Date)),
                deleted ? null : expense.Date,
                added ? null : entry.OriginalValues.GetValue<decimal>(nameof(Expense.Amount)),
                deleted ? null : expense.Amount,
                added ? null : entry.OriginalValues.GetValue<Guid>(nameof(Expense.CategoryId)),
                deleted ? null : expense.CategoryId,
                added ? null : entry.OriginalValues.GetValue<FinancialRecordStatus>(nameof(Expense.Status)),
                deleted ? null : expense.Status,
                added, deleted);
        }

        public IEnumerable<(Guid HouseholdId, DateOnly Month)> AffectedMonths()
        {
            if (OldDate.HasValue) yield return (HouseholdId, new DateOnly(OldDate.Value.Year, OldDate.Value.Month, 1));
            if (NewDate.HasValue) yield return (HouseholdId, new DateOnly(NewDate.Value.Year, NewDate.Value.Month, 1));
        }

        public decimal OldContribution(Guid householdId, DateOnly from, DateOnly to) =>
            HouseholdId == householdId && OldDate >= from && OldDate <= to && OldStatus == FinancialRecordStatus.Confirmed ? OldAmount ?? 0 : 0;

        public decimal NewContribution(Guid householdId, DateOnly from, DateOnly to) =>
            HouseholdId == householdId && NewDate >= from && NewDate <= to && NewStatus == FinancialRecordStatus.Confirmed ? NewAmount ?? 0 : 0;

        public IEnumerable<Guid> AffectedCategories(Guid householdId, DateOnly from, DateOnly to)
        {
            if (HouseholdId != householdId) yield break;
            if (OldDate >= from && OldDate <= to && OldCategoryId.HasValue) yield return OldCategoryId.Value;
            if (NewDate >= from && NewDate <= to && NewCategoryId.HasValue) yield return NewCategoryId.Value;
        }

        public decimal OldCategoryContribution(Guid householdId, Guid categoryId, DateOnly from, DateOnly to) =>
            HouseholdId == householdId && OldCategoryId == categoryId && OldDate >= from && OldDate <= to && OldStatus == FinancialRecordStatus.Confirmed ? OldAmount ?? 0 : 0;

        public decimal NewCategoryContribution(Guid householdId, Guid categoryId, DateOnly from, DateOnly to) =>
            HouseholdId == householdId && NewCategoryId == categoryId && NewDate >= from && NewDate <= to && NewStatus == FinancialRecordStatus.Confirmed ? NewAmount ?? 0 : 0;
    }
}
