using Cafe.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Cafe.Api.Data.Configurations;

public class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutboxItem>
{
    public void Configure(EntityTypeBuilder<EmailOutboxItem> b)
    {
        b.ToTable("EmailOutbox");
        b.HasKey(e => e.Id);
        b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(30);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.ToAddress).IsRequired().HasMaxLength(254);
        b.Property(e => e.ToName).IsRequired().HasMaxLength(100);
        b.Property(e => e.Subject).IsRequired().HasMaxLength(200);
        b.Property(e => e.HtmlBody).IsRequired();
        b.Property(e => e.TextBody).IsRequired();
        b.Property(e => e.LastError).HasMaxLength(500);

        b.HasIndex(e => new { e.Status, e.NextAttemptAt });   // the worker's "what's due?" query
        b.HasIndex(e => e.OrderId);
    }
}