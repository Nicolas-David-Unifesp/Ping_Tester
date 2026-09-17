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

        // Launch the executable DIRECTLY. Using the ".exe" name makes PATH
        // resolution reliable in a service/web context, and passing arguments
        // via ArgumentList keeps them discrete (no shell, no injection). We do
        // NOT force a StandardOutputEncoding — letting .NET use the default
        // avoids the empty-capture problems seen with cmd.exe/chcp and with
        // Console.OutputEncoding in a non-console app. Output is read fully with
        // ReadToEndAsync below.
        var psi = new ProcessStartInfo
        {
            FileName = ResolveExecutable(spec.Command),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var kv in spec.Arguments)
        {
            psi.ArgumentList.Add(kv.Key);
            psi.ArgumentList.Add(kv.Value);
        }
        foreach (var sw in spec.Switches)
            psi.ArgumentList.Add(sw);
        if (spec.Target.Length > 0)
            psi.ArgumentList.Add(spec.Target);

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível executar '{spec.Command}': {ex.Message}", ex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        // Read BOTH streams to completion, THEN wait for exit. Reading with
        // ReadToEndAsync (instead of BeginOutputReadLine + WaitForExit) avoids a
        // race where WaitForExit returns before the output events have been
        // delivered — which produced an empty capture.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        string stdoutText = "";
        string stderrText = "";
        try
        {
            stdoutText = await stdoutTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            stderrText = await stderrTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Timed out. Kill the process, but SALVAGE whatever partial output
            // was already produced (a long tracert on an offline host still
            // emits useful hop lines before we give up).
            TryKill(process);

            var partial = await TryReadCompleted(stdoutTask).ConfigureAwait(false);
            var partialErr = await TryReadCompleted(stderrTask).ConfigureAwait(false);
            var salvaged = string.IsNullOrWhiteSpace(partial) ? partialErr : partial;

            if (!string.IsNullOrWhiteSpace(salvaged))
                return salvaged + $"\n[Interrompido: '{spec.Command}' excedeu o tempo limite.]";

            throw new TimeoutException($"O comando '{spec.Command}' excedeu o tempo limite.");
        }

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
                $"'{spec.Command}' não produziu saída (exit={exit?.ToString() ?? "?"}). " +
                $"Executável: {ResolveExecutable(spec.Command)}, alvo: {spec.Target}");
        }

        return combined;
    }

    /// <summary>
    /// Resolves the executable name. On Windows, appending ".exe" makes PATH
    /// resolution reliable when launched from a service/web host. Elsewhere the
    /// bare name is used.
    /// </summary>
    private static string ResolveExecutable(string command)
    {
        if (System.OperatingSystem.IsWindows() &&
            !command.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
        {
            return command + ".exe";
        }
        return command;
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

    /// <summary>
    /// After the process is killed its output stream closes, so the pending
    /// ReadToEndAsync completes with whatever was buffered. Give it a brief
    /// moment and return that partial text (empty on any failure).
    /// </summary>
    private static async Task<string> TryReadCompleted(Task<string> readTask)
    {
        try
        {
            var done = await Task.WhenAny(readTask, Task.Delay(1500)).ConfigureAwait(false);
            if (done == readTask)
                return readTask.Result ?? "";
        }
        catch { /* ignore */ }
        return "";
    }
}
