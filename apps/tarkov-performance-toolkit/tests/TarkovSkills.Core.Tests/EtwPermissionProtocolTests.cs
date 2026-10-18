using TarkovSkills.Core.Permissions;

namespace TarkovSkills.Core.Tests;

public sealed class EtwPermissionProtocolTests
{
    [Theory]
    [InlineData(EtwPermissionAction.Add, EtwPermissionOutcome.Added)]
    [InlineData(EtwPermissionAction.Remove, EtwPermissionOutcome.Removed)]
    public async Task HelperAppliesOnlyTheVerifiedRequestedAction(EtwPermissionAction action, EtwPermissionOutcome outcome)
    {
        using var stream = new ScriptedStream([1, (byte)action, 0x41]);
        var calls = 0;
        var result = await EtwPermissionProtocol.HelperAsync(stream, action, actual =>
        {
            calls++; Assert.Equal(action, actual); return new(outcome);
        }, default);
        Assert.Equal(1, calls);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(new byte[] { 0x50, 0x40, (byte)outcome, 0, 0, 0, 0 }, stream.Written.ToArray());
    }

    [Theory]
    [InlineData(2, 1, 0x41)]
    [InlineData(1, 2, 0x41)]
    [InlineData(1, 1, 0x42)]
    public async Task MalformedHeaderMismatchedActionOrMissingConsentCannotMutate(int version, int action, int go)
    {
        using var stream = new ScriptedStream([(byte)version, (byte)action, (byte)go]);
        var result = await EtwPermissionProtocol.HelperAsync(stream, EtwPermissionAction.Add,
            _ => throw new InvalidOperationException("Mutation must not be reached"), default);
        Assert.Equal(EtwPermissionOutcome.InvalidRequest, result.Outcome);
    }

    [Fact]
    public async Task DisconnectedOrCancelledParentCannotAuthorizeAGroupChange()
    {
        using var disconnected = new ScriptedStream([1, 1]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => EtwPermissionProtocol.HelperAsync(disconnected,
            EtwPermissionAction.Add, _ => throw new InvalidOperationException("Must not mutate"), default));
        using var cancelled = new ScriptedStream([1, 1, 0x41]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => EtwPermissionProtocol.HelperAsync(cancelled,
            EtwPermissionAction.Add, _ => throw new InvalidOperationException("Must not mutate"), cancellation.Token));
    }

    [Fact]
    public async Task ParentMarksAuthorizationBeforeWaitingForAResult()
    {
        using var stream = new ScriptedStream([0x50, 0x40]);
        var progress = new EtwPermissionProtocol.Progress();
        await Assert.ThrowsAsync<EndOfStreamException>(() => EtwPermissionProtocol.ParentAsync(stream,
            EtwPermissionAction.Add, progress, default));
        Assert.True(progress.ApplyAuthorized);
        Assert.Equal(new byte[] { 1, 1, 0x41 }, stream.Written.ToArray());
    }

    [Fact]
    public async Task ParentRejectsAnInvalidHelperWithoutAuthorizingChanges()
    {
        using var stream = new ScriptedStream([0x42]);
        var progress = new EtwPermissionProtocol.Progress();
        var result = await EtwPermissionProtocol.ParentAsync(stream, EtwPermissionAction.Add, progress, default);
        Assert.Equal(EtwPermissionOutcome.InvalidRequest, result.Outcome);
        Assert.False(progress.ApplyAuthorized);
        Assert.Empty(stream.Written.ToArray());
    }

    [Fact]
    public async Task UnknownResultDoesNotClaimSuccess()
    {
        using var stream = new ScriptedStream([0x50, 0x40, 0xff, 0, 0, 0, 0]);
        var result = await EtwPermissionProtocol.ParentAsync(stream, EtwPermissionAction.Add, new(), default);
        Assert.Equal(EtwPermissionOutcome.Unconfirmed, result.Outcome);
    }

    private sealed class ScriptedStream(byte[] input) : Stream
    {
        private readonly MemoryStream _input = new(input);
        internal readonly MemoryStream Written = new();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _input.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => Written.WriteAsync(buffer, cancellationToken);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) { _input.Dispose(); Written.Dispose(); } base.Dispose(disposing); }
    }
}
