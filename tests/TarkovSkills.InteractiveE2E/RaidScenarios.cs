using TarkovSkills.StoreRegression;

namespace TarkovSkills.InteractiveE2E;

public sealed partial class InteractiveSuite
{
    private async Task<string> CaptureAsync(CancellationToken cancellation)
    {
        await EnterRaidAsync(cancellation);
        await gate.ReadyAsync($"Active raid detected. Permit one {options.Duration}s bundled PresentMon capture? " +
            "Stay in the raid until it finishes. No GUI, history append or upload is requested.", cancellation);
        Check.That(await RaidActiveAsync(cancellation), "Raid ended before capture consent was confirmed.");
        output.WriteLine($"Capturing for {options.Duration}s. An already-started command is awaited even if you stop the runner.");
        var result = await client.RunAsync(["capture", "--duration", options.Duration.ToString()], options.Duration + 45);
        cancellation.ThrowIfCancellationRequested();
        CaptureAssertions.Complete(result, options.Duration);
        return $"Automatic: complete {options.Duration}s capture; one sanitized JSON object, valid metrics/context. Raw output is not retained; no upload command issued.";
    }

    private async Task<string> RaidEndAsync(CancellationToken cancellation)
    {
        await EnterRaidAsync(cancellation);
        await gate.ReadyAsync("Permit a 120s capture that will be deliberately discarded? " +
            "After the LEAVE RAID prompt, manually extract or end this test raid while keeping Tarkov open. No game input is automated.", cancellation);
        Check.That(await RaidActiveAsync(cancellation), "Raid ended before the discard test started.");
        output.WriteLine("Starting the capture command. Wait for LEAVE RAID before ending the raid.");
        var capture = client.RunAsync(["capture", "--duration", "120"], 165);
        try
        {
            // There is no public CLI capture-start event. Avoid claiming the ETW timer is observable.
            await delay(TimeSpan.FromSeconds(8), cancellation);
            if (capture.IsCompleted)
            {
                CaptureAssertions.CheckAvailability(await capture);
                throw new TestFailure("Capture command ended before the manual raid-end action.");
            }
            Check.That(await RaidActiveAsync(cancellation), "Raid ended before the manual action prompt.");
            output.WriteLine("ACTION REQUIRED — LEAVE RAID NOW: manually extract/end the raid before the 120s command completes. Keep Tarkov open. No Enter is needed; waiting for the CLI result.");
            var result = await capture;
            cancellation.ThrowIfCancellationRequested();
            CaptureAssertions.Discarded(result);
            await WaitForRaidAsync(expected: false, cancellation);
            return "Automatic: active -> inactive raid, exit 12/discarded for raid end, no partial metrics/context in stdout. No upload command issued; package history not inspected.";
        }
        finally
        {
            // Never leave an owned command running after an interrupted human step.
            // Do not kill the game or external captures; let the bounded CLI invocation finish.
            await capture;
        }
    }

    private async Task EnterRaidAsync(CancellationToken cancellation)
    {
        await gate.ReadyAsync("Launch Tarkov, log in yourself and enter a test raid. Prefer a Local raid. " +
            "Type ready once gameplay is loaded; status polling will verify an active raid. No capture starts at this step.", cancellation);
        await WaitForRaidAsync(expected: true, cancellation);
    }

    private async Task WaitForRaidAsync(bool expected, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(options.WaitMinutes));
        output.WriteLine($"Waiting up to {options.WaitMinutes} min for raid_active={expected.ToString().ToLowerInvariant()} through the Store alias; Ctrl+C stops waiting.");
        try
        {
            while (await RaidActiveAsync(deadline.Token) != expected)
                await delay(TimeSpan.FromSeconds(2), deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new TestBlocked("Expected raid state was not detected before the wait deadline.");
        }
    }

    private async Task<bool> RaidActiveAsync(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var result = await client.RunAsync(["status"]);
        cancellation.ThrowIfCancellationRequested();
        using var json = result.Json();
        CaptureAssertions.Audit(json.RootElement);
        if (result.ExitCode != 0 || !json.RootElement.GetProperty("presentmon_ready").GetBoolean())
            throw new TestBlocked("Bundled capture dependency is not ready.");
        Check.That(json.RootElement.GetProperty("toolkit_version").GetString() == client.Version,
            "Store package and CLI version disagree.");
        var active = json.RootElement.GetProperty("raid_active").GetBoolean();
        var running = json.RootElement.GetProperty("tarkov_running").GetBoolean();
        Check.That(!active || running, "Active raid reported while Tarkov is closed.");
        if (options.Scenario == "raid-end" && !active && !running)
            throw new TestFailure("Tarkov was closed; this scenario requires ending the raid with the game still open.");
        return active;
    }
}
