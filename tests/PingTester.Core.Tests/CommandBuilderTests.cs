using PingTester.Core.Ips;
using PingTester.Core.Commands;

namespace PingTester.Core.Tests;

/// <summary>
/// TDD (RED first): the pure core decides WHAT PowerShell command to run
/// (as data) but never executes it. The shell executes the produced spec.
/// This keeps command construction testable and, crucially, safe: only a
/// validated HostAddress can reach the builder, and the value is passed as a
/// parameter argument rather than concatenated into a script string.
/// </summary>
public class CommandBuilderTests
{
    private static HostAddress Host(string ip) =>
        HostAddress.TryCreate(ip) ?? throw new System.Exception($"test setup: {ip} invalid");

    [Test]
    public void Ping_UsesTestConnectionCmdlet()
    {
        var spec = PowerShellCommandBuilder.BuildPing(Host("8.8.8.8"));

        Assert.Equal("Test-Connection", spec.Command);
    }

    [Test]
    public void Ping_PassesTargetAsArgument_NotConcatenated()
    {
        var spec = PowerShellCommandBuilder.BuildPing(Host("8.8.8.8"));

        // The IP must appear as a discrete argument value so the shell can pass
        // it via parameter binding (no string interpolation into a script).
        Assert.Equal("8.8.8.8", spec.Arguments["-TargetName"]);
    }

    [Test]
    public void Ping_SpecifiesCount()
    {
        var spec = PowerShellCommandBuilder.BuildPing(Host("8.8.8.8"));

        Assert.Equal("4", spec.Arguments["-Count"]);
    }

    [Test]
    public void Tracert_UsesTestNetConnectionCmdlet()
    {
        var spec = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"));

        Assert.Equal("Test-NetConnection", spec.Command);
    }

    [Test]
    public void Tracert_RequestsTraceRoute()
    {
        var spec = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"));

        Assert.True(spec.Switches.Contains("-TraceRoute"),
            "tracert spec must include the -TraceRoute switch");
    }

    [Test]
    public void Tracert_PassesTargetAsArgument()
    {
        var spec = PowerShellCommandBuilder.BuildTracert(Host("1.1.1.1"));

        Assert.Equal("1.1.1.1", spec.Arguments["-ComputerName"]);
    }

    [Test]
    public void Spec_RendersHumanReadableCommandLine()
    {
        var spec = PowerShellCommandBuilder.BuildPing(Host("8.8.8.8"));

        // A readable representation is handy for logs / the UI. It is NOT what
        // gets executed (the shell binds parameters), but must reflect the spec.
        var text = spec.ToDisplayString();
        Assert.True(text.Contains("Test-Connection"), "should name the cmdlet");
        Assert.True(text.Contains("8.8.8.8"), "should show the target");
    }
}
