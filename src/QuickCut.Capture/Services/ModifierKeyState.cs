using System.Runtime.InteropServices;
using System.Windows.Input;

namespace QuickCut.Capture.Services;

public static class ModifierKeyState
{
    private const int VirtualKeyShift = 0x10;

    public static bool IsShiftPressed() =>
        Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
        || IsKeyPressed(VirtualKeyShift);

    private static bool IsKeyPressed(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}
