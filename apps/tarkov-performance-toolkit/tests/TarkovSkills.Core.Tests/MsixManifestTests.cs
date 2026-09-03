using System.Xml.Linq;

namespace TarkovSkills.Core.Tests;

public sealed class MsixManifestTests
{
    [Fact]
    public void ManifestUsesReservedStoreIdentity()
    {
        var manifest = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AppxManifest.template.xml"));
        var identity = manifest.Descendants().Single(element => element.Name.LocalName == "Identity");
        Assert.Equal("TimmyTook.TarkovPerformanceToolkit", identity.Attribute("Name")?.Value);
        Assert.Equal("CN=55890398-71D9-4366-AF45-568B3BC3A786", identity.Attribute("Publisher")?.Value);
    }

    [Fact]
    public void ManifestUsesGuiLaunchAndConsoleAlias()
    {
        var manifest = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AppxManifest.template.xml"));
        var application = manifest.Descendants().Single(element => element.Name.LocalName == "Application");
        var alias = manifest.Descendants().Single(element => element.Name.LocalName == "ExecutionAlias");
        Assert.Equal("TarkovPerformanceToolkit.exe", application.Attribute("Executable")?.Value);
        Assert.Equal("tarkov-skills.exe", alias.Attribute("Alias")?.Value);
        Assert.Equal("TarkovSkills.exe", alias.Parent?.Parent?.Attribute("Executable")?.Value);
    }

    [Fact]
    public void ManifestRequestsOnlyRunFullTrust()
    {
        var manifest = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AppxManifest.template.xml"));
        var capabilities = manifest.Descendants().Where(element => element.Name.LocalName == "Capability").Select(element => element.Attribute("Name")?.Value).ToList();
        Assert.Equal(["runFullTrust"], capabilities);
        Assert.DoesNotContain(manifest.Descendants(), element => element.Name.LocalName == "PublisherCacheFolders");
    }
}
