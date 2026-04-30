using BonyadRazi.Portal.Api.Audit;
using Xunit;

namespace BonyadRazi.Portal.SecurityTests;

public sealed class AuditRedactionTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("pass")]
    [InlineData("pwd")]
    [InlineData("currentPassword")]
    [InlineData("newPassword")]
    [InlineData("token")]
    [InlineData("accessToken")]
    [InlineData("refreshToken")]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("jwt")]
    [InlineData("authorization")]
    [InlineData("bearer")]
    [InlineData("cookie")]
    [InlineData("set-cookie")]
    [InlineData("secret")]
    [InlineData("client_secret")]
    [InlineData("api_key")]
    [InlineData("apikey")]
    [InlineData("key")]
    [InlineData("connectionString")]
    [InlineData("connectionstring")]
    [InlineData("connection_string")]
    public void IsSensitiveKey_KnownSensitiveKeys_ShouldReturnTrue(string key)
    {
        Assert.True(AuditRedaction.IsSensitiveKey(key));
    }

    [Theory]
    [InlineData("page")]
    [InlineData("pageSize")]
    [InlineData("companyCode")]
    [InlineData("statusCode")]
    [InlineData("fromUtc")]
    [InlineData("toUtc")]
    public void IsSensitiveKey_NormalFilterKeys_ShouldReturnFalse(string key)
    {
        Assert.False(AuditRedaction.IsSensitiveKey(key));
    }

    [Theory]
    [InlineData("?pwd=123")]
    [InlineData("?jwt=abc")]
    [InlineData("?apikey=abc")]
    [InlineData("?connection_string=Server=.;Password=123")]
    [InlineData("?client_secret=abc")]
    public void RedactTextIfContainsSensitiveKey_SensitiveQuery_ShouldReturnRedacted(string queryString)
    {
        var result = AuditRedaction.RedactTextIfContainsSensitiveKey(queryString, 1024);

        Assert.Equal(AuditRedaction.RedactedValue, result);
    }

    [Fact]
    public void RedactTextIfContainsSensitiveKey_NormalQuery_ShouldPreserveValue()
    {
        var result = AuditRedaction.RedactTextIfContainsSensitiveKey("?page=1&pageSize=50", 1024);

        Assert.Equal("?page=1&pageSize=50", result);
    }
}
