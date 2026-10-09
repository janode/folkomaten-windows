using System.Diagnostics;
using System.Media;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Folkomaten.Core.Settings;
using Folkomaten.Core.Tenor;
using Folkomaten.Platform;

namespace Folkomaten.Views;

/// <summary>Innstillinger: den globale hotkeyen og legitimasjonen til Maskinporten som brukes til å hente fra Tenor.</summary>
internal partial class SettingsWindow : Window
{
    private const string RecordingPrompt = "Trykk snarvei…";

    private readonly ISettingsStore _settingsStore;
    private readonly CredentialStore _credentialStore;
    private readonly HotkeyService _hotkeyService;
    private Hotkey _hotkey;
    private bool _isRecording;

    public SettingsWindow(ISettingsStore settingsStore, CredentialStore credentialStore, HotkeyService hotkeyService)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _hotkeyService = hotkeyService;
        _hotkey = Hotkey.FromSetting(settingsStore.Current.Hotkey);
        HotkeyButton.Content = _hotkey.Display;
        VersionText.Text = $"Versjon {AppVersion()}";

        var credentials = credentialStore.Read();
        ClientIdBox.Text = credentials?.ClientId ?? "";
        KidBox.Text = credentials?.Kid ?? "";
        PrivateKeyBox.Text = credentials?.PrivateKeyPem ?? "";
        UpdateSaveButton();
    }

    // Informasjonsversjonen er pipelinens versjon, fulgt av "+<commit>" som ikke er til nytte her.
    private static string AppVersion() =>
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "ukjent";

    private void OnHotkeyButtonClicked(object sender, RoutedEventArgs e) => SetRecording(!_isRecording);

    private void SetRecording(bool isRecording)
    {
        _isRecording = isRecording;

        // Frigi hotkeyen under opptak: ellers åpner et trykk på den gjeldende kombinasjonen
        // popupen, og tastetrykket når aldri dette vinduet.
        if (isRecording)
        {
            _hotkeyService.Unregister();
        }
        else
        {
            RestoreHotkey();
        }

        HotkeyButton.Content = isRecording ? RecordingPrompt : _hotkey.Display;
        HotkeyButton.ToolTip = isRecording
            ? "Trykk ønsket tastkombinasjon (Esc avbryter)"
            : "Klikk for å endre hurtigtasten";
    }

    protected override void OnClosed(EventArgs e)
    {
        // Vinduet kan lukkes midt i et opptak.
        RestoreHotkey();
        base.OnClosed(e);
    }

    private void RestoreHotkey()
    {
        if (_hotkeyService.Current is null)
        {
            _hotkeyService.Register(_hotkey);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecording)
        {
            return;
        }

        // Svelg tasten, så den ikke utløser noe annet i vinduet.
        e.Handled = true;

        // Alt-kombinasjoner kommer som Key.System med den egentlige tasten i SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            SetRecording(false);
            return;
        }

        if (IsModifier(key))
        {
            return;
        }

        // Krev en modifikator: en enkelt tast som global hotkey ville kapret tastaturet.
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            SystemSounds.Beep.Play();
            return;
        }

        Apply(Hotkey.FromKeyPress(Keyboard.Modifiers, key));
        SetRecording(false);
    }

    private void Apply(Hotkey hotkey)
    {
        var previous = _hotkey;
        if (!_hotkeyService.Register(hotkey))
        {
            _hotkeyService.Register(previous);
            MessageBox.Show(
                this,
                $"{hotkey.Display} er i bruk av et annet program. Velg en annen kombinasjon.",
                "Hurtigtast",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _hotkey = hotkey;
        _settingsStore.Current.Hotkey = hotkey.ToSetting();
        _settingsStore.Save();
    }

    private static bool IsModifier(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private void OnCredentialTextChanged(object sender, TextChangedEventArgs e) => UpdateSaveButton();

    private void UpdateSaveButton()
    {
        // TextChanged utløses mens InitializeComponent fortsatt oppretter kontrollene.
        if (SaveButton is not null)
        {
            SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(ClientIdBox.Text)
                && !string.IsNullOrWhiteSpace(PrivateKeyBox.Text);
        }
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        var kid = KidBox.Text.Trim();
        _credentialStore.Save(new MaskinportenCredentials(
            ClientIdBox.Text.Trim(), PrivateKeyBox.Text.Trim(), kid.Length > 0 ? kid : null));
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Close();

    private void OnLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
