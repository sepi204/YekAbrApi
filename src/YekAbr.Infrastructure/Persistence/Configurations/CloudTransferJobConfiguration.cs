using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YekAbr.Domain.Entities;
using YekAbr.Infrastructure.Identity;

namespace YekAbr.Infrastructure.Persistence.Configurations;

public sealed class CloudTransferJobConfiguration : IEntityTypeConfiguration<CloudTransferJob>
{
    public void Configure(EntityTypeBuilder<CloudTransferJob> builder)
    {
        builder.ToTable("CloudTransferJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.SourceConnectedCloudAccountId)
            .IsRequired();

        builder.Property(x => x.DestinationConnectedCloudAccountId)
            .IsRequired();

        builder.Property(x => x.SourceItemId)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.SourceItemName)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.SourceItemType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.DestinationParentFolderId)
            .HasMaxLength(512);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ProgressPercentage)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.SourceConnectedCloudAccountId);
        builder.HasIndex(x => x.DestinationConnectedCloudAccountId);

        builder.HasOne(x => x.SourceConnectedCloudAccount)
            .WithMany()
            .HasForeignKey(x => x.SourceConnectedCloudAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DestinationConnectedCloudAccount)
            .WithMany()
            .HasForeignKey(x => x.DestinationConnectedCloudAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
