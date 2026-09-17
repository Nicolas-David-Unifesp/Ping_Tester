using System.Linq;
using PingTester.Core.Ips;

namespace PingTester.Core.Tests;

/// <summary>
/// TDD (RED first): specifies how raw user text is turned into a list of valid
/// IPs plus a list of errors. Pure function, no I/O.
/// </summary>
public class IpParserTests
{
    [Test]
    public void SingleValidIpv4_IsParsed()
    {
        var result = IpParser.Parse("8.8.8.8");

        Assert.Count(1, result.ValidHosts);
        Assert.Equal("8.8.8.8", result.ValidHosts[0].Value);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void MultipleLines_AreParsedIndividually()
    {
        var result = IpParser.Parse("8.8.8.8\n1.1.1.1\n192.168.0.1");

        Assert.Count(3, result.ValidHosts);
        Assert.Empty(result.Errors);
        Assert.Contains("1.1.1.1", result.ValidHosts.Select(h => h.Value).ToList());
    }

    [Test]
    public void BlankLinesAndWhitespace_AreIgnored()
    {
        var result = IpParser.Parse("  8.8.8.8  \n\n   \n1.1.1.1\n");

        Assert.Count(2, result.ValidHosts);
        Assert.Empty(result.Errors);
        Assert.Equal("8.8.8.8", result.ValidHosts[0].Value);
    }

    [Test]
    public void CommaAndSemicolonSeparated_AreSplit()
    {
        var result = IpParser.Parse("8.8.8.8, 1.1.1.1; 9.9.9.9");

        Assert.Count(3, result.ValidHosts);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void InvalidOctet_ProducesError()
    {
        var result = IpParser.Parse("999.1.1.1");

        Assert.Empty(result.ValidHosts);
        Assert.Count(1, result.Errors);
        Assert.Equal("999.1.1.1", result.Errors[0].RawValue);
    }

    [Test]
    public void ValidAndInvalidMixed_AreSeparated()
    {
        var result = IpParser.Parse("8.8.8.8\nnot-an-ip\n1.1.1.1");

        Assert.Count(2, result.ValidHosts);
        Assert.Count(1, result.Errors);
        Assert.Equal("not-an-ip", result.Errors[0].RawValue);
    }

    [Test]
    public void ValidIpv6_IsParsed()
    {
        var result = IpParser.Parse("2001:4860:4860::8888");

        Assert.Count(1, result.ValidHosts);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void DuplicateIps_AreDeduplicated()
    {
        var result = IpParser.Parse("8.8.8.8\n8.8.8.8\n8.8.8.8");

        Assert.Count(1, result.ValidHosts);
    }

    [Test]
    public void CommandInjectionAttempt_IsRejected()
    {
        // Security: the malicious fragment must NEVER become a valid host.
        // The clean IP part may be accepted, but "Remove-Item ..." must land
        // in Errors so it can never reach the PowerShell shell.
        var result = IpParser.Parse("8.8.8.8; Remove-Item C:\\ -Recurse");

        Assert.Count(1, result.ValidHosts);
        Assert.Equal("8.8.8.8", result.ValidHosts[0].Value);
        Assert.Count(1, result.Errors);
        Assert.False(result.ValidHosts.Any(h => h.Value.Contains("Remove-Item")),
            "malicious fragment must never be a valid host");
    }

    [Test]
    public void EmptyInput_ProducesNothing()
    {
        var result = IpParser.Parse("");

        Assert.Empty(result.ValidHosts);
        Assert.Empty(result.Errors);
    }
}
