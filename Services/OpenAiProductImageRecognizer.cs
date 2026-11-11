using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using otw.fings.api.management.Services.Interfaces;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Services;

public sealed class OpenAiProductImageRecognizer(
    IOptions<OpenAiSettings> settings,
    ILogger<OpenAiProductImageRecognizer> logger) : IProductImageRecognizer
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    private const string Instructions = """
        Analisa a fotografia de um produto destinado a uma lista de compras.
        Identifica apenas o produto principal visível e usa texto legível da embalagem quando exista.
        productName deve ser curto, claro e adequado para uma lista de compras em português de Portugal.
        Mantém brand e variant separados do nome genérico. Se não forem visíveis ou seguros, devolve null.
        suggestedQuantity e suggestedUnit representam a quantidade da embalagem apenas quando estiver explicitamente visível.
        confidence mede a certeza de que o produto foi corretamente identificado, entre 0 e 1.
        Não inventes marcas, variantes, quantidades ou unidades.
        """;

    private const string Schema = """
        {
          "type": "object",
          "properties": {
            "productName": { "type": "string" },
            "brand": { "type": ["string", "null"] },
            "variant": { "type": ["string", "null"] },
            "suggestedQuantity": { "type": ["number", "null"] },
            "suggestedUnit": { "type": ["string", "null"] },
            "confidence": { "type": "number" }
          },
          "required": ["productName", "brand", "variant", "suggestedQuantity", "suggestedUnit", "confidence"],
          "additionalProperties": false
        }
        """;

    public async Task<ProductRecognitionResponse> RecognizeAsync(
        Stream image,
        string contentType,
        long length,
        CancellationToken cancellationToken)
    {
        var configuration = settings.Value;
        var model = string.IsNullOrWhiteSpace(configuration.ProductRecognitionModel)
            ? configuration.Model
            : configuration.ProductRecognitionModel;
        if (string.IsNullOrWhiteSpace(configuration.ApiKey) || string.IsNullOrWhiteSpace(model))
        {
            throw new IntegrationUnavailableException("A integração OpenAI não está configurada.");
        }
        if (length <= 0 || length > configuration.MaximumProductImageBytes)
        {
            throw new ValidationException($"A imagem deve ter entre 1 byte e {configuration.MaximumProductImageBytes} bytes.");
        }
        if (!AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("Formato inválido. São aceites imagens JPEG, PNG e WebP.");
        }

        await using var memory = new MemoryStream((int)length);
        await image.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        ValidateSignature(bytes, contentType);

        ChatClient client = new(model, configuration.ApiKey);
        SystemChatMessage instructions = new(Instructions);
        UserChatMessage message = new([
            ChatMessageContentPart.CreateTextPart("Identifica o produto principal desta fotografia."),
            ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(bytes), contentType, ChatImageDetailLevel.High)
        ]);
        ChatCompletionOptions options = new()
        {
            Temperature = 0,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "shopping_product_recognition",
                BinaryData.FromString(Schema),
                jsonSchemaIsStrict: true)
        };

        try
        {
            ChatCompletion completion = await client.CompleteChatAsync([instructions, message], options, cancellationToken);
            var recognition = JsonSerializer.Deserialize<ProductRecognitionResponse>(
                completion.Content[0].Text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new IntegrationUnavailableException("A OpenAI devolveu uma identificação vazia.");
            if (string.IsNullOrWhiteSpace(recognition.ProductName))
            {
                throw new IntegrationUnavailableException("Não foi possível identificar um produto na fotografia.");
            }
            return recognition with { Confidence = Math.Clamp(recognition.Confidence, 0, 1) };
        }
        catch (IntegrationUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "OpenAI product recognition failed.");
            throw new IntegrationUnavailableException("Não foi possível identificar o produto.");
        }
    }

    private static void ValidateSignature(byte[] bytes, string contentType)
    {
        var valid = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => bytes.Length >= 12 &&
                            bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid) throw new ValidationException("O conteúdo do ficheiro não corresponde ao formato indicado.");
    }
}
