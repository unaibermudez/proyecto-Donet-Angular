using DocAssist.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocAssist.Api.Data.Configurations;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        // Columna de tipo vector de pgvector, con el tamaño fijo del modelo de embeddings.
        builder.Property(c => c.Embedding)
            .HasColumnType($"vector({DocumentChunk.EmbeddingDimensions})");

        // Índice HNSW para buscar los vectores más parecidos sin recorrer toda la tabla.
        // vector_cosine_ops: la búsqueda del paso 9 usará la distancia coseno.
        builder.HasIndex(c => c.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        // Un documento no puede tener dos fragmentos en la misma posición.
        builder.HasIndex(c => new { c.DocumentId, c.Index }).IsUnique();

        // Los fragmentos no tienen sentido sin su documento: se borran con él.
        builder.HasOne(c => c.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
