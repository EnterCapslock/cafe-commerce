using Cafe.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Cafe.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Name).IsRequired().HasMaxLength(100);
        b.Property(u => u.Email).IsRequired().HasMaxLength(254);
        b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(100);
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(u => u.Email).IsUnique();
    }
}