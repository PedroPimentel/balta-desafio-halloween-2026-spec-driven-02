using System.Text.Json;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinkShortener.Tests;

public sealed class SwaggerDocumentationTests
{
    [Fact]
    public async Task DevelopmentExposesSwaggerUiAndOpenApiDescription()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection, "Development");
        using var client = factory.CreateClient();

        using var uiResponse = await client.GetAsync("/swagger/index.html");
        Assert.Equal(System.Net.HttpStatusCode.OK, uiResponse.StatusCode);

        using var openApiResponse = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(System.Net.HttpStatusCode.OK, openApiResponse.StatusCode);

        using var document = JsonDocument.Parse(
            await openApiResponse.Content.ReadAsStringAsync());
        var createLinkOperation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/links")
            .GetProperty("post");

        Assert.Equal("CreateLink", createLinkOperation.GetProperty("operationId").GetString());
        Assert.True(createLinkOperation.GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(createLinkOperation.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(createLinkOperation.GetProperty("responses").TryGetProperty("409", out _));
    }

    [Fact]
    public async Task ProductionDoesNotExposeSwaggerUiOrOpenApiDocument()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = new LinkApiFactory(connection, "Production");
        using var client = factory.CreateClient();

        using var uiResponse = await client.GetAsync("/swagger/index.html");
        using var openApiResponse = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, uiResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, openApiResponse.StatusCode);
    }

    private sealed class LinkApiFactory(SqliteConnection connection, string environment)
        : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<LinkShortenerDbContext>();
                services.RemoveAll<DbContextOptions<LinkShortenerDbContext>>();
                services.AddDbContext<LinkShortenerDbContext>(options =>
                    options.UseSqlite(connection));
            });
        }
    }
}
