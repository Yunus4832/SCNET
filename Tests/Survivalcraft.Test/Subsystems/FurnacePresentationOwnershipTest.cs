using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Modding.Blocks;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FurnacePresentationOwnershipTest
{
    [Fact]
    public void AuthorityLoadsAndChangesLitFurnacesWithoutAccessingGraphics()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var behavior = new SubsystemFurnaceBlockBehavior();
        project.AddSubsystem(new SubsystemTerrain());
        project.AddSubsystem(new SubsystemBlockEntities());
        project.AddSubsystem(behavior);
        behavior.Load(new ValuesDictionary());
        var torches = new SubsystemTorchBlockBehavior();
        project.AddSubsystem(torches);
        torches.Load(new ValuesDictionary());
        var lit = BuiltInBlockRegistry.GetIndex<LitFurnaceBlock>();

        // No content manager, graphics device, or particle subsystem is initialized.
        behavior.OnBlockGenerated(lit, 1, 65, 2, true);
        behavior.OnBlockAdded(lit, 0, 1, 65, 2);
        behavior.OnBlockRemoved(lit, lit, 1, 65, 2);
    }
}
