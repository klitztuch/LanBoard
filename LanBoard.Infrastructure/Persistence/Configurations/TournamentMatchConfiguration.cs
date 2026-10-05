using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class TournamentMatchConfiguration : IEntityTypeConfiguration<TournamentMatch>
{
    public void Configure(EntityTypeBuilder<TournamentMatch> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.TournamentId, m.Round, m.Slot }).IsUnique();

        builder.HasOne(m => m.Tournament)
            .WithMany(t => t.Matches)
            .HasForeignKey(m => m.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (not Cascade) on the participant cross-references: TournamentMatch rows are
        // already deleted via the Tournament -> Matches cascade above, so cascading a second
        // time via Tournament -> Participants -> Match would be a redundant delete path.
        builder.HasOne(m => m.Participant1)
            .WithMany()
            .HasForeignKey(m => m.Participant1Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Participant2)
            .WithMany()
            .HasForeignKey(m => m.Participant2Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Winner)
            .WithMany()
            .HasForeignKey(m => m.WinnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
