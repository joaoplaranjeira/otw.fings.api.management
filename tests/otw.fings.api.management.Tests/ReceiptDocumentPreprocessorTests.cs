using System.Globalization;
using System.Text;
using otw.fings.api.management.Services;
using SkiaSharp;

namespace otw.fings.api.management.Tests;

public sealed class ReceiptDocumentPreprocessorTests
{
    [Fact]
    public void Prepare_RendersPdfPagesAsJpegImages()
    {
        var pdf = CreatePdf(2);

        var result = ReceiptDocumentPreprocessor.Prepare(pdf, "application/pdf", 10);

        Assert.True(result.IsPdf);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(2, result.Images.Count);
        Assert.Equal(100, result.ImagePreparation.Quality.Score);
        foreach (var imageBytes in result.Images)
        {
            using var image = SKBitmap.Decode(imageBytes);
            Assert.NotNull(image);
            Assert.True(image.Width > 0);
            Assert.True(image.Height > 0);
        }
    }

    [Fact]
    public void Prepare_RejectsPdfWithTooManyPages()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            ReceiptDocumentPreprocessor.Prepare(CreatePdf(2), "application/pdf", 1));

        Assert.Contains("mais de 1 página", exception.Message);
    }

    [Fact]
    public void Prepare_RejectsContentThatDoesNotMatchPdfMimeType()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            ReceiptDocumentPreprocessor.Prepare("not a pdf"u8.ToArray(), "application/pdf", 10));

        Assert.Contains("não corresponde a um PDF", exception.Message);
    }

    [Fact]
    public void Prepare_RejectsCorruptPdf()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            ReceiptDocumentPreprocessor.Prepare("%PDF-corrupt"u8.ToArray(), "application/pdf", 10));

        Assert.Contains("corrompido", exception.Message);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("application/pdf")]
    public void IsSupportedContentType_AcceptsReceiptFormats(string contentType)
    {
        Assert.True(ReceiptDocumentPreprocessor.IsSupportedContentType(contentType));
    }

    private static byte[] CreatePdf(int pageCount)
    {
        var objects = new SortedDictionary<int, string>();
        var pageObjectNumbers = Enumerable.Range(0, pageCount).Select(index => 3 + index).ToArray();
        var fontObjectNumber = 3 + pageCount;
        var firstContentObjectNumber = fontObjectNumber + 1;

        objects[1] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[2] = $"<< /Type /Pages /Kids [{string.Join(' ', pageObjectNumbers.Select(number => $"{number} 0 R"))}] /Count {pageCount} >>";
        objects[fontObjectNumber] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";

        for (var index = 0; index < pageCount; index++)
        {
            var pageObjectNumber = pageObjectNumbers[index];
            var contentObjectNumber = firstContentObjectNumber + index;
            var content = $"BT /F1 18 Tf 30 450 Td (Receipt page {index + 1}) Tj ET";
            objects[pageObjectNumber] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 500] /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>";
            objects[contentObjectNumber] = $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream";
        }

        var builder = new StringBuilder("%PDF-1.4\n%1234\n");
        var offsets = new Dictionary<int, int>();
        foreach (var (number, value) in objects)
        {
            offsets[number] = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.Append(number).Append(" 0 obj\n").Append(value).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        var objectCount = objects.Keys.Max() + 1;
        builder.Append("xref\n0 ").Append(objectCount).Append("\n");
        builder.Append("0000000000 65535 f \n");
        for (var number = 1; number < objectCount; number++)
        {
            builder.Append(offsets[number].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        builder.Append("trailer\n<< /Size ").Append(objectCount).Append(" /Root 1 0 R >>\n");
        builder.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
