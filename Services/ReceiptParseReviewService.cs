using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Services;

public sealed class ReceiptParseReviewService(IFinanceRepository repository) : IReceiptParseReviewService
{
    public async Task<ReceiptParseHistoryItemResponse> ValidateAsync(
        Guid householdId,
        Guid parseId,
        long userId,
        ValidateReceiptParseRequest request,
        CancellationToken cancellationToken)
    {
        var role = await repository.GetMemberRoleAsync(householdId, userId, cancellationToken)
            ?? throw new ForbiddenException("O utilizador não pertence ao agregado indicado.");
        if (role == HouseholdRole.Viewer)
        {
            throw new ForbiddenException("O perfil de consulta não pode validar parses.");
        }

        var record = await repository.GetReceiptParseRecordAsync(householdId, parseId, cancellationToken)
            ?? throw new NotFoundException("Parse não encontrado.");
        record.ValidationStatus = request.IsValid
            ? ReceiptParseValidationStatus.Valid
            : ReceiptParseValidationStatus.Invalid;
        record.ValidatedByUserId = userId;
        record.ValidatedAtUtc = DateTimeOffset.UtcNow;
        record.ValidationNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await repository.SaveChangesAsync(cancellationToken);
        return Map(record);
    }

    public async Task<ReceiptParseQualityHistoryResponse> GetHistoryAsync(
        Guid householdId,
        long userId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await repository.IsMemberAsync(householdId, userId, cancellationToken))
        {
            throw new ForbiddenException("O utilizador não pertence ao agregado indicado.");
        }

        var records = await repository.GetReceiptParseRecordsAsync(
            householdId,
            Math.Clamp(limit, 1, 200),
            cancellationToken);
        var counts = await repository.GetReceiptParseStatusCountsAsync(householdId, cancellationToken);
        var pending = counts.GetValueOrDefault(ReceiptParseValidationStatus.Pending);
        var valid = counts.GetValueOrDefault(ReceiptParseValidationStatus.Valid);
        var invalid = counts.GetValueOrDefault(ReceiptParseValidationStatus.Invalid);
        var reviewed = valid + invalid;
        decimal? validRate = reviewed == 0 ? null : Math.Round(valid * 100m / reviewed, 2);
        return new(pending + reviewed, pending, valid, invalid, validRate, records.Select(Map).ToArray());
    }

    private static ReceiptParseHistoryItemResponse Map(ReceiptParseRecord record) => new(
        record.Id,
        record.CreatedAtUtc,
        record.Model,
        record.LineCount,
        record.CategorizedLineCount,
        record.AverageConfidence,
        record.ValidationStatus,
        record.ValidatedAtUtc,
        record.ValidationNotes);
}
