using System.Windows;
using Microsoft.Win32;
using QuickCut.Capture.Services;

namespace QuickCut.Capture;

public partial class App : Application
{
    private SingleInstanceLock? _mainInstanceLock;
    private SingleInstanceCommandServer? _commandServer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        QuickCutTheme.Apply(Resources);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        var launchOptions = QuickCutLaunchArguments.Parse(e.Args);

        _mainInstanceLock = new SingleInstanceLock(QuickCutInstanceNames.MainMutexName);
        if (!_mainInstanceLock.IsPrimaryInstance)
        {
            if (!launchOptions.HotKeyLauncher)
            {
                _ = SingleInstanceCommandServer.TrySend(QuickCutSingleInstanceCommand.FromLaunchOptions(launchOptions));
            }

            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;

        _commandServer = new SingleInstanceCommandServer();
        _commandServer.CommandReceived += command =>
            Dispatcher.Invoke(() => window.HandleExternalCommand(command));

        if (launchOptions.StartInTray)
        {
            window.StartInTray();
            if (launchOptions.StartCapture)
            {
                window.QueueCapture(launchOptions.ClipboardOnly);
            }

            return;
        }

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _commandServer?.Dispose();
        _mainInstanceLock?.Dispose();
        base.OnExit(e);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            Dispatcher.Invoke(() => QuickCutTheme.Apply(Resources));
        }
    }
}

