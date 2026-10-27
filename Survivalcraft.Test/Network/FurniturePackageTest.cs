using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class FurniturePackageTest
{
    [Theory]
    [InlineData(-2)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void DesignChainRejectsInvalidLinkIndex(int index)
    {
        var design = new Game.Furniture.FurnitureDesign(null) { Index = 7 };
        design.SetValues(2, new int[8]);
        var node = design.Save();
        node.SetValue("NetworkIndex", 7);
        node.SetValue("LinkedDesign", index);
        var values = new EntitySystem.TemplatesDatabase.ValuesDictionary();
        values.SetValue("0", node);
        var package = new FurnitureDesignAddRequestPackage
            { DesignXml = CommonLib.SerializeVDict(values) };

        var error = Assert.Throws<InvalidOperationException>(() =>
            FurnitureSnapshotSerializer.ReadDesignChain(package.DesignXml, null)
        );

        Assert.Equal("Invalid furniture design link index.", error.Message);
    }

    [Fact]
    public void DesignChainPreservesIdsAndCircularLinks()
    {
        var first = new Game.Furniture.FurnitureDesign(null) { Index = 7 };
        var second = new Game.Furniture.FurnitureDesign(null) { Index = 12 };
        first.SetValues(2, new int[8]);
        second.SetValues(2, new int[8]);
        first.LinkedDesign = second;
        second.LinkedDesign = first;
        var package = FurniturePackageBuilder.Capture(first);
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<FurnitureDesignChainPackage>(ProtocolMessageCodec.Decode(reader));

        var chain = FurnitureSnapshotSerializer.ReadDesignChain(clone.DesignXml, null);

        Assert.Equal(2, chain.Count);
        Assert.Equal(7, chain[0].Index);
        Assert.Equal(12, chain[1].Index);
        Assert.Same(chain[1], chain[0].LinkedDesign);
        Assert.Same(chain[0], chain[1].LinkedDesign);
    }

    [Theory]
    [InlineData(FurniturePackage.EventType.Add, false, true)]
    [InlineData(FurniturePackage.EventType.RemoveFurnitureDesigns, false, true)]
    [InlineData(FurniturePackage.EventType.RequestAdd, true, false)]
    [InlineData(FurniturePackage.EventType.ImportFurnitureSet, true, false)]
    [InlineData(FurniturePackage.EventType.DesignChain, false, true)]
    public void CreationRequestsAndAuthoritativeDefinitionsHaveDistinctDirections(
        FurniturePackage.EventType type,
        bool server,
        bool client
    )
    {
        Assert.Equal(server, FurniturePackageHandler.AcceptsDirection(type, true));
        Assert.Equal(client, FurniturePackageHandler.AcceptsDirection(type, false));
    }

    [Fact]
    public void DesignDefinitionDoesNotCarryWorldMutationCoordinates()
    {
        var package = new FurnitureDesignAddedPackage
            { FurnitureIndex = 7, DesignXml = "definition" };
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<FurnitureDesignAddedPackage>(ProtocolMessageCodec.Decode(reader));

        Assert.Equal(7, clone.FurnitureIndex);
        Assert.Equal("definition", clone.DesignXml);
        Assert.Null(typeof(FurnitureDesignAddedPackage).GetProperty("PointDict"));
        Assert.Null(typeof(FurnitureDesignAddedPackage).GetProperty("StartValue"));

        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(clone));
    }

    [Fact]
    public void RenamePreservesDistinctOldAndNewNames()
    {
        var package = new FurnitureSetRenamedPackage { OldName = "old", NewName = "new" };
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<FurnitureSetRenamedPackage>(ProtocolMessageCodec.Decode(reader));

        Assert.Equal("old", clone.OldName);
        Assert.Equal("new", clone.NewName);
    }

    [Fact]
    public void ImportPreservesDisconnectedChainsWithUnassignedIds()
    {
        var first = new Game.Furniture.FurnitureDesign(null) { Index = -1 };
        var second = new Game.Furniture.FurnitureDesign(null) { Index = -1 };
        var third = new Game.Furniture.FurnitureDesign(null) { Index = -1 };
        foreach (var design in new[] { first, second, third })
        {
            design.SetValues(2, new int[8]);
        }

        first.LinkedDesign = second;
        second.LinkedDesign = first;
        var package = FurniturePackageBuilder.Capture([first, second, third], "imported");
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = Assert.IsType<FurnitureSetImportPackage>(ProtocolMessageCodec.Decode(reader));

        var designs = FurnitureSnapshotSerializer.ReadDesignChain(clone.DesignXml, null);

        Assert.Equal("imported", clone.Name);
        Assert.Equal(3, designs.Count);
        Assert.Same(designs[1], designs[0].LinkedDesign);
        Assert.Same(designs[0], designs[1].LinkedDesign);
        Assert.Null(designs[2].LinkedDesign);
        Assert.Equal(2, Game.Furniture.FurnitureDesign.ListChains(designs).Count);
    }
}
