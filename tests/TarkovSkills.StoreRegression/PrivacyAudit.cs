using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TarkovSkills.StoreRegression;

public static class PrivacyAudit
{
    private static readonly Regex Address = new(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])");
    private static readonly Regex LocalPath = new(@"(?i)\b[A-Z]:\\|\\\\[^\s\\]+\\|Users[\\/]");
    private static readonly Regex GuidValue = new(@"\b[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase);
    private static readonly Regex Ipv6Candidate = new(@"[0-9a-f:]{2,}(?:%[A-Za-z0-9]+)?", RegexOptions.IgnoreCase);
    private static readonly HashSet<string> VersionFields = new(StringComparer.OrdinalIgnoreCase)
        { "version", "driver_version", "game_version", "toolkit_version" };
    private static readonly HashSet<string> ForbiddenFields = new(StringComparer.OrdinalIgnoreCase)
        { "username", "hostname", "serial", "serial_number", "machine_guid", "machine_id",
          "ip_address", "controls", "sound", "path", "settings_directory", "csv_path" };

    public static IReadOnlyList<string> Findings(JsonElement root, string? userName = null, string? machineName = null)
    {
        var findings = new HashSet<string>();
        Visit(root, "");
        return findings.Order().ToArray();

        void Visit(JsonElement node, string field)
        {
            if (node.ValueKind == JsonValueKind.Object)
                foreach (var property in node.EnumerateObject())
                {
                    if (ForbiddenFields.Contains(property.Name)) findings.Add("private-field");
                    Visit(property.Value, property.Name);
                }
            else if (node.ValueKind == JsonValueKind.Array)
                foreach (var item in node.EnumerateArray()) Visit(item, field);
            else if (node.ValueKind == JsonValueKind.String)
            {
                var text = node.GetString()!;
                foreach (var identity in new[] { userName, machineName }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    if (Regex.IsMatch(text, @"(?<![A-Za-z0-9])" + Regex.Escape(identity!) + @"(?![A-Za-z0-9])",
                        RegexOptions.IgnoreCase)) findings.Add("personal-identity");
                if (LocalPath.IsMatch(text)) findings.Add("local-path");
                if (GuidValue.IsMatch(text)) findings.Add("machine-identifier");
                if (text.Contains("Control.ini", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Sound.ini", StringComparison.OrdinalIgnoreCase)) findings.Add("excluded-settings");
                if (!VersionFields.Contains(field) && Address.Matches(text).Any(m => IPAddress.TryParse(m.Value, out _)))
                    findings.Add("ip-address");
                if (Ipv6Candidate.Matches(text).Any(m => m.Value.Contains(':') &&
                    IPAddress.TryParse(m.Value, out var address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6))
                    findings.Add("ip-address");
            }
        }
    }
}
