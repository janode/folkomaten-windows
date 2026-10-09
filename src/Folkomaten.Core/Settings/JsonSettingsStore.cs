using System.Text.Json;

namespace Folkomaten.Core.Settings;

/// <summary>Holder <see cref="AppSettings"/> i en JSON-fil. En manglende eller uleselig fil gir standardverdier.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        _filePath = filePath;
        Current = Load(filePath);
    }

    public AppSettings Current { get; }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        // Skriv ved siden av målet og bytt om, så et krasj aldri etterlater en halvskrevet fil.
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Current, SerializerOptions));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    private static AppSettings Load(string filePath)
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath)) ?? new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
