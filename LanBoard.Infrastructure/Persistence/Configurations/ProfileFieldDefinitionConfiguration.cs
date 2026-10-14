using System.Text.Json;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanBoard.Infrastructure.Persistence.Configurations;

public class ProfileFieldDefinitionConfiguration : IEntityTypeConfiguration<ProfileFieldDefinition>
{
    public void Configure(EntityTypeBuilder<ProfileFieldDefinition> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Key).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Visibility).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Choices).HasColumnType("text[]").HasDefaultValueSql("'{}'");
        builder.Property(d => d.CreatedAt).IsRequired();

        // Npgsql maps Dictionary<string,string> to hstore by default; store culture → label as jsonb instead.
        builder.Property(d => d.Labels)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions.Default) ?? new(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                    v => v.Aggregate(0, (h, kv) => h ^ HashCode.Combine(kv.Key, kv.Value)),
                    v => new Dictionary<string, string>(v)));

        builder.HasIndex(d => d.Key).IsUnique();
    }
}
