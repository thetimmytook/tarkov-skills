using System.Text.Json;
using TarkovSkills.InteractiveE2E;
using TarkovSkills.StoreRegression;

try
{
    var options = InteractiveOptions.Parse(args);
    if (options.Help)
    {
        Console.WriteLine("Interactive Store E2E (developer-only; never run by ordinary regression)\n" +
            "  --scenario account    Prepare/reuse GUI runs in both apps, then manually check login/restore/shared sign-out\n" +
            "  --scenario capture    Wait for a raid and consent, then automatically validate a live capture\n" +
            "  --scenario raid-end   Wait for a raid and consent; manually end it, validate discarded output\n" +
            "  --duration 120|240    Only for capture (default: 120)\n" +
            "  --wait-minutes 1..120 Raid-state wait deadline (default: 30); human prompts have no time limit\n" +
            "  --report <file.json>  Redacted results; no auth tokens, raw reports or captures");
        UserGate.WriteMenu(Console.Out);
        Console.WriteLine("The prompt shows ready OR pass, depending on the step.\n" +
            "Ctrl+C stops waiting; an active CLI capture is awaited.\n" +
            "No automatic login, input, elevation, Goal changes or uploads. Default invocation only shows help.");
        return 0;
    }
    RegressionReport report;
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
        report = new(DateTimeOffset.UtcNow, [new("E2E-INTERACTIVE", "BLOCKED", "Run in an interactive terminal; redirected/CI input is refused.")]);
    else
    {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
            Console.WriteLine("Stopping. Any already-started CLI capture must finish/discard before the runner exits.");
        };
        Console.CancelKeyPress += handler;
        try
        {
            report = await new InteractiveSuite(options, new UserGate(Console.In, Console.Out), Console.Out)
                .RunAsync(stop.Token);
        }
        finally { Console.CancelKeyPress -= handler; }
    }
    foreach (var result in report.Cases)
        Console.WriteLine($"{result.Status,-11} {result.Id}: {result.Detail}");
    if (options.ReportPath is not null)
    {
        var path = Path.GetFullPath(options.ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
    }
    return report.ExitCode;
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Invalid interactive E2E arguments. Use --help.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Interactive runner stopped ({exception.GetType().Name}); raw diagnostics withheld.");
    return 1;
}
