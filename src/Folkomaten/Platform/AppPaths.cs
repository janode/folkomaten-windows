using System.IO;

namespace Folkomaten.Platform;

internal static class AppPaths
{
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Folkomaten");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string CredentialsFile => Path.Combine(DataDirectory, "credentials.bin");
}
