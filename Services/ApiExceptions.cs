namespace otw.fings.api.management.Services;

public sealed class NotFoundException(string message) : Exception(message);
public sealed class ValidationException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);
public sealed class GoneException(string message) : Exception(message);
public sealed class ForbiddenException(string message) : Exception(message);
public sealed class IntegrationUnavailableException(string message) : Exception(message);
public sealed class ReceiptImageQualityException(ReceiptImageQualityResponse quality) : Exception(
    "A qualidade da fotografia pode ser insuficiente para ler o talão com precisão. Submeta outra fotografia ou repita o pedido aceitando explicitamente o risco.")
{
    public ReceiptImageQualityResponse Quality { get; } = quality;
}
