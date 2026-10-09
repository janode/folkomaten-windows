using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using Folkomaten.Core;
using Folkomaten.Core.Settings;
using Folkomaten.Core.Tenor;
using Folkomaten.Platform;
using Folkomaten.ViewModels;
using Folkomaten.Views;

namespace Folkomaten;

/// <summary>
/// Kjører appen fra systemstatusfeltet: ingen hovedvindu og ingen knapp på oppgavelinjen.
/// Et klikk på tray-ikonet eller den globale hotkeyen åpner popupen.
/// </summary>
public partial class App : Application
{
    private const string InstanceMutexName = @"Local\Folkomaten.Instance";
    private const string ShowEventName = @"Local\Folkomaten.Show";

    private readonly HttpClient _httpClient = new();
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showEventRegistration;
    private JsonSettingsStore? _settingsStore;
    private CredentialStore? _credentialStore;
    private HotkeyService? _hotkeyService;
    private TrayIcon? _trayIcon;
    private PopupWindow? _popup;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            AskRunningInstanceToShow();
            Shutdown();
            return;
        }

        _settingsStore = new JsonSettingsStore(AppPaths.SettingsFile);
        _credentialStore = new CredentialStore(AppPaths.CredentialsFile, new DpapiSecretProtector());
        var viewModel = new PopupViewModel(new TestUserStore(_settingsStore));
        _popup = new PopupWindow(viewModel, _credentialStore, _httpClient, ShowSettings, Shutdown);
        _trayIcon = new TrayIcon(() => _popup.Toggle(PopupPlacement.NearTray), ShowSettings, Shutdown);

        _hotkeyService = new HotkeyService();
        _hotkeyService.Pressed += (_, _) => _popup.Toggle(PopupPlacement.Centered);
        var hotkey = Hotkey.FromSetting(_settingsStore.Current.Hotkey);
        if (!_hotkeyService.Register(hotkey))
        {
            _trayIcon.ShowMessage(
                $"Hurtigtasten {hotkey.Display} er i bruk av et annet program. Velg en annen i innstillingene.");
        }

        // Å starte exe-filen igjen åpner popupen i instansen som allerede kjører.
        _showEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ShowEventName);
        _showEventRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => _popup.ShowAt(PopupPlacement.Centered)),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showEventRegistration?.Unregister(waitObject: null);
        _showEvent?.Dispose();
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _httpClient.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void AskRunningInstanceToShow()
    {
        if (!EventWaitHandle.TryOpenExisting(ShowEventName, out var showEvent))
        {
            return;
        }

        using (showEvent)
        {
            // Windows lar bare en bakgrunnsprosess ta forgrunnen når forgrunnsprosessen tillater det.
            using var current = Process.GetCurrentProcess();
            foreach (var other in Process.GetProcessesByName(current.ProcessName))
            {
                using (other)
                {
                    if (other.Id != current.Id)
                    {
                        NativeMethods.AllowSetForegroundWindow(other.Id);
                    }
                }
            }

            showEvent.Set();
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settingsStore!, _credentialStore!, _hotkeyService!);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        _settingsWindow.Activate();
    }
}
