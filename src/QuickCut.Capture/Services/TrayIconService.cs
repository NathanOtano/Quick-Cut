using System.IO;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace QuickCut.Capture.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Drawing.Icon? _appIcon;
    private readonly Forms.NotifyIcon _notifyIcon;

    public event EventHandler? CaptureRequested;
    public event EventHandler? ShowRequested;
    public event EventHandler? OpenCapturesRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Capturer", null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Afficher QuickCut", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Ouvrir les captures", null, (_, _) => OpenCapturesRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _appIcon = LoadAppIcon();

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _appIcon ?? Drawing.SystemIcons.Application,
            Text = "QuickCut",
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (ShouldRequestCapture(e.Button))
            {
                CaptureRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    internal static bool ShouldRequestCapture(Forms.MouseButtons button) => button == Forms.MouseButtons.Left;

    public void ShowStillRunningTip()
    {
        _notifyIcon.BalloonTipTitle = "QuickCut reste actif";
        _notifyIcon.BalloonTipText = "Win + < ou AltGr + Impr. écran continuent de déclencher la capture. Utilise Quitter dans le menu QuickCut pour fermer.";
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(4000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _appIcon?.Dispose();
    }

    private static Drawing.Icon? LoadAppIcon()
    {
        var processPath = Environment.ProcessPath;
        if (processPath is null || !File.Exists(processPath))
        {
            return null;
        }

        return Drawing.Icon.ExtractAssociatedIcon(processPath);
    }
}
