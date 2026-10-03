using System.Text.Json;
using TarkovSkills.InteractiveE2E;
using TarkovSkills.StoreRegression;

namespace TarkovSkills.Regression.Tests;

public sealed class InteractiveE2ETests
{
    [Fact]
    public void DefaultInvocationIsOnlyHelp()
    {
        var options = InteractiveOptions.Parse([]);
        Assert.True(options.Help);
        Assert.Null(options.Scenario);
    }

    [Theory]
    [InlineData("--scenario", "all")]
    [InlineData("--scenario", "account", "--duration", "120")]
    [InlineData("--scenario", "raid-end", "--duration", "240")]
    [InlineData("--scenario", "capture", "--duration", "60")]
    [InlineData("--scenario", "capture", "--wait-minutes", "0")]
    [InlineData("--scenario", "capture", "--wait-minutes", "121")]
    [InlineData("--scenario", "capture", "--scenario", "account")]
    [InlineData("--report")]
    [InlineData("--report", "result.json")]
    public void InvalidOrAmbiguousArgumentsAreRejected(params string[] args) =>
        Assert.Throws<ArgumentException>(() => InteractiveOptions.Parse(args));

    [Fact]
    public async Task EmptyInputAndEnterAreNotConsent()
    {
        var log = new StringWriter();
        var gate = new UserGate(new StringReader("\nsecret-not-a-command\nready\n"), log);
        await gate.ReadyAsync("Do the step", CancellationToken.None);
        Assert.Equal(2, log.ToString().Split("Input not accepted.").Length - 1);
        Assert.Equal(3, log.ToString().Split("Choose an action:").Length - 1);
        Assert.DoesNotContain("secret-not-a-command", log.ToString());
    }

    [Theory]
    [InlineData(false, "ready", "  ready - continue", "  pass  -")]
    [InlineData(true, "pass", "  pass  - confirm the observed result", "  ready -")]
    public async Task EveryPromptUsesASeparatedMenu(bool confirmation, string answer, string acceptedLine, string excludedLine)
    {
        var log = new StringWriter();
        var gate = new UserGate(new StringReader(answer + "\n"), log);
        if (confirmation) await gate.ConfirmAsync("Complete the requested action.", CancellationToken.None);
        else await gate.ReadyAsync("Complete the requested action.", CancellationToken.None);
        var text = log.ToString().Replace("\r\n", "\n");
        Assert.Contains("ACTION REQUIRED:\nComplete the requested action.\n\nChoose an action:\n", text);
        Assert.Contains(acceptedLine + "\n  fail  - failed step\n  skip  - blocked\n\nEmpty input is not consent.\n\n> ", text);
        Assert.DoesNotContain(excludedLine, text);
        Assert.DoesNotContain("Type ready to continue;", text);
    }

    [Fact]
    public void HelpMenuShowsEachInputOnItsOwnLine()
    {
        var log = new StringWriter();
        UserGate.WriteMenu(log);
        Assert.Equal(["Choose an action:", "  ready - continue", "  pass  - confirm the observed result",
            "  fail  - failed step", "  skip  - blocked", "Empty input is not consent."],
            log.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task UserCanStopABlockedTerminalRead()
    {
        using var stop = new CancellationTokenSource();
        using var input = new BlockingReader();
        var waiting = new UserGate(input, TextWriter.Null).ReadyAsync("Wait for action", stop.Token);
        try
        {
            await input.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { input.Release.TrySetResult(null); }
    }

    [Theory]
    [InlineData("skip\n", false)]
    [InlineData("", false)]
    [InlineData("fail\n", true)]
    public async Task SkippedFailedOrEndedInputIsNotPass(string answer, bool failed)
    {
        var gate = new UserGate(new StringReader(answer), TextWriter.Null);
        if (failed) await Assert.ThrowsAsync<TestFailure>(() => gate.ConfirmAsync("Check UI", CancellationToken.None));
        else await Assert.ThrowsAsync<TestBlocked>(() => gate.ConfirmAsync("Check UI", CancellationToken.None));
    }

    [Fact]
    public async Task MissingPackageDoesNotRequestUserActions()
    {
        var log = new StringWriter();
        var suite = new InteractiveSuite(new("capture"), new UserGate(new StringReader(""), log), log,
            _ => throw new TestBlocked("Package not installed."));
        var report = await suite.RunAsync();
        Assert.Equal(2, report.ExitCode);
        Assert.Equal("NOT RUN", report.Cases.Last().Status);
        Assert.DoesNotContain("ACTION REQUIRED", log.ToString());
    }

    [Fact]
    public async Task AccountPassIsManualAndNeverExecutesCommands()
    {
        var client = new FakeCommands();
        var products = new List<StoreProduct>();
        var log = new StringWriter();
        var report = await Suite("account", "ready\npass\npass\npass\npass\npass\npass\n", client, log,
            factory: product => { products.Add(product); return client; }).RunAsync();
        Assert.Equal([StoreProduct.Toolkit, StoreProduct.Benchmark], products);
        Assert.Empty(client.Calls);
        Assert.Equal("MANUAL PASS", report.Cases.Last().Status);
        Assert.Equal(0, report.ExitCode);
        var text = log.ToString();
        Assert.True(text.IndexOf("STEP 1 - Prepare Toolkit", StringComparison.Ordinal) < text.IndexOf("STEP 3 - Sign in", StringComparison.Ordinal));
        Assert.True(text.IndexOf("STEP 2 - Prepare standalone Benchmark", StringComparison.Ordinal) < text.IndexOf("STEP 3 - Sign in", StringComparison.Ordinal));
        Assert.Contains("CLI capture tests do NOT add a run to GUI history.", text);
        Assert.Contains("SEPARATE local histories", text);
        Assert.Contains("NOT compiling the apps", text);
    }

    [Fact]
    public async Task AccountPreparationCanBeSkippedWithoutLoginOrCommands()
    {
        var client = new FakeCommands();
        var log = new StringWriter();
        var report = await Suite("account", "ready\nskip\n", client, log).RunAsync();
        Assert.Empty(client.Calls);
        Assert.Equal(2, report.ExitCode);
        Assert.DoesNotContain("STEP 3 - Sign in", log.ToString());
        Assert.DoesNotContain("STEP 5 - Shared sign-out", log.ToString());
    }

    [Fact]
    public async Task SkippingCaptureConsentNeverStartsCapture()
    {
        var client = new FakeCommands(_ => Status(true));
        var report = await Suite("capture", "ready\nskip\n", client).RunAsync();
        Assert.Equal(2, report.ExitCode);
        Assert.All(client.Calls, args => Assert.Equal("status", args[0]));
    }

    [Fact]
    public async Task CapturePollsForRaidThenRequiresSeparateConsent()
    {
        var statuses = new Queue<bool>([false, true, true]);
        var client = new FakeCommands(args => args[0] == "status" ? Status(statuses.Dequeue()) : CompleteCapture);
        var report = await Suite("capture", "ready\nready\n", client).RunAsync();
        Assert.Equal("PASS", report.Cases.Last().Status);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(["status", "status", "status", "capture"], client.Calls.Select(args => args[0]));
        Assert.Equal(["capture", "--duration", "120"], client.Calls.Last());
    }

    [Fact]
    public async Task RaidEndRequiresDiscardedJsonAndInactiveLiveGame()
    {
        var pending = new TaskCompletionSource<CommandOutput>(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusCalls = 0;
        var client = new FakeCommands(args =>
        {
            if (args[0] == "capture") return pending.Task;
            statusCalls++;
            if (statusCalls == 3) pending.SetResult(DiscardedCapture);
            return Task.FromResult(Status(statusCalls < 4));
        });
        var report = await Suite("raid-end", "ready\nready\n", client).RunAsync();
        Assert.Equal("PASS", report.Cases.Last().Status);
        Assert.Equal(4, statusCalls);
        Assert.True(pending.Task.IsCompleted);
    }

    [Fact]
    public async Task EarlyPermissionFailureIsBlockedWithoutRaidExitPrompt()
    {
        var log = new StringWriter();
        var client = new FakeCommands(args => args[0] == "status" ? Status(true)
            : new(10, "{\"status\":\"permission_required\",\"message\":\"permission\"}", ""));
        var report = await Suite("raid-end", "ready\nready\n", client, log).RunAsync();
        Assert.Equal(2, report.ExitCode);
        Assert.DoesNotContain("LEAVE RAID NOW", log.ToString());
    }

    [Fact]
    public async Task StopDuringCaptureAwaitsOwnedCommandAndDoesNotClaimPass()
    {
        using var stop = new CancellationTokenSource();
        var completed = false;
        var client = new FakeCommands(async args =>
        {
            if (args[0] == "status") return Status(true);
            stop.Cancel();
            await Task.Yield();
            completed = true;
            return CompleteCapture;
        });
        var report = await Suite("capture", "ready\nready\n", client).RunAsync(stop.Token);
        Assert.True(completed);
        Assert.Equal(2, report.ExitCode);
        Assert.Equal("BLOCKED", report.Cases.Last().Status);
    }

    [Theory]
    [InlineData(0, "{\"status\":\"discarded\",\"message\":\"raid ended\"}")]
    [InlineData(12, "{\"status\":\"discarded\",\"message\":\"raid ended\",\"performance\":{}}")]
    [InlineData(12, "{\"status\":\"discarded\",\"message\":\"insufficient frames\"}")]
    public void InvalidDiscardResultsNeverPass(int exit, string json) =>
        Assert.Throws<TestFailure>(() => CaptureAssertions.Discarded(new(exit, json, "")));

    [Fact]
    public void CompleteCaptureChecksMetricOrdering()
    {
        CaptureAssertions.Complete(CompleteCapture, 120);
        var bad = CompleteCapture with { Stdout = CompleteCapture.Stdout.Replace("\"one_percent_low_fps\":70", "\"one_percent_low_fps\":120") };
        Assert.Throws<TestFailure>(() => CaptureAssertions.Complete(bad, 120));
    }

    private static InteractiveSuite Suite(string scenario, string answers, FakeCommands client,
        TextWriter? log = null, Func<StoreProduct, IStoreCommands>? factory = null) =>
        new(new(scenario), new UserGate(new StringReader(answers), log ?? TextWriter.Null), log ?? TextWriter.Null,
            factory ?? (_ => client), (_, cancellation) => { cancellation.ThrowIfCancellationRequested(); return Task.CompletedTask; });

    private static CommandOutput Status(bool active) => new(0,
        JsonSerializer.Serialize(new { toolkit_version = "1.0.2", presentmon_ready = true, tarkov_running = true, raid_active = active }), "");

    private static CommandOutput DiscardedCapture => new(12,
        "{\"status\":\"discarded\",\"message\":\"The raid ended before capture completed. Partial data was discarded.\"}", "");

    private static CommandOutput CompleteCapture => new(0,
        "{\"inspection\":{},\"performance\":{\"duration_sec\":120,\"sample_count\":12000," +
        "\"average_fps\":100,\"one_percent_low_fps\":70,\"zero_point_one_percent_low_fps\":50," +
        "\"average_frametime_ms\":10,\"p95_frametime_ms\":15,\"p99_frametime_ms\":20}}", "");

    private sealed class FakeCommands : IStoreCommands
    {
        private readonly Func<string[], Task<CommandOutput>> run;
        public string Version => "1.0.2";
        public List<string[]> Calls { get; } = [];
        public FakeCommands() : this(new Func<string[], CommandOutput>(_ => throw new TestFailure("No command expected."))) { }
        public FakeCommands(Func<string[], CommandOutput> run) : this(args => Task.FromResult(run(args))) { }
        public FakeCommands(Func<string[], Task<CommandOutput>> run) => this.run = run;
        public Task<CommandOutput> RunAsync(string[] args, int timeoutSeconds = 45)
        {
            Calls.Add(args);
            return run(args);
        }
    }

    private sealed class BlockingReader : TextReader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string?> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override string? ReadLine()
        {
            Started.SetResult();
            return Release.Task.GetAwaiter().GetResult();
        }
    }
}
