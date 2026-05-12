using System.IO;
using System.Text.Json;
using QuickCut.Contracts.Annotations;

namespace QuickCut.Capture.Services;

public enum AnnotationSpeedSizeMode
{
    FastThick,
    SlowThick,
    Off,
}

public sealed class AnnotationSettings
{
    public const double MinimumStrokeWidth = 1;
    public const double MaximumStrokeWidth = 48;
    public const double DefaultPenWidth = 3;
    public const double DefaultPencilWidth = 3;
    public const double DefaultHighlighterWidth = 18;
    public const double DefaultEraserWidth = 48;
    public const double MaximumStoredEraserWidth = 1200;
    public const double DefaultPenSpeedVariation = 1;
    public const double DefaultPencilTextureVariation = 0.55;
    public const double MinimumBrushVariation = 0;
    public const double MaximumBrushVariation = 1;

    public string ActiveTool { get; set; } = QuickCutAnnotationDefaults.CaptureDefaultTool;

    public string SelectionTarget { get; set; } = QuickCutAnnotationDefaults.DefaultSelectionTarget;

    public string ColorHex { get; set; } = QuickCutAnnotationDefaults.DefaultColorHex;

    public double StrokeWidth { get; set; } = DefaultPenWidth;

    public double PenWidth { get; set; } = DefaultPenWidth;

    public double PencilWidth { get; set; } = DefaultPencilWidth;

    public double HighlighterWidth { get; set; } = DefaultHighlighterWidth;

    public double EraserWidth { get; set; } = DefaultEraserWidth;

    public double PenSpeedVariation { get; set; } = DefaultPenSpeedVariation;

    public double PencilTextureVariation { get; set; } = DefaultPencilTextureVariation;

    public bool PressureEnabled { get; set; } = true;

    public string SpeedSizeMode { get; set; } = "";

    public bool SpeedSizeEnabled { get; set; } = true;
}

public sealed class AnnotationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public AnnotationSettingsStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickCut",
            "annotation-settings.json");
    }

    public string SettingsPath { get; }

    public AnnotationSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return Sanitize(new AnnotationSettings());
            }

            var settings = JsonSerializer.Deserialize<AnnotationSettings>(File.ReadAllText(SettingsPath), JsonOptions);
            return Sanitize(settings);
        }
        catch
        {
            return new AnnotationSettings();
        }
    }

    public bool Save(AnnotationSettings settings)
    {
        try
        {
            var sanitized = Sanitize(settings);
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(sanitized, JsonOptions));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AnnotationSettings Sanitize(AnnotationSettings? settings)
    {
        settings ??= new AnnotationSettings();
        settings.ActiveTool = NormalizeAnnotationTool(settings.ActiveTool, QuickCutAnnotationDefaults.CaptureDefaultTool);
        settings.SelectionTarget = NormalizeSelectionTarget(settings.SelectionTarget);
        settings.ColorHex = IsValidColorHex(settings.ColorHex)
            ? settings.ColorHex
            : QuickCutAnnotationDefaults.DefaultColorHex;
        settings.SpeedSizeMode = NormalizeSpeedSizeMode(settings.SpeedSizeMode, settings.SpeedSizeEnabled);
        settings.SpeedSizeEnabled = settings.SpeedSizeMode != AnnotationSpeedSizeMode.Off.ToString();
        var legacyStrokeWidth = Math.Clamp(
            settings.StrokeWidth,
            AnnotationSettings.MinimumStrokeWidth,
            AnnotationSettings.MaximumStrokeWidth);
        var penWidth = settings.PenWidth == AnnotationSettings.DefaultPenWidth
            ? legacyStrokeWidth
            : settings.PenWidth;
        settings.PenWidth = Math.Clamp(
            penWidth,
            AnnotationSettings.MinimumStrokeWidth,
            AnnotationSettings.MaximumStrokeWidth);
        settings.PencilWidth = Math.Clamp(
            settings.PencilWidth,
            AnnotationSettings.MinimumStrokeWidth,
            AnnotationSettings.MaximumStrokeWidth);
        settings.HighlighterWidth = Math.Clamp(
            settings.HighlighterWidth,
            AnnotationSettings.MinimumStrokeWidth,
            AnnotationSettings.MaximumStrokeWidth);
        settings.EraserWidth = Math.Clamp(
            settings.EraserWidth,
            AnnotationSettings.MinimumStrokeWidth,
            AnnotationSettings.MaximumStoredEraserWidth);
        settings.PenSpeedVariation = Math.Clamp(
            settings.PenSpeedVariation,
            AnnotationSettings.MinimumBrushVariation,
            AnnotationSettings.MaximumBrushVariation);
        settings.PencilTextureVariation = Math.Clamp(
            settings.PencilTextureVariation,
            AnnotationSettings.MinimumBrushVariation,
            AnnotationSettings.MaximumBrushVariation);
        settings.StrokeWidth = settings.PenWidth;
        return settings;
    }

    private static bool IsValidColorHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 9 || value[0] != '#')
        {
            return false;
        }

        return value.Skip(1).All(Uri.IsHexDigit);
    }

    private static string NormalizeAnnotationTool(string? value, string fallback)
    {
        return Enum.TryParse<QuickCutAnnotationTool>(value, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : fallback;
    }

    private static string NormalizeSelectionTarget(string? value)
    {
        return Enum.TryParse<QuickCutAnnotationSelectionTarget>(value, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : QuickCutAnnotationDefaults.DefaultSelectionTarget;
    }

    private static string NormalizeSpeedSizeMode(string? value, bool legacyEnabled)
    {
        if (Enum.TryParse<AnnotationSpeedSizeMode>(value, ignoreCase: true, out var parsed))
        {
            return parsed.ToString();
        }

        return legacyEnabled
            ? AnnotationSpeedSizeMode.FastThick.ToString()
            : AnnotationSpeedSizeMode.Off.ToString();
    }
}
