using System.Windows.Ink;
using System.Windows.Input;

namespace QuickCut.Capture.Services;

public static class AnnotationStrokeTextureProcessor
{
    private const double FullStrengthMinimumPressure = 0.34;
    private const double MinimumJitterPixels = 0.15;
    private const double MaximumJitterPixels = 2.4;
    private const double FullStrengthJitterWidthRatio = 0.28;

    public static void ApplyPencilTexture(
        Stroke stroke,
        double variation,
        bool combineHardwarePressure)
    {
        var safeVariation = Math.Clamp(variation, 0, 1);
        if (safeVariation <= 0 || stroke.StylusPoints.Count == 0)
        {
            return;
        }

        var textureStrength = Math.Sqrt(safeVariation);
        var points = stroke.StylusPoints;
        var factors = new double[points.Count];
        var minimumPressure = 1 - textureStrength * (1 - FullStrengthMinimumPressure);
        var random = new Random(SeedFromPoints(points));
        var phase = random.NextDouble() * Math.PI * 2;
        var jitterRadius = Math.Clamp(
            stroke.DrawingAttributes.Width * (0.06 + textureStrength * FullStrengthJitterWidthRatio),
            MinimumJitterPixels,
            MaximumJitterPixels);
        var total = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var factor = GrainFactor(random, index, phase, minimumPressure);
            factors[index] = factor;
            total += factor;
        }

        var averagePressure = Math.Clamp(total / factors.Length, minimumPressure, 1);
        var attributes = stroke.DrawingAttributes.Clone();
        var scaledWidth = Math.Clamp(attributes.Width / averagePressure, 1, 80);
        attributes.Width = scaledWidth;
        attributes.Height = scaledWidth;
        attributes.FitToCurve = false;
        attributes.IgnorePressure = false;
        stroke.DrawingAttributes = attributes;

        for (var index = 0; index < points.Count; index++)
        {
            ApplyTextureJitter(points, index, random, phase, jitterRadius);

            var point = points[index];
            var hardwarePressure = combineHardwarePressure
                ? Math.Clamp(point.PressureFactor, 0.2, 1)
                : 1;
            point.PressureFactor = (float)Math.Clamp(hardwarePressure * factors[index], 0.2, 1);
            points[index] = point;
        }
    }

    public static StrokeCollection CreatePencilTextureStrokes(
        Stroke stroke,
        double variation,
        bool combineHardwarePressure)
    {
        var fragments = new StrokeCollection();
        var safeVariation = Math.Clamp(variation, 0, 1);
        if (safeVariation <= 0 || stroke.StylusPoints.Count < 2)
        {
            return fragments;
        }

        var texturedStroke = stroke.Clone();
        ApplyPencilTexture(texturedStroke, safeVariation, combineHardwarePressure);
        for (var index = 1; index < texturedStroke.StylusPoints.Count; index++)
        {
            var start = texturedStroke.StylusPoints[index - 1];
            var end = texturedStroke.StylusPoints[index];
            var segmentPressure = (start.PressureFactor + end.PressureFactor) / 2d;
            var width = Math.Clamp(texturedStroke.DrawingAttributes.Width * segmentPressure, 1, 80);
            fragments.Add(CreateSegmentStroke(start, end, texturedStroke.DrawingAttributes, width));
        }

        return fragments;
    }

    private static double GrainFactor(Random random, int index, double phase, double minimumPressure)
    {
        var randomGrain = random.NextDouble();
        var waveGrain = (Math.Sin(index * 2.17 + phase) + 1) / 2d;
        var raw = randomGrain * 0.72 + waveGrain * 0.28;
        if (index % 5 == 2)
        {
            raw *= 0.55;
        }

        return minimumPressure + raw * (1 - minimumPressure);
    }

    private static void ApplyTextureJitter(
        StylusPointCollection points,
        int index,
        Random random,
        double phase,
        double jitterRadius)
    {
        if (points.Count < 3 || index == 0 || index == points.Count - 1)
        {
            return;
        }

        var previous = points[index - 1];
        var next = points[index + 1];
        var point = points[index];
        var dx = next.X - previous.X;
        var dy = next.Y - previous.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0.001)
        {
            return;
        }

        var tangentX = dx / length;
        var tangentY = dy / length;
        var normalX = -tangentY;
        var normalY = tangentX;
        var normalJitter = (random.NextDouble() * 2 - 1) * jitterRadius;
        var tangentJitter = Math.Sin(index * 1.61 + phase) * jitterRadius * 0.28;

        point.X += normalX * normalJitter + tangentX * tangentJitter;
        point.Y += normalY * normalJitter + tangentY * tangentJitter;
        points[index] = point;
    }

    private static Stroke CreateSegmentStroke(
        StylusPoint start,
        StylusPoint end,
        DrawingAttributes sourceAttributes,
        double width)
    {
        var attributes = sourceAttributes.Clone();
        attributes.Width = width;
        attributes.Height = width;
        attributes.IgnorePressure = true;
        attributes.FitToCurve = false;

        var startPoint = new StylusPoint(start.X, start.Y) { PressureFactor = 1 };
        var endPoint = new StylusPoint(end.X, end.Y) { PressureFactor = 1 };
        return new Stroke(new StylusPointCollection { startPoint, endPoint }, attributes);
    }

    private static int SeedFromPoints(StylusPointCollection points)
    {
        unchecked
        {
            var seed = 17;
            foreach (var point in points)
            {
                seed = seed * 31 + point.X.GetHashCode();
                seed = seed * 31 + point.Y.GetHashCode();
            }

            return seed;
        }
    }
}
