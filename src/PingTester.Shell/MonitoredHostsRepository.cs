using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PingTester.Core.Ips;

namespace PingTester.Shell;

/// <summary>
/// IMPERATIVE SHELL — impure. Reads the fixed list of monitored hosts from a
/// CSV file on the server and delegates the actual IP extraction to the pure
/// <see cref="CsvIpExtractor"/> in the functional core.
///
/// The file location is configured once (e.g. "monitored-hosts.csv" next to
/// the app). Missing file => empty list (the UI shows "nenhum host").
/// </summary>
public sealed class MonitoredHostsRepository
{
    private readonly string _csvPath;

    public MonitoredHostsRepository(string csvPath)
    {
        _csvPath = csvPath;
    }

    /// <summary>Absolute path being read (useful for diagnostics/UI).</summary>
    public string CsvPath => _csvPath;

    /// <summary>True if the configured CSV file exists.</summary>
    public bool Exists => File.Exists(_csvPath);

    public async Task<IReadOnlyList<MonitoredHost>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_csvPath))
            return System.Array.Empty<MonitoredHost>();

        var text = await File.ReadAllTextAsync(_csvPath, cancellationToken).ConfigureAwait(false);

        // Pure core parses the structured CSV (ip + escola + dispositivo).
        return MonitoredHostCsvParser.Parse(text);
    }
}
