namespace PingTester.Core.Results;

/// <summary>
/// Domain result of a ping test. Immutable value produced by the pure parser.
/// </summary>
public sealed record PingResult(
    string Target,
    int PacketsSent,
    int PacketsReceived,
    double? AverageLatencyMs)
{
    public int PacketsLost => PacketsSent - PacketsReceived;

    /// <summary>Reachable if at least one echo reply succeeded.</summary>
    public bool IsReachable => PacketsReceived > 0;

    public double LossPercentage =>
        PacketsSent == 0 ? 0 : (double)PacketsLost / PacketsSent * 100.0;
}
