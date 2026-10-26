using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core;

// Archives revalidate even our own persisted logs: never zip arbitrary report files,
// authentication state, CSVs, history, paths, exception text or unrecognized fields.
internal static class SupportLogExport
{
    private const long MaximumFileBytes = 2 * 1024 * 1024;
    internal static void AddLogs(ZipArchive zip, string reports)
    {
        try
        {
            var captures = new DirectoryInfo(reports).EnumerateFiles("capture-*.log")
                .Where(file => Regex.IsMatch(file.Name, @"\Acapture-[0-9]{8}-[0-9]{6}-[0-9a-f]{32}\.log\z")
                    && file.LastWriteTimeUtc >= DateTime.UtcNow.AddMinutes(-15))
                .OrderByDescending(file => file.LastWriteTimeUtc).Take(2).ToArray();
            for (var i = 0; i < captures.Length; i++)
            {
                var text = ReadCapture(captures[i]);
                if (text.Length > 0) SupportArchive.WriteEntry(zip, $"capture-{i + 1}.log", text);
            }
            var permission = new FileInfo(Path.Combine(reports, "permission-setup.log"));
            if (Safe(permission))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(permission.FullName));
                var clean = CaptureRelayPrivacy.SanitizeForArchive(new("windows_capture_access", document.RootElement));
                if (clean is not null) SupportArchive.WriteEntry(zip, "permission-setup.log", clean.Value.GetRawText());
            }
        }
        catch { /* A partial log or unreadable file never suppresses the support report. */ }
    }

    private static bool Safe(FileInfo file) => file.Exists && file.Length <= MaximumFileBytes
        && (file.Attributes & FileAttributes.ReparsePoint) == 0;

    private static string ReadCapture(FileInfo file)
    {
        try
        {
            if (!Safe(file)) return "";
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var output = new StringBuilder();
            for (var i = 0; i < 4096 && reader.ReadLine() is { } line; i++)
            {
                if (line.Length > 16384) continue;
                try
                {
                    using var row = JsonDocument.Parse(line, new() { MaxDepth = 12 });
                    var root = row.RootElement;
                    if (root.GetProperty("schema_version").GetInt32() != 1) continue;
                    var stage = root.GetProperty("stage").GetString() ?? "";
                    var data = CaptureRelayPrivacy.SanitizeForArchive(new(stage, root.GetProperty("data")));
                    if (data is null) continue;
                    var utc = DateTimeOffset.TryParse(root.GetProperty("utc").GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var time) ? (DateTimeOffset?)time : null;
                    var elapsed = root.GetProperty("elapsed_seconds").TryGetDouble(out var seconds) && double.IsFinite(seconds)
                        && seconds >= 0 ? (double?)seconds : null;
                    output.AppendLine(JsonSerializer.Serialize(new { schema_version = 1, utc, elapsed_seconds = elapsed, stage, data }));
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { }
            }
            return output.ToString();
        }
        catch { return ""; }
    }
}
