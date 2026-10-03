using System.Text.Json;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Services;

namespace otw.fings.api.management.Tests;

public sealed class OpenAiReceiptParserTests
{
    [Fact]
    public void ExtractionInstructions_ProtectPortugueseReceiptMonetaryColumns()
    {
        Assert.Contains("I1, I2, I3", OpenAiReceiptParser.ExtractionInstructions);
        Assert.Contains("número mais à direita", OpenAiReceiptParser.ExtractionInstructions);
        Assert.Contains("quantity e unitPrice como null", OpenAiReceiptParser.ExtractionInstructions);
        Assert.Contains("soma os amount", OpenAiReceiptParser.ExtractionInstructions);
        Assert.Contains("resumo de IVA", OpenAiReceiptParser.ExtractionInstructions);
    }

    [Fact]
    public void ClassificationInstructions_IncludeReportedRegressions()
    {
        Assert.Contains("Wells", OpenAiReceiptParser.ClassificationInstructions);
        Assert.Contains("AERO OM BBT 100ML", OpenAiReceiptParser.ClassificationInstructions);
        Assert.Contains("ISDIN BEXIDENT", OpenAiReceiptParser.ClassificationInstructions);
        Assert.Contains("BEPANTHEN", OpenAiReceiptParser.ClassificationInstructions);
        Assert.Contains("Educação", OpenAiReceiptParser.ClassificationInstructions);
        Assert.Contains("nunca são evidência", OpenAiReceiptParser.ClassificationInstructions);
        Assert.Contains("Não aplicável", OpenAiReceiptParser.ClassificationInstructions);
    }

    [Fact]
    public void CreateClassificationOptions_ProducesOnlyValidCategorySubcategoryPairs()
    {
        var householdId = Guid.NewGuid();
        var category = new Category { HouseholdId = householdId, Name = "Saúde e bem-estar" };
        var activeSubcategory = new Subcategory { CategoryId = category.Id, Name = "Farmácia" };
        var inactiveSubcategory = new Subcategory
        {
            CategoryId = category.Id,
            Name = "Antiga",
            IsActive = false
        };
        category.Subcategories = [activeSubcategory, inactiveSubcategory];

        var options = OpenAiReceiptParser.CreateClassificationOptions([category]);

        Assert.Equal(2, options.Count);
        Assert.Contains(options, x => x.Category == category && x.Subcategory is null);
        Assert.Contains(options, x => x.Category == category && x.Subcategory == activeSubcategory);
        Assert.DoesNotContain(options, x => x.Subcategory == inactiveSubcategory);
    }

    [Fact]
    public void CreateClassificationSchema_RestrictsLineIndexesAndPairKeys()
    {
        var category = new Category { HouseholdId = Guid.NewGuid(), Name = "Saúde e bem-estar" };
        category.Subcategories = [new Subcategory { CategoryId = category.Id, Name = "Farmácia" }];
        var options = OpenAiReceiptParser.CreateClassificationOptions([category]);

        using var schema = JsonDocument.Parse(OpenAiReceiptParser.CreateClassificationSchema(options, 3));
        var properties = schema.RootElement.GetProperty("properties")
            .GetProperty("classifications")
            .GetProperty("items")
            .GetProperty("properties");
        var lineIndexes = properties.GetProperty("lineIndex").GetProperty("enum")
            .EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var keys = properties.GetProperty("classificationKey").GetProperty("anyOf")[0]
            .GetProperty("enum").EnumerateArray().Select(x => x.GetString()!).ToArray();

        Assert.Equal([0, 1, 2], lineIndexes);
        Assert.Equal(options.Select(x => x.Key), keys);
    }

    [Fact]
    public void CreateClassificationSchema_AllowsOnlyNullWhenThereAreNoOptions()
    {
        using var schema = JsonDocument.Parse(OpenAiReceiptParser.CreateClassificationSchema([], 1));
        var classificationKey = schema.RootElement.GetProperty("properties")
            .GetProperty("classifications")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("classificationKey");

        Assert.Equal("null", classificationKey.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("AERO OM BBT 100ML")]
    [InlineData("ISDIN BEE BIODIA BOTAS 5ML")]
    [InlineData("IZ BEPANTHEN")]
    public void ApplyClassificationGuardrails_MapsKnownWellsProductsToHealthPharmacy(string description)
    {
        var householdId = Guid.NewGuid();
        var health = new Category { HouseholdId = householdId, Name = "Saúde e bem-estar" };
        var pharmacy = new Subcategory { CategoryId = health.Id, Name = "Farmácia" };
        health.Subcategories = [pharmacy];
        var education = new Category { HouseholdId = householdId, Name = "Educação" };
        var options = OpenAiReceiptParser.CreateClassificationOptions([health, education]);
        var wrongOption = Assert.Single(options, x => x.Category == education && x.Subcategory is null);

        var result = OpenAiReceiptParser.ApplyClassificationGuardrails(
            "WELLS",
            description,
            wrongOption,
            options);

        Assert.NotNull(result);
        Assert.Equal(health.Id, result.Category.Id);
        Assert.Equal(pharmacy.Id, result.Subcategory?.Id);
    }

    [Fact]
    public void ApplyClassificationGuardrails_RecognizesKnownHealthProductWithoutMerchant()
    {
        var health = new Category { HouseholdId = Guid.NewGuid(), Name = "Saúde e bem-estar" };
        var pharmacy = new Subcategory { CategoryId = health.Id, Name = "Farmácia" };
        health.Subcategories = [pharmacy];
        var options = OpenAiReceiptParser.CreateClassificationOptions([health]);

        var result = OpenAiReceiptParser.ApplyClassificationGuardrails(null, "IZ BEPANTHEN", null, options);

        Assert.Equal(health.Id, result?.Category.Id);
        Assert.Equal(pharmacy.Id, result?.Subcategory?.Id);
    }

    [Fact]
    public void ResolveNotApplicableClassification_UsesFallbackWhenCategoryIsUnknown()
    {
        var notApplicable = new Category { HouseholdId = Guid.NewGuid(), Name = "Não aplicável" };
        var notApplicableSubcategory = new Subcategory
        {
            CategoryId = notApplicable.Id,
            Name = "Não aplicável"
        };
        notApplicable.Subcategories = [notApplicableSubcategory];
        var options = OpenAiReceiptParser.CreateClassificationOptions([notApplicable]);

        var result = OpenAiReceiptParser.ResolveNotApplicableClassification(null, options);

        Assert.Equal(notApplicable.Id, result?.Category.Id);
        Assert.Equal(notApplicableSubcategory.Id, result?.Subcategory?.Id);
    }

    [Fact]
    public void ResolveNotApplicableClassification_PreservesCategoryAndFillsUnknownSubcategory()
    {
        var category = new Category { HouseholdId = Guid.NewGuid(), Name = "Alimentação" };
        var notApplicable = new Subcategory { CategoryId = category.Id, Name = "Não aplicável" };
        category.Subcategories = [notApplicable];
        var options = OpenAiReceiptParser.CreateClassificationOptions([category]);
        var categoryOnly = Assert.Single(options, x => x.Category == category && x.Subcategory is null);

        var result = OpenAiReceiptParser.ResolveNotApplicableClassification(categoryOnly, options);

        Assert.Equal(category.Id, result?.Category.Id);
        Assert.Equal(notApplicable.Id, result?.Subcategory?.Id);
    }
}
