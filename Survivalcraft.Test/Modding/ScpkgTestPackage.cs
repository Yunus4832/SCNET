using System.Text;
using System.Text.Json;

using Content.Packaging;

namespace Survivalcraft.Test.Modding;

internal static class ScpkgTestPackage
{
    public static MemoryStream Create(
        string legacyManifestJson,
        IReadOnlyDictionary<string, string>? files = null,
        string? assemblyPath = null)
    {
        using var legacyDocument = JsonDocument.Parse(legacyManifestJson);
        var legacy = legacyDocument.RootElement;
        var identifier = legacy.GetProperty("id").GetString()!;
        var entrypoints = legacy.TryGetProperty("entrypoints", out var entrypointsElement)
            ? entrypointsElement.EnumerateObject().ToDictionary(
                property => property.Name,
                property => (object)property.Value.GetString()!,
                StringComparer.Ordinal)
            : new Dictionary<string, object>(StringComparer.Ordinal);
        var dependencies = legacy.TryGetProperty("dependencies", out var dependenciesElement)
            ? dependenciesElement.EnumerateArray().Select(dependency => new Dictionary<string, object>
            {
                ["identifier"] = dependency.GetProperty("id").GetString()!,
                ["minimumVersion"] = dependency.TryGetProperty("minimumVersion", out var minimumVersion)
                    ? minimumVersion.GetString()!
                    : "0.0.0",
                ["optional"] = dependency.TryGetProperty("optional", out var optional) && optional.GetBoolean()
            }).ToArray()
            : [];
        var metadata = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["side"] = legacy.TryGetProperty("side", out var side) ? side.GetString()! : "common",
            ["entrypoints"] = entrypoints,
            ["dependencies"] = dependencies
        });
        var manifest = new ContentPackageManifest(
            ContentPackageManifest.CurrentFormatVersion,
            ContentPackageType.Mod,
            identifier,
            legacy.GetProperty("name").GetString()!,
            legacy.GetProperty("version").GetString()!,
            new ContentPackagePayload("scnet.mod-v1", "payload/mod.json", "application/json"),
            metadata);
        var entries = new List<ContentPackageWriteEntry>
        {
            FromBytes("payload/mod.json", "{\"formatVersion\":1}"u8.ToArray())
        };
        if (assemblyPath is not null)
        {
            entries.Add(new ContentPackageWriteEntry(
                $"payload/assemblies/{Path.GetFileName(assemblyPath)}",
                new FileInfo(assemblyPath).Length,
                () => File.OpenRead(assemblyPath)));
        }

        if (files is not null)
        {
            entries.AddRange(files.Select(pair =>
                FromBytes($"payload/{pair.Key}", Encoding.UTF8.GetBytes(pair.Value))));
        }

        var output = new MemoryStream();
        ContentPackageWriter.Write(output, manifest, entries);
        output.Position = 0;
        return output;
    }

    private static ContentPackageWriteEntry FromBytes(string path, byte[] bytes) =>
        new(path, bytes.Length, () => new MemoryStream(bytes, writable: false));
}
