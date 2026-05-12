using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using QuickCut.Capture.Services;

namespace QuickCut.Capture;

public enum CaptureActionToastMode
{
    SavedFile,
    ClipboardOnly,
}

public enum CaptureActionToastAction
{
    Annotate,
    OpenWith,
    RevealInFolder,
    SaveAs,
    Delete,
}

public sealed class CaptureActionToastWindow : Window
{
    private static readonly nint TopmostWindowHandle = new(-1);
    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionNoMove = 0x0002;
    private const uint SetWindowPositionShowWindow = 0x0040;

    private static readonly CaptureActionToastAction[] SavedFileActions =
    [
        CaptureActionToastAction.Annotate,
        CaptureActionToastAction.OpenWith,
        CaptureActionToastAction.RevealInFolder,
        CaptureActionToastAction.SaveAs,
        CaptureActionToastAction.Delete,
    ];

    private static readonly CaptureActionToastAction[] ClipboardOnlyActions = [];

    public static TimeSpan AutoCloseDelay { get; } = TimeSpan.FromSeconds(30);

    private readonly Action? _defaultAction;
    private readonly Action? _escapeAction;
    private bool _escapeActionEnabled;
    private readonly DispatcherTimer _autoCloseTimer = new()
    {
        Interval = AutoCloseDelay,
    };

    public CaptureActionToastWindow(
        string imagePath,
        Action annotate,
        Action openWith,
        Action revealInFolder,
        Action saveAs,
        Action delete)
        : this(
            CaptureActionToastMode.SavedFile,
            "Capture copiée et enregistrée",
            $"{imagePath}{Environment.NewLine}Échap maintenant : supprimer le fichier, garder l'image copiée.",
            annotate,
            openWith,
            revealInFolder,
            saveAs,
            delete)
    {
    }

    public CaptureActionToastWindow()
        : this(
            CaptureActionToastMode.ClipboardOnly,
            "Capture copiée dans le presse-papier",
            "Mode Maj : aucun fichier n'a été créé.",
            annotate: null,
            openWith: null,
            revealInFolder: null,
            saveAs: null,
            delete: null)
    {
    }

    private CaptureActionToastWindow(
        CaptureActionToastMode mode,
        string titleText,
        string bodyText,
        Action? annotate,
        Action? openWith,
        Action? revealInFolder,
        Action? saveAs,
        Action? delete)
    {
        _defaultAction = annotate;
        _escapeAction = mode == CaptureActionToastMode.SavedFile ? delete : null;
        _escapeActionEnabled = _escapeAction is not null;
        Title = "QuickCut";
        Width = 430;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        ShowInTaskbar = false;
        ShowActivated = true;
        Topmost = true;
        SetResourceReference(BackgroundProperty, QuickCutTheme.WindowBackgroundBrushKey);

        var root = new Grid { Margin = new Thickness(16) };
        root.SetResourceReference(Panel.BackgroundProperty, QuickCutTheme.WindowBackgroundBrushKey);
        root.PreviewMouseLeftButtonUp += OnRootPreviewMouseLeftButtonUp;
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = titleText,
            FontWeight = FontWeights.SemiBold,
            FontSize = 16,
        });

        var pathText = new TextBlock
        {
            Text = bodyText,
            Margin = new Thickness(0, 8, 0, 14),
            TextWrapping = TextWrapping.Wrap,
        };
        pathText.SetResourceReference(TextBlock.ForegroundProperty, QuickCutTheme.MutedTextBrushKey);
        Grid.SetRow(pathText, 1);
        root.Children.Add(pathText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        foreach (var action in GetAvailableActions(mode))
        {
            var button = action switch
            {
                CaptureActionToastAction.Annotate when annotate is not null => IconButton(
                    QuickCutIcon.Pen,
                    "Annoter",
                    "Ouvre l'aperçu QuickCut avec stylo, surligneur, gomme et sauvegarde.",
                    annotate),
                CaptureActionToastAction.OpenWith when openWith is not null => IconButton(
                    QuickCutIcon.OpenWith,
                    "Ouvrir avec",
                    "Choisit l'application avec laquelle ouvrir cette capture.",
                    openWith),
                CaptureActionToastAction.RevealInFolder when revealInFolder is not null => IconButton(
                    QuickCutIcon.RevealInFolder,
                    "Afficher dans le dossier",
                    "Ouvre le dossier de captures et sélectionne ce fichier.",
                    revealInFolder),
                CaptureActionToastAction.SaveAs when saveAs is not null => IconButton(
                    QuickCutIcon.SaveAs,
                    "Enregistrer sous",
                    "Copie cette capture vers un emplacement choisi.",
                    saveAs),
                CaptureActionToastAction.Delete when delete is not null => IconButton(
                    QuickCutIcon.Delete,
                    "Supprimer",
                    "Supprime le fichier PNG de cette capture. L'image copiée reste dans le presse-papier.",
                    delete),
                _ => null,
            };

            if (button is not null)
            {
                buttons.Children.Add(button);
            }
        }

        if (buttons.Children.Count > 0)
        {
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);
        }

        Content = root;
        Loaded += (_, _) =>
        {
            PlaceBottomRight();
            BringToastToFront();
            _autoCloseTimer.Start();
        };
        Deactivated += (_, _) => _escapeActionEnabled = false;
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += (_, _) => _autoCloseTimer.Stop();
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            Close();
        };
    }

    public static IReadOnlyList<CaptureActionToastAction> GetAvailableActions(CaptureActionToastMode mode) => mode switch
    {
        CaptureActionToastMode.SavedFile => SavedFileActions,
        CaptureActionToastMode.ClipboardOnly => ClipboardOnlyActions,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    private void OnRootPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        Close();
        _defaultAction?.Invoke();
        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        var action = _escapeActionEnabled ? _escapeAction : null;
        Close();
        action?.Invoke();
        e.Handled = true;
    }

    private Button IconButton(QuickCutIcon icon, string automationName, string tooltip, Action action)
    {
        var button = new Button
        {
            Content = QuickCutIconGlyphs.CreateViewbox(icon),
            Width = 42,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = tooltip,
            Focusable = false,
        };
        button.Click += (_, _) =>
        {
            Close();
            action();
        };
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private void PlaceBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 20;
        Top = workArea.Bottom - ActualHeight - 20;
    }

    private void BringToastToFront()
    {
        Topmost = false;
        Topmost = true;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != nint.Zero)
        {
            _ = SetWindowPos(
                handle,
                TopmostWindowHandle,
                0,
                0,
                0,
                0,
                SetWindowPositionNoMove | SetWindowPositionNoSize | SetWindowPositionShowWindow);
        }

        Activate();
        Focus();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
