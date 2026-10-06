using Engine.Core;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class FurniturePackageTest
{
    [Theory]
    [InlineData(-2)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void DesignChainRejectsInvalidLinkIndex(int index)
    {
        var design = new Game.FurnitureDesign(null) { Index = 7 };
        design.SetValues(2, new int[8]);
        var node = design.Save();
        node.SetValue("NetworkIndex", 7);
        node.SetValue("LinkedDesign", index);
        var values = new EntitySystem.TemplatesDatabase.ValuesDictionary();
        values.SetValue("0", node);
        var package = new FurniturePackage { AddXml = CommonLib.SerializeVDict(values) };

        var error = Assert.Throws<InvalidOperationException>(() => package.ReadDesignChain(null));

        Assert.Equal("Invalid furniture design link index.", error.Message);
    }

    [Fact]
    public void DesignChainPreservesIdsAndCircularLinks()
    {
        var first = new Game.FurnitureDesign(null) { Index = 7 };
        var second = new Game.FurnitureDesign(null) { Index = 12 };
        first.SetValues(2, new int[8]);
        second.SetValues(2, new int[8]);
        first.LinkedDesign = second;
        second.LinkedDesign = first;
        var package = new FurniturePackage(first);
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new FurniturePackage();
        clone.ReadData(reader);

        var chain = clone.ReadDesignChain(null);

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
    public void CreationRequestsAndAuthoritativeDefinitionsHaveDistinctDirections(FurniturePackage.EventType type,
        bool server, bool client)
    {
        Assert.Equal(server, FurniturePackageHandler.AcceptsDirection(type, true));
        Assert.Equal(client, FurniturePackageHandler.AcceptsDirection(type, false));
    }

    [Fact]
    public void DesignDefinitionDoesNotCarryWorldMutationCoordinates()
    {
        var package = new FurniturePackage
        {
            PackageEventType = FurniturePackage.EventType.Add,
            FurnitureIndex = 7,
            AddXml = "definition",
            StartValue = 123
        };
        package.PointDict.Add(new Point3(1, 2, 3), 123);
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new FurniturePackage();

        clone.ReadData(reader);

        Assert.Equal(7, clone.FurnitureIndex);
        Assert.Equal("definition", clone.AddXml);
        Assert.Empty(clone.PointDict);
        Assert.Equal(0, clone.StartValue);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(clone));
    }

    [Fact]
    public void RenamePreservesDistinctOldAndNewNames()
    {
        var package = new FurniturePackage("old", "new");
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new FurniturePackage();

        clone.ReadData(reader);

        Assert.Equal("old", clone.AddXml);
        Assert.Equal("new", clone.FromName);
    }

    [Fact]
    public void ImportPreservesDisconnectedChainsWithUnassignedIds()
    {
        var first = new Game.FurnitureDesign(null) { Index = -1 };
        var second = new Game.FurnitureDesign(null) { Index = -1 };
        var third = new Game.FurnitureDesign(null) { Index = -1 };
        foreach (var design in new[] { first, second, third })
        {
            design.SetValues(2, new int[8]);
        }

        first.LinkedDesign = second;
        second.LinkedDesign = first;
        var package = new FurniturePackage([first, second, third], "imported");
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new FurniturePackage();
        clone.ReadData(reader);

        var designs = clone.ReadDesignChain(null);

        Assert.Equal("imported", clone.FromName);
        Assert.Equal(3, designs.Count);
        Assert.Same(designs[1], designs[0].LinkedDesign);
        Assert.Same(designs[0], designs[1].LinkedDesign);
        Assert.Null(designs[2].LinkedDesign);
        Assert.Equal(2, Game.FurnitureDesign.ListChains(designs).Count);
    }
}
