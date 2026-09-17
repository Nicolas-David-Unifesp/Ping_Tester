namespace PingTester.Core.Ips;

/// <summary>
/// An IP address that has already been validated by the functional core.
/// Being an instance of this type is the guarantee that the value is safe to
/// hand to the imperative shell (e.g. PowerShell cmdlets).
/// Immutable value object.
/// </summary>
public sealed record HostAddress
{
    public string Value { get; }

    private HostAddress(string value) => Value = value;

    /// <summary>
    /// The only way to construct a HostAddress: through validation.
    /// Returns null when the input is not a valid IPv4/IPv6 address.
    /// </summary>
    public static HostAddress? TryCreate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();

        // System.Net.IPAddress.TryParse is strict about the address grammar,
        // which is exactly the whitelist behaviour we want for security.
        if (!System.Net.IPAddress.TryParse(trimmed, out var parsed))
            return null;

        // Normalise (e.g. compress IPv6) so equality/dedup is consistent.
        return new HostAddress(parsed.ToString());
    }

    public override string ToString() => Value;
}
