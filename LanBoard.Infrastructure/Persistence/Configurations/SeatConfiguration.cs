using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Label).HasMaxLength(100).IsRequired();

        builder.HasOne(s => s.Party)
            .WithMany(p => p.Seats)
            .HasForeignKey(s => s.PartyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.AssignedUser)
            .WithMany(u => u.AssignedSeats)
            .HasForeignKey(s => s.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
