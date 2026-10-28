using System.Security.Cryptography;

namespace LinkShortener.Application.Links;

public sealed class ManagementKeyGenerator : IManagementKeyGenerator
{
    public string Generate()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
