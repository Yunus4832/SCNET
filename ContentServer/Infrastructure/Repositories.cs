using ContentServer.Domain.Administration;
using ContentServer.Domain.Contents;
using ContentServer.Domain.Packages;
using ContentServer.Domain.Publishers;
using ContentServer.Domain.Reviews;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Domain.ServerSources;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Repository.EntityFrameworkCore;

namespace ContentServer.Infrastructure;

public sealed class AdministratorRepository(
    ContentServerDbContext context
) : RepositoryBase<Administrator, AdministratorId, ContentServerDbContext>(context)
{
    private readonly ContentServerDbContext _context = context;

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        return _context.Administrators.AnyAsync(cancellationToken);
    }

    public Task<Administrator?> FindAsync(AdministratorId id, CancellationToken cancellationToken) =>
        _context.Administrators.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Administrator?> FindWithKeysAsync(AdministratorId id, CancellationToken cancellationToken) =>
        _context.Administrators.Include(x => x.Keys).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}

public sealed class PublisherRepository(
    ContentServerDbContext context
) : RepositoryBase<Publisher, PublisherId, ContentServerDbContext>(context)
{
    private readonly ContentServerDbContext _context = context;

    public Task<Publisher?> FindAsync(PublisherId id, CancellationToken cancellationToken)
    {
        return _context.Publishers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<Publisher?> FindWithKeysAsync(PublisherId id, CancellationToken cancellationToken)
    {
        return _context.Publishers.Include(item => item.Keys)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }
}

public sealed class ContentRepository(
    ContentServerDbContext context
) : RepositoryBase<ContentItem, ContentId, ContentServerDbContext>(context)
{
    private readonly ContentServerDbContext _context = context;

    public Task<ContentItem?> FindByPublisherAndIdentifierAsync(
        PublisherId publisherId,
        string normalizedIdentifier,
        CancellationToken cancellationToken
    )
    {
        return _context.Contents.Include(item => item.Versions).FirstOrDefaultAsync(
            item => item.PublisherId == publisherId && item.NormalizedIdentifier == normalizedIdentifier,
            cancellationToken);
    }

    public Task<ContentItem?> FindAsync(ContentId id, CancellationToken cancellationToken)
    {
        return _context.Contents.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<ContentItem?> FindByVersionAsync(ContentVersionId versionId, CancellationToken cancellationToken)
    {
        return _context.Contents.Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Versions.Any(version => version.Id == versionId), cancellationToken);
    }
}

public sealed class ReviewRecordRepository(
    ContentServerDbContext context
) : RepositoryBase<ReviewRecord, ReviewRecordId, ContentServerDbContext>(context);

public sealed class PackageBlobRepository(
    ContentServerDbContext context
) : RepositoryBase<PackageBlob, PackageBlobId, ContentServerDbContext>(context)
{
    private readonly ContentServerDbContext _context = context;

    public Task<PackageBlob?> FindByHashAsync(string hash, CancellationToken cancellationToken)
    {
        return _context.PackageBlobs.FirstOrDefaultAsync(
            package => package.Hash == hash,
            cancellationToken);
    }
}

public sealed class ServerSourceRegistrationRepository(
    ContentServerDbContext context
) : RepositoryBase<ServerSourceRegistration, ServerSourceRegistrationId, ContentServerDbContext>(context)
{
    private readonly ContentServerDbContext _context = context;

    public Task<ServerSourceRegistration?> FindAsync(ServerSourceRegistrationId id,
        CancellationToken cancellationToken)
    {
        return _context.ServerSources.FirstOrDefaultAsync(source => source.Id == id, cancellationToken);
    }

    public Task<bool> HasPendingOrActiveUrlAsync(string apiUrl, CancellationToken cancellationToken)
    {
        return _context.ServerSources.AnyAsync(source => source.ApiUrl == apiUrl &&
                                                        source.Status != ServerSourceRegistrationStatus.Rejected,
            cancellationToken);
    }

    public void Delete(ServerSourceRegistration source)
    {
        _context.ServerSources.Remove(source);
    }
}

public sealed class DirectoryServerRepository(
    ContentServerDbContext context
) : RepositoryBase<DirectoryServer, DirectoryServerId, ContentServerDbContext>(context)
{
    private readonly ContentServerDbContext _context = context;

    public Task<DirectoryServer?> FindAsync(DirectoryServerId id, CancellationToken cancellationToken) =>
        _context.DirectoryServers.FirstOrDefaultAsync(server => server.Id == id, cancellationToken);

    public Task<bool> HasPendingOrApprovedAddressAsync(string address, CancellationToken cancellationToken) =>
        _context.DirectoryServers.AnyAsync(server => server.Address == address &&
            server.ReviewStatus != DirectoryServerReviewStatus.Rejected, cancellationToken);

    public Task<bool> HasPendingOrApprovedAddressAsync(string address, DirectoryServerId excludedId,
        CancellationToken cancellationToken) =>
        _context.DirectoryServers.AnyAsync(server => server.Id != excludedId && server.Address == address &&
            server.ReviewStatus != DirectoryServerReviewStatus.Rejected, cancellationToken);

    public void Delete(DirectoryServer server) => _context.DirectoryServers.Remove(server);
}
