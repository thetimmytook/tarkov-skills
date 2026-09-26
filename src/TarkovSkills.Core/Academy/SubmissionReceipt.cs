using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TarkovSkills.Core.Academy;

public static class SubmissionReceipt
{
    public static string? ReadLookup(AcademyApiResult result, Guid id)
    {
        if (result.StatusCode != HttpStatusCode.OK || result.Content is null) return null;
        try
        {
            using var json = JsonDocument.Parse(result.Content);
            var root = json.RootElement;
            if (root.EnumerateObject().Count() != 1) return null;
            var item = root.GetProperty("item");
            if (item.GetProperty("client_run_id").GetGuid() != id) return null;
            var status = item.GetProperty("publication_status").GetString();
            if (status == "deleted")
                return item.EnumerateObject().Count() == 4 &&
                    item.GetProperty("public_run_id").ValueKind == JsonValueKind.Null &&
                    item.GetProperty("url").ValueKind == JsonValueKind.Null ? status : null;
            // Only receipt fields are needed; owner card hardware and metrics never enter the cache.
            var receipt = new Dictionary<string, JsonElement>
            {
                ["client_run_id"] = item.GetProperty("client_run_id"),
                ["publication_status"] = item.GetProperty("publication_status"),
                ["public_run_id"] = item.GetProperty("public_run_id"),
                ["url"] = item.GetProperty("url")
            };
            var reason = item.GetProperty("status_reason");
            if (status == "rejected") receipt["status_reason"] = reason;
            else if (reason.ValueKind != JsonValueKind.Null) return null;
            return Read(new AcademyApiResult(result.AuthStatus,
                status == "pending_review" ? HttpStatusCode.Accepted : HttpStatusCode.OK,
                JsonSerializer.SerializeToUtf8Bytes(receipt)), id);
        }
        catch { return null; }
    }

    public static string? Read(AcademyApiResult result, Guid id)
    {
        if (result.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Accepted) || result.Content is null) return null;
        try
        {
            using var json = JsonDocument.Parse(result.Content);
            var root = json.RootElement;
            if (root.GetProperty("client_run_id").GetGuid() != id) return null;
            var status = root.GetProperty("publication_status").GetString();
            var names = status == "rejected"
                ? new[] { "client_run_id", "publication_status", "public_run_id", "url", "status_reason" }
                : new[] { "client_run_id", "publication_status", "public_run_id", "url" };
            if (root.EnumerateObject().Count() != names.Length || root.EnumerateObject().Any(p => !names.Contains(p.Name))) return null;
            if (status == "published")
            {
                var publicId = root.GetProperty("public_run_id").GetString();
                return result.StatusCode == HttpStatusCode.OK && publicId is not null &&
                    Regex.IsMatch(publicId, "^br_[A-Za-z0-9_-]+$") &&
                    root.GetProperty("url").GetString() == "/bench/runs/" + publicId ? status : null;
            }
            if (root.GetProperty("public_run_id").ValueKind != JsonValueKind.Null || root.GetProperty("url").ValueKind != JsonValueKind.Null) return null;
            if (status == "pending_review" && result.StatusCode == HttpStatusCode.Accepted) return status;
            return status == "rejected" && result.StatusCode == HttpStatusCode.OK && root.GetProperty("status_reason").GetString() == "rejected" ? status : null;
        }
        catch { return null; }
    }
}
