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

public sealed class LinkStatisticsEndpointTests
{
    [Fact]
    public async Task GetReturnsAccessCountWhenManagementKeyIsValid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await MigrateAsync(factory);

        var createdLink = await CreateLinkAsync(client);
        using var redirectResponse = await client.GetAsync($"/{createdLink.Code}");
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/links/{createdLink.Code}/stats");
        request.Headers.Add("X-Management-Key", createdLink.ManagementKey);

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var statistics = await response.Content.ReadFromJsonAsync<LinkStatisticsResponse>();
        Assert.NotNull(statistics);
        Assert.Equal(createdLink.Code, statistics.Code);
        Assert.Equal(1, statistics.AccessCount);
    }

    [Fact]
    public async Task GetReturnsUnauthorizedWhenManagementKeyIsWrongOrMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient();
        await MigrateAsync(factory);

        var createdLink = await CreateLinkAsync(client);

        using var wrongKeyRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/links/{createdLink.Code}/stats");
        wrongKeyRequest.Headers.Add("X-Management-Key", "invalid-key");
        using var wrongKeyResponse = await client.SendAsync(wrongKeyRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongKeyResponse.StatusCode);
        Assert.Equal(
            "application/problem+json",
            wrongKeyResponse.Content.Headers.ContentType?.MediaType);

        using var missingKeyResponse = await client.GetAsync(
            $"/api/links/{createdLink.Code}/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, missingKeyResponse.StatusCode);
    }

    [Fact]
    public async Task GetReturnsNotFoundWhenCodeDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection);
        using var client = factory.CreateClient();
        await MigrateAsync(factory);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/links/MISSING/stats");
        request.Headers.Add("X-Management-Key", "some-management-key");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task MigrateAsync(LinkApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static async Task<CreateLinkResponse> CreateLinkAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/destination" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdLink = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
        Assert.NotNull(createdLink);
        Assert.False(string.IsNullOrWhiteSpace(createdLink.ManagementKey));
        return createdLink;
    }

    private sealed record CreateLinkResponse(string ShortUrl, string Code, string ManagementKey);

    private sealed record LinkStatisticsResponse(string Code, long AccessCount);

    private sealed class LinkApiFactory(SqliteConnection connection)
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
            });
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
