using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using TarkovSkills.Core.Authentication;

namespace TarkovSkills.Core.Academy;

// Success bytes are decoded by the endpoint's contract mapper. Error bodies and
// HttpResponseMessage/RequestMessage never cross this boundary into UI or reports.
public sealed class AcademyApiResult
{
    public DesktopAuthStatus AuthStatus { get; }
    public HttpStatusCode? StatusCode { get; }
    public byte[]? Content { get; }
    public string? ErrorCode { get; }
    internal string? CredentialBinding { get; }
    internal AcademyApiResult(DesktopAuthStatus authStatus, HttpStatusCode? statusCode, byte[]? content, string? errorCode = null, string? credentialBinding = null)
    {
        AuthStatus = authStatus;
        StatusCode = statusCode;
        Content = content;
        ErrorCode = errorCode;
        CredentialBinding = credentialBinding;
    }
    public override string ToString() => $"Academy API: {AuthStatus}, {StatusCode}";
}

public sealed class AcademyApiClient : IDisposable
{
    private readonly Uri baseUri;
    private readonly DesktopAuthSession session;
    private readonly HttpClient http;

    // The endpoint is trusted application configuration, never run data or a redirect.
    // Plain HTTP requires an explicit development opt-in and literal IPv4 loopback.
    public AcademyApiClient(Uri apiBaseUri, DesktopAuthSession session, bool allowLoopbackHttp = false)
        : this(apiBaseUri, session, new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false },
            allowLoopbackHttp) { }

    internal AcademyApiClient(Uri apiBaseUri, DesktopAuthSession session, HttpMessageHandler handler,
        bool allowLoopbackHttp = false)
    {
        if (!apiBaseUri.IsAbsoluteUri || apiBaseUri.UserInfo.Length != 0 ||
            apiBaseUri.Query.Length != 0 || apiBaseUri.Fragment.Length != 0 ||
            apiBaseUri.AbsolutePath.TrimEnd('/') != "/api/bench/v1" ||
            !(apiBaseUri.Scheme == Uri.UriSchemeHttps ||
              (allowLoopbackHttp && apiBaseUri.Scheme == Uri.UriSchemeHttp && apiBaseUri.Host == "127.0.0.1")))
        {
            handler.Dispose();
            throw new ArgumentException("Invalid Academy API configuration.", nameof(apiBaseUri));
        }
        baseUri = new Uri(apiBaseUri.AbsoluteUri.TrimEnd('/') + "/");
        this.session = session;
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 65536 };
    }

    // A read of one existing run, not a My runs list or a publication operation.
    public Task<AcademyApiResult> GetRunAsync(Guid clientRunId, CancellationToken cancellation = default) =>
        SendAsync(HttpMethod.Get, $"me/runs/by-client-id/{clientRunId:D}", null, cancellation);

    public Task<AcademyApiResult> SubmitAsync(PreparedSubmission submission, CancellationToken cancellation = default) =>
        SendAsync(HttpMethod.Post, "me/runs", submission.Json, cancellation);

    internal Task<AcademyApiResult> SubmitCheckedAsync(PreparedSubmission submission, string credentialBinding, CancellationToken cancellation) =>
        SendAsync(HttpMethod.Post, "me/runs", submission.Json, cancellation, credentialBinding);

    private async Task<AcademyApiResult> SendAsync(HttpMethod method, string path, string? json, CancellationToken cancellation, string? expectedBinding = null)
    {
        HttpStatusCode? statusCode = null;
        byte[]? content = null;
        string? errorCode = null;
        string? credentialBinding = null;
        var hasSent = false;
        var auth = await session.AuthorizeRequestAsync(async token =>
        {
            // Forget the first response if refresh/retry fails before receiving another.
            statusCode = null;
            content = null;
            errorCode = null;
            credentialBinding = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            // Another product may switch the shared login between lookup and POST.
            // A refresh inside this request is safe: it retains the same exclusive lease.
            if (!hasSent && expectedBinding is not null && expectedBinding != credentialBinding) return false;
            hasSent = true;
            using var request = new HttpRequestMessage(method, new Uri(baseUri, path));
            if (json is not null) request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                using var response = await http.SendAsync(request, cancellation);
                statusCode = response.StatusCode;
                if (response.IsSuccessStatusCode)
                    content = await response.Content.ReadAsByteArrayAsync(cancellation);
                else if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    try
                    {
                        using var error = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellation));
                        var code = error.RootElement.GetProperty("code").GetString();
                        if (code is "publication_deleted" or "idempotency_conflict") errorCode = code;
                    }
                    catch (JsonException) { }
                    catch (KeyNotFoundException) { }
                    catch (InvalidOperationException) { }
                }
                return response.StatusCode == HttpStatusCode.Unauthorized;
            }
            finally { request.Headers.Authorization = null; }
        }, cancellation);

        return new AcademyApiResult(auth, auth == DesktopAuthStatus.SignedIn ? statusCode : null,
            auth == DesktopAuthStatus.SignedIn ? content : null,
            auth == DesktopAuthStatus.SignedIn ? errorCode : null,
            auth == DesktopAuthStatus.SignedIn ? credentialBinding : null);
    }

    // The caller owns the shared auth session and cancels/awaits requests before disposal.
    public void Dispose() => http.Dispose();
}
