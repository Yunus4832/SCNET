using System.Security.Cryptography;

using ContentServer.Domain.Packages;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record StoredPackageBlob(
    PackageBlobId Id,
    string Hash,
    long Size,
    string FileName,
    string MediaType);

public sealed record StorePackageBlobCommand(
    string FileName,
    string MediaType,
    byte[] Data) : ICommand<StoredPackageBlob>;

public sealed class StorePackageBlobCommandHandler(PackageBlobRepository repository)
    : ICommandHandler<StorePackageBlobCommand, StoredPackageBlob>
{
    public async Task<StoredPackageBlob> Handle(
        StorePackageBlobCommand command,
        CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(command.Data)).ToLowerInvariant();
        var package = await repository.FindByHashAsync(hash, cancellationToken);
        if (package is null)
        {
            package = PackageBlob.Create(
                hash,
                command.FileName,
                command.MediaType,
                command.Data,
                DateTimeOffset.UtcNow);
            await repository.AddAsync(package, cancellationToken);
        }

        return new StoredPackageBlob(
            package.Id,
            package.Hash,
            package.Size,
            package.FileName,
            package.MediaType);
    }
}
