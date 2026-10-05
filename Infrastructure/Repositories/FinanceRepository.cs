using Microsoft.EntityFrameworkCore;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;

namespace otw.fings.api.management.Infrastructure.Repositories;

public sealed class FinanceRepository(FingsDbContext dbContext) : IFinanceRepository
{
    public Task<bool> IsMemberAsync(Guid householdId, long userId, CancellationToken cancellationToken) =>
        dbContext.HouseholdMembers.AnyAsync(x => x.HouseholdId == householdId && x.UserId == userId, cancellationToken);

    public async Task<HouseholdRole?> GetMemberRoleAsync(Guid householdId, long userId, CancellationToken cancellationToken) =>
        await dbContext.HouseholdMembers
            .Where(x => x.HouseholdId == householdId && x.UserId == userId)
            .Select(x => (HouseholdRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<HouseholdResponse>> GetHouseholdsAsync(long userId, CancellationToken cancellationToken) =>
        await dbContext.HouseholdMembers.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Household.Name)
            .Select(x => new HouseholdResponse(x.HouseholdId, x.Household.Name, x.Household.Currency, x.Household.TimeZone, x.Role))
            .ToListAsync(cancellationToken);

    public async Task AddHouseholdAsync(
        Household household,
        HouseholdMember membership,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken)
    {
        dbContext.Households.Add(household);
        dbContext.HouseholdMembers.Add(membership);
        dbContext.Categories.AddRange(categories);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HouseholdMemberResponse>> GetHouseholdMembersAsync(
        Guid householdId,
        CancellationToken cancellationToken) =>
        await dbContext.HouseholdMembers.AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.Role)
            .ThenBy(x => x.User.Name)
            .Select(x => new HouseholdMemberResponse(
                x.Id,
                x.UserId,
                x.User.Name,
                x.User.Username,
                x.User.Email,
                x.User.IsActive,
                x.Role))
            .ToListAsync(cancellationToken);

    public Task<HouseholdMember?> GetHouseholdMemberAsync(
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken) =>
        dbContext.HouseholdMembers
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == memberId, cancellationToken);

    public Task<bool> HasHouseholdMemberAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken) =>
        dbContext.HouseholdMembers.AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == userId,
            cancellationToken);

    public Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

    public Task<int> CountHouseholdOwnersAsync(Guid householdId, CancellationToken cancellationToken) =>
        dbContext.HouseholdMembers.CountAsync(
            x => x.HouseholdId == householdId && x.Role == HouseholdRole.Owner,
            cancellationToken);

    public async Task AddHouseholdMemberAsync(HouseholdMember member, CancellationToken cancellationToken)
    {
        dbContext.HouseholdMembers.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveHouseholdMemberAsync(HouseholdMember member, CancellationToken cancellationToken)
    {
        dbContext.HouseholdMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(Guid householdId, CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking().Include(x => x.Subcategories)
            .Where(x => x.HouseholdId == householdId && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Category?> GetCategoryAsync(Guid householdId, Guid categoryId, CancellationToken cancellationToken) =>
        dbContext.Categories.Include(x => x.Subcategories)
            .FirstOrDefaultAsync(x => x.Id == categoryId && x.HouseholdId == householdId && x.IsActive, cancellationToken);

    public Task<Subcategory?> GetSubcategoryAsync(Guid categoryId, Guid subcategoryId, CancellationToken cancellationToken) =>
        dbContext.Subcategories.FirstOrDefaultAsync(x => x.Id == subcategoryId && x.CategoryId == categoryId && x.IsActive, cancellationToken);

    public async Task AddCategoryAsync(Category category, CancellationToken cancellationToken)
    {
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSubcategoryAsync(Subcategory subcategory, CancellationToken cancellationToken)
    {
        dbContext.Subcategories.Add(subcategory);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasOverlappingBudgetAsync(
        Guid householdId,
        DateOnly startMonth,
        DateOnly endMonth,
        CancellationToken cancellationToken) =>
        dbContext.BudgetPeriods.AnyAsync(
            x => x.HouseholdId == householdId &&
                 x.Status == BudgetStatus.Active &&
                 x.StartMonth <= endMonth && x.EndMonth >= startMonth,
            cancellationToken);

    public async Task AddBudgetAsync(BudgetPeriod budget, IReadOnlyList<Income> incomes, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.BudgetPeriods.Add(budget);
        dbContext.Incomes.AddRange(incomes);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetPeriod>> GetBudgetsAsync(Guid householdId, CancellationToken cancellationToken) =>
        await dbContext.BudgetPeriods.AsNoTracking()
            .Include(x => x.Allocations).ThenInclude(x => x.Category)
            .Include(x => x.GeneratedIncomes)
            .Where(x => x.HouseholdId == householdId)
            .OrderByDescending(x => x.StartMonth)
            .ToListAsync(cancellationToken);

    public Task<BudgetPeriod?> GetBudgetForMonthAsync(Guid householdId, DateOnly month, CancellationToken cancellationToken) =>
        dbContext.BudgetPeriods.AsNoTracking()
            .Include(x => x.Allocations).ThenInclude(x => x.Category)
            .Where(x => x.HouseholdId == householdId && x.Status == BudgetStatus.Active && x.StartMonth <= month && x.EndMonth >= month)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Income>> GetIncomesAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.Incomes.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.Date >= from && x.Date <= to && x.Status != FinancialRecordStatus.Cancelled)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);

    public async Task AddExpenseAsync(Expense expense, CancellationToken cancellationToken)
    {
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Expense?> GetExpenseAsync(Guid householdId, Guid expenseId, CancellationToken cancellationToken) =>
        dbContext.Expenses
            .Include(x => x.Category).Include(x => x.Subcategory)
            .Include(x => x.Lines).ThenInclude(x => x.Category)
            .Include(x => x.Lines).ThenInclude(x => x.Subcategory)
            .SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == expenseId, cancellationToken);

    public async Task<IReadOnlyList<Expense>> GetExpensesAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.Expenses.AsNoTracking()
            .Include(x => x.Category).Include(x => x.Subcategory)
            .Include(x => x.Lines).ThenInclude(x => x.Category)
            .Include(x => x.Lines).ThenInclude(x => x.Subcategory)
            .Where(x => x.HouseholdId == householdId && x.Date >= from && x.Date <= to && x.Status != FinancialRecordStatus.Cancelled)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FrequentExpenseSuggestionResponse>> GetFrequentExpenseSuggestionsAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken)
    {
        var groups = await dbContext.Expenses.AsNoTracking()
            .Where(x =>
                x.HouseholdId == householdId &&
                x.Date >= from &&
                x.Date <= to &&
                x.Status != FinancialRecordStatus.Cancelled &&
                x.MerchantName != null)
            .GroupBy(x => new
            {
                MerchantName = x.MerchantName!,
                x.MerchantTaxNumber,
                x.CategoryId,
                CategoryName = x.Category.Name,
                x.SubcategoryId,
                SubcategoryName = x.Subcategory != null ? x.Subcategory.Name : null
            })
            .Select(group => new
            {
                group.Key,
                OccurrenceCount = group.Count(),
                LastOccurrenceDate = group.Max(x => x.Date)
            })
            .OrderByDescending(x => x.OccurrenceCount)
            .ThenByDescending(x => x.LastOccurrenceDate)
            .ThenBy(x => x.Key.MerchantName)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return groups.Select(group => new FrequentExpenseSuggestionResponse(
            group.Key.MerchantName,
            group.Key.MerchantTaxNumber,
            group.Key.CategoryId,
            group.Key.CategoryName,
            group.Key.SubcategoryId,
            group.Key.SubcategoryName,
            group.OccurrenceCount,
            group.LastOccurrenceDate)).ToArray();
    }

    public async Task AddRecurringExpenseAsync(RecurringExpense recurringExpense, CancellationToken cancellationToken)
    {
        dbContext.RecurringExpenses.Add(recurringExpense);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<RecurringExpense?> GetRecurringExpenseAsync(Guid householdId, Guid recurringExpenseId, CancellationToken cancellationToken) =>
        dbContext.RecurringExpenses.SingleOrDefaultAsync(
            x => x.HouseholdId == householdId && x.Id == recurringExpenseId,
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<RecurringExpense>> GetRecurringExpensesAsync(Guid householdId, CancellationToken cancellationToken) =>
        await dbContext.RecurringExpenses.AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.NextOccurrenceDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecurringExpense>> GetDueRecurringExpensesAsync(DateOnly through, CancellationToken cancellationToken) =>
        await dbContext.RecurringExpenses
            .Where(x => x.IsActive && x.NextOccurrenceDate <= through && (!x.EndDate.HasValue || x.NextOccurrenceDate <= x.EndDate))
            .ToListAsync(cancellationToken);

    public async Task MaterializeRecurringExpensesAsync(IReadOnlyList<Expense> expenses, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Expenses.AddRange(expenses);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddReceiptParseRecordAsync(ReceiptParseRecord record, CancellationToken cancellationToken)
    {
        dbContext.ReceiptParseRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ReceiptParseRecord?> GetReceiptParseRecordAsync(
        Guid householdId,
        Guid parseId,
        CancellationToken cancellationToken) =>
        dbContext.ReceiptParseRecords.SingleOrDefaultAsync(
            x => x.HouseholdId == householdId && x.Id == parseId,
            cancellationToken);

    public async Task<IReadOnlyList<ReceiptParseRecord>> GetReceiptParseRecordsAsync(
        Guid householdId,
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.ReceiptParseRecords.AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<ReceiptParseValidationStatus, int>> GetReceiptParseStatusCountsAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var counts = await dbContext.ReceiptParseRecords.AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .GroupBy(x => x.ValidationStatus)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        return counts.ToDictionary(x => x.Status, x => x.Count);
    }
}
