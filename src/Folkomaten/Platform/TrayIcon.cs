using System.Windows;
using WinForms = System.Windows.Forms;

namespace Folkomaten.Platform;

/// <summary>Ikonet i systemstatusfeltet: venstreklikk slår popupen av og på, høyreklikk åpner en liten meny.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const int BalloonTimeoutMilliseconds = 5000;

    private readonly WinForms.NotifyIcon _notifyIcon;

    public TrayIcon(Action toggle, Action openSettings, Action exit)
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Åpne", image: null, (_, _) => toggle());
        menu.Items.Add("Innstillinger…", image: null, (_, _) => openSettings());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Avslutt", image: null, (_, _) => exit());

        using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/folkomaten.ico")).Stream;
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream, WinForms.SystemInformation.SmallIconSize),
            Text = "Folkomaten",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                toggle();
            }
        };
    }

    public void ShowMessage(string text) =>
        _notifyIcon.ShowBalloonTip(BalloonTimeoutMilliseconds, "Folkomaten", text, WinForms.ToolTipIcon.Warning);

    public void Dispose()
    {
        // Skjul først: ellers blir et dødt ikon liggende i trayen til musen passerer over det.
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
