using Microsoft.EntityFrameworkCore;

namespace GigaChat.Net.EFCore;

public class GigaChatDbContext : DbContext
{
    public GigaChatDbContext(DbContextOptions options) : base(options)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    public DbSet<GigaChatThreadRecord> GigaChatThreads => Set<GigaChatThreadRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<GigaChatThreadRecord>(e =>
        {
            e.HasKey(x => x.ThreadId);
            e.Property(x => x.ThreadId).HasMaxLength(256);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
    }
}
