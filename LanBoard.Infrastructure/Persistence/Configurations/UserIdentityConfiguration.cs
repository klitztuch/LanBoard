using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class UserIdentityConfiguration : IEntityTypeConfiguration<UserIdentity>
{
    public void Configure(EntityTypeBuilder<UserIdentity> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Provider).HasMaxLength(50).IsRequired();
        builder.Property(i => i.ProviderUserId).HasMaxLength(200).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();

        builder.HasIndex(i => new { i.Provider, i.ProviderUserId }).IsUnique();

        builder.HasOne(i => i.User)
            .WithMany(u => u.Identities)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
