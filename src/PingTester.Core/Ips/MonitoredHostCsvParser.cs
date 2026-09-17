using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PingTester.Core.Ips;

/// <summary>
/// FUNCTIONAL CORE — pure. Structured reader for the monitoring CSV. Unlike
/// <see cref="CsvIpExtractor"/> (which only harvests IPs), this understands
/// named columns from a header row so each IP can carry a school and a device
/// label.
///
/// Rules:
///  - Delimiter may be ';' or ',' (Brazilian Excel uses ';').
///  - The header maps columns by name, matched case- and accent-insensitively:
///      IP        -> "ip"
///      Escola    -> "escola"
///      Dispositivo-> "dispositivo" | "equipamento" | "device"
///  - Column order is irrelevant; unknown columns are ignored.
///  - If the first line is NOT a header (no known column names), the file is
///    treated as header-less: the first cell of each line is the IP and the
///    labels are blank (backwards compatibility with old IP-only files).
///  - Rows without a valid IP are skipped. Duplicates keep the first.
///  - Quoted cells may contain the delimiter.
/// </summary>
public static class MonitoredHostCsvParser
{
    private static readonly char[] LineSeparators = { '\n', '\r' };

    public static IReadOnlyList<MonitoredHost> Parse(string? csvText)
    {
        var result = new List<MonitoredHost>();
        var seen = new HashSet<string>();

        if (string.IsNullOrWhiteSpace(csvText))
            return result;

        var lines = csvText.Split(LineSeparators, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return result;

        var delimiter = DetectDelimiter(lines[0]);

        // Determine whether the first line is a header.
        var firstCells = SplitCsvLine(lines[0], delimiter);
        var header = TryBuildHeaderMap(firstCells);

        int startIndex = header is null ? 0 : 1;
        // Without a header, the IP is the first column.
        int ipCol = header?.GetValueOrDefault("ip", -1) ?? 0;
        int escolaCol = header?.GetValueOrDefault("escola", -1) ?? -1;
        int dispositivoCol = header?.GetValueOrDefault("dispositivo", -1) ?? -1;

        for (int i = startIndex; i < lines.Length; i++)
        {
            var cells = SplitCsvLine(lines[i], delimiter);

            var ipRaw = CellAt(cells, ipCol);
            var host = HostAddress.TryCreate(ipRaw);
            if (host is null)
                continue;

            if (!seen.Add(host.Value))
                continue;

            var escola = CellAt(cells, escolaCol);
            var dispositivo = CellAt(cells, dispositivoCol);

            result.Add(new MonitoredHost(host, escola, dispositivo));
        }

        return result;
    }

    private static char DetectDelimiter(string line)
    {
        // Prefer ';' when present (Brazilian Excel); otherwise comma.
        return line.Contains(';') ? ';' : ',';
    }

    /// <summary>
    /// Builds a map of logical column -> index if the cells look like a header
    /// (i.e. at least one known column name is present). Returns null when the
    /// line is not a header (header-less, IP-only file).
    /// </summary>
    private static Dictionary<string, int>? TryBuildHeaderMap(IReadOnlyList<string> cells)
    {
        var map = new Dictionary<string, int>();
        bool foundKnown = false;

        for (int i = 0; i < cells.Count; i++)
        {
            var key = Normalize(cells[i]);
            switch (key)
            {
                case "ip":
                    if (!map.ContainsKey("ip")) map["ip"] = i;
                    foundKnown = true;
                    break;
                case "escola":
                    if (!map.ContainsKey("escola")) map["escola"] = i;
                    foundKnown = true;
                    break;
                case "dispositivo":
                case "equipamento":
                case "device":
                    if (!map.ContainsKey("dispositivo")) map["dispositivo"] = i;
                    foundKnown = true;
                    break;
            }
        }

        return foundKnown ? map : null;
    }

    private static string CellAt(IReadOnlyList<string> cells, int index)
    {
        if (index < 0 || index >= cells.Count)
            return "";
        return cells[index].Trim();
    }

    /// <summary>
    /// Splits a CSV line on the delimiter, honoring double-quoted cells that may
    /// contain the delimiter. Surrounding quotes are stripped.
    /// </summary>
    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == delimiter && !inQuotes)
            {
                cells.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }
        cells.Add(sb.ToString());

        return cells;
    }

    /// <summary>
    /// Lower-cases, trims and removes diacritics so header matching is case-
    /// and accent-insensitive (e.g. "Escola" / "ESCOLA" -> "escola").
    /// </summary>
    private static string Normalize(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        var decomposed = trimmed.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
