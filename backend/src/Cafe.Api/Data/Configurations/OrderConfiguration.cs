using Cafe.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Cafe.Api.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.HasKey(o => o.Id);
        b.Property(o => o.CustomerName).IsRequired().HasMaxLength(100);
        b.Property(o => o.CustomerEmail).IsRequired().HasMaxLength(254);
        b.Property(o => o.CustomerPhone).IsRequired().HasMaxLength(30);
        b.Property(o => o.TotalAmount).HasColumnType("numeric(10,2)");
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);

        // Powers the live order board: "active orders, oldest first"
        b.HasIndex(o => new { o.Status, o.CreatedAt });
        b.HasIndex(o => o.UserId);

        b.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);   // keep orders if an account is removed
    }
}