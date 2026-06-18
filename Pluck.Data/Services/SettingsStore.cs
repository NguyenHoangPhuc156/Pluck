using System.Text.Json;
using System.Text.Json.Serialization;
using Pluck.Data.Models;

namespace Pluck.Data.Services;

/// <summary>
/// Persists and loads <see cref="PluckSettings"/> as JSON under the user's local application data folder.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _settingsPath;

    /// <summary>
    /// Initializes a new store that reads and writes <c>settings.json</c> in the Pluck app data directory.
    /// </summary>
    /// <param name="appDataFolder">
    /// Optional root folder for settings storage. When <see langword="null"/>, uses
    /// <see cref="Environment.SpecialFolder.LocalApplicationData"/>/<c>Pluck</c>.
    /// </param>
    public SettingsStore(string? appDataFolder = null)
    {
        var folder = appDataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pluck");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    /// <summary>
    /// Loads settings from disk, returning defaults when the file is missing or cannot be parsed.
    /// </summary>
    /// <returns>The deserialized settings, or a new <see cref="PluckSettings"/> instance on failure.</returns>
    public PluckSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new PluckSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<PluckSettings>(json, JsonOptions) ?? new PluckSettings();
        }
        catch
        {
            return new PluckSettings();
        }
    }

    /// <summary>
    /// Serializes and writes the given settings to disk.
    /// </summary>
    /// <param name="settings">The settings instance to persist.</param>
    public void Save(PluckSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
