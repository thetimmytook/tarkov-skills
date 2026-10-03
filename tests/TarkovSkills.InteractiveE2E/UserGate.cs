using TarkovSkills.StoreRegression;

namespace TarkovSkills.InteractiveE2E;

public interface IUserGate
{
    Task ReadyAsync(string instruction, CancellationToken cancellation);
    Task ConfirmAsync(string instruction, CancellationToken cancellation);
}

public sealed class UserGate(TextReader input, TextWriter output) : IUserGate
{
    public Task ReadyAsync(string instruction, CancellationToken cancellation) =>
        AskAsync(instruction, "ready", cancellation);

    public Task ConfirmAsync(string instruction, CancellationToken cancellation) =>
        AskAsync(instruction, "pass", cancellation);

    private async Task AskAsync(string instruction, string accept, CancellationToken cancellation)
    {
        output.WriteLine();
        output.WriteLine("ACTION REQUIRED:");
        output.WriteLine(instruction);
        WriteMenu(output, accept);
        while (true)
        {
            output.Write("> ");
            // Console.In can perform a synchronous terminal read even through ReadLineAsync.
            // Keep it off the main thread so Ctrl+C can cancel the wait. No further reads are
            // started after cancellation; a pending terminal read dies with this developer process.
            var answer = await Task.Run(input.ReadLine, cancellation).WaitAsync(cancellation);
            if (answer is null) throw new TestBlocked("Interactive input ended; step was not confirmed.");
            switch (answer.Trim().ToLowerInvariant())
            {
                case "fail": throw new TestFailure("User reported that the requested step failed.");
                case "skip": throw new TestBlocked("User skipped the required action.");
                default:
                    if (answer.Trim().Equals(accept, StringComparison.OrdinalIgnoreCase)) return;
                    output.WriteLine("Input not accepted. Choose an action from the menu.");
                    WriteMenu(output, accept);
                    break;
            }
        }
    }

    public static void WriteMenu(TextWriter output, string? accept = null)
    {
        output.WriteLine();
        output.WriteLine("Choose an action:");
        if (accept is null or "ready") output.WriteLine("  ready - continue");
        if (accept is null or "pass") output.WriteLine("  pass  - confirm the observed result");
        output.WriteLine("  fail  - failed step");
        output.WriteLine("  skip  - blocked");
        output.WriteLine();
        output.WriteLine("Empty input is not consent.");
        output.WriteLine();
    }
}
