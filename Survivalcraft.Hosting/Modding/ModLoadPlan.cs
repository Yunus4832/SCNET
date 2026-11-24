using Content.Packaging;

namespace Game.Modding;

/// <summary>从完整的精确包配置建立并验证两端各自的加载计划。</summary>
public sealed class ModLoadPlan
{
    private readonly IReadOnlyList<ModPackageSource> _clientPackages;

    private readonly IReadOnlyList<ModPackageSource> _authorityPackages;

    private ModLoadPlan(ModProfile fullProfile, IReadOnlyList<Package> packages)
    {
        FullProfile = fullProfile;
        var client = Project(packages, ModScope.Client);
        var authority = Project(packages, ModScope.Server);
        ClientProfile = CreateProfile(fullProfile.Id, client);
        AuthorityProfile = CreateProfile(fullProfile.Id, authority);
        _clientPackages = client.Select(package => package.Source).ToArray();
        _authorityPackages = authority.Select(package => package.Source).ToArray();
    }

    public ModProfile FullProfile { get; }

    public ModProfile ClientProfile { get; }

    public ModProfile AuthorityProfile { get; }

    public IReadOnlyList<ModPackageSource> GetPackages(RuntimeSide side)
    {
        return side switch
        {
            RuntimeSide.Client => _clientPackages,
            RuntimeSide.Authority => _authorityPackages,
            _ => throw new ArgumentOutOfRangeException(nameof(side))
        };
    }

    /// <summary>校验缓存包的内容哈希并验证作用域和两端的依赖关系。</summary>
    public static ModLoadPlan Read(ModProfile profile, string directory, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ModProfileValidation.Validate(profile);
        var fullProfile = ModProfileManager.CreateSessionProfile(profile.Id, profile);
        var cache = new ContentPackageCache(directory);
        var packages = new List<Package>(fullProfile.Packages.Count);
        foreach (var requirement in fullProfile.Packages)
        {
            var hash = requirement.PackageHash;
            var fileName = hash + ModPackage.FileExtension;
            var manifest = ReadManifest(requirement, cache);
            log?.Invoke($"Verified local Mod {manifest.Id}@{manifest.Version} ({hash}).");
            packages.Add(
                new Package(requirement, manifest, new ModPackageSource(fileName, () => cache.OpenValidated(hash)))
            );
        }

        return new ModLoadPlan(fullProfile, packages);
    }

    /// <summary>读取服务器声明的精确客户端配置，拒绝只在权威端运行的包。</summary>
    public static ModLoadPlan ReadClientProfile(ModProfile profile, string directory, Action<string>? log = null)
    {
        var plan = Read(profile, directory, log);
        if (!ModProfileIdentity.AreEquivalent(plan.FullProfile, plan.ClientProfile))
        {
            throw new InvalidDataException("The remote client mod profile contains a server-only package.");
        }

        return plan;
    }

    private static ModManifest ReadManifest(ModPackageRequirement requirement, ContentPackageCache cache)
    {
        try
        {
            using var stream = cache.OpenValidated(requirement.PackageHash, out var inspection);
            var manifest = inspection.Manifest;
            if (manifest.Type == ContentPackageType.Mod &&
                string.Equals(manifest.Identifier, requirement.ModId, StringComparison.Ordinal) &&
                string.Equals(manifest.Version, requirement.Version, StringComparison.Ordinal))
            {
                return ModPackage.CreateRuntimeManifest(manifest);
            }
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileNotFoundException(MissingPackageMessage(requirement), exception);
        }

        throw new FileNotFoundException(MissingPackageMessage(requirement));
    }

    private static string MissingPackageMessage(ModPackageRequirement requirement)
    {
        return
            $"Required local Mod {requirement.ModId}@{requirement.Version} ({requirement.PackageHash}) is unavailable.";
    }

    private static IReadOnlyList<Package> Project(IEnumerable<Package> packages, ModScope side)
    {
        var contributions = packages
            .Where(package => package.Manifest.Scope == side || package.Manifest.Scope == ModScope.Both)
            .Select(package => (Package: (Package?)package, package.Manifest))
            .Prepend((Package: (Package?)null, Manifest: BuiltInContentMod.Manifest));
        try
        {
            return ModDependencyResolver.Resolve(contributions, contribution => contribution.Manifest)
                .Where(contribution => contribution.Package is not null)
                .Select(contribution => contribution.Package!)
                .ToArray();
        }
        catch (ModDependencyException error)
        {
            throw new ModDependencyException($"{side} mod profile: {error.Message}");
        }
    }

    private static ModProfile CreateProfile(string id, IReadOnlyList<Package> packages)
    {
        return ModProfileManager.CreateSessionProfile(
            id,
            new ModProfile { Packages = packages.Select(package => package.Requirement).ToList() }
        );
    }

    private sealed record Package(ModPackageRequirement Requirement, ModManifest Manifest, ModPackageSource Source);
}
