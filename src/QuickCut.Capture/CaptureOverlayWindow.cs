using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using QuickCut.Capture.Services;

namespace QuickCut.Capture;

public sealed class CaptureOverlayWindow : Window
{
    private const byte IdleDimOpacity = 118;
    private const byte OutsideSelectionDimOpacity = 118;
    private const int MinimumSelectionSize = 3;

    private readonly Canvas _canvas = new()
    {
        Background = Brushes.Transparent,
    };

    private readonly Rectangle _selection = new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
        StrokeThickness = 1.5,
        Fill = new SolidColorBrush(Color.FromArgb(22, 125, 211, 252)),
        Visibility = Visibility.Collapsed,
    };

    private readonly Rectangle _idleDimOverlay = new()
    {
        Fill = new SolidColorBrush(Color.FromArgb(IdleDimOpacity, 0, 0, 0)),
        IsHitTestVisible = false,
    };

    private readonly Rectangle _outsideDimOverlay = new()
    {
        Fill = new SolidColorBrush(Color.FromArgb(OutsideSelectionDimOpacity, 0, 0, 0)),
        IsHitTestVisible = false,
    };

    private readonly Grid _outsideOverlay = new();
    private readonly Border _multiCaptureHud = new()
    {
        Visibility = Visibility.Collapsed,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 18, 0, 0),
        Padding = new Thickness(12, 9, 12, 9),
        CornerRadius = new CornerRadius(8),
        Background = new SolidColorBrush(Color.FromArgb(226, 12, 18, 28)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(180, 125, 211, 252)),
        BorderThickness = new Thickness(1),
    };

    private readonly TextBlock _multiCaptureStatus = new()
    {
        Foreground = Brushes.White,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 13,
        Margin = new Thickness(0, 0, 12, 0),
    };

    private readonly Button _finishMultiCaptureButton = new()
    {
        Content = "Terminer",
        MinWidth = 84,
        MinHeight = 30,
        Padding = new Thickness(12, 4, 12, 4),
        Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
        BorderThickness = new Thickness(1),
    };

    private readonly List<Int32Rect> _selectedRegions = [];
    private readonly double _dipWidth;
    private readonly double _dipHeight;
    private readonly double _pixelScaleX;
    private readonly double _pixelScaleY;
    private readonly int _pixelWidth;
    private readonly int _pixelHeight;
    private Point? _start;
    private bool _multiCaptureEnabled;

    public Int32Rect? SelectedRegion { get; private set; }

    public IReadOnlyList<Int32Rect> SelectedRegions => _selectedRegions;

    public bool ClipboardOnlyRequested { get; private set; }

    public bool CaptureFullScreenRequested { get; private set; }

    public CaptureOverlayWindow(Int32Rect pixelBounds)
    {
        var dipBounds = GetVirtualScreenDipBounds(pixelBounds);
        _pixelWidth = pixelBounds.Width;
        _pixelHeight = pixelBounds.Height;
        _pixelScaleX = _pixelWidth / Math.Max(1, dipBounds.Width);
        _pixelScaleY = _pixelHeight / Math.Max(1, dipBounds.Height);
        _dipWidth = dipBounds.Width;
        _dipHeight = dipBounds.Height;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = true;
        Cursor = Cursors.Cross;
        Background = Brushes.Transparent;
        Left = dipBounds.X;
        Top = dipBounds.Y;
        Width = dipBounds.Width;
        Height = dipBounds.Height;

        var grid = new Grid();

        _idleDimOverlay.Width = _dipWidth;
        _idleDimOverlay.Height = _dipHeight;
        grid.Children.Add(_idleDimOverlay);

        _outsideOverlay.Width = _dipWidth;
        _outsideOverlay.Height = _dipHeight;
        _outsideOverlay.IsHitTestVisible = false;
        _outsideOverlay.Visibility = Visibility.Collapsed;
        _outsideDimOverlay.Width = _dipWidth;
        _outsideDimOverlay.Height = _dipHeight;
        _outsideOverlay.Children.Add(_outsideDimOverlay);
        UpdateOutsideOverlayClip(selectionRegion: null);
        grid.Children.Add(_outsideOverlay);

        _canvas.Width = _dipWidth;
        _canvas.Height = _dipHeight;
        _canvas.Children.Add(_selection);
        grid.Children.Add(_canvas);

        _finishMultiCaptureButton.Click += (_, e) =>
        {
            e.Handled = true;
            FinishMultiCapture();
        };
        _multiCaptureHud.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                _multiCaptureStatus,
                _finishMultiCaptureButton,
            },
        };
        grid.Children.Add(_multiCaptureHud);
        Content = grid;

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => FocusOverlay();
        Activated += (_, _) => FocusOverlay();
        UpdateMultiCaptureStatus();
    }

    public void EnableMultiCaptureMode(bool clipboardOnlyRequested = false)
    {
        if (!CheckAccess())
        {
            Dispatcher.Invoke(() => EnableMultiCaptureMode(clipboardOnlyRequested));
            return;
        }

        ClipboardOnlyRequested |= clipboardOnlyRequested;
        _multiCaptureEnabled = true;
        _multiCaptureHud.Visibility = Visibility.Visible;
        UpdateMultiCaptureStatus();
        FocusOverlay();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsEventFromMultiCaptureHud(e.OriginalSource))
        {
            return;
        }

        _start = ClampPoint(e.GetPosition(this));
        _selection.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_start.Value, _start.Value);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_start is null)
        {
            return;
        }

        UpdateSelection(_start.Value, ClampPoint(e.GetPosition(this)));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _start is null)
        {
            return;
        }

        ReleaseMouseCapture();
        var end = ClampPoint(e.GetPosition(this));
        var selectionRegion = CreateSelectionRect(_start.Value, end);
        var region = CreateRegion(_start.Value, end);
        if (region.Width >= MinimumSelectionSize && region.Height >= MinimumSelectionSize)
        {
            if (_multiCaptureEnabled)
            {
                ClipboardOnlyRequested |= ModifierKeyState.IsShiftPressed();
                AddMultiCaptureRegion(region, selectionRegion);
                ResetActiveSelection();
                return;
            }

            ClipboardOnlyRequested = ModifierKeyState.IsShiftPressed();
            _selectedRegions.Add(region);
            SelectedRegion = region;
            CloseWithResult(true);
            return;
        }

        if (_multiCaptureEnabled)
        {
            ResetActiveSelection();
        }
        else
        {
            CloseWithResult(false);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelCapture();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            if (_multiCaptureEnabled && _selectedRegions.Count > 0)
            {
                FinishMultiCapture();
            }
            else
            {
                CaptureFullScreen();
            }

            e.Handled = true;
            return;
        }
    }

    private void UpdateSelection(Point start, Point end)
    {
        var region = CreateSelectionRect(start, end);

        Canvas.SetLeft(_selection, region.Left);
        Canvas.SetTop(_selection, region.Top);
        _selection.Width = region.Width;
        _selection.Height = region.Height;
        if (region.Width > 0.5 && region.Height > 0.5)
        {
            _idleDimOverlay.Visibility = Visibility.Collapsed;
            _outsideOverlay.Visibility = Visibility.Visible;
            UpdateOutsideOverlayClip(region);
        }
        else
        {
            _idleDimOverlay.Visibility = Visibility.Visible;
            _outsideOverlay.Visibility = Visibility.Collapsed;
            UpdateOutsideOverlayClip(selectionRegion: null);
        }
    }

    private Point ClampPoint(Point point)
    {
        var x = Math.Clamp(point.X, 0, ActualWidth > 0 ? ActualWidth : Width);
        var y = Math.Clamp(point.Y, 0, ActualHeight > 0 ? ActualHeight : Height);
        return new Point(x, y);
    }

    private Int32Rect CreateRegion(Point start, Point end)
    {
        return CaptureCoordinateMapper.CreatePixelRegion(
            start,
            end,
            _pixelScaleX,
            _pixelScaleY,
            _pixelWidth,
            _pixelHeight);
    }

    private static Rect CreateSelectionRect(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        return new Rect(left, top, width, height);
    }

    private static Rect GetVirtualScreenDipBounds(Int32Rect pixelBounds)
    {
        if (SystemParameters.VirtualScreenWidth > 0 && SystemParameters.VirtualScreenHeight > 0)
        {
            return new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
        }

        return new Rect(pixelBounds.X, pixelBounds.Y, pixelBounds.Width, pixelBounds.Height);
    }

    private void UpdateOutsideOverlayClip(Rect? selectionRegion)
    {
        var fullRegion = new RectangleGeometry(new Rect(0, 0, Width, Height));
        if (selectionRegion is not { Width: > 0, Height: > 0 } region)
        {
            _outsideOverlay.Clip = fullRegion;
            return;
        }

        var selectedRegion = new RectangleGeometry(region);
        _outsideOverlay.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, fullRegion, selectedRegion);
    }

    private void CloseWithResult(bool result)
    {
        DialogResult = result;
        Close();
    }

    private void AddMultiCaptureRegion(Int32Rect pixelRegion, Rect selectionRegion)
    {
        _selectedRegions.Add(pixelRegion);
        SelectedRegion = _selectedRegions.Count == 1 ? pixelRegion : null;

        var rectangle = new Rectangle
        {
            Width = selectionRegion.Width,
            Height = selectionRegion.Height,
            Stroke = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(20, 125, 211, 252)),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(rectangle, selectionRegion.Left);
        Canvas.SetTop(rectangle, selectionRegion.Top);

        var badge = new Border
        {
            MinWidth = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = _selectedRegions.Count.ToString(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12,
            },
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(badge, selectionRegion.Left + 6);
        Canvas.SetTop(badge, selectionRegion.Top + 6);

        var selectionIndex = _canvas.Children.IndexOf(_selection);
        _canvas.Children.Insert(Math.Max(0, selectionIndex), rectangle);
        _canvas.Children.Insert(Math.Max(0, selectionIndex + 1), badge);
        UpdateMultiCaptureStatus();
    }

    private void ResetActiveSelection()
    {
        _start = null;
        _selection.Visibility = Visibility.Collapsed;
        _idleDimOverlay.Visibility = Visibility.Visible;
        _outsideOverlay.Visibility = Visibility.Collapsed;
        UpdateOutsideOverlayClip(selectionRegion: null);
    }

    private void FinishMultiCapture()
    {
        if (!_multiCaptureEnabled || _selectedRegions.Count == 0)
        {
            return;
        }

        ClipboardOnlyRequested |= ModifierKeyState.IsShiftPressed();
        CloseWithResult(true);
    }

    private void CaptureFullScreen()
    {
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        _selectedRegions.Clear();
        var fullScreenRegion = new Int32Rect(0, 0, _pixelWidth, _pixelHeight);
        _selectedRegions.Add(fullScreenRegion);
        SelectedRegion = fullScreenRegion;
        CaptureFullScreenRequested = true;
        ClipboardOnlyRequested |= ModifierKeyState.IsShiftPressed();
        CloseWithResult(true);
    }

    private void CancelCapture()
    {
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        _selectedRegions.Clear();
        SelectedRegion = null;
        CloseWithResult(false);
    }

    private void UpdateMultiCaptureStatus()
    {
        var count = _selectedRegions.Count;
        if (count == 0)
        {
            _multiCaptureStatus.Text = "Multi-capture · Entrée capture l'écran entier · Échap annule";
            _finishMultiCaptureButton.IsEnabled = false;
            _finishMultiCaptureButton.Opacity = 0.55;
            return;
        }

        var label = count <= 1 ? "zone" : "zones";
        _multiCaptureStatus.Text = $"Multi-capture · {count} {label} · Entrée pour terminer · Échap annule";
        _finishMultiCaptureButton.IsEnabled = true;
        _finishMultiCaptureButton.Opacity = 1;
    }

    private void FocusOverlay()
    {
        Activate();
        Focus();
        Keyboard.Focus(this);
    }

    private bool IsEventFromMultiCaptureHud(object source)
    {
        return _multiCaptureHud.Visibility == Visibility.Visible
            && source is DependencyObject dependencyObject
            && IsDescendantOf(dependencyObject, _multiCaptureHud);
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
    {
        for (var current = source; current is not null;)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current)
                ?? LogicalTreeHelper.GetParent(current) as DependencyObject;
        }

        return false;
    }
}
