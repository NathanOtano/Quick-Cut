using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using QuickCut.Capture.Services;

namespace QuickCut.Capture;

public sealed class AnnotationWindow : Window
{
    private enum AnnotationTool
    {
        Pen,
        Pencil,
        Highlighter,
        Eraser,
        Select,
        Pan,
    }

    private enum SelectionTarget
    {
        Annotations,
        Image,
        Both,
    }

    private enum ViewMode
    {
        ActualSize,
        FitWindow,
        Custom,
    }

    private readonly string _imagePath;
    private readonly Func<Window, Task<BitmapSource?>>? _captureAdditionalImageAsync;
    private readonly Action<string>? _captureDeleted;
    private BitmapSource _baseImage;
    private readonly Image _baseImageElement = new()
    {
        Stretch = Stretch.Fill,
    };
    private readonly AnnotationSettingsStore _settingsStore = new();
    private readonly Canvas _viewport = new()
    {
        ClipToBounds = true,
        Focusable = true,
    };
    private readonly Grid _viewHost = new();
    private readonly MatrixTransform _viewTransform = new();
    private readonly Canvas _surface = new();
    private readonly InkCanvas _inkCanvas = new();
    private readonly Dictionary<AnnotationTool, Button> _toolButtons = [];
    private readonly Dictionary<SelectionTarget, Button> _selectionTargetButtons = [];
    private readonly Dictionary<UIElement, Point> _imagePatchDragOrigins = [];
    private readonly StrokeCollection _imagePatchDragStrokes = [];
    private readonly Stack<StrokeCollection> _redoStrokeGroups = new();
    private readonly Rectangle _selectionPreview = new()
    {
        Stroke = Brushes.White,
        StrokeThickness = 1.5,
        StrokeDashArray = [4, 3],
        Fill = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)),
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
    };

    private AnnotationTool _activeTool = AnnotationTool.Pen;
    private SelectionTarget _selectionTarget = SelectionTarget.Annotations;
    private Color _currentColor = Colors.Red;
    private double _currentPenWidth = AnnotationSettings.DefaultPenWidth;
    private double _currentPencilWidth = AnnotationSettings.DefaultPencilWidth;
    private double _currentHighlighterWidth = AnnotationSettings.DefaultHighlighterWidth;
    private double _currentEraserWidth = AnnotationSettings.DefaultEraserWidth;
    private double _currentPenSpeedVariation = AnnotationSettings.DefaultPenSpeedVariation;
    private double _currentPencilTextureVariation = AnnotationSettings.DefaultPencilTextureVariation;
    private bool _pressureEnabled = true;
    private AnnotationSpeedSizeMode _speedSizeMode = AnnotationSpeedSizeMode.FastThick;
    private Slider? _strokeWidthSlider;
    private Slider? _brushVariationSlider;
    private TextBlock? _brushVariationText;
    private CheckBox? _pressureCheckBox;
    private Button? _speedSizeButton;
    private Point? _selectionStart;
    private Point _pan = new(0, 0);
    private Point _panOrigin;
    private Point _panStart;
    private double _zoom = 1;
    private double _rotationDegrees;
    private ViewMode _viewMode = ViewMode.FitWindow;
    private bool _isPanning;
    private bool _spacePanActive;
    private bool _isAddingCapture;
    private bool _isUpdatingStrokeWidthSlider;
    private bool _isUpdatingBrushVariationSlider;
    private bool _isDraggingImagePatch;
    private UIElement? _draggedImagePatch;
    private Point _imagePatchDragStart;
    private Vector _imagePatchStrokeDelta;
    private double _frameWidth;
    private double _frameHeight;
    private double _imageLeft;
    private double _imageTop;

    private const double MinimumZoom = 0.02;
    private const double MaximumZoom = 8;
    private const double ZoomStep = 1.15;
    private const double FitPadding = 2;
    private const double MaximumEraserScreenRatio = 0.4;
    private static readonly Guid StrokeGroupPropertyId = new("75fdbf60-697f-4569-9e0e-5a70e38a8be4");

    public AnnotationWindow(
        string imagePath,
        Func<Window, Task<BitmapSource?>>? captureAdditionalImageAsync = null,
        Action<string>? captureDeleted = null)
    {
        _imagePath = imagePath;
        _captureAdditionalImageAsync = captureAdditionalImageAsync;
        _captureDeleted = captureDeleted;
        _baseImage = LoadBitmap(imagePath);
        LoadAnnotationSettings();

        Title = "QuickCut - annotation";
        MinWidth = 960;
        MinHeight = 660;
        Width = 1180;
        Height = 800;
        SetResourceReference(BackgroundProperty, QuickCutTheme.WindowBackgroundBrushKey);

        Content = BuildLayout();
        Loaded += (_, _) => FitFrameToWindowAfterLayout();
        Closed += (_, _) => SaveAnnotationSettings();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        ApplyInitialTool();
    }

    private void LoadAnnotationSettings()
    {
        var settings = _settingsStore.Load();
        _activeTool = ParseEnum(settings.ActiveTool, AnnotationTool.Pen);
        _selectionTarget = ParseEnum(settings.SelectionTarget, SelectionTarget.Annotations);
        _currentColor = ParseColor(settings.ColorHex);
        _currentPenWidth = settings.PenWidth;
        _currentPencilWidth = settings.PencilWidth;
        _currentHighlighterWidth = settings.HighlighterWidth;
        _currentEraserWidth = settings.EraserWidth;
        _currentPenSpeedVariation = settings.PenSpeedVariation;
        _currentPencilTextureVariation = settings.PencilTextureVariation;
        _pressureEnabled = settings.PressureEnabled;
        _speedSizeMode = ParseEnum(settings.SpeedSizeMode, AnnotationSpeedSizeMode.FastThick);
    }

    private void SaveAnnotationSettings()
    {
        _ = _settingsStore.Save(new AnnotationSettings
        {
            ActiveTool = _activeTool.ToString(),
            SelectionTarget = _selectionTarget.ToString(),
            ColorHex = ColorToHex(_currentColor),
            StrokeWidth = _currentPenWidth,
            PenWidth = _currentPenWidth,
            PencilWidth = _currentPencilWidth,
            HighlighterWidth = _currentHighlighterWidth,
            EraserWidth = _currentEraserWidth,
            PenSpeedVariation = _currentPenSpeedVariation,
            PencilTextureVariation = _currentPencilTextureVariation,
            PressureEnabled = _pressureEnabled,
            SpeedSizeMode = _speedSizeMode.ToString(),
            SpeedSizeEnabled = _speedSizeMode != AnnotationSpeedSizeMode.Off,
        });
    }

    private void ApplyInitialTool()
    {
        switch (_activeTool)
        {
            case AnnotationTool.Highlighter:
                SetHighlighter();
                break;
            case AnnotationTool.Pencil:
                SetPencil();
                break;
            case AnnotationTool.Eraser:
                SetEraser();
                break;
            case AnnotationTool.Select:
                SetSelectMode();
                break;
            case AnnotationTool.Pan:
                SetPanMode();
                break;
            default:
                SetPen();
                break;
        }
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    private static Color ParseColor(string value)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }
        catch
        {
            return Colors.Red;
        }
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private DockPanel BuildLayout()
    {
        var root = new DockPanel();
        root.SetResourceReference(Panel.BackgroundProperty, QuickCutTheme.WindowBackgroundBrushKey);
        root.Children.Add(BuildToolbar());

        ResetFrameToBaseImage();
        _surface.Background = Brushes.Transparent;
        _surface.SnapsToDevicePixels = true;
        _baseImageElement.Source = _baseImage;
        _surface.Children.Add(_baseImageElement);

        _inkCanvas.Background = Brushes.Transparent;
        _inkCanvas.SnapsToDevicePixels = true;
        _inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        _inkCanvas.PreviewMouseDown += OnInkCanvasPreviewMouseDown;
        _inkCanvas.PreviewMouseMove += OnInkCanvasPreviewMouseMove;
        _inkCanvas.PreviewMouseUp += OnInkCanvasPreviewMouseUp;
        _inkCanvas.StrokeCollected += OnInkCanvasStrokeCollected;
        _inkCanvas.SelectionMoved += (_, _) => OnSelectionTransformed();
        _inkCanvas.SelectionResized += (_, _) => OnSelectionTransformed();
        _inkCanvas.Children.Add(_selectionPreview);
        _surface.Children.Add(_inkCanvas);

        _viewHost.RenderTransform = _viewTransform;
        _viewHost.SnapsToDevicePixels = true;
        _viewHost.Children.Add(_surface);
        ApplyFrameGeometry();

        _viewport.Children.Add(_viewHost);
        _viewport.PreviewMouseWheel += OnViewportPreviewMouseWheel;
        _viewport.PreviewMouseDown += OnViewportPreviewMouseDown;
        _viewport.PreviewMouseMove += OnViewportPreviewMouseMove;
        _viewport.PreviewMouseUp += OnViewportPreviewMouseUp;
        _viewport.SizeChanged += (_, _) =>
        {
            ApplyCurrentViewMode();
        };
        _viewport.SetResourceReference(Panel.BackgroundProperty, QuickCutTheme.WindowBackgroundBrushKey);
        root.Children.Add(_viewport);
        return root;
    }

    private void ResetFrameToBaseImage()
    {
        _frameWidth = Math.Max(1, _baseImage.PixelWidth);
        _frameHeight = Math.Max(1, _baseImage.PixelHeight);
        _imageLeft = 0;
        _imageTop = 0;
    }

    private void ApplyFrameGeometry()
    {
        _frameWidth = Math.Max(1, _frameWidth);
        _frameHeight = Math.Max(1, _frameHeight);

        _surface.Width = _frameWidth;
        _surface.Height = _frameHeight;
        _inkCanvas.Width = _frameWidth;
        _inkCanvas.Height = _frameHeight;
        _viewHost.Width = _frameWidth;
        _viewHost.Height = _frameHeight;

        _baseImageElement.Width = _baseImage.PixelWidth;
        _baseImageElement.Height = _baseImage.PixelHeight;
        Canvas.SetLeft(_baseImageElement, _imageLeft);
        Canvas.SetTop(_baseImageElement, _imageTop);
        Canvas.SetLeft(_inkCanvas, 0);
        Canvas.SetTop(_inkCanvas, 0);
    }

    private UIElement BuildToolbar()
    {
        var toolbar = new WrapPanel
        {
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(toolbar, Dock.Top);

        toolbar.Children.Add(ToolButton(AnnotationTool.Pen, QuickCutIcon.Pen, SetPen, "Stylo (P) : dessine avec la couleur et la taille actives."));
        toolbar.Children.Add(ToolButton(AnnotationTool.Pencil, QuickCutIcon.Pencil, SetPencil, "Crayon (C) : dessine un trait texturé avec variation de taille."));
        toolbar.Children.Add(ToolButton(AnnotationTool.Highlighter, QuickCutIcon.Highlighter, SetHighlighter, "Surligneur (L) : surligne avec la couleur et la taille actives."));
        toolbar.Children.Add(ToolButton(AnnotationTool.Eraser, QuickCutIcon.Eraser, SetEraser, "Gomme (E) : efface seulement les traits d'annotation."));
        toolbar.Children.Add(ToolButton(AnnotationTool.Select, QuickCutIcon.Select, SetSelectMode, "Sélection (V) : transforme annotations, image ou les deux."));
        toolbar.Children.Add(ToolButton(AnnotationTool.Pan, QuickCutIcon.Hand, SetPanMode, "Main (H ou espace maintenu) : déplace la capture dans la fenêtre."));
        toolbar.Children.Add(BuildSelectionTargetPicker());
        toolbar.Children.Add(IconButton(QuickCutIcon.Delete, DeleteSelection, "Supprimer la sélection (Suppr).", "Supprimer la sélection"));
        toolbar.Children.Add(IconButton(QuickCutIcon.ClearAnnotations, ClearAllAnnotations, "Supprimer toutes les annotations sans modifier l'image de base (Ctrl + Suppr).", "Supprimer toutes les annotations"));
        toolbar.Children.Add(Spacer());

        toolbar.Children.Add(IconLabel(QuickCutIcon.Palette, "Couleur active."));
        toolbar.Children.Add(ColorButton(Colors.Red, "Rouge"));
        toolbar.Children.Add(ColorButton(Colors.Yellow, "Jaune"));
        toolbar.Children.Add(ColorButton(Colors.DeepSkyBlue, "Bleu"));
        toolbar.Children.Add(ColorButton(Colors.LimeGreen, "Vert"));
        toolbar.Children.Add(ColorButton(Colors.Black, "Noir"));
        toolbar.Children.Add(ColorButton(Colors.White, "Blanc"));
        toolbar.Children.Add(Spacer());

        toolbar.Children.Add(IconLabel(QuickCutIcon.StrokeWidth, "Taille de l'outil actif."));
        _strokeWidthSlider = new Slider
        {
            Minimum = AnnotationSettings.MinimumStrokeWidth,
            Maximum = GetActiveToolMaximumStrokeWidth(),
            Value = GetActiveToolStrokeWidth(),
            Width = 150,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = GetActiveToolStrokeWidthTooltip(),
        };
        _strokeWidthSlider.ValueChanged += (_, _) =>
        {
            if (!_isUpdatingStrokeWidthSlider)
            {
                SetStrokeWidth(_strokeWidthSlider.Value);
            }
        };
        toolbar.Children.Add(_strokeWidthSlider);

        toolbar.Children.Add(IconLabel(QuickCutIcon.BrushVariation, "Vitesse du stylo ou texture du crayon selon l'outil actif."));
        _brushVariationText = new TextBlock
        {
            Text = GetActiveToolBrushVariationLabel(),
            MinWidth = 58,
            Margin = new Thickness(0, 7, 8, 0),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = GetActiveToolBrushVariationTooltip(),
        };
        _brushVariationText.SetResourceReference(TextBlock.ForegroundProperty, QuickCutTheme.MutedTextBrushKey);
        toolbar.Children.Add(_brushVariationText);
        _brushVariationSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = GetActiveToolBrushVariation() * 100,
            Width = 120,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = GetActiveToolBrushVariationTooltip(),
        };
        AutomationProperties.SetName(_brushVariationSlider, GetActiveToolBrushVariationAutomationName());
        _brushVariationSlider.ValueChanged += (_, _) =>
        {
            if (!_isUpdatingBrushVariationSlider)
            {
                SetBrushVariation(_brushVariationSlider.Value / 100d);
            }
        };
        toolbar.Children.Add(_brushVariationSlider);

        _pressureCheckBox = new CheckBox
        {
            Content = QuickCutIconGlyphs.CreateViewbox(QuickCutIcon.Pressure),
            IsChecked = _pressureEnabled,
            Margin = new Thickness(0, 7, 10, 0),
            ToolTip = "Utilise la pression fournie par Windows Ink ou le pilote Wacom quand elle est disponible.",
        };
        AutomationProperties.SetName(_pressureCheckBox, "Pression stylet");
        _pressureCheckBox.Checked += (_, _) => SetPressureEnabled(true);
        _pressureCheckBox.Unchecked += (_, _) => SetPressureEnabled(false);
        toolbar.Children.Add(_pressureCheckBox);

        _speedSizeButton = IconButton(
            QuickCutIcon.Speed,
            CycleSpeedSizeMode,
            GetSpeedSizeTooltip(),
            GetSpeedSizeAutomationName());
        toolbar.Children.Add(_speedSizeButton);
        toolbar.Children.Add(Spacer());

        toolbar.Children.Add(IconButton(QuickCutIcon.ZoomOut, () => ZoomBy(1 / ZoomStep), "Zoom arrière (Ctrl + -).", "Zoom arrière"));
        toolbar.Children.Add(IconButton(QuickCutIcon.ZoomIn, () => ZoomBy(ZoomStep), "Zoom avant (Ctrl + +).", "Zoom avant"));
        toolbar.Children.Add(IconButton(QuickCutIcon.ActualSize, ShowCaptureActualSize, "Afficher la capture en taille réelle, 1 pixel image = 1 pixel écran (Ctrl + 0).", "Taille réelle"));
        toolbar.Children.Add(IconButton(QuickCutIcon.Fit, FitFrameToWindow, "Adapter tout le cadre à la fenêtre.", "Adapter à la fenêtre"));
        toolbar.Children.Add(IconButton(QuickCutIcon.RotateLeft, () => RotateView(-90), "Rotation à gauche (Ctrl + Maj + R).", "Rotation à gauche"));
        toolbar.Children.Add(IconButton(QuickCutIcon.RotateRight, () => RotateView(90), "Rotation à droite (Ctrl + R).", "Rotation à droite"));
        toolbar.Children.Add(Spacer());

        var addCaptureButton = IconButton(
            QuickCutIcon.Capture,
            () => _ = AddCaptureToCurrentAnnotationAsync(),
            "Ajouter une nouvelle capture à cette image.",
            "Ajouter une capture");
        addCaptureButton.IsEnabled = _captureAdditionalImageAsync is not null;
        addCaptureButton.Opacity = _captureAdditionalImageAsync is not null ? 1 : 0.45;
        toolbar.Children.Add(addCaptureButton);
        toolbar.Children.Add(IconButton(QuickCutIcon.Undo, UndoLastStroke, "Annuler le dernier trait (Ctrl + Z).", "Annuler"));
        toolbar.Children.Add(IconButton(QuickCutIcon.Redo, RedoLastStroke, "Rétablir le dernier trait annulé (Ctrl + Y).", "Rétablir"));
        toolbar.Children.Add(IconButton(QuickCutIcon.Copy, CopyAnnotatedImage, "Copier l'image annotée (Ctrl + C).", "Copier"));
        toolbar.Children.Add(IconButton(QuickCutIcon.Save, SaveAnnotatedImage, "Enregistrer sur cette capture (Ctrl + S).", "Enregistrer"));
        toolbar.Children.Add(IconButton(QuickCutIcon.SaveAs, SaveAnnotatedImageAs, "Enregistrer sous (Ctrl + Maj + S).", "Enregistrer sous"));
        toolbar.Children.Add(IconButton(QuickCutIcon.Delete, DeleteCaptureFromDisk, "Supprimer cette capture du disque.", "Supprimer la capture du disque"));

        return toolbar;
    }

    private UIElement BuildSelectionTargetPicker()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 8, 0),
        };

        panel.Children.Add(SelectionTargetButton(SelectionTarget.Annotations, QuickCutIcon.AnnotationTarget, "Sélectionner seulement les annotations."));
        panel.Children.Add(SelectionTargetButton(SelectionTarget.Image, QuickCutIcon.ImageTarget, "Sélectionner une copie transformable de l'image."));
        panel.Children.Add(SelectionTargetButton(SelectionTarget.Both, QuickCutIcon.BothTargets, "Sélectionner annotations et image ensemble."));
        return panel;
    }

    private Button SelectionTargetButton(SelectionTarget target, QuickCutIcon icon, string tooltip)
    {
        var button = IconButton(icon, () => SetSelectionTarget(target), tooltip, tooltip);
        _selectionTargetButtons[target] = button;
        return button;
    }

    private Button ColorButton(Color color, string label)
    {
        var button = new Button
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(0, 3, 6, 3),
            Background = new SolidColorBrush(color),
            BorderBrush = new SolidColorBrush(Color.FromRgb(68, 64, 60)),
            ToolTip = $"Couleur : {label}.",
        };
        button.SetResourceReference(Control.BorderBrushProperty, QuickCutTheme.BorderBrushKey);
        button.Click += (_, _) =>
        {
            _currentColor = color;
            ApplyDrawingAttributes();
            SaveAnnotationSettings();
        };
        return button;
    }

    private static UIElement IconLabel(QuickCutIcon icon, string tooltip)
    {
        var label = new Border
        {
            Child = QuickCutIconGlyphs.CreateViewbox(icon),
            Width = 30,
            Height = 34,
            Margin = new Thickness(0, 0, 6, 6),
            ToolTip = tooltip,
        };
        return label;
    }

    private Button ToolButton(AnnotationTool tool, QuickCutIcon icon, Action action, string tooltip)
    {
        var button = IconButton(icon, action, tooltip, tooltip);
        _toolButtons[tool] = button;
        return button;
    }

    private static Button IconButton(QuickCutIcon icon, Action action, string tooltip, string automationName)
    {
        var button = new Button
        {
            Content = QuickCutIconGlyphs.CreateViewbox(icon),
            Width = 38,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 6),
            ToolTip = tooltip,
            Focusable = false,
        };
        AutomationProperties.SetName(button, automationName);
        button.Click += (_, _) => action();
        return button;
    }

    private static Border Spacer()
    {
        var spacer = new Border
        {
            Width = 1,
            Height = 28,
            Margin = new Thickness(8, 2, 16, 6),
        };
        spacer.SetResourceReference(Border.BackgroundProperty, QuickCutTheme.BorderBrushKey);
        return spacer;
    }

    private void SetPen()
    {
        _activeTool = AnnotationTool.Pen;
        HideSelectionPreview();
        StopPan();
        UpdateStrokeWidthSliderForActiveTool();
        UpdateBrushVariationSliderForActiveTool();
        _inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        ApplyDrawingAttributes();
        UpdateToolbarState();
        SaveAnnotationSettings();
    }

    private void SetPencil()
    {
        _activeTool = AnnotationTool.Pencil;
        HideSelectionPreview();
        StopPan();
        UpdateStrokeWidthSliderForActiveTool();
        UpdateBrushVariationSliderForActiveTool();
        _inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        ApplyDrawingAttributes();
        UpdateToolbarState();
        SaveAnnotationSettings();
    }

    private void SetHighlighter()
    {
        _activeTool = AnnotationTool.Highlighter;
        HideSelectionPreview();
        StopPan();
        UpdateStrokeWidthSliderForActiveTool();
        UpdateBrushVariationSliderForActiveTool();
        _inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        ApplyDrawingAttributes();
        UpdateToolbarState();
        SaveAnnotationSettings();
    }

    private void SetEraser()
    {
        _activeTool = AnnotationTool.Eraser;
        HideSelectionPreview();
        StopPan();
        ClearSelection();
        UpdateStrokeWidthSliderForActiveTool();
        UpdateBrushVariationSliderForActiveTool();
        ApplyEraserShape();
        _inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
        UpdateToolbarState();
        SaveAnnotationSettings();
    }

    private void SetSelectMode()
    {
        _activeTool = AnnotationTool.Select;
        HideSelectionPreview();
        StopPan();
        ApplySelectionMode();
        UpdateBrushVariationSliderForActiveTool();
        UpdateToolbarState();
        SaveAnnotationSettings();
    }

    private void SetPanMode()
    {
        _activeTool = AnnotationTool.Pan;
        HideSelectionPreview();
        ClearSelection();
        _inkCanvas.EditingMode = InkCanvasEditingMode.None;
        UpdateBrushVariationSliderForActiveTool();
        UpdateToolbarState();
        SaveAnnotationSettings();
    }

    private void SetSelectionTarget(SelectionTarget target)
    {
        _selectionTarget = target;
        if (_activeTool == AnnotationTool.Select)
        {
            ApplySelectionMode();
        }

        UpdateToolbarState();
        SaveAnnotationSettings();
    }

    private void ApplySelectionMode()
    {
        _inkCanvas.EditingMode = _selectionTarget == SelectionTarget.Annotations
            ? InkCanvasEditingMode.Select
            : InkCanvasEditingMode.None;
    }

    private void ApplyDrawingAttributes()
    {
        if (_activeTool is not (AnnotationTool.Pen or AnnotationTool.Pencil or AnnotationTool.Highlighter))
        {
            return;
        }

        var speedSizeEnabled = _activeTool == AnnotationTool.Pen
            && _speedSizeMode != AnnotationSpeedSizeMode.Off
            && _currentPenSpeedVariation > 0;
        var pencilTextureEnabled = _activeTool == AnnotationTool.Pencil
            && _currentPencilTextureVariation > 0;

        _inkCanvas.DefaultDrawingAttributes = AnnotationDrawingAttributesFactory.Create(
            _currentColor,
            GetActiveToolStrokeWidth(),
            _activeTool == AnnotationTool.Highlighter,
            _pressureEnabled,
            speedSizeEnabled || pencilTextureEnabled);
    }

    private void SetStrokeWidth(double width)
    {
        switch (_activeTool)
        {
            case AnnotationTool.Eraser:
                _currentEraserWidth = Math.Clamp(
                    width,
                    AnnotationSettings.MinimumStrokeWidth,
                    GetActiveToolMaximumStrokeWidth());
                break;
            case AnnotationTool.Highlighter:
                _currentHighlighterWidth = Math.Clamp(
                    width,
                    AnnotationSettings.MinimumStrokeWidth,
                    AnnotationSettings.MaximumStrokeWidth);
                break;
            case AnnotationTool.Pencil:
                _currentPencilWidth = Math.Clamp(
                    width,
                    AnnotationSettings.MinimumStrokeWidth,
                    AnnotationSettings.MaximumStrokeWidth);
                break;
            case AnnotationTool.Pen:
                _currentPenWidth = Math.Clamp(
                    width,
                    AnnotationSettings.MinimumStrokeWidth,
                    AnnotationSettings.MaximumStrokeWidth);
                break;
            default:
                return;
        }

        UpdateStrokeWidthSliderForActiveTool();

        if (_activeTool == AnnotationTool.Eraser)
        {
            ApplyEraserShape();
        }

        ApplyDrawingAttributes();
        SaveAnnotationSettings();
    }

    private double GetActiveToolStrokeWidth()
    {
        return _activeTool switch
        {
            AnnotationTool.Eraser => _currentEraserWidth,
            AnnotationTool.Highlighter => _currentHighlighterWidth,
            AnnotationTool.Pencil => _currentPencilWidth,
            _ => _currentPenWidth,
        };
    }

    private double GetActiveToolMaximumStrokeWidth()
    {
        if (_activeTool != AnnotationTool.Eraser)
        {
            return AnnotationSettings.MaximumStrokeWidth;
        }

        var screenSpan = Math.Max(SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        if (double.IsNaN(screenSpan) || double.IsInfinity(screenSpan) || screenSpan <= 0)
        {
            return AnnotationSettings.MaximumStrokeWidth;
        }

        return Math.Clamp(
            screenSpan * MaximumEraserScreenRatio,
            AnnotationSettings.MaximumStrokeWidth,
            AnnotationSettings.MaximumStoredEraserWidth);
    }

    private string GetActiveToolStrokeWidthTooltip()
    {
        return _activeTool switch
        {
            AnnotationTool.Eraser => "Diamètre de la gomme. Le maximum atteint environ 40 % du grand côté de l'écran.",
            AnnotationTool.Highlighter => "Épaisseur du surligneur.",
            AnnotationTool.Pencil => "Épaisseur moyenne du crayon.",
            _ => "Épaisseur du stylo.",
        };
    }

    private void UpdateStrokeWidthSliderForActiveTool()
    {
        if (_strokeWidthSlider is null)
        {
            return;
        }

        var maximum = GetActiveToolMaximumStrokeWidth();
        var value = Math.Clamp(
            GetActiveToolStrokeWidth(),
            AnnotationSettings.MinimumStrokeWidth,
            maximum);
        switch (_activeTool)
        {
            case AnnotationTool.Eraser:
                _currentEraserWidth = value;
                break;
            case AnnotationTool.Highlighter:
                _currentHighlighterWidth = value;
                break;
            case AnnotationTool.Pencil:
                _currentPencilWidth = value;
                break;
            case AnnotationTool.Pen:
                _currentPenWidth = value;
                break;
        }

        _isUpdatingStrokeWidthSlider = true;
        try
        {
            _strokeWidthSlider.Maximum = maximum;
            _strokeWidthSlider.Value = value;
            _strokeWidthSlider.ToolTip = GetActiveToolStrokeWidthTooltip();
        }
        finally
        {
            _isUpdatingStrokeWidthSlider = false;
        }
    }

    private void ApplyEraserShape()
    {
        _inkCanvas.EraserShape = new EllipseStylusShape(_currentEraserWidth, _currentEraserWidth);
    }

    private void SetBrushVariation(double variation)
    {
        var value = Math.Clamp(
            variation,
            AnnotationSettings.MinimumBrushVariation,
            AnnotationSettings.MaximumBrushVariation);
        switch (_activeTool)
        {
            case AnnotationTool.Pen:
                _currentPenSpeedVariation = value;
                break;
            case AnnotationTool.Pencil:
                _currentPencilTextureVariation = value;
                break;
            default:
                return;
        }

        UpdateBrushVariationSliderForActiveTool();
        ApplyDrawingAttributes();
        SaveAnnotationSettings();
    }

    private double GetActiveToolBrushVariation()
    {
        return _activeTool switch
        {
            AnnotationTool.Pencil => _currentPencilTextureVariation,
            AnnotationTool.Pen => _currentPenSpeedVariation,
            _ => 0,
        };
    }

    private bool ActiveToolHasBrushVariation()
    {
        return _activeTool is AnnotationTool.Pen or AnnotationTool.Pencil;
    }

    private string GetActiveToolBrushVariationTooltip()
    {
        return _activeTool switch
        {
            AnnotationTool.Pencil => "Texture du crayon : 0 donne un trait lisse, 100 ajoute un grain plus irrégulier.",
            AnnotationTool.Pen => "Vitesse du stylo : ajuste l'ampleur de la taille selon la vitesse.",
            _ => "Variation disponible pour le stylo et le crayon.",
        };
    }

    private string GetActiveToolBrushVariationLabel()
    {
        return _activeTool switch
        {
            AnnotationTool.Pencil => "Texture",
            AnnotationTool.Pen => "Vitesse",
            _ => "Variation",
        };
    }

    private string GetActiveToolBrushVariationAutomationName()
    {
        return _activeTool switch
        {
            AnnotationTool.Pencil => "Texture du crayon",
            AnnotationTool.Pen => "Vitesse du stylo",
            _ => "Variation du trait",
        };
    }

    private void UpdateBrushVariationSliderForActiveTool()
    {
        if (_brushVariationSlider is null)
        {
            return;
        }

        _isUpdatingBrushVariationSlider = true;
        try
        {
            _brushVariationSlider.IsEnabled = ActiveToolHasBrushVariation();
            _brushVariationSlider.Value = GetActiveToolBrushVariation() * 100;
            _brushVariationSlider.ToolTip = GetActiveToolBrushVariationTooltip();
            AutomationProperties.SetName(_brushVariationSlider, GetActiveToolBrushVariationAutomationName());
            if (_brushVariationText is not null)
            {
                _brushVariationText.Text = GetActiveToolBrushVariationLabel();
                _brushVariationText.ToolTip = GetActiveToolBrushVariationTooltip();
                _brushVariationText.Opacity = ActiveToolHasBrushVariation() ? 1 : 0.45;
            }
        }
        finally
        {
            _isUpdatingBrushVariationSlider = false;
        }
    }

    private void SetPressureEnabled(bool enabled)
    {
        _pressureEnabled = enabled;
        ApplyDrawingAttributes();
        SaveAnnotationSettings();
    }

    private void CycleSpeedSizeMode()
    {
        _speedSizeMode = _speedSizeMode switch
        {
            AnnotationSpeedSizeMode.FastThick => AnnotationSpeedSizeMode.SlowThick,
            AnnotationSpeedSizeMode.SlowThick => AnnotationSpeedSizeMode.Off,
            _ => AnnotationSpeedSizeMode.FastThick,
        };

        ApplyDrawingAttributes();
        UpdateSpeedSizeButtonState();
        SaveAnnotationSettings();
    }

    private void UpdateToolbarState()
    {
        var defaultBorder = TryFindResource(QuickCutTheme.BorderBrushKey) as Brush ?? Brushes.Gray;
        foreach (var (tool, button) in _toolButtons)
        {
            button.Opacity = tool == _activeTool ? 1 : 0.62;
            button.BorderBrush = tool == _activeTool
                ? Brushes.DeepSkyBlue
                : defaultBorder;
        }

        foreach (var (target, button) in _selectionTargetButtons)
        {
            button.Opacity = target == _selectionTarget ? 1 : 0.62;
            button.BorderBrush = target == _selectionTarget
                ? Brushes.DeepSkyBlue
                : defaultBorder;
        }

        UpdateSpeedSizeButtonState();

        _viewport.Cursor = _activeTool == AnnotationTool.Pan
            ? Cursors.Hand
            : Cursors.Arrow;
    }

    private void UpdateSpeedSizeButtonState()
    {
        if (_speedSizeButton is null)
        {
            return;
        }

        var appliesToActiveTool = _activeTool == AnnotationTool.Pen;
        var defaultBorder = TryFindResource(QuickCutTheme.BorderBrushKey) as Brush ?? Brushes.Gray;
        _speedSizeButton.IsEnabled = appliesToActiveTool;
        _speedSizeButton.Opacity = !appliesToActiveTool || _speedSizeMode == AnnotationSpeedSizeMode.Off ? 0.45 : 1;
        _speedSizeButton.BorderBrush = _speedSizeMode switch
        {
            AnnotationSpeedSizeMode.FastThick when appliesToActiveTool => Brushes.DeepSkyBlue,
            AnnotationSpeedSizeMode.SlowThick when appliesToActiveTool => Brushes.Goldenrod,
            _ => defaultBorder,
        };
        _speedSizeButton.ToolTip = appliesToActiveTool
            ? GetSpeedSizeTooltip()
            : "Taille selon vitesse : disponible avec le stylo.";
        AutomationProperties.SetName(_speedSizeButton, GetSpeedSizeAutomationName());
    }

    private string GetSpeedSizeTooltip()
    {
        return _speedSizeMode switch
        {
            AnnotationSpeedSizeMode.FastThick => "Taille selon vitesse : rapide plus gros, lent plus fin. Clique pour inverser.",
            AnnotationSpeedSizeMode.SlowThick => "Taille selon vitesse : lent plus gros, rapide plus fin. Clique pour désactiver.",
            _ => "Taille selon vitesse désactivée. Clique pour réactiver en mode rapide plus gros.",
        };
    }

    private string GetSpeedSizeAutomationName()
    {
        return _speedSizeMode switch
        {
            AnnotationSpeedSizeMode.FastThick => "Taille selon vitesse : rapide plus gros",
            AnnotationSpeedSizeMode.SlowThick => "Taille selon vitesse : lent plus gros",
            _ => "Taille selon vitesse désactivée",
        };
    }

    private void ShowCaptureActualSize()
    {
        _zoom = 1;
        _viewMode = ViewMode.ActualSize;
        CenterViewOnRect(new Rect(_imageLeft, _imageTop, _baseImage.PixelWidth, _baseImage.PixelHeight));
        UpdateViewTransform();
    }

    private void FitFrameToWindowAfterLayout()
    {
        _viewMode = ViewMode.FitWindow;
        FitFrameToWindow();
        _ = Dispatcher.InvokeAsync(FitFrameToWindow, DispatcherPriority.ContextIdle);
    }

    private void FitFrameToWindow()
    {
        var viewportWidth = Math.Max(1, _viewport.ActualWidth);
        var viewportHeight = Math.Max(1, _viewport.ActualHeight);
        var contentBounds = GetContentBounds();
        if (contentBounds.IsEmpty)
        {
            contentBounds = new Rect(0, 0, _frameWidth, _frameHeight);
        }

        var rotatedBounds = GetRotatedBounds(contentBounds.Width, contentBounds.Height);
        var availableWidth = Math.Max(1, viewportWidth - FitPadding * 2);
        var availableHeight = Math.Max(1, viewportHeight - FitPadding * 2);
        var fitZoom = Math.Min(availableWidth / rotatedBounds.Width, availableHeight / rotatedBounds.Height);

        _zoom = Math.Clamp(fitZoom, MinimumZoom, MaximumZoom);
        CenterViewOnRect(contentBounds);
        _viewMode = ViewMode.FitWindow;
        UpdateViewTransform();
    }

    private void ApplyCurrentViewMode()
    {
        switch (_viewMode)
        {
            case ViewMode.ActualSize:
                ShowCaptureActualSize();
                break;
            case ViewMode.FitWindow:
                FitFrameToWindow();
                break;
            default:
                UpdateViewTransform();
                break;
        }
    }

    private void CenterViewOnRect(Rect rect)
    {
        var frameCenter = new Point(_frameWidth / 2d, _frameHeight / 2d);
        var targetCenter = new Point(rect.Left + rect.Width / 2d, rect.Top + rect.Height / 2d);
        var offset = targetCenter - frameCenter;
        var matrix = Matrix.Identity;
        matrix.Scale(_zoom, _zoom);
        matrix.Rotate(_rotationDegrees);
        var transformed = matrix.Transform(offset);
        _pan = new Point(-transformed.X, -transformed.Y);
    }

    private Size GetRotatedBounds(double width, double height)
    {
        var radians = Math.Abs(_rotationDegrees % 180) * Math.PI / 180;
        var cosine = Math.Abs(Math.Cos(radians));
        var sine = Math.Abs(Math.Sin(radians));
        var rotatedWidth = width * cosine + height * sine;
        var rotatedHeight = width * sine + height * cosine;
        return new Size(Math.Max(1, rotatedWidth), Math.Max(1, rotatedHeight));
    }

    private void ZoomBy(double factor)
    {
        _zoom = Math.Clamp(_zoom * factor, MinimumZoom, MaximumZoom);
        _viewMode = ViewMode.Custom;
        UpdateViewTransform();
    }

    private void RotateView(double degrees)
    {
        var mode = _viewMode;
        _rotationDegrees = NormalizeRotation(_rotationDegrees + degrees);

        _viewMode = mode;
        ApplyCurrentViewMode();
    }

    private static double NormalizeRotation(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private void PanBy(double x, double y)
    {
        _pan = new Point(_pan.X + x, _pan.Y + y);
        _viewMode = ViewMode.Custom;
        UpdateViewTransform();
    }

    private void UpdateViewTransform()
    {
        var matrix = Matrix.Identity;
        matrix.Translate(-_frameWidth / 2d, -_frameHeight / 2d);
        matrix.Scale(_zoom, _zoom);
        matrix.Rotate(_rotationDegrees);
        matrix.Translate(
            Math.Max(1, _viewport.ActualWidth) / 2d + _pan.X,
            Math.Max(1, _viewport.ActualHeight) / 2d + _pan.Y);
        _viewTransform.Matrix = matrix;
    }

    private void OnViewportPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomBy(e.Delta > 0 ? ZoomStep : 1 / ZoomStep);
        e.Handled = true;
    }

    private void OnViewportPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShouldStartPan(e))
        {
            return;
        }

        _isPanning = true;
        _panStart = e.GetPosition(_viewport);
        _panOrigin = _pan;
        _viewport.Cursor = Cursors.SizeAll;
        _viewport.Focus();
        _viewport.CaptureMouse();
        e.Handled = true;
    }

    private bool ShouldStartPan(MouseButtonEventArgs e)
    {
        return e.ChangedButton == MouseButton.Middle
            || (_activeTool == AnnotationTool.Pan && e.ChangedButton == MouseButton.Left)
            || (_spacePanActive && e.ChangedButton == MouseButton.Left);
    }

    private void OnViewportPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var current = e.GetPosition(_viewport);
        _pan = new Point(
            _panOrigin.X + current.X - _panStart.X,
            _panOrigin.Y + current.Y - _panStart.Y);
        _viewMode = ViewMode.Custom;
        UpdateViewTransform();
        e.Handled = true;
    }

    private void OnViewportPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        StopPan();
        e.Handled = true;
    }

    private void StopPan()
    {
        if (_isPanning)
        {
            _isPanning = false;
            _viewport.ReleaseMouseCapture();
        }

        _viewport.Cursor = _activeTool == AnnotationTool.Pan
            ? Cursors.Hand
            : Cursors.Arrow;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        var hasCtrl = modifiers.HasFlag(ModifierKeys.Control);
        var hasShift = modifiers.HasFlag(ModifierKeys.Shift);

        if (e.Key == Key.Space)
        {
            _spacePanActive = true;
            _viewport.Cursor = Cursors.Hand;
            e.Handled = true;
            return;
        }

        if (hasCtrl && IsPlusKey(e.Key))
        {
            ZoomBy(ZoomStep);
            e.Handled = true;
            return;
        }

        if (hasCtrl && IsMinusKey(e.Key))
        {
            ZoomBy(1 / ZoomStep);
            e.Handled = true;
            return;
        }

        if (hasCtrl && IsZeroKey(e.Key))
        {
            ShowCaptureActualSize();
            e.Handled = true;
            return;
        }

        if (hasCtrl && e.Key == Key.R)
        {
            RotateView(hasShift ? -90 : 90);
            e.Handled = true;
            return;
        }

        if (hasCtrl && e.Key == Key.Z)
        {
            UndoLastStroke();
            e.Handled = true;
            return;
        }

        if (hasCtrl && e.Key == Key.Y)
        {
            RedoLastStroke();
            e.Handled = true;
            return;
        }

        if (hasCtrl && e.Key == Key.Delete)
        {
            ClearAllAnnotations();
            e.Handled = true;
            return;
        }

        if (hasCtrl && e.Key == Key.C)
        {
            CopyAnnotatedImage();
            e.Handled = true;
            return;
        }

        if (hasCtrl && e.Key == Key.S)
        {
            if (hasShift)
            {
                SaveAnnotatedImageAs();
            }
            else
            {
                SaveAnnotatedImage();
            }

            e.Handled = true;
            return;
        }

        if (HandlePanKey(e.Key, hasShift))
        {
            e.Handled = true;
            return;
        }

        if (!hasCtrl && !hasShift)
        {
            if (e.Key == Key.P)
            {
                SetPen();
                e.Handled = true;
            }
            else if (e.Key == Key.L)
            {
                SetHighlighter();
                e.Handled = true;
            }
            else if (e.Key == Key.C)
            {
                SetPencil();
                e.Handled = true;
            }
            else if (e.Key == Key.E)
            {
                SetEraser();
                e.Handled = true;
            }
            else if (e.Key == Key.V)
            {
                SetSelectMode();
                e.Handled = true;
            }
            else if (e.Key == Key.H)
            {
                SetPanMode();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                DeleteSelection();
                e.Handled = true;
            }
        }
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
        {
            return;
        }

        _spacePanActive = false;
        StopPan();
        e.Handled = true;
    }

    private bool HandlePanKey(Key key, bool hasShift)
    {
        if (Keyboard.FocusedElement is Slider)
        {
            return false;
        }

        var distance = hasShift ? 90 : 30;
        return key switch
        {
            Key.Left => PanByAndHandled(distance, 0),
            Key.Right => PanByAndHandled(-distance, 0),
            Key.Up => PanByAndHandled(0, distance),
            Key.Down => PanByAndHandled(0, -distance),
            _ => false,
        };
    }

    private bool PanByAndHandled(double x, double y)
    {
        PanBy(x, y);
        return true;
    }

    private static bool IsPlusKey(Key key) => key is Key.OemPlus or Key.Add;

    private static bool IsMinusKey(Key key) => key is Key.OemMinus or Key.Subtract;

    private static bool IsZeroKey(Key key) => key is Key.D0 or Key.NumPad0;

    private void OnInkCanvasPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_activeTool != AnnotationTool.Select || _selectionTarget == SelectionTarget.Annotations || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (IsImagePatchSource(e.OriginalSource))
        {
            return;
        }

        _selectionStart = ClampPoint(e.GetPosition(_inkCanvas));
        ShowSelectionPreview(_selectionStart.Value, _selectionStart.Value);
        _inkCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void OnInkCanvasPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_selectionStart is null)
        {
            return;
        }

        ShowSelectionPreview(_selectionStart.Value, ClampPoint(e.GetPosition(_inkCanvas)));
        e.Handled = true;
    }

    private void OnInkCanvasPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectionStart is null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _inkCanvas.ReleaseMouseCapture();
        var end = ClampPoint(e.GetPosition(_inkCanvas));
        var region = CreateSelectionRect(_selectionStart.Value, end);
        _selectionStart = null;
        HideSelectionPreview();

        if (region.Width >= 3 && region.Height >= 3)
        {
            SelectRegion(region);
        }

        e.Handled = true;
    }

    private void OnInkCanvasStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        if (_activeTool == AnnotationTool.Pencil)
        {
            var textureStrokes = AnnotationStrokeTextureProcessor.CreatePencilTextureStrokes(
                e.Stroke,
                _currentPencilTextureVariation,
                _pressureEnabled);
            ReplaceCollectedStroke(e.Stroke, textureStrokes);
        }
        else if (_activeTool == AnnotationTool.Pen && _speedSizeMode != AnnotationSpeedSizeMode.Off)
        {
            var speedStrokes = AnnotationStrokeVelocityProcessor.CreateSpeedSizedStrokes(
                e.Stroke,
                _pressureEnabled,
                _speedSizeMode,
                _currentPenSpeedVariation);
            ReplaceCollectedStroke(e.Stroke, speedStrokes);
        }

        ClearRedoHistory();
        EnsureFrameContainsContent(allowOriginShift: true);
    }

    private void OnSelectionTransformed()
    {
        ClearRedoHistory();
        EnsureFrameContainsContent(allowOriginShift: true);
    }

    private void ReplaceCollectedStroke(Stroke originalStroke, StrokeCollection replacementStrokes)
    {
        if (replacementStrokes.Count == 0)
        {
            return;
        }

        var groupId = Guid.NewGuid().ToString("N");
        foreach (var stroke in replacementStrokes)
        {
            stroke.AddPropertyData(StrokeGroupPropertyId, groupId);
        }

        _inkCanvas.Strokes.Remove(originalStroke);
        foreach (var stroke in replacementStrokes)
        {
            _inkCanvas.Strokes.Add(stroke);
        }
    }

    private void SelectRegion(Rect region)
    {
        var selectedStrokes = _selectionTarget == SelectionTarget.Image
            ? new StrokeCollection()
            : GetStrokesInRegion(region);

        var selectedElements = new List<UIElement>();
        if (_selectionTarget is SelectionTarget.Image or SelectionTarget.Both)
        {
            var imagePatch = CreateImagePatch(region);
            if (imagePatch is not null)
            {
                selectedElements.Add(imagePatch);
                ClearRedoHistory();
            }
        }

        _inkCanvas.EditingMode = InkCanvasEditingMode.Select;
        _inkCanvas.Select(selectedStrokes, selectedElements);
    }

    private Image? CreateImagePatch(Rect region)
    {
        var pixelRegion = ToPixelRegion(region);
        if (pixelRegion is null)
        {
            return null;
        }

        var cropped = new CroppedBitmap(_baseImage, pixelRegion.Value);
        cropped.Freeze();

        var image = new Image
        {
            Source = cropped,
            Width = pixelRegion.Value.Width,
            Height = pixelRegion.Value.Height,
            Stretch = Stretch.Fill,
            Cursor = Cursors.SizeAll,
            ToolTip = "Copie transformable : glisse depuis l'intérieur pour la déplacer.",
        };
        image.PreviewMouseLeftButtonDown += OnImagePatchPreviewMouseLeftButtonDown;
        image.PreviewMouseMove += OnImagePatchPreviewMouseMove;
        image.PreviewMouseLeftButtonUp += OnImagePatchPreviewMouseLeftButtonUp;
        image.LostMouseCapture += OnImagePatchLostMouseCapture;
        InkCanvas.SetLeft(image, _imageLeft + pixelRegion.Value.X);
        InkCanvas.SetTop(image, _imageTop + pixelRegion.Value.Y);
        _inkCanvas.Children.Add(image);
        return image;
    }

    private void OnImagePatchPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_activeTool != AnnotationTool.Select || e.ChangedButton != MouseButton.Left || sender is not UIElement imagePatch)
        {
            return;
        }

        if (!_inkCanvas.GetSelectedElements().Contains(imagePatch))
        {
            _inkCanvas.Select(new StrokeCollection(), [imagePatch]);
        }

        StartImagePatchDrag(imagePatch, e.GetPosition(_inkCanvas));
        imagePatch.CaptureMouse();
        e.Handled = true;
    }

    private void OnImagePatchPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingImagePatch || sender != _draggedImagePatch || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = e.GetPosition(_inkCanvas) - _imagePatchDragStart;
        foreach (var (element, origin) in _imagePatchDragOrigins)
        {
            SetElementPosition(element, new Point(origin.X + delta.X, origin.Y + delta.Y));
        }

        var strokeDelta = delta - _imagePatchStrokeDelta;
        if (strokeDelta.X != 0 || strokeDelta.Y != 0)
        {
            var matrix = Matrix.Identity;
            matrix.Translate(strokeDelta.X, strokeDelta.Y);
            _imagePatchDragStrokes.Transform(matrix, applyToStylusTip: false);
            _imagePatchStrokeDelta = delta;
        }

        e.Handled = true;
    }

    private void OnImagePatchPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingImagePatch || sender != _draggedImagePatch)
        {
            return;
        }

        EndImagePatchDrag(applyExpansion: true);
        e.Handled = true;
    }

    private void OnImagePatchLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isDraggingImagePatch && sender == _draggedImagePatch)
        {
            EndImagePatchDrag(applyExpansion: true);
        }
    }

    private void StartImagePatchDrag(UIElement imagePatch, Point start)
    {
        _isDraggingImagePatch = true;
        _draggedImagePatch = imagePatch;
        _imagePatchDragStart = start;
        _imagePatchStrokeDelta = new Vector(0, 0);
        ClearRedoHistory();
        _imagePatchDragOrigins.Clear();
        _imagePatchDragStrokes.Clear();

        foreach (var element in _inkCanvas.GetSelectedElements().Where(element => element != _selectionPreview))
        {
            _imagePatchDragOrigins[element] = GetElementPosition(element);
        }

        foreach (var stroke in _inkCanvas.GetSelectedStrokes())
        {
            _imagePatchDragStrokes.Add(stroke);
        }
    }

    private void EndImagePatchDrag(bool applyExpansion)
    {
        if (!_isDraggingImagePatch)
        {
            return;
        }

        var draggedElement = _draggedImagePatch;
        _isDraggingImagePatch = false;
        _draggedImagePatch = null;
        _imagePatchDragOrigins.Clear();
        _imagePatchDragStrokes.Clear();
        _imagePatchStrokeDelta = new Vector(0, 0);

        if (draggedElement?.IsMouseCaptured == true)
        {
            draggedElement.ReleaseMouseCapture();
        }

        if (applyExpansion)
        {
            EnsureFrameContainsContent(allowOriginShift: true);
        }
    }

    private void EnsureFrameContainsContent(bool allowOriginShift)
    {
        var expansion = AnnotationFramePlanner.PlanExpansion(
            _frameWidth,
            _frameHeight,
            GetContentBounds(),
            allowOriginShift);
        if (!expansion.Changed)
        {
            return;
        }

        var selectedStrokes = new StrokeCollection(_inkCanvas.GetSelectedStrokes());
        var selectedElements = _inkCanvas.GetSelectedElements()
            .Where(element => element != _selectionPreview)
            .ToArray();

        if (expansion.ShiftX != 0 || expansion.ShiftY != 0)
        {
            TranslateFrameContent(expansion.ShiftX, expansion.ShiftY);
        }

        _frameWidth = expansion.Width;
        _frameHeight = expansion.Height;
        ApplyFrameGeometry();
        RefreshSelection(selectedStrokes, selectedElements);

        ApplyCurrentViewMode();
    }

    private Rect GetContentBounds()
    {
        var bounds = new Rect(_imageLeft, _imageTop, _baseImage.PixelWidth, _baseImage.PixelHeight);
        foreach (var stroke in _inkCanvas.Strokes)
        {
            bounds.Union(stroke.GetBounds());
        }

        foreach (var element in _inkCanvas.Children.Cast<UIElement>().Where(element => element != _selectionPreview))
        {
            bounds.Union(GetElementBounds(element));
        }

        return bounds;
    }

    private void TranslateFrameContent(double x, double y)
    {
        _imageLeft += x;
        _imageTop += y;

        foreach (var element in _inkCanvas.Children.Cast<UIElement>().Where(element => element != _selectionPreview))
        {
            var position = GetElementPosition(element);
            SetElementPosition(element, new Point(position.X + x, position.Y + y));
        }

        var matrix = Matrix.Identity;
        matrix.Translate(x, y);
        _inkCanvas.Strokes.Transform(matrix, applyToStylusTip: false);
    }

    private void RefreshSelection(StrokeCollection selectedStrokes, UIElement[] selectedElements)
    {
        if (selectedStrokes.Count == 0 && selectedElements.Length == 0)
        {
            return;
        }

        _inkCanvas.Select(selectedStrokes, selectedElements);
    }

    private static Point GetElementPosition(UIElement element)
    {
        var left = InkCanvas.GetLeft(element);
        var top = InkCanvas.GetTop(element);
        return new Point(
            double.IsNaN(left) ? 0 : left,
            double.IsNaN(top) ? 0 : top);
    }

    private static void SetElementPosition(UIElement element, Point position)
    {
        InkCanvas.SetLeft(element, position.X);
        InkCanvas.SetTop(element, position.Y);
    }

    private static Rect GetElementBounds(UIElement element)
    {
        var position = GetElementPosition(element);
        var size = element.RenderSize;
        if (element is FrameworkElement frameworkElement)
        {
            var width = !double.IsNaN(frameworkElement.Width) && frameworkElement.Width > 0
                ? frameworkElement.Width
                : frameworkElement.ActualWidth;
            var height = !double.IsNaN(frameworkElement.Height) && frameworkElement.Height > 0
                ? frameworkElement.Height
                : frameworkElement.ActualHeight;
            size = new Size(Math.Max(size.Width, width), Math.Max(size.Height, height));
        }

        return new Rect(position, new Size(Math.Max(0, size.Width), Math.Max(0, size.Height)));
    }

    private StrokeCollection GetStrokesInRegion(Rect region)
    {
        var strokes = new StrokeCollection();
        foreach (var stroke in _inkCanvas.Strokes)
        {
            if (stroke.GetBounds().IntersectsWith(region))
            {
                strokes.Add(stroke);
            }
        }

        return strokes;
    }

    private Point ClampPoint(Point point)
    {
        var x = Math.Clamp(point.X, 0, _frameWidth);
        var y = Math.Clamp(point.Y, 0, _frameHeight);
        return new Point(x, y);
    }

    private static Rect CreateSelectionRect(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        return new Rect(left, top, width, height);
    }

    private Int32Rect? ToPixelRegion(Rect region)
    {
        var imageBounds = new Rect(_imageLeft, _imageTop, _baseImage.PixelWidth, _baseImage.PixelHeight);
        var sourceBounds = Rect.Intersect(region, imageBounds);
        if (sourceBounds.IsEmpty || sourceBounds.Width < 1 || sourceBounds.Height < 1)
        {
            return null;
        }

        var left = Math.Clamp((int)Math.Floor(sourceBounds.Left - _imageLeft), 0, _baseImage.PixelWidth);
        var top = Math.Clamp((int)Math.Floor(sourceBounds.Top - _imageTop), 0, _baseImage.PixelHeight);
        var right = Math.Clamp((int)Math.Ceiling(sourceBounds.Right - _imageLeft), 0, _baseImage.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(sourceBounds.Bottom - _imageTop), 0, _baseImage.PixelHeight);
        if (right <= left || bottom <= top)
        {
            return null;
        }

        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private bool IsImagePatchSource(object source)
    {
        return source is Image image
            && image != _baseImageElement
            && _inkCanvas.Children.Contains(image);
    }

    private void ShowSelectionPreview(Point start, Point end)
    {
        var region = CreateSelectionRect(start, end);
        InkCanvas.SetLeft(_selectionPreview, region.Left);
        InkCanvas.SetTop(_selectionPreview, region.Top);
        _selectionPreview.Width = region.Width;
        _selectionPreview.Height = region.Height;
        _selectionPreview.Visibility = Visibility.Visible;
    }

    private void HideSelectionPreview()
    {
        _selectionPreview.Visibility = Visibility.Collapsed;
    }

    private void DeleteSelection()
    {
        var selectedStrokes = _inkCanvas.GetSelectedStrokes().ToArray();
        var selectedElements = _inkCanvas.GetSelectedElements()
            .Where(element => element != _selectionPreview)
            .ToArray();

        foreach (var stroke in selectedStrokes)
        {
            _inkCanvas.Strokes.Remove(stroke);
        }

        foreach (var element in selectedElements)
        {
            _inkCanvas.Children.Remove(element);
        }

        if (selectedStrokes.Length > 0 || selectedElements.Length > 0)
        {
            ClearRedoHistory();
        }

        ClearSelection();
    }

    private void ClearAllAnnotations()
    {
        HideSelectionPreview();
        ClearSelection();
        ClearAnnotationLayer();
    }

    private void ClearAnnotationLayer()
    {
        ClearRedoHistory();
        _inkCanvas.Strokes.Clear();
        foreach (var element in _inkCanvas.Children.Cast<UIElement>().Where(element => element != _selectionPreview).ToArray())
        {
            _inkCanvas.Children.Remove(element);
        }
    }

    private void ClearSelection()
    {
        _inkCanvas.Select(new StrokeCollection(), Array.Empty<UIElement>());
    }

    private void UndoLastStroke()
    {
        if (_inkCanvas.Strokes.Count > 0)
        {
            var group = GetLastStrokeGroup();
            foreach (var stroke in group)
            {
                _inkCanvas.Strokes.Remove(stroke);
            }

            _redoStrokeGroups.Push(group);
        }
    }

    private StrokeCollection GetLastStrokeGroup()
    {
        var group = new StrokeCollection();
        if (_inkCanvas.Strokes.Count == 0)
        {
            return group;
        }

        var lastIndex = _inkCanvas.Strokes.Count - 1;
        var lastStroke = _inkCanvas.Strokes[lastIndex];
        var groupId = GetStrokeGroupId(lastStroke);
        if (groupId is null)
        {
            group.Add(lastStroke);
            return group;
        }

        var firstIndex = lastIndex;
        while (firstIndex > 0 && GetStrokeGroupId(_inkCanvas.Strokes[firstIndex - 1]) == groupId)
        {
            firstIndex--;
        }

        for (var index = firstIndex; index <= lastIndex; index++)
        {
            group.Add(_inkCanvas.Strokes[index]);
        }

        return group;
    }

    private static string? GetStrokeGroupId(Stroke stroke)
    {
        return stroke.ContainsPropertyData(StrokeGroupPropertyId)
            ? stroke.GetPropertyData(StrokeGroupPropertyId).ToString()
            : null;
    }

    private void RedoLastStroke()
    {
        if (_redoStrokeGroups.Count == 0)
        {
            return;
        }

        var group = _redoStrokeGroups.Pop();
        foreach (var stroke in group)
        {
            _inkCanvas.Strokes.Add(stroke);
        }

        EnsureFrameContainsContent(allowOriginShift: true);
    }

    private void ClearRedoHistory()
    {
        _redoStrokeGroups.Clear();
    }

    private async Task AddCaptureToCurrentAnnotationAsync()
    {
        if (_captureAdditionalImageAsync is null || _isAddingCapture)
        {
            return;
        }

        _isAddingCapture = true;
        try
        {
            var currentImage = RenderAnnotatedImage();
            var additionalImage = await _captureAdditionalImageAsync(this);
            if (additionalImage is null)
            {
                return;
            }

            SetBaseImage(CaptureImageComposer.Compose([currentImage, additionalImage]));
        }
        finally
        {
            _isAddingCapture = false;
        }
    }

    private void SetBaseImage(BitmapSource image)
    {
        _baseImage = image;
        _baseImageElement.Source = _baseImage;
        ResetFrameToBaseImage();
        ApplyFrameGeometry();

        _inkCanvas.Select(new StrokeCollection(), Array.Empty<UIElement>());
        ClearAnnotationLayer();

        HideSelectionPreview();
        _rotationDegrees = 0;
        _pan = new Point(0, 0);
        _viewMode = ViewMode.FitWindow;
        ApplyInitialTool();
        FitFrameToWindowAfterLayout();
    }

    private void CopyAnnotatedImage()
    {
        Clipboard.SetImage(RenderAnnotatedImage());
    }

    private void SaveAnnotatedImage()
    {
        SavePng(RenderAnnotatedImage(), _imagePath);
    }

    private void SaveAnnotatedImageAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Enregistrer la capture",
            Filter = "Image PNG (*.png)|*.png",
            FileName = System.IO.Path.GetFileName(_imagePath),
            InitialDirectory = System.IO.Path.GetDirectoryName(_imagePath),
            AddExtension = true,
            DefaultExt = ".png",
        };

        if (dialog.ShowDialog(this) == true)
        {
            SavePng(RenderAnnotatedImage(), dialog.FileName);
        }
    }

    private void DeleteCaptureFromDisk()
    {
        var result = MessageBox.Show(
            this,
            "Supprimer cette capture du disque et fermer l'annotation ?",
            "Supprimer la capture",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            if (File.Exists(_imagePath))
            {
                File.Delete(_imagePath);
            }

            _captureDeleted?.Invoke(_imagePath);
            Close();
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(
                this,
                $"Capture non supprimée : {ex.Message}",
                "Supprimer la capture",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private RenderTargetBitmap RenderAnnotatedImage()
    {
        HideSelectionPreview();
        ClearSelection();

        EnsureFrameContainsContent(allowOriginShift: true);

        var width = (int)Math.Ceiling(_frameWidth);
        var height = (int)Math.Ceiling(_frameHeight);
        var size = new Size(width, height);
        _surface.Measure(size);
        _surface.Arrange(new Rect(size));
        _surface.UpdateLayout();

        var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(_surface);
        rendered.Freeze();
        return rendered;
    }

    private static BitmapSource LoadBitmap(string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static void SavePng(BitmapSource image, string outputPath)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        var temporaryPath = outputPath + ".tmp";
        using (var stream = File.Create(temporaryPath))
        {
            encoder.Save(stream);
        }

        File.Move(temporaryPath, outputPath, overwrite: true);
    }
}
