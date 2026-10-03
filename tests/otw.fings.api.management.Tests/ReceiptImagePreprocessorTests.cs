using otw.fings.api.management.Services;
using SkiaSharp;

namespace otw.fings.api.management.Tests;

public sealed class ReceiptImagePreprocessorTests
{
    [Fact]
    public void Prepare_CropsAndSegmentsTallReceiptOnDarkBackground()
    {
        using var source = new SKBitmap(1000, 1800);
        source.Erase(new SKColor(20, 30, 40));
        using (var canvas = new SKCanvas(source))
        using (var paper = new SKPaint { Color = new SKColor(220, 235, 245) })
        using (var ink = new SKPaint { Color = new SKColor(50, 55, 60) })
        {
            canvas.DrawRect(new SKRect(280, 40, 720, 1800), paper);
            for (var y = 100; y < 1700; y += 28) canvas.DrawRect(new SKRect(330, y, 650, y + 5), ink);
        }
        var bytes = Encode(source);

        var result = ReceiptImagePreprocessor.Prepare(bytes);

        Assert.True(result.WasCropped);
        Assert.InRange(result.Bounds.Left, 180, 300);
        Assert.InRange(result.Bounds.Width, 430, 620);
        Assert.InRange(result.Images.Count, 2, 4);
        foreach (var segmentBytes in result.Images)
        {
            using var segment = SKBitmap.Decode(segmentBytes);
            Assert.NotNull(segment);
            Assert.True(segment.Width < source.Width);
            Assert.True(segment.Height <= Math.Max(640, (int)Math.Round(segment.Width * 1.6)));
        }
    }

    [Fact]
    public void Prepare_KeepsOriginalWhenNoReceiptCanBeDetected()
    {
        using var source = new SKBitmap(800, 600);
        source.Erase(new SKColor(20, 30, 40));
        var bytes = Encode(source);

        var result = ReceiptImagePreprocessor.Prepare(bytes);

        Assert.False(result.WasCropped);
        Assert.Single(result.Images);
        Assert.Same(bytes, result.Images[0]);
    }

    [Fact]
    public void Prepare_RatesBlurredReceiptBelowSharpReceipt()
    {
        using var sharp = CreateReceipt(900, 1600, 100, 40, 700, 1520);
        using var blurred = new SKBitmap(sharp.Width, sharp.Height);
        using (var canvas = new SKCanvas(blurred))
        using (var paint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(7, 7) })
        {
            canvas.DrawBitmap(sharp, 0, 0, paint);
        }

        var sharpResult = ReceiptImagePreprocessor.Prepare(Encode(sharp));
        var blurredResult = ReceiptImagePreprocessor.Prepare(Encode(blurred));

        Assert.True(sharpResult.Quality.Score >= 80);
        Assert.True(blurredResult.Quality.Score < 80);
        Assert.True(blurredResult.Quality.Score < sharpResult.Quality.Score);
        Assert.Contains(blurredResult.Quality.Warnings, warning => warning.Contains("desfocada"));
    }

    [Fact]
    public void Prepare_RatesSmallReceiptAsInsufficient()
    {
        using var source = CreateReceipt(420, 800, 85, 20, 250, 760);

        var result = ReceiptImagePreprocessor.Prepare(Encode(source));

        Assert.True(result.Quality.Score < 80);
        Assert.Contains(result.Quality.Warnings, warning => warning.Contains("poucos píxeis"));
    }

    private static SKBitmap CreateReceipt(int width, int height, int left, int top, int receiptWidth, int receiptHeight)
    {
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(15, 25, 35));
        using var canvas = new SKCanvas(bitmap);
        using var paper = new SKPaint { Color = new SKColor(220, 235, 245) };
        using var ink = new SKPaint { Color = new SKColor(45, 50, 55) };
        canvas.DrawRect(new SKRect(left, top, left + receiptWidth, top + receiptHeight), paper);
        for (var y = top + 45; y < top + receiptHeight - 30; y += 24)
        {
            canvas.DrawRect(new SKRect(left + 35, y, left + receiptWidth - 35, y + 4), ink);
        }
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }
}
