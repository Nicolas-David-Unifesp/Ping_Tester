using System.Collections.Generic;

namespace PingTester.Core.Ips;

/// <summary>A raw token that failed validation, kept for user feedback.</summary>
public sealed record IpParseError(string RawValue, string Reason);

/// <summary>
/// Outcome of parsing raw user text into IPs. Immutable; produced by the pure
/// <see cref="IpParser"/>.
/// </summary>
public sealed record IpParseResult(
    IReadOnlyList<HostAddress> ValidHosts,
    IReadOnlyList<IpParseError> Errors);
