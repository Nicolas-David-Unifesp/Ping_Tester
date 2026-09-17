using PingTester.Core.Results;

namespace PingTester.Shell;

/// <summary>
/// Combined ping + tracert outcome for a single host. Either result may carry
/// an <see cref="Error"/> string if the shell execution failed.
///
/// <see cref="RawPingOutput"/> and <see cref="RawTraceOutput"/> hold the
/// verbatim console text captured from ping.exe / tracert.exe. They are only
/// populated on the on-demand DETAIL path (monitoring "Ver detalhes"); the fast
/// ping sweep and the manual test leave them null to avoid shipping large text.
/// </summary>
public sealed record HostTestReport(
    string Target,
    PingResult? Ping,
    TraceResult? Trace,
    string? Error,
    string? RawPingOutput = null,
    string? RawTraceOutput = null);
