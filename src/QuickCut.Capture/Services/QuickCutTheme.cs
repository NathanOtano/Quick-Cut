using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace QuickCut.Capture.Services;

public static class QuickCutTheme
{
    public const string WindowBackgroundBrushKey = "QuickCut.WindowBackgroundBrush";
    public const string PanelBackgroundBrushKey = "QuickCut.PanelBackgroundBrush";
    public const string ControlBackgroundBrushKey = "QuickCut.ControlBackgroundBrush";
    public const string ControlHoverBackgroundBrushKey = "QuickCut.ControlHoverBackgroundBrush";
    public const string TextBrushKey = "QuickCut.TextBrush";
    public const string MutedTextBrushKey = "QuickCut.MutedTextBrush";
    public const string BorderBrushKey = "QuickCut.BorderBrush";

    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static void Apply(ResourceDictionary resources)
    {
        var isLight = IsWindowsAppThemeLight();

        resources[WindowBackgroundBrushKey] = Brush(isLight ? Colors.White : Colors.Black);
        resources[PanelBackgroundBrushKey] = Brush(isLight ? Colors.White : Colors.Black);
        resources[ControlBackgroundBrushKey] = Brush(isLight ? Color.FromRgb(245, 245, 245) : Color.FromRgb(24, 24, 27));
        resources[ControlHoverBackgroundBrushKey] = Brush(isLight ? Color.FromRgb(229, 229, 229) : Color.FromRgb(39, 39, 42));
        resources[TextBrushKey] = Brush(isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(248, 250, 252));
        resources[MutedTextBrushKey] = Brush(isLight ? Color.FromRgb(82, 82, 91) : Color.FromRgb(161, 161, 170));
        resources[BorderBrushKey] = Brush(isLight ? Color.FromRgb(212, 212, 216) : Color.FromRgb(63, 63, 70));
    }

    private static bool IsWindowsAppThemeLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
