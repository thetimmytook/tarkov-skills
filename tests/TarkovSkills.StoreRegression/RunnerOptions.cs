namespace TarkovSkills.StoreRegression;

public sealed record RunnerOptions(bool GoalWrite, bool NoRaid, bool Capture,
    int Duration, string? ReportPath, bool Help = false)
{
    public static RunnerOptions Parse(string[] args)
    {
        bool goal = false, noRaid = false, capture = false, help = false;
        int duration = 120;
        string? report = null;
        for (var i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException();
            switch (args[i])
            {
                case "--goal-write": goal = true; break;
                case "--no-raid": noRaid = true; break;
                case "--capture": capture = true; break;
                case "--report": report = Value(); break;
                case "--duration":
                    if (!int.TryParse(Value(), out duration) || duration is not (120 or 240))
                        throw new ArgumentException();
                    break;
                case "--help": help = true; break;
                default: throw new ArgumentException();
            }
        }
        if (noRaid && capture) throw new ArgumentException();
        if (duration != 120 && !capture && !help) throw new ArgumentException();
        if (report is not null && string.IsNullOrWhiteSpace(report)) throw new ArgumentException();
        return new(goal, noRaid, capture, duration, report, help);
    }
}
