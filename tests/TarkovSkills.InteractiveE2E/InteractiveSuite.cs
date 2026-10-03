using TarkovSkills.StoreRegression;

namespace TarkovSkills.InteractiveE2E;

public sealed partial class InteractiveSuite
{
    private readonly InteractiveOptions options;
    private readonly IUserGate gate;
    private readonly TextWriter output;
    private readonly Func<StoreProduct, IStoreCommands> createClient;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly List<CaseResult> cases = [];
    private IStoreCommands client = null!;

    public InteractiveSuite(InteractiveOptions options, IUserGate gate, TextWriter output,
        Func<StoreProduct, IStoreCommands>? createClient = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.options = options;
        this.gate = gate;
        this.output = output;
        this.createClient = createClient ?? (product => new StoreAliasClient(product));
        this.delay = delay ?? Task.Delay;
    }

    public async Task<RegressionReport> RunAsync(CancellationToken cancellation = default)
    {
        var ready = await CaseAsync("E2E-INSTALLED", () =>
        {
            client = createClient(StoreProduct.Toolkit);
            var detail = $"Signed Store Toolkit {client.Version}; registered alias.";
            if (options.Scenario == "account")
                detail += $" Signed Store Benchmark {createClient(StoreProduct.Benchmark).Version}; registered alias.";
            return Task.FromResult(detail);
        });
        if (!ready)
            cases.Add(new("E2E-" + options.Scenario, "NOT RUN", "Installed-package prerequisite did not pass."));
        else
        {
            switch (options.Scenario)
            {
                case "account": await CaseAsync("E2E-ACCOUNT", () => AccountAsync(cancellation), manual: true); break;
                case "capture": await CaseAsync("E2E-CAPTURE", () => CaptureAsync(cancellation)); break;
                case "raid-end": await CaseAsync("E2E-RAID-END", () => RaidEndAsync(cancellation)); break;
                default: throw new ArgumentException();
            }
        }
        return new(DateTimeOffset.UtcNow, cases);
    }

    private async Task<bool> CaseAsync(string id, Func<Task<string>> action, bool manual = false)
    {
        output.WriteLine($"RUN      {id}");
        try
        {
            cases.Add(new(id, manual ? "MANUAL PASS" : "PASS", await action()));
            return true;
        }
        catch (OperationCanceledException) { cases.Add(new(id, "BLOCKED", "Interactive scenario stopped by user.")); }
        catch (TestBlocked exception) { cases.Add(new(id, "BLOCKED", exception.Message)); }
        catch (TestFailure exception) { cases.Add(new(id, "FAIL", exception.Message)); }
        catch (Exception exception) { cases.Add(new(id, "FAIL", $"Unexpected {exception.GetType().Name}; raw diagnostics withheld.")); }
        return false;
    }
}
