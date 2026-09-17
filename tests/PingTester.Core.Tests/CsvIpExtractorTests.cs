using System.Linq;
using PingTester.Core.Ips;

namespace PingTester.Core.Tests;

/// <summary>
/// TDD (RED first): extract IPs from raw CSV text. Pure function, no file I/O —
/// the shell reads the file and hands the raw string here.
/// </summary>
public class CsvIpExtractorTests
{
    [Test]
    public void SingleColumn_NoHeader_ExtractsIps()
    {
        var csv = "8.8.8.8\n1.1.1.1\n9.9.9.9";

        var result = CsvIpExtractor.Extract(csv);

        Assert.Count(3, result.ValidHosts);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void HeaderRowWithNoIp_IsIgnored()
    {
        var csv = "ip,descricao\n8.8.8.8,Google\n1.1.1.1,Cloudflare";

        var result = CsvIpExtractor.Extract(csv);

        Assert.Count(2, result.ValidHosts);
        // "ip" and "descricao" and the labels are not IPs, but should not be
        // reported as errors — non-IP cells in a CSV are simply skipped.
        Assert.Empty(result.Errors);
    }

    [Test]
    public void IpCanBeInAnyColumn()
    {
        var csv = "servidor,ip\nDNS Google,8.8.8.8\nDNS Cloudflare,1.1.1.1";

        var result = CsvIpExtractor.Extract(csv);

        Assert.Count(2, result.ValidHosts);
        Assert.Contains("8.8.8.8", result.ValidHosts.Select(h => h.Value).ToList());
    }

    [Test]
    public void SemicolonDelimitedCsv_IsSupported()
    {
        // Brazilian Excel often exports with ';' as the delimiter.
        var csv = "ip;nome\n8.8.8.8;Google\n1.1.1.1;Cloudflare";

        var result = CsvIpExtractor.Extract(csv);

        Assert.Count(2, result.ValidHosts);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void QuotedCells_AreUnwrapped()
    {
        var csv = "\"8.8.8.8\",\"Google DNS\"\n\"1.1.1.1\",\"Cloudflare\"";

        var result = CsvIpExtractor.Extract(csv);

        Assert.Count(2, result.ValidHosts);
    }

    [Test]
    public void BlankLines_AreIgnored()
    {
        var csv = "8.8.8.8\n\n\n1.1.1.1\n";

        var result = CsvIpExtractor.Extract(csv);

        Assert.Count(2, result.ValidHosts);
    }

    [Test]
    public void Duplicates_AcrossRows_AreDeduplicated()
    {
        var csv = "ip\n8.8.8.8\n8.8.8.8";

        var result = CsvIpExtractor.Extract(csv);

        Assert.Count(1, result.ValidHosts);
    }

    [Test]
    public void EmptyCsv_ProducesNothing()
    {
        var result = CsvIpExtractor.Extract("");

        Assert.Empty(result.ValidHosts);
        Assert.Empty(result.Errors);
    }
}
