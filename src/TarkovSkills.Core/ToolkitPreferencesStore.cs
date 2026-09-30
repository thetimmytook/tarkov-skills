using System.Text.Json;

namespace TarkovSkills.Core;

public sealed record ToolkitPreferences
{
    public bool ShowGetStarted { get; init; } = true;
}

public sealed class ToolkitPreferencesStore
{
    private readonly string _path;

    public ToolkitPreferencesStore(string? path = null) =>
        _path = path ?? Path.Combine(AppPaths.DataDirectory, "toolkit-ui.json");

    public ToolkitPreferences Load()
    {
        try
        {
            return JsonSerializer.Deserialize<ToolkitPreferences>(File.ReadAllText(_path), JsonDefaults.Options)
                ?? new ToolkitPreferences();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing or unreadable UI preference must not prevent collection.
            return new ToolkitPreferences();
        }
    }

    public void Save(ToolkitPreferences preferences) =>
        AppPaths.AtomicWrite(_path, JsonSerializer.Serialize(preferences, JsonDefaults.Options));
}
