using System.Linq;
using PingTester.Core.Results;

namespace PingTester.Core.Tests;

/// <summary>
/// TDD (RED first): parse the TEXT output of the classic Windows ping.exe /
/// tracert.exe (Portuguese locale). Pure functions — no process, no network.
/// The samples below are the real output captured on the target machine.
/// </summary>
public class PingOutputParserTests
{
    // Real output of: ping -n 4 127.0.0.1  (all successful, sub-millisecond)
    private const string SuccessLoopback =
@"Disparando 127.0.0.1 com 32 bytes de dados:
Resposta de 127.0.0.1: bytes=32 tempo<1ms TTL=128
Resposta de 127.0.0.1: bytes=32 tempo<1ms TTL=128
Resposta de 127.0.0.1: bytes=32 tempo<1ms TTL=128
Resposta de 127.0.0.1: bytes=32 tempo<1ms TTL=128

Estatísticas do Ping para 127.0.0.1:
    Pacotes: Enviados = 4, Recebidos = 4, Perdidos = 0 (0% de
             perda),
Aproximar um número redondo de vezes em milissegundos:
    Mínimo = 0ms, Máximo = 0ms, Média = 0ms";

    // Real output of: ping -n 4 10.113.96.148  (all successful, tempo=Xms)
    private const string SuccessRemote =
@"Disparando 10.113.96.148 com 32 bytes de dados:
Resposta de 10.113.96.148: bytes=32 tempo=9ms TTL=58
Resposta de 10.113.96.148: bytes=32 tempo=11ms TTL=58
Resposta de 10.113.96.148: bytes=32 tempo=9ms TTL=58
Resposta de 10.113.96.148: bytes=32 tempo=12ms TTL=58

Estatísticas do Ping para 10.113.96.148:
    Pacotes: Enviados = 4, Recebidos = 4, Perdidos = 0 (0% de
             perda),
Aproximar um número redondo de vezes em milissegundos:
    Mínimo = 9ms, Máximo = 12ms, Média = 10ms";

    // Real output of: ping 8.8.8.8  (100% loss / timeouts)
    private const string AllTimeout =
@"Disparando 8.8.8.8 com 32 bytes de dados:
Esgotado o tempo limite do pedido.
Esgotado o tempo limite do pedido.
Esgotado o tempo limite do pedido.
Esgotado o tempo limite do pedido.

Estatísticas do Ping para 8.8.8.8:
    Pacotes: Enviados = 4, Recebidos = 0, Perdidos = 4 (100% de
             perda),";

    [Test]
    public void SuccessRemote_CountsSentAndReceived()
    {
        var result = PingOutputParser.Parse("10.113.96.148", SuccessRemote);

        Assert.Equal(4, result.PacketsSent);
        Assert.Equal(4, result.PacketsReceived);
        Assert.Equal(0, result.PacketsLost);
        Assert.True(result.IsReachable, "replies were received");
    }

    [Test]
    public void SuccessRemote_ComputesAverageLatency()
    {
        var result = PingOutputParser.Parse("10.113.96.148", SuccessRemote);

        // (9 + 11 + 9 + 12) / 4 = 10.25
        Assert.Equal(10.25, result.AverageLatencyMs);
    }

    [Test]
    public void SuccessLoopback_SubMillisecond_TreatedAsZero()
    {
        var result = PingOutputParser.Parse("127.0.0.1", SuccessLoopback);

        Assert.Equal(4, result.PacketsReceived);
        Assert.True(result.IsReachable);
        // "tempo<1ms" is counted as a successful reply with latency 0.
        Assert.Equal(0.0, result.AverageLatencyMs);
    }

    [Test]
    public void AllTimeout_IsUnreachable()
    {
        var result = PingOutputParser.Parse("8.8.8.8", AllTimeout);

        Assert.Equal(4, result.PacketsSent);
        Assert.Equal(0, result.PacketsReceived);
        Assert.Equal(4, result.PacketsLost);
        Assert.False(result.IsReachable, "no replies");
        Assert.Null(result.AverageLatencyMs);
    }

    [Test]
    public void AllTimeout_LossIs100Percent()
    {
        var result = PingOutputParser.Parse("8.8.8.8", AllTimeout);
        Assert.Equal(100.0, result.LossPercentage);
    }

    [Test]
    public void PartialLoss_IsCountedFromStatisticsLine()
    {
        // 4 sent, 3 received -> reachable, 1 lost. Latency lines present for
        // the successful replies only.
        var text =
@"Disparando 10.0.0.5 com 32 bytes de dados:
Resposta de 10.0.0.5: bytes=32 tempo=5ms TTL=64
Esgotado o tempo limite do pedido.
Resposta de 10.0.0.5: bytes=32 tempo=7ms TTL=64
Resposta de 10.0.0.5: bytes=32 tempo=6ms TTL=64

Estatísticas do Ping para 10.0.0.5:
    Pacotes: Enviados = 4, Recebidos = 3, Perdidos = 1 (25% de
             perda),";

        var result = PingOutputParser.Parse("10.0.0.5", text);

        Assert.Equal(4, result.PacketsSent);
        Assert.Equal(3, result.PacketsReceived);
        Assert.Equal(1, result.PacketsLost);
        Assert.True(result.IsReachable);
        // Average of successful replies: (5 + 7 + 6) / 3 = 6
        Assert.Equal(6.0, result.AverageLatencyMs);
    }

    [Test]
    public void UnreachableHost_IsNotCountedAsReply()
    {
        // "Host de destino inacessível" is a failure, not a reply.
        var text =
@"Disparando 192.168.1.9 com 32 bytes de dados:
Resposta de 192.168.1.1: Host de destino inacessível.
Resposta de 192.168.1.1: Host de destino inacessível.

Estatísticas do Ping para 192.168.1.9:
    Pacotes: Enviados = 2, Recebidos = 0, Perdidos = 2 (100% de
             perda),";

        var result = PingOutputParser.Parse("192.168.1.9", text);

        Assert.Equal(0, result.PacketsReceived);
        Assert.False(result.IsReachable, "host unreachable is not a successful reply");
    }

    [Test]
    public void Target_IsPreserved()
    {
        var result = PingOutputParser.Parse("10.113.96.148", SuccessRemote);
        Assert.Equal("10.113.96.148", result.Target);
    }

    [Test]
    public void EmptyOutput_IsUnreachable()
    {
        var result = PingOutputParser.Parse("8.8.8.8", "");

        Assert.Equal(0, result.PacketsReceived);
        Assert.False(result.IsReachable);
    }
}

public class TracertOutputParserTests
{
    // Real output of: tracert -d -h 15 10.113.96.148  (destination reached)
    private const string TraceReached =
@"Rastreando a rota para 10.113.96.148 com no máximo 15 saltos

  1    <1 ms    <1 ms    <1 ms  10.181.44.1
  2    <1 ms    <1 ms    <1 ms  10.127.103.45
  3     3 ms     3 ms     4 ms  201.61.225.13
  4    10 ms     8 ms    12 ms  189.9.247.61
  5     9 ms     8 ms    10 ms  189.9.247.62
  6    10 ms    12 ms    12 ms  10.127.53.234
  7     9 ms    12 ms    12 ms  10.113.96.148

Rastreamento concluído.";

    // Real output of: tracert -d -h 15 8.8.8.8  (all hops time out)
    private const string TraceAllTimeout =
@"Rastreando a rota para 8.8.8.8 com no máximo 15 saltos

  1     *        *        *     Esgotado o tempo limite do pedido.
  2     *        *        *     Esgotado o tempo limite do pedido.
  3     *        *        *     Esgotado o tempo limite do pedido.

Rastreamento concluído.";

    [Test]
    public void Reached_ParsesAllHopsInOrder()
    {
        var result = TracertOutputParser.Parse("10.113.96.148", TraceReached);

        Assert.Count(7, result.Hops);
        Assert.Equal(1, result.Hops[0].Number);
        Assert.Equal("10.181.44.1", result.Hops[0].Address);
        Assert.Equal(7, result.Hops[6].Number);
        Assert.Equal("10.113.96.148", result.Hops[6].Address);
    }

    [Test]
    public void Reached_LastHopEqualsTarget_MarksDestinationReached()
    {
        var result = TracertOutputParser.Parse("10.113.96.148", TraceReached);

        Assert.True(result.DestinationReached, "last hop is the target");
        Assert.Equal("10.113.96.148", result.Target);
    }

    [Test]
    public void AllTimeout_ProducesTimedOutHops_WithNoAddress()
    {
        var result = TracertOutputParser.Parse("8.8.8.8", TraceAllTimeout);

        // Hops that only show "* * *" have no resolvable address.
        Assert.Count(3, result.Hops);
        foreach (var hop in result.Hops)
            Assert.Equal("", hop.Address);
    }

    [Test]
    public void AllTimeout_DestinationNotReached()
    {
        var result = TracertOutputParser.Parse("8.8.8.8", TraceAllTimeout);
        Assert.False(result.DestinationReached);
    }

    [Test]
    public void EmptyOutput_ProducesNoHops()
    {
        var result = TracertOutputParser.Parse("8.8.8.8", "");

        Assert.Empty(result.Hops);
        Assert.False(result.DestinationReached);
    }

    [Test]
    public void PartialOutput_WithInterruptedTrailer_ParsesHopsIgnoresTrailer()
    {
        // When a slow tracert is interrupted, the executor appends a trailer
        // line. The parser should still read the real hops and ignore it.
        var text =
@"Rastreando a rota para 8.8.8.8 com no máximo 15 saltos

  1    <1 ms    <1 ms    <1 ms  10.181.44.1
  2     *        *        *     Esgotado o tempo limite do pedido.
[Interrompido: 'tracert' excedeu o tempo limite.]";

        var result = TracertOutputParser.Parse("8.8.8.8", text);

        Assert.Count(2, result.Hops);
        Assert.Equal("10.181.44.1", result.Hops[0].Address);
        Assert.Equal("", result.Hops[1].Address);
        Assert.False(result.DestinationReached);
    }
}
