using ContentServer.Domain.Administration;
using ContentServer.Domain.Contents;
using ContentServer.Domain.Packages;
using ContentServer.Domain.Publishers;
using ContentServer.Domain.Reviews;

using MediatR;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Repository.EntityFrameworkCore;

namespace ContentServer.Infrastructure;

public sealed partial class ContentServerDbContext(
    DbContextOptions<ContentServerDbContext> options,
    IMediator mediator
) : AppDbContextBase(options, mediator)
{
    public DbSet<Administrator> Administrators => Set<Administrator>();
    public DbSet<AdministratorKey> AdministratorKeys => Set<AdministratorKey>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<PublisherKey> PublisherKeys => Set<PublisherKey>();
    public DbSet<ContentItem> Contents => Set<ContentItem>();
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
    public DbSet<PackageBlob> PackageBlobs => Set<PackageBlob>();
    public DbSet<ReviewRecord> ReviewRecords => Set<ReviewRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentServerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ConfigureStronglyTypedIdValueConverter(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);
    }
}
