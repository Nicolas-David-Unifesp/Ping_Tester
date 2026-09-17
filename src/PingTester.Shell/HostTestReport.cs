using PingTester.Core.Results;

namespace PingTester.Shell;

/// <summary>
/// Combined ping + tracert outcome for a single host. Either result may carry
/// an <see cref="Error"/> string if the shell execution failed.
/// </summary>
public sealed record HostTestReport(
    string Target,
    PingResult? Ping,
    TraceResult? Trace,
    string? Error);
