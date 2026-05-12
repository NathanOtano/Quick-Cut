using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace QuickCut.Capture.Services;

public sealed class GlobalHotKeyService : IDisposable
{
    private const int HotKeyMessage = 0x0312;
    private const int PrintScreenVirtualKey = 0x2C;
    private const int Oem102VirtualKey = 0xE2;
    private const int AltModifier = 0x0001;
    private const int ControlModifier = 0x0002;
    private const int ShiftModifier = 0x0004;
    private const int WindowsModifier = 0x0008;
    private const int NoRepeatModifier = 0x4000;

    private readonly HwndSource _source;
    private readonly int _hotKeyId;
    private readonly Dictionary<int, RegisteredHotKey> _registeredHotKeys = [];

    public event EventHandler<GlobalHotKeyPressedEventArgs>? Pressed;

    public IReadOnlyList<string> RegisteredHotKeyLabels =>
        _registeredHotKeys
            .OrderBy(hotKey => hotKey.Key)
            .Select(hotKey => hotKey.Value.Label)
            .ToArray();

    public GlobalHotKeyService(nint windowHandle, int hotKeyId = 1)
    {
        _hotKeyId = hotKeyId;
        _source = HwndSource.FromHwnd(windowHandle)
            ?? throw new InvalidOperationException("Impossible d'attacher le raccourci global à la fenêtre.");
        _source.AddHook(OnWindowMessage);
    }

    public static IReadOnlyList<string> DefaultHotKeyLabels { get; } =
    [
        "Win + <",
        "AltGr + Impr. écran",
    ];

    public static string GetDefaultHotKeySummary() => string.Join(" ou ", DefaultHotKeyLabels);

    public bool TryRegisterDefaultHotKeys()
    {
        UnregisterAll();
        _ = TryRegister(_hotKeyId, WindowsModifier, Oem102VirtualKey, "Win + <", clipboardOnly: false);
        _ = TryRegister(_hotKeyId + 1, ControlModifier | AltModifier, PrintScreenVirtualKey, "AltGr + Impr. écran", clipboardOnly: false);
        _ = TryRegister(_hotKeyId + 2, WindowsModifier | ShiftModifier, Oem102VirtualKey, "Maj + Win + <", clipboardOnly: true);
        _ = TryRegister(_hotKeyId + 3, ControlModifier | AltModifier | ShiftModifier, PrintScreenVirtualKey, "Maj + AltGr + Impr. écran", clipboardOnly: true);
        return _registeredHotKeys.Count > 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(OnWindowMessage);
    }

    private nint OnWindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == HotKeyMessage && _registeredHotKeys.TryGetValue(wParam.ToInt32(), out var hotKey))
        {
            handled = true;
            Pressed?.Invoke(this, new GlobalHotKeyPressedEventArgs(hotKey.ClipboardOnly));
        }

        return nint.Zero;
    }

    private bool TryRegister(int id, int modifiers, int virtualKey, string label, bool clipboardOnly)
    {
        var registered = RegisterHotKey(_source.Handle, id, modifiers | NoRepeatModifier, virtualKey);
        if (registered)
        {
            _registeredHotKeys[id] = new RegisteredHotKey(label, clipboardOnly);
        }

        return registered;
    }

    private void UnregisterAll()
    {
        foreach (var id in _registeredHotKeys.Keys.ToArray())
        {
            _ = UnregisterHotKey(_source.Handle, id);
        }

        _registeredHotKeys.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    private sealed record RegisteredHotKey(string Label, bool ClipboardOnly);
}

public sealed class GlobalHotKeyPressedEventArgs(bool clipboardOnly) : EventArgs
{
    public bool ClipboardOnly { get; } = clipboardOnly;
}
