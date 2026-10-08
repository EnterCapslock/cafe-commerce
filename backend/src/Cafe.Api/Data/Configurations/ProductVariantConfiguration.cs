using Cafe.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Cafe.Api.Data.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.HasKey(v => v.Id);
        b.Property(v => v.Name).IsRequired().HasMaxLength(50);
        b.Property(v => v.Price).HasColumnType("numeric(10,2)");

        b.HasIndex(v => new { v.ProductId, v.Name }).IsUnique();

        // Deleting a product cascades to its variants, but past orders block
        // the delete (OrderItem -> Variant is Restrict). Use IsActive instead.
        b.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}