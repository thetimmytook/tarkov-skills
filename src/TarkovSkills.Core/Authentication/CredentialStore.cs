using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TarkovSkills.Core.Authentication;

// Class, not record: never generate a ToString() that prints credential values.
internal sealed class DesktopCredential
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public bool RevocationPending { get; set; }
}

internal interface ICredentialStore
{
    IDisposable AcquireLease();
    DesktopCredential? Load();
    void Save(DesktopCredential credential);
    void Clear();
}

internal sealed class CredentialStore : ICredentialStore
{
    internal const string SharedFolderName = "TarkovDesktopAuth";
    private readonly string file;
    private readonly byte[] entropy;

    public CredentialStore(AuthConfiguration config, string directory)
    {
        Directory.CreateDirectory(directory);
        entropy = SHA256.HashData(Encoding.UTF8.GetBytes("TarkovDesktopAuth.v1\n" + config.Issuer + "\n" + config.ClientId));
        file = Path.Combine(directory, Convert.ToHexString(entropy) + ".credential");
    }

    internal static string SharedDirectory => AppPaths.GetPackageFamilyName() is null
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TarkovSkills", "shared-auth-v1")
        : Windows.Storage.ApplicationData.Current.GetPublisherCacheFolder(SharedFolderName).Path;

    public IDisposable AcquireLease()
    {
        try { return new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
        {
            throw new CredentialStoreBusy();
        }
    }

    public DesktopCredential? Load()
    {
        if (!File.Exists(file))
        {
            return null;
        }

        if (new FileInfo(file).Length > 65536) throw new AuthenticationDenied();
        var plaintext = ProtectedData.Unprotect(File.ReadAllBytes(file), entropy, DataProtectionScope.CurrentUser);
        try
        {
            var credential = JsonSerializer.Deserialize<DesktopCredential>(plaintext) ?? throw new AuthenticationDenied();
            if (string.IsNullOrWhiteSpace(credential.AccessToken) || string.IsNullOrWhiteSpace(credential.RefreshToken) ||
                credential.AccessToken.Length > 4096 || credential.RefreshToken.Length > 8192 ||
                credential.AccessToken.Any(char.IsWhiteSpace) || credential.RefreshToken.Any(char.IsWhiteSpace) ||
                credential.ExpiresAt == default)
                throw new AuthenticationDenied();
            return credential;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public void Save(DesktopCredential credential)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(credential);
        var temporary = file + ".tmp";
        try
        {
            var encrypted = ProtectedData.Protect(plaintext, entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, file, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            File.Delete(temporary);
        }
    }

    public void Clear() => File.Delete(file);
}

internal sealed class CredentialStoreBusy : IOException { }
