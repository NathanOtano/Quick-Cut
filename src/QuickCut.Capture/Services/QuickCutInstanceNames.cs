using System.Security.Principal;

namespace QuickCut.Capture.Services;

internal static class QuickCutInstanceNames
{
    public static string MainMutexName => $@"Local\QuickCut.Capture.Main.{UserScope}";

    public static string MainPipeName => $"QuickCut.Capture.Main.{UserScope}";

    private static string UserScope =>
        Sanitize(WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName);

    private static string Sanitize(string value)
    {
        var characters = value
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_')
            .ToArray();
        return new string(characters);
    }
}
