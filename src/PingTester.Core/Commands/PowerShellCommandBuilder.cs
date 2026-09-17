using System;
using System.Collections.Generic;
using PingTester.Core.Ips;

namespace PingTester.Core.Commands;

/// <summary>
/// FUNCTIONAL CORE — pure. Builds the command specs for ping and tracert using
/// the PowerShell cmdlets Test-Connection and Test-NetConnection -TraceRoute.
/// It only produces data; it never touches the network or a process.
/// </summary>
public static class PowerShellCommandBuilder
{
    /// <summary>Number of echo requests sent by a ping.</summary>
    public const int PingCount = 4;

    /// <summary>Maximum number of hops for the traceroute.</summary>
    public const int MaxHops = 15;

    /// <summary>Bounds accepted for the ping packet count.</summary>
    public const int MinPingCount = 1;
    public const int MaxPingCount = 10;

    /// <summary>
    /// Builds: ping -n &lt;count&gt; [-w &lt;timeoutMs&gt;] &lt;ip&gt;
    /// Uses the classic ping.exe (works via ICMP, same as the CMD prompt) —
    /// the Windows PowerShell 5.1 Test-Connection cmdlet fails via WMI on the
    /// target environment.
    ///
    /// <paramref name="count"/> defaults to 4 (full ping, manual tab) and is
    /// clamped to 1..10. <paramref name="timeoutMs"/> is optional: when set, it
    /// adds "-w" so that unresponsive hosts give up quickly — the fast
    /// monitoring sweep passes count=2, timeoutMs=1000.
    /// </summary>
    public static PowerShellCommandSpec BuildPing(HostAddress host, int count = PingCount, int? timeoutMs = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        var packets = Math.Clamp(count, MinPingCount, MaxPingCount);

        var args = new Dictionary<string, string>
        {
            ["-n"] = packets.ToString(),
        };

        if (timeoutMs is int ms && ms > 0)
            args["-w"] = ms.ToString();

        return new PowerShellCommandSpec(
            command: "ping",
            arguments: args,
            switches: Array.Empty<string>(),
            target: host.Value);
    }

    /// <summary>Absolute bounds accepted for a tracert hop limit.</summary>
    public const int MinHopLimit = 1;
    public const int MaxHopLimit = 255;

    /// <summary>
    /// <summary>Per-hop timeout (ms) for tracert. Keeps offline destinations
    /// from hanging on every unresponsive hop.</summary>
    public const int TracertPerHopTimeoutMs = 1000;

    /// Builds: tracert -d -h &lt;maxHops&gt; -w 1000 &lt;ip&gt;
    /// -d skips reverse-DNS (faster); -h caps the hop count; -w bounds the wait
    /// per hop so unresponsive/offline routes finish quickly.
    /// The hop limit defaults to <see cref="MaxHops"/> (15) and is clamped to
    /// the valid 1..255 range.
    /// </summary>
    public static PowerShellCommandSpec BuildTracert(HostAddress host, int maxHops = MaxHops)
    {
        ArgumentNullException.ThrowIfNull(host);

        var hops = Math.Clamp(maxHops, MinHopLimit, MaxHopLimit);

        var args = new Dictionary<string, string>
        {
            ["-h"] = hops.ToString(),
            ["-w"] = TracertPerHopTimeoutMs.ToString(),
        };

        return new PowerShellCommandSpec(
            command: "tracert",
            arguments: args,
            switches: new[] { "-d" },
            target: host.Value);
    }
}
