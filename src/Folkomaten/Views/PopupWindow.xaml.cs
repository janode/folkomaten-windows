using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Folkomaten.Core;
using Folkomaten.Core.Tenor;
using Folkomaten.Platform;
using Folkomaten.ViewModels;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Folkomaten.Views;

internal enum PopupPlacement
{
    /// <summary>Over systemstatusfeltet, under pekeren: brukes ved klikk på tray-ikonet.</summary>
    NearTray,

    /// <summary>Midt på skjermen pekeren er på: brukes for hotkeyen.</summary>
    Centered,
}

/// <summary>Hovedpanelet: søk, listen over testbrukere og handlingene for å hente nye.</summary>
internal partial class PopupWindow : Window
{
    private const string BulkOrderUrl = "https://ra-preprod.bankidnorge.no/#!/bulk-order";
    private const string TextFileFilter = "Tekstfiler (*.txt;*.csv)|*.txt;*.csv|Alle filer (*.*)|*.*";
    private const int ScreenMargin = 8;
    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(1.2);

    // Et klikk på tray-ikonet deaktiverer (og skjuler) først popupen, og ber så om å slå den av eller på.
    // Uten dette tidsvinduet ville det samme klikket åpnet den igjen.
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly PopupViewModel _viewModel;
    private readonly CredentialStore _credentialStore;
    private readonly HttpClient _httpClient;
    private readonly Action _openSettings;
    private readonly Action _exit;
    private long _hiddenAtTimestamp;

    public PopupWindow(
        PopupViewModel viewModel, CredentialStore credentialStore, HttpClient httpClient, Action openSettings, Action exit)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _credentialStore = credentialStore;
        _httpClient = httpClient;
        _openSettings = openSettings;
        _exit = exit;
        DataContext = viewModel;
    }

    public void Toggle(PopupPlacement placement)
    {
        if (IsVisible)
        {
            HidePopup();
        }
        else if (Stopwatch.GetElapsedTime(_hiddenAtTimestamp) > ReopenGuard)
        {
            ShowAt(placement);
        }
    }

    public void ShowAt(PopupPlacement placement)
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        var scale = VisualTreeHelper.GetDpi(this);
        var width = (int)(Width * scale.DpiScaleX);
        var height = (int)(Height * scale.DpiScaleY);

        var pointer = WinForms.Cursor.Position;
        var area = WinForms.Screen.FromPoint(pointer).WorkingArea;
        var (x, y) = placement == PopupPlacement.Centered
            ? (area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2)
            : (pointer.X - width / 2, area.Bottom - height - ScreenMargin);
        x = Math.Clamp(x, area.Left + ScreenMargin, Math.Max(area.Left + ScreenMargin, area.Right - width - ScreenMargin));
        y = Math.Clamp(y, area.Top + ScreenMargin, Math.Max(area.Top + ScreenMargin, area.Bottom - height - ScreenMargin));

        // Posisjon i enhetspiksler: WPFs Left/Top er i DIP-er for den skjermen vinduet sist var på.
        NativeMethods.SetWindowPos(
            handle, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);

        Show();
        NativeMethods.ForceForeground(handle);
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Avrundede hjørner og en skygge på Windows 11; ignoreres på Windows 10.
        var preference = NativeMethods.DwmwcpRound;
        _ = NativeMethods.DwmSetWindowAttribute(
            new WindowInteropHelper(this).Handle, NativeMethods.DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    private void HidePopup()
    {
        if (IsVisible)
        {
            _hiddenAtTimestamp = Stopwatch.GetTimestamp();
            Hide();
        }
    }

    private void OnDeactivated(object? sender, EventArgs e) => HidePopup();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePopup();
            e.Handled = true;
        }
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.Search = "";
        SearchBox.Focus();
    }

    private void OnRowClicked(object sender, MouseButtonEventArgs e) => Copy(RowOf(sender));

    private void OnCopyClicked(object sender, RoutedEventArgs e) => Copy(RowOf(sender));

    private void OnFavoriteClicked(object sender, RoutedEventArgs e) => _viewModel.ToggleFavorite(RowOf(sender));

    private static UserRowViewModel RowOf(object sender) => (UserRowViewModel)((FrameworkElement)sender).DataContext;

    private static async void Copy(UserRowViewModel row)
    {
        try
        {
            Clipboard.SetDataObject(row.User.Fnr, copy: true);
        }
        catch (ExternalException)
        {
            // En annen prosess holder utklippstavlen åpen.
            SystemSounds.Beep.Play();
            return;
        }

        row.IsCopied = true;
        await Task.Delay(CopiedFeedbackDuration);
        row.IsCopied = false;
    }

    private void OnMoreClicked(object sender, RoutedEventArgs e) => OpenMenu((Button)sender);

    private void OnFetchClicked(object sender, RoutedEventArgs e) => OpenMenu((Button)sender);

    private void OpenMenu(Button button)
    {
        var menu = button.ContextMenu!;
        menu.DataContext = _viewModel;
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    private void OnLoadEmbeddedClicked(object sender, RoutedEventArgs e) => _viewModel.LoadEmbedded();

    private void OnClearListClicked(object sender, RoutedEventArgs e) => _viewModel.Clear();

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        HidePopup();
        _openSettings();
    }

    private void OnExitClicked(object sender, RoutedEventArgs e) => _exit();

    private void OnOrderClicked(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(BulkOrderUrl) { UseShellExecute = true });

    private void OnLoadFileClicked(object sender, RoutedEventArgs e)
    {
        HidePopup();
        var dialog = new OpenFileDialog { Title = "Velg fil med testbrukere", Filter = TextFileFilter };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _viewModel.LoadFile(dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowError("Kunne ikke lese fila", exception.Message);
        }
    }

    private async void OnFetchCountClicked(object sender, RoutedEventArgs e)
    {
        var count = int.Parse((string)((MenuItem)sender).Tag);
        if (_credentialStore.Read() is not { IsComplete: true } credentials)
        {
            HidePopup();
            _openSettings();
            return;
        }

        _viewModel.IsFetchingFromTenor = true;
        try
        {
            var maskinporten = new MaskinportenClient(_httpClient, credentials, TimeProvider.System);
            var tenor = new TenorClient(_httpClient, maskinporten, TimeProvider.System);
            SaveUsersToFile(await tenor.FetchUsers(count, CancellationToken.None));
        }
        catch (Exception exception) when (exception is TenorException or MaskinportenException
            or HttpRequestException or TaskCanceledException)
        {
            HidePopup();
            ShowError("Tenor-feil", exception.Message);
        }
        finally
        {
            _viewModel.IsFetchingFromTenor = false;
        }
    }

    private void SaveUsersToFile(IReadOnlyList<TestUser> users)
    {
        HidePopup();
        var dialog = new SaveFileDialog
        {
            Title = "Lagre testbrukere fra Tenor – last fila opp i BankID preprod, og last den så inn her",
            FileName = "testbrukere.txt",
            Filter = TextFileFilter,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, TestUserGenerator.FileData(users));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowError("Kunne ikke lagre fila", exception.Message);
        }
    }

    private static void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
}
