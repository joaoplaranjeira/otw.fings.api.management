using System.Text.Json;

namespace otw.fings.api.management.Services.Notifications;

public sealed class AlwaysRuleEvaluator : INotificationRuleEvaluator
{
    public string ConditionType => "Always";
    public IReadOnlySet<string> SupportedEventTypes => NotificationEventTypes.All;
    public Task<RuleEvaluationResult> EvaluateAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken) =>
        Task.FromResult(RuleEvaluationResult.Match());
    public void Validate(NotificationRule rule)
    {
        using var document = JsonDocument.Parse(rule.ConditionJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ValidationException("A condição Always deve ser um objeto JSON.");
    }
}

public sealed class BudgetThresholdCrossedRuleEvaluator : INotificationRuleEvaluator
{
    private sealed record Condition(decimal ThresholdPercentage, string Direction = "up", Guid? CategoryId = null);
    public string ConditionType => "BudgetThresholdCrossed";
    public IReadOnlySet<string> SupportedEventTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "Budget.UsageChanged" };

    public Task<RuleEvaluationResult> EvaluateAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken)
    {
        var condition = Read(rule);
        var data = notificationEvent.Data.RootElement;
        var previous = data.GetProperty("previousPercentage").GetDecimal();
        var current = data.GetProperty("currentPercentage").GetDecimal();
        var eventCategory = data.TryGetProperty("categoryId", out var categoryElement) && categoryElement.ValueKind != JsonValueKind.Null
            ? categoryElement.GetGuid() : (Guid?)null;
        var crossed = eventCategory == condition.CategoryId && (condition.Direction == "down"
            ? previous > condition.ThresholdPercentage && current <= condition.ThresholdPercentage
            : previous < condition.ThresholdPercentage && current >= condition.ThresholdPercentage);
        if (!crossed) return Task.FromResult(RuleEvaluationResult.NoMatch);
        var budgetId = data.GetProperty("budgetId").GetGuid();
        var period = data.GetProperty("period").GetString();
        var category = eventCategory.HasValue ? $":category:{eventCategory.Value}" : string.Empty;
        return Task.FromResult(RuleEvaluationResult.Match($"budget:{budgetId}:{period}{category}:threshold:{condition.ThresholdPercentage}"));
    }

    public void Validate(NotificationRule rule) => _ = Read(rule);

    private static Condition Read(NotificationRule rule)
    {
        var condition = JsonSerializer.Deserialize<Condition>(rule.ConditionJson, JsonOptions())
            ?? throw new ValidationException("A condição de limite do orçamento é obrigatória.");
        if (condition.ThresholdPercentage is < 0 or > 100) throw new ValidationException("A percentagem deve estar entre 0 e 100.");
        if (condition.Direction is not ("up" or "down")) throw new ValidationException("A direção deve ser 'up' ou 'down'.");
        return condition;
    }

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };
}

public sealed class ExpenseAmountAboveRuleEvaluator : INotificationRuleEvaluator
{
    private sealed record Condition(decimal Amount);
    public string ConditionType => "ExpenseAmountAbove";
    public IReadOnlySet<string> SupportedEventTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "Expense.Created", "Expense.Updated" };
    public Task<RuleEvaluationResult> EvaluateAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken)
    {
        var condition = Read(rule);
        var amount = notificationEvent.Data.RootElement.GetProperty("amount").GetDecimal();
        return Task.FromResult(amount > condition.Amount ? RuleEvaluationResult.Match($"amount-above:{condition.Amount}") : RuleEvaluationResult.NoMatch);
    }
    public void Validate(NotificationRule rule) => _ = Read(rule);
    private static Condition Read(NotificationRule rule)
    {
        var value = JsonSerializer.Deserialize<Condition>(rule.ConditionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ValidationException("A condição de valor é obrigatória.");
        if (value.Amount < 0) throw new ValidationException("O valor limite não pode ser negativo.");
        return value;
    }
}

public sealed class CategoryExpenseThresholdRuleEvaluator : INotificationRuleEvaluator
{
    private sealed record Condition(Guid CategoryId, decimal Amount);
    public string ConditionType => "CategoryExpenseThreshold";
    public IReadOnlySet<string> SupportedEventTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "Expense.Created", "Expense.Updated" };
    public Task<RuleEvaluationResult> EvaluateAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken)
    {
        var condition = Read(rule);
        var data = notificationEvent.Data.RootElement;
        var matches = data.GetProperty("categoryId").GetGuid() == condition.CategoryId && data.GetProperty("amount").GetDecimal() > condition.Amount;
        return Task.FromResult(matches ? RuleEvaluationResult.Match($"category:{condition.CategoryId}:amount-above:{condition.Amount}") : RuleEvaluationResult.NoMatch);
    }
    public void Validate(NotificationRule rule) => _ = Read(rule);
    private static Condition Read(NotificationRule rule)
    {
        var value = JsonSerializer.Deserialize<Condition>(rule.ConditionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ValidationException("A condição de categoria é obrigatória.");
        if (value.CategoryId == Guid.Empty || value.Amount < 0) throw new ValidationException("A categoria e um valor não negativo são obrigatórios.");
        return value;
    }
}

public sealed class MonthlyExpenseThresholdRuleEvaluator : INotificationRuleEvaluator
{
    private sealed record Condition(decimal Amount, Guid? CategoryId = null);
    public string ConditionType => "MonthlyExpenseThreshold";
    public IReadOnlySet<string> SupportedEventTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "Budget.UsageChanged" };
    public Task<RuleEvaluationResult> EvaluateAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken)
    {
        var condition = Read(rule);
        var data = notificationEvent.Data.RootElement;
        var eventCategory = data.TryGetProperty("categoryId", out var category) && category.ValueKind != JsonValueKind.Null ? category.GetGuid() : (Guid?)null;
        var previous = data.GetProperty("previousSpentAmount").GetDecimal();
        var current = data.GetProperty("currentSpentAmount").GetDecimal();
        var matches = eventCategory == condition.CategoryId && previous < condition.Amount && current >= condition.Amount;
        var period = data.GetProperty("period").GetString();
        var suffix = $"monthly:{period}:category:{condition.CategoryId?.ToString() ?? "all"}:amount:{condition.Amount}";
        return Task.FromResult(matches ? RuleEvaluationResult.Match(suffix) : RuleEvaluationResult.NoMatch);
    }
    public void Validate(NotificationRule rule) => _ = Read(rule);
    private static Condition Read(NotificationRule rule)
    {
        var value = JsonSerializer.Deserialize<Condition>(rule.ConditionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ValidationException("A condição mensal é obrigatória.");
        if (value.Amount < 0) throw new ValidationException("O valor mensal não pode ser negativo.");
        return value;
    }
}
