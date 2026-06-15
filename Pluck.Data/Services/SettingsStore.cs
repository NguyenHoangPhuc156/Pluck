using System.Text.Json;
using System.Text.Json.Serialization;
using Pluck.Data.Models;

namespace Pluck.Data.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _settingsPath;

    public SettingsStore(string? appDataFolder = null)
    {
        var folder = appDataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pluck");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

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

    public void Save(PluckSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
