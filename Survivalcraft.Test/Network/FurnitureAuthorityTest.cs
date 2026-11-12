using Game.Blocks;
using Game.Components;
using Game.Furniture;
using Game.Network;
using Game.Network.Handling;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class FurnitureAuthorityTest
{
    [Fact]
    public void ClientCannotAllocateDesignFromLocalPreview()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var behavior = new SubsystemFurnitureBlockBehavior();
        project.InitializeSubsystem(behavior);
        var design = new FurnitureDesign(null, null) { Index = -1 };
        design.SetValues(2, new int[8]);

        Assert.Null(behavior.TryAddDesignChain(design, true));
        Assert.Equal(-1, design.Index);
        Assert.All(behavior.FurnitureDesigns, Assert.Null);
    }

    [Fact]
    public void ClientCanReuseInstalledAuthoritativeDesign()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var behavior = new SubsystemFurnitureBlockBehavior();
        project.InitializeSubsystem(behavior);
        var authoritative = new FurnitureDesign(null, null) { Index = 7 };
        authoritative.SetValues(2, new int[8]);
        behavior.InstallNetworkDesign(authoritative);
        var preview = authoritative.Clone();

        Assert.Same(authoritative, behavior.TryAddDesignChain(preview, true));
        Assert.Same(authoritative, behavior.GetDesign(7));
        Assert.Single(behavior.FurnitureDesigns.OfType<FurnitureDesign>());
    }

    [Fact]
    public void ClientDoesNotCalculateCraftingResultFromPartialInventory()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var table = new ComponentCraftingTable();
        project.InitializeComponent(table);

        table.UpdateCraftingResult();
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);
        Assert.Null(new FurnitureBlock().GetAdHocCraftingRecipe(terrain, ["invalid:input"], 0f, null));
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, NodeRole.Client, true)]
    [InlineData(RuntimeSide.Client, NodeRole.Authority, false)]
    public void FurniturePaintingUsesItsTerrainWorldInsteadOfTheGlobalRole(
        RuntimeSide side,
        NodeRole applicationRole,
        bool shouldPaint
    )
    {
        var previousRole = CommonLib.Net.Role;
        try
        {
            CommonLib.Net.Role = applicationRole;
            var node = new NetNode(
                NetNodeOptions.Default,
                true,
                PackageHandlerCatalog.Empty,
                side == RuntimeSide.Authority ? NodeRole.Authority : NodeRole.Client
            );
            using var scope = new WorldCatalogScope(node);
            var terrain = new SubsystemTerrain();
            var behavior = new SubsystemFurnitureBlockBehavior();
            scope.Project.InitializeSubsystem(terrain);
            scope.Project.InitializeSubsystem(behavior);
            terrain.SubsystemFurnitureBlockBehavior = behavior;
            var blockIndex = Array.FindIndex(scope.Catalog.Blocks, block => block is PaintedCubeBlock);
            Assert.True(blockIndex > 0);
            var design = new FurnitureDesign(scope.Catalog, terrain) { Index = 7 };
            design.SetValues(2, Enumerable.Repeat(Terrain.MakeBlockValue(blockIndex), 8).ToArray());
            behavior.InstallNetworkDesign(design);
            var value = Terrain.MakeBlockValue(227, FurnitureBlock.SetDesignIndex(0, design.Index, 0, false));

            var painted = new FurnitureBlock().Paint(terrain, value, 3);

            Assert.Equal(shouldPaint, painted != value);
            Assert.Equal(shouldPaint ? 1 : 0, node.PendingPackageCount);
            Assert.Equal(shouldPaint ? 2 : 1, behavior.FurnitureDesigns.OfType<FurnitureDesign>().Count());
            Assert.All(Enumerable.Range(0, 8), index => Assert.Equal(0, Terrain.ExtractData(design.GetValue(index))));
        }
        finally { CommonLib.Net.Role = previousRole; }
    }
}
