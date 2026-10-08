using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using otw.fings.api.management.Infrastructure.Data;

namespace otw.fings.api.management.Services.Notifications;

public sealed record RenderedNotification(string Title, string Body, string? ActionUrl);

public interface INotificationTemplateRenderer
{
    Task<RenderedNotification> RenderAsync(NotificationTemplate template, NotificationEventEnvelope notificationEvent, long userId, CancellationToken cancellationToken);
}

public sealed partial class NotificationTemplateRenderer(FingsDbContext dbContext) : INotificationTemplateRenderer
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTokens =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["Expense.Created"] = ExpenseTokens(),
            ["Expense.Updated"] = ExpenseTokens(),
            ["Expense.Deleted"] = ExpenseTokens(),
            ["RecurringExpense.Materialized"] = ExpenseTokens(),
            ["Budget.UsageChanged"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "householdName", "actorName", "budgetId", "period", "budgetAmount", "previousSpentAmount",
                "currentSpentAmount", "previousPercentage", "currentPercentage"
            }
        };

    public async Task<RenderedNotification> RenderAsync(
        NotificationTemplate template,
        NotificationEventEnvelope notificationEvent,
        long userId,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in notificationEvent.Data.RootElement.EnumerateObject())
        {
            values[property.Name] = Format(property.Value);
        }
        if (notificationEvent.HouseholdId.HasValue)
        {
            values["householdName"] = await dbContext.Households.AsNoTracking()
                .Where(x => x.Id == notificationEvent.HouseholdId.Value).Select(x => x.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        }
        if (notificationEvent.ActorUserId.HasValue)
        {
            values["actorName"] = await dbContext.Users.AsNoTracking()
                .Where(x => x.Id == notificationEvent.ActorUserId.Value).Select(x => x.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        }

        var allowed = AllowedTokens.TryGetValue(notificationEvent.EventType, out var tokens) ? tokens : new HashSet<string>();
        string Render(string value) => TokenRegex().Replace(value, match =>
        {
            var token = match.Groups[1].Value;
            if (!allowed.Contains(token)) throw new ValidationException($"O token '{token}' não é permitido para {notificationEvent.EventType}.");
            return values.GetValueOrDefault(token, string.Empty);
        });

        return new RenderedNotification(Render(template.TitleTemplate), Render(template.BodyTemplate),
            template.ActionUrlTemplate is null ? null : Render(template.ActionUrlTemplate));
    }

    public static void ValidateTokens(NotificationTemplate template, string eventType)
    {
        var allowed = AllowedTokens.TryGetValue(eventType, out var tokens) ? tokens : new HashSet<string>();
        foreach (var value in new[] { template.TitleTemplate, template.BodyTemplate, template.ActionUrlTemplate })
        {
            if (value is null) continue;
            foreach (Match match in TokenRegex().Matches(value))
            {
                if (!allowed.Contains(match.Groups[1].Value)) throw new ValidationException($"O template contém um token não permitido: {match.Groups[1].Value}.");
            }
        }
    }

    private static HashSet<string> ExpenseTokens() => new(StringComparer.OrdinalIgnoreCase)
    {
        "householdName", "actorName", "expenseId", "amount", "previousAmount", "currency", "description",
        "merchantName", "categoryId", "categoryName", "date", "origin", "recurringExpenseId"
    };

    private static string Format(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => string.Empty,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number.ToString("0.##", CultureInfo.GetCultureInfo("pt-PT")),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText()
    };

    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9]*)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
