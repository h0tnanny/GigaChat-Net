using Microsoft.EntityFrameworkCore;

namespace GigaChat.Net.EFCore;

/// <summary>
/// EF Core DbContext for persisting GigaChat agent threads.
/// Derive from this class in your application to add it to your own DbContext,
/// or use it directly with a provider-specific <see cref="DbContextOptions"/>.
/// </summary>
public class GigaChatDbContext : DbContext
{
    /// <inheritdoc />
    public GigaChatDbContext(DbContextOptions<GigaChatDbContext> options) : base(options) { }

    /// <summary>Agent thread records.</summary>
    public DbSet<GigaChatThreadRecord> GigaChatThreads => Set<GigaChatThreadRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GigaChatThreadRecord>(entity =>
        {
            entity.HasKey(x => x.ThreadId);
            entity.Property(x => x.ThreadId).HasMaxLength(256);
            entity.Property(x => x.HistoryJson).IsRequired();
            entity.Property(x => x.StepsJson).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
        });
    }
}
