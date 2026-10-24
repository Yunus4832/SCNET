namespace Survivalcraft.Test.Network;

public sealed class EntityPackageDecodingTest
{
    [Theory]
    [InlineData(EntityPackage.EventType.LoadOne)]
    [InlineData(EntityPackage.EventType.LoadList)]
    public void DecodingRetainsPayloadWithoutLoadingWorldComponents(EntityPackage.EventType type)
    {
        byte[] payload = [1, 2, 3, 4];
        using var writer = new ProtocolWriter();
        writer.WriteEnum(type);
        writer.Write(0);
        writer.Write((ushort)0);
        writer.WriteBuff(payload);
        using var reader = new ProtocolReader(writer.Data());
        var package = (EntityPackage)ProtocolMessageCodec.Read((byte)PackageType.Entity, reader);

        Assert.Equal(type, package.Type);
        Assert.Equal(payload, package.EntityData);
        Assert.Empty(package.CharacterEntities);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }
}
