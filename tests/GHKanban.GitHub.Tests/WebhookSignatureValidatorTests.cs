using GHKanban.GitHub;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace GHKanban.GitHub.Tests;

public class WebhookSignatureValidatorTests
{
    [Fact]
    public void ValidatesCorrectSignature()
    {
        const string secret = "super-secret";
        const string body = "{\"action\":\"opened\"}";
        var sig = ComputeSignature(secret, body);

        Assert.True(WebhookSignatureValidator.Validate(secret, body, sig));
    }

    [Fact]
    public void RejectsTamperedBody()
    {
        const string secret = "super-secret";
        var sig = ComputeSignature(secret, "{\"action\":\"opened\"}");
        Assert.False(WebhookSignatureValidator.Validate(secret, "{\"action\":\"closed\"}", sig));
    }

    [Fact]
    public void RejectsMalformedHeader()
    {
        Assert.False(WebhookSignatureValidator.Validate("s", "b", "notvalid"));
        Assert.False(WebhookSignatureValidator.Validate("s", "b", ""));
    }

    private static string ComputeSignature(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
