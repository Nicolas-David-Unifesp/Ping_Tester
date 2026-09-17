using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PingTester.Core.Commands;

namespace PingTester.Shell;

/// <summary>
/// IMPERATIVE SHELL — impure. Real implementation that launches PowerShell and
/// runs the cmdlet described by the spec, piping the result through
/// ConvertTo-Json so the pure core can parse a stable format.
///
/// SECURITY: the command name is a fixed cmdlet chosen by our own core, and the
/// argument VALUES are passed via a parameters hashtable using PowerShell
/// splatting (@params). Values are therefore bound as data, never interpreted
/// as script — so a value can't inject commands. As defence in depth we also
/// reject any argument value that isn't a plain IP-ish token.
///
/// NOTE: This runs PowerShell and real network probes, so it only works on a
/// machine with PowerShell installed (your Windows server). It is intentionally
/// NOT exercised by the offline unit tests.
/// </summary>
public sealed class PowerShellExecutor : IPowerShellExecutor
{
    private readonly string _executable;
    private readonly TimeSpan _timeout;

    /// <param name="executable">
    /// "powershell" (Windows PowerShell) or "pwsh" (PowerShell 7+).
    /// </param>
    public PowerShellExecutor(string executable = "powershell", TimeSpan? timeout = null)
    {
        _executable = executable;
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
    }

    public async Task<string> ExecuteAsync(PowerShellCommandSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var script = BuildScript(spec);

        var psi = new ProcessStartInfo
        {
            FileName = _executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // -NoProfile for speed/determinism, -NonInteractive so it never blocks,
        // -Command runs our generated script.
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
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
            throw new TimeoutException($"O comando PowerShell '{spec.Command}' excedeu o tempo limite.");
        }

        var output = stdout.ToString().Trim();
        if (string.IsNullOrEmpty(output) && stderr.Length > 0)
            throw new InvalidOperationException($"PowerShell falhou: {stderr}");

        return output;
    }

    /// <summary>
    /// Builds a script that splats a parameters hashtable into the cmdlet and
    /// converts the result to JSON. Argument values are placed in the hashtable
    /// as string literals (single-quoted, with any single quotes doubled), so
    /// they are data, not code.
    /// </summary>
    private static string BuildScript(PowerShellCommandSpec spec)
    {
        var sb = new StringBuilder();
        sb.Append("$ErrorActionPreference='SilentlyContinue'; ");
        sb.Append("$p=@{");

        var first = true;
        foreach (var kv in spec.Arguments)
        {
            var name = StripLeadingDash(kv.Key);
            ValidateArgumentValue(kv.Value);
            if (!first) sb.Append("; ");
            sb.Append(name).Append("='").Append(EscapeSingleQuotes(kv.Value)).Append('\'');
            first = false;
        }
        sb.Append("}; ");

        // Switches are appended directly (they carry no user data).
        var switches = new StringBuilder();
        foreach (var sw in spec.Switches)
            switches.Append(' ').Append(sw);

        // -WarningAction/-Depth keep JSON clean and complete.
        sb.Append(spec.Command)
          .Append(" @p")
          .Append(switches)
          .Append(" -WarningAction SilentlyContinue | ConvertTo-Json -Depth 4 -Compress");

        return sb.ToString();
    }

    private static string StripLeadingDash(string name) =>
        name.StartsWith('-') ? name[1..] : name;

    private static string EscapeSingleQuotes(string value) =>
        value.Replace("'", "''");

    /// <summary>
    /// Defence in depth: even though values are bound as data, reject anything
    /// that isn't a plausible host token (IPv4/IPv6/hostname chars) or number.
    /// </summary>
    private static void ValidateArgumentValue(string value)
    {
        foreach (var c in value)
        {
            bool ok = char.IsLetterOrDigit(c) || c == '.' || c == ':' || c == '-' || c == '_';
            if (!ok)
                throw new ArgumentException($"Valor de argumento inválido para PowerShell: '{value}'");
        }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
    }
}
