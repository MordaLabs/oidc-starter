namespace Backend.Auth;

// Deliberately conservative ASCII address policy. It does not interpret provider aliases.
internal static class InvitationEmail
{
    private const string LocalPunctuation = "!#$%&'*+-/=?^_`{|}~.";

    public static bool TryCanonicalize(string? value, out string canonical)
    {
        canonical = string.Empty;
        if (value is null)
        {
            return false;
        }

        var address = value.Trim();
        if (address.Length is < 3 or > 254 || address.Any(static c => c is < '!' or > '~'))
        {
            return false;
        }

        var separator = address.IndexOf('@');
        if (separator is < 1 or > 64 || separator != address.LastIndexOf('@'))
        {
            return false;
        }

        var local = address[..separator];
        var domain = address[(separator + 1)..];
        if (domain.Length is < 3 or > 253
            || local[0] == '.'
            || local[^1] == '.'
            || local.Contains("..", StringComparison.Ordinal)
            || !local.All(static c => IsAsciiLetterOrDigit(c) || LocalPunctuation.Contains(c))
            || !domain.Contains('.'))
        {
            return false;
        }

        foreach (var label in domain.Split('.'))
        {
            if (label.Length is < 1 or > 63
                || label[0] == '-'
                || label[^1] == '-'
                || !label.All(static c => IsAsciiLetterOrDigit(c) || c == '-'))
            {
                return false;
            }
        }

        canonical = address.ToLowerInvariant();
        return true;
    }

    private static bool IsAsciiLetterOrDigit(char c)
        => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
