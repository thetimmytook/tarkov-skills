using System.Text.Json;

namespace TarkovSkills.Core.Tests;

public sealed class PresentMonPermissionProbeTests
{
    [Fact]
    public async Task NativeProbeEitherRecordsOrReturnsPermissionRequiredWithoutUacOrCsv()
    {
        if (!CaptureDiagnostics.PermissionProbeEnabled)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => new PresentMonRunner().ProbeAsync(null, default));
            using var session = new Permissions.CaptureAccessSession();
            var approvals = 0;
            await Assert.ThrowsAsync<NotSupportedException>(() => session.TestCaptureAccessAsync(_ =>
            {
                approvals++;
                return Task.FromResult(true);
            }, default));
            Assert.Equal(0, approvals);
            return;
        }
        var folder = Path.Combine(Path.GetTempPath(), "capture-access-probe-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var log = CaptureDiagnosticLog.Create(folder, 3, true))
            {
                try { await new PresentMonRunner().ProbeAsync(log, default); }
                catch (PresentMonPermissionException) { /* Expected on a clean test account; never elevate or change its groups. */ }
            }
            var file = Assert.Single(Directory.GetFiles(folder));
            Assert.StartsWith("permission-probe-", Path.GetFileName(file));
            var rows = File.ReadLines(file).Select(line => JsonDocument.Parse(line)).ToArray();
            try
            {
                var exited = Assert.Single(rows, row => row.RootElement.GetProperty("stage").GetString() == "presentmon_exited");
                var data = exited.RootElement.GetProperty("data");
                Assert.True(data.GetProperty("exit_code").GetInt32() == 0 || data.GetProperty("access_denied_reported").GetBoolean());
            }
            finally { foreach (var row in rows) row.Dispose(); }
            Assert.DoesNotContain(Directory.GetFiles(AppContext.BaseDirectory), path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
