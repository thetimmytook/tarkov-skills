using System.Net;

namespace TarkovSkills.Core.Academy;

public enum SubmissionOutcome { Confirmed, NotFound, Unconfirmed, Conflict, StorageUnavailable }
public sealed record SubmissionCheck(SubmissionOutcome Outcome, PublicationCheckpoint? Checkpoint = null);

public sealed class SubmissionWorkflow(AcademyApiClient api, SubmissionOutbox outbox)
{
    public async Task<SubmissionCheck> CheckAsync(Guid id, CancellationToken cancellation = default)
        => (await CheckCoreAsync(id, cancellation)).Check;

    private async Task<(SubmissionCheck Check, string? Binding)> CheckCoreAsync(Guid id, CancellationToken cancellation)
    {
        var result = await api.GetRunAsync(id, cancellation);
        var status = SubmissionReceipt.ReadLookup(result, id);
        if (status is not null) return (Save(id, status), result.CredentialBinding);
        return (new(result.StatusCode == HttpStatusCode.NotFound ? SubmissionOutcome.NotFound : SubmissionOutcome.Unconfirmed), result.CredentialBinding);
    }

    // Called only by the explicit Send action. Never invoked from restore, polling or collection.
    public async Task<SubmissionCheck> SubmitAsync(PreparedSubmission submission, CancellationToken cancellation = default)
    {
        PublicationCheckpoint? previous;
        try { previous = outbox.ReadStatus(submission.ClientRunId); }
        catch { return new(SubmissionOutcome.StorageUnavailable); }
        if (previous?.Status == "deleted") return new(SubmissionOutcome.Confirmed, previous);

        // Resolve an earlier uncertain POST first. A confirmed run is never POSTed again.
        var check = await CheckCoreAsync(submission.ClientRunId, cancellation);
        if (check.Check.Outcome != SubmissionOutcome.NotFound) return check.Check;
        // 404 hides foreign ownership too. Do not recreate a known run after an account switch.
        if (previous is not null || check.Binding is null) return new(SubmissionOutcome.Unconfirmed);
        cancellation.ThrowIfCancellationRequested();
        var result = await api.SubmitCheckedAsync(submission, check.Binding, cancellation);
        var status = SubmissionReceipt.Read(result, submission.ClientRunId);
        if (status is not null) return Save(submission.ClientRunId, status);
        if (result.ErrorCode == "publication_deleted") return Save(submission.ClientRunId, "deleted");
        if (result.ErrorCode == "idempotency_conflict") return new(SubmissionOutcome.Conflict);
        // No automatic POST retry after a network/response error. The next explicit action
        // performs the same lookup, and if absent reuses the exact frozen request.
        return new(SubmissionOutcome.Unconfirmed);
    }

    private SubmissionCheck Save(Guid id, string status)
    {
        try { return new(SubmissionOutcome.Confirmed, outbox.SaveStatus(id, status)); }
        catch { return new(SubmissionOutcome.StorageUnavailable, new(id, status, DateTimeOffset.UtcNow)); }
    }
}
