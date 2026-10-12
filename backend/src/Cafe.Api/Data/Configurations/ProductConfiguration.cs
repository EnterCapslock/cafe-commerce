using Cafe.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Cafe.Api.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).IsRequired().HasMaxLength(120);
        b.Property(p => p.Description).IsRequired().HasMaxLength(1000);
        b.Property(p => p.ImageUrl).HasMaxLength(500);
        b.Property(p => p.ImagePublicId).HasMaxLength(200);
        b.Property(p => p.Category).HasConversion<string>().HasMaxLength(30);

        b.HasIndex(p => new { p.Category, p.IsActive });

    }
}