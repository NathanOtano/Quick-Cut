using QuickCut.Capture.Services;

namespace QuickCut.Contracts.Tests.Capture;

public sealed class AnnotationSettingsStoreTests
{
    [Fact]
    public void LoadReturnsDefaultsWhenSettingsFileIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), "QuickCut.Tests", Guid.NewGuid().ToString("N"), "annotation-settings.json");
        var store = new AnnotationSettingsStore(path);

        var settings = store.Load();

        Assert.Equal("Pen", settings.ActiveTool);
        Assert.Equal("#FFFF0000", settings.ColorHex);
        Assert.Equal(AnnotationSettings.DefaultPenWidth, settings.PenWidth);
        Assert.Equal(AnnotationSettings.DefaultPencilWidth, settings.PencilWidth);
        Assert.Equal(AnnotationSettings.DefaultHighlighterWidth, settings.HighlighterWidth);
        Assert.Equal(AnnotationSettings.DefaultEraserWidth, settings.EraserWidth);
        Assert.Equal(AnnotationSettings.DefaultPenSpeedVariation, settings.PenSpeedVariation);
        Assert.Equal(AnnotationSettings.DefaultPencilTextureVariation, settings.PencilTextureVariation);
        Assert.True(settings.PressureEnabled);
        Assert.Equal(AnnotationSpeedSizeMode.FastThick.ToString(), settings.SpeedSizeMode);
        Assert.True(settings.SpeedSizeEnabled);
    }

    [Fact]
    public void SaveAndLoadRoundTripsAnnotationSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "QuickCut.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "annotation-settings.json");
        var store = new AnnotationSettingsStore(path);

        try
        {
            var saved = store.Save(new AnnotationSettings
            {
                ActiveTool = "Highlighter",
                SelectionTarget = "Both",
                ColorHex = "#FF00BFFF",
                PenWidth = 9,
                PencilWidth = 7,
                HighlighterWidth = 22,
                EraserWidth = 640,
                PenSpeedVariation = 0.35,
                PencilTextureVariation = 0.7,
                PressureEnabled = false,
                SpeedSizeMode = AnnotationSpeedSizeMode.SlowThick.ToString(),
                SpeedSizeEnabled = true,
            });

            var loaded = store.Load();

            Assert.True(saved);
            Assert.Equal("Highlighter", loaded.ActiveTool);
            Assert.Equal("Both", loaded.SelectionTarget);
            Assert.Equal("#FF00BFFF", loaded.ColorHex);
            Assert.Equal(9, loaded.StrokeWidth);
            Assert.Equal(9, loaded.PenWidth);
            Assert.Equal(7, loaded.PencilWidth);
            Assert.Equal(22, loaded.HighlighterWidth);
            Assert.Equal(640, loaded.EraserWidth);
            Assert.Equal(0.35, loaded.PenSpeedVariation);
            Assert.Equal(0.7, loaded.PencilTextureVariation);
            Assert.False(loaded.PressureEnabled);
            Assert.Equal(AnnotationSpeedSizeMode.SlowThick.ToString(), loaded.SpeedSizeMode);
            Assert.True(loaded.SpeedSizeEnabled);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveAndLoadAllowsLargeEraserWithoutExpandingDrawingStroke()
    {
        var root = Path.Combine(Path.GetTempPath(), "QuickCut.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "annotation-settings.json");
        var store = new AnnotationSettingsStore(path);

        try
        {
            var saved = store.Save(new AnnotationSettings
            {
                PenWidth = 999,
                PencilWidth = 999,
                HighlighterWidth = 999,
                EraserWidth = 800,
                PenSpeedVariation = 99,
                PencilTextureVariation = -99,
            });

            var loaded = store.Load();

            Assert.True(saved);
            Assert.Equal(AnnotationSettings.MaximumStrokeWidth, loaded.StrokeWidth);
            Assert.Equal(AnnotationSettings.MaximumStrokeWidth, loaded.PenWidth);
            Assert.Equal(AnnotationSettings.MaximumStrokeWidth, loaded.PencilWidth);
            Assert.Equal(AnnotationSettings.MaximumStrokeWidth, loaded.HighlighterWidth);
            Assert.Equal(800, loaded.EraserWidth);
            Assert.Equal(AnnotationSettings.MaximumBrushVariation, loaded.PenSpeedVariation);
            Assert.Equal(AnnotationSettings.MinimumBrushVariation, loaded.PencilTextureVariation);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadMigratesLegacyStrokeWidthToPenWidth()
    {
        var root = Path.Combine(Path.GetTempPath(), "QuickCut.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "annotation-settings.json");
        var store = new AnnotationSettingsStore(path);

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, """{"StrokeWidth":11}""");

            var loaded = store.Load();

            Assert.Equal(11, loaded.StrokeWidth);
            Assert.Equal(11, loaded.PenWidth);
            Assert.Equal(AnnotationSettings.DefaultPencilWidth, loaded.PencilWidth);
            Assert.Equal(AnnotationSettings.DefaultHighlighterWidth, loaded.HighlighterWidth);
            Assert.Equal(AnnotationSettings.DefaultEraserWidth, loaded.EraserWidth);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadConvertsLegacyDisabledSpeedSizeToOffMode()
    {
        var root = Path.Combine(Path.GetTempPath(), "QuickCut.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "annotation-settings.json");
        var store = new AnnotationSettingsStore(path);

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, """{"SpeedSizeEnabled":false}""");

            var loaded = store.Load();

            Assert.Equal(AnnotationSpeedSizeMode.Off.ToString(), loaded.SpeedSizeMode);
            Assert.False(loaded.SpeedSizeEnabled);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
