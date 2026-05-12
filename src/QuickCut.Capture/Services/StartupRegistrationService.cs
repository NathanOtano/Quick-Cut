using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace QuickCut.Capture.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickCut";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(ValueName) as string;
        var executablePath = GetExecutablePath();
        return string.Equals(value, BuildRunCommand(executablePath), StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, BuildHotKeyLauncherCommand(executablePath), StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, BuildTrayCommand(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(ValueName, BuildRunCommand(GetExecutablePath()), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public bool TryStartStartupInstance()
    {
        try
        {
            var executablePath = GetExecutablePath();
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = QuickCutLaunchArguments.TrayArgument,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string BuildRunCommand(string executablePath) => BuildTrayCommand(executablePath);

    public static string BuildHotKeyLauncherCommand(string executablePath) =>
        $"\"{executablePath}\" {QuickCutLaunchArguments.HotKeyLauncherArgument}";

    public static string BuildTrayCommand(string executablePath) =>
        $"\"{executablePath}\" {QuickCutLaunchArguments.TrayArgument}";

    private static string GetExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        return Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Impossible de résoudre le chemin de QuickCut.");
    }
}
