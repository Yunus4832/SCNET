using System.Text.Json;
using System.Text.Json.Serialization;

using Content.Packaging;

namespace Game.Modding;

public sealed record ModDependency(string Id, string? MinimumVersion = null, bool Optional = false);

public sealed record ModManifest(
    string Id,
    string Name,
    string Version,
    IReadOnlyList<ModDependency>? Dependencies = null,
    string? Entrypoint = null,
    string? ClientContentEntrypoint = null,
    [property: JsonRequired] bool CanRemoveFromWorld = false,
    [property: JsonRequired] ModScope Scope = ModScope.Both
)
{
    public ModId ModId => new(Id);

    public SemanticVersion ParsedVersion => SemanticVersion.TryParse(Version, out var version)
        ? version
        : throw new InvalidOperationException($"Mod {Id} has invalid version \"{Version}\".");

    public IReadOnlyList<ModDependency> RequiredDependencies => Dependencies ?? [];

    public static ModManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<ModManifest>(json, _jsonOptions) ??
                       throw new InvalidOperationException("Mod manifest is empty.");
        manifest.Validate();
        return manifest;
    }

    public void Validate()
    {
        _ = ModId;
        _ = ParsedVersion;
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (!Enum.IsDefined(Scope))
        {
            throw new InvalidOperationException($"Mod {Id} has invalid scope '{Scope}'.");
        }

        if (Entrypoint != null) { ArgumentException.ThrowIfNullOrWhiteSpace(Entrypoint); }

        if (ClientContentEntrypoint != null) { ArgumentException.ThrowIfNullOrWhiteSpace(ClientContentEntrypoint); }

        if (Scope == ModScope.Client && Entrypoint is not null)
        {
            throw new InvalidOperationException($"Client mod {Id} cannot declare an IMod entrypoint.");
        }

        if (Scope == ModScope.Server && ClientContentEntrypoint is not null)
        {
            throw new InvalidOperationException($"Server mod {Id} cannot declare a client content entrypoint.");
        }

        var dependencyIds = new HashSet<ModId>();
        foreach (var dependency in RequiredDependencies)
        {
            var dependencyId = new ModId(dependency.Id);
            if (dependencyId == ModId)
            {
                throw new InvalidOperationException($"Mod {Id} cannot depend on itself.");
            }

            if (!dependencyIds.Add(dependencyId))
            {
                throw new InvalidOperationException($"Mod {Id} declares dependency {dependencyId} more than once.");
            }

            if (dependency.MinimumVersion is not null && !SemanticVersion.TryParse(dependency.MinimumVersion, out _))
            {
                throw new InvalidOperationException(
                    $"Mod {Id} has invalid minimum version \"{dependency.MinimumVersion}\" for {dependencyId}."
                );
            }
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(
            new JsonStringEnumConverter<ModScope>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        );
        return options;
    }
}
