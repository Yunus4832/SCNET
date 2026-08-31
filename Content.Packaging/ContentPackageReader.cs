using System.Buffers;
using System.IO.Compression;
using System.Text;

using Content.Packaging.Payloads;

namespace Content.Packaging;

public static class ContentPackageReader
{
    public const string FileExtension = ".scpkg";
    public const int MaxManifestBytes = 64 * 1024;
    public const int MaxFileCount = 10_000;
    public const long MaxEntryBytes = 128L * 1024 * 1024;
    public const long MaxTotalBytes = 200L * 1024 * 1024;
    private const int _copyBufferSize = 64 * 1024;

    public static ContentPackageInspection Inspect(
        Stream stream,
        ContentPayloadCodecRegistry? payloadCodecs = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entries = ReadEntries(archive);
            var manifestEntry = entries.SingleOrDefault(entry => entry.Path == "manifest.json")
                                ?? throw new ContentPackageException("Package does not contain manifest.json.");
            var manifestBytes = ReadManifest(manifestEntry.Entry);
            var manifest = ContentPackageManifest.Parse(manifestBytes);
            var entriesByPath = entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
            var context = new ContentPayloadValidationContext(
                manifest,
                entriesByPath.Keys.ToHashSet(StringComparer.Ordinal),
                path => entriesByPath[path].Entry.Open());
            (payloadCodecs ?? ContentPayloadCodecRegistry.Default).Get(manifest.Type).Validate(context);
            var hash = ContentPackageHash.Compute(entries.Select(entry => new ContentPackageHashEntry(
                entry.Path,
                entry.Length,
                entry.Path == "manifest.json"
                    ? () => new MemoryStream(manifestBytes, writable: false)
                    : entry.Entry.Open)));
            return new ContentPackageInspection(manifest, hash,
                entries.Select(entry => new ContentPackageEntry(entry.Path, entry.Length)).ToArray());
        }
        catch (InvalidDataException exception)
        {
            throw new ContentPackageException("Package is not a valid ZIP archive.", exception);
        }
    }

    private static List<ZipEntry> ReadEntries(ZipArchive archive)
    {
        var result = new List<ZipEntry>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var caseInsensitivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                throw new ContentPackageException("Directory ZIP entries are not allowed.");
            }

            var unixFileType = (entry.ExternalAttributes >> 16) & 0xf000;
            if (unixFileType == 0xa000)
            {
                throw new ContentPackageException($"Symbolic link ZIP entry '{entry.FullName}' is not allowed.");
            }

            var path = ValidatePath(entry.FullName);
            if (!paths.Add(path) || !caseInsensitivePaths.Add(path))
            {
                throw new ContentPackageException($"Package contains duplicate or case-ambiguous path '{path}'.");
            }

            if (entry.Length > MaxEntryBytes)
            {
                throw new ContentPackageException($"Package entry '{path}' exceeds the {MaxEntryBytes} byte limit.");
            }

            if (entry.CompressedLength > 0 && entry.Length > entry.CompressedLength * 200)
            {
                throw new ContentPackageException($"Package entry '{path}' exceeds the compression ratio limit.");
            }

            if (path != "manifest.json" && HasArchiveSignature(entry))
            {
                throw new ContentPackageException($"Nested archive payload '{path}' is not allowed.");
            }

            checked
            {
                totalBytes += entry.Length;
            }

            if (totalBytes > MaxTotalBytes)
            {
                throw new ContentPackageException($"Package exceeds the {MaxTotalBytes} byte uncompressed limit.");
            }

            result.Add(new ZipEntry(path, entry));
            if (result.Count > MaxFileCount)
            {
                throw new ContentPackageException($"Package exceeds the {MaxFileCount} file limit.");
            }
        }

        if (result.Count == 0)
        {
            throw new ContentPackageException("Package does not contain files.");
        }

        return result;
    }

    private static bool HasArchiveSignature(ZipArchiveEntry entry)
    {
        if (entry.Length < 4)
        {
            return false;
        }

        Span<byte> signature = stackalloc byte[4];
        using var stream = entry.Open();
        var read = 0;
        while (read < signature.Length)
        {
            var count = stream.Read(signature[read..]);
            if (count == 0)
            {
                return false;
            }
            read += count;
        }

        return signature is [0x50, 0x4b, 0x03, 0x04] or
            [0x50, 0x4b, 0x05, 0x06] or
            [0x50, 0x4b, 0x07, 0x08];
    }

    private static byte[] ReadManifest(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxManifestBytes)
        {
            throw new ContentPackageException("manifest.json exceeds the 64 KiB limit.");
        }

        using var stream = entry.Open();
        using var memory = new MemoryStream((int)entry.Length);
        CopyExact(stream, memory, entry.Length, "manifest.json");
        var bytes = memory.ToArray();
        if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
        {
            throw new ContentPackageException("manifest.json must be UTF-8 without BOM.");
        }

        return bytes;
    }

    private static string ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains('\0') || Encoding.UTF8.GetByteCount(path) > 240 || !path.IsNormalized())
        {
            throw new ContentPackageException($"Package path '{path}' is invalid.");
        }

        var segments = path.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".." || segment.Any(char.IsControl)) ||
            (path != "manifest.json" && !path.StartsWith("payload/", StringComparison.Ordinal)))
        {
            throw new ContentPackageException($"Package path '{path}' is invalid.");
        }

        return path;
    }

    private static void CopyExact(Stream input, Stream output, long expectedLength, string path)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(_copyBufferSize);
        try
        {
            long total = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > expectedLength)
                {
                    throw new ContentPackageException($"Package entry '{path}' exceeds its declared size.");
                }

                output.Write(buffer, 0, read);
            }

            if (total != expectedLength)
            {
                throw new ContentPackageException($"Package entry '{path}' has an unexpected size.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed record ZipEntry(string Path, ZipArchiveEntry Entry)
    {
        public long Length => Entry.Length;
    }
}
