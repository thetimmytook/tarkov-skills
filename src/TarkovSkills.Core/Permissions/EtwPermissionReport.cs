using System.Text.Json;

namespace TarkovSkills.Core.Permissions;

// One bounded, local, latest-attempt report. The elevated helper never opens
// application state. Account identities, pipe names and exception text are excluded.
public static class EtwPermissionReport
{
    public static bool Save(EtwPermissionAction action, EtwPermissionResult result) =>
        SaveTo(AppPaths.ReportsDirectory, action, result);

    internal static bool SaveTo(string directory, EtwPermissionAction action, EtwPermissionResult result)
    {
        try
        {
            if (!Enum.IsDefined(action) || !Enum.IsDefined(result.Outcome)) return false;
            var text = JsonSerializer.Serialize(new
            {
                schema_version = 1, stage = "windows_capture_access", utc = DateTimeOffset.UtcNow,
                action = action.ToString(), outcome = result.Outcome.ToString(), windows_error = result.WindowsError,
                message = result.Message, windows_version = Environment.OSVersion.Version.ToString()
            }, new JsonSerializerOptions { WriteIndented = true });
            AppPaths.AtomicWrite(Path.Combine(directory, "permission-setup.log"), text);
            return true;
        }
        catch (Exception) { return false; }
    }
}
