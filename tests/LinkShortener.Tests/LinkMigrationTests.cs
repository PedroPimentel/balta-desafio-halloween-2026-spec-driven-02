using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Tests;

public sealed class LinkMigrationTests
{
    [Fact]
    public async Task MigrationCreatesLinksTableAndPersistsLinkData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LinkShortenerDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LinkShortenerDbContext(options);
        await dbContext.Database.MigrateAsync();

        var link = new Link(
            "ABC1234",
            "https://example.com/destination",
            DateTime.UtcNow,
            null);

        dbContext.Links.Add(link);
        await dbContext.SaveChangesAsync();

        var persistedLink = await dbContext.Links.SingleAsync();

        Assert.Equal("ABC1234", persistedLink.Code);
        Assert.Equal("https://example.com/destination", persistedLink.TargetUrl);
        Assert.Null(persistedLink.ExpiresAtUtc);
        Assert.Equal(0, persistedLink.AccessCount);

        dbContext.Links.Add(new Link(
            "ABC1234",
            "https://example.com/another-destination",
            DateTime.UtcNow,
            null));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}
