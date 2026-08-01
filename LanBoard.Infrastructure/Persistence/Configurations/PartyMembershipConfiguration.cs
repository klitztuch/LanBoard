using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class PartyMembershipConfiguration : IEntityTypeConfiguration<PartyMembership>
{
    public void Configure(EntityTypeBuilder<PartyMembership> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.JoinedAt).IsRequired();

        builder.HasIndex(m => new { m.UserId, m.PartyId }).IsUnique();

        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Party)
            .WithMany(p => p.Memberships)
            .HasForeignKey(m => m.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
