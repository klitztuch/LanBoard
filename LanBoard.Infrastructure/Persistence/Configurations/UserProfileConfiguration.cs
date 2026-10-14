using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.RealName).HasMaxLength(100);
        // No DB default: Public is the enum's CLR default, so EF would replace an explicit Public with it.
        // The entity initializer (= Orga) provides the default instead.
        builder.Property(p => p.RealNameVisibility).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Clan).HasMaxLength(50);
        builder.Property(p => p.FavoriteGames).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        builder.Property(p => p.ContactEmail).HasMaxLength(254);
        builder.Property(p => p.NotificationOptIns).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
