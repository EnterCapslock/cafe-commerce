using Cafe.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Cafe.Api.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.ProductName).IsRequired().HasMaxLength(120);
        b.Property(i => i.VariantName).IsRequired().HasMaxLength(50);
        b.Property(i => i.PriceAtPurchase).HasColumnType("numeric(10,2)");

        b.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(i => i.ProductVariant)
            .WithMany()
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(i => i.OrderId);
    }
}