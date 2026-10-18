using System.IO.Compression;
using System.Text.Json;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Tests;

public sealed class SupportArchiveTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "support-tests-" + Guid.NewGuid().ToString("N"));
    private string Reports => Path.Combine(root, "reports");
    private string Fallback => Path.Combine(root, "fallback");

    [Fact]
    public void ArchiveContainsOnlyRevalidatedLogsAndReportAndIssueHasNoPrivatePaths()
    {
        Directory.CreateDirectory(Reports);
        var secret = @"PRIVATE-USER C:\Users\private-user\credentials 192.168.1.2 device-serial";
        var capture = Path.Combine(Reports, $"capture-20261018-120000-{Guid.NewGuid():N}.log");
        var row = JsonSerializer.Serialize(new { schema_version = 1, utc = DateTimeOffset.UtcNow, elapsed_seconds = 2,
            stage = "presentmon_exited", data = new { exit_code = 6, access_denied_reported = true, message = secret,
                role = secret, pid = 1234, path = secret, credentials = secret } });
        File.WriteAllText(capture, row + "\n{unfinished");
        File.WriteAllText(Path.Combine(Reports, "permission-setup.log"), JsonSerializer.Serialize(new
        { schema_version = 1, stage = "windows_capture_access", action = "Add", outcome = "Added", message = secret, account = secret }));
        foreach (var name in new[] { "benchmark.json", "private.credential", "capture-secrets.log", "raw.csv" })
            File.WriteAllText(Path.Combine(Reports, name), secret);
        var bundle = SupportArchive.CreateIn(Reports, Fallback, DesktopAuthProduct.Toolkit, secret,
            SupportOperation.Capture, new IOException(secret));
        Assert.NotNull(bundle.ReportPath); Assert.NotNull(bundle.ArchivePath);
        using var zip = ZipFile.OpenRead(bundle.ArchivePath);
        Assert.Equal(new[] { "capture-1.log", "permission-setup.log", "report.txt" }, zip.Entries.Select(e => e.FullName).Order().ToArray());
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            Assert.DoesNotContain("PRIVATE-USER", text); Assert.DoesNotContain("private-user", text);
            Assert.DoesNotContain("192.168", text); Assert.DoesNotContain("device-serial", text);
            Assert.DoesNotContain("credentials", text); Assert.DoesNotContain("\"pid\"", text);
        }
        var uri = Uri.UnescapeDataString(bundle.IssueUri.AbsoluteUri);
        Assert.StartsWith(SupportArchive.IssuesUrl, uri);
        Assert.Contains("manually", uri); Assert.Contains("public", uri);
        Assert.DoesNotContain(secret, uri); Assert.DoesNotContain(root, uri);
        Assert.DoesNotContain("C:\\Users", uri); Assert.Contains("Version: unknown", bundle.Report);
        Assert.Contains("storage_or_io_failure", bundle.Report);
    }

    [Fact]
    public void MissingOrMalformedLogsStillProduceAUsableSupportArchive()
    {
        Directory.CreateDirectory(Reports);
        File.WriteAllText(Path.Combine(Reports, $"capture-20261018-120000-{Guid.NewGuid():N}.log"), "invalid");
        File.WriteAllText(Path.Combine(Reports, "permission-setup.log"), "incomplete");
        var bundle = SupportArchive.CreateIn(Reports, Fallback, DesktopAuthProduct.Benchmark, "1.0.6", SupportOperation.Save, new Exception("private"));
        using var zip = ZipFile.OpenRead(bundle.ArchivePath!);
        Assert.Single(zip.Entries); Assert.Equal("report.txt", zip.Entries[0].FullName);
    }

    [Fact]
    public void UnwritableDataDirectoryFallsBackWithoutLeakingTheStorageError()
    {
        Directory.CreateDirectory(root); File.WriteAllText(Reports, "blocked");
        var bundle = SupportArchive.CreateIn(Reports, Fallback, DesktopAuthProduct.Toolkit, "1.0.3", SupportOperation.Save,
            new UnauthorizedAccessException(Reports));
        Assert.StartsWith(Fallback, bundle.ArchivePath);
        Assert.DoesNotContain(root, bundle.Report);
        Assert.DoesNotContain(root, Uri.UnescapeDataString(bundle.IssueUri.AbsoluteUri));
    }

    [Fact]
    public void FailureOfBothStorageLocationsStillOffersAnIssueWithASafeReport()
    {
        Directory.CreateDirectory(root); File.WriteAllText(Reports, "blocked"); File.WriteAllText(Fallback, "blocked");
        var bundle = SupportArchive.CreateIn(Reports, Fallback, DesktopAuthProduct.Toolkit, "1.0.3", SupportOperation.Save, new Exception("private"));
        Assert.Null(bundle.ArchivePath); Assert.Null(bundle.ReportPath);
        Assert.DoesNotContain("private", bundle.Report);
        Assert.Contains("No ZIP could be created", Uri.UnescapeDataString(bundle.IssueUri.AbsoluteUri));
    }

    [Fact]
    public void FatalCrashIsOfferedOnceOnNextLaunchWithoutTrustingPersistedReportText()
    {
        var bundle = SupportArchive.CreateIn(Reports, Fallback, DesktopAuthProduct.Toolkit, "1.0.3", SupportOperation.Application, new Exception());
        SupportArchive.MarkPending(bundle);
        File.WriteAllText(bundle.ReportPath!, "PRIVATE USER PATH");
        var restored = SupportArchive.TakePendingIn(Reports);
        Assert.NotNull(restored); Assert.Equal(bundle.ArchivePath, restored.ArchivePath);
        Assert.DoesNotContain("PRIVATE", restored.Report + restored.IssueUri.AbsoluteUri);
        Assert.Null(SupportArchive.TakePendingIn(Reports));
        File.WriteAllText(Path.Combine(Reports, "pending-crash.txt"), "../../private.zip");
        Assert.Null(SupportArchive.TakePendingIn(Reports));
    }

    [Fact]
    public void RetentionKeepsTwentyCaptureLogsAndTenSupportBundlesWithoutDeletingOtherFiles()
    {
        Directory.CreateDirectory(Reports);
        for (var i = 0; i < 25; i++)
        {
            var file = Path.Combine(Reports, $"capture-20261018-120000-{Guid.NewGuid():N}.log");
            File.WriteAllText(file, ""); File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-1).AddSeconds(i));
        }
        File.WriteAllText(Path.Combine(Reports, "capture-unrelated.log"), "preserve");
        using (CaptureDiagnosticLog.Create(Reports, 120)) { }
        Assert.Equal(21, Directory.GetFiles(Reports, "capture-*.log").Length); // 20 owned + unrelated
        for (var i = 0; i < 12; i++)
            SupportArchive.CreateIn(Reports, Fallback, DesktopAuthProduct.Toolkit, "1.0.3", SupportOperation.Save, new IOException());
        Assert.Equal(10, Directory.GetFiles(Reports, "support-*.zip").Length);
        Assert.Equal(10, Directory.GetFiles(Reports, "support-*.txt").Length);
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(Reports, "capture-unrelated.log")));
    }

    [Theory]
    [InlineData("cancelled", false)]
    [InlineData("Tarkov closed before capture completed. Partial data was discarded.", false)]
    [InlineData("The raid ended before capture completed. Partial data was discarded.", false)]
    [InlineData("Only 7.9 seconds of valid frame data were captured.", true)]
    public void ExpectedCancellationAndGameExitAreNotReportedAsBugs(string message, bool expected)
    {
        Exception error = message == "cancelled" ? new OperationCanceledException() : new CaptureDiscardedException(message);
        Assert.Equal(expected, SupportArchive.NeedsCaptureReport(error));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
