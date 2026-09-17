using System.Collections.Generic;

namespace PingTester.Core.Ips;

/// <summary>
/// FUNCTIONAL CORE — pure. Extracts IP addresses from the raw text of a CSV
/// file. The imperative shell is responsible for reading the file from disk;
/// this function only sees the resulting string.
///
/// Rules:
///  - Cells may be delimited by comma or semicolon (Brazilian Excel uses ';').
///  - The IP can be in any column; non-IP cells (headers, labels) are skipped.
///  - Surrounding quotes and whitespace are stripped.
///  - Duplicates are removed; order of first appearance is preserved.
/// </summary>
public static class CsvIpExtractor
{
    private static readonly char[] LineSeparators = { '\n', '\r' };
    private static readonly char[] CellSeparators = { ',', ';' };

    public static IpParseResult Extract(string? csvText)
    {
        var valid = new List<HostAddress>();
        var seen = new HashSet<string>();

        if (string.IsNullOrWhiteSpace(csvText))
            return new IpParseResult(valid, System.Array.Empty<IpParseError>());

        var lines = csvText.Split(LineSeparators, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var cells = line.Split(CellSeparators, System.StringSplitOptions.RemoveEmptyEntries
                                                 | System.StringSplitOptions.TrimEntries);

            foreach (var cell in cells)
            {
                var cleaned = Unquote(cell);
                var host = HostAddress.TryCreate(cleaned);

                // Non-IP cells (headers, names) are intentionally skipped, not
                // treated as errors — a CSV legitimately has non-IP columns.
                if (host is null)
                    continue;

                if (seen.Add(host.Value))
                    valid.Add(host);
            }
        }

        return new IpParseResult(valid, System.Array.Empty<IpParseError>());
    }

    private static string Unquote(string cell)
    {
        var trimmed = cell.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            trimmed = trimmed[1..^1].Trim();
        return trimmed;
    }
}
