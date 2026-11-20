namespace Survivalcraft.Test.Network;

public sealed class EntityPackageDecodingTest
{
    [Theory]
    [InlineData(EntityPackage.EventType.LoadOne)]
    [InlineData(EntityPackage.EventType.LoadList)]
    public void DecodingRetainsPayloadWithoutLoadingWorldComponents(EntityPackage.EventType type)
    {
        byte[] payload = [1, 2, 3, 4];
        EntityPackage message = type == EntityPackage.EventType.LoadOne
            ? new SingleEntitySnapshotPackage { EntityData = payload }
            : new EntitySnapshotPackage { EntityData = payload };
        var restored = ProtocolMessageCodec.Decode(ProtocolMessageCodec.Encode(message));
        if (restored is SingleEntitySnapshotPackage single)
        {
            Assert.Equal(payload, single.EntityData);
            Assert.Empty(single.CharacterEntities);
        }
        else
        {
            var batch = Assert.IsType<EntitySnapshotPackage>(restored);
            Assert.Equal(payload, batch.EntityData);
            Assert.Empty(batch.CharacterEntities);
        }
    }
}
