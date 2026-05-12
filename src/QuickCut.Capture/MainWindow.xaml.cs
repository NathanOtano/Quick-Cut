using System.Diagnostics;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QuickCut.Capture.Services;

namespace QuickCut.Capture;

public partial class MainWindow : Window
{
    private readonly CaptureImageWriter _imageWriter = new();
    private readonly StartupRegistrationService _startupRegistration = new();
    private TrayIconService? _trayIcon;
    private GlobalHotKeyService? _captureHotKey;
    private CaptureOverlayWindow? _activeCaptureOverlay;
    private string? _lastImagePath;
    private bool _isCapturing;
    private bool _isExiting;
    private bool _startupCheckboxReady;

    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = $"Prêt. Utilise {GlobalHotKeyService.GetDefaultHotKeySummary()} ou le bouton Capturer.";
        LastJobText.Text = $"Dossier : {_imageWriter.RootPath}";
        InitializeStartupCheckBox();
        SourceInitialized += OnSourceInitialized;
    }

    public void StartInTray()
    {
        _ = new WindowInteropHelper(this).EnsureHandle();
        EnsureRuntimeServices();
        Hide();
    }

    public void HandleExternalCommand(QuickCutSingleInstanceCommand command)
    {
        switch (command.Kind)
        {
            case QuickCutSingleInstanceCommandKind.Capture:
                QueueCapture(clipboardOnlyRequested: false);
                break;
            case QuickCutSingleInstanceCommandKind.CaptureClipboardOnly:
                QueueCapture(clipboardOnlyRequested: true);
                break;
            default:
                ShowMainWindow();
                break;
        }
    }

    public void QueueCapture(bool clipboardOnlyRequested)
    {
        _ = Dispatcher.InvokeAsync(
            async () => await StartOrExtendCaptureAsync(clipboardOnlyRequested),
            DispatcherPriority.Background);
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        await StartOrExtendCaptureAsync(ModifierKeyState.IsShiftPressed());
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        EnsureRuntimeServices();
    }

    private void EnsureRuntimeServices()
    {
        if (_trayIcon is not null || _captureHotKey is not null)
        {
            return;
        }

        _trayIcon = new TrayIconService();
        _trayIcon.CaptureRequested += async (_, _) => await StartOrExtendCaptureAsync(ModifierKeyState.IsShiftPressed());
        _trayIcon.ShowRequested += (_, _) => ShowMainWindow();
        _trayIcon.OpenCapturesRequested += (_, _) => OpenCapturesFolder();
        _trayIcon.ExitRequested += (_, _) => ExitApplication();

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            handle = new WindowInteropHelper(this).EnsureHandle();
        }

        _captureHotKey = new GlobalHotKeyService(handle);
        _captureHotKey.Pressed += async (_, e) => await StartOrExtendCaptureAsync(e.ClipboardOnly);

        if (_captureHotKey.TryRegisterDefaultHotKeys())
        {
            StatusText.Text = $"Prêt. Raccourci actif : {string.Join(" ou ", _captureHotKey.RegisteredHotKeyLabels)}.";
        }
        else
        {
            StatusText.Text = _startupRegistration.IsEnabled()
                ? "Prêt. Les raccourcis sont gérés par le lanceur Windows; le bouton Capturer fonctionne aussi."
                : "Prêt. Les raccourcis clavier sont indisponibles; le bouton Capturer fonctionne.";
        }
    }

    private void InitializeStartupCheckBox()
    {
        StartupCheckBox.IsChecked = _startupRegistration.IsEnabled();
        _startupCheckboxReady = true;
    }

    private async Task StartOrExtendCaptureAsync(bool clipboardOnlyRequested = false)
    {
        if (_activeCaptureOverlay is not null)
        {
            _activeCaptureOverlay.EnableMultiCaptureMode(clipboardOnlyRequested);
            StatusText.Text = "Multi-capture active : sélectionne les zones puis termine.";
            return;
        }

        await StartCaptureAsync(clipboardOnlyRequested);
    }

    private async Task StartCaptureAsync(bool clipboardOnlyRequested)
    {
        if (_isCapturing)
        {
            _activeCaptureOverlay?.EnableMultiCaptureMode(clipboardOnlyRequested);
            return;
        }

        _isCapturing = true;
        CaptureButton.IsEnabled = false;
        StatusText.Text = "Préparation de la capture...";

        try
        {
            var result = await CaptureAndStoreSelectionAsync(clipboardOnlyRequested);
            if (result is null)
            {
                StatusText.Text = "Capture annulée.";
                return;
            }

            if (result.ImagePath is null)
            {
                StatusText.Text = "Capture copiée dans le presse-papier.";
                _lastImagePath = null;
                OpenLastImageButton.IsEnabled = false;
                ShowClipboardOnlyCaptureActions();
                LastJobText.Text = "Mode Maj : aucune image enregistrée sur disque.";
                return;
            }

            StatusText.Text = "Capture copiée et enregistrée.";
            _lastImagePath = result.ImagePath;
            OpenLastImageButton.IsEnabled = true;
            ShowSavedCaptureActions(result.ImagePath);
            LastJobText.Text = $"Image : {result.ImagePath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur : {ex.Message}";
        }
        finally
        {
            CaptureButton.IsEnabled = true;
            _isCapturing = false;
        }
    }

    private async Task<CompletedCapture?> CaptureAndStoreSelectionAsync(bool clipboardOnlyRequested)
    {
        var capturedImage = await CaptureSelectionImageAsync();
        if (capturedImage is null)
        {
            return null;
        }

        Clipboard.SetImage(capturedImage.Image);
        if (clipboardOnlyRequested || capturedImage.ClipboardOnlyRequested)
        {
            return new CompletedCapture(ImagePath: null);
        }

        var savedCapture = await _imageWriter.SaveCaptureAsync(capturedImage.Image);
        return new CompletedCapture(savedCapture.ImagePath);
    }

    private async Task<CapturedSelection?> CaptureSelectionImageAsync(Window? windowToHide = null)
    {
        StatusText.Text = "Sélectionne une zone sur l'écran assombri...";
        var shouldRestoreMainWindow = IsVisible;
        var shouldRestoreRequestingWindow = windowToHide?.IsVisible == true;
        if (shouldRestoreRequestingWindow)
        {
            windowToHide!.Hide();
        }

        Hide();

        try
        {
            if (shouldRestoreMainWindow || shouldRestoreRequestingWindow)
            {
                await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            }

            var screenBounds = NativeScreenCapture.GetVirtualScreenBounds();
            var overlay = new CaptureOverlayWindow(screenBounds);
            _activeCaptureOverlay = overlay;
            var accepted = overlay.ShowDialog() == true;
            _activeCaptureOverlay = null;
            var selectedRegions = overlay.SelectedRegions.ToArray();
            if (!accepted || selectedRegions.Length == 0)
            {
                return null;
            }

            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);

            if (overlay.CaptureFullScreenRequested)
            {
                return new CapturedSelection(
                    NativeScreenCapture.CaptureVirtualScreen().Image,
                    overlay.ClipboardOnlyRequested);
            }

            if (selectedRegions.Length == 1)
            {
                return new CapturedSelection(
                    NativeScreenCapture.CaptureVirtualScreenRegion(selectedRegions[0]),
                    overlay.ClipboardOnlyRequested);
            }

            var capturedScreen = NativeScreenCapture.CaptureVirtualScreen();
            var captures = selectedRegions
                .Select(region => NativeScreenCapture.Crop(capturedScreen.Image, region))
                .ToArray();
            return new CapturedSelection(
                CaptureImageComposer.Compose(captures),
                overlay.ClipboardOnlyRequested);
        }
        finally
        {
            _activeCaptureOverlay = null;
            if (shouldRestoreMainWindow)
            {
                Show();
                Activate();
            }

            if (shouldRestoreRequestingWindow)
            {
                windowToHide!.Show();
                windowToHide.Activate();
            }
        }
    }

    private async Task<BitmapSource?> CaptureAdditionalImageForAnnotationAsync(Window annotationWindow)
    {
        if (_isCapturing)
        {
            _activeCaptureOverlay?.EnableMultiCaptureMode();
            return null;
        }

        _isCapturing = true;
        CaptureButton.IsEnabled = false;
        StatusText.Text = "Sélectionne la zone à ajouter à l'annotation...";

        try
        {
            return (await CaptureSelectionImageAsync(annotationWindow))?.Image;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ajout de capture impossible : {ex.Message}";
            return null;
        }
        finally
        {
            CaptureButton.IsEnabled = true;
            _isCapturing = false;
        }
    }

    private void OpenJobsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenCapturesFolder();
    }

    private void OpenImageButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_imageWriter.RootPath);
        var dialog = new OpenFileDialog
        {
            Title = "Ouvrir une image à annoter",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Tous les fichiers (*.*)|*.*",
            InitialDirectory = _imageWriter.RootPath,
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) == true)
        {
            _lastImagePath = dialog.FileName;
            OpenLastImageButton.IsEnabled = true;
            LastJobText.Text = $"Image : {dialog.FileName}";
            OpenAnnotationWindow(dialog.FileName);
        }
    }

    private void OpenCapturesFolder()
    {
        Directory.CreateDirectory(_imageWriter.RootPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = _imageWriter.RootPath,
            UseShellExecute = true,
        });
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
        Application.Current.Shutdown();
    }

    private void OpenLastImageButton_Click(object sender, RoutedEventArgs e)
    {
        OpenAnnotationWindow();
    }

    private void ShowSavedCaptureActions(string imagePath)
    {
        var toast = new CaptureActionToastWindow(
            imagePath,
            () => OpenAnnotationWindow(imagePath),
            () => OpenCaptureWith(imagePath),
            () => RevealCaptureInExplorer(imagePath),
            () => SaveCaptureAs(imagePath),
            () => DeleteCapture(imagePath));
        AttachToastOwner(toast);
        toast.Show();
    }

    private void ShowClipboardOnlyCaptureActions()
    {
        var toast = new CaptureActionToastWindow();
        AttachToastOwner(toast);
        toast.Show();
    }

    private void AttachToastOwner(Window toast)
    {
        var owner = OwnedWindows
            .OfType<Window>()
            .Where(window => window.IsVisible)
            .OrderByDescending(window => window.IsActive)
            .FirstOrDefault();
        if (owner is null && IsVisible)
        {
            owner = this;
        }

        if (owner is not null)
        {
            toast.Owner = owner;
        }
    }

    private void OpenAnnotationWindow()
    {
        if (string.IsNullOrWhiteSpace(_lastImagePath) || !File.Exists(_lastImagePath))
        {
            return;
        }

        OpenAnnotationWindow(_lastImagePath);
    }

    private void OpenAnnotationWindow(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            return;
        }

        var window = new AnnotationWindow(imagePath, CaptureAdditionalImageForAnnotationAsync, MarkCaptureDeleted)
        {
            Owner = this,
        };
        window.Show();
    }

    private void SaveLastImageAs()
    {
        if (string.IsNullOrWhiteSpace(_lastImagePath) || !File.Exists(_lastImagePath))
        {
            return;
        }

        SaveCaptureAs(_lastImagePath);
    }

    private void SaveCaptureAs(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            StatusText.Text = "Capture introuvable pour l'enregistrement.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Enregistrer la capture",
            Filter = "Image PNG (*.png)|*.png",
            FileName = Path.GetFileName(imagePath),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            AddExtension = true,
            DefaultExt = ".png",
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.Copy(imagePath, dialog.FileName, overwrite: true);
            LastJobText.Text = $"Image copiée vers : {dialog.FileName}";
        }
    }

    private void RevealCaptureInExplorer(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            StatusText.Text = "Capture introuvable dans le dossier.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{imagePath}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Dossier non ouvert : {ex.Message}";
        }
    }

    private void OpenCaptureWith(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            StatusText.Text = "Capture introuvable pour l'ouverture.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = imagePath,
                UseShellExecute = true,
                Verb = "openas",
            });
        }
        catch (Exception)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,OpenAs_RunDLL \"{imagePath}\"",
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ouvrir avec indisponible : {ex.Message}";
            }
        }
    }

    private void DeleteCapture(string imagePath)
    {
        try
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }

            MarkCaptureDeleted(imagePath);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Capture non supprimée : {ex.Message}";
        }
    }

    private void MarkCaptureDeleted(string imagePath)
    {
        if (string.Equals(_lastImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
        {
            _lastImagePath = null;
            OpenLastImageButton.IsEnabled = false;
            LastJobText.Text = $"Dossier : {_imageWriter.RootPath}";
        }

        StatusText.Text = "Capture supprimée. L'image copiée reste dans le presse-papier.";
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_startupCheckboxReady)
        {
            return;
        }

        try
        {
            _startupRegistration.SetEnabled(StartupCheckBox.IsChecked == true);
            if (StartupCheckBox.IsChecked == true)
            {
                _ = _startupRegistration.TryStartStartupInstance();
                StatusText.Text = "Démarrage Windows activé. QuickCut reste prêt dans la zone de notification.";
            }
            else
            {
                StatusText.Text = "Démarrage Windows désactivé.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Démarrage Windows non modifié : {ex.Message}";
            _startupCheckboxReady = false;
            StartupCheckBox.IsChecked = _startupRegistration.IsEnabled();
            _startupCheckboxReady = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            var result = MessageBox.Show(
                this,
                "Mettre QuickCut en zone de notification ?\n\nLes raccourcis et notifications resteront actifs. Choisis Non pour quitter complètement.",
                "Fermer QuickCut",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (result == MessageBoxResult.Yes)
            {
                e.Cancel = true;
                Hide();
                _trayIcon?.ShowStillRunningTip();
                return;
            }

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            _isExiting = true;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _captureHotKey?.Dispose();
        _trayIcon?.Dispose();
        base.OnClosed(e);

        if (_isExiting)
        {
            Application.Current.Shutdown();
        }
    }

    private sealed record CapturedSelection(BitmapSource Image, bool ClipboardOnlyRequested);

    private sealed record CompletedCapture(string? ImagePath);
}
