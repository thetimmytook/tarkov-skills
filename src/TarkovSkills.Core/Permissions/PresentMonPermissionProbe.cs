using System.Diagnostics;

namespace TarkovSkills.Core;

public sealed partial class PresentMonRunner
{
    // Diagnostic build only. Exercise the same ETW providers without requiring a
    // game, creating CSV, or treating the probe as a benchmark result.
    internal async Task ProbeAsync(CaptureDiagnosticLog? diagnostic, CancellationToken cancellation)
    {
        if (!CaptureDiagnostics.PermissionProbeEnabled) throw new NotSupportedException("The permission probe is not enabled in this build.");
        VerifyDependency();
        await CleanupLegacyOrphanedSessionAsync(diagnostic).ConfigureAwait(false);
        await StopSessionAsync(OwnedSessionName, diagnostic, CaptureProcessRole.prepare_cleanup).ConfigureAwait(false);
        var info = new ProcessStartInfo(AppPaths.PresentMonFile)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in BuildProbeArguments()) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (!process.Start()) throw new InvalidOperationException("The permission probe could not start.");
            _ownProcess?.Invoke(process);
            var clock = Stopwatch.StartNew();
            using var output = diagnostic is null ? null : new PresentMonDiagnosticOutput(process.StandardOutput,
                diagnostic, CaptureProcessRole.capture, CaptureOutputStream.stdout);
            using var error = diagnostic is null ? null : new PresentMonDiagnosticOutput(process.StandardError,
                diagnostic, CaptureProcessRole.capture, CaptureOutputStream.stderr);
            using var registration = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
            var stdout = output?.Completion ?? process.StandardOutput.ReadToEndAsync();
            var stderr = error?.Completion ?? process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            finally
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                await process.WaitForExitAsync().ConfigureAwait(false);
                if (diagnostic is not null)
                {
                    await PresentMonDiagnosticOutput.DrainAsync(output!, error!).ConfigureAwait(false);
                    diagnostic.ProcessExited(CaptureProcessRole.capture, process.ExitCode, clock.Elapsed.TotalSeconds,
                        cancellation.IsCancellationRequested, timeout.IsCancellationRequested && !cancellation.IsCancellationRequested,
                        output!.Snapshot(), error!.Snapshot());
                }
            }
            _ = await stdout.ConfigureAwait(false); var failure = await stderr.ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw CreateExitException(process.ExitCode, failure);
            // Pinned PresentMon suppresses recording start/stop messages with --no_csv.
            // Trace initialization failures return nonzero; require the timed interval
            // as well so a successful immediate exit cannot pass this probe.
            if (clock.Elapsed.TotalSeconds < 2.5)
                throw new InvalidDataException("The permission probe ended before the tracing interval completed.");
        }
        finally
        {
            try { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync().ConfigureAwait(false); } } catch { }
            await StopSessionAsync(OwnedSessionName, diagnostic, CaptureProcessRole.final_cleanup).ConfigureAwait(false);
        }
    }

    internal static IReadOnlyList<string> BuildProbeArguments() => ["--session_name", OwnedSessionName,
        "--stop_existing_session", "--process_name", "EscapeFromTarkov.exe", "--timed", "3", "--terminate_after_timed",
        "--no_csv", "--no_console_stats"];
}
