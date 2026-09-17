namespace PingTester.Core.Ips;

/// <summary>
/// A monitored host with its optional labels (school and device), as read from
/// the monitoring CSV. Immutable value object.
/// </summary>
public sealed record MonitoredHost(HostAddress Host, string Escola, string Dispositivo);
