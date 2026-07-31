using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class LanPartyConfiguration : IEntityTypeConfiguration<LanParty>
{
    public void Configure(EntityTypeBuilder<LanParty> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Location).HasMaxLength(200).IsRequired();
        builder.Property(p => p.InviteCode).HasMaxLength(20);
        builder.Property(p => p.Date).IsRequired();

        builder.HasIndex(p => p.InviteCode).IsUnique();
        builder.HasIndex(p => p.IsActive).IsUnique().HasFilter("\"IsActive\" = true");

        builder.HasOne(p => p.CreatedByUser)
            .WithMany(u => u.CreatedParties)
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
