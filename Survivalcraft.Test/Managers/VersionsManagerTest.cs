using System.Reflection;

using Content.Packaging;

using Game.Managers;

namespace Survivalcraft.Test.Managers;

public sealed class VersionsManagerTest
{
    [Fact]
    public void ProductVersionAndProtocolComeFromTheGameAssembly()
    {
        var assembly = typeof(VersionsManager).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.Equal(informational, VersionsManager.BuildVersion);
        Assert.Equal(informational.Split('+', 2)[0], VersionsManager.Version);
        Assert.True(SemanticVersion.TryParse(VersionsManager.Version, out _));
        Assert.Equal($"{VersionsManager.GameName}-{VersionsManager.Version}", VersionsManager.Title);
        Assert.Equal(int.Parse(assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "GameProtocolVersion").Value!), VersionsManager.ProtocolVersion);
        Assert.True(VersionsManager.ProtocolVersion > 0);
    }
}
