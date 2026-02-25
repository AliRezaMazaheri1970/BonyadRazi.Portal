using System.Net;
using System.Net.Sockets;

namespace Gateway;

public static class GatewaySecurityHelper
{
    /// <summary>
    /// Gets the real client IP. Prefers X-Forwarded-For (first hop) if present.
    /// Handles IPv4-mapped IPv6 addresses.
    /// </summary>
    public static IPAddress? GetClientIp(HttpContext ctx)
    {
        // If behind reverse proxy / load balancer:
        if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var first = forwarded.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (IPAddress.TryParse(first, out var parsed))
            {
                if (parsed.IsIPv4MappedToIPv6) parsed = parsed.MapToIPv4();
                return parsed;
            }
        }

        var ip = ctx.Connection.RemoteIpAddress;
        if (ip is null) return null;

        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip;
    }

    /// <summary>
    /// Checks whether current request path is allowed by the configured prefixes.
    /// If no prefixes configured, it allows everything.
    /// </summary>
    public static bool IsPathAllowedByPrefixes(PathString path, IEnumerable<string> prefixes)
    {
        if (prefixes is null) return true;

        var list = prefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToArray();

        if (list.Length == 0) return true;

        return list.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks IP allowlist based on CIDR ranges in config:
    /// Security:AllowedCidrs = ["192.168.93.0/27", ...]
    /// Also supports loopback allowance in Development if enabled:
    /// Security:AllowLoopbackInDevelopment = true/false
    /// If no CIDRs configured, it allows everything.
    /// </summary>
    public static bool IsIpAllowed(HttpContext ctx, IConfiguration config, IHostEnvironment env)
    {
        // ✅ configurable bypass for Testing
        var bypassInTesting = config.GetValue("Security:BypassIpAllowlistInTesting", true);
        if (env.IsEnvironment("Testing") && bypassInTesting)
            return true;

        var ip = GetClientIp(ctx);
        if (ip is null) return false;

        var allowLoopback = config.GetValue("Security:AllowLoopbackInDevelopment", true);
        if (allowLoopback && (env.IsDevelopment() || env.IsEnvironment("Testing")) && IPAddress.IsLoopback(ip))
            return true;

        var cidrs = (config.GetSection("Security:AllowedCidrs").Get<string[]>() ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToArray();

        if (cidrs.Length == 0) return true;

        if (ip.AddressFamily != AddressFamily.InterNetwork)
            return false;

        return cidrs.Any(c => CidrMatch(ip, c));
    }

    /// <summary>
    /// IPv4 CIDR match: "192.168.93.0/27".
    /// Returns false for invalid CIDR.
    /// </summary>
    public static bool CidrMatch(IPAddress ip, string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr)) return false;

        var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        if (!IPAddress.TryParse(parts[0], out var network)) return false;
        if (!int.TryParse(parts[1], out var prefix)) return false;
        if (prefix < 0 || prefix > 32) return false;

        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        if (network.AddressFamily != AddressFamily.InterNetwork) return false;

        var ipBytes = ip.GetAddressBytes();
        var netBytes = network.GetAddressBytes();

        uint ipInt = ((uint)ipBytes[0] << 24) | ((uint)ipBytes[1] << 16) | ((uint)ipBytes[2] << 8) | ipBytes[3];
        uint netInt = ((uint)netBytes[0] << 24) | ((uint)netBytes[1] << 16) | ((uint)netBytes[2] << 8) | netBytes[3];

        uint mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
        return (ipInt & mask) == (netInt & mask);
    }
}