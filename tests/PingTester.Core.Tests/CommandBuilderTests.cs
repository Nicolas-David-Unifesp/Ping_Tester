using System.Linq;
using PingTester.Core.Ips;
using PingTester.Core.Commands;

namespace PingTester.Core.Tests;

/// <summary>
/// TDD: the pure core decides WHAT command to run (as data) but never executes
/// it. We use the classic executables ping.exe / tracert.exe (the cmdlets fail
/// via WMI on Windows PowerShell 5.1 in the target environment). Only a
/// validated HostAddress can reach the builder, and the IP is carried as the
/// discrete positional Target (never interpolated into a script).
/// </summary>
public class CommandBuilderTests
{
    private static HostAddress Host(string ip) =>
        HostAddress.TryCreate(ip) ?? throw new System.Exception($"test setup: {ip} invalid");

    [Test]
    public void Ping_UsesPingExecutable()
    {
        var spec = PowerShellCommandBuilder.BuildPing(Host("8.8.8.8"));
        Assert.Equal("ping", spec.Command);
    }

    [Test]
    public void Ping_CarriesTargetAsPositional()
    {
        var spec = PowerShellCommandBuilder.BuildPing(Host("8.8.8.8"));
        Assert.Equal("8.8.8.8", spec.Target);
    }

    [Test]
    public void Ping_SpecifiesCountViaDashN()
    {
        var spec = PowerShellCommandBuilder.BuildPing(Host("8.8.8.8"));
        Assert.Equal("4", spec.Arguments["-n"]);
    }

    [Test]
    public void Tracert_UsesTracertExecutable()
    {
        var spec = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"));
        Assert.Equal("tracert", spec.Command);
    }

    [Test]
    public void Tracert_DefaultsToHopLimit15()
    {
        var spec = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"));
        Assert.Equal("15", spec.Arguments["-h"]);
    }

    [Test]
    public void Tracert_AcceptsCustomHopLimit()
    {
        // The monitoring tab uses a shorter trace (8 hops) for speed.
        var spec = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"), maxHops: 8);
        Assert.Equal("8", spec.Arguments["-h"]);
    }

    [Test]
    public void Tracert_ClampsHopLimitToSaneRange()
    {
        // Guard against absurd values; hop count must stay 1..255.
        var tooLow = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"), maxHops: 0);
        Assert.Equal("1", tooLow.Arguments["-h"]);

        var tooHigh = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"), maxHops: 999);
        Assert.Equal("255", tooHigh.Arguments["-h"]);
    }

    [Test]
    public void Tracert_UsesNumericFlag_SkipsDns()
    {
        var spec = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"));
        Assert.True(spec.Switches.Contains("-d"), "tracert should use -d to skip reverse DNS");
    }

    [Test]
    public void Tracert_CarriesTargetAsPositional()
    {
        var spec = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"));
        Assert.Equal("1.1.1.1", spec.Target);
    }

    [Test]
    public void Spec_RendersHumanReadableCommandLine()
    {
        var spec = PowerShellCommandBuilder.BuildPing(Host("8.8.8.8"));

        // Illustrative only (the shell passes discrete args). Should read like:
        // "ping -n 4 8.8.8.8"
        var text = spec.ToDisplayString();
        Assert.True(text.Contains("ping"), "should name the executable");
        Assert.True(text.Contains("8.8.8.8"), "should show the target");
        Assert.True(text.Contains("-n"), "should show the count flag");
    }
}
