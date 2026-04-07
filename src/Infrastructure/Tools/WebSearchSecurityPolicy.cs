using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Tools;

internal static class WebSearchSecurityPolicy
{
    public static async Task<bool> IsUriAllowedAsync(Uri uri, WebSearchOptions options, CancellationToken ct)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.Host;
        if (IsLocalOrPrivateHostLiteral(host))
        {
            return false;
        }

        if (MatchesHost(host, options.DeniedHosts))
        {
            return false;
        }

        if (options.AllowedHosts.Length > 0)
        {
            return MatchesHost(host, options.AllowedHosts);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            return addresses.Length > 0 && addresses.All(address => !IsPrivateAddress(address));
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesHost(string host, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (string.Equals(host, candidate, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith($".{candidate}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLocalOrPrivateHostLiteral(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IsPrivateAddress(address);
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true,
                172 when bytes[1] is >= 16 and <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast;
        }

        return false;
    }
}
