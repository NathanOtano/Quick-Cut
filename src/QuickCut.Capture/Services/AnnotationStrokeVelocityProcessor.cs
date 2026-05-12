using System.Windows.Ink;
using System.Windows.Input;

namespace QuickCut.Capture.Services;

public static class AnnotationStrokeVelocityProcessor
{
    private const double SlowDistance = 1.0;
    private const double FastDistance = 13;
    private const double RelativeDistanceMinimumSpan = 4;
    private const double FullStrengthMinimumPressure = 0.42;

    public static void Apply(Stroke stroke, bool combineHardwarePressure)
    {
        Apply(stroke, combineHardwarePressure, AnnotationSpeedSizeMode.FastThick);
    }

    public static void Apply(
        Stroke stroke,
        bool combineHardwarePressure,
        AnnotationSpeedSizeMode mode,
        double strength = AnnotationSettings.DefaultPenSpeedVariation)
    {
        var safeStrength = Math.Clamp(strength, 0, 1);
        if (mode == AnnotationSpeedSizeMode.Off || safeStrength <= 0 || stroke.StylusPoints.Count < 2)
        {
            return;
        }

        var minimumPressure = MinimumPressureForStrength(safeStrength);
        var neutralPressure = (1 + minimumPressure) / 2d;
        var pointDistances = PointDistances(stroke.StylusPoints);
        var pointPressures = SpeedPressures(pointDistances, mode, safeStrength);

        var attributes = stroke.DrawingAttributes.Clone();
        var scaledWidth = Math.Clamp(attributes.Width / neutralPressure, 1, 80);
        attributes.Width = scaledWidth;
        attributes.Height = scaledWidth;
        attributes.IgnorePressure = false;
        stroke.DrawingAttributes = attributes;

        var points = stroke.StylusPoints;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var hardwarePressure = combineHardwarePressure
                ? Math.Clamp(point.PressureFactor, 0.2, 1)
                : 1;

            point.PressureFactor = (float)Math.Clamp(hardwarePressure * pointPressures[index], 0.2, 1);
            points[index] = point;
        }
    }

    public static StrokeCollection CreateSpeedSizedStrokes(
        Stroke stroke,
        bool combineHardwarePressure,
        AnnotationSpeedSizeMode mode,
        double strength = AnnotationSettings.DefaultPenSpeedVariation)
    {
        var fragments = new StrokeCollection();
        var safeStrength = Math.Clamp(strength, 0, 1);
        if (mode == AnnotationSpeedSizeMode.Off || safeStrength <= 0 || stroke.StylusPoints.Count < 2)
        {
            return fragments;
        }

        var minimumPressure = MinimumPressureForStrength(safeStrength);
        var neutralPressure = (1 + minimumPressure) / 2d;
        var pointDistances = PointDistances(stroke.StylusPoints);
        var pointPressures = SpeedPressures(pointDistances, mode, safeStrength);
        var scaledBaseWidth = Math.Clamp(stroke.DrawingAttributes.Width / neutralPressure, 1, 80);

        for (var index = 1; index < stroke.StylusPoints.Count; index++)
        {
            var start = stroke.StylusPoints[index - 1];
            var end = stroke.StylusPoints[index];
            var startHardwarePressure = combineHardwarePressure ? Math.Clamp(start.PressureFactor, 0.2, 1) : 1;
            var endHardwarePressure = combineHardwarePressure ? Math.Clamp(end.PressureFactor, 0.2, 1) : 1;
            var segmentPressure = (
                pointPressures[index - 1] * startHardwarePressure
                + pointPressures[index] * endHardwarePressure) / 2d;
            var width = Math.Clamp(scaledBaseWidth * segmentPressure, 1, 80);
            fragments.Add(CreateSegmentStroke(start, end, stroke.DrawingAttributes, width, fitToCurve: true));
        }

        return fragments;
    }

    public static double PressureForDistance(
        double distance,
        AnnotationSpeedSizeMode mode = AnnotationSpeedSizeMode.FastThick,
        double strength = AnnotationSettings.DefaultPenSpeedVariation)
    {
        return PressureForDistance(distance, mode, strength, SlowDistance, FastDistance);
    }

    private static double PressureForDistance(
        double distance,
        AnnotationSpeedSizeMode mode,
        double strength,
        double observedSlow,
        double observedFast)
    {
        var minimumPressure = MinimumPressureForStrength(strength);
        var absoluteRatio = DistanceRatio(distance, SlowDistance, FastDistance);
        var observedRatio = observedFast - observedSlow >= RelativeDistanceMinimumSpan
            ? DistanceRatio(distance, observedSlow, observedFast)
            : absoluteRatio;
        var ratio = Math.Max(absoluteRatio, observedRatio);
        return mode switch
        {
            AnnotationSpeedSizeMode.SlowThick => 1 - ratio * (1 - minimumPressure),
            AnnotationSpeedSizeMode.Off => 1,
            _ => minimumPressure + ratio * (1 - minimumPressure),
        };
    }

    private static double MinimumPressureForStrength(double strength)
    {
        var safeStrength = Math.Clamp(strength, 0, 1);
        return 1 - safeStrength * (1 - FullStrengthMinimumPressure);
    }

    private static double[] SpeedPressures(
        double[] pointDistances,
        AnnotationSpeedSizeMode mode,
        double strength)
    {
        var pointPressures = new double[pointDistances.Length];
        var (observedSlow, observedFast) = ObservedDistanceRange(pointDistances);
        for (var index = 0; index < pointDistances.Length; index++)
        {
            pointPressures[index] = PressureForDistance(
                pointDistances[index],
                mode,
                strength,
                observedSlow,
                observedFast);
        }

        return pointPressures;
    }

    private static double DistanceRatio(double distance, double slowDistance, double fastDistance)
    {
        return Math.Clamp((distance - slowDistance) / Math.Max(0.1, fastDistance - slowDistance), 0, 1);
    }

    private static double[] PointDistances(StylusPointCollection points)
    {
        var distances = new double[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            distances[index] = index switch
            {
                0 => Distance(points[0], points[1]),
                _ when index == points.Count - 1 => Distance(points[index - 1], points[index]),
                _ => (Distance(points[index - 1], points[index]) + Distance(points[index], points[index + 1])) / 2d,
            };
        }

        return distances;
    }

    private static (double Slow, double Fast) ObservedDistanceRange(double[] distances)
    {
        var sorted = distances.Order().ToArray();
        var slow = sorted[(int)Math.Floor((sorted.Length - 1) * 0.2)];
        var fast = sorted[(int)Math.Ceiling((sorted.Length - 1) * 0.8)];
        return (slow, fast);
    }

    private static Stroke CreateSegmentStroke(
        StylusPoint start,
        StylusPoint end,
        DrawingAttributes sourceAttributes,
        double width,
        bool fitToCurve)
    {
        var attributes = sourceAttributes.Clone();
        attributes.Width = width;
        attributes.Height = width;
        attributes.IgnorePressure = true;
        attributes.FitToCurve = fitToCurve;

        var startPoint = new StylusPoint(start.X, start.Y) { PressureFactor = 1 };
        var endPoint = new StylusPoint(end.X, end.Y) { PressureFactor = 1 };
        return new Stroke(new StylusPointCollection { startPoint, endPoint }, attributes);
    }

    private static double Distance(StylusPoint start, StylusPoint end)
    {
        var x = end.X - start.X;
        var y = end.Y - start.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
