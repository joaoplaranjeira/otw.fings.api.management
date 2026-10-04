using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Services.Interfaces;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Services;

public sealed class OpenAiReceiptParser(
    IFinanceRepository repository,
    IOptions<OpenAiSettings> settings,
    ILogger<OpenAiReceiptParser> logger) : IReceiptParser
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    internal const string ExtractionInstructions = """
        És um especialista em leitura de talões de compras em Portugal.
        Extrai apenas a informação factual visível na imagem, sem classificar os artigos e sem inventar texto ilegível.
        Cada linha deve corresponder a uma parcela do talão e preservar a descrição impressa tão fielmente quanto possível.
        Usa EUR quando a moeda não estiver indicada mas o talão for português. As datas devem usar YYYY-MM-DD.

        # Leitura das parcelas e dos valores
        - Identifica primeiro o cabeçalho da tabela e respeita as colunas impressas em cada linha.
        - Em talões portugueses, marcadores como I1, I2, I3 ou semelhantes no início da linha são normalmente
          códigos/taxas de IVA. Nunca os interpretes como quantidade, preço unitário ou valor da parcela.
        - O amount é o valor monetário impresso na coluna de valor da mesma linha, normalmente o número mais à direita.
          Lê esse valor diretamente da imagem; não o deduzas a partir de outros números.
        - Números de lote, referências, dosagens, percentagens, códigos fiscais e texto de carimbos sobrepostos
          nunca são valores nem quantidades de artigos.
        - Define quantity e unitPrice como null quando não estiverem explicitamente impressos na linha do artigo.
          Não assumas que um artigo tem quantidade 1 apenas por existir uma linha.
        - Mantém descontos explicitamente impressos como parcelas negativas.

        # Verificação obrigatória
        - Antes de responder, soma os amount de todas as parcelas, incluindo descontos, e compara com o total pago.
        - Se a soma não coincidir, relê a coluna dos valores linha a linha. Não alteres um valor claramente impresso
          apenas para forçar a igualdade; acrescenta antes um warning que identifique a linha incerta.
        - O resumo de IVA pode confirmar o total, mas as bases e os valores de IVA não são parcelas de compra.
        """;

    internal const string ValueVerificationInstructions = """
        Faz uma segunda leitura independente dos valores monetários de um talão português.
        A extração anterior é apenas uma hipótese: confirma cada número diretamente na imagem e corrige-o quando necessário.

        # Regras obrigatórias
        - Mantém exatamente os mesmos lineIndex; não juntes, dividas, acrescentes nem removas parcelas.
        - Para cada parcela, lê amount diretamente da coluna monetária impressa mais à direita na mesma linha.
        - amountText deve transcrever literalmente o valor visível, incluindo vírgula decimal e sinal negativo.
        - Nunca calcules amount a partir de quantity ou unitPrice e nunca alteres um valor para forçar a soma.
        - Códigos de IVA como I1/I2/I3, referências, dosagens, percentagens e números de lote não são valores.
        - Descontos impressos com sinal negativo devem continuar negativos.
        - quantity e unitPrice ficam null quando não estiverem explicitamente impressos.
        - Confiança significa certeza de leitura visual do número, não certeza aritmética.
        - Confirma também o total pago diretamente na zona de totais e transcreve-o em totalText.
        """;

    internal const string ClassificationInstructions = """
        És um especialista em classificação de despesas pessoais e familiares em Portugal.
        Receberás os dados já extraídos de um talão e uma lista fechada de opções. Para cada lineIndex devolve exatamente
        uma classificação. Escolhe apenas a key de uma opção fornecida ou null quando não houver correspondência segura.
        Quando não houver correspondência segura, prefere null; a aplicação atribuirá "Não aplicável".

        # Método
        - Identifica primeiro o setor do comerciante usando merchantName e o contexto conjunto de todas as linhas.
        - Interpreta depois cada descrição no contexto desse comerciante; descrições de talões são frequentemente abreviadas.
        - Classifica pela natureza real do produto ou serviço, nunca por semelhança superficial entre palavras.
        - Cada key já representa um par categoria/subcategoria válido. Não combines opções por conta própria.
        - Usa a opção só de categoria apenas quando nenhuma subcategoria fornecida for adequada.
        - A confidence deve refletir a certeza semântica e estar entre 0 e 1.

        # Farmácia e parafarmácia
        - Em farmácias, parafarmácias e lojas de saúde como a Wells, medicamentos, suplementos, primeiros socorros,
          cuidados de bebé, higiene oral terapêutica e produtos dermatológicos pertencem a
          "Saúde e bem-estar" / "Farmácia", quando essa opção existir.
        - Cosmética, perfumaria ou higiene pessoal comum sem finalidade terapêutica clara pode pertencer a
          "Compras pessoais" / "Higiene e cosmética".

        # Restrições importantes
        - "Educação" exige uma ligação inequívoca a ensino, escola, formação, livros ou material escolar.
        - "Eletrónica" exige um dispositivo ou acessório inequivocamente identificado, como cabo, carregador,
          auscultadores ou equipamento eletrónico.
        - Códigos de produto, siglas, marcas, números e indicações como 100ML ou 5ML nunca são evidência de
          educação ou eletrónica.

        # Exemplos de referência
        - Wells/farmácia + "AERO OM BBT 100ML" => Saúde e bem-estar / Farmácia.
        - Wells/farmácia + "ISDIN BEXIDENT", "BIODIA", "BEPANTHEN" => Saúde e bem-estar / Farmácia.
        - "Empregada de limpeza" ou limpeza doméstica => Habitação / Limpeza.
        - "Cabo USB-C" ou "Carregador" => Compras pessoais / Eletrónica.
        """;

    private const string ExtractionSchema = """
    {
      "type": "object",
      "properties": {
        "merchantName": { "type": ["string", "null"] },
        "merchantTaxNumber": { "type": ["string", "null"] },
        "documentNumber": { "type": ["string", "null"] },
        "purchaseDate": { "type": ["string", "null"] },
        "currency": { "type": "string" },
        "subtotal": { "type": ["number", "null"] },
        "tax": { "type": ["number", "null"] },
        "total": { "type": "number" },
        "lines": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "description": { "type": "string" },
              "quantity": { "type": ["number", "null"] },
              "unitPrice": { "type": ["number", "null"] },
              "amount": { "type": "number" }
            },
            "required": ["description", "quantity", "unitPrice", "amount"],
            "additionalProperties": false
          }
        },
        "warnings": { "type": "array", "items": { "type": "string" } }
      },
      "required": ["merchantName", "merchantTaxNumber", "documentNumber", "purchaseDate", "currency", "subtotal", "tax", "total", "lines", "warnings"],
      "additionalProperties": false
    }
    """;

    private const string ClassificationSchema = """
    {
      "type": "object",
      "properties": {
        "classifications": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "lineIndex": { "type": "integer" },
              "classificationKey": { "type": ["string", "null"] },
              "confidence": { "type": "number" }
            },
            "required": ["lineIndex", "classificationKey", "confidence"],
            "additionalProperties": false
          }
        }
      },
      "required": ["classifications"],
      "additionalProperties": false
    }
    """;

    private const string ValueVerificationSchema = """
    {
      "type": "object",
      "properties": {
        "totalText": { "type": "string" },
        "total": { "type": "number" },
        "totalConfidence": { "type": "number" },
        "lines": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "lineIndex": { "type": "integer" },
              "quantity": { "type": ["number", "null"] },
              "quantityConfidence": { "type": "number" },
              "unitPriceText": { "type": ["string", "null"] },
              "unitPrice": { "type": ["number", "null"] },
              "unitPriceConfidence": { "type": "number" },
              "amountText": { "type": "string" },
              "amount": { "type": "number" },
              "amountConfidence": { "type": "number" }
            },
            "required": ["lineIndex", "quantity", "quantityConfidence", "unitPriceText", "unitPrice", "unitPriceConfidence", "amountText", "amount", "amountConfidence"],
            "additionalProperties": false
          }
        }
      },
      "required": ["totalText", "total", "totalConfidence", "lines"],
      "additionalProperties": false
    }
    """;

    public async Task<ReceiptParseResponse> ParseAsync(
        Guid householdId,
        long userId,
        Stream image,
        string contentType,
        long length,
        bool acceptLowQuality,
        CancellationToken cancellationToken)
    {
        if (!await repository.IsMemberAsync(householdId, userId, cancellationToken))
        {
            throw new ForbiddenException("O utilizador não pertence ao agregado indicado.");
        }

        var configuration = settings.Value;
        if (string.IsNullOrWhiteSpace(configuration.ApiKey) || string.IsNullOrWhiteSpace(configuration.Model))
        {
            throw new IntegrationUnavailableException("A integração OpenAI não está configurada.");
        }
        if (length <= 0 || length > configuration.MaximumReceiptBytes)
        {
            throw new ValidationException($"A imagem deve ter entre 1 byte e {configuration.MaximumReceiptBytes} bytes.");
        }
        if (!AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("Formato inválido. São aceites imagens JPEG, PNG e WebP.");
        }

        await using var memory = new MemoryStream((int)length);
        await image.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        ValidateSignature(bytes, contentType);
        var preparedImages = ReceiptImagePreprocessor.Prepare(bytes);
        var preparedContentType = preparedImages.WasCropped ? "image/jpeg" : contentType;
        logger.LogInformation(
            "Receipt image preparation: cropped={WasCropped}, bounds={Bounds}, segments={SegmentCount}, quality={QualityScore}.",
            preparedImages.WasCropped,
            preparedImages.Bounds,
            preparedImages.Images.Count,
            preparedImages.Quality.Score);
        var imageQuality = MapImageQuality(
            preparedImages.Quality,
            configuration.MinimumReceiptImageQuality,
            acceptLowQuality);
        if (preparedImages.Quality.Score < configuration.MinimumReceiptImageQuality && !acceptLowQuality)
        {
            throw new ReceiptImageQualityException(imageQuality);
        }

        var categories = await repository.GetCategoriesAsync(householdId, cancellationToken);
        var classificationOptions = CreateClassificationOptions(categories);
        var extractionModel = string.IsNullOrWhiteSpace(configuration.ExtractionModel)
            ? configuration.Model
            : configuration.ExtractionModel;
        ChatClient extractionClient = new(extractionModel, configuration.ApiKey);
        ChatClient classificationClient = new(configuration.Model, configuration.ApiKey);

        var parsed = await ExtractAsync(extractionClient, preparedImages.Images, preparedContentType, cancellationToken);
        if (configuration.VerifyReceiptValues && parsed.Lines.Count > 0)
        {
            var verifiedValues = await VerifyValuesAsync(
                extractionClient,
                preparedImages.Images,
                preparedContentType,
                parsed,
                cancellationToken);
            if (verifiedValues is not null)
            {
                parsed = ApplyVerifiedValues(parsed, verifiedValues);
            }
        }
        IReadOnlyList<ParsedClassification> classifications = parsed.Lines.Count == 0
            ? []
            : await ClassifyAsync(classificationClient, parsed, classificationOptions, cancellationToken);
        var warnings = parsed.Warnings.ToList();
        if (imageQuality.AcceptedWithRisk)
        {
            warnings.Add("O parse foi efetuado apesar de a qualidade da fotografia poder ser insuficiente.");
        }
        var classificationsByLine = classifications
            .Where(x => x.LineIndex >= 0 && x.LineIndex < parsed.Lines.Count)
            .GroupBy(x => x.LineIndex)
            .ToDictionary(x => x.Key, x => x.First());
        var optionsByKey = classificationOptions.ToDictionary(x => x.Key, StringComparer.Ordinal);
        var notApplicableOption = ResolveNotApplicableClassification(null, classificationOptions);

        var lines = parsed.Lines.Select((line, index) =>
        {
            if (!classificationsByLine.TryGetValue(index, out var classification))
            {
                warnings.Add($"Não foi devolvida classificação para '{line.Description}'.");
                return MapLine(line, notApplicableOption, 0);
            }

            if (classification.ClassificationKey is null)
            {
                return MapLine(line, notApplicableOption, classification.Confidence);
            }

            if (!optionsByKey.TryGetValue(classification.ClassificationKey, out var classificationOption))
            {
                warnings.Add($"A classificação devolvida para '{line.Description}' não é válida.");
                return MapLine(line, notApplicableOption, 0);
            }

            var guardedOption = ApplyClassificationGuardrails(
                parsed.MerchantName,
                line.Description,
                classificationOption,
                classificationOptions);
            var completedOption = ResolveNotApplicableClassification(guardedOption, classificationOptions);
            return MapLine(line, completedOption, classification.Confidence);
        }).ToArray();

        var lineTotal = lines.Sum(x => x.Amount);
        if (Math.Abs(lineTotal - parsed.Total) > 0.02m)
        {
            warnings.Add("A soma das parcelas não coincide com o total do talão.");
        }

        var parseRecord = new ReceiptParseRecord
        {
            HouseholdId = householdId,
            RequestedByUserId = userId,
            Model = extractionModel,
            LineCount = lines.Length,
            CategorizedLineCount = lines.Count(x =>
                x.SuggestedCategoryId.HasValue && Normalize(x.SuggestedCategoryName) != "NAO APLICAVEL"),
            AverageConfidence = lines.Length == 0 ? 0 : Math.Round(lines.Average(x => x.Confidence), 4)
        };
        await repository.AddReceiptParseRecordAsync(parseRecord, cancellationToken);

        return new(
            parseRecord.Id,
            parsed.MerchantName,
            parsed.MerchantTaxNumber,
            parsed.DocumentNumber,
            DateOnly.TryParse(parsed.PurchaseDate, out var date) ? date : null,
            string.IsNullOrWhiteSpace(parsed.Currency) ? "EUR" : parsed.Currency.ToUpperInvariant(),
            parsed.Subtotal,
            parsed.Tax,
            parsed.Total,
            lineTotal,
            lines,
            warnings.Distinct().ToArray(),
            imageQuality);
    }

    private static ReceiptImageQualityResponse MapImageQuality(
        ReceiptImagePreprocessor.ImageQualityAssessment quality,
        int minimumQuality,
        bool acceptedWithRisk) => new(
            quality.Score,
            quality.Score >= minimumQuality ? "Muito boa" : quality.Score >= 60 ? "Aceitável" : "Insuficiente",
            quality.Score < minimumQuality && acceptedWithRisk,
            quality.Warnings);

    private async Task<ParsedReceipt> ExtractAsync(
        ChatClient client,
        IReadOnlyList<byte[]> images,
        string contentType,
        CancellationToken cancellationToken)
    {
        SystemChatMessage instructions = new(ExtractionInstructions);
        var content = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart(images.Count == 1
                ? "Extrai os dados deste talão."
                : "As imagens são recortes sobrepostos do mesmo talão, ordenados de cima para baixo. Extrai cada parcela uma única vez e usa as zonas repetidas apenas para confirmar a leitura.")
        };
        content.AddRange(images.Select(image =>
            ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(image), contentType, ChatImageDetailLevel.High)));
        UserChatMessage message = new(content);
        ChatCompletionOptions options = new()
        {
            Temperature = 0,
#pragma warning disable OPENAI001 // Seed is intentionally pinned to make repeated OCR attempts reproducible.
            Seed = 240930,
#pragma warning restore OPENAI001
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "receipt_extraction",
                BinaryData.FromString(ExtractionSchema),
                jsonSchemaIsStrict: true)
        };

        try
        {
            ChatCompletion completion = await client.CompleteChatAsync([instructions, message], options, cancellationToken);
            return JsonSerializer.Deserialize<ParsedReceipt>(
                completion.Content[0].Text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new IntegrationUnavailableException("A OpenAI devolveu uma extração vazia.");
        }
        catch (IntegrationUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "OpenAI receipt extraction failed.");
            throw new IntegrationUnavailableException("Não foi possível extrair o talão.");
        }
    }

    private async Task<VerifiedReceiptValues?> VerifyValuesAsync(
        ChatClient client,
        IReadOnlyList<byte[]> images,
        string contentType,
        ParsedReceipt receipt,
        CancellationToken cancellationToken)
    {
        var previousExtraction = new
        {
            total = receipt.Total,
            lines = receipt.Lines.Select((line, index) => new
            {
                lineIndex = index,
                line.Description,
                line.Quantity,
                line.UnitPrice,
                line.Amount
            })
        };
        SystemChatMessage instructions = new(ValueVerificationInstructions);
        var content = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart(
                "Revê os valores desta extração contra os recortes do talão, ordenados de cima para baixo:\n" +
                JsonSerializer.Serialize(previousExtraction))
        };
        content.AddRange(images.Select(image =>
            ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(image), contentType, ChatImageDetailLevel.High)));
        UserChatMessage message = new(content);
        ChatCompletionOptions options = new()
        {
            Temperature = 0,
#pragma warning disable OPENAI001
            Seed = 240931,
#pragma warning restore OPENAI001
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "receipt_value_verification",
                BinaryData.FromString(CreateValueVerificationSchema(receipt.Lines.Count)),
                jsonSchemaIsStrict: true)
        };

        try
        {
            ChatCompletion completion = await client.CompleteChatAsync([instructions, message], options, cancellationToken);
            return JsonSerializer.Deserialize<VerifiedReceiptValues>(
                completion.Content[0].Text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OpenAI receipt value verification failed; keeping the first extraction.");
            return null;
        }
    }

    internal static ParsedReceipt ApplyVerifiedValues(ParsedReceipt receipt, VerifiedReceiptValues verification)
    {
        if (verification.Total <= 0 || verification.Lines.Count != receipt.Lines.Count)
        {
            return receipt;
        }
        var linesByIndex = verification.Lines
            .Where(x => x.LineIndex >= 0 && x.LineIndex < receipt.Lines.Count)
            .GroupBy(x => x.LineIndex)
            .ToDictionary(x => x.Key, x => x.ToArray());
        if (linesByIndex.Count != receipt.Lines.Count || linesByIndex.Values.Any(x => x.Length != 1))
        {
            return receipt;
        }

        var candidateLines = receipt.Lines.Select((line, index) =>
        {
            var verified = linesByIndex[index][0];
            return line with
            {
                Quantity = verified.QuantityConfidence >= 0.9m ? verified.Quantity : line.Quantity,
                UnitPrice = verified.UnitPriceConfidence >= 0.9m ? verified.UnitPrice : line.UnitPrice,
                Amount = verified.AmountConfidence >= 0.85m ? verified.Amount : line.Amount
            };
        }).ToArray();
        var candidateTotal = verification.TotalConfidence >= 0.9m ? verification.Total : receipt.Total;
        var originalDifference = Math.Abs(receipt.Lines.Sum(x => x.Amount) - receipt.Total);
        var candidateDifference = Math.Abs(candidateLines.Sum(x => x.Amount) - candidateTotal);
        var changed = candidateTotal != receipt.Total || candidateLines.Where((line, index) => line != receipt.Lines[index]).Any();
        if (!changed || candidateDifference > originalDifference + 0.001m)
        {
            return receipt;
        }

        return receipt with
        {
            Total = candidateTotal,
            Lines = candidateLines,
            Warnings = receipt.Warnings.Append("Os valores monetários foram confirmados numa segunda leitura da imagem.").ToArray()
        };
    }

    private async Task<IReadOnlyList<ParsedClassification>> ClassifyAsync(
        ChatClient client,
        ParsedReceipt receipt,
        IReadOnlyList<ClassificationOption> classificationOptions,
        CancellationToken cancellationToken)
    {
        var context = new
        {
            receipt = new
            {
                receipt.MerchantName,
                receipt.MerchantTaxNumber,
                receipt.DocumentNumber,
                lines = receipt.Lines.Select((line, index) => new { lineIndex = index, line.Description })
            },
            classificationOptions = classificationOptions.Select(x => new
            {
                key = x.Key,
                category = x.Category.Name,
                subcategory = x.Subcategory?.Name
            })
        };
        SystemChatMessage instructions = new(ClassificationInstructions);
        UserChatMessage message = new("Classifica todas as linhas usando este contexto JSON:\n" + JsonSerializer.Serialize(context));
        ChatCompletionOptions options = new()
        {
            Temperature = 0,
#pragma warning disable OPENAI001 // Seed is intentionally pinned to make repeated classifications reproducible.
            Seed = 240930,
#pragma warning restore OPENAI001
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "receipt_classification",
                BinaryData.FromString(CreateClassificationSchema(classificationOptions, receipt.Lines.Count)),
                jsonSchemaIsStrict: true)
        };

        try
        {
            ChatCompletion completion = await client.CompleteChatAsync([instructions, message], options, cancellationToken);
            var parsed = JsonSerializer.Deserialize<ParsedClassifications>(
                completion.Content[0].Text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new IntegrationUnavailableException("A OpenAI devolveu uma classificação vazia.");
            return parsed.Classifications;
        }
        catch (IntegrationUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "OpenAI receipt classification failed.");
            throw new IntegrationUnavailableException("Não foi possível classificar o talão.");
        }
    }

    private static ReceiptLineParseResponse MapLine(
        ParsedLine line,
        ClassificationOption? classificationOption,
        decimal confidence) => new(
        line.Description,
        line.Quantity,
        line.UnitPrice,
        line.Amount,
        classificationOption?.Category.Id,
        classificationOption?.Category.Name,
        classificationOption?.Subcategory?.Id,
        classificationOption?.Subcategory?.Name,
        Math.Clamp(confidence, 0, 1));

    private static void ValidateSignature(byte[] bytes, string contentType)
    {
        var valid = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid) throw new ValidationException("O conteúdo do ficheiro não corresponde ao formato declarado.");
    }

    internal static IReadOnlyList<ClassificationOption> CreateClassificationOptions(IReadOnlyList<Category> categories)
    {
        var options = new List<ClassificationOption>();
        var orderedCategories = categories.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        for (var categoryIndex = 0; categoryIndex < orderedCategories.Length; categoryIndex++)
        {
            var category = orderedCategories[categoryIndex];
            options.Add(new($"c{categoryIndex}", category, null));
            var subcategories = category.Subcategories.Where(x => x.IsActive)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            for (var subcategoryIndex = 0; subcategoryIndex < subcategories.Length; subcategoryIndex++)
            {
                options.Add(new($"c{categoryIndex}s{subcategoryIndex}", category, subcategories[subcategoryIndex]));
            }
        }
        return options;
    }

    internal static string CreateClassificationSchema(
        IReadOnlyList<ClassificationOption> classificationOptions,
        int lineCount)
    {
        var schema = JsonNode.Parse(ClassificationSchema)?.AsObject()
            ?? throw new InvalidOperationException("O schema de classificação não é válido.");
        var properties = schema["properties"]!["classifications"]!["items"]!["properties"]!.AsObject();
        var lineIndexes = new JsonArray();
        for (var index = 0; index < lineCount; index++)
        {
            lineIndexes.Add(index);
        }
        properties["lineIndex"]!["enum"] = lineIndexes;
        properties["classificationKey"] = CreateNullableStringEnum(
            classificationOptions.Select(x => x.Key),
            "Key de um par categoria/subcategoria fornecido ou null.");
        return schema.ToJsonString();
    }

    internal static string CreateValueVerificationSchema(int lineCount)
    {
        var schema = JsonNode.Parse(ValueVerificationSchema)?.AsObject()
            ?? throw new InvalidOperationException("O schema de verificação de valores não é válido.");
        var lineIndexes = new JsonArray();
        for (var index = 0; index < lineCount; index++)
        {
            lineIndexes.Add(index);
        }
        schema["properties"]!["lines"]!["items"]!["properties"]!["lineIndex"]!["enum"] = lineIndexes;
        return schema.ToJsonString();
    }

    internal static ClassificationOption? ApplyClassificationGuardrails(
        string? merchantName,
        string lineDescription,
        ClassificationOption? selectedOption,
        IReadOnlyList<ClassificationOption> classificationOptions)
    {
        var merchant = Normalize(merchantName);
        var description = Normalize(lineDescription);
        var isHealthMerchant = merchant.Contains("WELLS", StringComparison.Ordinal) ||
                               merchant.Contains("FARMACIA", StringComparison.Ordinal) ||
                               merchant.Contains("PARAFARMACIA", StringComparison.Ordinal);
        var healthPharmacy = classificationOptions.FirstOrDefault(x =>
            Normalize(x.Category.Name) == "SAUDE E BEM-ESTAR" &&
            Normalize(x.Subcategory?.Name) == "FARMACIA");
        if (healthPharmacy is null)
        {
            return selectedOption;
        }

        var isKnownHealthProduct = description.Contains("AERO OM", StringComparison.Ordinal) ||
                                   description.Contains("BEXIDENT", StringComparison.Ordinal) ||
                                   description.Contains("BEPANTHEN", StringComparison.Ordinal) ||
                                   description.Contains("BIODIA", StringComparison.Ordinal) ||
                                   description.Contains("ISDIN", StringComparison.Ordinal);
        if (isKnownHealthProduct)
        {
            return healthPharmacy;
        }
        if (!isHealthMerchant)
        {
            return selectedOption;
        }

        var selectedCategory = Normalize(selectedOption?.Category.Name);
        var selectedSubcategory = Normalize(selectedOption?.Subcategory?.Name);
        var isClearlyIncoherent = selectedCategory == "EDUCACAO" || selectedSubcategory == "ELETRONICA";
        return isClearlyIncoherent ? healthPharmacy : selectedOption;
    }

    internal static ClassificationOption? ResolveNotApplicableClassification(
        ClassificationOption? selectedOption,
        IReadOnlyList<ClassificationOption> classificationOptions)
    {
        if (selectedOption?.Subcategory is not null)
        {
            return selectedOption;
        }

        if (selectedOption is not null)
        {
            return classificationOptions.FirstOrDefault(x =>
                       x.Category.Id == selectedOption.Category.Id &&
                       Normalize(x.Subcategory?.Name) == "NAO APLICAVEL")
                   ?? selectedOption;
        }

        return classificationOptions.FirstOrDefault(x =>
                   Normalize(x.Category.Name) == "NAO APLICAVEL" &&
                   Normalize(x.Subcategory?.Name) == "NAO APLICAVEL")
               ?? classificationOptions.FirstOrDefault(x => Normalize(x.Category.Name) == "NAO APLICAVEL");
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static JsonObject CreateNullableStringEnum(IEnumerable<string> allowedValues, string description)
    {
        var values = new JsonArray();
        foreach (var value in allowedValues.Distinct(StringComparer.Ordinal))
        {
            values.Add(value);
        }

        if (values.Count == 0)
        {
            return new JsonObject { ["type"] = "null", ["description"] = description };
        }

        return new JsonObject
        {
            ["description"] = description,
            ["anyOf"] = new JsonArray
            {
                new JsonObject { ["type"] = "string", ["enum"] = values },
                new JsonObject { ["type"] = "null" }
            }
        };
    }

    internal sealed record ClassificationOption(string Key, Category Category, Subcategory? Subcategory);

    internal sealed record ParsedReceipt(
        string? MerchantName,
        string? MerchantTaxNumber,
        string? DocumentNumber,
        string? PurchaseDate,
        string Currency,
        decimal? Subtotal,
        decimal? Tax,
        decimal Total,
        IReadOnlyList<ParsedLine> Lines,
        IReadOnlyList<string> Warnings);

    internal sealed record ParsedLine(
        string Description,
        decimal? Quantity,
        decimal? UnitPrice,
        decimal Amount);

    private sealed record ParsedClassifications(IReadOnlyList<ParsedClassification> Classifications);

    private sealed record ParsedClassification(int LineIndex, string? ClassificationKey, decimal Confidence);

    internal sealed record VerifiedReceiptValues(
        string TotalText,
        decimal Total,
        decimal TotalConfidence,
        IReadOnlyList<VerifiedReceiptLine> Lines);

    internal sealed record VerifiedReceiptLine(
        int LineIndex,
        decimal? Quantity,
        decimal QuantityConfidence,
        string? UnitPriceText,
        decimal? UnitPrice,
        decimal UnitPriceConfidence,
        string AmountText,
        decimal Amount,
        decimal AmountConfidence);
}
