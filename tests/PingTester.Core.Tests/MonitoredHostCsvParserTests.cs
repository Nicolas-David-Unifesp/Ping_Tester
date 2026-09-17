using System.Linq;
using PingTester.Core.Ips;

namespace PingTester.Core.Tests;

/// <summary>
/// TDD (RED first): structured CSV reader for the monitoring list. Unlike the
/// generic CsvIpExtractor (which just harvests any IP), this reader understands
/// named columns from a header row: ip, escola, dispositivo. Column order is
/// irrelevant; header names are matched case/accent-insensitively. Pure — no I/O.
/// </summary>
public class MonitoredHostCsvParserTests
{
    [Test]
    public void ParsesIpEscolaDispositivo_FromHeader()
    {
        var csv =
@"ip;escola;dispositivo
10.113.96.148;EMEF Machado;Roteador principal";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Count(1, hosts);
        Assert.Equal("10.113.96.148", hosts[0].Host.Value);
        Assert.Equal("EMEF Machado", hosts[0].Escola);
        Assert.Equal("Roteador principal", hosts[0].Dispositivo);
    }

    [Test]
    public void ColumnOrder_DoesNotMatter()
    {
        var csv =
@"dispositivo;ip;escola
Switch sala 3;192.168.0.10;EEEP João";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Equal("192.168.0.10", hosts[0].Host.Value);
        Assert.Equal("EEEP João", hosts[0].Escola);
        Assert.Equal("Switch sala 3", hosts[0].Dispositivo);
    }

    [Test]
    public void HeaderNames_AreCaseAndAccentInsensitive()
    {
        // "IP", "Escola" and "Dispositivo" with caps/accents must still map.
        var csv =
@"IP;Escola;Dispositivo
10.0.0.1;Escola A;AP pátio";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Equal("Escola A", hosts[0].Escola);
        Assert.Equal("AP pátio", hosts[0].Dispositivo);
    }

    [Test]
    public void DispositivoSynonyms_AreAccepted()
    {
        // "equipamento" / "device" are accepted as the device column.
        var csv =
@"ip;escola;equipamento
10.0.0.2;Escola B;Modem";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Equal("Modem", hosts[0].Dispositivo);
    }

    [Test]
    public void MissingEscolaOrDispositivo_AreBlank()
    {
        var csv =
@"ip;escola;dispositivo
10.0.0.3;;
10.0.0.4;Escola C;";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Count(2, hosts);
        Assert.Equal("", hosts[0].Escola);
        Assert.Equal("", hosts[0].Dispositivo);
        Assert.Equal("Escola C", hosts[1].Escola);
        Assert.Equal("", hosts[1].Dispositivo);
    }

    [Test]
    public void RowsWithoutValidIp_AreSkipped()
    {
        var csv =
@"ip;escola;dispositivo
nao-eh-ip;Escola X;Coisa
10.0.0.5;Escola Y;Roteador";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Count(1, hosts);
        Assert.Equal("10.0.0.5", hosts[0].Host.Value);
    }

    [Test]
    public void CommaDelimiter_IsAlsoSupported()
    {
        var csv =
@"ip,escola,dispositivo
10.0.0.6,Escola Z,Switch";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Equal("Escola Z", hosts[0].Escola);
        Assert.Equal("Switch", hosts[0].Dispositivo);
    }

    [Test]
    public void QuotedCellsWithDelimiterInside_AreHandled()
    {
        // A quoted value may contain the delimiter (e.g. a comma in the name).
        var csv =
"ip;escola;dispositivo\n" +
"10.0.0.7;\"Escola A, B e C\";\"Roteador; principal\"";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Equal("Escola A, B e C", hosts[0].Escola);
        Assert.Equal("Roteador; principal", hosts[0].Dispositivo);
    }

    [Test]
    public void HeaderlessFile_WithOnlyIps_StillWorks_BlankLabels()
    {
        // Backwards compatibility: old files with just IPs (no header).
        var csv =
@"10.0.0.8
10.0.0.9";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Count(2, hosts);
        Assert.Equal("10.0.0.8", hosts[0].Host.Value);
        Assert.Equal("", hosts[0].Escola);
        Assert.Equal("", hosts[0].Dispositivo);
    }

    [Test]
    public void Empty_ProducesNothing()
    {
        Assert.Empty(MonitoredHostCsvParser.Parse(""));
    }

    [Test]
    public void DuplicateIps_AreDeduplicated_KeepingFirst()
    {
        var csv =
@"ip;escola;dispositivo
10.0.0.10;Primeira;A
10.0.0.10;Segunda;B";

        var hosts = MonitoredHostCsvParser.Parse(csv);

        Assert.Count(1, hosts);
        Assert.Equal("Primeira", hosts[0].Escola);
    }
}
