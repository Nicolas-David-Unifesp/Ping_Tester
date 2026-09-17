using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PingTester.Core.Commands;

/// <summary>
/// A description (as pure data) of a PowerShell cmdlet invocation. Produced by
/// the functional core; executed by the imperative shell. The shell is expected
/// to bind <see cref="Arguments"/> as named parameters (NOT string
/// interpolation), which keeps execution safe from injection.
/// Immutable.
/// </summary>
public sealed class PowerShellCommandSpec
{
    /// <summary>The cmdlet name, e.g. "Test-Connection".</summary>
    public string Command { get; }

    /// <summary>Named parameters with values, e.g. "-TargetName" => "8.8.8.8".</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; }

    /// <summary>Valueless switch parameters, e.g. "-TraceRoute".</summary>
    public IReadOnlyList<string> Switches { get; }

    public PowerShellCommandSpec(
        string command,
        IReadOnlyDictionary<string, string> arguments,
        IReadOnlyList<string> switches)
    {
        Command = command;
        Arguments = arguments;
        Switches = switches;
    }

    /// <summary>
    /// Human-readable rendering for logs and UI. This is illustrative only —
    /// the shell binds parameters directly and does not run this string.
    /// </summary>
    public string ToDisplayString()
    {
        var sb = new StringBuilder(Command);
        foreach (var kv in Arguments)
            sb.Append(' ').Append(kv.Key).Append(' ').Append(kv.Value);
        foreach (var sw in Switches)
            sb.Append(' ').Append(sw);
        return sb.ToString();
    }
}
