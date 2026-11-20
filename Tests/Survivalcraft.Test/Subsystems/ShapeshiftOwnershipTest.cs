using System.Reflection;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class ShapeshiftOwnershipTest
{
    [Fact]
    public void ClientCannotStartItsOwnTransformationAndReplaysTheAuthorityStartOnce()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var spawn = new ComponentSpawn();
        var shapeshifter = new ComponentShapeshifter { IsEnabled = true };
        project.InitializeComponent(shapeshifter);
        shapeshifter.Entity.EntityId = 42;
        Set(shapeshifter, "_componentSpawn", spawn);
        Register(project, shapeshifter);

        // No simulation dependencies are supplied: replica updates must not evaluate the moon or random timer.
        shapeshifter.Update(100);
        shapeshifter.ShapeshiftTo("Werewolf");
        Assert.False(spawn.IsDespawning);

        var runtime = WorldRuntimeContext.For(project);
        var handler = new ComponentBehaviorPackageHandler();
        handler.Handle(
            new CreatureShapeshiftStatePackage { EntityId = 42, StartTime = 123 },
            new PackageReceiveContext(Game.Network.WorldNetworkBinding.For(runtime).Node, runtime, null)
        );
        handler.Handle(
            new CreatureShapeshiftStatePackage { EntityId = 42, StartTime = 124 },
            new PackageReceiveContext(Game.Network.WorldNetworkBinding.For(runtime).Node, runtime, null)
        );
        Assert.Equal(123d, spawn.DespawnTime);
        Assert.Equal(3f, spawn.DespawnDuration);
    }

    [Fact]
    public void AuthorityStartsAndSnapshotsTransformationWithoutPresentationResources()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        project.AddSubsystem(new SubsystemNetworkInterest());
        var info = new SubsystemGameInfo
        {
            TotalElapsedGameTime = 123, WorldSettings = new WorldSettings()
        };
        var spawn = new ComponentSpawn();
        Set(spawn, "_subsystemGameInfo", info);
        var shapeshifter = new ComponentShapeshifter();
        project.InitializeComponent(shapeshifter);
        Set(shapeshifter, "_componentSpawn", spawn);
        Set(shapeshifter, "_subsystemGameInfo", info);
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(shapeshifter.Entity)!).Add(new ComponentBody());

        shapeshifter.ShapeshiftTo("Werewolf");
        shapeshifter.Update(.1f);
        Assert.Equal(123d, spawn.DespawnTime);
        var values = new ValuesDictionary();
        shapeshifter.Save(values, null!);
        Assert.Equal("Werewolf", values.GetValue<string>("SpawnEntityTemplateName"));
        Assert.Equal(123d, values.GetValue<double>("ShapeshiftStartTime"));
    }

    private static void Register(RuntimeTestProject project, Component component)
    {
        ((List<Component>)typeof(Entity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(component.Entity)!).Add(component);
        ((Dictionary<Entity, bool>)typeof(Project).GetField(
            "entityDictionary",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(project)!).Add(component.Entity, true);
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
