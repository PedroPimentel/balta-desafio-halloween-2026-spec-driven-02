using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Tests;

public sealed class LinkRepositoryConcurrencyTests
{
    [Fact]
    public async Task IncrementAccessCountAsyncDoesNotLoseConcurrentUpdates()
    {
        const int accessCount = 64;
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"LinkShortener-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Default Timeout=30;Pooling=False";
        var options = new DbContextOptionsBuilder<LinkShortenerDbContext>()
            .UseSqlite(connectionString)
            .Options;

        try
        {
            await using (var dbContext = new LinkShortenerDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Links.Add(new Link(
                    "CONCUR1",
                    "https://example.com/target",
                    DateTime.UtcNow,
                    null));
                await dbContext.SaveChangesAsync();
            }

            var updates = Enumerable.Range(0, accessCount)
                .Select(_ => IncrementAccessCountAsync(options, "CONCUR1"));

            var results = await Task.WhenAll(updates);

            Assert.All(results, Assert.True);

            await using var verificationContext = new LinkShortenerDbContext(options);
            var link = await verificationContext.Links.SingleAsync();

            Assert.Equal(accessCount, link.AccessCount);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static async Task<bool> IncrementAccessCountAsync(
        DbContextOptions<LinkShortenerDbContext> options,
        string code)
    {
        await using var dbContext = new LinkShortenerDbContext(options);
        var repository = new LinkRepository(dbContext);
        return await repository.IncrementAccessCountAsync(code, CancellationToken.None);
    }
}
