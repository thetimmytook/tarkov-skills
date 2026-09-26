using System.Diagnostics;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class AcademyBuildConfigurationTests
{
    [Theory]
    [InlineData("benchmark")]
    [InlineData("toolkit")]
    public async Task PackageValidatorRejectsDevelopmentApi(string product)
    {
        var repo = AuthBuildConfigurationTests.FindRepository();
        var path = Path.Combine(Path.GetTempPath(), "academy-invalid-package-" + Guid.NewGuid().ToString("N") + ".msix");
        try
        {
            // Minimal inspection fixture, never an installable or distributed package.
            using (var archive = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (var name in new[] { "AppxManifest.xml", "TarkovPerformanceBenchmark.exe", "TarkovPerformanceToolkit.exe", "TarkovSkills.exe",
                    "TarkovSkills.Core.dll", "TarkovBenchmark.Feature.dll", "tools/PresentMon/PresentMon.exe", "Assets/AppIcon.ico", "Assets/AppIcon.png",
                    "Assets/Square44x44Logo.png", "Assets/Square150x150Logo.png", "Assets/Wide310x150Logo.png", "Assets/StoreLogo.png" })
                    archive.CreateEntry(name);
                using (var destination = archive.CreateEntry("desktop-auth.json").Open())
                using (var source = File.OpenRead(Path.Combine(repo, "config", "desktop-auth.production.json"))) source.CopyTo(destination);
                using var writer = new StreamWriter(archive.CreateEntry("academy-api.json").Open());
                writer.Write("{\"baseUri\":\"http://127.0.0.1:8787/api/bench/v1\"}");
            }
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            // Let Windows PowerShell initialize its own module paths when tests run under pwsh.
            start.Environment.Remove("PSModulePath");
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File",
                Path.Combine(repo, "build", "test-msix-package.ps1"), "-Product", product, "-PackagePath", path }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(true); throw; }
            Assert.NotEqual(0, process.ExitCode);
            var output = await stdout + await stderr;
            Assert.True(output.Contains("Packaged academy-api.json differs", StringComparison.Ordinal), output);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("Debug", null, true, "https://api.example/api/bench/v1", true, false)]
    [InlineData("Release", null, true, "https://api.example/api/bench/v1", true, true)]
    [InlineData("Debug", "Production", true, "https://api.example/api/bench/v1", true, true)]
    [InlineData("Debug", "Production", true, null, false, false)]
    [InlineData("Release", null, true, null, false, false)]
    [InlineData("Release", "Development", true, "https://api.example/api/bench/v1", false, false)]
    [InlineData("Debug", "Other", true, "https://api.example/api/bench/v1", false, false)]
    [InlineData("Debug", "Production", true, "http://127.0.0.1:8787/api/bench/v1", false, false)]
    [InlineData("Debug", null, false, "https://api.example/api/bench/v1", true, false)]
    public async Task SelectionAndValidation(string config, string? environment, bool development, string? production, bool success, bool usesProduction)
    {
        var root = Path.Combine(Path.GetTempPath(), "academy-build-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "build"));
        Directory.CreateDirectory(Path.Combine(root, "config"));
        try
        {
            var repo = AuthBuildConfigurationTests.FindRepository();
            foreach (var name in new[] { "AcademyApi.props", "test-academy-api-config.ps1" })
                File.Copy(Path.Combine(repo, "build", name), Path.Combine(root, "build", name));
            const string local = "{\"baseUri\":\"http://127.0.0.1:8787/api/bench/v1\"}";
            if (development) File.WriteAllText(Path.Combine(root, "config", "academy-api.development.local.json"), local);
            var prodJson = System.Text.Json.JsonSerializer.Serialize(new { baseUri = production });
            if (production is not null) File.WriteAllText(Path.Combine(root, "config", "academy-api.production.json"), prodJson);
            Directory.CreateDirectory(Path.Combine(root, "publish"));
            var output = Path.Combine(root, "publish", "academy-api.json");
            File.WriteAllText(output, "stale");
            File.WriteAllText(Path.Combine(root, "fixture.proj"), """
                <Project>
                  <PropertyGroup><TargetDir>$(MSBuildProjectDirectory)/bin/</TargetDir><PublishDir>$(MSBuildProjectDirectory)/publish/</PublishDir></PropertyGroup>
                  <Import Project="build/AcademyApi.props" />
                  <Target Name="PrepareForBuild" />
                  <Target Name="Build" DependsOnTargets="PrepareForBuild">
                    <Copy SourceFiles="@(Content)" DestinationFiles="@(Content->'$(TargetDir)%(TargetPath)')" />
                  </Target>
                  <Target Name="Publish" DependsOnTargets="Build">
                    <Copy SourceFiles="@(Content)" DestinationFiles="@(Content->'$(PublishDir)%(TargetPath)')" />
                  </Target>
                </Project>
                """);
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment.Remove("AcademyApiEnvironment");
            foreach (var argument in new[] { "msbuild", "fixture.proj", "-t:Publish", "-nologo", "-v:quiet", "-p:Configuration=" + config })
                start.ArgumentList.Add(argument);
            if (environment is not null) start.ArgumentList.Add("-p:AcademyApiEnvironment=" + environment);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(true); throw; }
            Assert.True((process.ExitCode == 0) == success, await stdout + await stderr);
            if (!success) Assert.Equal("stale", File.ReadAllText(output));
            else if (usesProduction) Assert.Equal(prodJson, File.ReadAllText(output));
            else if (development) Assert.Equal(local, File.ReadAllText(output));
            else Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}

