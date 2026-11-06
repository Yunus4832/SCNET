using System.Text;
using System.Text.Json;

using Content.Packaging;

using Game.Modding;

namespace Survivalcraft.Test.Modding;

internal static class ScpkgTestPackage
{
    public static MemoryStream Create(
        string manifestJson,
        IReadOnlyDictionary<string, string>? files = null,
        string? assemblyPath = null
    )
    {
        var definition = ModManifest.Parse(manifestJson);
        var identifier = definition.Id;
        var dependencies = definition.RequiredDependencies.Select(dependency => new
            {
                identifier = dependency.Id,
                minimumVersion = dependency.MinimumVersion ?? "0.0.0",
                optional = dependency.Optional
            }
        ).ToArray();
        var metadata = JsonSerializer.SerializeToElement(
            new Dictionary<string, object?>
            {
                ["entrypoint"] = definition.Entrypoint,
                ["clientContentEntrypoint"] = definition.ClientContentEntrypoint,
                ["dependencies"] = dependencies
            }
        );
        var manifest = new ContentPackageManifest(
            ContentPackageManifest.CurrentFormatVersion,
            ContentPackageType.Mod,
            identifier,
            definition.Name,
            "Test summary",
            "Test package description",
            definition.Version,
            new ContentPackagePayload("scnet.mod-v1", "payload/mod.json", "application/json"),
            metadata
        );
        var entries = new List<ContentPackageWriteEntry>
        {
            FromBytes("payload/mod.json", "{\"formatVersion\":1}"u8.ToArray())
        };
        if (assemblyPath is not null)
        {
            entries.Add(
                new ContentPackageWriteEntry(
                    $"payload/assemblies/{Path.GetFileName(assemblyPath)}",
                    new FileInfo(assemblyPath).Length,
                    () => File.OpenRead(assemblyPath)
                )
            );
        }

        if (files is not null)
        {
            entries.AddRange(
                files.Select(pair =>
                    FromBytes($"payload/{pair.Key}", Encoding.UTF8.GetBytes(pair.Value))
                )
            );
        }

        var output = new MemoryStream();
        ContentPackageWriter.Write(output, manifest, entries);
        output.Position = 0;
        return output;
    }

    private static ContentPackageWriteEntry FromBytes(string path, byte[] bytes) =>
        new(path, bytes.Length, () => new MemoryStream(bytes, writable: false));
}
