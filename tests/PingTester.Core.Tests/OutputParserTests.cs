using System.Linq;
using PingTester.Core.Results;

namespace PingTester.Core.Tests;

/// <summary>
/// TDD (RED first): parse the JSON that the shell captures from the cmdlets
/// (via ConvertTo-Json) into domain results. Pure functions — no process, no
/// network. Parsing JSON rather than localized human text keeps this stable
/// across PowerShell locales/versions.
/// </summary>
public class PingOutputParserTests
{
    // Shape produced by: Test-Connection -TargetName 8.8.8.8 -Count 4 | ConvertTo-Json
    // (fields relevant to us: Status, Latency/Ping replies). Simplified sample.
    private const string SuccessJson = @"[
        { ""Status"": ""Success"", ""Latency"": 10, ""Address"": ""8.8.8.8"" },
        { ""Status"": ""Success"", ""Latency"": 12, ""Address"": ""8.8.8.8"" },
        { ""Status"": ""Success"", ""Latency"": 11, ""Address"": ""8.8.8.8"" },
        { ""Status"": ""Success"", ""Latency"": 9,  ""Address"": ""8.8.8.8"" }
    ]";

    private const string PartialLossJson = @"[
        { ""Status"": ""Success"",         ""Latency"": 10, ""Address"": ""8.8.8.8"" },
        { ""Status"": ""TimedOut"",        ""Latency"": 0,  ""Address"": ""8.8.8.8"" },
        { ""Status"": ""Success"",         ""Latency"": 20, ""Address"": ""8.8.8.8"" },
        { ""Status"": ""DestinationHostUnreachable"", ""Latency"": 0, ""Address"": ""8.8.8.8"" }
    ]";

    [Test]
    public void AllSuccess_ReportsSuccessAndZeroLoss()
    {
        var result = PingOutputParser.Parse("8.8.8.8", SuccessJson);

        Assert.True(result.IsReachable, "all replies succeeded");
        Assert.Equal(4, result.PacketsSent);
        Assert.Equal(4, result.PacketsReceived);
        Assert.Equal(0, result.PacketsLost);
    }

    [Test]
    public void AllSuccess_ComputesAverageLatency()
    {
        var result = PingOutputParser.Parse("8.8.8.8", SuccessJson);

        // (10 + 12 + 11 + 9) / 4 = 10.5
        Assert.Equal(10.5, result.AverageLatencyMs);
    }

    [Test]
    public void PartialLoss_CountsReceivedAndLost()
    {
        var result = PingOutputParser.Parse("8.8.8.8", PartialLossJson);

        Assert.Equal(4, result.PacketsSent);
        Assert.Equal(2, result.PacketsReceived);
        Assert.Equal(2, result.PacketsLost);
        Assert.True(result.IsReachable, "at least one reply succeeded");
    }

    [Test]
    public void PartialLoss_AveragesOnlySuccessfulReplies()
    {
        var result = PingOutputParser.Parse("8.8.8.8", PartialLossJson);

        // Only successful latencies (10, 20) count: average 15.
        Assert.Equal(15.0, result.AverageLatencyMs);
    }

    [Test]
    public void SingleObject_NotArray_IsHandled()
    {
        // ConvertTo-Json emits a single object (not an array) when Count = 1.
        var single = @"{ ""Status"": ""Success"", ""Latency"": 7, ""Address"": ""8.8.8.8"" }";

        var result = PingOutputParser.Parse("8.8.8.8", single);

        Assert.Equal(1, result.PacketsSent);
        Assert.Equal(1, result.PacketsReceived);
        Assert.Equal(7.0, result.AverageLatencyMs);
    }

    [Test]
    public void AllTimedOut_IsUnreachable()
    {
        var json = @"[
            { ""Status"": ""TimedOut"", ""Latency"": 0, ""Address"": ""10.0.0.1"" },
            { ""Status"": ""TimedOut"", ""Latency"": 0, ""Address"": ""10.0.0.1"" }
        ]";

        var result = PingOutputParser.Parse("10.0.0.1", json);

        Assert.False(result.IsReachable, "no replies succeeded");
        Assert.Equal(0, result.PacketsReceived);
        Assert.Null(result.AverageLatencyMs);
    }

    [Test]
    public void Target_IsPreserved()
    {
        var result = PingOutputParser.Parse("8.8.8.8", SuccessJson);
        Assert.Equal("8.8.8.8", result.Target);
    }
}

public class TracertOutputParserTests
{
    // Shape from: Test-NetConnection 1.1.1.1 -TraceRoute | ConvertTo-Json
    // TraceRoute is an array of hop addresses; PingSucceeded is a bool.
    private const string TraceJson = @"{
        ""ComputerName"": ""1.1.1.1"",
        ""RemoteAddress"": ""1.1.1.1"",
        ""PingSucceeded"": true,
        ""TraceRoute"": [ ""192.168.0.1"", ""10.0.0.1"", ""200.200.200.1"", ""1.1.1.1"" ]
    }";

    [Test]
    public void ParsesAllHopsInOrder()
    {
        var result = TracertOutputParser.Parse("1.1.1.1", TraceJson);

        Assert.Count(4, result.Hops);
        Assert.Equal("192.168.0.1", result.Hops[0].Address);
        Assert.Equal(1, result.Hops[0].Number);
        Assert.Equal("1.1.1.1", result.Hops[3].Address);
        Assert.Equal(4, result.Hops[3].Number);
    }

    [Test]
    public void PreservesTargetAndReachability()
    {
        var result = TracertOutputParser.Parse("1.1.1.1", TraceJson);

        Assert.Equal("1.1.1.1", result.Target);
        Assert.True(result.DestinationReached);
    }

    [Test]
    public void EmptyTraceRoute_ProducesNoHops()
    {
        var json = @"{ ""ComputerName"": ""1.1.1.1"", ""PingSucceeded"": false, ""TraceRoute"": [] }";

        var result = TracertOutputParser.Parse("1.1.1.1", json);

        Assert.Empty(result.Hops);
        Assert.False(result.DestinationReached);
    }

    [Test]
    public void SingleHop_AsScalar_IsHandled()
    {
        // ConvertTo-Json may emit a scalar instead of a 1-element array.
        var json = @"{ ""ComputerName"": ""1.1.1.1"", ""PingSucceeded"": true, ""TraceRoute"": ""1.1.1.1"" }";

        var result = TracertOutputParser.Parse("1.1.1.1", json);

        Assert.Count(1, result.Hops);
        Assert.Equal("1.1.1.1", result.Hops[0].Address);
    }
}
