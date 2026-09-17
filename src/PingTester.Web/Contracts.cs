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


// --- Monitoring tab -------------------------------------------------------

/// <summary>Status of one monitored host (ping-only, fast path).</summary>
public sealed record MonitorItemDto(
    string Target,
    string Escola,
    string Dispositivo,
    bool Online,
    double? AverageLatencyMs,
    double LossPercentage,
    string? Error);

/// <summary>Snapshot returned by the monitoring endpoint.</summary>
public sealed record MonitorResponse(
    IReadOnlyList<MonitorItemDto> Items,
    int Total,
    int Online,
    System.DateTimeOffset CheckedAt,
    bool FromCache,
    bool SourceExists,
    string SourcePath);


/// <summary>
/// Detail view for the monitoring tab: the flat ping+trace result plus the
/// verbatim console output of ping.exe / tracert.exe for display.
/// </summary>
public sealed record MonitorDetailDto(
    HostResultDto Result,
    string Escola,
    string Dispositivo,
    string? RawPingOutput,
    string? RawTraceOutput);
