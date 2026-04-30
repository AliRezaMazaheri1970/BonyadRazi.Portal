using Microsoft.AspNetCore.Http;
using System.Net;
using Xunit;
using BonyadRazi.Portal.Api.Audit;

namespace BonyadRazi.Portal.SecurityTests;

public sealed class RequestAuditMetadataFactoryTests
{
    [Fact]
    public void ResolveClientIp_IgnoresSpoofedXForwardedForHeader()
    {
        var context = new DefaultHttpContext();

        context.Connection.RemoteIpAddress = IPAddress.Parse("10.10.10.10");
        context.Request.Headers["X-Forwarded-For"] = "192.168.93.5";

        var ip = RequestAuditMetadataFactory.ResolveClientIp(context);

        Assert.Equal("10.10.10.10", ip);
    }

    [Fact]
    public void Create_UsesConnectionRemoteIp_NotSpoofedForwardedHeader()
    {
        var context = new DefaultHttpContext();

        context.Connection.RemoteIpAddress = IPAddress.Parse("10.10.10.10");
        context.Request.Headers["X-Forwarded-For"] = "192.168.93.5";
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        var metadata = RequestAuditMetadataFactory.Create(context);

        Assert.True(metadata.TryGetValue("ip", out var ip));
        Assert.Equal("10.10.10.10", ip);
    }
}