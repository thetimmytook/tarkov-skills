namespace TarkovSkills.InteractiveE2E;

public sealed record InteractiveOptions(string? Scenario, int Duration = 120, int WaitMinutes = 30,
    string? ReportPath = null, bool Help = false)
{
    public static InteractiveOptions Parse(string[] args)
    {
        string? scenario = null, report = null;
        int duration = 120, wait = 30;
        bool help = args.Length == 0, hasDuration = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException();
            if (!seen.Add(args[i])) throw new ArgumentException();
            switch (args[i])
            {
                case "--scenario": scenario = Value(); break;
                case "--report": report = Value(); break;
                case "--duration":
                    hasDuration = true;
                    if (!int.TryParse(Value(), out duration) || duration is not (120 or 240)) throw new ArgumentException();
                    break;
                case "--wait-minutes":
                    if (!int.TryParse(Value(), out wait) || wait is < 1 or > 120) throw new ArgumentException();
                    break;
                case "--help": help = true; break;
                default: throw new ArgumentException();
            }
        }
        if (scenario is not (null or "account" or "capture" or "raid-end")) throw new ArgumentException();
        if (!help && scenario is null) throw new ArgumentException();
        if (hasDuration && scenario != "capture") throw new ArgumentException();
        if (report is not null && string.IsNullOrWhiteSpace(report)) throw new ArgumentException();
        return new(scenario, duration, wait, report, help);
    }
}
