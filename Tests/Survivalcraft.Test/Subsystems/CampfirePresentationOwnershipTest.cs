using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class CampfirePresentationOwnershipTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void CampfireFactsDoNotDependOnParticleOrPresentationAvailability(RuntimeSide side)
    {
        using var project = new RuntimeTestProject(side);
        var rules = new SubsystemCampfireBlockBehavior();
        project.InitializeSubsystem(rules);
        var point = new Point3(1, 64, 1);
        rules.OnBlockGenerated(Terrain.MakeBlockValue(209, 5), point.X, point.Y, point.Z, true);
        Assert.Equal(point, Assert.Single(rules.Campfires));
        rules.OnBlockModified(Terrain.MakeBlockValue(209, 8), 0, point.X, point.Y, point.Z);
        Assert.Equal(point, Assert.Single(rules.Campfires));
        rules.OnBlockRemoved(0, 0, point.X, point.Y, point.Z);
        rules.OnBlockRemoved(0, 0, point.X, point.Y, point.Z);
        Assert.Empty(rules.Campfires);
    }

    [Fact]
    public void SizeAndRemovalFactsCoalesceBeforeClientResourceCreationAndDisposeUnsubscribes()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var rules = new SubsystemCampfireBlockBehavior();
        project.AddSubsystem(rules);
        project.AddSubsystem(new SubsystemParticles());
        project.AddSubsystem(new SubsystemAmbientSounds());
        project.AddSubsystem(new SubsystemTerrain());
        var view = new SubsystemCampfirePresentation();
        project.AddSubsystem(view);
        view.Load(new ValuesDictionary());
        var point = new Point3(1, 64, 1);
        rules.OnBlockAdded(Terrain.MakeBlockValue(209, 4), 0, point.X, point.Y, point.Z);
        rules.OnBlockModified(Terrain.MakeBlockValue(209, 7), 0, point.X, point.Y, point.Z);
        var pending = (IDictionary)typeof(SubsystemCampfirePresentation).GetField(
            "_pending",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(view)!;
        Assert.Equal(7, Terrain.ExtractData((int)pending[point]!));
        rules.OnBlockRemoved(0, 0, point.X, point.Y, point.Z);
        Assert.Equal(0, pending[point]);
        view.Dispose();
        view.Dispose();
        rules.OnBlockAdded(Terrain.MakeBlockValue(209, 3), 0, point.X, point.Y, point.Z);
        Assert.Empty(pending);
    }
}
