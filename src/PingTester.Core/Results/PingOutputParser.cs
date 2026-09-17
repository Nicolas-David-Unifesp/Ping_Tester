using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PingTester.Core.Results;

/// <summary>
/// FUNCTIONAL CORE — pure. Parses the TEXT output of the classic Windows
/// ping.exe (Portuguese locale) into a <see cref="PingResult"/>.
///
/// Strategy:
///  - Packet counts come from the authoritative "Estatísticas" line
///    ("Enviados = N, Recebidos = M, Perdidos = ...").
///  - Latency values come from the individual reply lines ("Resposta de ...
///    tempo=Xms" or "tempo&lt;1ms"). The average uses only those replies.
///  - If the statistics line is absent, fall back to counting reply lines.
///
/// This deliberately uses the classic executable (not a cmdlet) because on the
/// target environment the Windows PowerShell 5.1 Test-Connection cmdlet fails
/// via WMI, while ping.exe works — same mechanism as the CMD prompt.
/// </summary>
public static class PingOutputParser
{
    // "Resposta de 10.113.96.148: bytes=32 tempo=9ms TTL=58"  -> latency 9
    // "Resposta de 127.0.0.1: bytes=32 tempo<1ms TTL=128"     -> latency 0
    // Group 1 = the operator (= or <), Group 2 = the number.
    private static readonly Regex ReplyWithTime =
        new(@"Resposta de .*?tempo\s*([=<])\s*(\d+)\s*ms", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A reply line that is actually a failure (e.g. host unreachable) — must
    // NOT be counted as a successful reply even though it starts with "Resposta".
    private static readonly Regex ReplyFailure =
        new(@"Resposta de .*(inacess|inalcan)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "Pacotes: Enviados = 4, Recebidos = 3, Perdidos = 1"
    private static readonly Regex StatsLine =
        new(@"Enviados\s*=\s*(\d+).*?Recebidos\s*=\s*(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    public static PingResult Parse(string target, string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return new PingResult(target, 0, 0, null);

        // Latencies from successful reply lines (skip failure "Resposta" lines).
        var latencies = new List<double>();
        foreach (Match m in ReplyWithTime.Matches(output))
        {
            // Ensure this match isn't part of a failure line.
            var lineStart = output.LastIndexOf('\n', m.Index < output.Length ? m.Index : output.Length - 1);
            var lineEnd = output.IndexOf('\n', m.Index);
            if (lineEnd < 0) lineEnd = output.Length;
            var line = output.Substring(lineStart + 1, lineEnd - lineStart - 1);
            if (ReplyFailure.IsMatch(line))
                continue;

            var op = m.Groups[1].Value;
            if (double.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            {
                // "tempo<1ms" means below 1 ms; report it as 0.
                latencies.Add(op == "<" ? 0.0 : ms);
            }
        }

        int sent, received;
        var stats = StatsLine.Match(output);
        if (stats.Success)
        {
            sent = int.Parse(stats.Groups[1].Value, CultureInfo.InvariantCulture);
            received = int.Parse(stats.Groups[2].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            // No statistics line: infer from successful reply lines.
            received = latencies.Count;
            sent = received;
        }

        double? avg = latencies.Count > 0 ? latencies.Average() : null;

        // If statistics claim replies but we found no latency lines (rare
        // locale quirk), still report reachability from the count.
        return new PingResult(target, sent, received, avg);
    }
}
