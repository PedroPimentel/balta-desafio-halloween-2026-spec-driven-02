using System.Net;
using System.Net.Http.Json;
using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinkShortener.Tests;

public sealed class CreateLinkEndpointTests
{
    [Fact]
    public async Task PostCreatesLinkAndReturnsCreatedResponse()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/destination" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
        Assert.NotNull(result);
        Assert.Matches("^[A-Z1-9]{7}$", result.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.ManagementKey));
        Assert.Equal($"http://localhost:5041/{result.Code}", result.ShortUrl);
        Assert.Equal(result.ShortUrl, response.Headers.Location?.ToString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext =
            verificationScope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
        var persistedLink = await verificationContext.Links.SingleAsync();

        Assert.Equal(result.Code, persistedLink.Code);
        Assert.Equal("https://example.com/destination", persistedLink.TargetUrl);
        Assert.NotNull(persistedLink.ManagementKeyHash);
        Assert.NotEqual(result.ManagementKey, persistedLink.ManagementKeyHash);
        Assert.Equal(64, persistedLink.ManagementKeyHash.Length);
    }

    [Fact]
    public async Task PostReturnsProblemDetailsForInvalidTargetUrl()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "ftp://example.com/file" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostRetriesWhenGeneratedCodeIsAlreadyInUse()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(
            connection,
            new SequenceCodeGenerator("AAAAAAA", "BBBBBBB"));
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            await dbContext.Database.MigrateAsync();
            dbContext.Links.Add(new Link(
                "AAAAAAA",
                "https://example.com/existing",
                DateTime.UtcNow,
                null));
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/new" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
        Assert.NotNull(result);
        Assert.Equal("BBBBBBB", result.Code);
    }

    [Fact]
    public async Task PostCreatesLinkWithCustomCode()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(
            connection,
            new SequenceCodeGenerator());
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/destination", code = "MYLINK9" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
        Assert.NotNull(result);
        Assert.Equal("MYLINK9", result.Code);
        Assert.Equal("http://localhost:5041/MYLINK9", result.ShortUrl);
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("ABC0")]
    [InlineData("abcD")]
    [InlineData("AB C")]
    [InlineData("AB-C")]
    public async Task PostReturnsProblemDetailsForInvalidCustomCode(string code)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/destination", code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostReturnsConflictWhenCustomCodeIsAlreadyInUse()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            await dbContext.Database.MigrateAsync();
            dbContext.Links.Add(new Link(
                "MYCODE",
                "https://example.com/existing",
                DateTime.UtcNow,
                null));
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/new", code = "MYCODE" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    private sealed record CreateLinkResponse(string ShortUrl, string Code, string ManagementKey);

    private sealed class LinkApiFactory(
        SqliteConnection connection,
        ILinkCodeGenerator? codeGenerator = null)
        : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<LinkShortenerDbContext>();
                services.RemoveAll<DbContextOptions<LinkShortenerDbContext>>();
                services.AddDbContext<LinkShortenerDbContext>(options =>
                    options.UseSqlite(connection));

                services.RemoveAll<ILinkHostResolver>();
                services.AddSingleton<ILinkHostResolver>(
                    new FixedHostResolver(IPAddress.Parse("93.184.216.34")));

                if (codeGenerator is not null)
                {
                    services.RemoveAll<ILinkCodeGenerator>();
                    services.AddSingleton(codeGenerator);
                }
            });
        }
    }

    private sealed class SequenceCodeGenerator(params string[] codes) : ILinkCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public string Generate()
        {
            return _codes.Dequeue();
        }
    }

    private sealed class FixedHostResolver(IPAddress address) : ILinkHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            return Task.FromResult(new[] { address });
        }
    }
}
