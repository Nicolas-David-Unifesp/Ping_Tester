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
///   core builds spec  ->  shell executes (JSON)  ->  core parses JSON  ->  result
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

            // 2) Impure shell executes them, capturing JSON.
            var pingJson = await _executor.ExecuteAsync(pingSpec, ct).ConfigureAwait(false);
            var traceJson = await _executor.ExecuteAsync(traceSpec, ct).ConfigureAwait(false);

            // 3) Pure core parses the raw output back into domain results.
            var ping = PingOutputParser.Parse(host.Value, pingJson);
            var trace = TracertOutputParser.Parse(host.Value, traceJson);

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
}
