using Microsoft.Win32;

namespace Folkomaten.Platform;

/// <summary>Starter appen ved pålogging via registernøkkelen <c>Run</c> for gjeldende bruker.</summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Folkomaten";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>True bare når oppføringen peker på denne exe-filen: en oppføring etterlatt av en flyttet eller eldre kopi teller ikke.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            key.SetValue(ValueName, Command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
