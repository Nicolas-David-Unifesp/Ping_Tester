using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PingTester.Core.Ips;
using PingTester.Shell;

namespace PingTester.Web;

/// <summary>
/// Coordinates the monitoring tab: loads the fixed host list from CSV, runs a
/// fast ping-only sweep in parallel, and caches the result for a short window
/// so that reopening the tab (or several users) doesn't re-run everything.
/// A forced refresh bypasses the cache.
/// </summary>
public sealed class MonitorService
{
    private readonly MonitoredHostsRepository _repository;
    private readonly NetworkTestOrchestrator _orchestrator;
    private readonly TimeSpan _cacheTtl;
    private readonly int _pingParallelism;
    private readonly int _traceHops;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private MonitorResponse? _cached;
    private DateTimeOffset _cachedAt;

    public MonitorService(
        MonitoredHostsRepository repository,
        NetworkTestOrchestrator orchestrator,
        TimeSpan cacheTtl,
        int pingParallelism,
        int traceHops)
    {
        _repository = repository;
        _orchestrator = orchestrator;
        _cacheTtl = cacheTtl;
        _pingParallelism = pingParallelism;
        _traceHops = traceHops;
    }

    public int TraceHops => _traceHops;

    /// <summary>
    /// Returns the monitoring snapshot. Uses the cache unless it is stale or
    /// <paramref name="forceRefresh"/> is set (the "Atualizar" button).
    /// </summary>
    public async Task<MonitorResponse> GetStatusAsync(bool forceRefresh, CancellationToken ct)
    {
        // Fast path: fresh cache and no forced refresh.
        if (!forceRefresh && _cached is not null &&
            DateTimeOffset.UtcNow - _cachedAt < _cacheTtl)
        {
            return _cached with { FromCache = true };
        }

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring the lock (another request may have just
            // refreshed it).
            if (!forceRefresh && _cached is not null &&
                DateTimeOffset.UtcNow - _cachedAt < _cacheTtl)
            {
                return _cached with { FromCache = true };
            }

            var hosts = await _repository.LoadAsync(ct).ConfigureAwait(false);
            var reports = await _orchestrator
                .PingAllAsync(hosts, _pingParallelism, ct)
                .ConfigureAwait(false);

            var items = reports.Select(r => new MonitorItemDto(
                Target: r.Target,
                Online: r.Ping?.IsReachable ?? false,
                AverageLatencyMs: r.Ping?.AverageLatencyMs,
                LossPercentage: r.Ping?.LossPercentage ?? 100,
                Error: r.Error)).ToList();

            var response = new MonitorResponse(
                Items: items,
                Total: items.Count,
                Online: items.Count(i => i.Online),
                CheckedAt: DateTimeOffset.UtcNow,
                FromCache: false,
                SourceExists: _repository.Exists,
                SourcePath: _repository.CsvPath);

            _cached = response;
            _cachedAt = DateTimeOffset.UtcNow;
            return response;
        }
        finally
        {
            _lock.Release();
        }
    }
}
