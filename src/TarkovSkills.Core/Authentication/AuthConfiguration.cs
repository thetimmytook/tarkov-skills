using System.Text.Json;

namespace TarkovSkills.Core.Authentication;

public enum DesktopAuthProduct { Benchmark, Toolkit }

public sealed class AuthConfiguration
{
    public string Issuer { get; }
    public string ClientId { get; }
    public DesktopAuthProduct Product { get; }

    public AuthConfiguration(string issuer, string clientId, DesktopAuthProduct product)
    {
        // The trusted product configuration may use a Clerk production custom domain.
        // Endpoints always remain on this exact HTTPS origin; discovery cannot redirect them.
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.HostNameType != UriHostNameType.Dns || uri.IsLoopback ||
            uri.GetLeftPart(UriPartial.Authority) != issuer || uri.UserInfo.Length != 0 ||
            !uri.IsDefaultPort || string.IsNullOrWhiteSpace(clientId) || clientId == "REPLACE" ||
            clientId.Length > 256 || clientId.Any(char.IsWhiteSpace) || !Enum.IsDefined(product))
            throw new AuthenticationDenied();

        Issuer = issuer;
        ClientId = clientId;
        Product = product;
    }

    public static AuthConfiguration Read(string path, DesktopAuthProduct product)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Any(p => p.Name is not ("issuer" or "clientId")) ||
            root.EnumerateObject().Count() != 2)
            throw new AuthenticationDenied();
        return new(JsonFields.Text(root, "issuer"), JsonFields.Text(root, "clientId"), product);
    }
}

internal sealed class AuthenticationDenied() : Exception("Authentication operation failed.");
internal sealed class CredentialRejected() : Exception("The credential is no longer valid.");
internal sealed class RevocationDenied(int status, string code) : Exception("Remote revocation was not acknowledged.")
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

internal static class JsonFields
{
    public static string Text(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new AuthenticationDenied();
        return value.GetString()!;
    }
}
