using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class ProfileFieldValueConfiguration : IEntityTypeConfiguration<ProfileFieldValue>
{
    public void Configure(EntityTypeBuilder<ProfileFieldValue> builder)
    {
        builder.HasKey(v => new { v.UserId, v.FieldId });
        builder.Property(v => v.Value).HasMaxLength(200).IsRequired();

        builder.HasIndex(v => v.FieldId);

        // Values hang off the profile row (which cascades from Users), so deleting a user removes them too.
        builder.HasOne<UserProfile>()
            .WithMany(p => p.FieldValues)
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Field)
            .WithMany()
            .HasForeignKey(v => v.FieldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
