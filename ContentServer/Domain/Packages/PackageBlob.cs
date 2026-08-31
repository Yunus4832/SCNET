using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Packages;

public partial record PackageBlobId : IGuidStronglyTypedId;

public sealed class PackageBlob : Entity<PackageBlobId>, IAggregateRoot
{
    private PackageBlob()
    {
    }

    public string Hash { get; private set; } = string.Empty;

    public string BlobHash { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string MediaType { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static PackageBlob Create(
        string hash,
        string blobHash,
        long size,
        string fileName,
        string mediaType,
        DateTimeOffset now)
    {
        return new PackageBlob
        {
            Hash = hash,
            BlobHash = blobHash,
            Size = size,
            FileName = fileName,
            MediaType = mediaType,
            CreatedAt = now
        };
    }
}
