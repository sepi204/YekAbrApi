using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YekAbr.Domain.Entities;
using YekAbr.Infrastructure.Identity;

namespace YekAbr.Infrastructure.Persistence.Configurations;

public sealed class ConnectedCloudAccountConfiguration : IEntityTypeConfiguration<ConnectedCloudAccount>
{
    public void Configure(EntityTypeBuilder<ConnectedCloudAccount> builder)
    {
        builder.ToTable("ConnectedCloudAccounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        // Stored as int for compact, stable persistence across provider additions.
        builder.Property(x => x.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.AccountEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ProviderAccountId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.AccessToken)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.RefreshToken)
            .HasColumnType("text");

        builder.Property(x => x.RootFolderId)
            .HasMaxLength(512);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Provider);
        builder.HasIndex(x => new { x.UserId, x.Provider, x.ProviderAccountId })
            .IsUnique();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
