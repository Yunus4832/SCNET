using System.Reflection;

using EntitySystem.TemplatesDatabase;

using Game.Components;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class AuthoritySpawnSchedulingTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority, true)]
    [InlineData(RuntimeSide.Client, false)]
    public void SpawnAndLootSimulationAreScheduledOnlyByAuthority(RuntimeSide side, bool expected)
    {
        using var project = new RuntimeTestProject(side);
        var runtime = WorldRuntimeContext.For(project);
        foreach (var updateable in new IUpdateable[]
                     { new SubsystemSpawn(), new SubsystemCreatureSpawn(), new ComponentLoot(), new ComponentSpawn() })
        {
            Assert.Equal(expected, SubsystemUpdate.ShouldScheduleUpdateable(updateable, runtime));
        }
    }

    [Fact]
    public void ClientSpawnAndLootUpdatesDoNotReadAuthorityDependencies()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var spawn = new SubsystemSpawn();
        project.InitializeSubsystem(spawn);
        spawn.Update(.1f);
        var loot = new ComponentLoot();
        project.InitializeComponent(loot);
        loot.Update(.1f);
        var values = new ValuesDictionary();
        loot.Save(values, null!);
        Assert.False(values.GetValue<bool>("LootDropped"));
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, true)]
    [InlineData(RuntimeSide.Client, false)]
    public void ReplicaDespawnWaitsForAuthoritativeEntityRemoval(RuntimeSide side, bool removed)
    {
        using var project = new RuntimeTestProject(side);
        var spawn = new ComponentSpawn { DespawnTime = 0, DespawnDuration = 0 };
        project.InitializeComponent(spawn);
        Set(spawn, "_subsystemGameInfo", new SubsystemGameInfo { TotalElapsedGameTime = 1 });
        project.AddEntity(spawn.Entity);
        var notifications = 0;
        spawn.Despawned += _ => notifications++;

        spawn.Update(.1f);

        Assert.Equal(removed, !spawn.Entity.IsAddedToProject);
        Assert.Equal(removed ? 1 : 0, notifications);
        if (!removed)
        {
            project.RemoveEntity(spawn.Entity, true);
            Assert.False(spawn.Entity.IsAddedToProject);
            Assert.Equal(0, notifications);
        }
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, true)]
    [InlineData(RuntimeSide.Client, false)]
    public void OnlyAuthorityCommitsTheLootDroppedFlag(RuntimeSide side, bool expected)
    {
        using var project = new RuntimeTestProject(side);
        var creature = new ComponentCreature
        {
            ComponentHealth = new ComponentHealth { DeathTime = 0, CorpseDuration = 0 }
        };
        project.InitializeComponent(creature);
        var loot = new ComponentLoot();
        project.InitializeComponent(loot);
        Set(loot, "_componentCreature", creature);
        Set(loot, "_subsystemGameInfo", new SubsystemGameInfo { TotalElapsedGameTime = 1 });

        loot.Update(.1f);
        loot.Update(.1f);

        var values = new ValuesDictionary();
        loot.Save(values, null!);
        Assert.Equal(expected, values.GetValue<bool>("LootDropped"));
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
