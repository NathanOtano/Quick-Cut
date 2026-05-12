using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickCut.Capture.Services;

public sealed record ScreenCaptureResult(BitmapSource Image, Int32Rect PixelBounds);
