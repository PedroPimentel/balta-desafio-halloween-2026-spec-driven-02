using System.Net;
using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinkShortener.Tests;

public sealed class RedirectEndpointTests
{
    [Fact]
    public async Task GetRedirectsToTargetUrlAndCountsAccess()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            await dbContext.Database.MigrateAsync();
            dbContext.Links.Add(new Link(
                "ABCD123",
                "https://example.com/target",
                DateTime.UtcNow,
                null));
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.GetAsync("/ABCD123");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://example.com/target", response.Headers.Location?.ToString());

        using var repeatedResponse = await client.GetAsync("/ABCD123");

        Assert.Equal(HttpStatusCode.Redirect, repeatedResponse.StatusCode);
        Assert.Equal("https://example.com/target", repeatedResponse.Headers.Location?.ToString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
        var persistedLink = await verificationContext.Links.SingleAsync();

        Assert.Equal(2, persistedLink.AccessCount);
    }

    [Fact]
    public async Task GetReturnsNotFoundWhenCodeDoesNotExist()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var response = await client.GetAsync("/MISSING");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReturnsGoneWhenLinkHasExpired()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            await dbContext.Database.MigrateAsync();
            dbContext.Links.Add(new Link(
                "EXPIRED",
                "https://example.com/expired",
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow.AddMinutes(-1)));
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.GetAsync("/EXPIRED");

        Assert.Equal((HttpStatusCode)410, response.StatusCode);
    }

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
                    new FixedHostResolver(System.Net.IPAddress.Parse("93.184.216.34")));

                if (codeGenerator is not null)
                {
                    services.RemoveAll<ILinkCodeGenerator>();
                    services.AddSingleton(codeGenerator);
                }
            });
        }
    }

    private sealed class FixedHostResolver(System.Net.IPAddress address) : ILinkHostResolver
    {
        public Task<System.Net.IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            return Task.FromResult(new[] { address });
        }
    }
}