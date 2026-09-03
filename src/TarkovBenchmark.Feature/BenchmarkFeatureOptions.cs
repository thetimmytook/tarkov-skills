using TarkovSkills.Core;

namespace TarkovBenchmark.Feature;

public sealed record BenchmarkFeatureOptions(
    string ApplicationVersion,
    bool CollectRequested = false,
    bool SourceSkill = false,
    bool ShowCopyResults = false,
    Action<CommandResult>? ResultWriter = null)
{
    public static BenchmarkFeatureOptions ForToolkit(string applicationVersion) =>
        new(applicationVersion, ShowCopyResults: true);

    public static BenchmarkFeatureOptions ForStandalone(
        string applicationVersion,
        bool collectRequested,
        bool sourceSkill,
        Action<CommandResult>? resultWriter) =>
        new(
            applicationVersion,
            CollectRequested: collectRequested,
            SourceSkill: sourceSkill,
            ShowCopyResults: false,
            ResultWriter: resultWriter);
}
