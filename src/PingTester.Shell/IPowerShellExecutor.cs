using System.Threading;
using System.Threading.Tasks;
using PingTester.Core.Commands;

namespace PingTester.Shell;

/// <summary>
/// IMPERATIVE SHELL boundary. Abstracts the single side effect of the whole
/// application: running a PowerShell cmdlet and capturing its output.
///
/// The functional core produces the <see cref="PowerShellCommandSpec"/>; the
/// implementation of this interface is the ONLY place that touches a process.
/// Keeping it behind an interface lets the orchestrator be tested with a fake.
/// </summary>
public interface IPowerShellExecutor
{
    /// <summary>
    /// Executes the given command spec and returns its output as JSON text
    /// (the implementation pipes the cmdlet result through ConvertTo-Json).
    /// </summary>
    Task<string> ExecuteAsync(PowerShellCommandSpec spec, CancellationToken cancellationToken = default);
}
