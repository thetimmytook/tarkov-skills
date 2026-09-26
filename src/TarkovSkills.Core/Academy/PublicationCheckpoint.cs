using System.Text.Json.Serialization;

namespace TarkovSkills.Core.Academy;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublicationCheckpoint(Guid ClientRunId, string Status, DateTimeOffset CheckedAt)
{
    internal static bool IsConfirmed(string? status) => status is "pending_review" or "published" or "rejected" or "deleted";
}
