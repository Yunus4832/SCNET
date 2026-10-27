using Engine.Core.Mathematics;

using Game.Protocol.Blocks;
using Game.Protocol.Serialization;
using Game.Protocol.Terrain;

namespace Game.Protocol.Test;

public sealed class FurnitureCodecTest
{
    public static IEnumerable<object[]> Modes() =>
        Enum.GetValues<FurniturePackage.EventType>().Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(Modes))]
    public void FurnitureModesCarryOnlyPreparedPayload(FurniturePackage.EventType type)
    {
        var message = (type switch
        {
            FurniturePackage.EventType.RequestAdd => (FurniturePackage)new
                FurnitureDesignAddRequestPackage
                {
                    DesignXml = "opaque game design or set name", FurnitureIndex = 42,
                    CellFace = new CellFace(-3, 64, 2, 1), StartValue = 7,
                    PointDict = new Dictionary<Point3, int> { [new Point3(-3, 64, 2)] = 7 }
                },
            FurniturePackage.EventType.Add => new FurnitureDesignAddedPackage
                { DesignXml = "opaque game design or set name", FurnitureIndex = 42 },
            FurniturePackage.EventType.NewFurnitureSet => new FurnitureSetCreatedPackage
                { Name = "opaque game design or set name", ImportedFrom = "source" },
            FurniturePackage.EventType.DeleteFurnitureSet => new FurnitureSetDeletedPackage
                { Name = "opaque game design or set name" },
            FurniturePackage.EventType.RenameFurnitureSet => new FurnitureSetRenamedPackage
                { OldName = "opaque game design or set name", NewName = "source" },
            FurniturePackage.EventType.MoveFurnitureSet => new FurnitureSetMovePackage
                { SetName = "opaque game design or set name", FurnitureIndex = 42 },
            FurniturePackage.EventType.AddToFurnitureSet => new FurnitureSetAddDesignPackage
                { SetName = "opaque game design or set name", FurnitureIndex = 42 },
            FurniturePackage.EventType.DesignChain => new FurnitureDesignChainPackage
                { DesignXml = "opaque game design or set name" },
            FurniturePackage.EventType.RemoveFurnitureDesigns =>
                new FurnitureDesignsRemovedPackage { ToRemoveList = [4, 5] },
            FurniturePackage.EventType.ImportFurnitureSet => new FurnitureSetImportPackage
                { DesignXml = "opaque game design or set name", Name = "source" },
            _ => throw new InvalidOperationException("Unsupported message operation.")
        });
        Assert.True(ProtocolMessageCodec.IsDefined(message.GetType()));
        var writer = ProtocolMessageCodec.Encode(message);
        var bytes = writer;
        var reader = bytes;
        var restored = Assert.IsAssignableFrom<FurniturePackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(type, restored.PackageEventType);

        var repeated = ProtocolMessageCodec.Encode(restored);
        Assert.Equal(bytes, repeated);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void RemovalCountIsBoundedBeforeAllocation(int count)
    {
        var malformed = WireTestData.ReplaceField(
            new FurnitureDesignsRemovedPackage(),
            0,
            count < 0 ? [0xff] : [0xdd, 0x7f, 0xff, 0xff, 0xff]
        );
        Assert.Throws<InvalidDataException>(() => ProtocolMessageCodec.Decode(malformed));
    }
}
