using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.GameAppId).HasMaxLength(50);
        builder.Property(s => s.GameName).HasMaxLength(200);
        builder.Property(s => s.LastSeen).IsRequired();
        builder.Property(s => s.JoinedAt).IsRequired();

        builder.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Party)
            .WithMany(p => p.Sessions)
            .HasForeignKey(s => s.PartyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
