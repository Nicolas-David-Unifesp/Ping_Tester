using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PingTester.Core.Results;

/// <summary>
/// FUNCTIONAL CORE — pure. Parses the TEXT output of the classic Windows
/// tracert.exe (Portuguese locale) into a <see cref="TraceResult"/>.
///
/// Each hop line starts with a hop number, then three timing columns, then the
/// resolved address (or only "* * *" when the hop did not answer). Example:
///   "  3     3 ms     3 ms     4 ms  201.61.225.13"
///   "  1     *        *        *     Esgotado o tempo limite do pedido."
///
/// Destination is considered reached when the last hop's address equals the
/// traced target.
/// </summary>
public static class TracertOutputParser
{
    // Capture leading hop number and the remainder of the line.
    private static readonly Regex HopLine =
        new(@"^\s*(\d{1,3})\s+(.*)$", RegexOptions.Compiled);

    // An IPv4 (or IPv6-ish) address token at the END of the remainder.
    private static readonly Regex TrailingAddress =
        new(@"([0-9A-Fa-f\.:]+)\s*$", RegexOptions.Compiled);

    public static TraceResult Parse(string target, string output)
    {
        var hops = new List<TraceHop>();

        if (string.IsNullOrWhiteSpace(output))
            return new TraceResult(target, false, hops);

        var lines = output.Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            var m = HopLine.Match(line);
            if (!m.Success)
                continue;

            var number = int.Parse(m.Groups[1].Value);
            var remainder = m.Groups[2].Value.Trim();

            // A hop that only timed out has no address (just "* * * Esgotado...").
            string address = "";
            if (!remainder.StartsWith("*"))
            {
                var addr = TrailingAddress.Match(remainder);
                if (addr.Success && LooksLikeAddress(addr.Groups[1].Value))
                    address = addr.Groups[1].Value;
            }

            hops.Add(new TraceHop(number, address));
        }

        bool reached = hops.Count > 0 &&
                       hops[^1].Address.Length > 0 &&
                       hops[^1].Address == target;

        return new TraceResult(target, reached, hops);
    }

    private static bool LooksLikeAddress(string token)
    {
        // Must contain a dot or colon and at least one digit — filters out
        // stray words while accepting IPv4 and IPv6.
        bool hasSep = token.Contains('.') || token.Contains(':');
        bool hasDigit = false;
        foreach (var c in token) if (char.IsDigit(c)) { hasDigit = true; break; }
        return hasSep && hasDigit;
    }
}
