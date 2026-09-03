using System.Xml.Linq;

namespace TarkovPerformanceBenchmark.Tests;

public sealed class MsixManifestTests
{
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap5 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";
    private static readonly XNamespace Uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
    private static readonly XNamespace Rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

    [Fact]
    public void StoreManifestUsesReservedIdentityAndStableAlias()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AppxManifest.template.xml"));
        var identity = document.Root!.Element(Foundation + "Identity")!;
        var application = document.Descendants(Foundation + "Application").Single();
        var alias = document.Descendants(Uap5 + "ExecutionAlias").Single();
        var capabilities = document.Descendants(Rescap + "Capability").Select(x => (string?)x.Attribute("Name")).ToArray();

        Assert.Equal("TimmyTook.TarkovPerformanceBenchmark", (string?)identity.Attribute("Name"));
        Assert.Equal("CN=55890398-71D9-4366-AF45-568B3BC3A786", (string?)identity.Attribute("Publisher"));
        Assert.Equal("x64", (string?)identity.Attribute("ProcessorArchitecture"));
        Assert.Equal("TarkovPerformanceBenchmark.exe", (string?)application.Attribute("Executable"));
        Assert.Equal("packagedClassicApp", (string?)application.Attribute(Uap10 + "RuntimeBehavior"));
        Assert.Equal("mediumIL", (string?)application.Attribute(Uap10 + "TrustLevel"));
        Assert.Equal("tarkov-benchmark.exe", (string?)alias.Attribute("Alias"));
        Assert.Contains("runFullTrust", capabilities);
        Assert.DoesNotContain("unvirtualizedResources", capabilities);
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "PublisherCacheFolders");
    }
}
