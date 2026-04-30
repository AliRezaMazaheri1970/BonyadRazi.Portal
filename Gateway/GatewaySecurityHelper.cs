using System.Net;
using System.Net.Sockets;

namespace Gateway;

public static class GatewaySecurityHelper
{
    public static IPAddress? GetClientIp(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress;

        if (ip is null)
            return null;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        return ip;
    }

    public static bool IsPathAllowedByPrefixes(PathString path, IEnumerable<string>? prefixes)
    {
        if (prefixes is null)
            return true;

        var list = prefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToArray();

        if (list.Length == 0)
            return true;

        return list.Any(prefix =>
            path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsIpAllowed(HttpContext ctx, IConfiguration config, IHostEnvironment env)
    {
        var bypassInTesting = config.GetValue("Security:BypassIpAllowlistInTesting", true);
        if (env.IsEnvironment("Testing") && bypassInTesting)
            return true;

        var ip = GetClientIp(ctx);
        if (ip is null)
            return false;

        var allowLoopback = config.GetValue("Security:AllowLoopbackInDevelopment", true);

        if (allowLoopback &&
            (env.IsDevelopment() || env.IsEnvironment("Testing")) &&
            IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var cidrs = (config.GetSection("Security:AllowedCidrs").Get<string[]>() ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToArray();

        if (cidrs.Length == 0)
            return true;

        if (ip.AddressFamily != AddressFamily.InterNetwork)
            return false;

        return cidrs.Any(cidr => CidrMatch(ip, cidr));
    }

    public static bool CidrMatch(IPAddress ip, string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
            return false;

        var parts = cidr.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
            return false;

        if (!IPAddress.TryParse(parts[0], out var network))
            return false;

        if (!int.TryParse(parts[1], out var prefix))
            return false;

        if (prefix < 0 || prefix > 32)
            return false;

        if (ip.AddressFamily != AddressFamily.InterNetwork)
            return false;

        if (network.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var ipBytes = ip.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();

        uint ipInt =
            ((uint)ipBytes[0] << 24) |
            ((uint)ipBytes[1] << 16) |
            ((uint)ipBytes[2] << 8) |
            ipBytes[3];

        uint networkInt =
            ((uint)networkBytes[0] << 24) |
            ((uint)networkBytes[1] << 16) |
            ((uint)networkBytes[2] << 8) |
            networkBytes[3];

        uint mask = prefix == 0
            ? 0u
            : 0xFFFFFFFFu << (32 - prefix);

        return (ipInt & mask) == (networkInt & mask);
    }
}