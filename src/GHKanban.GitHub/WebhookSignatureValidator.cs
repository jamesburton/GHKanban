using System.Security.Cryptography;
using System.Text;

namespace GHKanban.GitHub;

/// <summary>Validates GitHub webhook HMAC-SHA256 signatures.</summary>
public static class WebhookSignatureValidator
{
    /// <summary>
    /// Returns <see langword="true"/> when the <paramref name="signatureHeader"/> matches the
    /// HMAC-SHA256 of <paramref name="body"/> keyed with <paramref name="secret"/>.
    /// The comparison is constant-time to prevent timing attacks.
    /// </summary>
    /// <param name="secret">The shared webhook secret configured in GitHub.</param>
    /// <param name="body">The raw request body string.</param>
    /// <param name="signatureHeader">The value of the <c>X-Hub-Signature-256</c> header.</param>
    public static bool Validate(string secret, string body, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.Ordinal))
            return false;

        var supplied = signatureHeader["sha256=".Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();

        // Constant-time comparison to prevent timing-based signature oracles.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(supplied));
    }
}
