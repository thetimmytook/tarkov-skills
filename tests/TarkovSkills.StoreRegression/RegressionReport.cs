namespace TarkovSkills.StoreRegression;

public sealed record CaseResult(string Id, string Status, string Detail);

public sealed record RegressionReport(DateTimeOffset GeneratedAt, IReadOnlyList<CaseResult> Cases)
{
    public int ExitCode => Cases.Any(c => c.Status == "FAIL") ? 1
        : Cases.Any(c => c.Status == "BLOCKED") ? 2 : 0;
}

public sealed class TestFailure(string message) : Exception(message);
public sealed class TestBlocked(string message) : Exception(message);

public static class Check
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new TestFailure(message);
    }
}
