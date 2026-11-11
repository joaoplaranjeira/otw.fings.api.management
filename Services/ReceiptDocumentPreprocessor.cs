using PDFtoImage;
using PDFtoImage.Exceptions;
using SkiaSharp;

namespace otw.fings.api.management.Services;

internal static class ReceiptDocumentPreprocessor
{
    internal const string PdfContentType = "application/pdf";
    private const int PdfRenderDpi = 144;
    private const long MaximumRenderedPagePixels = 12_000_000;

    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    internal static bool IsSupportedContentType(string contentType) =>
        ImageContentTypes.Contains(contentType) ||
        string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase);

    internal static PreparedReceiptDocument Prepare(byte[] bytes, string contentType, int maximumPdfPages)
    {
        if (ImageContentTypes.Contains(contentType))
        {
            ValidateImageSignature(bytes, contentType);
            var prepared = ReceiptImagePreprocessor.Prepare(bytes);
            return new(prepared.Images, prepared.WasCropped ? "image/jpeg" : contentType, false, prepared);
        }

        if (!string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Formato inválido. São aceites imagens JPEG, PNG e WebP, ou documentos PDF.");
        }

        ValidatePdfSignature(bytes);
        return PreparePdf(bytes, maximumPdfPages);
    }

    private static PreparedReceiptDocument PreparePdf(byte[] bytes, int maximumPdfPages)
    {
        if (!OperatingSystem.IsLinux() &&
            !OperatingSystem.IsMacOS() &&
            !OperatingSystem.IsWindows() &&
            !OperatingSystem.IsBrowser() &&
            !OperatingSystem.IsAndroidVersionAtLeast(31) &&
            !OperatingSystem.IsIOSVersionAtLeast(13, 6) &&
            !OperatingSystem.IsMacCatalystVersionAtLeast(13, 5))
        {
            throw new ValidationException("A conversão de PDFs não está disponível nesta plataforma.");
        }

        try
        {
#pragma warning disable CA1416 // The runtime platforms supported by PDFtoImage are checked above.
            var pageCount = Conversion.GetPageCount(bytes);
            if (pageCount == 0)
            {
                throw new ValidationException("O PDF não contém páginas.");
            }
            if (pageCount > maximumPdfPages)
            {
                var unit = maximumPdfPages == 1 ? "página" : "páginas";
                throw new ValidationException($"O PDF não pode ter mais de {maximumPdfPages} {unit}.");
            }

            var pageSizes = Conversion.GetPageSizes(bytes);
            foreach (var pageSize in pageSizes)
            {
                var renderedWidth = Math.Ceiling(pageSize.Width * PdfRenderDpi / 72d);
                var renderedHeight = Math.Ceiling(pageSize.Height * PdfRenderDpi / 72d);
                if (renderedWidth <= 0 || renderedHeight <= 0 || renderedWidth * renderedHeight > MaximumRenderedPagePixels)
                {
                    throw new ValidationException("Uma das páginas do PDF tem dimensões demasiado grandes.");
                }
            }

            var images = new List<byte[]>();
            var wasCropped = false;
            var options = new RenderOptions(Dpi: PdfRenderDpi, BackgroundColor: SKColors.White, UseTiling: true);
            foreach (var page in Conversion.ToImages(bytes, Enumerable.Range(0, pageCount), options: options))
            {
                using (page)
                using (var encoded = page.Encode(SKEncodedImageFormat.Jpeg, 94))
                {
                    var preparedPage = ReceiptImagePreprocessor.Prepare(encoded.ToArray());
                    images.AddRange(preparedPage.Images);
                    wasCropped |= preparedPage.WasCropped;
                }
            }

            if (images.Count == 0)
            {
                throw new ValidationException("Não foi possível renderizar as páginas do PDF.");
            }
#pragma warning restore CA1416

            var quality = new ReceiptImagePreprocessor.ImageQualityAssessment(
                100, 1, 1, 1, true, []);
            var prepared = new ReceiptImagePreprocessor.PreparedReceiptImages(
                images,
                wasCropped,
                new SKRectI(),
                quality);
            return new(images, "image/jpeg", true, prepared);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (PdfPasswordProtectedException)
        {
            throw new ValidationException("Não são aceites PDFs protegidos por palavra-passe.");
        }
        catch (PdfUnsupportedSecuritySchemeException)
        {
            throw new ValidationException("O esquema de segurança do PDF não é suportado.");
        }
        catch (PdfException)
        {
            throw new ValidationException("O PDF está corrompido ou usa um formato não suportado.");
        }
    }

    private static void ValidatePdfSignature(byte[] bytes)
    {
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
        {
            throw new ValidationException("O conteúdo do ficheiro não corresponde a um PDF.");
        }
    }

    private static void ValidateImageSignature(byte[] bytes, string contentType)
    {
        var valid = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid)
        {
            throw new ValidationException("O conteúdo do ficheiro não corresponde ao formato declarado.");
        }
    }

    internal sealed record PreparedReceiptDocument(
        IReadOnlyList<byte[]> Images,
        string ContentType,
        bool IsPdf,
        ReceiptImagePreprocessor.PreparedReceiptImages ImagePreparation);
}
