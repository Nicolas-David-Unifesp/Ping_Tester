using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PingTester.Core.Commands;
using PingTester.Core.Ips;
using PingTester.Core.Results;

namespace PingTester.Shell;

/// <summary>
/// IMPERATIVE SHELL — orchestration. Wires the pure functional core to the
/// impure <see cref="IPowerShellExecutor"/>:
///
///   core builds spec  ->  shell executes (text)  ->  core parses text  ->  result
///
/// The orchestrator itself holds no parsing/validation logic; it only sequences
/// pure calls around the single side effect. Runs hosts concurrently with a
/// bounded degree of parallelism.
/// </summary>
public sealed class NetworkTestOrchestrator
{
    private readonly IPowerShellExecutor _executor;
    private readonly int _maxParallelism;

    public NetworkTestOrchestrator(IPowerShellExecutor executor, int maxParallelism = 4)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _maxParallelism = Math.Max(1, maxParallelism);
    }

    /// <summary>
    /// FAST PATH for the monitoring tab: runs ONLY ping for every host,
    /// concurrently. No tracert (which is slow). Suitable for ~70 hosts.
    /// The parallelism can be raised for this path via <paramref name="parallelism"/>.
    /// </summary>
    public async Task<IReadOnlyList<HostTestReport>> PingAllAsync(
        IReadOnlyList<HostAddress> hosts,
        int parallelism = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hosts);

        var degree = parallelism > 0 ? parallelism : _maxParallelism;
        using var gate = new SemaphoreSlim(degree);

        var tasks = hosts.Select(async host =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await PingHostAsync(host, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        });

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// ON-DEMAND path for the monitoring tab: runs tracert for a single host
    /// with a custom hop limit (the monitoring tab uses 8). Ping is not run.
    /// </summary>
    public async Task<HostTestReport> TraceAsync(
        HostAddress host,
        int maxHops = PowerShellCommandBuilder.MaxHops,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        try
        {
            var traceSpec = PowerShellCommandBuilder.BuildTracert(host, maxHops);
            var traceText = await _executor.ExecuteAsync(traceSpec, cancellationToken).ConfigureAwait(false);
            var trace = TracertOutputParser.Parse(host.Value, traceText);
            return new HostTestReport(host.Value, Ping: null, Trace: trace, Error: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new HostTestReport(host.Value, Ping: null, Trace: null, Error: ex.Message);
        }
    }

    public async Task<IReadOnlyList<HostTestReport>> RunAsync(
        IReadOnlyList<HostAddress> hosts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hosts);

        using var gate = new SemaphoreSlim(_maxParallelism);

        var tasks = hosts.Select(async host =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await TestHostAsync(host, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        });

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<HostTestReport> TestHostAsync(HostAddress host, CancellationToken ct)
    {
        try
        {
            // 1) Pure core decides the commands.
            var pingSpec = PowerShellCommandBuilder.BuildPing(host);
            var traceSpec = PowerShellCommandBuilder.BuildTracert(host);

            // 2) Impure shell executes them, capturing text output.
            var pingText = await _executor.ExecuteAsync(pingSpec, ct).ConfigureAwait(false);
            var traceText = await _executor.ExecuteAsync(traceSpec, ct).ConfigureAwait(false);

            // 3) Pure core parses the raw output back into domain results.
            var ping = PingOutputParser.Parse(host.Value, pingText);
            var trace = TracertOutputParser.Parse(host.Value, traceText);

            return new HostTestReport(host.Value, ping, trace, Error: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new HostTestReport(host.Value, Ping: null, Trace: null, Error: ex.Message);
        }
    }

    private async Task<HostTestReport> PingHostAsync(HostAddress host, CancellationToken ct)
    {
        try
        {
            var pingSpec = PowerShellCommandBuilder.BuildPing(host);
            var pingText = await _executor.ExecuteAsync(pingSpec, ct).ConfigureAwait(false);
            var ping = PingOutputParser.Parse(host.Value, pingText);
            return new HostTestReport(host.Value, ping, Trace: null, Error: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new HostTestReport(host.Value, Ping: null, Trace: null, Error: ex.Message);
        }
    }
}
