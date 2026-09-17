using System.Collections.Generic;
using System.Linq;
using PingTester.Core.Ips;
using PingTester.Shell;

namespace PingTester.Web;

/// <summary>Maps domain/shell results to the flat DTOs the frontend consumes.</summary>
public static class ResultMapper
{
    public static HostResultDto ToDto(HostTestReport report)
    {
        var ping = report.Ping;
        var trace = report.Trace;

        return new HostResultDto(
            Target: report.Target,
            PingReachable: ping?.IsReachable ?? false,
            PacketsSent: ping?.PacketsSent ?? 0,
            PacketsReceived: ping?.PacketsReceived ?? 0,
            PacketsLost: ping?.PacketsLost ?? 0,
            LossPercentage: ping?.LossPercentage ?? 0,
            AverageLatencyMs: ping?.AverageLatencyMs,
            TraceDestinationReached: trace?.DestinationReached ?? false,
            Hops: trace?.Hops.Select(h => new HopDto(h.Number, h.Address)).ToList()
                  ?? new List<HopDto>(),
            Error: report.Error);
    }

    /// <summary>
    /// Detail mapping for the monitoring tab: includes the verbatim console
    /// output alongside the parsed result.
    /// </summary>
    public static MonitorDetailDto ToDetailDto(HostTestReport report) =>
        new(
            Result: ToDto(report),
            RawPingOutput: report.RawPingOutput,
            RawTraceOutput: report.RawTraceOutput);

    public static IReadOnlyList<InvalidInputDto> ToInvalid(IReadOnlyList<IpParseError> errors) =>
        errors.Select(e => new InvalidInputDto(e.RawValue, e.Reason)).ToList();
}
