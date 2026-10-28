using LinkShortener.Application.Links;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class LinkShortenerDbContext(DbContextOptions<LinkShortenerDbContext> options)
    : DbContext(options)
{
    public DbSet<Link> Links => Set<Link>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<Link>();

        link.ToTable("Links");
        link.HasKey(entity => entity.Id);
        link.Property(entity => entity.Code)
            .IsRequired()
            .HasMaxLength(20);
        link.HasIndex(entity => entity.Code)
            .IsUnique();
        link.Property(entity => entity.TargetUrl)
            .IsRequired()
            .HasMaxLength(2048);
        link.Property(entity => entity.CreatedAtUtc)
            .IsRequired();
        link.Property(entity => entity.AccessCount)
            .IsRequired()
            .HasDefaultValue(0L);
        link.Property(entity => entity.ManagementKeyHash)
            .HasMaxLength(64);
    }
}
