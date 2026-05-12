using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickCut.Capture.Services;

public readonly record struct CaptureImageSize(int Width, int Height);

public readonly record struct CaptureImagePlacement(int X, int Y, int Width, int Height);

public sealed record CaptureImageLayout(
    int Width,
    int Height,
    IReadOnlyList<CaptureImagePlacement> Placements);

public static class CaptureImageComposer
{
    public const double TargetAspectRatio = 16d / 9d;
    public const int DefaultGutter = 12;

    public static CaptureImageLayout PlanLayout(
        IReadOnlyList<CaptureImageSize> sizes,
        int gutter = DefaultGutter)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        if (sizes.Count == 0)
        {
            throw new ArgumentException("At least one image size is required.", nameof(sizes));
        }

        if (gutter < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gutter), "Gutter must be positive.");
        }

        foreach (var size in sizes)
        {
            if (size.Width <= 0 || size.Height <= 0)
            {
                throw new ArgumentException("Image sizes must be positive.", nameof(sizes));
            }
        }

        CaptureImageLayout? best = null;
        var bestScore = double.MaxValue;
        var bestArea = long.MaxValue;

        for (var columns = 1; columns <= sizes.Count; columns++)
        {
            var candidate = BuildRowMajorLayout(sizes, columns, gutter);
            var score = AspectScore(candidate.Width, candidate.Height);
            var area = (long)candidate.Width * candidate.Height;
            if (score < bestScore || (Math.Abs(score - bestScore) < 0.0001 && area < bestArea))
            {
                best = candidate;
                bestScore = score;
                bestArea = area;
            }
        }

        return best!;
    }

    public static BitmapSource Compose(
        IReadOnlyList<BitmapSource> images,
        int gutter = DefaultGutter,
        Color? backgroundColor = null)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
        {
            throw new ArgumentException("At least one image is required.", nameof(images));
        }

        if (images.Count == 1)
        {
            return images[0];
        }

        var layout = PlanLayout(
            images.Select(image => new CaptureImageSize(image.PixelWidth, image.PixelHeight)).ToArray(),
            gutter);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var background = new SolidColorBrush(backgroundColor ?? Color.FromRgb(18, 18, 18));
            context.DrawRectangle(background, null, new Rect(0, 0, layout.Width, layout.Height));

            for (var index = 0; index < images.Count; index++)
            {
                var placement = layout.Placements[index];
                context.DrawImage(
                    images[index],
                    new Rect(placement.X, placement.Y, placement.Width, placement.Height));
            }
        }

        var rendered = new RenderTargetBitmap(layout.Width, layout.Height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        rendered.Freeze();
        return rendered;
    }

    private static CaptureImageLayout BuildRowMajorLayout(
        IReadOnlyList<CaptureImageSize> sizes,
        int columns,
        int gutter)
    {
        var rows = new List<RowPlan>();
        for (var start = 0; start < sizes.Count; start += columns)
        {
            var count = Math.Min(columns, sizes.Count - start);
            var width = 0;
            var height = 0;
            for (var index = start; index < start + count; index++)
            {
                width += sizes[index].Width;
                height = Math.Max(height, sizes[index].Height);
            }

            width += gutter * Math.Max(0, count - 1);
            rows.Add(new RowPlan(start, count, width, height));
        }

        var totalWidth = rows.Max(row => row.Width);
        var totalHeight = rows.Sum(row => row.Height) + gutter * Math.Max(0, rows.Count - 1);
        var placements = new CaptureImagePlacement[sizes.Count];
        var y = 0;
        foreach (var row in rows)
        {
            var x = 0;
            for (var offset = 0; offset < row.Count; offset++)
            {
                var index = row.Start + offset;
                var size = sizes[index];
                placements[index] = new CaptureImagePlacement(x, y, size.Width, size.Height);
                x += size.Width + gutter;
            }

            y += row.Height + gutter;
        }

        return new CaptureImageLayout(totalWidth, totalHeight, placements);
    }

    private static double AspectScore(int width, int height)
    {
        var aspect = width / (double)height;
        return Math.Abs(Math.Log(aspect / TargetAspectRatio));
    }

    private sealed record RowPlan(int Start, int Count, int Width, int Height);
}
