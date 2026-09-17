using System.Collections.Generic;

namespace PingTester.Core.Results;

/// <summary>A single hop in a traceroute. Immutable.</summary>
public sealed record TraceHop(int Number, string Address);

/// <summary>
/// Domain result of a traceroute test. Immutable value produced by the pure
/// parser.
/// </summary>
public sealed record TraceResult(
    string Target,
    bool DestinationReached,
    IReadOnlyList<TraceHop> Hops);
