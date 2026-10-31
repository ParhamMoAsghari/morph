using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Morph.EntityEngine.Storage;

internal static partial class PgIdent
{
    private static readonly Regex SafeIdent = SafeIdentRegex();

    public static string Sanitize(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var sanitized = SafeIdent.Replace(lowered, "_");
        if (string.IsNullOrEmpty(sanitized) || char.IsDigit(sanitized[0]))
        {
            sanitized = "t_" + sanitized;
        }

        return sanitized.Length > 48 ? sanitized[..48] : sanitized;
    }

    public static string QuoteIdent(string ident) => "\"" + ident.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public static string Qualify(string schema, string table) =>
        $"{QuoteIdent(schema)}.{QuoteIdent(table)}";

    public static string ShortId(Guid id) => id.ToString("N")[..12];

    public static string Fingerprint(params string[] parts)
    {
        var payload = string.Join('|', parts);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [GeneratedRegex(@"[^a-z0-9_]+", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentRegex();
}
