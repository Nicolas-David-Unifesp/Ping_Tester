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

    public static PowerShellCommandSpec BuildPing(HostAddress host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var args = new Dictionary<string, string>
        {
            ["-TargetName"] = host.Value,
            ["-Count"] = PingCount.ToString(),
        };

        return new PowerShellCommandSpec(
            command: "Test-Connection",
            arguments: args,
            switches: Array.Empty<string>());
    }

    public static PowerShellCommandSpec BuildTracert(HostAddress host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var args = new Dictionary<string, string>
        {
            ["-ComputerName"] = host.Value,
        };

        return new PowerShellCommandSpec(
            command: "Test-NetConnection",
            arguments: args,
            switches: new[] { "-TraceRoute" });
    }
}
