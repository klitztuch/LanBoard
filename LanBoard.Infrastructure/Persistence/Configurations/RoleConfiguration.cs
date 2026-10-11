using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.Property(r => r.NormalizedName).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Permissions).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasIndex(r => r.NormalizedName).IsUnique();

        // Anonymous seed without Permissions: a List<string> in HasData makes the model non-deterministic
        // (PendingModelChangesWarning on every migrate). The column default yields the empty array instead.
        builder.HasData(new
        {
            Id = Role.AdminId,
            Name = "Admin",
            NormalizedName = "ADMIN",
            IsSystem = true,
            CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
