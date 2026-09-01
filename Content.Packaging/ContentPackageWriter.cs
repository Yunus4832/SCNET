using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Content.Packaging;

public sealed record ContentPackageWriteEntry(string Path, long Length, Func<Stream> OpenRead);

public static class ContentPackageWriter
{
    private const int _copyBufferSize = 64 * 1024;

    public static string Write(
        Stream destination,
        ContentPackageManifest manifest,
        IEnumerable<ContentPackageWriteEntry> payloadEntries)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(payloadEntries);

        if (!destination.CanRead || !destination.CanWrite || !destination.CanSeek)
        {
            throw new ArgumentException("Content package destination must be readable, writable and seekable.",
                nameof(destination));
        }

        destination.Position = 0;
        destination.SetLength(0);
        var entries = payloadEntries.OrderBy(entry => entry.Path, Utf8PathComparer.Instance).ToArray();
        var manifestBytes = SerializeManifest(manifest);
        ValidateEntries(entries, manifestBytes.Length);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "manifest.json", manifestBytes);

        using (var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "manifest.json", manifestBytes, CompressionLevel.Optimal);
            foreach (var entry in entries)
            {
                AppendHeader(hash, entry.Path, entry.Length);
                var compression = entry.Path.EndsWith(".png", StringComparison.Ordinal)
                    ? CompressionLevel.NoCompression
                    : CompressionLevel.Optimal;
                var zipEntry = archive.CreateEntry(entry.Path, compression);
                using var source = entry.OpenRead() ??
                                   throw new ContentPackageException($"Payload source '{entry.Path}' returned null.");
                using var target = zipEntry.Open();
                CopyAndHash(source, target, hash, entry.Length, entry.Path);
            }
        }

        var expectedHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        var endPosition = destination.Position;
        destination.Position = 0;
        var inspection = ContentPackageReader.Inspect(destination);
        if (!string.Equals(expectedHash, inspection.PackageHash, StringComparison.Ordinal))
        {
            throw new ContentPackageException("Writer and Reader produced different PackageHash values.");
        }

        destination.Position = endPosition;
        return expectedHash;
    }

    public static byte[] SerializeManifest(ContentPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        using var output = new MemoryStream();
        using (var writer =
               new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", manifest.FormatVersion);
            writer.WriteString("type", TypeToString(manifest.Type));
            writer.WriteString("identifier", manifest.Identifier);
            writer.WriteString("name", manifest.Name);
            writer.WriteString("version", manifest.Version);
            writer.WritePropertyName("payload");
            writer.WriteStartObject();
            writer.WriteString("format", manifest.Payload.Format);
            writer.WriteString("entry", manifest.Payload.Entry);
            writer.WriteString("mediaType", manifest.Payload.MediaType);
            writer.WriteEndObject();
            writer.WritePropertyName("metadata");
            manifest.Metadata.WriteTo(writer);
            writer.WriteEndObject();
        }

        var bytes = output.ToArray();
        _ = ContentPackageManifest.Parse(bytes);
        return bytes;
    }

    private static void ValidateEntries(IReadOnlyList<ContentPackageWriteEntry> entries, int manifestLength)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var caseInsensitivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalLength = manifestLength;
        if (entries.Count + 1 > ContentPackageReader.MaxFileCount)
        {
            throw new ContentPackageException("Package contains too many files.");
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Path) || !entry.Path.StartsWith("payload/", StringComparison.Ordinal) ||
                entry.Path.Contains('\\') || entry.Path.Split('/').Any(part => part is "" or "." or "..") ||
                !entry.Path.IsNormalized() || Encoding.UTF8.GetByteCount(entry.Path) > 240 ||
                entry.Path.Any(char.IsControl) ||
                entry.Length is < 0 or > ContentPackageReader.MaxEntryBytes || entry.OpenRead is null ||
                !paths.Add(entry.Path) || !caseInsensitivePaths.Add(entry.Path))
            {
                throw new ContentPackageException($"Payload write entry '{entry.Path}' is invalid.");
            }

            checked
            {
                totalLength += entry.Length;
            }

            if (totalLength > ContentPackageReader.MaxTotalBytes)
            {
                throw new ContentPackageException("Package exceeds the uncompressed size limit.");
            }
        }
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] bytes, CompressionLevel compression)
    {
        var entry = archive.CreateEntry(path, compression);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static void Append(IncrementalHash hash, string path, ReadOnlySpan<byte> content)
    {
        AppendHeader(hash, path, content.Length);
        hash.AppendData(content);
    }

    private static void AppendHeader(IncrementalHash hash, string path, long length)
    {
        Span<byte> pathLength = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(pathLength, checked((uint)Encoding.UTF8.GetByteCount(path)));
        hash.AppendData(pathLength);
        hash.AppendData(Encoding.UTF8.GetBytes(path));
        Span<byte> contentLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(contentLength, checked((ulong)length));
        hash.AppendData(contentLength);
    }

    private static void CopyAndHash(Stream source, Stream target, IncrementalHash hash, long expectedLength,
        string path)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(_copyBufferSize);
        try
        {
            long copied = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                copied += read;
                if (copied > expectedLength)
                {
                    throw new ContentPackageException($"Payload source '{path}' exceeds its declared length.");
                }

                target.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
            }

            if (copied != expectedLength)
            {
                throw new ContentPackageException($"Payload source '{path}' does not match its declared length.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string TypeToString(ContentPackageType type) => type switch
    {
        ContentPackageType.Mod => "mod",
        ContentPackageType.World => "world",
        ContentPackageType.BlocksTexture => "blocksTexture",
        ContentPackageType.CharacterSkin => "characterSkin",
        ContentPackageType.FurniturePack => "furniturePack",
        _ => throw new ContentPackageException("Unsupported content package type.")
    };
}
