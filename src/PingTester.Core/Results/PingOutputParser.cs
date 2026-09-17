using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PingTester.Core.Results;

/// <summary>
/// FUNCTIONAL CORE — pure. Parses the JSON captured from
/// `Test-Connection ... | ConvertTo-Json` into a <see cref="PingResult"/>.
/// Parsing JSON (not localized console text) keeps this deterministic and
/// stable across PowerShell locales and versions.
/// </summary>
public static class PingOutputParser
{
    public static PingResult Parse(string target, string json)
    {
        var replies = ReadReplies(json);

        var sent = replies.Count;
        var successful = replies.Where(r => IsSuccess(r.Status)).ToList();
        var received = successful.Count;

        double? avg = received > 0
            ? successful.Average(r => r.Latency)
            : null;

        return new PingResult(target, sent, received, avg);
    }

    private static bool IsSuccess(string? status) =>
        string.Equals(status, "Success", System.StringComparison.OrdinalIgnoreCase);

    private static List<Reply> ReadReplies(string json)
    {
        var replies = new List<Reply>();
        if (string.IsNullOrWhiteSpace(json))
            return replies;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // ConvertTo-Json emits an array for multiple items, a single object
        // for one item. Handle both.
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
                replies.Add(ReadReply(element));
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            replies.Add(ReadReply(root));
        }

        return replies;
    }

    private static Reply ReadReply(JsonElement element)
    {
        string? status = element.TryGetProperty("Status", out var s)
            ? s.GetString()
            : null;

        double latency = 0;
        if (element.TryGetProperty("Latency", out var l) && l.ValueKind == JsonValueKind.Number)
            latency = l.GetDouble();

        return new Reply(status, latency);
    }

    private readonly record struct Reply(string? Status, double Latency);
}
