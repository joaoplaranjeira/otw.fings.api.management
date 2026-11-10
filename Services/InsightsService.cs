using otw.fings.api.management.Domain.Catalogs;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Services;

public sealed class InsightsService(
    IFinanceRepository repository,
    TimeProvider? timeProvider = null) : IInsightsService
{
    private const int HistoryWindowMonths = 3;
    private const decimal SmallExpenseLimit = 10m;
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<HouseholdInsightsResponse> GetInsightsAsync(
        Guid householdId,
        long userId,
        DateOnly month,
        CancellationToken cancellationToken)
    {
        _ = await repository.GetMemberRoleAsync(householdId, userId, cancellationToken)
            ?? throw new ForbiddenException("O utilizador não pertence ao agregado indicado.");
        var household = await repository.GetHouseholdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Agregado não encontrado.");

        month = new DateOnly(month.Year, month.Month, 1);
        var monthEnd = month.AddMonths(1).AddDays(-1);
        var today = TodayInHousehold(household.TimeZone);
        var analysisEnd = month.Year == today.Year && month.Month == today.Month
            ? today
            : monthEnd;
        if (month > new DateOnly(today.Year, today.Month, 1))
        {
            analysisEnd = month.AddDays(-1);
        }

        var historyStart = month.AddMonths(-HistoryWindowMonths);
        var allExpenses = analysisEnd >= historyStart
            ? await repository.GetExpensesAsync(householdId, historyStart, analysisEnd, cancellationToken)
            : [];
        var confirmed = allExpenses
            .Where(x => x.Status == FinancialRecordStatus.Confirmed)
            .ToArray();
        var current = confirmed.Where(x => x.Date >= month && x.Date <= analysisEnd).ToArray();
        var historical = confirmed.Where(x => x.Date < month).ToArray();
        var currentSlices = Flatten(current);
        var historicalSlices = Flatten(historical);
        var budgets = await repository.GetBudgetsAsync(householdId, cancellationToken);
        var currentBudget = FindBudget(budgets, month);
        var recurring = await repository.GetRecurringExpensesAsync(householdId, cancellationToken);
        var historyMonths = historical
            .Select(x => new { x.Date.Year, x.Date.Month })
            .Distinct()
            .Count();

        var insights = new List<InsightResponse>();
        AddBudgetPace(insights, current, currentBudget, month, monthEnd, analysisEnd, household.Currency);
        AddSpendingTrend(insights, current, historical, month, monthEnd, analysisEnd, historyMonths, household.Currency);
        AddSpendingDriver(insights, currentSlices, historicalSlices, month, analysisEnd, historyMonths, household.Currency);
        AddUnusualExpense(insights, current, historical, month, analysisEnd, household.Currency);
        AddSmallExpenses(insights, current, month, analysisEnd, household.Currency);
        AddMerchantConcentration(insights, current, month, analysisEnd, household.Currency);
        AddRecurringCommitments(insights, recurring, currentBudget, month, monthEnd, household.Currency);
        AddPotentialSubscriptions(insights, confirmed, month, analysisEnd, household.Currency);
        AddBudgetAccuracy(insights, confirmed, budgets, month, analysisEnd, household.Currency);
        AddDataQuality(insights, current, currentSlices, month, analysisEnd);

        return new HouseholdInsightsResponse(
            month,
            clock.GetUtcNow(),
            household.Currency,
            Math.Min(historyMonths, HistoryWindowMonths),
            insights);
    }

    private static void AddBudgetPace(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<Expense> current,
        BudgetPeriod? budget,
        DateOnly month,
        DateOnly monthEnd,
        DateOnly analysisEnd,
        string currency)
    {
        if (budget is null || budget.MonthlyAmount <= 0 || analysisEnd < month) return;

        var spent = current.Sum(x => x.Amount);
        var elapsedDays = analysisEnd.Day;
        var totalDays = monthEnd.Day;
        var budgetUsed = Percentage(spent, budget.MonthlyAmount);
        var timeElapsed = Percentage(elapsedDays, totalDays);
        var projected = elapsedDays == 0 ? 0 : spent / elapsedDays * totalDays;
        var difference = projected - budget.MonthlyAmount;
        var severity = projected > budget.MonthlyAmount
            ? (projected > budget.MonthlyAmount * 1.15m ? InsightSeverity.Critical : InsightSeverity.Warning)
            : InsightSeverity.Positive;
        var direction = difference > 0
            ? $"A este ritmo, a projeção ultrapassa o orçamento em {Money(difference, currency)}."
            : $"A este ritmo, a projeção fica {Money(Math.Abs(difference), currency)} abaixo do orçamento.";

        insights.Add(new(
            "budget-pace",
            InsightType.BudgetPace,
            severity,
            $"{budgetUsed:0.#}% do orçamento utilizado",
            $"Decorreram {timeElapsed:0.#}% dos dias do mês. {direction}",
            elapsedDays >= 14 ? 0.9m : elapsedDays >= 7 ? 0.7m : 0.5m,
            current.Count,
            [
                Metric("spent", "Gasto confirmado", spent, currency),
                Metric("budget", "Orçamento", budget.MonthlyAmount, currency),
                Metric("projected", "Projeção", projected, currency),
                Metric("elapsedDays", "Dias decorridos", elapsedDays, "days")
            ],
            Filter(month, analysisEnd)));
    }

    private static void AddSpendingTrend(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<Expense> current,
        IReadOnlyCollection<Expense> historical,
        DateOnly month,
        DateOnly monthEnd,
        DateOnly analysisEnd,
        int historyMonths,
        string currency)
    {
        if (analysisEnd < month || historyMonths < 2) return;
        var elapsedDay = analysisEnd.Day;
        var priorTotals = Enumerable.Range(1, HistoryWindowMonths)
            .Select(offset => month.AddMonths(-offset))
            .Select(start => historical
                .Where(x => x.Date >= start && x.Date <= ComparableEnd(start, elapsedDay))
                .Sum(x => x.Amount))
            .ToArray();
        var nonEmpty = priorTotals.Where(x => x > 0).ToArray();
        if (nonEmpty.Length < 2) return;

        var currentTotal = current.Sum(x => x.Amount);
        var average = nonEmpty.Average();
        var change = average == 0 ? 0 : (currentTotal - average) / average * 100m;
        var severity = change >= 20 ? InsightSeverity.Warning
            : change <= -10 ? InsightSeverity.Positive
            : InsightSeverity.Information;
        var comparison = analysisEnd == monthEnd ? "meses completos" : $"primeiros {elapsedDay} dias";

        insights.Add(new(
            "spending-trend",
            InsightType.SpendingTrend,
            severity,
            change >= 0 ? $"Despesa aumentou {change:0.#}%" : $"Despesa diminuiu {Math.Abs(change):0.#}%",
            $"Comparação com a média dos {nonEmpty.Length} {comparison} anteriores.",
            nonEmpty.Length == 3 ? 0.9m : 0.75m,
            historical.Count,
            [
                Metric("current", "Período atual", currentTotal, currency),
                Metric("historicalAverage", "Média histórica", average, currency),
                Metric("change", "Variação", change, "percent")
            ],
            Filter(month, analysisEnd)));
    }

    private static void AddSpendingDriver(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<ExpenseSlice> current,
        IReadOnlyCollection<ExpenseSlice> historical,
        DateOnly month,
        DateOnly analysisEnd,
        int historyMonths,
        string currency)
    {
        if (analysisEnd < month || historyMonths < 2 || current.Count == 0) return;
        var availableMonths = historical.Select(x => new { x.Date.Year, x.Date.Month }).Distinct().Count();
        if (availableMonths == 0) return;

        var historicalByCategory = historical
            .GroupBy(x => x.CategoryId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount) / availableMonths);
        var driver = current.GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(x => new
            {
                x.Key.CategoryId,
                x.Key.CategoryName,
                Current = x.Sum(y => y.Amount),
                Historical = historicalByCategory.GetValueOrDefault(x.Key.CategoryId)
            })
            .Select(x => new { x.CategoryId, x.CategoryName, x.Current, x.Historical, Delta = x.Current - x.Historical })
            .Where(x => x.Delta > 0)
            .OrderByDescending(x => x.Delta)
            .FirstOrDefault();
        if (driver is null) return;

        var subcategory = current
            .Where(x => x.CategoryId == driver.CategoryId)
            .GroupBy(x => new { x.SubcategoryId, Name = x.SubcategoryName ?? x.CategoryName })
            .Select(x => new { x.Key.SubcategoryId, x.Key.Name, Amount = x.Sum(y => y.Amount) })
            .OrderByDescending(x => x.Amount)
            .First();
        var change = driver.Historical > 0 ? driver.Delta / driver.Historical * 100m : 100m;

        insights.Add(new(
            "spending-driver",
            InsightType.SpendingDriver,
            change >= 25 ? InsightSeverity.Warning : InsightSeverity.Information,
            $"{driver.CategoryName} explica o maior aumento",
            $"A categoria está {Money(driver.Delta, currency)} acima da média mensal recente. {subcategory.Name} representa {Money(subcategory.Amount, currency)} no período atual.",
            availableMonths == 3 ? 0.9m : 0.75m,
            current.Count(x => x.CategoryId == driver.CategoryId),
            [
                Metric("current", "Gasto atual", driver.Current, currency),
                Metric("historicalAverage", "Média histórica", driver.Historical, currency),
                Metric("increase", "Aumento", driver.Delta, currency),
                Metric("topSubcategory", subcategory.Name, subcategory.Amount, currency)
            ],
            Filter(month, analysisEnd, driver.CategoryId, subcategory.SubcategoryId)));
    }

    private static void AddUnusualExpense(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<Expense> current,
        IReadOnlyCollection<Expense> historical,
        DateOnly month,
        DateOnly analysisEnd,
        string currency)
    {
        var candidates = current.Where(x => !string.IsNullOrWhiteSpace(x.MerchantName))
            .Select(expense =>
            {
                var baseline = historical
                    .Where(x => SameMerchant(x.MerchantName, expense.MerchantName))
                    .Select(x => x.Amount)
                    .OrderBy(x => x)
                    .ToArray();
                var median = Median(baseline);
                var deviations = baseline.Select(x => Math.Abs(x - median)).OrderBy(x => x).ToArray();
                var mad = Median(deviations);
                var ratio = median > 0 ? expense.Amount / median : 0;
                var robustScore = mad > 0 ? Math.Abs(expense.Amount - median) / mad : ratio;
                return new { Expense = expense, Baseline = baseline, Median = median, Ratio = ratio, RobustScore = robustScore };
            })
            .Where(x => x.Baseline.Length >= 3 && x.Expense.Amount >= x.Median * 1.4m && x.RobustScore >= 2m)
            .OrderByDescending(x => x.Ratio)
            .FirstOrDefault();
        if (candidates is null) return;

        var increase = (candidates.Ratio - 1m) * 100m;
        insights.Add(new(
            $"unusual-expense-{candidates.Expense.Id}",
            InsightType.UnusualExpense,
            InsightSeverity.Warning,
            $"Compra acima do habitual em {candidates.Expense.MerchantName}",
            $"O valor foi {increase:0.#}% superior à mediana das {candidates.Baseline.Length} compras anteriores neste comerciante.",
            Math.Min(0.95m, 0.65m + candidates.Baseline.Length * 0.05m),
            candidates.Baseline.Length,
            [
                Metric("amount", "Valor da compra", candidates.Expense.Amount, currency),
                Metric("merchantMedian", "Mediana habitual", candidates.Median, currency),
                Metric("increase", "Diferença", increase, "percent")
            ],
            Filter(month, analysisEnd, merchantName: candidates.Expense.MerchantName)));
    }

    private static void AddSmallExpenses(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<Expense> current,
        DateOnly month,
        DateOnly analysisEnd,
        string currency)
    {
        var small = current.Where(x => x.Amount > 0 && x.Amount < SmallExpenseLimit).ToArray();
        if (small.Length < 3) return;
        var total = small.Sum(x => x.Amount);
        var totalSpent = current.Sum(x => x.Amount);
        insights.Add(new(
            "small-expenses",
            InsightType.SmallExpenses,
            Percentage(total, totalSpent) >= 15 ? InsightSeverity.Warning : InsightSeverity.Information,
            $"{small.Length} pequenas despesas somam {Money(total, currency)}",
            $"Foram contabilizados movimentos inferiores a {Money(SmallExpenseLimit, currency)}, equivalentes a {Percentage(total, totalSpent):0.#}% da despesa do período.",
            0.95m,
            small.Length,
            [
                Metric("count", "Número de despesas", small.Length, "count"),
                Metric("total", "Total acumulado", total, currency),
                Metric("share", "Peso na despesa", Percentage(total, totalSpent), "percent")
            ],
            Filter(month, analysisEnd, maximumAmount: SmallExpenseLimit)));
    }

    private static void AddMerchantConcentration(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<Expense> current,
        DateOnly month,
        DateOnly analysisEnd,
        string currency)
    {
        var total = current.Sum(x => x.Amount);
        if (total <= 0) return;
        var merchants = current.Where(x => !string.IsNullOrWhiteSpace(x.MerchantName))
            .GroupBy(x => x.MerchantName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => new { Name = x.Key, Amount = x.Sum(y => y.Amount), Count = x.Count() })
            .OrderByDescending(x => x.Amount)
            .Take(3)
            .ToArray();
        if (merchants.Length == 0) return;
        var concentration = Percentage(merchants.Sum(x => x.Amount), total);
        var names = string.Join(", ", merchants.Select(x => x.Name));
        insights.Add(new(
            "merchant-concentration",
            InsightType.MerchantConcentration,
            concentration >= 60 ? InsightSeverity.Warning : InsightSeverity.Information,
            $"Os principais comerciantes concentram {concentration:0.#}% da despesa",
            $"{names} são os comerciantes com maior valor acumulado no período.",
            0.95m,
            merchants.Sum(x => x.Count),
            [
                Metric("topMerchantsTotal", "Total nos principais comerciantes", merchants.Sum(x => x.Amount), currency),
                Metric("share", "Concentração", concentration, "percent"),
                Metric("merchantCount", "Comerciantes considerados", merchants.Length, "count")
            ],
            Filter(month, analysisEnd)));
    }

    private static void AddRecurringCommitments(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<RecurringExpense> recurring,
        BudgetPeriod? budget,
        DateOnly month,
        DateOnly monthEnd,
        string currency)
    {
        var applicable = recurring.Where(x => x.StartDate <= monthEnd && (!x.EndDate.HasValue || x.EndDate >= month)).ToArray();
        if (applicable.Length == 0) return;
        var monthly = applicable.Sum(MonthlyEquivalent);
        var share = budget is null ? 0 : Percentage(monthly, budget.MonthlyAmount);
        insights.Add(new(
            "recurring-commitments",
            InsightType.RecurringCommitments,
            share >= 70 ? InsightSeverity.Warning : InsightSeverity.Information,
            $"Compromissos recorrentes de {Money(monthly, currency)} por mês",
            budget is null
                ? "Estimativa mensal equivalente das regras recorrentes ativas no período."
                : $"Representam aproximadamente {share:0.#}% do orçamento mensal.",
            0.95m,
            applicable.Length,
            [
                Metric("monthlyEquivalent", "Equivalente mensal", monthly, currency),
                Metric("budgetShare", "Peso no orçamento", share, "percent"),
                Metric("ruleCount", "Regras recorrentes", applicable.Length, "count")
            ],
            new(month, monthEnd, Origin: ExpenseOrigin.Recurring)));
    }

    private static void AddPotentialSubscriptions(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<Expense> expenses,
        DateOnly month,
        DateOnly analysisEnd,
        string currency)
    {
        var candidates = expenses
            .Where(x => x.Origin != ExpenseOrigin.Recurring && !string.IsNullOrWhiteSpace(x.MerchantName) && x.Amount > 0)
            .GroupBy(x => x.MerchantName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderBy(x => x.Date).ToArray();
                var intervals = ordered.Zip(ordered.Skip(1), (a, b) => b.Date.DayNumber - a.Date.DayNumber).ToArray();
                var average = ordered.Average(x => x.Amount);
                var variation = average == 0 ? 1 : ordered.Max(x => Math.Abs(x.Amount - average)) / average;
                return new
                {
                    Merchant = group.Key,
                    Items = ordered,
                    Average = average,
                    MedianInterval = Median(intervals.Select(x => (decimal)x).OrderBy(x => x).ToArray()),
                    Variation = variation,
                    DistinctMonths = ordered.Select(x => new { x.Date.Year, x.Date.Month }).Distinct().Count()
                };
            })
            .Where(x => x.Items.Length >= 3 && x.DistinctMonths >= 3 && x.MedianInterval is >= 25 and <= 35 && x.Variation <= 0.15m)
            .OrderByDescending(x => x.Average)
            .ToArray();
        if (candidates.Length == 0) return;
        var total = candidates.Sum(x => x.Average);
        insights.Add(new(
            "potential-subscriptions",
            InsightType.PotentialSubscriptions,
            InsightSeverity.Information,
            $"{candidates.Length} possíveis pagamentos mensais detetados",
            $"Os pagamentos a {string.Join(", ", candidates.Take(3).Select(x => x.Merchant))} repetem-se aproximadamente todos os meses com valores semelhantes. Confirma antes de os tratar como subscrições.",
            candidates.All(x => x.Items.Length >= 4) ? 0.85m : 0.7m,
            candidates.Sum(x => x.Items.Length),
            [
                Metric("candidateCount", "Possíveis subscrições", candidates.Length, "count"),
                Metric("monthlyEstimate", "Estimativa mensal", total, currency)
            ],
            Filter(month.AddMonths(-HistoryWindowMonths), analysisEnd)));
    }

    private static void AddBudgetAccuracy(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<Expense> confirmed,
        IReadOnlyCollection<BudgetPeriod> budgets,
        DateOnly month,
        DateOnly analysisEnd,
        string currency)
    {
        var evaluated = Enumerable.Range(0, 4)
            .Select(offset => month.AddMonths(-offset))
            .Select(period => new { Month = period, Budget = FindBudget(budgets, period) })
            .Where(x => x.Budget is not null)
            .ToArray();
        var currentBudget = FindBudget(budgets, month);
        if (evaluated.Length >= 3)
        {
            var overspending = evaluated
                .SelectMany(item => item.Budget!.Allocations.Select(allocation =>
                {
                    var end = item.Month == month ? analysisEnd : item.Month.AddMonths(1).AddDays(-1);
                    var spent = Flatten(confirmed.Where(x => x.Date >= item.Month && x.Date <= end))
                        .Where(x => x.CategoryId == allocation.CategoryId)
                        .Sum(x => x.Amount);
                    return new { allocation.CategoryId, allocation.Category.Name, Overspent = spent > allocation.MonthlyAmount };
                }))
                .GroupBy(x => new { x.CategoryId, x.Name })
                .Select(x => new { x.Key.CategoryId, x.Key.Name, Count = x.Count(y => y.Overspent), Evaluated = x.Count() })
                .Where(x => x.Evaluated >= 3 && x.Count >= 3)
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();
            if (overspending is not null)
            {
                insights.Add(new(
                    "budget-accuracy",
                    InsightType.BudgetAccuracy,
                    InsightSeverity.Warning,
                    $"{overspending.Name} excede frequentemente o orçamento",
                    $"A categoria ultrapassou a respetiva alocação em {overspending.Count} dos últimos {overspending.Evaluated} meses avaliados.",
                    0.9m,
                    overspending.Evaluated,
                    [
                        Metric("monthsExceeded", "Meses acima do orçamento", overspending.Count, "count"),
                        Metric("monthsEvaluated", "Meses avaliados", overspending.Evaluated, "count")
                    ],
                    Filter(month, analysisEnd, overspending.CategoryId)));
                return;
            }
        }

        if (currentBudget is null) return;
        var unallocated = currentBudget.MonthlyAmount - currentBudget.Allocations.Sum(x => x.MonthlyAmount);
        if (unallocated <= 0) return;
        insights.Add(new(
            "budget-accuracy",
            InsightType.BudgetAccuracy,
            InsightSeverity.Information,
            $"{Money(unallocated, currency)} do orçamento não estão distribuídos",
            "Atribuir este valor a categorias torna os desvios e alertas por categoria mais representativos.",
            1m,
            currentBudget.Allocations.Count,
            [
                Metric("unallocated", "Valor não distribuído", unallocated, currency),
                Metric("unallocatedShare", "Percentagem não distribuída", Percentage(unallocated, currentBudget.MonthlyAmount), "percent")
            ]));
    }

    private static void AddDataQuality(
        ICollection<InsightResponse> insights,
        IReadOnlyCollection<Expense> current,
        IReadOnlyCollection<ExpenseSlice> slices,
        DateOnly month,
        DateOnly analysisEnd)
    {
        if (current.Count == 0) return;
        var uncategorized = slices.Where(x =>
                string.Equals(x.CategoryName, StandardCategoryCatalog.NotApplicableName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.SubcategoryName, StandardCategoryCatalog.NotApplicableName, StringComparison.OrdinalIgnoreCase))
            .Sum(x => Math.Abs(x.Amount));
        var total = slices.Sum(x => Math.Abs(x.Amount));
        var uncategorizedShare = Percentage(uncategorized, total);
        var missingMerchant = current.Count(x => string.IsNullOrWhiteSpace(x.MerchantName));
        var missingMerchantShare = Percentage(missingMerchant, current.Count);
        var severity = uncategorizedShare >= 15 || missingMerchantShare >= 25
            ? InsightSeverity.Warning
            : InsightSeverity.Positive;
        var title = severity == InsightSeverity.Positive
            ? "Boa cobertura dos dados deste mês"
            : "Há dados que podem ser melhorados";

        insights.Add(new(
            "data-quality",
            InsightType.DataQuality,
            severity,
            title,
            $"{uncategorizedShare:0.#}% do valor está em categorias não aplicáveis e {missingMerchant} de {current.Count} despesas não têm comerciante.",
            1m,
            current.Count,
            [
                Metric("uncategorizedShare", "Valor sem classificação útil", uncategorizedShare, "percent"),
                Metric("missingMerchantCount", "Despesas sem comerciante", missingMerchant, "count"),
                Metric("missingMerchantShare", "Despesas sem comerciante", missingMerchantShare, "percent")
            ],
            Filter(month, analysisEnd)));
    }

    private DateOnly TodayInHousehold(string timeZoneId)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
        }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    }

    private static IReadOnlyList<ExpenseSlice> Flatten(IEnumerable<Expense> expenses) => expenses
        .SelectMany(expense => expense.Lines.Count > 0
            ? expense.Lines.Select(line => new ExpenseSlice(
                expense.Date,
                line.CategoryId,
                line.Category.Name,
                line.SubcategoryId,
                line.Subcategory?.Name,
                line.Amount))
            : [new ExpenseSlice(
                expense.Date,
                expense.CategoryId,
                expense.Category.Name,
                expense.SubcategoryId,
                expense.Subcategory?.Name,
                expense.Amount)])
        .ToArray();

    private static BudgetPeriod? FindBudget(IEnumerable<BudgetPeriod> budgets, DateOnly month) => budgets
        .Where(x => x.Status != BudgetStatus.Cancelled && x.StartMonth <= month && x.EndMonth >= month)
        .OrderByDescending(x => x.Status == BudgetStatus.Active)
        .FirstOrDefault();

    private static decimal MonthlyEquivalent(RecurringExpense expense) => expense.Frequency switch
    {
        RecurrenceFrequency.Weekly => expense.Amount * 52m / 12m,
        RecurrenceFrequency.Monthly => expense.Amount,
        RecurrenceFrequency.Quarterly => expense.Amount / 3m,
        RecurrenceFrequency.Annual => expense.Amount / 12m,
        _ => 0m
    };

    private static DateOnly ComparableEnd(DateOnly month, int day) =>
        month.AddMonths(1).AddDays(-1).Day < day
            ? month.AddMonths(1).AddDays(-1)
            : new DateOnly(month.Year, month.Month, day);

    private static bool SameMerchant(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static decimal Median(IReadOnlyList<decimal> ordered) => ordered.Count switch
    {
        0 => 0,
        _ when ordered.Count % 2 == 1 => ordered[ordered.Count / 2],
        _ => (ordered[ordered.Count / 2 - 1] + ordered[ordered.Count / 2]) / 2m
    };

    private static decimal Percentage(decimal value, decimal total) => total == 0 ? 0 : value / total * 100m;
    private static string Money(decimal value, string currency) => $"{value:0.##} {currency}";
    private static InsightMetricResponse Metric(string key, string label, decimal value, string unit) => new(key, label, Math.Round(value, 2), unit);
    private static InsightExpenseFilterResponse Filter(
        DateOnly from,
        DateOnly to,
        Guid? categoryId = null,
        Guid? subcategoryId = null,
        string? merchantName = null,
        decimal? maximumAmount = null,
        ExpenseOrigin? origin = null) =>
        new(from, to, categoryId, subcategoryId, merchantName, maximumAmount, origin);

    private sealed record ExpenseSlice(
        DateOnly Date,
        Guid CategoryId,
        string CategoryName,
        Guid? SubcategoryId,
        string? SubcategoryName,
        decimal Amount);
}
