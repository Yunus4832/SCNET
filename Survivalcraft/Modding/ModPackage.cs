using System.IO.Compression;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

using Content.Packaging;

using Game.Modding.Blocks;
using Game.Modding.Content;
using Game.Modding.Data;

namespace Game.Modding;

public sealed class ModPackage
{
    public const string FileExtension = ContentPackageReader.FileExtension;
    public const string SearchPattern = "*.scpkg";
    private readonly IReadOnlyDictionary<string, byte[]> _assemblies;
    private readonly IReadOnlyDictionary<string, byte[]> _dataFiles;
    private readonly IReadOnlyDictionary<string, byte[]> _assetFiles;

    private ModPackage(
        string source,
        ModManifest manifest,
        IReadOnlyDictionary<string, byte[]> assemblies,
        IReadOnlyDictionary<string, byte[]> dataFiles,
        IReadOnlyDictionary<string, byte[]> assetFiles,
        string packageHash)
    {
        Source = source;
        Manifest = manifest;
        _assemblies = assemblies;
        _dataFiles = dataFiles;
        _assetFiles = assetFiles;
        PackageHash = packageHash;
    }

    public string Source { get; }

    public ModManifest Manifest { get; }

    public string PackageHash { get; }

    public static ModPackage Read(string source, Stream stream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var inspection = ContentPackageReader.Inspect(stream);
            if (inspection.Manifest.Type != ContentPackageType.Mod)
            {
                throw new ContentPackageException("Content package is not a Mod package.");
            }

            var manifest = CreateRuntimeManifest(inspection.Manifest);
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
            var assemblies = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var dataFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var assetFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var assetPrefix = $"payload/assets/{manifest.Id}/";
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("payload/data/", StringComparison.Ordinal))
                {
                    dataFiles.Add(entry.FullName["payload/".Length..], ReadEntry(entry));
                }
                else if (entry.FullName.StartsWith(assetPrefix, StringComparison.Ordinal))
                {
                    assetFiles.Add(entry.FullName[assetPrefix.Length..], ReadEntry(entry));
                }
                else if (entry.FullName.StartsWith("payload/assemblies/", StringComparison.Ordinal))
                {
                    assemblies.Add(Path.GetFileNameWithoutExtension(entry.Name), ReadEntry(entry));
                }
            }

            return new ModPackage(source, manifest, assemblies, dataFiles, assetFiles, inspection.PackageHash);
        }
        catch (Exception exception) when (exception is ContentPackageException or InvalidDataException or JsonException)
        {
            throw new ModPackageException(source, exception.Message, exception);
        }
    }

    private static ModManifest CreateRuntimeManifest(ContentPackageManifest packageManifest)
    {
        var metadata = packageManifest.Metadata;
        var side = metadata.GetProperty("side").GetString() switch
        {
            "common" => ModSide.Common,
            "client" => ModSide.Client,
            "server" => ModSide.Server,
            _ => throw new ContentPackageException("Mod side is invalid.")
        };
        var entrypointsElement = metadata.GetProperty("entrypoints");
        var entrypoints = new ModEntrypoints(
            GetOptionalString(entrypointsElement, "common"),
            GetOptionalString(entrypointsElement, "client"),
            GetOptionalString(entrypointsElement, "server"));
        var dependencies = metadata.GetProperty("dependencies").EnumerateArray()
            .Select(dependency => new ModDependency(
                dependency.GetProperty("identifier").GetString()!,
                dependency.GetProperty("minimumVersion").GetString(),
                dependency.GetProperty("optional").GetBoolean()))
            .ToArray();
        return new ModManifest(packageManifest.Identifier, packageManifest.Name, packageManifest.Version,
            dependencies, side, entrypoints);
    }

    private static string? GetOptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var input = entry.Open();
        using var output = new MemoryStream(checked((int)entry.Length));
        input.CopyTo(output);
        return output.ToArray();
    }

    public ModDescriptor CreateDescriptor(ModSide hostSide)
    {
        if (Manifest.Side != ModSide.Common && Manifest.Side != hostSide)
        {
            throw new ModPackageException(Source, $"Mod side {Manifest.Side} cannot load on {hostSide}.");
        }

        var entrypoint = Manifest.Entrypoints?.GetFor(hostSide);
        if (string.IsNullOrWhiteSpace(entrypoint) && _dataFiles.Count == 0 && _assetFiles.Count == 0)
        {
            throw new ModPackageException(Source,
                $"Mod does not define an entrypoint or data contributions for {hostSide}.");
        }

        ModAssemblyLoadScope? lifetime = null;
        Func<IMod>? codeFactory = null;
        if (!string.IsNullOrWhiteSpace(entrypoint))
        {
            lifetime = new ModAssemblyLoadScope(Source, _assemblies, entrypoint);
            codeFactory = lifetime.CreateMod;
        }

        return new ModDescriptor(
            Manifest,
            () => new PackageMod(Manifest.ModId, _dataFiles, _assetFiles, codeFactory?.Invoke()),
            lifetime,
            PackageHash);
    }

}

internal sealed class PackageMod(
    ModId owner,
    IReadOnlyDictionary<string, byte[]> dataFiles,
    IReadOnlyDictionary<string, byte[]> assetFiles,
    IMod? codeMod) : IMod
{
    public void Configure(IModContext context)
    {
        foreach (var (path, bytes) in dataFiles.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var id = new ResourceId(owner, ToContributionPath(path));
            if (path.StartsWith("data/blocks/", StringComparison.Ordinal) &&
                path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                context.Extensions.RegisterBlockData(id, () => Encoding.UTF8.GetString(bytes));
            }
            else if (path.StartsWith("data/database/", StringComparison.Ordinal) &&
                     path.EndsWith(".xdb", StringComparison.OrdinalIgnoreCase))
            {
                context.Extensions.RegisterXmlData(
                    XmlDataExtensions.DatabaseRegistryName,
                    id,
                    XmlContributionMode.Patch,
                    () => XmlDataExtensions.ParseUtf8(bytes));
            }
            else if (path.StartsWith("data/recipes/", StringComparison.Ordinal) &&
                     path.EndsWith(".cr", StringComparison.OrdinalIgnoreCase))
            {
                context.Extensions.RegisterXmlData(
                    XmlDataExtensions.RecipeRegistryName,
                    id,
                    XmlContributionMode.Patch,
                    () => XmlDataExtensions.ParseUtf8(bytes));
            }
            else if (path.StartsWith("data/clothing/", StringComparison.Ordinal) &&
                     path.EndsWith(".clo", StringComparison.OrdinalIgnoreCase))
            {
                context.Extensions.RegisterXmlData(
                    XmlDataExtensions.ClothingRegistryName,
                    id,
                    XmlContributionMode.Patch,
                    () => XmlDataExtensions.ParseUtf8(bytes));
            }
        }

        foreach (var (relativePath, bytes) in assetFiles.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            context.Extensions.RegisterContent(
                new ResourceId(owner, relativePath),
                relativePath,
                bytes);
        }

        codeMod?.Configure(context);
    }

    public void Start(IModContext context) => codeMod?.Start(context);

    public void Stop() => codeMod?.Stop();

    private static string ToContributionPath(string path)
    {
        var relative = path["data/".Length..];
        var extension = Path.GetExtension(relative);
        return string.IsNullOrEmpty(extension) ? relative : relative[..^extension.Length];
    }
}

public static class ModPackageCatalog
{
    public static IReadOnlyList<ModPackage> Discover(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        var packages = new List<ModPackage>();
        foreach (var path in Directory
                     .EnumerateFiles(directoryPath, ModPackage.SearchPattern, SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(path);
            packages.Add(ModPackage.Read(path, stream));
        }

        return packages;
    }

    public static IReadOnlyList<ModPackage> Discover(IEnumerable<ModPackageSource> sources)
    {
        var packages = new List<ModPackage>();
        foreach (var source in sources.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            using var stream = source.OpenRead();
            packages.Add(ModPackage.Read(source.Name, stream));
        }

        return packages;
    }

    public static IReadOnlyList<ModDescriptor> CreateLoadPlan(string directoryPath, ModSide hostSide)
    {
        return CreateLoadPlan(Discover(directoryPath), hostSide);
    }

    public static IReadOnlyList<ModDescriptor> CreateLoadPlan(
        IEnumerable<ModPackageSource> sources,
        ModSide hostSide)
    {
        return CreateLoadPlan(Discover(sources), hostSide);
    }

    private static IReadOnlyList<ModDescriptor> CreateLoadPlan(
        IEnumerable<ModPackage> packages,
        ModSide hostSide)
    {
        var descriptors = packages
            .Select(package => package.CreateDescriptor(hostSide))
            .ToArray();
        try
        {
            return ModDependencyResolver.Resolve(descriptors);
        }
        catch
        {
            foreach (var descriptor in descriptors)
            {
                descriptor.Lifetime?.Dispose();
            }

            throw;
        }
    }
}

public sealed record ModPackageSource(string Name, Func<Stream> OpenRead);

public sealed class ModPackageException : Exception
{
    public ModPackageException(string source, string message, Exception? innerException = null)
        : base($"Mod package {source}: {message}", innerException)
    {
    }
}

internal sealed class ModAssemblyLoadScope : IDisposable
{
    private readonly string _entrypoint;
    private ModAssemblyLoadContext? _loadContext;
    private bool _created;

    public ModAssemblyLoadScope(string source, IReadOnlyDictionary<string, byte[]> assemblies, string entrypoint)
    {
        _entrypoint = entrypoint;
        _loadContext = new ModAssemblyLoadContext(source, assemblies);
    }

    public IMod CreateMod()
    {
        if (_created)
        {
            throw new InvalidOperationException("Mod entrypoint has already been created.");
        }

        var loadContext = _loadContext ?? throw new ObjectDisposedException(nameof(ModAssemblyLoadScope));
        var separator = _entrypoint.LastIndexOf(',');
        if (separator <= 0 || separator == _entrypoint.Length - 1)
        {
            throw new InvalidOperationException(
                $"Entrypoint \"{_entrypoint}\" must use the format \"Namespace.Type, AssemblyName\".");
        }

        var typeName = _entrypoint[..separator].Trim();
        var assemblyName = _entrypoint[(separator + 1)..].Trim();
        var assembly = loadContext.LoadModAssembly(assemblyName);
        var type = assembly.GetType(typeName, true, false)!;
        if (!typeof(IMod).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Entrypoint {type.FullName} does not implement {nameof(IMod)}.");
        }

        _created = true;
        return (IMod)(Activator.CreateInstance(type)
                      ?? throw new InvalidOperationException($"Could not create mod entrypoint {type.FullName}."));
    }

    public void Dispose()
    {
        var loadContext = Interlocked.Exchange(ref _loadContext, null);
        loadContext?.Unload();
    }
}

internal sealed class ModAssemblyLoadContext(
    string source,
    IReadOnlyDictionary<string, byte[]> assemblies
) : AssemblyLoadContext($"Mod:{source}", isCollectible: true)
{
    public Assembly LoadModAssembly(string assemblyName)
    {
        return Assemblies.FirstOrDefault(assembly =>
                   string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
               ?? LoadFromPackage(assemblyName)
               ?? throw new FileNotFoundException($"Assembly {assemblyName} was not found in the mod package.");
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var sharedAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
        if (sharedAssembly is not null)
        {
            return sharedAssembly;
        }

        return assemblyName.Name is null ? null : LoadFromPackage(assemblyName.Name);
    }

    private Assembly? LoadFromPackage(string assemblyName)
    {
        if (!assemblies.TryGetValue(assemblyName, out var bytes))
        {
            return null;
        }

        using var stream = new MemoryStream(bytes, writable: false);
        return LoadFromStream(stream);
    }
}
