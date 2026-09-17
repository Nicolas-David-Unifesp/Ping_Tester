using System.Text;

namespace PingTester.Core.Commands;

/// <summary>
/// A description (as pure data) of a command invocation. Produced by the
/// functional core; executed by the imperative shell.
///
/// For the classic executables we use (ping / tracert), <see cref="Arguments"/>
/// are named options (e.g. "-n" => "4"), <see cref="Switches"/> are valueless
/// flags (e.g. "-d"), and <see cref="Target"/> is the positional host argument
/// placed at the END of the command line (e.g. `ping -n 4 8.8.8.8`).
///
/// The shell passes each piece as a discrete process argument (never string
/// interpolation), which keeps execution safe from injection.
/// Immutable.
/// </summary>
public sealed class PowerShellCommandSpec
{
    /// <summary>The executable name, e.g. "ping" or "tracert".</summary>
    public string Command { get; }

    /// <summary>Named options with values, e.g. "-n" => "4".</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; }

    /// <summary>Valueless switch flags, e.g. "-d".</summary>
    public IReadOnlyList<string> Switches { get; }

    /// <summary>
    /// The positional target (host/IP), rendered last. Empty when not used.
    /// </summary>
    public string Target { get; }

    public PowerShellCommandSpec(
        string command,
        IReadOnlyDictionary<string, string> arguments,
        IReadOnlyList<string> switches,
        string target = "")
    {
        Command = command;
        Arguments = arguments;
        Switches = switches;
        Target = target;
    }

    /// <summary>
    /// Human-readable rendering for logs and UI. This is illustrative only —
    /// the shell passes discrete process arguments and does not run this string.
    /// </summary>
    public string ToDisplayString()
    {
        var sb = new StringBuilder(Command);
        foreach (var kv in Arguments)
            sb.Append(' ').Append(kv.Key).Append(' ').Append(kv.Value);
        foreach (var sw in Switches)
            sb.Append(' ').Append(sw);
        if (Target.Length > 0)
            sb.Append(' ').Append(Target);
        return sb.ToString();
    }
}
