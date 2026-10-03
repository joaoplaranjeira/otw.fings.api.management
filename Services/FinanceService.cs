using otw.fings.api.management.Domain.Catalogs;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Services;

public sealed class FinanceService(
    IFinanceRepository repository,
    IRecurringExpenseMaterializer? materializer = null) : IFinanceService
{
    private readonly IRecurringExpenseMaterializer recurringExpenseMaterializer =
        materializer ?? new RecurringExpenseMaterializer(repository);

    public Task<IReadOnlyList<HouseholdResponse>> GetHouseholdsAsync(long userId, CancellationToken cancellationToken) =>
        repository.GetHouseholdsAsync(userId, cancellationToken);

    public async Task<HouseholdResponse> CreateHouseholdAsync(
        long userId,
        CreateHouseholdRequest request,
        CancellationToken cancellationToken)
    {
        var household = new Household
        {
            Name = request.Name.Trim(),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            TimeZone = request.TimeZone.Trim()
        };
        var membership = new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = userId,
            Role = HouseholdRole.Owner
        };
        var categories = StandardCategoryCatalog.CreateFor(household.Id);
        await repository.AddHouseholdAsync(household, membership, categories, cancellationToken);
        return new(household.Id, household.Name, household.Currency, household.TimeZone, membership.Role);
    }

    public IReadOnlyList<HouseholdRoleResponse> GetHouseholdRoles() =>
        Enum.GetValues<HouseholdRole>()
            .Select(role => new HouseholdRoleResponse(role, role.ToString()))
            .ToArray();

    public async Task<IReadOnlyList<HouseholdMemberResponse>> GetHouseholdMembersAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        return await repository.GetHouseholdMembersAsync(householdId, cancellationToken);
    }

    public async Task<HouseholdMemberResponse> AddHouseholdMemberAsync(
        Guid householdId,
        long userId,
        AddHouseholdMemberRequest request,
        CancellationToken cancellationToken)
    {
        var requesterRole = await EnsureCanManageMembersAsync(householdId, userId, cancellationToken);
        if (!Enum.IsDefined(request.Role))
        {
            throw new ValidationException("O papel indicado não é válido.");
        }
        if (requesterRole == HouseholdRole.Administrator &&
            request.Role is HouseholdRole.Owner or HouseholdRole.Administrator)
        {
            throw new ForbiddenException("Um administrador só pode adicionar membros com os papéis Member ou Viewer.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await repository.GetActiveUserByEmailAsync(email, cancellationToken)
            ?? throw new NotFoundException("Não existe um utilizador ativo com este email.");
        if (await repository.HasHouseholdMemberAsync(householdId, user.Id, cancellationToken))
        {
            throw new ConflictException("O utilizador já pertence a este agregado.");
        }

        var member = new HouseholdMember
        {
            HouseholdId = householdId,
            UserId = user.Id,
            Role = request.Role
        };
        await repository.AddHouseholdMemberAsync(member, cancellationToken);
        return new(member.Id, user.Id, user.Name, user.Username, user.Email, user.IsActive, member.Role);
    }

    public async Task RemoveHouseholdMemberAsync(
        Guid householdId,
        Guid memberId,
        long userId,
        CancellationToken cancellationToken)
    {
        var requesterRole = await EnsureCanManageMembersAsync(householdId, userId, cancellationToken);
        var member = await repository.GetHouseholdMemberAsync(householdId, memberId, cancellationToken)
            ?? throw new NotFoundException("Membro não encontrado.");
        if (member.UserId == userId)
        {
            throw new ValidationException("Não pode remover a sua própria associação ao agregado.");
        }
        if (requesterRole == HouseholdRole.Administrator &&
            member.Role is HouseholdRole.Owner or HouseholdRole.Administrator)
        {
            throw new ForbiddenException("Um administrador não pode remover owners ou outros administradores.");
        }
        if (member.Role == HouseholdRole.Owner &&
            await repository.CountHouseholdOwnersAsync(householdId, cancellationToken) <= 1)
        {
            throw new ConflictException("Não é possível remover o único owner do agregado.");
        }

        await repository.RemoveHouseholdMemberAsync(member, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        return (await repository.GetCategoriesAsync(householdId, cancellationToken)).Select(Map).ToArray();
    }

    public async Task<CategoryResponse> CreateCategoryAsync(
        Guid householdId,
        long userId,
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        var category = new Category
        {
            HouseholdId = householdId,
            Name = request.Name.Trim(),
            Color = request.Color?.Trim(),
            Icon = request.Icon?.Trim()
        };
        category.Subcategories.Add(new Subcategory
        {
            CategoryId = category.Id,
            Name = StandardCategoryCatalog.NotApplicableName
        });
        await repository.AddCategoryAsync(category, cancellationToken);
        return Map(category);
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(
        Guid householdId,
        Guid categoryId,
        long userId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        var category = await repository.GetCategoryAsync(householdId, categoryId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");
        category.Name = request.Name.Trim();
        category.Color = EmptyToNull(request.Color);
        category.Icon = EmptyToNull(request.Icon);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(category);
    }

    public async Task<SubcategoryResponse> CreateSubcategoryAsync(
        Guid householdId,
        Guid categoryId,
        long userId,
        CreateSubcategoryRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        _ = await repository.GetCategoryAsync(householdId, categoryId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");
        var subcategory = new Subcategory { CategoryId = categoryId, Name = request.Name.Trim() };
        await repository.AddSubcategoryAsync(subcategory, cancellationToken);
        return new(subcategory.Id, subcategory.Name, subcategory.IsActive);
    }

    public async Task<SubcategoryResponse> UpdateSubcategoryAsync(
        Guid householdId,
        Guid categoryId,
        Guid subcategoryId,
        long userId,
        UpdateSubcategoryRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        _ = await repository.GetCategoryAsync(householdId, categoryId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");
        var subcategory = await repository.GetSubcategoryAsync(categoryId, subcategoryId, cancellationToken)
            ?? throw new NotFoundException("Subcategoria não encontrada.");
        subcategory.Name = request.Name.Trim();
        await repository.SaveChangesAsync(cancellationToken);
        return new(subcategory.Id, subcategory.Name, subcategory.IsActive);
    }

    public async Task<BudgetPeriodResponse> CreateBudgetAsync(
        Guid householdId,
        long userId,
        CreateBudgetPeriodRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        var startMonth = FirstOfMonth(request.StartMonth);
        var endMonth = FirstOfMonth(request.EndMonth);
        if (startMonth > endMonth)
        {
            throw new ValidationException("O mês inicial não pode ser posterior ao mês final.");
        }
        if (request.MonthlyAmount <= 0)
        {
            throw new ValidationException("O valor mensal do orçamento deve ser superior a zero.");
        }
        if (await repository.HasOverlappingBudgetAsync(householdId, startMonth, endMonth, cancellationToken))
        {
            throw new ConflictException("O período indicado sobrepõe-se a um orçamento ativo.");
        }

        var requestedAllocations = request.Allocations ?? [];
        if (requestedAllocations.GroupBy(x => x.CategoryId).Any(x => x.Count() > 1))
        {
            throw new ValidationException("Uma categoria não pode aparecer mais do que uma vez no orçamento.");
        }
        if (requestedAllocations.Any(x => x.MonthlyAmount < 0) || requestedAllocations.Sum(x => x.MonthlyAmount) > request.MonthlyAmount)
        {
            throw new ValidationException("As alocações têm de ser positivas e não podem ultrapassar o orçamento mensal.");
        }

        var budget = new BudgetPeriod
        {
            HouseholdId = householdId,
            Name = request.Name.Trim(),
            StartMonth = startMonth,
            EndMonth = endMonth,
            MonthlyAmount = request.MonthlyAmount
        };
        foreach (var allocation in requestedAllocations)
        {
            _ = await repository.GetCategoryAsync(householdId, allocation.CategoryId, cancellationToken)
                ?? throw new ValidationException($"A categoria {allocation.CategoryId} não pertence ao agregado.");
            budget.Allocations.Add(new BudgetCategoryAllocation
            {
                BudgetPeriodId = budget.Id,
                CategoryId = allocation.CategoryId,
                MonthlyAmount = allocation.MonthlyAmount
            });
        }

        var incomes = EnumerateMonths(startMonth, endMonth).Select(month => new Income
        {
            HouseholdId = householdId,
            BudgetPeriodId = budget.Id,
            Date = month,
            Amount = request.MonthlyAmount,
            Description = $"Orçamento mensal — {request.Name.Trim()}",
            Origin = IncomeOrigin.Budget,
            Status = FinancialRecordStatus.Planned
        }).ToArray();
        await repository.AddBudgetAsync(budget, incomes, cancellationToken);

        var categories = await repository.GetCategoriesAsync(householdId, cancellationToken);
        var categoryNames = categories.ToDictionary(x => x.Id, x => x.Name);
        return Map(budget, incomes.Length, categoryNames);
    }

    public async Task<IReadOnlyList<BudgetPeriodResponse>> GetBudgetsAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        var budgets = await repository.GetBudgetsAsync(householdId, cancellationToken);
        return budgets.Select(x => Map(
            x,
            x.GeneratedIncomes.Count,
            x.Allocations.ToDictionary(a => a.CategoryId, a => a.Category.Name))).ToArray();
    }

    public async Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(
        Guid householdId,
        long userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        ValidateRange(from, to);
        return (await repository.GetIncomesAsync(householdId, from, to, cancellationToken))
            .Select(x => new IncomeResponse(x.Id, x.Date, x.Amount, x.Description, x.Origin, x.Status, x.BudgetPeriodId))
            .ToArray();
    }

    public async Task<ExpenseResponse> CreateExpenseAsync(
        Guid householdId,
        long userId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        if (request.Amount <= 0)
        {
            throw new ValidationException("O valor da despesa deve ser superior a zero.");
        }
        if (!Enum.IsDefined(request.Origin))
        {
            throw new ValidationException("A origem da despesa é inválida.");
        }

        var requestedLines = request.Lines is { Count: > 0 }
            ? request.Lines
            : null;
        if (!request.CategoryId.HasValue && request.SubcategoryId.HasValue)
        {
            throw new ValidationException("Não é possível indicar uma subcategoria principal sem categoria principal.");
        }
        if (requestedLines is not null && requestedLines.Sum(x => x.Amount) != request.Amount)
        {
            throw new ValidationException("A soma das parcelas tem de ser igual ao valor total da despesa.");
        }

        var resolvedLines = new List<(CreateExpenseLineRequest Request, Category Category, Subcategory? Subcategory)>();
        if (requestedLines is not null)
        {
            foreach (var line in requestedLines)
            {
                if (line.Amount == 0) throw new ValidationException("O valor de uma parcela não pode ser zero.");
                if (line.Quantity is <= 0) throw new ValidationException("A quantidade de uma parcela deve ser superior a zero.");
                if (line.UnitPrice == 0) throw new ValidationException("O preço unitário de uma parcela não pode ser zero.");
                var lineCategory = await repository.GetCategoryAsync(householdId, line.CategoryId, cancellationToken)
                    ?? throw new ValidationException("A categoria de uma parcela não pertence ao agregado.");
                Subcategory? lineSubcategory = null;
                if (line.SubcategoryId.HasValue)
                {
                    lineSubcategory = await repository.GetSubcategoryAsync(lineCategory.Id, line.SubcategoryId.Value, cancellationToken)
                        ?? throw new ValidationException("A subcategoria de uma parcela não pertence à categoria indicada.");
                }
                resolvedLines.Add((line, lineCategory, lineSubcategory));
            }
        }

        Category category;
        Subcategory? subcategory;
        if (request.CategoryId.HasValue)
        {
            category = await repository.GetCategoryAsync(householdId, request.CategoryId.Value, cancellationToken)
                ?? throw new ValidationException("A categoria não pertence ao agregado.");
            subcategory = request.SubcategoryId.HasValue
                ? await repository.GetSubcategoryAsync(category.Id, request.SubcategoryId.Value, cancellationToken)
                    ?? throw new ValidationException("A subcategoria não pertence à categoria indicada.")
                : null;
        }
        else if (resolvedLines.Count > 0)
        {
            var mainLine = resolvedLines.OrderByDescending(x => x.Request.Amount).First();
            category = mainLine.Category;
            subcategory = mainLine.Subcategory;
        }
        else
        {
            throw new ValidationException("A categoria é obrigatória quando a despesa não tem parcelas explícitas.");
        }

        var expense = new Expense
        {
            HouseholdId = householdId,
            CategoryId = category.Id,
            SubcategoryId = subcategory?.Id,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            MerchantName = EmptyToNull(request.MerchantName),
            MerchantTaxNumber = EmptyToNull(request.MerchantTaxNumber),
            Origin = request.Origin
        };
        if (resolvedLines.Count == 0)
        {
            resolvedLines.Add((
                new CreateExpenseLineRequest(category.Id, subcategory?.Id, expense.Description, 1, expense.Amount, expense.Amount),
                category,
                subcategory));
        }
        for (var index = 0; index < resolvedLines.Count; index++)
        {
            var line = resolvedLines[index];
            expense.Lines.Add(new ExpenseLine
            {
                ExpenseId = expense.Id,
                CategoryId = line.Category.Id,
                SubcategoryId = line.Subcategory?.Id,
                Description = line.Request.Description.Trim(),
                Quantity = line.Request.Quantity,
                UnitPrice = line.Request.UnitPrice,
                Amount = line.Request.Amount,
                Position = index + 1,
                Category = line.Category,
                Subcategory = line.Subcategory
            });
        }
        await repository.AddExpenseAsync(expense, cancellationToken);
        expense.Category = category;
        expense.Subcategory = subcategory;
        return Map(expense);
    }

    public async Task<ExpenseResponse> UpdateExpenseAsync(
        Guid householdId,
        Guid expenseId,
        long userId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        if (request.Amount <= 0) throw new ValidationException("O valor da despesa deve ser superior a zero.");
        if (!request.CategoryId.HasValue && request.SubcategoryId.HasValue)
        {
            throw new ValidationException("Não é possível indicar uma subcategoria principal sem categoria principal.");
        }

        var expense = await repository.GetExpenseAsync(householdId, expenseId, cancellationToken)
            ?? throw new NotFoundException("Despesa não encontrada.");
        var hasAutomaticLine = HasAutomaticLine(expense);
        List<(CreateExpenseLineRequest Request, Category Category, Subcategory? Subcategory)>? resolvedLines = null;
        if (request.Lines is not null)
        {
            if (request.Lines.Count == 0) throw new ValidationException("A despesa tem de ter pelo menos uma parcela.");
            if (request.Lines.Sum(x => x.Amount) != request.Amount)
            {
                throw new ValidationException("A soma das parcelas tem de ser igual ao valor total da despesa.");
            }
            resolvedLines = await ResolveLinesAsync(householdId, request.Lines, cancellationToken);
        }

        Category category;
        Subcategory? subcategory;
        if (request.CategoryId.HasValue)
        {
            category = await repository.GetCategoryAsync(householdId, request.CategoryId.Value, cancellationToken)
                ?? throw new ValidationException("A categoria não pertence ao agregado.");
            subcategory = request.SubcategoryId.HasValue
                ? await repository.GetSubcategoryAsync(category.Id, request.SubcategoryId.Value, cancellationToken)
                    ?? throw new ValidationException("A subcategoria não pertence à categoria indicada.")
                : null;
        }
        else if (resolvedLines is not null)
        {
            var mainLine = resolvedLines.OrderByDescending(x => x.Request.Amount).First();
            category = mainLine.Category;
            subcategory = mainLine.Subcategory;
        }
        else
        {
            throw new ValidationException("A categoria principal é obrigatória quando as parcelas não são enviadas.");
        }

        expense.CategoryId = category.Id;
        expense.SubcategoryId = subcategory?.Id;
        expense.Date = request.Date;
        expense.Amount = request.Amount;
        expense.Description = request.Description.Trim();
        expense.MerchantName = EmptyToNull(request.MerchantName);
        expense.MerchantTaxNumber = EmptyToNull(request.MerchantTaxNumber);
        expense.Category = category;
        expense.Subcategory = subcategory;

        if (resolvedLines is not null)
        {
            ReplaceLines(expense, resolvedLines);
        }
        else if (expense.Lines.Count == 0 || hasAutomaticLine)
        {
            ReplaceLines(expense,
            [
                (new CreateExpenseLineRequest(category.Id, subcategory?.Id, expense.Description, 1, expense.Amount, expense.Amount),
                    category, subcategory)
            ]);
        }
        else if (expense.Lines.Sum(x => x.Amount) != expense.Amount)
        {
            throw new ValidationException("Para alterar o total de uma despesa com parcelas próprias, envie também a nova lista de parcelas.");
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Map(expense);
    }

    public async Task<ExpenseResponse> ReplaceExpenseLinesAsync(
        Guid householdId,
        Guid expenseId,
        long userId,
        ReplaceExpenseLinesRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        if (request.Lines.Count == 0) throw new ValidationException("A despesa tem de ter pelo menos uma parcela.");

        var expense = await repository.GetExpenseAsync(householdId, expenseId, cancellationToken)
            ?? throw new NotFoundException("Despesa não encontrada.");
        if (request.Lines.Sum(x => x.Amount) != expense.Amount)
        {
            throw new ValidationException("A soma das parcelas tem de ser igual ao valor total da despesa.");
        }

        var resolvedLines = await ResolveLinesAsync(householdId, request.Lines, cancellationToken);
        ReplaceLines(expense, resolvedLines);
        var mainLine = resolvedLines.OrderByDescending(x => x.Request.Amount).First();
        expense.CategoryId = mainLine.Category.Id;
        expense.SubcategoryId = mainLine.Subcategory?.Id;
        expense.Category = mainLine.Category;
        expense.Subcategory = mainLine.Subcategory;
        await repository.SaveChangesAsync(cancellationToken);
        return Map(expense);
    }

    public async Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(
        Guid householdId,
        long userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        ValidateRange(from, to);
        return (await repository.GetExpensesAsync(householdId, from, to, cancellationToken)).Select(Map).ToArray();
    }

    public async Task<BudgetDashboardResponse> GetDashboardAsync(
        Guid householdId,
        long userId,
        DateOnly month,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        month = FirstOfMonth(month);
        var lastDay = month.AddMonths(1).AddDays(-1);
        var budget = await repository.GetBudgetForMonthAsync(householdId, month, cancellationToken);
        var expenses = await repository.GetExpensesAsync(householdId, month, lastDay, cancellationToken);
        var incomes = await repository.GetIncomesAsync(householdId, month, lastDay, cancellationToken);
        var confirmedExpenses = expenses.Where(x => x.Status == FinancialRecordStatus.Confirmed).Sum(x => x.Amount);

        var categorizedAmounts = expenses.Where(x => x.Status == FinancialRecordStatus.Confirmed)
            .SelectMany(expense => expense.Lines.Count > 0
                ? expense.Lines.Select(line => (line.CategoryId, line.Category.Name, line.Amount))
                : [(expense.CategoryId, expense.Category.Name, expense.Amount)]);
        var expenseGroups = categorizedAmounts
            .GroupBy(x => new { x.CategoryId, x.Name })
            .ToDictionary(x => x.Key.CategoryId, x => (x.Key.Name, Amount: x.Sum(e => e.Amount)));
        var allocationMap = budget?.Allocations.ToDictionary(x => x.CategoryId) ?? [];
        var categoryIds = expenseGroups.Keys.Union(allocationMap.Keys).ToArray();
        var categoryStatuses = categoryIds.Select(id =>
        {
            var spent = expenseGroups.GetValueOrDefault(id).Amount;
            var allocation = allocationMap.GetValueOrDefault(id);
            var name = allocation?.Category.Name ?? expenseGroups[id].Name;
            return new CategoryBudgetStatusResponse(id, name, allocation?.MonthlyAmount, spent, allocation is null ? null : allocation.MonthlyAmount - spent);
        }).OrderBy(x => x.CategoryName).ToArray();

        var monthlyBudget = budget?.MonthlyAmount ?? 0;
        return new(
            month,
            monthlyBudget,
            incomes.Where(x => x.Status == FinancialRecordStatus.Planned).Sum(x => x.Amount),
            confirmedExpenses,
            monthlyBudget - confirmedExpenses,
            categoryStatuses);
    }

    public async Task<RecurringExpenseResponse> CreateRecurringExpenseAsync(
        Guid householdId,
        long userId,
        CreateRecurringExpenseRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        if (request.Amount <= 0) throw new ValidationException("O valor da despesa recorrente deve ser superior a zero.");
        if (request.EndDate.HasValue && request.EndDate < request.StartDate)
        {
            throw new ValidationException("A data final não pode ser anterior à data inicial.");
        }
        _ = await repository.GetCategoryAsync(householdId, request.CategoryId, cancellationToken)
            ?? throw new ValidationException("A categoria não pertence ao agregado.");
        if (request.SubcategoryId.HasValue &&
            await repository.GetSubcategoryAsync(request.CategoryId, request.SubcategoryId.Value, cancellationToken) is null)
        {
            throw new ValidationException("A subcategoria não pertence à categoria indicada.");
        }

        var recurring = new RecurringExpense
        {
            HouseholdId = householdId,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            Description = request.Description.Trim(),
            MerchantName = EmptyToNull(request.MerchantName),
            MerchantTaxNumber = EmptyToNull(request.MerchantTaxNumber),
            Amount = request.Amount,
            Frequency = request.Frequency,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NextOccurrenceDate = request.StartDate
        };
        await repository.AddRecurringExpenseAsync(recurring, cancellationToken);
        if (request.MaterializeNow)
        {
            await recurringExpenseMaterializer.MaterializeAsync(recurring, TodayInLisbon(), cancellationToken);
        }
        return Map(recurring);
    }

    public async Task<IReadOnlyList<RecurringExpenseResponse>> GetRecurringExpensesAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        return (await repository.GetRecurringExpensesAsync(householdId, cancellationToken)).Select(Map).ToArray();
    }

    public async Task<RecurringExpenseMaterializationResponse> MaterializeRecurringExpenseAsync(
        Guid householdId,
        Guid recurringExpenseId,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        var recurring = await repository.GetRecurringExpenseAsync(householdId, recurringExpenseId, cancellationToken)
            ?? throw new NotFoundException("Despesa recorrente não encontrada.");
        var through = TodayInLisbon();
        var createdCount = await recurringExpenseMaterializer.MaterializeAsync(recurring, through, cancellationToken);
        return new(createdCount, through, recurring.NextOccurrenceDate, recurring.IsActive);
    }

    public async Task<RecurringExpenseResponse> UpdateRecurringExpenseAsync(
        Guid householdId,
        Guid recurringExpenseId,
        long userId,
        UpdateRecurringExpenseRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        if (request.Amount <= 0) throw new ValidationException("O valor da despesa recorrente deve ser superior a zero.");
        if (request.EndDate.HasValue && request.EndDate < request.StartDate)
        {
            throw new ValidationException("A data final não pode ser anterior à data inicial.");
        }
        if (!Enum.IsDefined(request.Frequency)) throw new ValidationException("Periodicidade recorrente inválida.");

        _ = await repository.GetCategoryAsync(householdId, request.CategoryId, cancellationToken)
            ?? throw new ValidationException("A categoria não pertence ao agregado.");
        if (request.SubcategoryId.HasValue &&
            await repository.GetSubcategoryAsync(request.CategoryId, request.SubcategoryId.Value, cancellationToken) is null)
        {
            throw new ValidationException("A subcategoria não pertence à categoria indicada.");
        }

        var recurring = await repository.GetRecurringExpenseAsync(householdId, recurringExpenseId, cancellationToken)
            ?? throw new NotFoundException("Despesa recorrente não encontrada.");
        var previousNextOccurrence = recurring.NextOccurrenceDate;

        recurring.CategoryId = request.CategoryId;
        recurring.SubcategoryId = request.SubcategoryId;
        recurring.Description = request.Description.Trim();
        recurring.MerchantName = EmptyToNull(request.MerchantName);
        recurring.MerchantTaxNumber = EmptyToNull(request.MerchantTaxNumber);
        recurring.Amount = request.Amount;
        recurring.Frequency = request.Frequency;
        recurring.StartDate = request.StartDate;
        recurring.EndDate = request.EndDate;

        var nextOccurrence = request.StartDate;
        while (nextOccurrence < previousNextOccurrence)
        {
            nextOccurrence = RecurringExpenseMaterializer.Next(nextOccurrence, request.Frequency);
        }
        recurring.NextOccurrenceDate = nextOccurrence;
        recurring.IsActive = !request.EndDate.HasValue || nextOccurrence <= request.EndDate;

        await repository.SaveChangesAsync(cancellationToken);
        return Map(recurring);
    }

    private async Task EnsureMemberAsync(Guid householdId, long userId, bool write, CancellationToken cancellationToken)
    {
        var role = await repository.GetMemberRoleAsync(householdId, userId, cancellationToken)
            ?? throw new ForbiddenException("O utilizador não pertence ao agregado indicado.");
        if (write && role == HouseholdRole.Viewer)
        {
            throw new ForbiddenException("O utilizador não tem permissões de escrita neste agregado.");
        }
    }

    private async Task<HouseholdRole> EnsureCanManageMembersAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken)
    {
        var role = await repository.GetMemberRoleAsync(householdId, userId, cancellationToken)
            ?? throw new ForbiddenException("O utilizador não pertence ao agregado indicado.");
        if (role is not HouseholdRole.Owner and not HouseholdRole.Administrator)
        {
            throw new ForbiddenException("O utilizador não tem permissões para gerir membros deste agregado.");
        }
        return role;
    }

    private static CategoryResponse Map(Category category) => new(
        category.Id,
        category.Name,
        category.Color,
        category.Icon,
        category.IsActive,
        category.Subcategories.Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new SubcategoryResponse(x.Id, x.Name, x.IsActive)).ToArray());

    private static ExpenseResponse Map(Expense expense) => new(
        expense.Id, expense.Date, expense.Amount, expense.Description,
        expense.CategoryId, expense.Category.Name, expense.SubcategoryId, expense.Subcategory?.Name,
        expense.MerchantName, expense.MerchantTaxNumber, expense.Origin, expense.Status,
        expense.Lines.OrderBy(x => x.Position).Select(x => new ExpenseLineResponse(
            x.Id, x.Description, x.Quantity, x.UnitPrice, x.Amount,
            x.CategoryId, x.Category.Name, x.SubcategoryId, x.Subcategory?.Name, x.Position)).ToArray());

    private static RecurringExpenseResponse Map(RecurringExpense expense) => new(
        expense.Id, expense.CategoryId, expense.SubcategoryId, expense.Description,
        expense.MerchantName, expense.MerchantTaxNumber, expense.Amount, expense.Frequency,
        expense.StartDate, expense.EndDate, expense.NextOccurrenceDate, expense.IsActive);

    private static DateOnly TodayInLisbon()
    {
        var lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, lisbon).DateTime);
    }

    private async Task<List<(CreateExpenseLineRequest Request, Category Category, Subcategory? Subcategory)>> ResolveLinesAsync(
        Guid householdId,
        IReadOnlyList<CreateExpenseLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var resolved = new List<(CreateExpenseLineRequest, Category, Subcategory?)>(lines.Count);
        foreach (var line in lines)
        {
            if (line.Amount == 0) throw new ValidationException("O valor de uma parcela não pode ser zero.");
            if (line.Quantity is <= 0) throw new ValidationException("A quantidade de uma parcela deve ser superior a zero.");
            if (line.UnitPrice == 0) throw new ValidationException("O preço unitário de uma parcela não pode ser zero.");
            var category = await repository.GetCategoryAsync(householdId, line.CategoryId, cancellationToken)
                ?? throw new ValidationException("A categoria de uma parcela não pertence ao agregado.");
            Subcategory? subcategory = null;
            if (line.SubcategoryId.HasValue)
            {
                subcategory = await repository.GetSubcategoryAsync(category.Id, line.SubcategoryId.Value, cancellationToken)
                    ?? throw new ValidationException("A subcategoria de uma parcela não pertence à categoria indicada.");
            }
            resolved.Add((line, category, subcategory));
        }
        return resolved;
    }

    private static bool HasAutomaticLine(Expense expense)
    {
        if (expense.Lines.Count != 1) return false;
        var line = expense.Lines.Single();
        return line.Description == expense.Description &&
               line.Amount == expense.Amount &&
               line.CategoryId == expense.CategoryId &&
               line.SubcategoryId == expense.SubcategoryId &&
               line.Quantity == 1 &&
               line.UnitPrice == expense.Amount;
    }

    private static void ReplaceLines(
        Expense expense,
        IReadOnlyList<(CreateExpenseLineRequest Request, Category Category, Subcategory? Subcategory)> lines)
    {
        var existing = expense.Lines.OrderBy(x => x.Position).ToArray();
        for (var index = 0; index < lines.Count; index++)
        {
            var resolved = lines[index];
            var line = index < existing.Length
                ? existing[index]
                : new ExpenseLine { Id = Guid.Empty, ExpenseId = expense.Id };
            line.CategoryId = resolved.Category.Id;
            line.SubcategoryId = resolved.Subcategory?.Id;
            line.Description = resolved.Request.Description.Trim();
            line.Quantity = resolved.Request.Quantity;
            line.UnitPrice = resolved.Request.UnitPrice;
            line.Amount = resolved.Request.Amount;
            line.Position = index + 1;
            line.Category = resolved.Category;
            line.Subcategory = resolved.Subcategory;
            if (index >= existing.Length) expense.Lines.Add(line);
        }
        for (var index = lines.Count; index < existing.Length; index++)
        {
            expense.Lines.Remove(existing[index]);
        }
    }

    private static BudgetPeriodResponse Map(BudgetPeriod budget, int incomeCount, IReadOnlyDictionary<Guid, string> categoryNames) => new(
        budget.Id, budget.Name, budget.StartMonth, budget.EndMonth, budget.MonthlyAmount, budget.Status, incomeCount,
        budget.Allocations.Select(x => new BudgetAllocationResponse(x.CategoryId, categoryNames[x.CategoryId], x.MonthlyAmount)).ToArray());

    private static DateOnly FirstOfMonth(DateOnly value) => new(value.Year, value.Month, 1);

    private static IEnumerable<DateOnly> EnumerateMonths(DateOnly start, DateOnly end)
    {
        for (var month = start; month <= end; month = month.AddMonths(1))
        {
            yield return month;
        }
    }

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (from > to) throw new ValidationException("A data inicial não pode ser posterior à data final.");
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
