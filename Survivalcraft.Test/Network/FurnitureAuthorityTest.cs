using Game.Components;
using Game.Furniture;
using Game.Managers;
using Game.Network;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class FurnitureAuthorityTest : IDisposable
{
    private readonly WorldRole _previousWorldRole = GameManager.Role;

    [Fact]
    public void ClientCannotAllocateDesignFromLocalPreview()
    {
        CommonLib.Net.Role = WorldRole.RemoteClient;
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
        CommonLib.Net.Role = WorldRole.RemoteClient;
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
        CommonLib.Net.Role = WorldRole.RemoteClient;
        var table = new ComponentCraftingTable();

        table.UpdateCraftingResult();
    }

    public void Dispose()
    {
        CommonLib.Net.Role = _previousWorldRole;
    }
}
