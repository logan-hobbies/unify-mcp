using System.Security.Cryptography;
using System.Text;

namespace UnifyMcp.Auth;

public static class BearerTokenAuth
{
    private const string Scheme = "Bearer ";

    /// <summary>
    /// Compares an Authorization header against the expected token in constant time.
    /// </summary>
    public static bool Matches(string? authorizationHeader, string expectedToken)
    {
        if (string.IsNullOrEmpty(authorizationHeader)
            || !authorizationHeader.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presented = authorizationHeader.AsSpan(Scheme.Length).Trim();
        var presentedBytes = Encoding.UTF8.GetBytes(presented.ToString());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);

        return presentedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(presentedBytes, expectedBytes);
    }
}
