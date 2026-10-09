using System.Reflection;
using System.Reflection.Emit;
using TarkovSkills.Core.Academy;

namespace TarkovSkills.Core.Tests;

public sealed class ReviewBoundaryTests
{
    [Fact]
    public void UnavailableFallbackHasNoDependencyOnResourceAggregation()
    {
        var fallback = typeof(ResourceTelemetry).GetMethod("Unavailable", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.DoesNotContain(Calls(fallback), method => method.DeclaringType == typeof(ResourceSummary));
    }

    [Fact]
    public void FrozenRequestValidationDoesNotConstructOrProjectALocalBenchmarkRun()
    {
        var validate = typeof(SubmissionPayload).GetMethod("ValidateStored", BindingFlags.NonPublic | BindingFlags.Static)!;
        var calls = Calls(validate).ToArray();
        Assert.DoesNotContain(calls, method => method.DeclaringType == typeof(BenchmarkRun));
        Assert.DoesNotContain(calls, method => method.DeclaringType == typeof(SubmissionPayload) && method.Name == "Create");
    }

    // Architectural boundaries are intentional here: a successful fallback example
    // cannot prove independence from a failing aggregator. Follow helper calls too.
    private static IEnumerable<MethodBase> Calls(MethodBase root)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(opcode => unchecked((ushort)opcode.Value));
        var visited = new HashSet<MethodBase>();
        var pending = new Stack<MethodBase>(); pending.Push(root);
        while (pending.TryPop(out var method))
        {
            if (!visited.Add(method) || method.GetMethodBody()?.GetILAsByteArray() is not { } il) continue;
            for (var offset = 0; offset < il.Length;)
            {
                ushort code = il[offset++];
                if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
                var opcode = opcodes[code];
                if (opcode.OperandType == OperandType.InlineMethod)
                {
                    var called = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset),
                        method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null)!;
                    yield return called;
                    if (called.DeclaringType?.Assembly == root.DeclaringType!.Assembly) pending.Push(called);
                }
                offset += opcode.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                    _ => 4
                };
            }
        }
    }
}
