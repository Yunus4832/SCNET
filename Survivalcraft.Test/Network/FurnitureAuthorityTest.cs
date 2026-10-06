using Game;
using Game.Components;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class FurnitureAuthorityTest : IDisposable
{
    private readonly WorkType _previousWorkType = CommonLib.WorkType;

    [Fact]
    public void ClientCannotAllocateDesignFromLocalPreview()
    {
        CommonLib.WorkType = WorkType.Client;
        var behavior = new SubsystemFurnitureBlockBehavior();
        var design = new FurnitureDesign(null) { Index = -1 };
        design.SetValues(2, new int[8]);

        Assert.Null(behavior.TryAddDesignChain(design, true));
        Assert.Equal(-1, design.Index);
        Assert.All(behavior.FurnitureDesigns, Assert.Null);
    }

    [Fact]
    public void ClientCanReuseInstalledAuthoritativeDesign()
    {
        CommonLib.WorkType = WorkType.Client;
        var behavior = new SubsystemFurnitureBlockBehavior();
        var authoritative = new FurnitureDesign(null) { Index = 7 };
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
        CommonLib.WorkType = WorkType.Client;
        var table = new ComponentCraftingTable();

        table.UpdateCraftingResult();
    }

    public void Dispose()
    {
        CommonLib.WorkType = _previousWorkType;
    }
}
