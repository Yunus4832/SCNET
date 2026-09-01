using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Content.Packaging;

public sealed record ContentPackageHashEntry(string Path, long Length, Func<Stream> OpenRead);

public static class ContentPackageHash
{
    private const int _bufferSize = 64 * 1024;

    public static string Compute(IEnumerable<ContentPackageHashEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var pathLength = new byte[sizeof(uint)];
        var contentLength = new byte[sizeof(ulong)];
        foreach (var entry in entries.OrderBy(entry => entry.Path, Utf8PathComparer.Instance))
        {
            var pathBytes = Encoding.UTF8.GetBytes(entry.Path);
            BinaryPrimitives.WriteUInt32BigEndian(pathLength, checked((uint)pathBytes.Length));
            hash.AppendData(pathLength);
            hash.AppendData(pathBytes);
            BinaryPrimitives.WriteUInt64BigEndian(contentLength, checked((ulong)entry.Length));
            hash.AppendData(contentLength);
            using var stream = entry.OpenRead() ??
                               throw new ContentPackageException($"Hash source '{entry.Path}' returned null.");
            AppendContent(hash, stream, entry.Length, entry.Path);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendContent(IncrementalHash hash, Stream stream, long expectedLength, string path)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        try
        {
            long total = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > expectedLength)
                {
                    throw new ContentPackageException($"Hash source '{path}' exceeds its declared length.");
                }

                hash.AppendData(buffer, 0, read);
            }

            if (total != expectedLength)
            {
                throw new ContentPackageException($"Hash source '{path}' does not match its declared length.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
