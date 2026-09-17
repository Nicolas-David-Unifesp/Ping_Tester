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

    /// <summary>
    /// Builds: ping -n 4 &lt;ip&gt;
    /// Uses the classic ping.exe (works via ICMP, same as the CMD prompt) —
    /// the Windows PowerShell 5.1 Test-Connection cmdlet fails via WMI on the
    /// target environment.
    /// </summary>
    public static PowerShellCommandSpec BuildPing(HostAddress host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var args = new Dictionary<string, string>
        {
            ["-n"] = PingCount.ToString(),
        };

        return new PowerShellCommandSpec(
            command: "ping",
            arguments: args,
            switches: Array.Empty<string>(),
            target: host.Value);
    }

    /// <summary>
    /// Builds: tracert -d -h 15 &lt;ip&gt;
    /// -d skips reverse-DNS (faster); -h 15 caps the hop count.
    /// </summary>
    public static PowerShellCommandSpec BuildTracert(HostAddress host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var args = new Dictionary<string, string>
        {
            ["-h"] = MaxHops.ToString(),
        };

        return new PowerShellCommandSpec(
            command: "tracert",
            arguments: args,
            switches: new[] { "-d" },
            target: host.Value);
    }
}
