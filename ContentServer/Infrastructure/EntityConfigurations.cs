using ContentServer.Domain.Administration;
using ContentServer.Domain.Contents;
using ContentServer.Domain.Packages;
using ContentServer.Domain.Publishers;
using ContentServer.Domain.Reviews;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using NetCorePal.Extensions.Repository.EntityFrameworkCore;

namespace ContentServer.Infrastructure;

public sealed class PublisherConfiguration : IEntityTypeConfiguration<Publisher>
{
    public void Configure(EntityTypeBuilder<Publisher> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseGuidVersion7ValueGenerator();
        b.Property(x => x.Status).HasConversion<string>();
        b.HasMany(x => x.Keys).WithOne(x => x.Owner).HasForeignKey(x => x.PublisherId);
        b.HasIndex(x => x.Status);
    }
}

public sealed class PublisherKeyConfiguration : IEntityTypeConfiguration<PublisherKey>
{
    public void Configure(EntityTypeBuilder<PublisherKey> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseGuidVersion7ValueGenerator();
        b.HasIndex(x => x.KeyHash).IsUnique();
    }
}

public sealed class ContentItemConfiguration : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> b)
    {
        b.ToTable("Contents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseGuidVersion7ValueGenerator();
        b.Property(x => x.Status).HasConversion<string>();
        b.HasIndex(x => x.NormalizedIdentifier).IsUnique();
        b.HasMany(x => x.Versions).WithOne(x => x.Owner).HasForeignKey(x => x.ContentId);
    }
}

public sealed class ContentVersionConfiguration : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseGuidVersion7ValueGenerator();
        b.Property(x => x.Status).HasConversion<string>();
        b.HasIndex(x => new { x.ContentId, x.Version }).IsUnique();
        b.HasOne<PackageBlob>().WithMany().HasForeignKey(x => x.PackageBlobId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PackageBlobConfiguration : IEntityTypeConfiguration<PackageBlob>
{
    public void Configure(EntityTypeBuilder<PackageBlob> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseGuidVersion7ValueGenerator();
        b.HasIndex(x => x.Hash).IsUnique();
    }
}

public sealed class AdministratorConfiguration : IEntityTypeConfiguration<Administrator>
{
    public void Configure(EntityTypeBuilder<Administrator> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseGuidVersion7ValueGenerator();
        b.Property(x => x.Status).HasConversion<string>();
        b.HasMany(x => x.Keys).WithOne(x => x.Owner).HasForeignKey(x => x.AdministratorId);
    }
}

public sealed class AdministratorKeyConfiguration : IEntityTypeConfiguration<AdministratorKey>
{
    public void Configure(EntityTypeBuilder<AdministratorKey> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseGuidVersion7ValueGenerator();
        b.HasIndex(x => x.KeyHash).IsUnique();
    }
}

public sealed class ReviewRecordConfiguration : IEntityTypeConfiguration<ReviewRecord>
{
    public void Configure(EntityTypeBuilder<ReviewRecord> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseGuidVersion7ValueGenerator();
    }
}
