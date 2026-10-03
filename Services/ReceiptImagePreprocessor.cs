using SkiaSharp;

namespace otw.fings.api.management.Services;

internal static class ReceiptImagePreprocessor
{
    private const int MaximumSegments = 4;

    internal static PreparedReceiptImages Prepare(byte[] source)
    {
        using var image = SKBitmap.Decode(source);
        if (image is null)
        {
            return new([source], false, new SKRectI(), ImageQualityAssessment.Unreadable);
        }

        var bounds = FindReceiptBounds(image);
        if (bounds is null)
        {
            return new(
                [source],
                false,
                new SKRectI(0, 0, image.Width, image.Height),
                AssessQuality(image, receiptDetected: false));
        }

        using var receipt = Crop(image, bounds.Value);
        var quality = AssessQuality(receipt, receiptDetected: true);
        var targetHeight = Math.Min(receipt.Height, Math.Max(640, (int)Math.Round(receipt.Width * 1.6)));
        if (receipt.Height <= targetHeight * 1.15)
        {
            return new([Encode(receipt)], true, bounds.Value, quality);
        }

        var overlap = Math.Max(80, (int)Math.Round(targetHeight * 0.14));
        var step = targetHeight - overlap;
        var segments = new List<byte[]>();
        for (var top = 0; top < receipt.Height && segments.Count < MaximumSegments; top += step)
        {
            if (top + targetHeight > receipt.Height) top = Math.Max(0, receipt.Height - targetHeight);
            using var segment = Crop(receipt, new SKRectI(0, top, receipt.Width, Math.Min(receipt.Height, top + targetHeight)));
            segments.Add(Encode(segment));
            if (top + targetHeight >= receipt.Height) break;
        }
        return new(segments, true, bounds.Value, quality);
    }

    private static SKRectI? FindReceiptBounds(SKBitmap image)
    {
        const int detectionMaximumDimension = 768;
        var scale = Math.Min(1d, detectionMaximumDimension / (double)Math.Max(image.Width, image.Height));
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        var mask = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Min(image.Height - 1, (int)Math.Round(y / scale));
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(image.Width - 1, (int)Math.Round(x / scale));
                mask[y * width + x] = IsPaperPixel(image.GetPixel(sourceX, sourceY));
            }
        }

        var visited = new bool[mask.Length];
        var queue = new int[mask.Length];
        SKRectI? best = null;
        double bestScore = double.MinValue;
        var minimumPixels = Math.Max(64, mask.Length / 200);
        for (var seed = 0; seed < mask.Length; seed++)
        {
            if (!mask[seed] || visited[seed]) continue;
            var head = 0;
            var tail = 0;
            queue[tail++] = seed;
            visited[seed] = true;
            var left = seed % width;
            var right = left;
            var top = seed / width;
            var bottom = top;
            var count = 0;
            while (head < tail)
            {
                var current = queue[head++];
                var x = current % width;
                var y = current / width;
                count++;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);

                TryEnqueue(x - 1, y);
                TryEnqueue(x + 1, y);
                TryEnqueue(x, y - 1);
                TryEnqueue(x, y + 1);
            }

            if (count < minimumPixels) continue;
            var componentWidth = right - left + 1;
            var componentHeight = bottom - top + 1;
            if (componentWidth < width * 0.12 || componentWidth > width * 0.85 ||
                componentHeight < height * 0.35 || componentHeight < componentWidth * 1.15) continue;

            var density = count / (double)(componentWidth * componentHeight);
            var center = (left + right) / 2d;
            var centerCloseness = 1 - Math.Min(1, Math.Abs(center - width / 2d) / (width / 2d));
            var score = componentHeight / (double)height * 4 + count / (double)mask.Length * 8 + density + centerCloseness;
            if (score > bestScore)
            {
                bestScore = score;
                best = new SKRectI(left, top, right + 1, bottom + 1);
            }

            void TryEnqueue(int candidateX, int candidateY)
            {
                if ((uint)candidateX >= (uint)width || (uint)candidateY >= (uint)height) return;
                var candidate = candidateY * width + candidateX;
                if (!mask[candidate] || visited[candidate]) return;
                visited[candidate] = true;
                queue[tail++] = candidate;
            }
        }

        if (best is null) return null;
        var leftOriginal = (int)Math.Floor(best.Value.Left / scale);
        var rightOriginal = (int)Math.Ceiling(best.Value.Right / scale);
        var topOriginal = (int)Math.Floor(best.Value.Top / scale);
        var bottomOriginal = (int)Math.Ceiling(best.Value.Bottom / scale);
        var detectedWidth = rightOriginal - leftOriginal;
        var detectedHeight = bottomOriginal - topOriginal;
        var horizontalPadding = Math.Max(12, (int)Math.Round(detectedWidth * 0.1));
        var verticalPadding = Math.Max(12, (int)Math.Round(detectedHeight * 0.025));
        leftOriginal = Math.Max(0, leftOriginal - horizontalPadding);
        rightOriginal = Math.Min(image.Width, rightOriginal + horizontalPadding);
        topOriginal = Math.Max(0, topOriginal - verticalPadding);
        bottomOriginal = Math.Min(image.Height, bottomOriginal + verticalPadding);
        return new SKRectI(leftOriginal, topOriginal, rightOriginal, bottomOriginal);
    }

    private static bool IsPaperPixel(SKColor pixel)
    {
        var maximum = Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue));
        var minimum = Math.Min(pixel.Red, Math.Min(pixel.Green, pixel.Blue));
        var luminance = (pixel.Red * 299 + pixel.Green * 587 + pixel.Blue * 114) / 1000;
        var neutralWhite = luminance >= 200 && maximum - minimum <= 20;
        var coolWhite = pixel.Blue >= pixel.Green && pixel.Green >= pixel.Red &&
                        pixel.Blue - pixel.Red is >= 15 and <= 100 && pixel.Green - pixel.Red >= 8;
        return neutralWhite || (luminance >= 140 && coolWhite);
    }

    private static SKBitmap Crop(SKBitmap source, SKRectI bounds)
    {
        var target = new SKBitmap(bounds.Width, bounds.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(target);
        canvas.DrawBitmap(source, bounds, new SKRect(0, 0, target.Width, target.Height));
        return target;
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 94);
        return data.ToArray();
    }

    private static ImageQualityAssessment AssessQuality(SKBitmap receipt, bool receiptDetected)
    {
        var resolutionScore = Math.Clamp((receipt.Width - 320d) / 380d, 0, 1);
        var sampleStep = Math.Max(1, Math.Max(receipt.Width, receipt.Height) / 1000);
        double laplacianTotal = 0;
        var sampledPixels = 0;
        var wellExposedPixels = 0;
        var exposurePixels = 0;
        for (var y = sampleStep; y < receipt.Height - sampleStep; y += sampleStep)
        {
            for (var x = sampleStep; x < receipt.Width - sampleStep; x += sampleStep)
            {
                var center = Luminance(receipt.GetPixel(x, y));
                var laplacian = Math.Abs(
                    4 * center -
                    Luminance(receipt.GetPixel(x - sampleStep, y)) -
                    Luminance(receipt.GetPixel(x + sampleStep, y)) -
                    Luminance(receipt.GetPixel(x, y - sampleStep)) -
                    Luminance(receipt.GetPixel(x, y + sampleStep)));
                laplacianTotal += laplacian;
                sampledPixels++;
                if (center is >= 18 and <= 247) wellExposedPixels++;
                exposurePixels++;
            }
        }

        var averageLaplacian = sampledPixels == 0 ? 0 : laplacianTotal / sampledPixels;
        var sharpnessScore = Math.Clamp((averageLaplacian - 3d) / 11d, 0, 1);
        var exposedFraction = exposurePixels == 0 ? 0 : wellExposedPixels / (double)exposurePixels;
        var exposureScore = Math.Clamp((exposedFraction - 0.55d) / 0.35d, 0, 1);
        var detectionScore = receiptDetected ? 1d : 0.55d;
        var score = (int)Math.Round(
            100 * (resolutionScore * 0.3 + sharpnessScore * 0.5 + exposureScore * 0.1 + detectionScore * 0.1));

        var warnings = new List<string>();
        if (resolutionScore < 0.7) warnings.Add("O talão ocupa poucos píxeis na fotografia; aproxime a câmara.");
        if (sharpnessScore < 0.7) warnings.Add("A fotografia pode estar desfocada ou com texto pouco nítido.");
        if (exposureScore < 0.65) warnings.Add("A fotografia pode ter zonas demasiado escuras ou demasiado claras.");
        if (!receiptDetected) warnings.Add("Não foi possível isolar o talão do fundo com segurança.");
        return new(score, resolutionScore, sharpnessScore, exposureScore, receiptDetected, warnings);
    }

    private static int Luminance(SKColor pixel) =>
        (pixel.Red * 299 + pixel.Green * 587 + pixel.Blue * 114) / 1000;

    internal sealed record PreparedReceiptImages(
        IReadOnlyList<byte[]> Images,
        bool WasCropped,
        SKRectI Bounds,
        ImageQualityAssessment Quality);

    internal sealed record ImageQualityAssessment(
        int Score,
        double ResolutionScore,
        double SharpnessScore,
        double ExposureScore,
        bool ReceiptDetected,
        IReadOnlyList<string> Warnings)
    {
        internal static readonly ImageQualityAssessment Unreadable = new(
            0, 0, 0, 0, false, ["Não foi possível ler a imagem."]);
    }
}
