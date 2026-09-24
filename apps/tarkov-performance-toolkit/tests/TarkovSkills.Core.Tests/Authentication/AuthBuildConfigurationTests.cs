using System.Diagnostics;
using System.Text.Json;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class AuthBuildConfigurationTests
{
    [Theory]
    [InlineData("Benchmark")]
    [InlineData("Toolkit")]
    public async Task DebugUsesSharedDevelopmentFileAndReleaseAlwaysUsesProduction(string product)
    {
        using var fixture = new BuildFixture(product);
        fixture.WriteConfig("development.local", "fixture-development");
        fixture.WriteConfig("production", "fixture-production");
        Assert.Equal(0, (await fixture.Build("Debug")).ExitCode);
        Assert.Equal("fixture-development", fixture.PublishedClient());
        Assert.Equal(0, (await fixture.Build("Release")).ExitCode);
        Assert.Equal("fixture-production", fixture.PublishedClient());
        Assert.Equal(0, (await fixture.Build("Debug", "Production")).ExitCode);
        Assert.Equal("fixture-production", fixture.PublishedClient());
    }

    [Theory]
    [InlineData("Benchmark")]
    [InlineData("Toolkit")]
    public async Task MissingProductionFailsDespiteAvailableDevelopmentAndOldPropertyOverrides(string product)
    {
        using var fixture = new BuildFixture(product);
        fixture.WriteConfig("development.local", "fixture-development");
        var result = await fixture.Build("Release", extra: "-p:" + product + "DesktopAuthConfiguration=" + fixture.ConfigPath("development.local"));
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Development fallback is forbidden", result.Output);
        Assert.False(File.Exists(fixture.PublishedFile));
    }

    [Theory]
    [InlineData("Benchmark")]
    [InlineData("Toolkit")]
    public async Task ReleaseRefusesExplicitDevelopmentSelection(string product)
    {
        using var fixture = new BuildFixture(product);
        fixture.WriteConfig("development.local", "fixture-development");
        fixture.WriteConfig("production", "fixture-production");
        var result = await fixture.Build("Release", "Development");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Release builds require", result.Output);
    }

    [Theory]
    [InlineData("{\"issuer\":\"\",\"clientId\":\"\"}")]
    [InlineData("{\"issuer\":\"https://auth.example.com\",\"clientId\":\"fixture\",\"client_secret\":\"private-fixture\"}")]
    [InlineData("{\"issuer\":\"http://auth.example.com\",\"clientId\":\"fixture\"}")]
    public async Task InvalidProductionCannotFallBackToValidDevelopment(string invalid)
    {
        using var fixture = new BuildFixture("Benchmark");
        fixture.WriteConfig("development.local", "fixture-development");
        File.WriteAllText(fixture.ConfigPath("production"), invalid);
        var result = await fixture.Build("Release");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Desktop auth configuration must contain only", result.Output);
        Assert.DoesNotContain("private-fixture", result.Output);
        Assert.False(File.Exists(fixture.PublishedFile));
    }

    [Fact]
    public async Task DebugWithoutDevelopmentFileRemovesStaleAuthFromBuildAndPublish()
    {
        using var fixture = new BuildFixture("Toolkit");
        fixture.WriteConfig("production", "fixture-production");
        Assert.Equal(0, (await fixture.Build("Release")).ExitCode);
        Assert.True(File.Exists(fixture.PublishedFile));
        Assert.Equal(0, (await fixture.Build("Debug")).ExitCode);
        Assert.False(File.Exists(fixture.PublishedFile));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "bin", "desktop-auth.json")));
    }

    private sealed class BuildFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "desktop-auth-build-test-" + Guid.NewGuid().ToString("N"));
        public string PublishedFile => Path.Combine(Root, "publish", "desktop-auth.json");

        public BuildFixture(string product)
        {
            var repo = FindRepository();
            Directory.CreateDirectory(Path.Combine(Root, "build"));
            Directory.CreateDirectory(Path.Combine(Root, "config"));
            foreach (var file in new[] { "DesktopAuth.props", "test-desktop-auth-config.ps1" })
                File.Copy(Path.Combine(repo, "build", file), Path.Combine(Root, "build", file));
            // Exercise the real imported selection/validation/copy metadata without rebuilding WPF.
            File.WriteAllText(Path.Combine(Root, "fixture.proj"), """
                <Project>
                  <PropertyGroup>
                    <TargetDir>$(MSBuildProjectDirectory)/bin/</TargetDir>
                    <PublishDir>$(MSBuildProjectDirectory)/publish/</PublishDir>
                  </PropertyGroup>
                  <Import Project="build/DesktopAuth.props" />
                  <Target Name="PrepareForBuild" />
                  <Target Name="Build" DependsOnTargets="PrepareForBuild">
                    <Copy SourceFiles="@(Content)" DestinationFiles="@(Content->'$(TargetDir)%(TargetPath)')" />
                  </Target>
                  <Target Name="Publish" DependsOnTargets="Build">
                    <Copy SourceFiles="@(Content)" DestinationFiles="@(Content->'$(PublishDir)%(TargetPath)')" />
                  </Target>
                </Project>
                """);
            Product = product;
        }

        private string Product { get; }
        public string ConfigPath(string suffix) => Path.Combine(Root, "config", "desktop-auth." + suffix + ".json");
        public void WriteConfig(string suffix, string client) => File.WriteAllText(ConfigPath(suffix),
            JsonSerializer.Serialize(new { issuer = "https://auth.example.com", clientId = client }));
        public string PublishedClient()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(PublishedFile));
            return document.RootElement.GetProperty("clientId").GetString()!;
        }

        public async Task<(int ExitCode, string Output)> Build(string configuration, string? environment = null, string? extra = null)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.Environment.Remove("DesktopAuthEnvironment");
            foreach (var arg in new[] { "msbuild", "fixture.proj", "-t:Publish", "-nologo", "-v:quiet",
                "-p:Configuration=" + configuration, "-p:AssemblyName=TarkovPerformance" + Product })
                start.ArgumentList.Add(arg);
            if (environment is not null) start.ArgumentList.Add("-p:DesktopAuthEnvironment=" + environment);
            if (extra is not null) start.ArgumentList.Add(extra);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(entireProcessTree: true); throw; }
            return (process.ExitCode, await output + await error);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    internal static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "build", "DesktopAuth.props")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository build tooling not found.");
    }
}
