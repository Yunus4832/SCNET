using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class EntityPackageDecodingTest
{
    [Theory]
    [InlineData(EntityPackage.EventType.LoadOne)]
    [InlineData(EntityPackage.EventType.LoadList)]
    public void DecodingRetainsPayloadWithoutLoadingWorldComponents(EntityPackage.EventType type)
    {
        byte[] payload = [1, 2, 3, 4];
        using var writer = new PackageStreamWriter();
        writer.WriteEnum(type);
        writer.Write(0);
        writer.WriteBuff(payload);
        using var reader = new PackageStreamReader(writer.Data());
        var package = new EntityPackage();

        package.ReadData(reader);

        Assert.Equal(type, package.Type);
        Assert.Equal(payload, package.EntityData);
        Assert.Empty(package.Entities);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }
}
