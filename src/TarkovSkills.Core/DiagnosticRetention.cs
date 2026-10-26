namespace TarkovSkills.Core;

internal static class DiagnosticRetention
{
    internal static void Trim(string directory, string pattern, int keep, string? preserve = null)
    {
        try
        {
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles(pattern)
                .Where(file => (file.Attributes & FileAttributes.ReparsePoint) == 0 && file.Name != preserve
                    && System.Text.RegularExpressions.Regex.IsMatch(file.Name,
                        @"\A(capture|support)-[0-9]{8}-[0-9]{6}-[0-9a-f]{32}\.(log|zip|txt)\z"))
                .OrderByDescending(file => file.LastWriteTimeUtc).Skip(keep))
            {
                try { file.Delete(); } catch { /* Open files and storage failures do not affect capture. */ }
            }
        }
        catch { }
    }
}
