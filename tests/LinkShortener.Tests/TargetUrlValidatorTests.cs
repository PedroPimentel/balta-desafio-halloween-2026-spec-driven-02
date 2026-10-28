using System.Net;
using System.Net.Sockets;
using LinkShortener.Application.Links;

namespace LinkShortener.Tests;

public sealed class TargetUrlValidatorTests
{
    [Theory]
    [InlineData("http://example.com/path")]
    [InlineData("https://example.com/path")]
    public async Task ValidateAsyncAcceptsHttpAndHttps(string targetUrl)
    {
        var validator = CreateValidator(IPAddress.Parse("93.184.216.34"));

        var result = await validator.ValidateAsync(targetUrl);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/file.txt")]
    [InlineData("http:///missing-host")]
    [InlineData("http://localhost")]
    [InlineData("https://service.local")]
    [InlineData("https://service.test")]
    [InlineData("https://127.0.0.1")]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://192.168.1.1")]
    [InlineData("https://[::1]")]
    [InlineData("https://[fc00::1]")]
    [InlineData("https://[2001:db8::1]")]
    public async Task ValidateAsyncRejectsInvalidOrNonPublicUrls(string? targetUrl)
    {
        var validator = CreateValidator(IPAddress.Parse("93.184.216.34"));

        var result = await validator.ValidateAsync(targetUrl);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task ValidateAsyncRejectsUrlsLongerThan2048Characters()
    {
        var validator = CreateValidator(IPAddress.Parse("93.184.216.34"));
        var targetUrl = $"https://example.com/{new string('a', 2048)}";

        var result = await validator.ValidateAsync(targetUrl);

        Assert.False(result.IsValid);
        Assert.Contains("2048", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsyncRejectsHostnameResolvingToPrivateAddress()
    {
        var validator = CreateValidator(IPAddress.Parse("192.168.1.10"));

        var result = await validator.ValidateAsync("https://example.com");

        Assert.False(result.IsValid);
        Assert.Contains("publicamente acessível", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsyncRejectsHostnameThatCannotBeResolved()
    {
        var validator = new TargetUrlValidator(new FailingHostResolver());

        var result = await validator.ValidateAsync("https://example.com");

        Assert.False(result.IsValid);
        Assert.Contains("resolvido publicamente", result.ErrorMessage);
    }

    private static TargetUrlValidator CreateValidator(IPAddress address)
    {
        return new TargetUrlValidator(new FixedHostResolver(address));
    }

    private sealed class FixedHostResolver(IPAddress address) : ILinkHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            return Task.FromResult(new[] { address });
        }
    }

    private sealed class FailingHostResolver : ILinkHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }
    }
}
