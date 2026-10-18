using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core.Tests;

public sealed class EtwCaptureBrokerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposedHostPipeReturnsSanitizedDisconnectOrCancellation(bool cancelled)
    {
        using var stream = new ClosingStream();
        using var connection = new EtwCaptureConnection(stream);
        using var cancellation = new CancellationTokenSource();
        var capture = connection.CaptureAsync(120, () => { }, null, cancellation.Token);
        await stream.Reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (cancelled) cancellation.Cancel();
        connection.Dispose();
        if (cancelled)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.WaitAsync(TimeSpan.FromSeconds(5)));
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains("disconnected", error.Message);
            Assert.DoesNotContain("private-pipe", error.Message);
        }
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task OneAuthenticatedConnectionSupportsRepeatedCapturesAndExactWindowAlignment()
    {
        var captures = 0;
        using var pipes = await Pipes.OpenAsync();
        var broker = EtwCaptureBroker.RunAsync(pipes.Client, (duration, started, log, _) =>
        {
            captures++; Assert.Equal(120, duration); started();
            log?.ProcessOutput(CaptureProcessRole.capture, CaptureOutputStream.stdout, 1, 18, false,
                PresentMonOutputMessages.Classify("Started recording."));
            return Task.FromResult(Frames());
        });
        using var connection = new EtwCaptureConnection(pipes.Server);
        var starts = 0;
        for (var i = 0; i < 3; i++)
        {
            var capture = await connection.CaptureAsync(120, () => starts++, null, default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(120, capture.Window.Duration); Assert.Equal(100, capture.Window.Intervals[0].Start);
        }
        Assert.Equal(3, starts); Assert.Equal(3, captures);
        connection.Dispose(); await broker.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancelStopsOnlyTheCurrentCaptureAndTheConnectionCanBeReused()
    {
        using var pipes = await Pipes.OpenAsync();
        var attempts = 0;
        var broker = EtwCaptureBroker.RunAsync(pipes.Client, async (_, started, _, token) =>
        {
            started();
            if (++attempts == 1) await Task.Delay(Timeout.Infinite, token);
            return Frames();
        });
        using var connection = new EtwCaptureConnection(pipes.Server);
        using var cancellation = new CancellationTokenSource();
        var capturing = connection.CaptureAsync(120, () => cancellation.Cancel(), null, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(connection.IsConnected);
        var retry = await connection.CaptureAsync(120, () => { }, null, default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(7200, retry.Metrics.SampleCount);
        connection.Dispose(); await broker.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ClosingOrCrashingTheHostPipeCancelsItsActiveCollector()
    {
        using var pipes = await Pipes.OpenAsync();
        var entered = new TaskCompletionSource(); var stopped = new TaskCompletionSource();
        var broker = EtwCaptureBroker.RunAsync(pipes.Client, async (_, started, _, token) =>
        {
            started(); entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); return Frames(); }
            finally { stopped.SetResult(); }
        });
        var writer = new CapturePacketWriter(pipes.Server);
        await writer.WriteAsync(new(CapturePacketKind.Start, 120), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pipes.Server.Dispose();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await broker.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CollectorExceptionIsReturnedAsAFixedCodeWithoutPrivateText()
    {
        using var pipes = await Pipes.OpenAsync();
        var broker = EtwCaptureBroker.RunAsync(pipes.Client, (_, _, _, _) => throw new Exception(@"C:\Users\secret-name\private-file"));
        using var connection = new EtwCaptureConnection(pipes.Server);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CaptureAsync(120, () => { }, null, default));
        Assert.DoesNotContain("secret-name", error.Message);
        Assert.Contains("could not complete", error.Message);
        connection.Dispose(); await broker.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NativeCollectorFailurePreservesOnlyTheNumericWindowsError()
    {
        using var pipes = await Pipes.OpenAsync();
        var broker = EtwCaptureBroker.RunAsync(pipes.Client, (_, _, _, _) => throw new System.ComponentModel.Win32Exception(5, "private-account-path"));
        await new CapturePacketWriter(pipes.Server).WriteAsync(new(CapturePacketKind.Start, 120), default);
        var failure = await EtwCaptureWire.ReadAsync(pipes.Server, default);
        Assert.Equal(CapturePacketKind.Failed, failure.Kind);
        Assert.Equal(CaptureFailure.Collector, failure.Failure); Assert.Equal(5, failure.WindowsError);
        Assert.DoesNotContain("private-account-path", System.Text.Json.JsonSerializer.Serialize(failure));
        pipes.Server.Dispose(); await broker.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvalidCommandDisconnectsWithoutLaunchingTheCollector()
    {
        using var pipes = await Pipes.OpenAsync(); var calls = 0;
        var broker = EtwCaptureBroker.RunAsync(pipes.Client, (_, _, _, _) => { calls++; return Task.FromResult(Frames()); });
        await new CapturePacketWriter(pipes.Server).WriteAsync(new(CapturePacketKind.Start, 30), default);
        await broker.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(0, calls);
    }

    [Fact]
    public async Task DiagnosticProbeUsesTheSameConnectionWithoutReturningOrSavingABenchmark()
    {
        if (!CaptureDiagnostics.PermissionProbeEnabled)
        {
            using var unused = new MemoryStream();
            using var disabledConnection = new EtwCaptureConnection(unused);
            await Assert.ThrowsAsync<NotSupportedException>(() => disabledConnection.ProbeAsync(null, default));
            Assert.Equal(0, unused.Length);
            return;
        }
        using var pipes = await Pipes.OpenAsync(); var probes = 0;
        var broker = EtwCaptureBroker.RunAsync(pipes.Client, (_, _, _, _) => throw new Exception("Not a benchmark"),
            (_, _) => { probes++; return Task.CompletedTask; });
        using var connection = new EtwCaptureConnection(pipes.Server);
        await connection.ProbeAsync(null, default).WaitAsync(TimeSpan.FromSeconds(5));
        await connection.ProbeAsync(null, default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, probes);
        connection.Dispose(); await broker.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task JobClosureTerminatesOnlyItsOwnedDiagnosticChild()
    {
        // Standard loopback utility is a harmless long-lived child for the Windows
        // job-object lifetime check. Production only assigns pinned PresentMon.
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ping.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var arg in new[] { "127.0.0.1", "-n", "30" }) start.ArgumentList.Add(arg);
        using var child = Process.Start(start)!;
        try
        {
            using (var job = new CaptureProcessJob()) { job.Add(child); Assert.False(child.HasExited); }
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(child.HasExited);
        }
        finally { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } }
    }

    private static FrameCapture Frames() => new(new(7200, 120, 60, 50, 40, 16.667, 20, 25), new([new(100, 220)]));

    private sealed class ClosingStream : Stream
    {
        internal TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        { Reading.TrySetResult(); return new(_read.Task); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default) => ValueTask.CompletedTask;
        protected override void Dispose(bool disposing)
        { if (disposing) _read.TrySetException(new ObjectDisposedException("private-pipe")); base.Dispose(disposing); }
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class Pipes(NamedPipeServerStream server, NamedPipeClientStream client) : IDisposable
    {
        internal NamedPipeServerStream Server { get; } = server;
        internal NamedPipeClientStream Client { get; } = client;
        internal static async Task<Pipes> OpenAsync()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var name = EtwPermissionRequest.PipePrefix + Guid.NewGuid().ToString("N");
            var server = EtwPermissionTransport.CreateServer(name, identity.User!);
            var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var waiting = server.WaitForConnectionAsync(timeout.Token);
            await client.ConnectAsync(timeout.Token); await waiting;
            return new(server, client);
        }
        public void Dispose() { Server.Dispose(); Client.Dispose(); }
    }
}
