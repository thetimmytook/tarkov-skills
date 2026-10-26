using System.IO.Compression;
using System.Text;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core;

public enum SupportOperation { Capture, Save, Read, Settings, Application }
public sealed record SupportBundle(string Report, Uri IssueUri, string? ReportPath, string? ArchivePath);

// This creates local artifacts only. Opening a prefilled issue and attaching its ZIP
// are separate user actions; no network client or credentials enter this service.
public static class SupportArchive
{
    public const string IssuesUrl = "https://github.com/thetimmytook/tarkov-skills/issues/new";
    private static string FallbackDirectory => Path.Combine(Path.GetTempPath(), "TarkovSkills", "reports");

    public static SupportBundle Create(DesktopAuthProduct product, string version, SupportOperation operation, Exception error) =>
        CreateIn(AppPaths.ReportsDirectory, FallbackDirectory, product, version, operation, error);

    public static void MarkPending(SupportBundle bundle)
    {
        if (bundle.ArchivePath is not { } archive) return;
        try { AppPaths.AtomicWrite(Path.Combine(Path.GetDirectoryName(archive)!, "pending-crash.txt"), Path.GetFileName(archive)); }
        catch { }
    }

    public static SupportBundle? TakePending() => TakePendingIn(AppPaths.ReportsDirectory) ?? TakePendingIn(FallbackDirectory);

    internal static SupportBundle? TakePendingIn(string directory)
    {
        try
        {
            var marker = Path.Combine(directory, "pending-crash.txt");
            if (!File.Exists(marker) || new FileInfo(marker).Length > 100) return null;
            var name = File.ReadAllText(marker);
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"\Asupport-[0-9]{8}-[0-9]{6}-[0-9a-f]{32}\.zip\z")) return null;
            var archive = Path.Combine(directory, name);
            if (!File.Exists(archive) || (File.GetAttributes(archive) & FileAttributes.ReparsePoint) != 0) return null;
            File.Delete(marker);
            // Persisted report text is never copied into a public issue.
            var report = "The previous application session ended unexpectedly. This support report was not uploaded.\n";
            var body = report + $"\nDescribe what happened and manually attach `{name}` after reviewing it. GitHub issues are public.";
            return new(report, new Uri(IssuesUrl + "?title=Application%20terminated%20unexpectedly&body=" + Uri.EscapeDataString(body)),
                null, archive);
        }
        catch { return null; }
    }

    public static bool NeedsCaptureReport(Exception error) => error switch
    {
        OperationCanceledException => false,
        CaptureDiscardedException discarded => discarded.Message.StartsWith("Only ", StringComparison.Ordinal),
        _ => true
    };

    internal static SupportBundle CreateIn(string reports, string fallback, DesktopAuthProduct product, string version,
        SupportOperation operation, Exception error)
    {
        var name = product == DesktopAuthProduct.Toolkit ? "Tarkov Performance Toolkit" : "Tarkov Performance Benchmark";
        var safeVersion = Version.TryParse(version, out var parsed) ? parsed.ToString() : "unknown";
        var safeOperation = Enum.IsDefined(operation) ? operation.ToString() : "Application";
        var reason = error switch
        {
            PresentMonPermissionException => "trace_permission_required",
            PresentMonSessionException => "trace_session_conflict",
            CaptureDiscardedException => "insufficient_valid_duration",
            UnauthorizedAccessException => "storage_access_denied",
            InvalidDataException => "invalid_data",
            IOException => "storage_or_io_failure",
            _ => "unexpected_failure"
        };
        var report = $"{name}\nVersion: {safeVersion}\nOperation: {safeOperation}\nReason: {reason}\n" +
            $"Windows: {Environment.OSVersion.Version}\nUTC: {DateTimeOffset.UtcNow:O}\nError code: {error.HResult}\n" +
            (CaptureDiagnostics.Enabled ? $"Test build: {CaptureDiagnostics.BuildLabel}\n" : "") +
            "This support report was not uploaded. The log archive is local.\n";
        var stem = $"support-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        string? reportPath = null, archivePath = null;
        foreach (var directory in new[] { reports, fallback }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Directory.CreateDirectory(directory);
                var candidateReport = Path.Combine(directory, stem + ".txt");
                File.WriteAllText(candidateReport, report, new UTF8Encoding(false));
                reportPath = candidateReport;
                DiagnosticRetention.Trim(directory, "support-*.txt", 9, Path.GetFileName(candidateReport));
                var candidateZip = Path.Combine(directory, stem + ".zip");
                try
                {
                    using var file = new FileStream(candidateZip, FileMode.CreateNew, FileAccess.Write);
                    using var zip = new ZipArchive(file, ZipArchiveMode.Create);
                    WriteEntry(zip, "report.txt", report);
                    SupportLogExport.AddLogs(zip, reports);
                }
                catch
                {
                    try { File.Delete(candidateZip); } catch { }
                    throw;
                }
                archivePath = candidateZip;
                DiagnosticRetention.Trim(directory, "support-*.zip", 9, Path.GetFileName(candidateZip));
                break;
            }
            catch { /* The original failure must remain reportable even when storage is unavailable. */ }
        }
        var attachment = archivePath is null ? "No ZIP could be created. The sanitized report is included below."
            : $"Attach `{Path.GetFileName(archivePath)}` manually using GitHub's attachment control. The app has not uploaded it.";
        var body = "### What happened\nDescribe the steps and what you expected.\n\n### Diagnostic report\n```text\n" + report +
            "```\n\n" + attachment + "\n\nGitHub issues and attachments are public. Review the archive before attaching it.\n";
        var issue = new Uri(IssuesUrl + "?title=" + Uri.EscapeDataString($"{name}: {safeOperation} — {reason}") +
            "&body=" + Uri.EscapeDataString(body));
        return new(report, issue, reportPath, archivePath);
    }

    internal static void WriteEntry(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
