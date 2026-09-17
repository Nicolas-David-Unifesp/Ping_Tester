using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PingTester.Core.Commands;

namespace PingTester.Shell;

/// <summary>
/// IMPERATIVE SHELL — impure. Runs the classic Windows network executables
/// (ping.exe / tracert.exe) described by the spec and captures their TEXT
/// output. These use ICMP directly — the same mechanism as the CMD prompt —
/// which works on the target environment where the Windows PowerShell 5.1
/// Test-Connection cmdlet fails via WMI.
///
/// SECURITY: the executable is a fixed name chosen by our own core; every
/// option, switch and the target IP are passed as DISCRETE process arguments
/// (ProcessStartInfo.ArgumentList), never concatenated into a shell string, so
/// a value cannot inject a command. As defence in depth we also reject any
/// argument/target value that isn't a plain IP-ish token.
///
/// ENCODING: ping/tracert write to the console using the OEM code page (e.g.
/// 850 in Brazilian Windows). We decode stdout with that code page so accented
/// text ("Estatísticas", "máximo", "concluído") is read correctly.
///
/// NOTE: runs real processes + network probes, so it only works on Windows. It
/// is intentionally NOT exercised by the offline unit tests (which use a fake).
/// </summary>
public sealed class PowerShellExecutor : IPowerShellExecutor
{
    private readonly TimeSpan _timeout;
    private readonly Encoding _outputEncoding;

    /// <param name="executable">
    /// Kept for backwards compatibility with existing DI wiring; ignored now
    /// that we invoke ping/tracert directly rather than through a shell.
    /// </param>
    /// <param name="timeout">Per-command timeout (tracert can be slow).</param>
    public PowerShellExecutor(string executable = "powershell", TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
        _outputEncoding = ResolveOemEncoding();
    }

    public async Task<string> ExecuteAsync(PowerShellCommandSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var psi = new ProcessStartInfo
        {
            FileName = spec.Command, // "ping" or "tracert" — resolved from PATH
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = _outputEncoding,
            StandardErrorEncoding = _outputEncoding,
        };

        // Named options: e.g. "-n" "4".
        foreach (var kv in spec.Arguments)
        {
            ValidateToken(kv.Key);
            ValidateToken(kv.Value);
            psi.ArgumentList.Add(kv.Key);
            psi.ArgumentList.Add(kv.Value);
        }

        // Valueless switches: e.g. "-d".
        foreach (var sw in spec.Switches)
        {
            ValidateToken(sw);
            psi.ArgumentList.Add(sw);
        }

        // Positional target (the IP) goes LAST: e.g. "ping -n 4 8.8.8.8".
        if (spec.Target.Length > 0)
        {
            ValidateToken(spec.Target);
            psi.ArgumentList.Add(spec.Target);
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível executar '{spec.Command}': {ex.Message}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new TimeoutException($"O comando '{spec.Command}' excedeu o tempo limite.");
        }

        var stdoutText = stdout.ToString();
        var stderrText = stderr.ToString();

        // ping/tracert exit non-zero on 100% loss / "no reply" — that is a
        // VALID result, not an error. Their message text may land on stdout or,
        // in some environments, on stderr. So we combine both and return
        // whatever text exists. Only when there is NO text at all (nothing on
        // either stream) do we surface a real execution failure.
        var combined = stdoutText;
        if (!string.IsNullOrWhiteSpace(stderrText))
        {
            combined = string.IsNullOrWhiteSpace(combined)
                ? stderrText
                : combined + stderrText;
        }

        if (string.IsNullOrWhiteSpace(combined))
        {
            // Truly nothing came back — treat as an execution failure so the
            // caller can report it.
            throw new InvalidOperationException(
                $"'{spec.Command}' não produziu saída.");
        }

        return combined;
    }

    /// <summary>
    /// Resolves the console OEM encoding used by ping/tracert. On Windows the
    /// process console encoding already reflects the OEM code page (e.g. 850 in
    /// Brazilian Windows), so <see cref="Console.OutputEncoding"/> is correct.
    /// Falls back to UTF-8 elsewhere.
    /// </summary>
    private static Encoding ResolveOemEncoding()
    {
        try
        {
            return Console.OutputEncoding;
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>
    /// Defence in depth: allow only plain host/option tokens (alphanumerics and
    /// . : - _ /). Reject anything that could be abused, even though arguments
    /// are already passed as discrete process args.
    /// </summary>
    private static void ValidateToken(string value)
    {
        foreach (var c in value)
        {
            bool ok = char.IsLetterOrDigit(c) || c == '.' || c == ':' || c == '-' || c == '_' || c == '/';
            if (!ok)
                throw new ArgumentException($"Valor de argumento inválido: '{value}'");
        }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
    }
}
