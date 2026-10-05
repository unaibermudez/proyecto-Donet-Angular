using DocAssist.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocAssist.Api.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.Property(d => d.FileName).HasMaxLength(255);
        builder.Property(d => d.ContentType).HasMaxLength(100);
        builder.Property(d => d.StoredFileName).HasMaxLength(100);
        builder.HasIndex(d => d.StoredFileName).IsUnique();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(d => d.StatusMessage).HasMaxLength(1000);

        // Uno a muchos opcional: un producto tiene varios documentos y un documento
        // tiene como mucho un producto. Si se borra el producto, sus documentos se
        // quedan como generales (product_id = NULL) en lugar de borrarse.
        builder.HasOne(d => d.Product)
            .WithMany(p => p.Documents)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
