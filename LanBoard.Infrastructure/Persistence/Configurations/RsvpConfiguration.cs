using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class RsvpConfiguration : IEntityTypeConfiguration<Rsvp>
{
    public void Configure(EntityTypeBuilder<Rsvp> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RespondedAt).IsRequired();

        builder.HasIndex(r => new { r.UserId, r.PartyId }).IsUnique();

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Party)
            .WithMany()
            .HasForeignKey(r => r.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
