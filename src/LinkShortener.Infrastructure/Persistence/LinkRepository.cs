using LinkShortener.Application.Links;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class LinkRepository(LinkShortenerDbContext dbContext) : ILinkRepository
{
    public async Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
    {
        dbContext.Links.Add(link);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException
            {
                SqliteErrorCode: 19,
                SqliteExtendedErrorCode: 2067
            })
        {
            dbContext.Entry(link).State = EntityState.Detached;
            return false;
        }
    }

    public Task<Link?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return dbContext.Links
            .AsNoTracking()
            .SingleOrDefaultAsync(link => link.Code == code, cancellationToken);
    }

    public async Task<bool> IncrementAccessCountAsync(string code, CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.Links
            .Where(link => link.Code == code)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    link => link.AccessCount,
                    link => link.AccessCount + 1),
                cancellationToken);

        return affectedRows > 0;
    }
}
