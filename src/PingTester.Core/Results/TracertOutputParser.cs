using System.Collections.Generic;
using System.Text.Json;

namespace PingTester.Core.Results;

/// <summary>
/// FUNCTIONAL CORE — pure. Parses the JSON captured from
/// `Test-NetConnection ... -TraceRoute | ConvertTo-Json` into a
/// <see cref="TraceResult"/>.
/// </summary>
public static class TracertOutputParser
{
    public static TraceResult Parse(string target, string json)
    {
        var hops = new List<TraceHop>();
        bool reached = false;

        if (!string.IsNullOrWhiteSpace(json))
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("PingSucceeded", out var ps) &&
                    (ps.ValueKind == JsonValueKind.True || ps.ValueKind == JsonValueKind.False))
                {
                    reached = ps.GetBoolean();
                }

                if (root.TryGetProperty("TraceRoute", out var tr))
                    hops = ReadHops(tr);
            }
        }

        return new TraceResult(target, reached, hops);
    }

    private static List<TraceHop> ReadHops(JsonElement traceRoute)
    {
        var hops = new List<TraceHop>();
        int number = 1;

        if (traceRoute.ValueKind == JsonValueKind.Array)
        {
            foreach (var hop in traceRoute.EnumerateArray())
            {
                var address = hop.ValueKind == JsonValueKind.String
                    ? hop.GetString()
                    : hop.ToString();
                if (!string.IsNullOrWhiteSpace(address))
                    hops.Add(new TraceHop(number++, address));
            }
        }
        else if (traceRoute.ValueKind == JsonValueKind.String)
        {
            // A single hop may be emitted as a scalar rather than an array.
            var address = traceRoute.GetString();
            if (!string.IsNullOrWhiteSpace(address))
                hops.Add(new TraceHop(number, address));
        }

        return hops;
    }
}
