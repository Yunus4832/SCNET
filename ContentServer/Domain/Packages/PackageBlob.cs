using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Packages;

public partial record PackageBlobId : IGuidStronglyTypedId;

public sealed class PackageBlob : Entity<PackageBlobId>, IAggregateRoot
{
    private PackageBlob()
    {
    }

    public string Hash { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string MediaType { get; private set; } = string.Empty;

    public byte[] Data { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public static PackageBlob Create(
        string hash,
        string fileName,
        string mediaType,
        byte[] data,
        DateTimeOffset now)
    {
        return new PackageBlob
        {
            Hash = hash,
            Size = data.LongLength,
            FileName = fileName,
            MediaType = mediaType,
            Data = data,
            CreatedAt = now
        };
    }
}
