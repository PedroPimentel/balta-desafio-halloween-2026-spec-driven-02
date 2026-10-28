using System.Net;
using System.Net.Sockets;

namespace LinkShortener.Application.Links;

public sealed class TargetUrlValidator(ILinkHostResolver hostResolver)
{
    private const int MaximumUrlLength = 2048;

    public async Task<TargetUrlValidationResult> ValidateAsync(
        string? targetUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return TargetUrlValidationResult.Invalid("A URL de destino é obrigatória.");
        }

        if (targetUrl.Length > MaximumUrlLength)
        {
            return TargetUrlValidationResult.Invalid(
                $"A URL de destino não pode exceder {MaximumUrlLength} caracteres.");
        }

        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return TargetUrlValidationResult.Invalid(
                "A URL de destino deve ser absoluta e usar o esquema http ou https.");
        }

        if (IsReservedHostname(uri.IdnHost))
        {
            return TargetUrlValidationResult.Invalid(
                "A URL de destino deve apontar para um endereço publicamente acessível.");
        }

        if (IPAddress.TryParse(uri.Host, out var ipAddress))
        {
            return IsPublicAddress(ipAddress)
                ? TargetUrlValidationResult.Valid
                : TargetUrlValidationResult.Invalid(
                    "A URL de destino deve apontar para um endereço publicamente acessível.");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await hostResolver.ResolveAsync(uri.IdnHost, cancellationToken);
        }
        catch (SocketException)
        {
            return TargetUrlValidationResult.Invalid(
                "O host da URL de destino não pôde ser resolvido publicamente.");
        }

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            return TargetUrlValidationResult.Invalid(
                "A URL de destino deve apontar para um endereço publicamente acessível.");
        }

        return TargetUrlValidationResult.Valid;
    }

    private static bool IsReservedHostname(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".test", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".example", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return IsPublicIpv6Address(address);
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] != 0
            && bytes[0] != 10
            && bytes[0] != 127
            && bytes[0] < 224
            && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            && !(bytes[0] == 169 && bytes[1] == 254)
            && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            && !(bytes[0] == 192 && bytes[1] == 168)
            && !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0)
            && !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2)
            && !(bytes[0] == 198 && bytes[1] is 18 or 19)
            && !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
            && !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
    }

    private static bool IsPublicIpv6Address(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        return bytes[0] is >= 0x20 and <= 0x3f
            && !(bytes[0] == 0x20 && bytes[1] == 0x01
                && bytes[2] == 0x0d && bytes[3] == 0xb8)
            && !(bytes[0] == 0x20 && bytes[1] == 0x02)
            && !address.IsIPv6LinkLocal
            && !address.IsIPv6Multicast
            && !address.IsIPv6SiteLocal;
    }
}
