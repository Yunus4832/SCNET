using System.IO.Compression;
using System.Text;

namespace Content.Packaging.Test;

public sealed class ContentPackageReaderTest
{
    [Fact]
    public void LogicalHashIgnoresZipOrderAndCompression()
    {
        using var first = CreateModPackage(CompressionLevel.NoCompression, false);
        using var second = CreateModPackage(CompressionLevel.Optimal, true);

        var firstInspection = ContentPackageReader.Inspect(first);
        var secondInspection = ContentPackageReader.Inspect(second);

        Assert.Equal(firstInspection.PackageHash, secondInspection.PackageHash);
        Assert.Equal(ContentPackageType.Mod, firstInspection.Manifest.Type);
        Assert.Equal("example.test", firstInspection.Manifest.Identifier);
    }

    [Fact]
    public void PublicPackageHashVectorMatchesProtocol()
    {
        var manifest = "{}"u8.ToArray();
        var payload = "hello"u8.ToArray();
        var hash = ContentPackageHash.Compute([
            HashEntry("payload/a.txt", payload),
            HashEntry("manifest.json", manifest)
        ]);

        Assert.Equal("5b41dca579cbbcf86944e71360af4baef1b5a480de07159a2bb51c519a963fb5", hash);
    }

    [Fact]
    public void GoldenModSourceProducesPublishedPackageHash()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Assets", "GoldenMod");
        var payloadRoot = Path.Combine(root, "payload");
        var manifest = ContentPackageManifest.Parse(File.ReadAllBytes(Path.Combine(root, "manifest.json")));
        var entries = Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories)
            .Select(path => new ContentPackageWriteEntry(
                $"payload/{Path.GetRelativePath(payloadRoot, path).Replace('\\', '/')}",
                new FileInfo(path).Length,
                () => File.OpenRead(path)))
            .ToArray();
        using var package = new MemoryStream();

        var hash = ContentPackageWriter.Write(package, manifest, entries);

        Assert.Equal("3f6d65a916b78a55ab6bab6a2c246888b4f7aa41913eff3c0d3c882bd6263a9a", hash);
    }

    [Fact]
    public void PackageHashSortsPathsByUtf8BytesRatherThanUtf16CodeUnits()
    {
        var hash = ContentPackageHash.Compute([
            HashEntry("payload/\U00010000.txt", "B"u8.ToArray()),
            HashEntry("payload/\ue000.txt", "A"u8.ToArray())
        ]);

        Assert.Equal("7c759cd7448ef42962c80391449514052c26b91f5c7bb5687325a77291b42d56", hash);
    }

    [Fact]
    public void LogicalHashChangesWhenPayloadChanges()
    {
        using var first = CreateModPackage(CompressionLevel.Optimal, false, "first");
        using var second = CreateModPackage(CompressionLevel.Optimal, false, "second");

        Assert.NotEqual(ContentPackageReader.Inspect(first).PackageHash,
            ContentPackageReader.Inspect(second).PackageHash);
    }

    [Fact]
    public void RejectsCaseAmbiguousPaths()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "manifest.json", _modManifest);
            WriteEntry(archive, "payload/mod.json", "{\"formatVersion\":1}");
            WriteEntry(archive, "payload/data/Items.txt", "first");
            WriteEntry(archive, "payload/data/items.txt", "second");
        }

        stream.Position = 0;
        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageReader.Inspect(stream));
        Assert.Contains("case-ambiguous", exception.Message);
    }

    [Fact]
    public void RejectsDuplicateManifestEntries()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "manifest.json", _modManifest);
            WriteEntry(archive, "manifest.json", _modManifest);
            WriteEntry(archive, "payload/mod.json", "{\"formatVersion\":1}");
            WriteEntry(archive, "payload/data/example.txt", "data");
        }

        stream.Position = 0;

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageReader.Inspect(stream));
        Assert.Contains("duplicate", exception.Message);
    }

    [Fact]
    public void RejectsMissingManifest()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "payload/mod.json", "{\"formatVersion\":1}");
        }

        stream.Position = 0;

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageReader.Inspect(stream));
        Assert.Contains("does not contain manifest", exception.Message);
    }

    [Fact]
    public void RejectsSymbolicLinkEntry()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "manifest.json", _modManifest);
            WriteEntry(archive, "payload/mod.json", "{\"formatVersion\":1}");
            var link = archive.CreateEntry("payload/data/link");
            link.ExternalAttributes = unchecked((int)0xa1ff0000);
            using var writer = new StreamWriter(link.Open(), new UTF8Encoding(false));
            writer.Write("target");
        }

        stream.Position = 0;

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageReader.Inspect(stream));
        Assert.Contains("Symbolic link", exception.Message);
    }

    [Theory]
    [InlineData("payload/../escape.txt")]
    [InlineData("/payload/escape.txt")]
    [InlineData("payload\\escape.txt")]
    public void RejectsDangerousPaths(string path)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "manifest.json", _modManifest);
            WriteEntry(archive, "payload/mod.json", "{\"formatVersion\":1}");
            WriteEntry(archive, path, "data");
        }

        stream.Position = 0;

        Assert.Throws<ContentPackageException>(() => ContentPackageReader.Inspect(stream));
    }

    [Fact]
    public void RejectsNestedArchivePayloadBySignature()
    {
        byte[] nested;
        using (var nestedStream = new MemoryStream())
        {
            using (var nestedArchive = new ZipArchive(nestedStream, ZipArchiveMode.Create, true))
            {
                WriteEntry(nestedArchive, "file.txt", "nested");
            }

            nested = nestedStream.ToArray();
        }

        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(_modManifest));
        using var stream = new MemoryStream();

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageWriter.Write(stream, manifest,
            [Entry("payload/mod.json", "{\"formatVersion\":1}"), Entry("payload/data/disguised.bin", nested)]));

        Assert.Contains("Nested archive", exception.Message);
    }

    [Fact]
    public void RejectsCompressionBombRatio()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "manifest.json", _modManifest);
            WriteEntry(archive, "payload/mod.json", "{\"formatVersion\":1}");
            var entry = archive.CreateEntry("payload/data/zeros.bin", CompressionLevel.SmallestSize);
            using var output = entry.Open();
            output.Write(new byte[1024 * 1024]);
        }

        stream.Position = 0;

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageReader.Inspect(stream));
        Assert.Contains("compression ratio", exception.Message);
    }

    [Fact]
    public void RejectsUnknownManifestProperty()
    {
        using var stream = CreateModPackage(CompressionLevel.Optimal, false,
            manifest: _modManifest.Replace("\n}", ",\n  \"unexpected\": true\n}"));

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageReader.Inspect(stream));
        Assert.Contains("unknown property", exception.Message);
    }

    [Fact]
    public void RejectsDuplicateManifestProperty()
    {
        using var stream = CreateModPackage(CompressionLevel.Optimal, false,
            manifest: _modManifest.Replace("\"version\": \"1.0.0\",",
                "\"version\": \"1.0.0\",\n  \"version\": \"2.0.0\","));

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageReader.Inspect(stream));
        Assert.Contains("duplicate property", exception.Message);
    }

    [Fact]
    public void InvalidModPayloadJsonUsesProtocolException()
    {
        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(_modManifest));
        using var stream = new MemoryStream();

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageWriter.Write(stream, manifest,
            [Entry("payload/mod.json", "{"), Entry("payload/data/example.txt", "data")]));

        Assert.Contains("invalid JSON", exception.Message);
    }

    [Fact]
    public void WriterProducesPackageAcceptedByReaderWithTheSameHash()
    {
        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(_modManifest));
        var data = "writer contribution"u8.ToArray();
        var mod = "{\"formatVersion\":1}"u8.ToArray();
        using var stream = new MemoryStream();

        var writtenHash = ContentPackageWriter.Write(stream, manifest,
        [
            new ContentPackageWriteEntry(
                "payload/mod.json",
                mod.Length,
                () => new MemoryStream(mod, writable: false)
            ),
            new ContentPackageWriteEntry(
                "payload/data/example.txt",
                data.Length,
                () => new MemoryStream(data, writable: false)
            )
        ]);

        stream.Position = 0;
        var inspection = ContentPackageReader.Inspect(stream);
        Assert.Equal(writtenHash, inspection.PackageHash);
    }

    [Theory]
    [MemberData(nameof(GoldenPackages))]
    public void GoldenPackageRoundTripsThroughWriterAndReader(string manifestJson, ContentPackageType type,
        string expectedHash,
        ContentPackageWriteEntry[] entries)
    {
        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(manifestJson));
        using var stream = new MemoryStream();

        var writtenHash = ContentPackageWriter.Write(stream, manifest, entries);

        stream.Position = 0;
        var inspection = ContentPackageReader.Inspect(stream);
        Assert.Equal(type, inspection.Manifest.Type);
        Assert.Equal(expectedHash, inspection.PackageHash);
        Assert.Equal(writtenHash, inspection.PackageHash);
    }

    [Fact]
    public void RejectsPngWithInvalidChunkCrc()
    {
        var image = _onePixelPng.ToArray();
        image[^1] ^= 1;
        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(
            ImageManifest("blocksTexture", "payload/texture.png", "scnet.blocks-texture.png-v1",
                "d24bfcc2-5f12-4956-9d91-0cbe5d5b224a")));
        using var stream = new MemoryStream();
        var exception = Assert.Throws<ContentPackageException>(() =>
            ContentPackageWriter.Write(stream, manifest, [Entry("payload/texture.png", image)]));
        Assert.Contains("CRC", exception.Message);
    }

    [Fact]
    public void RejectsImageDimensionsThatDisagreeWithMetadata()
    {
        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(
            ImageManifest("characterSkin", "payload/skin.png", "scnet.character-skin.png-v1",
                "1a562cd5-d5f8-4bc7-ac74-c13a8017856f").Replace("\"width\": 1", "\"width\": 2")));
        using var stream = new MemoryStream();

        var exception = Assert.Throws<ContentPackageException>(() =>
            ContentPackageWriter.Write(stream, manifest, [Entry("payload/skin.png", _onePixelPng)]));

        Assert.Contains("dimensions", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsWorldFilesOutsideCanonicalLayout()
    {
        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes("""
                                                                           {
                                                                             "formatVersion": 1,
                                                                             "type": "world",
                                                                             "identifier": "0f46af8f-134f-4b52-9397-c43ecbcd79d7",
                                                                             "name": "World",
                                                                             "version": "1.0.0",
                                                                             "payload": { "format": "scnet.world-v1", "entry": "payload/world/Project.xml", "mediaType": "application/xml" },
                                                                             "metadata": { "projectFormat": "scnet-project-xml-v1", "regionsDirectory": "payload/world/Regions" }
                                                                           }
                                                                           """));
        using var stream = new MemoryStream();

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageWriter.Write(stream, manifest,
        [
            Entry("payload/world/Project.xml",
                "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\"><Subsystems /><Entities /></Project>"),
            Entry("payload/world/notes.txt", "not canonical")
        ]));

        Assert.Contains("World payload path", exception.Message);
    }

    [Fact]
    public void RejectsFurnitureDesignWithoutCanonicalNumericIndex()
    {
        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes("""
                                                                           {
                                                                             "formatVersion": 1,
                                                                             "type": "furniturePack",
                                                                             "identifier": "1d5700c1-85da-41e2-b9f0-d0422e2f7937",
                                                                             "name": "Furniture",
                                                                             "version": "1.0.0",
                                                                             "payload": { "format": "scnet.furniture-designs-xml-v1", "entry": "payload/furniture/FurnitureDesigns.xml", "mediaType": "application/xml" },
                                                                             "metadata": { "designCount": 1 }
                                                                           }
                                                                           """));
        using var stream = new MemoryStream();

        var exception = Assert.Throws<ContentPackageException>(() => ContentPackageWriter.Write(stream, manifest,
        [
            Entry("payload/furniture/FurnitureDesigns.xml",
                "<FurnitureDesigns><Values Name=\"01\" /></FurnitureDesigns>")
        ]));

        Assert.Contains("invalid design", exception.Message);
    }

    [Fact]
    public void RejectsPngWhoseEncodedPixelsCannotBeDecodedEvenWhenChunkCrcIsValid()
    {
        var image = _onePixelPng.ToArray();
        var idatOffset = FindPngChunk(image, "IDAT"u8);
        var dataLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(
            image.AsSpan(idatOffset, 4));
        image[idatOffset + 8] ^= 0xff;
        var crc = ComputeCrc32(image.AsSpan(idatOffset + 4, 4 + dataLength));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            image.AsSpan(idatOffset + 8 + dataLength, 4), crc);
        var manifest = ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(
            ImageManifest("blocksTexture", "payload/texture.png", "scnet.blocks-texture.png-v1",
                "4a6e0ea4-c9eb-4916-9f30-844e17099323")));

        using var stream = new MemoryStream();
        Assert.Throws<ContentPackageException>(() =>
            ContentPackageWriter.Write(stream, manifest, [Entry("payload/texture.png", image)]));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0+build")]
    public void RejectsInvalidOrUnsupportedSemVer(string version)
    {
        var json = _modManifest.Replace("\"version\": \"1.0.0\"", $"\"version\": \"{version}\"");
        Assert.Throws<ContentPackageException>(() =>
            ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void RejectsNonNormalizedDisplayName()
    {
        var json = _modManifest.Replace("Example Test", "Cafe\u0301");
        Assert.Throws<ContentPackageException>(() =>
            ContentPackageManifest.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-beta.11", "1.0.0-rc.1")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("999999999999999999999.0.0", "1000000000000000000000.0.0")]
    public void SemanticVersionUsesSemVerPrecedence(string lower, string higher)
    {
        Assert.True(SemanticVersion.Parse(lower) < SemanticVersion.Parse(higher));
    }

    public static TheoryData<string, ContentPackageType, string, ContentPackageWriteEntry[]> GoldenPackages()
    {
        var data = new TheoryData<string, ContentPackageType, string, ContentPackageWriteEntry[]>
        {
            {
                _modManifest, ContentPackageType.Mod,
                "b3915f3d63c4f78b7c924ebbb38b487d508c3cb2c22b741d49f920255733fc83", [
                    Entry("payload/mod.json", "{\"formatVersion\":1}"),
                    Entry("payload/data/example.txt", "golden mod data")
                ]
            },
            {
                """
                {
                  "formatVersion": 1,
                  "type": "world",
                  "identifier": "7e96a4f3-fae8-4727-9fcb-9d35c2fc08b6",
                  "name": "Golden World",
                  "version": "1.0.0",
                  "payload": { "format": "scnet.world-v1", "entry": "payload/world/Project.xml", "mediaType": "application/xml" },
                  "metadata": { "projectFormat": "scnet-project-xml-v1", "regionsDirectory": "payload/world/Regions" }
                }
                """,
                ContentPackageType.World,
                "722ee264e95bb2477a2f482b87e86d1f7ae3356ea2b6d4c7495079cdaba6df8a", [
                    Entry("payload/world/Project.xml",
                        "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\"><Subsystems /><Entities /></Project>"),
                    Entry("payload/world/Regions/0,0.dat", "region")
                ]
            },
            {
                ImageManifest("blocksTexture", "payload/texture.png", "scnet.blocks-texture.png-v1",
                    "1f8f7ff4-923e-45f5-9d0e-10b1ed44b0fa"),
                ContentPackageType.BlocksTexture,
                "9b5714e3e6b09ee2214484ba21d29c430fef23356c36c3a2c1bcb03a77da899f",
                [Entry("payload/texture.png", _onePixelPng)]
            },
            {
                ImageManifest("characterSkin", "payload/skin.png", "scnet.character-skin.png-v1",
                    "af7db01d-9225-4b90-83d5-967d57e58b7a"),
                ContentPackageType.CharacterSkin,
                "23293b6b083b4bb73bbc741911f89d131be0a7d5b697619536a4a55d2bcdf213",
                [Entry("payload/skin.png", _onePixelPng)]
            },
            {
                """
                {
                  "formatVersion": 1,
                  "type": "furniturePack",
                  "identifier": "a11bf107-f0d0-442f-94f2-c0ff1a2a8794",
                  "name": "Golden Furniture",
                  "version": "1.0.0",
                  "payload": { "format": "scnet.furniture-designs-xml-v1", "entry": "payload/furniture/FurnitureDesigns.xml", "mediaType": "application/xml" },
                  "metadata": { "designCount": 1 }
                }
                """,
                ContentPackageType.FurniturePack,
                "7e877a868154ba31715a59120c93b7d380263d8eda42c6ce44d0c0ab818f0068", [
                    Entry("payload/furniture/FurnitureDesigns.xml",
                        "<FurnitureDesigns><Values Name=\"0\" /></FurnitureDesigns>")
                ]
            }
        };
        return data;
    }

    private static MemoryStream CreateModPackage(CompressionLevel compression, bool reverseOrder, string data = "value",
        string manifest = _modManifest)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entries = new (string Path, string Contents)[]
            {
                ("manifest.json", manifest),
                ("payload/mod.json", "{\"formatVersion\":1}"),
                ("payload/data/example.txt", data)
            };
            foreach (var entry in reverseOrder ? entries.Reverse() : entries)
            {
                WriteEntry(archive, entry.Path, entry.Contents, compression,
                    reverseOrder
                        ? new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                        : new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string path, string contents,
        CompressionLevel compression = CompressionLevel.Optimal,
        DateTimeOffset? timestamp = null)
    {
        var entry = archive.CreateEntry(path, compression);
        if (timestamp is not null)
        {
            entry.LastWriteTime = timestamp.Value;
        }

        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }

    private static ContentPackageWriteEntry Entry(string path, string contents) =>
        Entry(path, Encoding.UTF8.GetBytes(contents));

    private static ContentPackageWriteEntry Entry(string path, byte[] contents) =>
        new(path, contents.Length, () => new MemoryStream(contents, writable: false));

    private static ContentPackageHashEntry HashEntry(string path, byte[] contents) =>
        new(path, contents.Length, () => new MemoryStream(contents, writable: false));

    private static int FindPngChunk(byte[] png, ReadOnlySpan<byte> type)
    {
        var offset = 8;
        while (offset + 12 <= png.Length)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            if (png.AsSpan(offset + 4, 4).SequenceEqual(type))
            {
                return offset;
            }

            offset += 12 + length;
        }

        throw new InvalidOperationException("PNG chunk was not found.");
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
            }
        }

        return ~crc;
    }

    private static string ImageManifest(string type, string entry, string format, string identifier) => $$"""
          {
            "formatVersion": 1,
            "type": "{{type}}",
            "identifier": "{{identifier}}",
            "name": "Golden {{type}}",
            "version": "1.0.0",
            "payload": { "format": "{{format}}", "entry": "{{entry}}", "mediaType": "image/png" },
            "metadata": { "width": 1, "height": 1 }
          }
          """;

    private static readonly byte[] _onePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR4nGP4DwQACfsD/fteaysAAAAASUVORK5CYII=");

    private const string _modManifest = """
                                        {
                                          "formatVersion": 1,
                                          "type": "mod",
                                          "identifier": "example.test",
                                          "name": "Example Test",
                                          "version": "1.0.0",
                                          "payload": {
                                            "format": "scnet.mod-v1",
                                            "entry": "payload/mod.json",
                                            "mediaType": "application/json"
                                          },
                                          "metadata": {
                                            "side": "common",
                                            "entrypoints": {},
                                            "dependencies": []
                                          }
                                        }
                                        """;
}
