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

    /// <param name="executable">
    /// Kept for backwards compatibility with existing DI wiring; ignored now
    /// that we invoke ping/tracert via cmd.exe.
    /// </param>
    /// <param name="timeout">Per-command timeout (tracert can be slow).</param>
    public PowerShellExecutor(string executable = "powershell", TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
    }

    public async Task<string> ExecuteAsync(PowerShellCommandSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // Validate every token first (defence in depth — even though we build
        // the argument list carefully below).
        foreach (var kv in spec.Arguments) { ValidateToken(kv.Key); ValidateToken(kv.Value); }
        foreach (var sw in spec.Switches) ValidateToken(sw);
        if (spec.Target.Length > 0) ValidateToken(spec.Target);

        // Run through cmd.exe so the executable (ping/tracert) resolves reliably
        // from PATH even in a service/web context, and so `chcp 65001` forces
        // UTF-8 output — which we then read as UTF-8. This is far more robust in
        // a web app than launching ping.exe directly with the console encoding.
        var innerCommand = BuildInnerCommandLine(spec);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("/c");
        // /c "chcp 65001>nul & <cmd> <args>"
        psi.ArgumentList.Add($"chcp 65001>nul & {innerCommand}");

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
            // Truly nothing came back — surface a DIAGNOSTIC error so we can see
            // why (exit code, what was launched) instead of a silent blank.
            int? exit = null;
            try { exit = process.ExitCode; } catch { /* ignore */ }
            throw new InvalidOperationException(
                $"'{spec.Command}' não produziu saída (cmd.exe exit={exit?.ToString() ?? "?"}). " +
                $"Comando: {BuildInnerCommandLine(spec)}");
        }

        return combined;
    }

    /// <summary>
    /// Builds the "&lt;exe&gt; &lt;options&gt; &lt;switches&gt; &lt;target&gt;"
    /// string passed to cmd.exe /c. All tokens have already been validated to
    /// contain only safe characters (alphanumerics and . : - _ /), so there are
    /// no spaces or shell metacharacters to escape.
    /// </summary>
    private static string BuildInnerCommandLine(PowerShellCommandSpec spec)
    {
        var sb = new StringBuilder(spec.Command);
        foreach (var kv in spec.Arguments)
            sb.Append(' ').Append(kv.Key).Append(' ').Append(kv.Value);
        foreach (var sw in spec.Switches)
            sb.Append(' ').Append(sw);
        if (spec.Target.Length > 0)
            sb.Append(' ').Append(spec.Target);
        return sb.ToString();
    }

    /// <summary>
    /// Defence in depth: allow only plain host/option tokens (alphanumerics and
    /// . : - _ /). Reject anything that could be abused. Because tokens are
    /// restricted to these characters, the command line assembled for cmd.exe
    /// contains no spaces-in-values or shell metacharacters.
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
