using System.Collections.Generic;

namespace PingTester.Web;

/// <summary>Request body for testing typed IPs.</summary>
public sealed record TestRequest(string Ips);

/// <summary>Flattened, UI-friendly view of a single host's ping+trace result.</summary>
public sealed record HostResultDto(
    string Target,
    bool PingReachable,
    int PacketsSent,
    int PacketsReceived,
    int PacketsLost,
    double LossPercentage,
    double? AverageLatencyMs,
    bool TraceDestinationReached,
    IReadOnlyList<HopDto> Hops,
    string? Error);

public sealed record HopDto(int Number, string Address);

/// <summary>Full response: results plus any inputs that failed validation.</summary>
public sealed record TestResponse(
    IReadOnlyList<HostResultDto> Results,
    IReadOnlyList<InvalidInputDto> Invalid);

public sealed record InvalidInputDto(string Value, string Reason);
