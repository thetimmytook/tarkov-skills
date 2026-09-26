using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using TarkovSkills.Core.Academy;
using Xunit;

namespace TarkovSkills.Core.Tests.Authentication;

public sealed class ComparisonTests
{
    [Fact]
    public void QueryContainsOnlyPublicCohortFields()
    {
        var run = SubmissionTests.Run();
        var query = JsonNode.Parse(BenchmarkComparison.Query(run, SubmissionPayload.GpuNames(run)[0]))!;
        Assert.Equal(new[] { "execution", "game_resolution", "game_version", "hardware", "map" }, query.AsObject().Select(p => p.Key).Order().ToArray());
        Assert.Equal(new[] { "cpu_name", "gpu_name", "ram_gb" }, query["hardware"]!.AsObject().Select(p => p.Key).Order().ToArray());
        Assert.DoesNotContain(run.RunId, query.ToJsonString());
        Assert.Equal("woods", query["map"]!.GetValue<string>());
    }

    [Fact]
    public async Task BrowseIsAnonymousAndPreservesDemoMarker()
    {
        using var client = Client(new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/bench/v1/runs?view=groups&limit=6&map=streets", request.RequestUri!.PathAndQuery);
            return """
                {"view":"groups","summary":{"run_count":1},"groups":[{"hardware":{"cpu":{"name":"CPU"},"gpu":{"name":"GPU"},"ram_gb":32},"preview_runs":[{"captured_day":"2026-09-26","map":{"id":"streets","name":"Streets"},"execution":"local","game_resolution":null,"game_version":null,"metrics":{"average_fps":60,"one_percent_low_fps":40},"is_synthetic":true}]}]}
                """;
        }));
        var result = await client.BrowseAsync("streets", default);
        Assert.True(Assert.Single(result.Runs).IsSynthetic);
        Assert.Equal(60, result.Runs[0].AverageFps);
        Assert.Contains("not a ranking", result.Description);
    }

    [Fact]
    public async Task NoExactMatchesKeepsLocalRunWithoutInventingPeers()
    {
        using var client = Client(new Handler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/cohorts/query", request.RequestUri!.AbsoluteUri);
            return """
                {"status":"no_data","criteria":{"hardware":{"cpu":{"name":"CPU"},"gpu":{"name":"GPU"},"ram_gb":32}},"runs":[],"truncated":false}
                """;
        }));
        var run = SubmissionTests.Run();
        var result = await client.CompareAsync(run, SubmissionPayload.GpuNames(run)[0], default);
        Assert.True(Assert.Single(result.Runs).IsLocal);
        Assert.Contains("No public runs match", result.Description);
    }

    [Fact]
    public async Task ExactMatchesUseSharedMetricsAndShowTruncation()
    {
        using var client = Client(new Handler(_ => """
            {"status":"matches","criteria":{"hardware":{"cpu":{"name":"CPU"},"gpu":{"name":"GPU"},"ram_gb":32}},"counts":{"run_count":25,"contributor_count":3},"truncated":true,"runs":[{"captured_day":"2026-09-26","map":{"id":"woods","name":"Woods"},"execution":"local","game_resolution":{"width":1920,"height":1080},"game_version":"0.16","metrics":{"average_fps":70,"one_percent_low_fps":50},"is_synthetic":false}]}
            """));
        var run = SubmissionTests.Run();
        var result = await client.CompareAsync(run, SubmissionPayload.GpuNames(run)[0], default);
        Assert.Equal(2, result.Runs.Count);
        Assert.True(result.Runs[0].IsLocal);
        Assert.False(result.Runs[1].IsLocal);
        Assert.Equal(50, result.Runs[1].OnePercentLowFps);
        Assert.Contains("1 of 25", result.Description);
        Assert.Contains("limited to the latest 20", result.Description);
    }

    [Fact]
    public async Task ErrorsDoNotExposeServerResponse()
    {
        using var client = Client(new Handler(_ => "private server details", HttpStatusCode.InternalServerError));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.BrowseAsync("streets", default));
        Assert.DoesNotContain("private server details", error.Message);
    }

    [Fact]
    public async Task UnknownMapCannotChangeEndpoint()
    {
        using var client = Client(new Handler(_ => throw new InvalidOperationException()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.BrowseAsync("../me/runs", default));
    }

    private static PublicBenchmarkClient Client(Handler handler) => new(new(new Uri("https://fixture.example/api/bench/v1"), false), handler);
    private sealed class Handler(Func<HttpRequestMessage, string> respond, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("Cookie"));
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(respond(request), Encoding.UTF8, "application/json") });
        }
    }
}
