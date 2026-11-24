using System.Text.Json;

using Game.Content;
using Game.Modding;
using Game.World;

namespace Survivalcraft.Test.Modding;

/// <summary>验证精确包配置的作用域投影与跨端依赖。</summary>
public sealed class ModLoadPlanTest : IDisposable
{
    private readonly DirectoryInfo _repository = Directory.CreateTempSubdirectory("scnet-mod-plan-");

    [Fact]
    public void WorldKeepsAllExactPackagesAndServerAdvertisesOnlyClientPackages()
    {
        var shared = Import("test.shared", "both", new ModDependency("game"));
        var client = Import("test.client", "client", new ModDependency("test.shared"));
        var server = Import("test.server", "server", new ModDependency("test.shared"));
        _ = Import("test.unselected", "client");
        var full = new ModProfile { Packages = [client, server, shared] };

        var plan = ModLoadPlan.Read(full, _repository.FullName);

        Assert.Equal([client.PackageHash, server.PackageHash, shared.PackageHash], Hashes(plan.FullProfile));
        Assert.Equal([shared.PackageHash, client.PackageHash], Hashes(plan.ClientProfile));
        Assert.Equal([shared.PackageHash, server.PackageHash], Hashes(plan.AuthorityProfile));
        Assert.Equal(2, plan.GetPackages(RuntimeSide.Client).Count);
        Assert.Equal(2, plan.GetPackages(RuntimeSide.Authority).Count);

        var remote = ModLoadPlan.ReadClientProfile(plan.ClientProfile, _repository.FullName);
        Assert.True(ModProfileIdentity.AreEquivalent(remote.FullProfile, plan.ClientProfile));
        Assert.Throws<InvalidDataException>(() => ModLoadPlan.ReadClientProfile(full, _repository.FullName));
        Assert.NotEqual(ModProfileFingerprint.Compute(full), ModProfileFingerprint.Compute(plan.ClientProfile));
    }

    [Theory]
    [InlineData("client", "server", "Client", false)]
    [InlineData("server", "client", "Server", false)]
    [InlineData("both", "server", "Client", false)]
    [InlineData("both", "client", "Server", false)]
    [InlineData("client", "server", "Client", true)]
    [InlineData("server", "client", "Server", true)]
    public void DependenciesAreResolvedAgainstPackagesOnTheSameSide(
        string ownerScope,
        string dependencyScope,
        string failingSide,
        bool optional
    )
    {
        var dependency = Import("test.dependency", dependencyScope);
        var owner = Import("test.owner", ownerScope, new ModDependency(dependency.ModId, Optional: optional));
        var profile = new ModProfile { Packages = [owner, dependency] };

        if (optional)
        {
            Assert.Equal(2, ModLoadPlan.Read(profile, _repository.FullName).FullProfile.Packages.Count);
        }
        else
        {
            var error = Assert.Throws<ModDependencyException>(() => ModLoadPlan.Read(profile, _repository.FullName));
            Assert.Contains(failingSide, error.Message);
            Assert.Contains(dependency.ModId, error.Message);
        }
    }

    public void Dispose() => _repository.Delete(true);

    private ModPackageRequirement Import(string identifier, string scope, params ModDependency[] dependencies)
    {
        var manifest = JsonSerializer.Serialize(
            new
            {
                id = identifier,
                name = identifier,
                version = "1.0.0",
                scope,
                entrypoint = scope == "server" ? "Test.Entry, TestMod" : null,
                canRemoveFromWorld = true,
                dependencies
            }
        );
        using var package = ScpkgTestPackage.Create(
            manifest,
            new Dictionary<string, string> { [$"assets/{identifier}/Scope.txt"] = scope }
        );
        var entry = new ContentPackageCache(_repository.FullName).ImportAsync(package).GetAwaiter().GetResult();
        return new ModPackageRequirement
        {
            ModId = entry.Identifier,
            Version = entry.Version,
            PackageHash = entry.PackageHash
        };
    }

    private static IEnumerable<string> Hashes(ModProfile profile) =>
        profile.Packages.Select(package => package.PackageHash);
}
