namespace Folkomaten.Core.Settings;

public interface ISettingsStore
{
    /// <summary>Den ene delte innstillingsinstansen. Endre den, og kall så <see cref="Save"/>.</summary>
    AppSettings Current { get; }

    void Save();
}
