using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Address).HasMaxLength(4000);
        builder.Property(l => l.Directions).HasMaxLength(4000);
        builder.Property(l => l.Parking).HasMaxLength(4000);
        builder.Property(l => l.Notes).HasMaxLength(4000);

        // Default Postgres collation: case-sensitive, as intended.
        builder.HasIndex(l => l.Name).IsUnique();
    }
}
