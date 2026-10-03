using System.Text.Json;
using TarkovSkills.StoreRegression;

try
{
    var options = RunnerOptions.Parse(args);
    if (options.Help)
    {
        Console.WriteLine("Store CLI regression (developer tool, Windows x64)\n" +
            "  --report <file.json>   Write a redacted test report\n" +
            "  --goal-write           Temporarily change Goal; restore its values\n" +
            "                         Goal timestamp/source can change through the CLI\n" +
            "  --no-raid              Test rejected captures outside a raid\n" +
            "  --capture              Explicitly allow one live PresentMon capture\n" +
            "  --duration 120|240     Live capture duration (default: 120)\n" +
            "Default: read-only smoke/validation checks. Never uploads or opens GUI.");
        return 0;
    }
    var report = await new RegressionSuite(options).RunAsync();
    foreach (var result in report.Cases)
        Console.WriteLine($"{result.Status,-8} {result.Id}: {result.Detail}");
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
    Console.Error.WriteLine("Invalid runner arguments. Use --help.");
    return 2;
}
catch (Exception exception)
{
    // Do not expose exception messages, paths, command output or user Goal text.
    Console.Error.WriteLine($"Regression runner stopped ({exception.GetType().Name}).");
    return 1;
}
