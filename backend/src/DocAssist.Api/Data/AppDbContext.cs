using DocAssist.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DocAssist.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // La migración incluirá CREATE EXTENSION IF NOT EXISTS vector.
        modelBuilder.HasPostgresExtension("vector");

        // Aplica todas las clases IEntityTypeConfiguration de este proyecto.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
