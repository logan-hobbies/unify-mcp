using UnifyMcp.Auth;

namespace UnifyMcp.Tests;

public class BearerTokenAuthTests
{
    [Theory]
    [InlineData("Bearer secret-123", true)]
    [InlineData("bearer secret-123", true)]
    [InlineData("Bearer  secret-123 ", true)]
    [InlineData("Bearer secret-12", false)]
    [InlineData("Bearer secret-1234", false)]
    [InlineData("Basic secret-123", false)]
    [InlineData("secret-123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Matches_only_exact_bearer_token(string? header, bool expected)
    {
        Assert.Equal(expected, BearerTokenAuth.Matches(header, "secret-123"));
    }
}
