using System.Collections.Generic;

namespace PingTester.Core.Ips;

/// <summary>
/// FUNCTIONAL CORE — pure. Turns arbitrary user text into validated hosts plus
/// errors. No I/O, no side effects, fully deterministic and unit-testable.
/// </summary>
public static class IpParser
{
    // Tokens can be separated by newlines, commas or semicolons. This lets us
    // accept pasted lists as well as one-per-line input.
    private static readonly char[] Separators = { '\n', '\r', ',', ';' };

    public static IpParseResult Parse(string? rawText)
    {
        var valid = new List<HostAddress>();
        var errors = new List<IpParseError>();
        var seen = new HashSet<string>();

        if (string.IsNullOrWhiteSpace(rawText))
            return new IpParseResult(valid, errors);

        var tokens = rawText.Split(Separators, System.StringSplitOptions.RemoveEmptyEntries
                                             | System.StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var host = HostAddress.TryCreate(token);
            if (host is null)
            {
                errors.Add(new IpParseError(token, "Não é um endereço IP válido."));
                continue;
            }

            // Deduplicate on the normalised value.
            if (seen.Add(host.Value))
                valid.Add(host);
        }

        return new IpParseResult(valid, errors);
    }
}
