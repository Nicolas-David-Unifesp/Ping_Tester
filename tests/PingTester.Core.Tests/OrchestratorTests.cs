using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PingTester.Core.Commands;
using PingTester.Core.Ips;
using PingTester.Shell;

namespace PingTester.Core.Tests;

/// <summary>
/// The orchestrator lives in the shell but its WIRING (core -> executor ->
/// core) is deterministic and can be tested offline with a fake executor that
/// returns canned JSON instead of running PowerShell.
/// </summary>
public class OrchestratorTests
{
    /// <summary>Fake executor: returns canned ping/tracert text output.</summary>
    private sealed class FakeExecutor : IPowerShellExecutor
    {
        public List<string> Executed { get; } = new();
        public List<PowerShellCommandSpec> Specs { get; } = new();

        public Task<string> ExecuteAsync(PowerShellCommandSpec spec, CancellationToken cancellationToken = default)
        {
            Executed.Add(spec.Command);
            Specs.Add(spec);

            // The fake now returns the classic ping.exe / tracert.exe TEXT
            // output (Portuguese), matching what the real executor captures.
            string text = spec.Command switch
            {
                "ping" =>
@"Disparando 8.8.8.8 com 32 bytes de dados:
Resposta de 8.8.8.8: bytes=32 tempo=10ms TTL=58
Resposta de 8.8.8.8: bytes=32 tempo=20ms TTL=58

Estatísticas do Ping para 8.8.8.8:
    Pacotes: Enviados = 2, Recebidos = 2, Perdidos = 0 (0% de
             perda),",
                "tracert" =>
@"Rastreando a rota para 8.8.8.8 com no máximo 15 saltos

  1     1 ms     1 ms     1 ms  192.168.0.1
  2    10 ms    20 ms    15 ms  8.8.8.8

Rastreamento concluído.",
                _ => ""
            };
            return Task.FromResult(text);
        }
    }

    private static HostAddress Host(string ip) =>
        HostAddress.TryCreate(ip) ?? throw new System.Exception("test setup");

    [Test]
    public void RunsBothPingAndTracert_PerHost()
    {
        var fake = new FakeExecutor();
        var orch = new NetworkTestOrchestrator(fake);

        var reports = orch.RunAsync(new[] { Host("8.8.8.8") }).GetAwaiter().GetResult();

        Assert.Contains("ping", fake.Executed);
        Assert.Contains("tracert", fake.Executed);
        Assert.Count(1, reports);
    }

    [Test]
    public void ParsesPingResult_ThroughCore()
    {
        var orch = new NetworkTestOrchestrator(new FakeExecutor());

        var report = orch.RunAsync(new[] { Host("8.8.8.8") }).GetAwaiter().GetResult().Single();

        Assert.NotNull(report.Ping);
        Assert.Equal(2, report.Ping!.PacketsReceived);
        Assert.Equal(15.0, report.Ping.AverageLatencyMs); // (10+20)/2
        Assert.Null(report.Error);
    }

    [Test]
    public void ParsesTraceResult_ThroughCore()
    {
        var orch = new NetworkTestOrchestrator(new FakeExecutor());

        var report = orch.RunAsync(new[] { Host("8.8.8.8") }).GetAwaiter().GetResult().Single();

        Assert.NotNull(report.Trace);
        Assert.Count(2, report.Trace!.Hops);
        Assert.True(report.Trace.DestinationReached);
    }

    [Test]
    public void MultipleHosts_AllProduceReports()
    {
        var orch = new NetworkTestOrchestrator(new FakeExecutor());

        var reports = orch.RunAsync(new[] { Host("8.8.8.8"), Host("1.1.1.1"), Host("9.9.9.9") })
                          .GetAwaiter().GetResult();

        Assert.Count(3, reports);
    }

    [Test]
    public void ExecutorThrows_IsCapturedAsError_NotCrash()
    {
        var orch = new NetworkTestOrchestrator(new ThrowingExecutor());

        var report = orch.RunAsync(new[] { Host("8.8.8.8") }).GetAwaiter().GetResult().Single();

        Assert.Null(report.Ping);
        Assert.NotNull(report.Error);
    }

    private sealed class ThrowingExecutor : IPowerShellExecutor
    {
        public Task<string> ExecuteAsync(PowerShellCommandSpec spec, CancellationToken cancellationToken = default)
            => throw new System.InvalidOperationException("boom");
    }

    // ---- Monitoring tab: ping-only (fast) --------------------------------

    [Test]
    public void PingAll_RunsOnlyPing_NoTracert()
    {
        var fake = new FakeExecutor();
        var orch = new NetworkTestOrchestrator(fake);

        var results = orch.PingAllAsync(new[] { Host("8.8.8.8"), Host("1.1.1.1") })
                          .GetAwaiter().GetResult();

        Assert.Count(2, results);
        Assert.Contains("ping", fake.Executed);
        Assert.False(fake.Executed.Contains("tracert"),
            "monitoring open must NOT run tracert (too slow)");
    }

    [Test]
    public void PingAll_ReportsReachability()
    {
        var orch = new NetworkTestOrchestrator(new FakeExecutor());

        var result = orch.PingAllAsync(new[] { Host("8.8.8.8") }).GetAwaiter().GetResult().Single();

        Assert.NotNull(result.Ping);
        Assert.True(result.Ping!.IsReachable);
        Assert.Null(result.Trace); // no trace on the fast path
    }

    // ---- Monitoring tab: on-demand trace with custom hop limit -----------

    [Test]
    public void Trace_UsesRequestedHopLimit()
    {
        var fake = new FakeExecutor();
        var orch = new NetworkTestOrchestrator(fake);

        orch.TraceAsync(Host("8.8.8.8"), maxHops: 8).GetAwaiter().GetResult();

        var traceSpec = fake.Specs.Single(s => s.Command == "tracert");
        Assert.Equal("8", traceSpec.Arguments["-h"]);
    }

    [Test]
    public void Trace_ReturnsParsedTrace()
    {
        var orch = new NetworkTestOrchestrator(new FakeExecutor());

        var report = orch.TraceAsync(Host("8.8.8.8"), maxHops: 8).GetAwaiter().GetResult();

        Assert.NotNull(report.Trace);
        Assert.Count(2, report.Trace!.Hops);
        Assert.Null(report.Ping); // trace-only path
    }
}
