using System.Text.Json;

namespace TarkovSkills.Core.Academy;

public sealed record AcademyConfiguration(Uri BaseUri, bool AllowLoopbackHttp)
{
    public static AcademyConfiguration Read(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 ||
            !root.TryGetProperty("baseUri", out var endpoint)) throw new InvalidDataException("Invalid API configuration.");
        var uri = new Uri(endpoint.GetString()!, UriKind.Absolute);
#if DEBUG
        const bool allowLocal = true;
#else
        const bool allowLocal = false;
#endif
        return new(uri, allowLocal);
    }
}
